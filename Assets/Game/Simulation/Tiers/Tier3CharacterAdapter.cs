using System;
using Game.Domain;
using Game.Domain.Characters;
using Game.Simulation.Research;

namespace Game.Simulation.Tiers
{
    public sealed class Tier3CharacterAdapter : ICharacterTierAdapter, ICharacterCheckpointAdapter
    {
        private readonly Tier3CharacterRegistry records;
        private readonly OffCameraSimulationService simulation;
        private readonly Func<SimulationTimePoint> currentTime;
        private long lastAutomaticStepIndex = long.MinValue;

        public Tier3CharacterAdapter(Tier3CharacterRegistry registry)
        {
            records = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Tier3CharacterAdapter(Tier3CharacterRegistry registry, OffCameraSimulationService simulationService,
            Func<SimulationTimePoint> currentTimeProvider) : this(registry)
        {
            simulation = simulationService ?? throw new ArgumentNullException(nameof(simulationService));
            currentTime = currentTimeProvider ?? throw new ArgumentNullException(nameof(currentTimeProvider));
        }

        public CharacterSimulationTier Tier => CharacterSimulationTier.Tier3;
        public int AutomaticBatchCount { get; private set; }
        public bool IsActive(StableEntityId characterId) => records.TryGet(characterId, out _);
        public CharacterRuntimeState Capture(StableEntityId characterId)
        {
            var record = records.Get(characterId);
            if (simulation == null) return record.State;
            var advanced = simulation.AdvanceState(record.State, currentTime());
            record.Replace(advanced);
            return advanced;
        }

        public CharacterRuntimeState CaptureCheckpoint(StableEntityId characterId) => records.Get(characterId).State;

        public void ValidateMaterialization(Character character, CharacterRuntimeState state)
        {
            ValidateCheckpoint(character, state);
            if (simulation != null) simulation.ValidateTarget(state, currentTime());
        }

        public void ValidateCheckpoint(Character character, CharacterRuntimeState state)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Id != state.CharacterId)
                throw new ArgumentException("Character and Tier 3 state must share one identity.", nameof(state));
            if (IsActive(character.Id))
                throw new InvalidOperationException("Tier 3 representation is already active.");
        }

        public void MaterializeCheckpoint(Character character, CharacterRuntimeState state)
        {
            ValidateCheckpoint(character, state);
            records.Add(new Tier3CharacterRecord(state));
        }

        public void Materialize(Character character, CharacterRuntimeState state)
        {
            ValidateMaterialization(character, state);
            var materialized = simulation == null ? state : simulation.AdvanceState(state, currentTime());
            records.Add(new Tier3CharacterRecord(materialized));
        }

        public void Dematerialize(StableEntityId characterId)
        {
            if (!records.Remove(characterId))
                throw new InvalidOperationException("Tier 3 representation is not active.");
        }

        public OffCameraSimulationResult AdvanceAllToCurrent()
        {
            if (simulation == null) throw new InvalidOperationException("Off-camera simulation is not configured.");
            if (records.Count == 0) return default;
            var target = currentTime();
            var stepIndex = target.CalendarTicks / simulation.FixedStep.Ticks;
            if (stepIndex == lastAutomaticStepIndex) return default;
            lastAutomaticStepIndex = stepIndex;
            AutomaticBatchCount++;
            return simulation.Advance(records.All, target);
        }

        public int AdvanceResearchToCurrent(ResearchService research)
        {
            if (research == null) throw new ArgumentNullException(nameof(research));
            if (currentTime == null) throw new InvalidOperationException("Off-camera simulation is not configured.");
            var target = currentTime().CalendarTicks;
            var count = 0;
            foreach (var record in records.All)
                if (research.IsAssigned(record.CharacterId) &&
                    research.AdvanceWorkerTo(record.CharacterId, target, true) > 0) count++;
            return count;
        }

        /// <summary>
        /// Tier 3 records are runtime projections rather than durable authority. Loading an older checkpoint
        /// replaces the canonical clock, so projection cursors must be rebased before the next automatic tick.
        /// </summary>
        public void RebaseAfterLoad(SimulationTimePoint restoredTime)
        {
            // Saved cursors and deterministic accumulators are authoritative; do not erase them.
            if (simulation != null)
                foreach (var record in records.All) simulation.ValidateTarget(record.State, restoredTime);
            lastAutomaticStepIndex = long.MinValue;
        }
    }
}
