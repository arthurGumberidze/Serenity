using System;
using Game.Domain;
using Game.Domain.Characters;
using Unity.Mathematics;

namespace Game.ECS.Tier2
{
    /// <summary>
    /// Explicit transfer DTO between the canonical character boundary and its Tier 2 projection.
    /// It deliberately contains only state supported by the U12 runtime.
    /// </summary>
    public readonly struct Tier2TransferState
    {
        public StableEntityId CharacterId { get; }
        public CharacterSex Sex { get; }
        public long BiologicalBirthTick { get; }
        public CharacterLifeState LifeState { get; }
        public long? BiologicalDeathTick { get; }
        public float3 Position { get; }
        public float3 UnitsPerGameDay { get; }
        public bool IsMoving { get; }
        public long ProcessedCalendarTicks { get; }
        public long ProcessedBiologicalTicks { get; }
        public uint StepCount { get; }

        public Tier2TransferState(StableEntityId characterId, CharacterSex sex, long biologicalBirthTick,
            CharacterLifeState lifeState, long? biologicalDeathTick, float3 position, float3 unitsPerGameDay,
            bool isMoving, long processedCalendarTicks = 0, long processedBiologicalTicks = 0,
            uint stepCount = 0)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(characterId));
            if (!Enum.IsDefined(typeof(CharacterSex), sex)) throw new ArgumentOutOfRangeException(nameof(sex));
            if (!Enum.IsDefined(typeof(CharacterLifeState), lifeState)) throw new ArgumentOutOfRangeException(nameof(lifeState));
            if (biologicalBirthTick < 0) throw new ArgumentOutOfRangeException(nameof(biologicalBirthTick));
            if (lifeState == CharacterLifeState.Alive && biologicalDeathTick.HasValue)
                throw new ArgumentException("A living Tier 2 projection cannot have a death tick.", nameof(biologicalDeathTick));
            if (lifeState == CharacterLifeState.Dead &&
                (!biologicalDeathTick.HasValue || biologicalDeathTick.Value < biologicalBirthTick))
                throw new ArgumentException("A dead Tier 2 projection requires a valid death tick.", nameof(biologicalDeathTick));
            if (!math.all(math.isfinite(position))) throw new ArgumentOutOfRangeException(nameof(position));
            if (!math.all(math.isfinite(unitsPerGameDay))) throw new ArgumentOutOfRangeException(nameof(unitsPerGameDay));
            if (processedCalendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(processedCalendarTicks));
            if (processedBiologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(processedBiologicalTicks));

            CharacterId = characterId;
            Sex = sex;
            BiologicalBirthTick = biologicalBirthTick;
            LifeState = lifeState;
            BiologicalDeathTick = biologicalDeathTick;
            Position = position;
            UnitsPerGameDay = unitsPerGameDay;
            IsMoving = isMoving;
            ProcessedCalendarTicks = processedCalendarTicks;
            ProcessedBiologicalTicks = processedBiologicalTicks;
            StepCount = stepCount;
        }

        public static Tier2TransferState FromCharacter(Character character, float3 position,
            float3 unitsPerGameDay, bool isMoving)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            var state = character.CaptureState();
            return new Tier2TransferState(state.Id, state.Sex, state.BiologicalBirthTick, state.LifeState,
                state.BiologicalDeathTick, position, unitsPerGameDay, isMoving);
        }
    }
}
