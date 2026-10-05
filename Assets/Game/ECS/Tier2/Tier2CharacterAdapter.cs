using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Simulation.Tiers;
using Unity.Mathematics;

namespace Game.ECS.Tier2
{
    /// <summary>U13 adapter over the existing U12 materialization/extraction boundary.</summary>
    public sealed class Tier2CharacterAdapter : ICharacterTierAdapter
    {
        private readonly Tier2Runtime runtime;
        private readonly Dictionary<StableEntityId, CharacterRuntimeState> continuity =
            new Dictionary<StableEntityId, CharacterRuntimeState>();

        public Tier2CharacterAdapter(Tier2Runtime tier2Runtime)
        {
            runtime = tier2Runtime ?? throw new ArgumentNullException(nameof(tier2Runtime));
        }

        public CharacterSimulationTier Tier => CharacterSimulationTier.Tier2;
        public bool IsActive(StableEntityId characterId) => runtime.Materializer.TryGetEntity(characterId, out _);

        public CharacterRuntimeState Capture(StableEntityId characterId)
        {
            var extracted = runtime.Extract(characterId);
            if (!continuity.TryGetValue(characterId, out var retained))
                throw new InvalidOperationException("Tier 2 continuity state is missing.");
            return retained.WithProjection(ToWorld(extracted.Position), ToWorld(extracted.UnitsPerGameDay),
                extracted.IsMoving, extracted.ProcessedCalendarTicks, extracted.ProcessedBiologicalTicks,
                extracted.StepCount);
        }

        public void ValidateMaterialization(Character character, CharacterRuntimeState state)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Id != state.CharacterId)
                throw new ArgumentException("Character and Tier 2 state must share one identity.", nameof(state));
            if (IsActive(character.Id)) throw new InvalidOperationException("Tier 2 projection is already active.");
            CreateTransfer(character, state);
        }

        public void Materialize(Character character, CharacterRuntimeState state)
        {
            ValidateMaterialization(character, state);
            var transfer = CreateTransfer(character, state);
            runtime.Materializer.Materialize(transfer);
            continuity.Add(character.Id, state);
        }

        public void Dematerialize(StableEntityId characterId)
        {
            runtime.Dematerialize(characterId);
            continuity.Remove(characterId);
        }

        private static Tier2TransferState CreateTransfer(Character character, CharacterRuntimeState state)
        {
            var domain = character.CaptureState();
            return new Tier2TransferState(character.Id, domain.Sex, domain.BiologicalBirthTick, domain.LifeState,
                domain.BiologicalDeathTick, ToFloat3(state.Position), ToFloat3(state.UnitsPerGameDay), state.IsMoving,
                state.ProcessedCalendarTicks, state.ProcessedBiologicalTicks, state.StepCount);
        }

        private static float3 ToFloat3(WorldPosition value) => new float3(value.X, value.Y, value.Z);
        private static WorldPosition ToWorld(float3 value) => new WorldPosition(value.x, value.y, value.z);
    }
}
