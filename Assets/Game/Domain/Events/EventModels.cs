using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.Events
{
    internal static class EventIdRules
    {
        public static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96 || value != value.Trim() ||
                !value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
                throw new ArgumentException("Event IDs must be canonical lowercase text.", parameterName);
            return value;
        }
    }

    public readonly struct EventDefinitionId : IEquatable<EventDefinitionId>, IComparable<EventDefinitionId>
    {
        public EventDefinitionId(string value) { Value = EventIdRules.Require(value, nameof(value)); }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(EventDefinitionId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(EventDefinitionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventDefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EventDefinitionId left, EventDefinitionId right) => left.Equals(right);
        public static bool operator !=(EventDefinitionId left, EventDefinitionId right) => !left.Equals(right);
    }

    public readonly struct EventNodeId : IEquatable<EventNodeId>, IComparable<EventNodeId>
    {
        public EventNodeId(string value) { Value = EventIdRules.Require(value, nameof(value)); }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(EventNodeId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(EventNodeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventNodeId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EventNodeId left, EventNodeId right) => left.Equals(right);
        public static bool operator !=(EventNodeId left, EventNodeId right) => !left.Equals(right);
    }

    public readonly struct EventChoiceId : IEquatable<EventChoiceId>, IComparable<EventChoiceId>
    {
        public EventChoiceId(string value) { Value = EventIdRules.Require(value, nameof(value)); }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(EventChoiceId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(EventChoiceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventChoiceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EventChoiceId left, EventChoiceId right) => left.Equals(right);
        public static bool operator !=(EventChoiceId left, EventChoiceId right) => !left.Equals(right);
    }

    public readonly struct EventInstanceId : IEquatable<EventInstanceId>, IComparable<EventInstanceId>
    {
        public EventInstanceId(StableEntityId value)
        {
            if (!value.IsValid) throw new ArgumentException("Event instance ID must be valid.", nameof(value));
            Value = value;
        }
        public StableEntityId Value { get; }
        public bool IsValid => Value.IsValid;
        public int CompareTo(EventInstanceId other) => Value.CompareTo(other.Value);
        public bool Equals(EventInstanceId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is EventInstanceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(EventInstanceId left, EventInstanceId right) => left.Equals(right);
        public static bool operator !=(EventInstanceId left, EventInstanceId right) => !left.Equals(right);
    }

    public readonly struct EventOwnerId : IEquatable<EventOwnerId>, IComparable<EventOwnerId>
    {
        public EventOwnerId(string value) { Value = EventIdRules.Require(value, nameof(value)); }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(EventOwnerId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(EventOwnerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventOwnerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum EventCategory { Character = 1, Family = 2, Dynasty = 3, Settlement = 4, Building = 5, Military = 6, Research = 7 }
    public enum EventAudience { Player = 1, Ai = 2 }
    public enum EventTargetKind { Character = 1, Dynasty = 2, Building = 3, MilitaryUnit = 4, Battle = 5, Settlement = 6, WorldLocation = 7, Faction = 8 }
    public enum EventTargetSelector { Explicit = 1, PlayerCharacter = 2, PlayerCouple = 3, AiCharacter = 4 }
    public enum EventTriggerKind { AbsoluteTick = 1, Periodic = 2, StatePredicate = 3, CanonicalHook = 4, Explicit = 5 }
    public enum CanonicalEventHookKind { CharacterStateChanged = 1, BattleFinished = 2, BuildingChanged = 3, ResearchCompleted = 4 }
    public enum EventConditionKind { Always = 1, TargetExists = 2, TargetAlive = 3, TargetAdult = 4, TargetsAreSpouses = 5 }
    public enum EventEffectKind { None = 1, RelationshipEvent = 2, HealthCondition = 3 }
    public enum InvalidEventTargetPolicy { Cancel = 1, Expire = 2, AlternateNode = 3 }
    public enum EventStatus { PendingChoice = 1, Waiting = 2, Completed = 3, Cancelled = 4, Expired = 5, Invalid = 6 }

    public readonly struct EventTargetRef : IEquatable<EventTargetRef>, IComparable<EventTargetRef>
    {
        public EventTargetRef(EventTargetKind kind, string persistentId)
        {
            if (!Enum.IsDefined(typeof(EventTargetKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(persistentId) || persistentId.Length > 128 || persistentId != persistentId.Trim())
                throw new ArgumentException("Persistent target ID is required.", nameof(persistentId));
            Kind = kind;
            PersistentId = persistentId;
        }
        public EventTargetKind Kind { get; }
        public string PersistentId { get; }
        public static EventTargetRef Character(StableEntityId id)
        {
            if (!id.IsValid) throw new ArgumentException("Character ID required.", nameof(id));
            return new EventTargetRef(EventTargetKind.Character, id.ToString());
        }
        public bool TryGetStableEntityId(out StableEntityId id) => StableEntityId.TryParse(PersistentId, out id);
        public int CompareTo(EventTargetRef other)
        {
            var kind = Kind.CompareTo(other.Kind);
            return kind != 0 ? kind : string.Compare(PersistentId, other.PersistentId, StringComparison.Ordinal);
        }
        public bool Equals(EventTargetRef other) => Kind == other.Kind &&
            string.Equals(PersistentId, other.PersistentId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventTargetRef other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(PersistentId ?? string.Empty);
        public override string ToString() => Kind + ":" + PersistentId;
    }

    public sealed class EventTargetSet
    {
        public EventTargetSet(IEnumerable<EventTargetRef> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var values = targets.ToArray();
            if (values.Length == 0 || values.Length > 8) throw new ArgumentException("One to eight targets are required.", nameof(targets));
            if (values.Distinct().Count() != values.Length) throw new ArgumentException("Duplicate event target.", nameof(targets));
            Targets = new ReadOnlyCollection<EventTargetRef>(values);
        }
        public IReadOnlyList<EventTargetRef> Targets { get; }
        public EventTargetRef Primary => Targets[0];
        public string StableKey => string.Join("|", Targets.Select(x => x.ToString()));
    }

    public sealed class EventTriggerDefinition
    {
        public EventTriggerDefinition(EventTriggerKind kind, long firstTick = 0, long intervalTicks = 0,
            CanonicalEventHookKind hookKind = default)
        {
            if (!Enum.IsDefined(typeof(EventTriggerKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (firstTick < 0 || intervalTicks < 0) throw new ArgumentOutOfRangeException(nameof(firstTick));
            if (kind == EventTriggerKind.Periodic && intervalTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            if (kind == EventTriggerKind.StatePredicate && intervalTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            if (kind == EventTriggerKind.AbsoluteTick && intervalTicks != 0)
                throw new ArgumentException("Absolute triggers cannot have an interval.", nameof(intervalTicks));
            if (kind == EventTriggerKind.CanonicalHook && !Enum.IsDefined(typeof(CanonicalEventHookKind), hookKind))
                throw new ArgumentOutOfRangeException(nameof(hookKind));
            Kind = kind; FirstTick = firstTick; IntervalTicks = intervalTicks; HookKind = hookKind;
        }
        public EventTriggerKind Kind { get; }
        public long FirstTick { get; }
        public long IntervalTicks { get; }
        public CanonicalEventHookKind HookKind { get; }
    }

    public sealed class EventConditionDefinition
    {
        public EventConditionDefinition(EventConditionKind kind, int targetIndex = 0)
        {
            if (!Enum.IsDefined(typeof(EventConditionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (targetIndex < 0 || targetIndex > 7) throw new ArgumentOutOfRangeException(nameof(targetIndex));
            Kind = kind; TargetIndex = targetIndex;
        }
        public EventConditionKind Kind { get; }
        public int TargetIndex { get; }
    }

    public sealed class EventEffectDefinition
    {
        public EventEffectDefinition(EventEffectKind kind, string parameter = "none", int targetIndex = 0,
            int secondaryTargetIndex = 1)
        {
            if (!Enum.IsDefined(typeof(EventEffectKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind != EventEffectKind.None) EventIdRules.Require(parameter, nameof(parameter));
            if (targetIndex < 0 || targetIndex > 7 || secondaryTargetIndex < 0 || secondaryTargetIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            Kind = kind; Parameter = parameter ?? string.Empty; TargetIndex = targetIndex;
            SecondaryTargetIndex = secondaryTargetIndex;
        }
        public EventEffectKind Kind { get; }
        public string Parameter { get; }
        public int TargetIndex { get; }
        public int SecondaryTargetIndex { get; }
    }

    public sealed class EventTransitionDefinition
    {
        public EventTransitionDefinition(EventNodeId targetNodeId, int weight, long delayTicks = 0,
            EventConditionDefinition condition = null)
        {
            if (!targetNodeId.IsValid) throw new ArgumentException("Transition target is required.", nameof(targetNodeId));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            if (delayTicks < 0) throw new ArgumentOutOfRangeException(nameof(delayTicks));
            TargetNodeId = targetNodeId; Weight = weight; DelayTicks = delayTicks;
            Condition = condition ?? new EventConditionDefinition(EventConditionKind.Always);
        }
        public EventNodeId TargetNodeId { get; }
        public int Weight { get; }
        public long DelayTicks { get; }
        public EventConditionDefinition Condition { get; }
    }

    public sealed class EventChoiceDefinition
    {
        public EventChoiceDefinition(EventChoiceId id, string label, int aiScore,
            IEnumerable<EventEffectDefinition> effects = null, IEnumerable<EventTransitionDefinition> transitions = null)
        {
            if (!id.IsValid) throw new ArgumentException("Choice ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(label) || label.Length > 160 || label != label.Trim())
                throw new ArgumentException("Choice label is required.", nameof(label));
            Id = id; Label = label; AiScore = aiScore;
            Effects = new ReadOnlyCollection<EventEffectDefinition>((effects ?? Array.Empty<EventEffectDefinition>()).ToArray());
            Transitions = new ReadOnlyCollection<EventTransitionDefinition>((transitions ?? Array.Empty<EventTransitionDefinition>()).ToArray());
            if (Effects.Any(x => x == null) || Transitions.Any(x => x == null))
                throw new ArgumentException("Choice content cannot contain null.");
            if (Effects.Count > 1)
                throw new ArgumentException("U28 choices use one authoritative mutation at most; compose a transactional handler for complex outcomes.");
        }
        public EventChoiceId Id { get; }
        public string Label { get; }
        public int AiScore { get; }
        public IReadOnlyList<EventEffectDefinition> Effects { get; }
        public IReadOnlyList<EventTransitionDefinition> Transitions { get; }
    }

    public sealed class EventNodeDefinition
    {
        public EventNodeDefinition(EventNodeId id, string title, string description,
            IEnumerable<EventChoiceDefinition> choices, bool optional = false)
        {
            if (!id.IsValid) throw new ArgumentException("Node ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(title) || title.Length > 120 || title != title.Trim())
                throw new ArgumentException("Node title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(description) || description.Length > 800 || description != description.Trim())
                throw new ArgumentException("Node description is required.", nameof(description));
            Id = id; Title = title; Description = description; Optional = optional;
            Choices = new ReadOnlyCollection<EventChoiceDefinition>((choices ?? throw new ArgumentNullException(nameof(choices))).ToArray());
            if (Choices.Count == 0 || Choices.Any(x => x == null)) throw new ArgumentException("Every node needs at least one choice.", nameof(choices));
        }
        public EventNodeId Id { get; }
        public string Title { get; }
        public string Description { get; }
        public bool Optional { get; }
        public IReadOnlyList<EventChoiceDefinition> Choices { get; }
    }

    public sealed class EventDefinition
    {
        public EventDefinition(EventDefinitionId id, EventCategory category, EventAudience audience,
            EventOwnerId ownerId, EventTargetSelector targetSelector, EventTriggerDefinition trigger,
            int weight, long cooldownTicks, EventNodeId startNodeId, IEnumerable<EventNodeDefinition> nodes,
            IEnumerable<EventConditionDefinition> conditions = null,
            InvalidEventTargetPolicy invalidTargetPolicy = InvalidEventTargetPolicy.Cancel,
            EventNodeId invalidTargetNodeId = default)
        {
            if (!id.IsValid || !ownerId.IsValid || !startNodeId.IsValid) throw new ArgumentException("Definition identity fields are required.");
            if (!Enum.IsDefined(typeof(EventCategory), category) || !Enum.IsDefined(typeof(EventAudience), audience) ||
                !Enum.IsDefined(typeof(EventTargetSelector), targetSelector) ||
                !Enum.IsDefined(typeof(InvalidEventTargetPolicy), invalidTargetPolicy)) throw new ArgumentOutOfRangeException();
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            if (cooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            Id = id; Category = category; Audience = audience; OwnerId = ownerId; TargetSelector = targetSelector;
            Trigger = trigger; Weight = weight; CooldownTicks = cooldownTicks; StartNodeId = startNodeId;
            InvalidTargetPolicy = invalidTargetPolicy; InvalidTargetNodeId = invalidTargetNodeId;
            Nodes = new ReadOnlyCollection<EventNodeDefinition>((nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
            Conditions = new ReadOnlyCollection<EventConditionDefinition>((conditions ?? Array.Empty<EventConditionDefinition>()).ToArray());
        }
        public EventDefinitionId Id { get; }
        public EventCategory Category { get; }
        public EventAudience Audience { get; }
        public EventOwnerId OwnerId { get; }
        public EventTargetSelector TargetSelector { get; }
        public EventTriggerDefinition Trigger { get; }
        public int Weight { get; }
        public long CooldownTicks { get; }
        public EventNodeId StartNodeId { get; }
        public IReadOnlyList<EventNodeDefinition> Nodes { get; }
        public IReadOnlyList<EventConditionDefinition> Conditions { get; }
        public InvalidEventTargetPolicy InvalidTargetPolicy { get; }
        public EventNodeId InvalidTargetNodeId { get; }
    }

    public sealed class EventCatalog
    {
        private readonly Dictionary<EventDefinitionId, EventDefinition> definitions;
        public EventCatalog(IEnumerable<EventDefinition> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            definitions = new Dictionary<EventDefinitionId, EventDefinition>();
            foreach (var definition in values)
            {
                if (definition == null) throw new ArgumentException("Event definition cannot be null.", nameof(values));
                if (!definitions.TryAdd(definition.Id, definition)) throw new ArgumentException("Duplicate event definition: " + definition.Id);
                ValidateDefinition(definition);
            }
            if (definitions.Count == 0) throw new ArgumentException("At least one event definition is required.", nameof(values));
            checked { var total = 0; foreach (var definition in definitions.Values) total += definition.Weight; }
        }
        public IReadOnlyList<EventDefinition> All => definitions.Values.OrderBy(x => x.Id).ToArray();
        public EventDefinition Get(EventDefinitionId id) => definitions.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown event definition: " + id);
        public bool TryGet(EventDefinitionId id, out EventDefinition definition) => definitions.TryGetValue(id, out definition);

        private static void ValidateDefinition(EventDefinition definition)
        {
            var nodes = new Dictionary<EventNodeId, EventNodeDefinition>();
            foreach (var node in definition.Nodes)
            {
                if (node == null || !nodes.TryAdd(node.Id, node)) throw new ArgumentException("Duplicate or null event node in " + definition.Id);
                var choices = new HashSet<EventChoiceId>();
                foreach (var choice in node.Choices)
                {
                    if (!choices.Add(choice.Id)) throw new ArgumentException("Duplicate event choice in " + node.Id);
                    checked { var total = 0; foreach (var transition in choice.Transitions) total += transition.Weight; }
                }
            }
            if (!nodes.ContainsKey(definition.StartNodeId)) throw new ArgumentException("Missing event start node: " + definition.StartNodeId);
            if (definition.InvalidTargetPolicy == InvalidEventTargetPolicy.AlternateNode &&
                !nodes.ContainsKey(definition.InvalidTargetNodeId)) throw new ArgumentException("Missing invalid-target alternate node.");
            foreach (var node in nodes.Values)
                foreach (var choice in node.Choices)
                    foreach (var transition in choice.Transitions)
                        if (!nodes.ContainsKey(transition.TargetNodeId)) throw new ArgumentException("Transition points to a missing node: " + transition.TargetNodeId);

            var reachable = new HashSet<EventNodeId>();
            var queue = new Queue<EventNodeId>();
            queue.Enqueue(definition.StartNodeId);
            if (definition.InvalidTargetPolicy == InvalidEventTargetPolicy.AlternateNode)
                queue.Enqueue(definition.InvalidTargetNodeId);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!reachable.Add(id)) continue;
                foreach (var transition in nodes[id].Choices.SelectMany(x => x.Transitions)) queue.Enqueue(transition.TargetNodeId);
            }
            if (nodes.Values.Any(x => !x.Optional && !reachable.Contains(x.Id))) throw new ArgumentException("Event contains an unreachable required node.");

            var visiting = new HashSet<EventNodeId>();
            var visited = new HashSet<EventNodeId>();
            foreach (var node in nodes.Values) ValidateImmediateCycles(node.Id, nodes, visiting, visited);
        }

        private static void ValidateImmediateCycles(EventNodeId id, IDictionary<EventNodeId, EventNodeDefinition> nodes,
            ISet<EventNodeId> visiting, ISet<EventNodeId> visited)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Event contains an immediate transition cycle at " + id);
            foreach (var transition in nodes[id].Choices.SelectMany(x => x.Transitions).Where(x => x.DelayTicks == 0))
                ValidateImmediateCycles(transition.TargetNodeId, nodes, visiting, visited);
            visiting.Remove(id);
            visited.Add(id);
        }
    }

    public sealed class EventNodeResolutionState
    {
        public EventNodeResolutionState(EventNodeId nodeId, EventChoiceId choiceId, long resolvedTick)
        {
            if (!nodeId.IsValid || !choiceId.IsValid) throw new ArgumentException("Resolved node and choice IDs are required.");
            if (resolvedTick < 0) throw new ArgumentOutOfRangeException(nameof(resolvedTick));
            NodeId = nodeId; ChoiceId = choiceId; ResolvedTick = resolvedTick;
        }
        public EventNodeId NodeId { get; }
        public EventChoiceId ChoiceId { get; }
        public long ResolvedTick { get; }
    }

    public sealed class EventInstanceState
    {
        public EventInstanceState(EventInstanceId instanceId, EventInstanceId rootInstanceId,
            EventDefinitionId definitionId, EventNodeId currentNodeId, EventOwnerId ownerId, EventAudience audience,
            IEnumerable<EventTargetRef> targets, long offeredTick, long dueTick, long resolvedTick,
            EventStatus status, ulong occurrenceSequence, int nodeSequence, EventChoiceId? selectedChoiceId,
            IEnumerable<EventNodeResolutionState> resolvedNodes, IEnumerable<string> appliedEffectMarkers,
            string outcomeCode)
        {
            if (!instanceId.IsValid || !rootInstanceId.IsValid || !definitionId.IsValid || !currentNodeId.IsValid || !ownerId.IsValid)
                throw new ArgumentException("Event instance identities are required.");
            if (!Enum.IsDefined(typeof(EventAudience), audience) || !Enum.IsDefined(typeof(EventStatus), status)) throw new ArgumentOutOfRangeException();
            if (offeredTick < 0 || dueTick < offeredTick || resolvedTick < -1 || nodeSequence < 0) throw new ArgumentOutOfRangeException();
            var targetValues = (targets ?? throw new ArgumentNullException(nameof(targets))).ToArray();
            if (targetValues.Length == 0 || targetValues.Distinct().Count() != targetValues.Length) throw new ArgumentException("Event targets must be unique.");
            InstanceId = instanceId; RootInstanceId = rootInstanceId; DefinitionId = definitionId; CurrentNodeId = currentNodeId;
            OwnerId = ownerId; Audience = audience; Targets = new ReadOnlyCollection<EventTargetRef>(targetValues);
            OfferedTick = offeredTick; DueTick = dueTick; ResolvedTick = resolvedTick; Status = status;
            OccurrenceSequence = occurrenceSequence; NodeSequence = nodeSequence; SelectedChoiceId = selectedChoiceId;
            ResolvedNodes = new ReadOnlyCollection<EventNodeResolutionState>((resolvedNodes ?? Array.Empty<EventNodeResolutionState>()).ToArray());
            AppliedEffectMarkers = new ReadOnlyCollection<string>((appliedEffectMarkers ?? Array.Empty<string>()).ToArray());
            OutcomeCode = outcomeCode ?? string.Empty;
        }
        public EventInstanceId InstanceId { get; }
        public EventInstanceId RootInstanceId { get; }
        public EventDefinitionId DefinitionId { get; }
        public EventNodeId CurrentNodeId { get; }
        public EventOwnerId OwnerId { get; }
        public EventAudience Audience { get; }
        public IReadOnlyList<EventTargetRef> Targets { get; }
        public long OfferedTick { get; }
        public long DueTick { get; }
        public long ResolvedTick { get; }
        public EventStatus Status { get; }
        public ulong OccurrenceSequence { get; }
        public int NodeSequence { get; }
        public EventChoiceId? SelectedChoiceId { get; }
        public IReadOnlyList<EventNodeResolutionState> ResolvedNodes { get; }
        public IReadOnlyList<string> AppliedEffectMarkers { get; }
        public string OutcomeCode { get; }
        public bool IsTerminal => Status == EventStatus.Completed || Status == EventStatus.Cancelled ||
            Status == EventStatus.Expired || Status == EventStatus.Invalid;
    }

    public sealed class EventCooldownState
    {
        public EventCooldownState(EventDefinitionId definitionId, string scopeKey, long nextEligibleTick)
        {
            if (!definitionId.IsValid || string.IsNullOrWhiteSpace(scopeKey)) throw new ArgumentException("Cooldown key is required.");
            if (nextEligibleTick < 0) throw new ArgumentOutOfRangeException(nameof(nextEligibleTick));
            DefinitionId = definitionId; ScopeKey = scopeKey; NextEligibleTick = nextEligibleTick;
        }
        public EventDefinitionId DefinitionId { get; }
        public string ScopeKey { get; }
        public long NextEligibleTick { get; }
        public string Key => DefinitionId + "|" + ScopeKey;
    }

    public sealed class EventHistoryEntry
    {
        public EventHistoryEntry(EventInstanceId instanceId, EventInstanceId rootInstanceId,
            EventDefinitionId definitionId, EventCategory category, EventAudience audience, EventOwnerId ownerId,
            IEnumerable<EventTargetRef> targets, long offeredTick, long resolvedTick, EventChoiceId? choiceId,
            string outcomeCode, EventStatus status)
        {
            if (!instanceId.IsValid || !rootInstanceId.IsValid || !definitionId.IsValid || !ownerId.IsValid)
                throw new ArgumentException("History identities are required.");
            if (offeredTick < 0 || resolvedTick < offeredTick ||
                !Enum.IsDefined(typeof(EventCategory), category) ||
                !Enum.IsDefined(typeof(EventAudience), audience) ||
                !Enum.IsDefined(typeof(EventStatus), status))
                throw new ArgumentOutOfRangeException();
            if (status != EventStatus.Completed && status != EventStatus.Cancelled &&
                status != EventStatus.Expired && status != EventStatus.Invalid)
                throw new ArgumentException("Event history may contain terminal states only.", nameof(status));
            var targetValues = (targets ?? throw new ArgumentNullException(nameof(targets))).ToArray();
            if (targetValues.Length == 0 || targetValues.Length > 8 ||
                targetValues.Distinct().Count() != targetValues.Length)
                throw new ArgumentException("Event history targets must be unique.", nameof(targets));
            InstanceId = instanceId; RootInstanceId = rootInstanceId; DefinitionId = definitionId; Category = category;
            Audience = audience; OwnerId = ownerId; Targets = new ReadOnlyCollection<EventTargetRef>(targetValues);
            OfferedTick = offeredTick; ResolvedTick = resolvedTick; ChoiceId = choiceId;
            OutcomeCode = outcomeCode ?? string.Empty; Status = status;
        }
        public EventInstanceId InstanceId { get; }
        public EventInstanceId RootInstanceId { get; }
        public EventDefinitionId DefinitionId { get; }
        public EventCategory Category { get; }
        public EventAudience Audience { get; }
        public EventOwnerId OwnerId { get; }
        public IReadOnlyList<EventTargetRef> Targets { get; }
        public long OfferedTick { get; }
        public long ResolvedTick { get; }
        public EventChoiceId? ChoiceId { get; }
        public string OutcomeCode { get; }
        public EventStatus Status { get; }
    }

    public sealed class EventSaveState
    {
        public EventSaveState(long worldSeed, long lastProcessedTick, ulong nextRequestSequence,
            IEnumerable<EventInstanceState> instances, IEnumerable<EventCooldownState> cooldowns,
            IEnumerable<EventHistoryEntry> history, long archivedCount, ulong archivedChecksum)
        {
            if (lastProcessedTick < 0 || archivedCount < 0) throw new ArgumentOutOfRangeException();
            WorldSeed = worldSeed; LastProcessedTick = lastProcessedTick; NextRequestSequence = nextRequestSequence;
            Instances = new ReadOnlyCollection<EventInstanceState>((instances ?? Array.Empty<EventInstanceState>()).ToArray());
            Cooldowns = new ReadOnlyCollection<EventCooldownState>((cooldowns ?? Array.Empty<EventCooldownState>()).ToArray());
            History = new ReadOnlyCollection<EventHistoryEntry>((history ?? Array.Empty<EventHistoryEntry>()).ToArray());
            ArchivedCount = archivedCount; ArchivedChecksum = archivedChecksum;
        }
        public long WorldSeed { get; }
        public long LastProcessedTick { get; }
        public ulong NextRequestSequence { get; }
        public IReadOnlyList<EventInstanceState> Instances { get; }
        public IReadOnlyList<EventCooldownState> Cooldowns { get; }
        public IReadOnlyList<EventHistoryEntry> History { get; }
        public long ArchivedCount { get; }
        public ulong ArchivedChecksum { get; }
    }
}
