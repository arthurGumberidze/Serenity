using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Time;
using Game.Simulation.Work;

namespace Game.Simulation.AI
{
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
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<StableEntityId, Entry> byId = new Dictionary<StableEntityId, Entry>();
        private int thinkCursor;
        private int needCursor;
        private int actionCursor;
        private long calendarTicks;

        public Tier1AiScheduler(CharacterRegistry characters, Tier1AiAgentRegistry agents, Tier1AiWorld world,
            Tier1AiSettings settings = null, WorkManager workManager = null)
        {
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
            this.agents = agents ?? throw new ArgumentNullException(nameof(agents));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.settings = settings ?? Tier1AiSettings.Default;
            work = workManager;
        }

        public int Count => entries.Count;
        public long CalendarTicks => calendarTicks;
        public Tier1AiSettings Settings => settings;
        public Tier1AiSchedulerMetrics Metrics { get; } = new Tier1AiSchedulerMetrics();
        public const double CriticalRestEnergy = 0.10d;

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
                var elapsed = TimeSpan.FromTicks(calendarTicks - entry.LastActionTicks);
                entry.LastActionTicks = calendarTicks;
                entry.Agent.UpdatePosition(entry.Movement.CurrentPosition);
                if (entry.Action == null) continue;
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

            var idle = new UtilityCandidate(UtilityActionKind.Idle, new UtilityScore(0.05d), true, 2);
            var rest = new UtilityCandidate(UtilityActionKind.Rest,
                new UtilityScore(1d - entry.Agent.Needs.Energy), true, 0);
            var haulAvailable = entry.Movement.IsAvailable &&
                world.HaulJobs.TryFindJob(entry.Agent, settings.MaxHaulUnits, out _);
            var haulScore = 0.65d * entry.Agent.Needs.Energy * (1d - 0.5d * entry.Agent.Needs.Hunger);
            var haul = new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(haulScore), haulAvailable, 1);
            var best = UtilitySelector.Select(rest, haul, idle);

            if (entry.Action != null)
            {
                var current = entry.Action.Kind == UtilityActionKind.Rest ? rest :
                    entry.Action.Kind == UtilityActionKind.Haul
                        ? new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(haulScore), entry.Action.IsValid, 1)
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
                case UtilityActionKind.Haul:
                    if (world.HaulJobs.TryClaim(entry.Agent, settings.MaxHaulUnits, out var job, out var claim))
                        entry.Action = new HaulActionExecution(entry.Agent, entry.Movement, world, settings, job, claim);
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
            else
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
            entry.LastActionTicks = calendarTicks;
        }

        private static bool IsManual(UtilityActionKind kind) =>
            kind == UtilityActionKind.ManualMove || kind == UtilityActionKind.ManualHaul;

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
