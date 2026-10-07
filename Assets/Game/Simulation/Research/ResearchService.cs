using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Research;
using Game.Simulation.Tiers;
using Game.Domain.Health;

namespace Game.Simulation.Research
{
    public interface IResearchRateProvider
    {
        int GetRatePerThousand(StableEntityId workerId);
    }

    public sealed class ConstantResearchRateProvider : IResearchRateProvider
    {
        public ConstantResearchRateProvider(int ratePerThousand = 1000)
        {
            if (ratePerThousand <= 0 || ratePerThousand > 10000)
                throw new ArgumentOutOfRangeException(nameof(ratePerThousand));
            RatePerThousand = ratePerThousand;
        }

        public int RatePerThousand { get; }
        public int GetRatePerThousand(StableEntityId workerId)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            return RatePerThousand;
        }
    }

    public sealed class CharacterResearchRateProvider : IResearchRateProvider
    {
        private static readonly SkillId ResearchSkill = new SkillId("research");
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;

        public CharacterResearchRateProvider(CharacterRegistry registry, ICharacterCapabilityProvider capabilityProvider = null)
        {
            characters = registry ?? throw new ArgumentNullException(nameof(registry));
            capabilities = capabilityProvider;
        }

        public int GetRatePerThousand(StableEntityId workerId)
        {
            if (!characters.TryGet(workerId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return 0;
            var rate = 750 + character.Attributes.Intelligence.Value * 5;
            if (character.TryGetSkill(ResearchSkill, out var skill)) rate += skill.Level.Value * 5;
            if (capabilities != null)
                rate = rate * capabilities.GetCapabilities(workerId).ResearchPerThousand / 1000;
            return rate;
        }
    }

    public sealed class ResearchProject
    {
        private ResearchProject(ResearchProjectState state)
        {
            TechnologyId = state.TechnologyId;
            AccumulatedWorkTicks = state.AccumulatedWorkTicks;
            RequiredWorkTicks = state.RequiredWorkTicks;
            StartTick = state.StartTick;
        }

        public TechnologyId TechnologyId { get; }
        public long AccumulatedWorkTicks { get; private set; }
        public long RequiredWorkTicks { get; }
        public long StartTick { get; }
        public double Progress => RequiredWorkTicks == 0 ? 1d : (double)AccumulatedWorkTicks / RequiredWorkTicks;

        public static ResearchProject Create(TechnologyDefinition definition, long startTick)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Restore(new ResearchProjectState(definition.Id, 0, definition.ResearchWorkRequiredTicks, startTick));
        }

        public static ResearchProject Restore(ResearchProjectState state) => new ResearchProject(state);

        public long AddWork(long workTicks)
        {
            if (workTicks < 0) throw new ArgumentOutOfRangeException(nameof(workTicks));
            var applied = Math.Min(workTicks, RequiredWorkTicks - AccumulatedWorkTicks);
            AccumulatedWorkTicks = checked(AccumulatedWorkTicks + applied);
            return applied;
        }

        public ResearchProjectState CaptureState() =>
            new ResearchProjectState(TechnologyId, AccumulatedWorkTicks, RequiredWorkTicks, StartTick);
    }

    public sealed class ResearchService : IResearchUnlockView
    {
        private sealed class WorkerProgress
        {
            public StableEntityId Id;
            public long LastProcessedTick;
            public int Remainder;
        }

        private readonly TechnologyCatalog catalog;
        private readonly IResearchRateProvider rates;
        private readonly HashSet<TechnologyId> completed = new HashSet<TechnologyId>();
        private readonly Dictionary<StableEntityId, WorkerProgress> workers =
            new Dictionary<StableEntityId, WorkerProgress>();
        private ResearchUnlockSet unlocks;

        public ResearchService(TechnologyCatalog technologyCatalog, IResearchRateProvider rateProvider = null)
        {
            catalog = technologyCatalog ?? throw new ArgumentNullException(nameof(technologyCatalog));
            rates = rateProvider ?? new ConstantResearchRateProvider();
            foreach (var technology in catalog.StartingKnowledge) completed.Add(technology.Id);
            RebuildUnlocks();
        }

        public TechnologyCatalog Catalog => catalog;
        public ResearchProject ActiveProject { get; private set; }
        public IResearchUnlockView Unlocks => unlocks;
        public long LastProcessedTick { get; private set; }
        public long LastCompletionTick { get; private set; } = -1;
        public int CompletionCount { get; private set; }
        public IReadOnlyList<TechnologyId> CompletedTechnologies => completed.OrderBy(x => x).ToArray();
        public IReadOnlyList<StableEntityId> AssignedWorkers => workers.Keys.OrderBy(x => x).ToArray();
        public bool StoneAgeCompleted => completed.Contains(new TechnologyId("stone_age_mastery"));

        public bool IsCompleted(TechnologyId id) => completed.Contains(id);
        public bool IsBuildingUnlocked(Game.Domain.Buildings.BuildingDefinitionId id) => unlocks.IsBuildingUnlocked(id);
        public bool IsProductionRecipeUnlocked(Game.Domain.Production.ProductionRecipeId id) => unlocks.IsProductionRecipeUnlocked(id);
        public bool HasCapability(CapabilityId id) => unlocks.HasCapability(id);
        public bool IsAssigned(StableEntityId workerId) => workers.ContainsKey(workerId);
        public int GetRatePerThousand(StableEntityId workerId) => rates.GetRatePerThousand(workerId);
        public bool ArePrerequisitesMet(TechnologyDefinition definition) =>
            definition.Prerequisites.All(completed.Contains);
        public bool IsAvailable(TechnologyId id)
        {
            var definition = catalog.Get(id);
            return !completed.Contains(id) && (ActiveProject == null || ActiveProject.TechnologyId != id) &&
                ArePrerequisitesMet(definition);
        }

        public IReadOnlyList<TechnologyDefinition> AvailableTechnologies => catalog.All.Where(x => IsAvailable(x.Id)).ToArray();

        public bool Start(TechnologyId id)
        {
            if (ActiveProject != null || !IsAvailable(id)) return false;
            ActiveProject = ResearchProject.Create(catalog.Get(id), LastProcessedTick);
            return true;
        }

        public bool Cancel()
        {
            if (ActiveProject == null) return false;
            ActiveProject = null;
            return true;
        }

        public void AssignWorker(StableEntityId workerId)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            if (!TryAssignWorker(workerId))
                throw new InvalidOperationException("Research worker is already assigned.");
        }

        public bool TryAssignWorker(StableEntityId workerId)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            return workers.TryAdd(workerId, new WorkerProgress
                { Id = workerId, LastProcessedTick = LastProcessedTick });
        }

        public int AssignWorkers(IEnumerable<StableEntityId> workerIds)
        {
            if (workerIds == null) throw new ArgumentNullException(nameof(workerIds));
            var assigned = 0;
            foreach (var workerId in workerIds.Distinct().OrderBy(x => x))
                if (TryAssignWorker(workerId)) assigned++;
            return assigned;
        }

        public int UnassignWorkers(IEnumerable<StableEntityId> workerIds)
        {
            if (workerIds == null) throw new ArgumentNullException(nameof(workerIds));
            var removed = 0;
            foreach (var workerId in workerIds.Distinct().OrderBy(x => x))
                if (UnassignWorker(workerId)) removed++;
            return removed;
        }

        public bool UnassignWorker(StableEntityId workerId) => workers.Remove(workerId);

        public long AdvanceWorkerTo(StableEntityId workerId, long targetCalendarTick, bool activelyResearching)
        {
            if (targetCalendarTick < 0) throw new ArgumentOutOfRangeException(nameof(targetCalendarTick));
            if (!workers.TryGetValue(workerId, out var worker)) return 0;
            // Tier transitions and scene teardown can leave a lower-tier caller with a stale
            // absolute checkpoint. It must not rewind or duplicate canonical research work.
            if (targetCalendarTick < worker.LastProcessedTick) return 0;
            var startTick = worker.LastProcessedTick;
            var elapsed = targetCalendarTick - startTick;
            worker.LastProcessedTick = targetCalendarTick;
            LastProcessedTick = Math.Max(LastProcessedTick, targetCalendarTick);
            if (!activelyResearching || elapsed == 0 || ActiveProject == null) return 0;
            var rate = rates.GetRatePerThousand(workerId);
            if (rate <= 0) return 0;
            var numerator = checked(elapsed * (long)rate + worker.Remainder);
            var work = numerator / 1000;
            var newRemainder = (int)(numerator % 1000);
            if (work == 0) { worker.Remainder = newRemainder; return 0; }
            var remaining = ActiveProject.RequiredWorkTicks - ActiveProject.AccumulatedWorkTicks;
            if (work < remaining)
            {
                ActiveProject.AddWork(work);
                worker.Remainder = newRemainder;
                return work;
            }
            var neededNumerator = checked(remaining * 1000L - worker.Remainder);
            var ticksToCompletion = Math.Max(0L, (neededNumerator + rate - 1L) / rate);
            ActiveProject.AddWork(remaining);
            worker.Remainder = 0;
            CompleteActive(checked(startTick + ticksToCompletion));
            return remaining;
        }

        public int AdvanceWorkersTo(long targetCalendarTick, IEnumerable<StableEntityId> activelyResearching)
        {
            if (activelyResearching == null) throw new ArgumentNullException(nameof(activelyResearching));
            var active = new HashSet<StableEntityId>(activelyResearching);
            var count = 0;
            foreach (var workerId in workers.Keys.OrderBy(x => x).ToArray())
            {
                if (AdvanceWorkerTo(workerId, targetCalendarTick, active.Contains(workerId)) > 0) count++;
            }
            return count;
        }

        public ResearchSaveState CaptureState() => new ResearchSaveState(LastProcessedTick,
            completed, ActiveProject?.CaptureState(), workers.Values.Select(x =>
                new ResearchWorkerState(x.Id, x.LastProcessedTick, x.Remainder)));

        public void RestoreState(ResearchSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var restoredCompleted = new HashSet<TechnologyId>();
            foreach (var id in state.Completed)
            {
                var definition = catalog.Get(id);
                if (!restoredCompleted.Add(id)) throw new ArgumentException("Duplicate completed technology.", nameof(state));
                foreach (var prerequisite in definition.Prerequisites)
                    if (!state.Completed.Contains(prerequisite))
                        throw new ArgumentException("Completed technology has an incomplete prerequisite.", nameof(state));
            }
            foreach (var starting in catalog.StartingKnowledge)
                if (!restoredCompleted.Contains(starting.Id))
                    throw new ArgumentException("Starting knowledge is missing from research state.", nameof(state));
            ResearchProject restoredProject = null;
            if (state.ActiveProject.HasValue)
            {
                var projectState = state.ActiveProject.Value;
                var definition = catalog.Get(projectState.TechnologyId);
                if (restoredCompleted.Contains(definition.Id) || definition.ResearchWorkRequiredTicks != projectState.RequiredWorkTicks ||
                    !definition.Prerequisites.All(restoredCompleted.Contains))
                    throw new ArgumentException("Invalid active research project.", nameof(state));
                restoredProject = ResearchProject.Restore(projectState);
            }
            var restoredWorkers = new Dictionary<StableEntityId, WorkerProgress>();
            foreach (var worker in state.Workers)
                if (!restoredWorkers.TryAdd(worker.WorkerId, new WorkerProgress
                    { Id = worker.WorkerId, LastProcessedTick = worker.LastProcessedTick, Remainder = worker.FractionalRemainder }))
                    throw new ArgumentException("Duplicate research worker.", nameof(state));
            completed.Clear(); foreach (var id in restoredCompleted) completed.Add(id);
            workers.Clear(); foreach (var pair in restoredWorkers) workers.Add(pair.Key, pair.Value);
            ActiveProject = restoredProject;
            LastProcessedTick = state.LastProcessedTick;
            LastCompletionTick = -1;
            CompletionCount = 0;
            RebuildUnlocks();
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)LastProcessedTick));
            foreach (var id in completed.OrderBy(x => x)) AddText(ref hash, id.ToString());
            if (ActiveProject != null)
            {
                AddText(ref hash, ActiveProject.TechnologyId.ToString());
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)ActiveProject.AccumulatedWorkTicks));
            }
            foreach (var worker in workers.Values.OrderBy(x => x.Id))
            {
                AddId(ref hash, worker.Id);
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)worker.LastProcessedTick));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)worker.Remainder));
            }
            return hash;
        }

        private void CompleteActive(long completionTick)
        {
            if (ActiveProject == null || !completed.Add(ActiveProject.TechnologyId))
                throw new InvalidOperationException("Research completion may be committed only once.");
            LastCompletionTick = completionTick;
            CompletionCount = checked(CompletionCount + 1);
            ActiveProject = null;
            RebuildUnlocks();
        }

        private void RebuildUnlocks() => unlocks = new ResearchUnlockSet(catalog, completed);

        private static void AddId(ref ulong hash, StableEntityId id)
        {
            var bytes = id.ToGuid().ToByteArray();
            for (var i = 0; i < bytes.Length; i++) DeterministicKeyedRandom.AddByte(ref hash, bytes[i]);
        }

        private static void AddText(ref ulong hash, string text)
        {
            for (var i = 0; i < text.Length; i++) DeterministicKeyedRandom.Add(ref hash, text[i]);
        }
    }
}
