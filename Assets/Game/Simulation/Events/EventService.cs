using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Events;
using Game.Simulation.Tiers;

namespace Game.Simulation.Events
{
    public interface IEventTargetSource
    {
        IEnumerable<EventTargetSet> GetTargets(EventDefinition definition);
        bool Evaluate(EventConditionDefinition condition, IReadOnlyList<EventTargetRef> targets);
        bool IsValid(EventTargetRef target);
        string GetDisplayName(EventTargetRef target);
    }

    public interface IEventEffectExecutor
    {
        bool Supports(EventEffectKind kind);
        bool CanApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets);
        bool TryApply(EventEffectDefinition effect, IReadOnlyList<EventTargetRef> targets,
            long calendarTick, out string outcomeCode);
    }

    public enum EventChoiceCommandStatus
    {
        Applied = 1,
        InstanceNotFound = 2,
        NotPending = 3,
        NotPlayerOwned = 4,
        ChoiceNotFound = 5,
        TargetInvalid = 6,
        EffectRejected = 7
    }

    public readonly struct EventChoiceCommandResult
    {
        public EventChoiceCommandResult(EventChoiceCommandStatus status, EventInstanceState instance)
        {
            Status = status; Instance = instance;
        }
        public EventChoiceCommandStatus Status { get; }
        public EventInstanceState Instance { get; }
        public bool Applied => Status == EventChoiceCommandStatus.Applied;
    }

    public readonly struct EventAdvanceResult
    {
        public EventAdvanceResult(int offered, int resolved, int expired, ulong stateHash)
        {
            Offered = offered; Resolved = resolved; Expired = expired; StateHash = stateHash;
        }
        public int Offered { get; }
        public int Resolved { get; }
        public int Expired { get; }
        public ulong StateHash { get; }
    }

    /// <summary>
    /// One engine-independent event scheduler. Definitions and targets are evaluated in stable order;
    /// random samples are counter-keyed and never consume a shared cursor.
    /// </summary>
    public sealed class EventService
    {
        public const int DefaultHistoryLimit = 128;
        public const int DefaultImmediateTransitionBudget = 32;
        private const uint OfferSelectionStream = 0x55323801u;
        private const uint TransitionSelectionStream = 0x55323802u;
        private readonly EventCatalog catalog;
        private readonly IEventTargetSource targets;
        private readonly IEventEffectExecutor effects;
        private readonly long worldSeed;
        private readonly int noEventWeight;
        private readonly int historyLimit;
        private readonly int immediateTransitionBudget;
        private readonly Dictionary<EventInstanceId, EventInstanceState> instances = new Dictionary<EventInstanceId, EventInstanceState>();
        private readonly Dictionary<string, EventCooldownState> cooldowns = new Dictionary<string, EventCooldownState>(StringComparer.Ordinal);
        private readonly List<EventHistoryEntry> history = new List<EventHistoryEntry>();
        private readonly SortedSet<long> checkpointBuffer = new SortedSet<long>();
        private ulong nextRequestSequence;
        private long archivedCount;
        private ulong archivedChecksum;

        public EventService(EventCatalog eventCatalog, IEventTargetSource targetSource,
            IEventEffectExecutor effectExecutor, long simulationWorldSeed, long initialProcessedTick = 0,
            int noEventWeight = 0, int historyLimit = DefaultHistoryLimit,
            int immediateTransitionBudget = DefaultImmediateTransitionBudget)
        {
            catalog = eventCatalog ?? throw new ArgumentNullException(nameof(eventCatalog));
            targets = targetSource ?? throw new ArgumentNullException(nameof(targetSource));
            effects = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor));
            if (initialProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(initialProcessedTick));
            if (noEventWeight < 0) throw new ArgumentOutOfRangeException(nameof(noEventWeight));
            if (historyLimit <= 0) throw new ArgumentOutOfRangeException(nameof(historyLimit));
            if (immediateTransitionBudget <= 0) throw new ArgumentOutOfRangeException(nameof(immediateTransitionBudget));
            worldSeed = simulationWorldSeed; LastProcessedTick = initialProcessedTick;
            this.noEventWeight = noEventWeight; this.historyLimit = historyLimit;
            this.immediateTransitionBudget = immediateTransitionBudget;
            ValidateHandlers();
        }

        public EventCatalog Catalog => catalog;
        public long WorldSeed => worldSeed;
        public long LastProcessedTick { get; private set; }
        public int HistoryLimit => historyLimit;
        public long ArchivedCount => archivedCount;
        public ulong ArchivedChecksum => archivedChecksum;
        public IReadOnlyList<EventInstanceState> ActiveInstances => instances.Values.OrderBy(x => x.InstanceId).ToArray();
        public IReadOnlyList<EventInstanceState> PendingPlayerEvents => instances.Values
            .Where(x => x.Audience == EventAudience.Player && x.Status == EventStatus.PendingChoice)
            .OrderBy(x => x.OfferedTick).ThenBy(x => x.InstanceId).ToArray();
        public IReadOnlyList<EventHistoryEntry> History => history.OrderBy(x => x.ResolvedTick).ThenBy(x => x.InstanceId).ToArray();
        public IReadOnlyList<EventCooldownState> Cooldowns => cooldowns.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();

        public string GetTargetDisplayName(EventTargetRef target) => targets.GetDisplayName(target);

        public EventAdvanceResult AdvanceTo(long calendarTick)
            => AdvanceTo(calendarTick, true);

        public EventAdvanceResult AdvanceTo(long calendarTick, bool includeStateHash)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick), "Event time cannot move backwards.");
            if (calendarTick == LastProcessedTick)
                return new EventAdvanceResult(0, 0, 0, includeStateHash ? ComputeStateHash() : 0UL);
            var offered = 0; var resolved = 0; var expired = 0;
            var checkpoints = BuildCheckpoints(LastProcessedTick, calendarTick);
            while (checkpoints.Count > 0)
            {
                var checkpoint = checkpoints.Min;
                checkpoints.Remove(checkpoint);
                ActivateWaiting(checkpoint, calendarTick, checkpoints, ref resolved, ref expired);
                EvaluateScheduled(checkpoint, ref offered, ref resolved, ref expired, checkpoints, calendarTick);
            }
            LastProcessedTick = calendarTick;
            return new EventAdvanceResult(offered, resolved, expired, includeStateHash ? ComputeStateHash() : 0UL);
        }

        public EventInstanceState Enqueue(EventDefinitionId definitionId, EventTargetSet targetSet, long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var definition = catalog.Get(definitionId);
            if (definition.Trigger.Kind != EventTriggerKind.Explicit)
                throw new InvalidOperationException("Only explicit definitions can be enqueued directly.");
            if (!IsEligible(definition, targetSet, calendarTick))
                throw new InvalidOperationException("The explicit event is not eligible for this target and tick.");
            return Offer(definition, targetSet, calendarTick, nextRequestSequence++, null, calendarTick);
        }

        public IReadOnlyList<EventInstanceState> RaiseHook(CanonicalEventHookKind hookKind, long calendarTick)
        {
            if (!Enum.IsDefined(typeof(CanonicalEventHookKind), hookKind)) throw new ArgumentOutOfRangeException(nameof(hookKind));
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var created = new List<EventInstanceState>();
            foreach (var definition in catalog.All.Where(x => x.Trigger.Kind == EventTriggerKind.CanonicalHook && x.Trigger.HookKind == hookKind))
                foreach (var targetSet in targets.GetTargets(definition).OrderBy(x => x.StableKey, StringComparer.Ordinal))
                    if (IsEligible(definition, targetSet, calendarTick))
                        created.Add(Offer(definition, targetSet, calendarTick, nextRequestSequence++, null, calendarTick));
            return created.AsReadOnly();
        }

        public EventChoiceCommandResult ResolvePlayerChoice(EventInstanceId instanceId, EventChoiceId choiceId,
            long calendarTick)
        {
            if (!instances.TryGetValue(instanceId, out var instance))
                return new EventChoiceCommandResult(EventChoiceCommandStatus.InstanceNotFound, null);
            if (instance.Status != EventStatus.PendingChoice)
                return new EventChoiceCommandResult(EventChoiceCommandStatus.NotPending, instance);
            if (instance.Audience != EventAudience.Player)
                return new EventChoiceCommandResult(EventChoiceCommandStatus.NotPlayerOwned, instance);
            var definition = catalog.Get(instance.DefinitionId);
            var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
            var choice = node.Choices.FirstOrDefault(x => x.Id == choiceId);
            if (choice == null) return new EventChoiceCommandResult(EventChoiceCommandStatus.ChoiceNotFound, instance);
            if (!AreTargetsValid(definition, instance.Targets) &&
                instance.CurrentNodeId != definition.InvalidTargetNodeId)
            {
                HandleInvalidTarget(definition, instance, calendarTick);
                return new EventChoiceCommandResult(EventChoiceCommandStatus.TargetInvalid,
                    instances.TryGetValue(instanceId, out var changed) ? changed : instance);
            }
            if (!CanApply(choice, instance.Targets))
                return new EventChoiceCommandResult(EventChoiceCommandStatus.EffectRejected, instance);
            var budget = immediateTransitionBudget;
            var result = ResolveChoice(definition, instance, choice, calendarTick, ref budget);
            return new EventChoiceCommandResult(EventChoiceCommandStatus.Applied, result);
        }

        public EventSaveState CaptureState() => new EventSaveState(worldSeed, LastProcessedTick, nextRequestSequence,
            ActiveInstances.Select(Clone), Cooldowns.Select(Clone), History.Select(Clone), archivedCount, archivedChecksum);

        public void RestoreState(EventSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.WorldSeed != worldSeed) throw new InvalidOperationException("Event save world seed does not match this session.");
            if (state.LastProcessedTick < 0 || state.History.Count > historyLimit) throw new ArgumentException("Invalid event save bounds.");
            var restoredInstances = new Dictionary<EventInstanceId, EventInstanceState>();
            var restoredScopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in state.Instances)
            {
                var value = Clone(source);
                if (!restoredInstances.TryAdd(value.InstanceId, value)) throw new ArgumentException("Duplicate event instance ID.");
                ValidateInstance(value);
                var definition = catalog.Get(value.DefinitionId);
                if (!restoredScopes.Add(definition.Id + "|" + ScopeKey(definition, value.Targets)))
                    throw new ArgumentException("Duplicate active event definition/scope.");
            }
            var restoredCooldowns = new Dictionary<string, EventCooldownState>(StringComparer.Ordinal);
            foreach (var source in state.Cooldowns)
            {
                var value = Clone(source);
                if (!catalog.TryGet(value.DefinitionId, out var definition)) throw new ArgumentException("Cooldown references a missing definition.");
                if (!value.ScopeKey.StartsWith(definition.OwnerId + "|", StringComparison.Ordinal))
                    throw new ArgumentException("Cooldown scope does not match its definition owner.");
                if (!restoredCooldowns.TryAdd(value.Key, value)) throw new ArgumentException("Duplicate event cooldown key.");
            }
            var restoredHistory = new List<EventHistoryEntry>();
            var historyIds = new HashSet<EventInstanceId>();
            foreach (var source in state.History)
            {
                var value = Clone(source);
                if (!catalog.TryGet(value.DefinitionId, out var definition)) throw new ArgumentException("History references a missing definition.");
                ValidateHistory(value, definition);
                if (!historyIds.Add(value.InstanceId) || restoredInstances.ContainsKey(value.InstanceId))
                    throw new ArgumentException("Duplicate active/history event instance ID.");
                restoredHistory.Add(value);
            }
            instances.Clear(); foreach (var pair in restoredInstances) instances.Add(pair.Key, pair.Value);
            cooldowns.Clear(); foreach (var pair in restoredCooldowns) cooldowns.Add(pair.Key, pair.Value);
            history.Clear(); history.AddRange(restoredHistory.OrderBy(x => x.ResolvedTick).ThenBy(x => x.InstanceId));
            LastProcessedTick = state.LastProcessedTick; nextRequestSequence = state.NextRequestSequence;
            archivedCount = state.ArchivedCount; archivedChecksum = state.ArchivedChecksum;
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, unchecked((ulong)worldSeed)); Add(ref hash, unchecked((ulong)LastProcessedTick));
            Add(ref hash, nextRequestSequence); Add(ref hash, unchecked((ulong)archivedCount)); Add(ref hash, archivedChecksum);
            foreach (var instance in ActiveInstances) AddInstance(ref hash, instance);
            foreach (var cooldown in Cooldowns)
            {
                AddString(ref hash, cooldown.DefinitionId.ToString()); AddString(ref hash, cooldown.ScopeKey);
                Add(ref hash, unchecked((ulong)cooldown.NextEligibleTick));
            }
            foreach (var entry in History)
            {
                AddString(ref hash, entry.InstanceId.ToString()); AddString(ref hash, entry.DefinitionId.ToString());
                Add(ref hash, unchecked((ulong)entry.ResolvedTick)); AddString(ref hash, entry.ChoiceId?.ToString() ?? string.Empty);
                AddString(ref hash, entry.OutcomeCode); Add(ref hash, (ulong)entry.Status);
            }
            return hash;
        }

        private SortedSet<long> BuildCheckpoints(long afterTick, long targetTick)
        {
            var result = checkpointBuffer;
            result.Clear();
            foreach (var definition in catalog.All)
            {
                var trigger = definition.Trigger;
                if (trigger.Kind == EventTriggerKind.AbsoluteTick)
                {
                    if (trigger.FirstTick > afterTick && trigger.FirstTick <= targetTick) result.Add(trigger.FirstTick);
                }
                else if (trigger.Kind == EventTriggerKind.Periodic || trigger.Kind == EventTriggerKind.StatePredicate)
                {
                    if (trigger.IntervalTicks <= 0) continue;
                    var first = trigger.FirstTick;
                    if (first <= afterTick)
                    {
                        var count = checked((afterTick - first) / trigger.IntervalTicks + 1L);
                        first = checked(first + count * trigger.IntervalTicks);
                    }
                    for (var tick = first; tick <= targetTick; tick = checked(tick + trigger.IntervalTicks))
                    {
                        result.Add(tick);
                        if (tick > long.MaxValue - trigger.IntervalTicks) break;
                    }
                }
            }
            foreach (var instance in instances.Values.Where(x => x.Status == EventStatus.Waiting &&
                         x.DueTick > afterTick && x.DueTick <= targetTick)) result.Add(instance.DueTick);
            return result;
        }

        private void EvaluateScheduled(long tick, ref int offered, ref int resolved, ref int expired,
            SortedSet<long> checkpoints, long targetTick)
        {
            var candidates = new List<Candidate>();
            foreach (var definition in catalog.All)
            {
                if (!IsDue(definition.Trigger, tick)) continue;
                foreach (var targetSet in targets.GetTargets(definition).OrderBy(x => x.StableKey, StringComparer.Ordinal))
                    if (IsEligible(definition, targetSet, tick)) candidates.Add(new Candidate(definition, targetSet));
            }
            foreach (var group in candidates.GroupBy(x => x.Definition.OwnerId + "|" + x.Targets.StableKey)
                         .OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var ordered = group.OrderBy(x => x.Definition.Id).ToArray();
                long total = noEventWeight;
                checked { foreach (var candidate in ordered) total += candidate.Definition.Weight; }
                var sample = SampleForSelection(group.Key + "|" + string.Join("|", ordered.Select(x => x.Definition.Id.ToString())),
                    tick, OfferSelectionStream, 0) % unchecked((ulong)total);
                if (sample < unchecked((ulong)noEventWeight)) continue;
                sample -= unchecked((ulong)noEventWeight);
                Candidate selected = default;
                foreach (var candidate in ordered)
                {
                    if (sample < unchecked((ulong)candidate.Definition.Weight)) { selected = candidate; break; }
                    sample -= unchecked((ulong)candidate.Definition.Weight);
                }
                if (selected.Definition == null) continue;
                var occurrence = Occurrence(selected.Definition.Trigger, tick);
                var beforeHistory = history.Count;
                var instance = Offer(selected.Definition, selected.Targets, tick, occurrence, checkpoints, targetTick);
                offered++;
                if (!instances.ContainsKey(instance.InstanceId)) resolved += history.Count > beforeHistory ? 1 : 0;
                if (instance.Status == EventStatus.Expired || instance.Status == EventStatus.Cancelled || instance.Status == EventStatus.Invalid) expired++;
            }
        }

        private void ActivateWaiting(long tick, long targetTick, SortedSet<long> checkpoints,
            ref int resolved, ref int expired)
        {
            foreach (var instance in instances.Values.Where(x => x.Status == EventStatus.Waiting && x.DueTick <= tick)
                         .OrderBy(x => x.DueTick).ThenBy(x => x.InstanceId).ToArray())
            {
                var definition = catalog.Get(instance.DefinitionId);
                if (!AreTargetsValid(definition, instance.Targets))
                {
                    HandleInvalidTarget(definition, instance, tick);
                    if (definition.InvalidTargetPolicy == InvalidEventTargetPolicy.AlternateNode &&
                        instances.TryGetValue(instance.InstanceId, out var alternate) &&
                        definition.Audience == EventAudience.Ai)
                    {
                        var budget = immediateTransitionBudget;
                        var before = history.Count;
                        ResolveAi(definition, alternate, tick, ref budget, checkpoints, targetTick);
                        if (history.Count > before) resolved++;
                    }
                    else expired++;
                    continue;
                }
                var entered = Replace(instance, status: EventStatus.PendingChoice, dueTick: tick);
                instances[instance.InstanceId] = entered;
                if (definition.Audience == EventAudience.Ai)
                {
                    var budget = immediateTransitionBudget;
                    var before = history.Count;
                    ResolveAi(definition, entered, tick, ref budget, checkpoints, targetTick);
                    if (history.Count > before) resolved++;
                }
            }
        }

        private EventInstanceState Offer(EventDefinition definition, EventTargetSet targetSet, long tick,
            ulong occurrence, SortedSet<long> checkpoints, long targetTick)
        {
            var id = CreateInstanceId(definition.Id, targetSet.StableKey, tick, occurrence);
            if (instances.TryGetValue(id, out var existing)) return existing;
            if (history.Any(x => x.InstanceId == id)) return null;
            var state = new EventInstanceState(id, id, definition.Id, definition.StartNodeId, definition.OwnerId,
                definition.Audience, targetSet.Targets, tick, tick, -1, EventStatus.PendingChoice, occurrence, 0,
                null, null, null, "offered");
            instances.Add(id, state);
            var cooldown = new EventCooldownState(definition.Id, ScopeKey(definition, targetSet.Targets),
                checked(tick + definition.CooldownTicks));
            cooldowns[cooldown.Key] = cooldown;
            if (definition.Audience == EventAudience.Ai)
            {
                var budget = immediateTransitionBudget;
                return ResolveAi(definition, state, tick, ref budget, checkpoints, targetTick);
            }
            return state;
        }

        private EventInstanceState ResolveAi(EventDefinition definition, EventInstanceState instance, long tick,
            ref int budget, SortedSet<long> checkpoints, long targetTick)
        {
            var node = definition.Nodes.Single(x => x.Id == instance.CurrentNodeId);
            var choices = node.Choices.Where(x => CanApply(x, instance.Targets)).OrderByDescending(x => x.AiScore)
                .ThenBy(x => x.Id).ToArray();
            if (choices.Length == 0)
            {
                Complete(definition, Replace(instance, status: EventStatus.Invalid, resolvedTick: tick,
                    outcomeCode: "no_eligible_choice"), tick);
                return instance;
            }
            return ResolveChoice(definition, instance, choices[0], tick, ref budget, checkpoints, targetTick);
        }

        private EventInstanceState ResolveChoice(EventDefinition definition, EventInstanceState instance,
            EventChoiceDefinition choice, long tick, ref int budget, SortedSet<long> checkpoints = null,
            long targetTick = long.MaxValue)
        {
            if (--budget < 0 || instance.NodeSequence >= immediateTransitionBudget)
            {
                var invalid = Replace(instance, status: EventStatus.Invalid, resolvedTick: tick,
                    outcomeCode: "transition_budget_exceeded");
                Complete(definition, invalid, tick); return invalid;
            }
            var markers = instance.AppliedEffectMarkers.ToList();
            var outcome = "no_effect";
            for (var i = 0; i < choice.Effects.Count; i++)
            {
                var marker = instance.CurrentNodeId + ":" + i;
                if (markers.Contains(marker, StringComparer.Ordinal)) continue;
                if (!effects.TryApply(choice.Effects[i], instance.Targets, tick, out outcome)) return instance;
                markers.Add(marker);
            }
            var resolvedNodes = instance.ResolvedNodes.ToList();
            if (resolvedNodes.Any(x => x.NodeId == instance.CurrentNodeId)) return instance;
            resolvedNodes.Add(new EventNodeResolutionState(instance.CurrentNodeId, choice.Id, tick));
            var transitions = choice.Transitions.Where(x => targets.Evaluate(x.Condition, instance.Targets))
                .OrderBy(x => x.TargetNodeId).ThenBy(x => x.DelayTicks).ToArray();
            if (transitions.Length == 0)
            {
                var completed = Replace(instance, status: EventStatus.Completed, resolvedTick: tick,
                    selectedChoiceId: choice.Id, resolvedNodes: resolvedNodes, markers: markers, outcomeCode: outcome);
                Complete(definition, completed, tick); return completed;
            }
            long total = 0; checked { foreach (var transition in transitions) total += transition.Weight; }
            var sample = SampleForSelection(definition.Id + "|" + instance.CurrentNodeId + "|" + choice.Id + "|" + instance.Targets[0],
                tick, TransitionSelectionStream, instance.NodeSequence) % unchecked((ulong)total);
            EventTransitionDefinition selected = null;
            foreach (var transition in transitions)
            {
                if (sample < unchecked((ulong)transition.Weight)) { selected = transition; break; }
                sample -= unchecked((ulong)transition.Weight);
            }
            var due = checked(tick + selected.DelayTicks);
            var next = Replace(instance, currentNodeId: selected.TargetNodeId,
                status: selected.DelayTicks == 0 ? EventStatus.PendingChoice : EventStatus.Waiting,
                dueTick: due, nodeSequence: instance.NodeSequence + 1, selectedChoiceId: choice.Id,
                resolvedNodes: resolvedNodes, markers: markers, outcomeCode: outcome);
            instances[instance.InstanceId] = next;
            if (selected.DelayTicks > 0)
            {
                if (checkpoints != null && due <= targetTick) checkpoints.Add(due);
                return next;
            }
            if (definition.Audience == EventAudience.Ai) return ResolveAi(definition, next, tick, ref budget, checkpoints, targetTick);
            return next;
        }

        private void Complete(EventDefinition definition, EventInstanceState instance, long tick)
        {
            instances.Remove(instance.InstanceId);
            var lastChoice = instance.ResolvedNodes.LastOrDefault()?.ChoiceId ?? instance.SelectedChoiceId;
            history.Add(new EventHistoryEntry(instance.InstanceId, instance.RootInstanceId, definition.Id,
                definition.Category, definition.Audience, definition.OwnerId, instance.Targets, instance.OfferedTick,
                tick, lastChoice, instance.OutcomeCode, instance.Status));
            while (history.Count > historyLimit)
            {
                var removed = history[0]; history.RemoveAt(0); archivedCount++;
                archivedChecksum = DeterministicKeyedRandom.Mix(archivedChecksum ^ StableStringHash(removed.InstanceId + "|" + removed.OutcomeCode));
            }
        }

        private void HandleInvalidTarget(EventDefinition definition, EventInstanceState instance, long tick)
        {
            if (definition.InvalidTargetPolicy == InvalidEventTargetPolicy.AlternateNode)
            {
                instances[instance.InstanceId] = Replace(instance, currentNodeId: definition.InvalidTargetNodeId,
                    status: EventStatus.PendingChoice, dueTick: tick, nodeSequence: instance.NodeSequence + 1,
                    outcomeCode: "retargeted_to_alternate_node");
                return;
            }
            var status = definition.InvalidTargetPolicy == InvalidEventTargetPolicy.Expire ? EventStatus.Expired : EventStatus.Cancelled;
            Complete(definition, Replace(instance, status: status, resolvedTick: tick, outcomeCode: "invalid_target"), tick);
        }

        private bool IsEligible(EventDefinition definition, EventTargetSet targetSet, long tick)
        {
            if (!AreTargetsValid(definition, targetSet.Targets)) return false;
            if (definition.Conditions.Any(x => !targets.Evaluate(x, targetSet.Targets))) return false;
            var scope = ScopeKey(definition, targetSet.Targets);
            if (instances.Values.Any(x => x.DefinitionId == definition.Id &&
                ScopeKey(definition, x.Targets) == scope)) return false;
            var key = definition.Id + "|" + scope;
            return !cooldowns.TryGetValue(key, out var cooldown) || tick >= cooldown.NextEligibleTick;
        }

        private bool AreTargetsValid(EventDefinition definition, IReadOnlyList<EventTargetRef> values)
        {
            if (values == null || values.Count == 0 || values.Any(x => !targets.IsValid(x))) return false;
            return definition.Conditions.All(x => targets.Evaluate(x, values));
        }

        private bool CanApply(EventChoiceDefinition choice, IReadOnlyList<EventTargetRef> targetValues) =>
            choice.Effects.All(x => effects.CanApply(x, targetValues));

        private void ValidateHandlers()
        {
            foreach (var effect in catalog.All.SelectMany(x => x.Nodes).SelectMany(x => x.Choices).SelectMany(x => x.Effects))
                if (!effects.Supports(effect.Kind)) throw new ArgumentException("No registered handler for event effect " + effect.Kind);
        }

        private void ValidateInstance(EventInstanceState instance)
        {
            if (!catalog.TryGet(instance.DefinitionId, out var definition)) throw new ArgumentException("Event instance references a missing definition.");
            var node = definition.Nodes.FirstOrDefault(x => x.Id == instance.CurrentNodeId);
            if (node == null) throw new ArgumentException("Event instance references a missing node.");
            if (instance.Audience != definition.Audience || instance.OwnerId.Equals(definition.OwnerId) == false)
                throw new ArgumentException("Event instance audience/owner mismatch.");
            if (instance.IsTerminal) throw new ArgumentException("Terminal instances belong in event history.");
            if (instance.RootInstanceId != instance.InstanceId || instance.ResolvedTick != -1)
                throw new ArgumentException("Event instance identity/tick invariant is invalid.");
            if (!TargetShapeMatches(definition, instance.Targets))
                throw new ArgumentException("Event instance target kinds do not match its definition.");
            if (instance.ResolvedNodes.Select(x => x.NodeId).Distinct().Count() != instance.ResolvedNodes.Count)
                throw new ArgumentException("Duplicate resolved event node marker.");
            if (instance.AppliedEffectMarkers.Distinct(StringComparer.Ordinal).Count() != instance.AppliedEffectMarkers.Count)
                throw new ArgumentException("Duplicate applied effect marker.");
            foreach (var resolution in instance.ResolvedNodes)
            {
                var resolvedNode = definition.Nodes.FirstOrDefault(x => x.Id == resolution.NodeId);
                if (resolvedNode == null || resolvedNode.Choices.All(x => x.Id != resolution.ChoiceId))
                    throw new ArgumentException("Event resolution references a missing node or choice.");
            }
            if (instance.ResolvedNodes.Any(x => x.NodeId == instance.CurrentNodeId))
                throw new ArgumentException("The pending event node is already resolved.");
            if (instance.SelectedChoiceId.HasValue && definition.Nodes.SelectMany(x => x.Choices)
                .All(x => x.Id != instance.SelectedChoiceId.Value))
                throw new ArgumentException("Event instance selected choice is missing from its definition.");
            foreach (var marker in instance.AppliedEffectMarkers)
            {
                var parts = marker.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var index) || index < 0)
                    throw new ArgumentException("Invalid applied event effect marker.");
                var resolved = instance.ResolvedNodes.FirstOrDefault(x => x.NodeId.ToString() == parts[0]);
                var resolvedDefinition = definition.Nodes.FirstOrDefault(x => x.Id.ToString() == parts[0]);
                var resolvedChoice = resolvedDefinition?.Choices.FirstOrDefault(x => resolved != null && x.Id == resolved.ChoiceId);
                if (resolved == null || resolvedChoice == null || index >= resolvedChoice.Effects.Count)
                    throw new ArgumentException("Applied event effect marker does not match a resolved effect.");
            }
            var expectedId = CreateInstanceId(definition.Id,
                string.Join("|", instance.Targets.Select(x => x.ToString())), instance.OfferedTick,
                instance.OccurrenceSequence);
            if (expectedId != instance.InstanceId)
                throw new ArgumentException("Event instance deterministic identity is invalid.");
            if (!AreTargetsValidForRestore(instance.Targets)) throw new ArgumentException("Event instance contains an invalid target reference.");
        }

        private static void ValidateHistory(EventHistoryEntry entry, EventDefinition definition)
        {
            if (entry.RootInstanceId != entry.InstanceId ||
                entry.Category != definition.Category || entry.Audience != definition.Audience ||
                entry.OwnerId.Equals(definition.OwnerId) == false || !TargetShapeMatches(definition, entry.Targets))
                throw new ArgumentException("Event history entry does not match its definition/checkpoint.");
            if (entry.ChoiceId.HasValue && definition.Nodes.SelectMany(x => x.Choices)
                .All(x => x.Id != entry.ChoiceId.Value))
                throw new ArgumentException("Event history references a missing choice.");
        }

        private static bool TargetShapeMatches(EventDefinition definition, IReadOnlyList<EventTargetRef> values)
        {
            if (values == null || values.Count == 0) return false;
            if (definition.TargetSelector == EventTargetSelector.Explicit) return true;
            if (values.Any(x => x.Kind != EventTargetKind.Character)) return false;
            return definition.TargetSelector == EventTargetSelector.PlayerCouple
                ? values.Count == 2
                : values.Count == 1;
        }

        private bool AreTargetsValidForRestore(IReadOnlyList<EventTargetRef> values) =>
            values != null && values.Count > 0 && values.All(x => targets.IsValid(x));

        private static bool IsDue(EventTriggerDefinition trigger, long tick)
        {
            if (trigger.Kind == EventTriggerKind.AbsoluteTick) return tick == trigger.FirstTick;
            if (trigger.Kind != EventTriggerKind.Periodic && trigger.Kind != EventTriggerKind.StatePredicate) return false;
            return tick >= trigger.FirstTick && (tick - trigger.FirstTick) % trigger.IntervalTicks == 0;
        }

        private static ulong Occurrence(EventTriggerDefinition trigger, long tick) => trigger.IntervalTicks > 0
            ? unchecked((ulong)((tick - trigger.FirstTick) / trigger.IntervalTicks)) : unchecked((ulong)tick);

        private EventInstanceId CreateInstanceId(EventDefinitionId definitionId, string targetKey, long tick, ulong occurrence)
        {
            var identity = StableStringHash(definitionId + "|" + targetKey);
            var first = DeterministicKeyedRandom.Sample64(worldSeed, identity, unchecked((ulong)tick), OfferSelectionStream);
            var second = DeterministicKeyedRandom.Sample64(worldSeed, identity ^ occurrence, occurrence, TransitionSelectionStream);
            return new EventInstanceId(StableEntityId.FromDeterministicHash(first, second));
        }

        private ulong SampleForSelection(string key, long tick, uint stream, int sequence) =>
            DeterministicKeyedRandom.Sample64(worldSeed, StableStringHash(key), unchecked((ulong)tick ^ (uint)sequence), stream);

        private static string ScopeKey(EventDefinition definition, IReadOnlyList<EventTargetRef> values) =>
            definition.OwnerId + "|" + string.Join("|", values.Select(x => x.ToString()));

        private static ulong StableStringHash(string value)
        {
            var hash = 14695981039346656037UL;
            if (value != null)
                for (var i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    DeterministicKeyedRandom.AddByte(ref hash, (byte)c);
                    DeterministicKeyedRandom.AddByte(ref hash, (byte)(c >> 8));
                }
            return hash;
        }

        private static void AddInstance(ref ulong hash, EventInstanceState value)
        {
            AddString(ref hash, value.InstanceId.ToString()); AddString(ref hash, value.DefinitionId.ToString());
            AddString(ref hash, value.CurrentNodeId.ToString()); Add(ref hash, (ulong)value.Status);
            Add(ref hash, unchecked((ulong)value.OfferedTick)); Add(ref hash, unchecked((ulong)value.DueTick));
            Add(ref hash, value.OccurrenceSequence); Add(ref hash, unchecked((ulong)value.NodeSequence));
            foreach (var target in value.Targets) AddString(ref hash, target.ToString());
            foreach (var resolution in value.ResolvedNodes)
            { AddString(ref hash, resolution.NodeId.ToString()); AddString(ref hash, resolution.ChoiceId.ToString()); Add(ref hash, unchecked((ulong)resolution.ResolvedTick)); }
            foreach (var marker in value.AppliedEffectMarkers.OrderBy(x => x, StringComparer.Ordinal)) AddString(ref hash, marker);
        }

        private static void Add(ref ulong hash, ulong value) => DeterministicKeyedRandom.Add(ref hash, value);
        private static void AddString(ref ulong hash, string value) => Add(ref hash, StableStringHash(value));

        private static EventInstanceState Replace(EventInstanceState value, EventNodeId? currentNodeId = null,
            long? dueTick = null, long? resolvedTick = null, EventStatus? status = null, int? nodeSequence = null,
            EventChoiceId? selectedChoiceId = null, IEnumerable<EventNodeResolutionState> resolvedNodes = null,
            IEnumerable<string> markers = null, string outcomeCode = null) =>
            new EventInstanceState(value.InstanceId, value.RootInstanceId, value.DefinitionId,
                currentNodeId ?? value.CurrentNodeId, value.OwnerId, value.Audience, value.Targets,
                value.OfferedTick, dueTick ?? value.DueTick, resolvedTick ?? value.ResolvedTick,
                status ?? value.Status, value.OccurrenceSequence, nodeSequence ?? value.NodeSequence,
                selectedChoiceId ?? value.SelectedChoiceId, resolvedNodes ?? value.ResolvedNodes,
                markers ?? value.AppliedEffectMarkers, outcomeCode ?? value.OutcomeCode);

        private static EventInstanceState Clone(EventInstanceState value) => Replace(value,
            resolvedNodes: value.ResolvedNodes.Select(x => new EventNodeResolutionState(x.NodeId, x.ChoiceId, x.ResolvedTick)),
            markers: value.AppliedEffectMarkers.ToArray());
        private static EventCooldownState Clone(EventCooldownState value) =>
            new EventCooldownState(value.DefinitionId, value.ScopeKey, value.NextEligibleTick);
        private static EventHistoryEntry Clone(EventHistoryEntry value) =>
            new EventHistoryEntry(value.InstanceId, value.RootInstanceId, value.DefinitionId, value.Category,
                value.Audience, value.OwnerId, value.Targets, value.OfferedTick, value.ResolvedTick,
                value.ChoiceId, value.OutcomeCode, value.Status);

        private readonly struct Candidate
        {
            public Candidate(EventDefinition definition, EventTargetSet targets) { Definition = definition; Targets = targets; }
            public EventDefinition Definition { get; }
            public EventTargetSet Targets { get; }
        }
    }
}
