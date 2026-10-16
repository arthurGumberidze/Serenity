using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Time;
using Game.Simulation.Work;
using Game.Domain.Health;

namespace Game.Simulation.AI
{
    public interface IExternalCharacterActivityOwner
    {
        bool IsExternallyControlled(StableEntityId characterId);
    }

    public sealed class Tier1AiSchedulerMetrics
    {
        public long TotalDecisions { get; internal set; }
        public long TotalNeedUpdates { get; internal set; }
        public long TotalActionUpdates { get; internal set; }
        public int LastDecisionBatch { get; internal set; }
        public int MaxDecisionBatch { get; internal set; }
    }

    public sealed class Tier1AiScheduler
    {
        public enum AutonomousWorkMode { LegacyUtility = 0, NeedsOnly = 1 }
        private sealed class Entry
        {
            public Tier1AiAgentState Agent;
            public ITier1MovementDriver Movement;
            public IUtilityActionExecution Action;
            public long NextThinkTicks;
            public long LastNeedTicks;
            public long LastActionTicks;
        }

        private readonly CharacterRegistry characters;
        private readonly Tier1AiAgentRegistry agents;
        private readonly Tier1AiWorld world;
        private readonly Tier1AiSettings settings;
        private readonly WorkManager work;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<StableEntityId, Entry> byId = new Dictionary<StableEntityId, Entry>();
        private int thinkCursor;
        private int needCursor;
        private int actionCursor;
        private long calendarTicks;
        private AutonomousWorkMode autonomousWorkMode = AutonomousWorkMode.LegacyUtility;
        private IExternalCharacterActivityOwner externalActivityOwner;

        public Tier1AiScheduler(CharacterRegistry characters, Tier1AiAgentRegistry agents, Tier1AiWorld world,
            Tier1AiSettings settings = null, WorkManager workManager = null,
            ICharacterCapabilityProvider capabilityProvider = null)
        {
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
            this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.settings = settings ?? Tier1AiSettings.Default;
            work = workManager;
            capabilities = capabilityProvider;
        }

        public int Count => entries.Count;
        public long CalendarTicks => calendarTicks;
        public Tier1AiSettings Settings => settings;
        public Tier1AiSchedulerMetrics Metrics { get; } = new Tier1AiSchedulerMetrics();
        public AutonomousWorkMode WorkMode => autonomousWorkMode;

        public void SetAutonomousWorkMode(AutonomousWorkMode mode)
        {
            if (!Enum.IsDefined(typeof(AutonomousWorkMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            autonomousWorkMode = mode;
        }
        public const double CriticalRestEnergy = 0.10d;
        public const double CriticalHunger = 0.90d;
        public const double CriticalHungerCompletion = 0.20d;

        public void BindExternalActivityOwner(IExternalCharacterActivityOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (externalActivityOwner != null && !ReferenceEquals(externalActivityOwner, owner))
                throw new InvalidOperationException("The scheduler is already bound to another external activity owner.");
            externalActivityOwner = owner;
        }

        public void SuspendForExternalControl(StableEntityId characterId)
        {
            if (!byId.TryGetValue(characterId, out var entry))
                throw new InvalidOperationException("Tier 1 AI agent is not registered for direct control.");
            if (entry.Action != null)
            {
                entry.Action.Cancel();
                entry.Action = null;
            }
            entry.Movement.Stop();
            entry.Agent.UpdatePosition(entry.Movement.CurrentPosition);
            entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
        }

        public void ResumeAfterExternalControl(StableEntityId characterId)
        {
            if (!byId.TryGetValue(characterId, out var entry)) return;
            entry.NextThinkTicks = calendarTicks;
            entry.LastActionTicks = calendarTicks;
            entry.Agent.UpdatePosition(entry.Movement.CurrentPosition);
        }

        public bool CanInterruptForDirectWork(StableEntityId characterId)
        {
            if (!byId.TryGetValue(characterId, out var entry)) return false;
            if (externalActivityOwner != null && externalActivityOwner.IsExternallyControlled(characterId)) return false;
            if (entry.Agent.Needs.Energy <= CriticalRestEnergy || entry.Agent.Needs.Hunger >= CriticalHunger)
                return false;
            return entry.Action == null || entry.Action.IsInterruptible;
        }

        public bool TryInterruptForDirectWork(StableEntityId characterId)
        {
            if (!CanInterruptForDirectWork(characterId) || !byId.TryGetValue(characterId, out var entry)) return false;
            entry.Action?.Cancel();
            entry.Action = null;
            entry.Movement.Stop();
            entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
            entry.NextThinkTicks = calendarTicks;
            return true;
        }

        public void Register(Character character, Tier1AiAgentState agent, ITier1MovementDriver movement)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (character.Id != agent.CharacterId || character.Id != movement.CharacterId)
                throw new ArgumentException("Character, AI state and movement driver must share one StableEntityId.");
            if (!characters.TryGet(character.Id, out var active) || !ReferenceEquals(active, character))
                throw new InvalidOperationException("The AI character must be registered in the active CharacterRegistry.");
            if (byId.ContainsKey(character.Id)) throw new InvalidOperationException("Tier 1 AI agent already registered.");
            agents.Add(agent);
            var phase = StablePhase(character.Id, settings.ThinkInterval.Ticks, settings.StaggerSlots);
            var entry = new Entry
            {
                Agent = agent,
                Movement = movement,
                NextThinkTicks = checked(calendarTicks + phase),
                LastNeedTicks = calendarTicks,
                LastActionTicks = calendarTicks
            };
            var index = FindInsertionIndex(character.Id);
            entries.Insert(index, entry);
            byId.Add(character.Id, entry);
            NormalizeCursors();
        }

        public bool Unregister(StableEntityId characterId)
        {
            if (!byId.TryGetValue(characterId, out var entry)) return false;
            entry.Action?.Cancel();
            entry.Movement.Stop();
            world.Claims.ReleaseForAgent(characterId);
            byId.Remove(characterId);
            entries.Remove(entry);
            agents.Remove(characterId, entry.Agent);
            entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Unmaterialized);
            NormalizeCursors();
            return true;
        }

        public void Reset()
        {
            for (var i = entries.Count - 1; i >= 0; i--) Unregister(entries[i].Agent.CharacterId);
        }

        public void Advance(GameTimeAdvance advance)
        {
            if (advance.CalendarDelta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(advance));
            Metrics.LastDecisionBatch = 0;
            if (advance.CalendarDelta == TimeSpan.Zero || entries.Count == 0) return;
            calendarTicks = checked(calendarTicks + advance.CalendarDelta.Ticks);
            UpdateNeeds();
            UpdateActions();
            AdvanceResearch();
            EvaluateDueAgents();
        }

        private void UpdateNeeds()
        {
            var examined = 0;
            var updated = 0;
            while (examined < entries.Count && updated < settings.NeedBatchSize)
            {
                var entry = entries[needCursor];
                needCursor = (needCursor + 1) % entries.Count;
                examined++;
                var elapsedTicks = calendarTicks - entry.LastNeedTicks;
                if (elapsedTicks < settings.NeedUpdateInterval.Ticks) continue;
                var days = TimeSpan.FromTicks(elapsedTicks).TotalDays;
                var energyDecrease = entry.Agent.CurrentAction == UtilityActionKind.Rest
                    ? 0d : settings.EnergyDecreasePerDay * days;
                entry.Agent.Needs.Advance(settings.HungerIncreasePerDay * days, energyDecrease);
                entry.LastNeedTicks = calendarTicks;
                updated++;
                Metrics.TotalNeedUpdates++;
            }
        }

        private void UpdateActions()
        {
            var count = Math.Min(settings.ActionBatchSize, entries.Count);
            for (var i = 0; i < count; i++)
            {
                var entry = entries[actionCursor];
                actionCursor = (actionCursor + 1) % entries.Count;
                var externallyControlled = externalActivityOwner != null &&
                    externalActivityOwner.IsExternallyControlled(entry.Agent.CharacterId);
                // Idle agents cannot move. Avoid crossing the engine presentation boundary merely to copy the
                // same position into canonical state; active actions and direct control retain the exact path.
                if (entry.Action == null && !externallyControlled) continue;
                var elapsed = TimeSpan.FromTicks(calendarTicks - entry.LastActionTicks);
                entry.LastActionTicks = calendarTicks;
                entry.Agent.UpdatePosition(entry.Movement.CurrentPosition);
                if (externallyControlled)
                {
                    if (entry.Action != null)
                    {
                        entry.Action.Cancel();
                        entry.Action = null;
                        entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    }
                    continue;
                }
                if (entry.Action == null) continue;
                if (capabilities != null && entry.Action.Kind != UtilityActionKind.Rest &&
                    !capabilities.GetCapabilities(entry.Agent.CharacterId).CanPerformOrdinaryWork)
                {
                    entry.Action.Cancel();
                    entry.Action = null;
                    entry.Movement.Stop();
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    continue;
                }
                entry.Action.Tick(elapsed);
                Metrics.TotalActionUpdates++;
                if (!entry.Action.IsFinished) continue;
                entry.Agent.SetAction(UtilityActionKind.Idle,
                    entry.Action.Succeeded ? AiActionPhase.Completed : AiActionPhase.Failed);
                entry.Action = null;
            }
        }

        private void EvaluateDueAgents()
        {
            var examined = 0;
            var evaluated = 0;
            while (examined < entries.Count && evaluated < settings.ThinkBatchSize)
            {
                var entry = entries[thinkCursor];
                thinkCursor = (thinkCursor + 1) % entries.Count;
                examined++;
                if (entry.NextThinkTicks > calendarTicks) continue;
                Think(entry);
                entry.NextThinkTicks = checked(calendarTicks + settings.ThinkInterval.Ticks);
                evaluated++;
            }
            Metrics.LastDecisionBatch = evaluated;
            Metrics.MaxDecisionBatch = Math.Max(Metrics.MaxDecisionBatch, evaluated);
        }

        private void Think(Entry entry)
        {
            entry.Agent.RecordDecision();
            Metrics.TotalDecisions++;
            if (externalActivityOwner != null && externalActivityOwner.IsExternallyControlled(entry.Agent.CharacterId))
                return;
            var characterCapabilities = capabilities?.GetCapabilities(entry.Agent.CharacterId);
            if (characterCapabilities.HasValue && !characterCapabilities.Value.CanPerformOrdinaryWork)
            {
                entry.Action?.Cancel();
                entry.Action = null;
                entry.Movement.Stop();
                entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                return;
            }
            if (entry.Action != null && !entry.Action.IsValid)
            {
                entry.Action.Cancel();
                entry.Action = null;
            }
            if (entry.Action != null && !entry.Action.IsInterruptible) return;

            // U11 precedence: a carrying phase is protected above; otherwise critical Energy wins.
            if (entry.Agent.Needs.Energy <= CriticalRestEnergy)
            {
                if (entry.Action == null || entry.Action.Kind != UtilityActionKind.Rest)
                {
                    entry.Action?.Cancel();
                    entry.Action = null;
                    Start(entry, UtilityActionKind.Rest);
                }
                return;
            }

            if (entry.Agent.Needs.Hunger >= CriticalHunger)
            {
                if (entry.Action == null || entry.Action.Kind != UtilityActionKind.Eat)
                {
                    entry.Action?.Cancel();
                    entry.Action = null;
                    Start(entry, UtilityActionKind.Eat);
                }
                return;
            }

            if (entry.Action != null && IsManual(entry.Action.Kind)) return;
            if (work != null)
            {
                if (!work.TryGetAssigned(entry.Agent.CharacterId, out var assigned))
                {
                    work.TryAssignNext(entry.Agent.CharacterId, out _);
                    work.TryGetAssigned(entry.Agent.CharacterId, out assigned);
                }
                if (assigned != null)
                {
                    entry.Action?.Cancel();
                    entry.Action = null;
                    StartManual(entry, assigned);
                    return;
                }
            }

            if (autonomousWorkMode == AutonomousWorkMode.NeedsOnly)
            {
                if (entry.Action != null && entry.Action.Kind != UtilityActionKind.Rest &&
                    entry.Action.Kind != UtilityActionKind.Eat)
                {
                    entry.Action.Cancel();
                    entry.Action = null;
                }
                if (entry.Action == null)
                {
                    entry.Movement.Stop();
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                }
                return;
            }

            var idle = new UtilityCandidate(UtilityActionKind.Idle, new UtilityScore(0.05d), true, 3);
            var rest = new UtilityCandidate(UtilityActionKind.Rest,
                new UtilityScore(1d - entry.Agent.Needs.Energy), true, 0);
            var haulAvailable = entry.Movement.IsAvailable &&
                world.HaulJobs.TryFindJob(entry.Agent, settings.MaxHaulUnits, out _);
            var haulScore = 0.65d * entry.Agent.Needs.Energy * (1d - 0.5d * entry.Agent.Needs.Hunger);
            var haul = new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(haulScore), haulAvailable, 2);
            var productionAvailable = entry.Movement.IsAvailable && world.Production != null &&
                world.Production.HasEligibleWork(entry.Agent.CharacterId, entry.Agent.Position);
            var productionScore = 0.75d * entry.Agent.Needs.Energy * (1d - 0.35d * entry.Agent.Needs.Hunger);
            var production = new UtilityCandidate(UtilityActionKind.Produce, new UtilityScore(productionScore),
                productionAvailable, 1);
            var researchAvailable = world.Research != null && world.Research.ActiveProject != null &&
                world.Research.IsAssigned(entry.Agent.CharacterId) &&
                (!characterCapabilities.HasValue || characterCapabilities.Value.CanResearch);
            var researchScore = entry.Agent.Needs.Energy * (1d - 0.15d * entry.Agent.Needs.Hunger);
            var research = new UtilityCandidate(UtilityActionKind.Research, new UtilityScore(researchScore),
                researchAvailable, 0);
            var best = UtilitySelector.Select(new[] { rest, research, production, haul, idle });

            if (entry.Action != null)
            {
                var current = entry.Action.Kind == UtilityActionKind.Rest ? rest :
                    entry.Action.Kind == UtilityActionKind.Research
                        ? new UtilityCandidate(UtilityActionKind.Research, new UtilityScore(researchScore), entry.Action.IsValid, 0)
                    : entry.Action.Kind == UtilityActionKind.Produce
                        ? new UtilityCandidate(UtilityActionKind.Produce, new UtilityScore(productionScore), entry.Action.IsValid, 1)
                        : entry.Action.Kind == UtilityActionKind.Haul
                            ? new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(haulScore), entry.Action.IsValid, 2)
                            : idle;
                if (!UtilitySelector.ShouldSwitch(current, best, settings.SwitchMargin, entry.Action.IsValid,
                        entry.Action.IsInterruptible)) return;
                entry.Action.Cancel();
                entry.Action = null;
            }

            Start(entry, best.Action);
        }

        private void Start(Entry entry, UtilityActionKind action)
        {
            switch (action)
            {
                case UtilityActionKind.Rest:
                    entry.Movement.Stop();
                    entry.Action = new RestActionExecution(entry.Agent, settings);
                    break;
                case UtilityActionKind.Eat:
                    entry.Movement.Stop();
                    entry.Action = new EatActionExecution(entry.Agent, settings);
                    break;
                case UtilityActionKind.Haul:
                    if (world.HaulJobs.TryClaim(entry.Agent, settings.MaxHaulUnits, out var job, out var claim))
                        entry.Action = new HaulActionExecution(entry.Agent, entry.Movement, world, settings, job, claim);
                    else
                        entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    break;
                case UtilityActionKind.Produce:
                    if (world.Production != null &&
                        world.Production.TryReserve(entry.Agent.CharacterId, entry.Agent.Position, out var productionJob))
                        entry.Action = new ProductionActionExecution(entry.Agent, entry.Movement,
                            world.Production, settings, productionJob);
                    else
                        entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    break;
                case UtilityActionKind.Research:
                    if (world.Research != null && world.Research.ActiveProject != null &&
                        world.Research.IsAssigned(entry.Agent.CharacterId))
                        entry.Action = new ResearchActionExecution(entry.Agent, entry.Movement, world.Research);
                    else
                        entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    break;
                default:
                    entry.Movement.Stop();
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Idle);
                    break;
            }
            entry.LastActionTicks = calendarTicks;
        }

        private void StartManual(Entry entry, WorkOrder job)
        {
            if (job.Type == WorkJobType.Move)
            {
                if (!entry.Movement.IsAvailable)
                {
                    work.Fail(job.Id);
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Failed);
                }
                else
                {
                    entry.Action = new ManualMoveActionExecution(entry.Agent, entry.Movement, work, job,
                        settings.MovementTimeout);
                }
            }
            else if (job.Type == WorkJobType.Haul)
            {
                var source = new InventoryOwner(InventoryOwnerKind.WorldPile, job.Target.EntityId);
                var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                    job.Destination.Value.EntityId);
                if (world.HaulJobs.TryClaimSpecified(entry.Agent, source, destination, job.ResourceId.Value,
                        job.Quantity, out var candidate, out var claim))
                {
                    var haul = new HaulActionExecution(entry.Agent, entry.Movement, world, settings,
                        candidate, claim, UtilityActionKind.ManualHaul);
                    entry.Action = new ManualHaulActionExecution(haul, claim, work, job);
                }
                else
                {
                    work.Fail(job.Id);
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Failed);
                }
            }
            else if (job.Type == WorkJobType.Gather || job.Type == WorkJobType.Mine ||
                     job.Type == WorkJobType.Chop || job.Type == WorkJobType.Farm ||
                     job.Type == WorkJobType.Craft)
            {
                if (world.Production != null && entry.Movement.IsAvailable &&
                    world.Production.TryReserveAtSite(entry.Agent.CharacterId, job.Target.EntityId, out var productionJob))
                {
                    var production = new ProductionActionExecution(entry.Agent, entry.Movement,
                        world.Production, settings, productionJob);
                    entry.Action = new ManualProductionActionExecution(production, work, job);
                }
                else
                {
                    work.Fail(job.Id);
                    entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Failed);
                }
            }
            else if (!entry.Movement.IsAvailable)
            {
                work.Fail(job.Id);
                entry.Agent.SetAction(UtilityActionKind.Idle, AiActionPhase.Failed);
            }
            else
            {
                Action completion = null;
                if (job.Type == WorkJobType.Build && world.Buildings != null &&
                    world.Buildings.TryGet(job.Target.EntityId, out var building))
                    completion = building.MarkCompleted;
                entry.Action = new ManualDirectActionExecution(entry.Agent, entry.Movement, work, job,
                    settings.MovementTimeout, completion);
            }
            entry.LastActionTicks = calendarTicks;
        }

        private static bool IsManual(UtilityActionKind kind) =>
            kind == UtilityActionKind.ManualMove || kind == UtilityActionKind.ManualHaul ||
            kind == UtilityActionKind.ManualTask;

        private void AdvanceResearch()
        {
            if (world.Research == null || world.Research.ActiveProject == null) return;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!world.Research.IsAssigned(entry.Agent.CharacterId)) continue;
                world.Research.AdvanceWorkerTo(entry.Agent.CharacterId, calendarTicks,
                    entry.Action != null && entry.Action.Kind == UtilityActionKind.Research && entry.Action.IsValid &&
                    (capabilities == null || capabilities.GetCapabilities(entry.Agent.CharacterId).CanResearch));
            }
        }

        private int FindInsertionIndex(StableEntityId id)
        {
            var target = id.ToString();
            var low = 0;
            var high = entries.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (string.CompareOrdinal(entries[middle].Agent.CharacterId.ToString(), target) < 0) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private static long StablePhase(StableEntityId id, long intervalTicks, int slots)
        {
            unchecked
            {
                uint hash = 2166136261;
                var text = id.ToString();
                for (var i = 0; i < text.Length; i++) hash = (hash ^ text[i]) * 16777619;
                var slot = (long)(hash % (uint)slots);
                return intervalTicks / slots * slot;
            }
        }

        private void NormalizeCursors()
        {
            if (entries.Count == 0) { thinkCursor = needCursor = actionCursor = 0; return; }
            thinkCursor %= entries.Count;
            needCursor %= entries.Count;
            actionCursor %= entries.Count;
        }
    }
}
