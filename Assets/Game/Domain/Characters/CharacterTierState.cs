using System;
using Game.Domain.AI;

namespace Game.Domain.Characters
{
    public enum CharacterSimulationTier
    {
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    /// <summary>
    /// Representation-neutral mutable continuity state. Durable biography, family, health,
    /// traits, skills, profession and relationships remain owned by Character.
    /// </summary>
    public readonly struct CharacterRuntimeState : IEquatable<CharacterRuntimeState>
    {
        public CharacterRuntimeState(StableEntityId characterId, WorldPosition position,
            WorldPosition unitsPerGameDay, bool isMoving, string locationKey = "local",
            long processedCalendarTicks = 0, long processedBiologicalTicks = 0, uint stepCount = 0,
            double hunger = 0d, double energy = 1d)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(locationKey) || locationKey != locationKey.Trim() || locationKey.Length > 64)
                throw new ArgumentException("Location key must contain 1-64 trimmed characters.", nameof(locationKey));
            if (processedCalendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(processedCalendarTicks));
            if (processedBiologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(processedBiologicalTicks));
            if (double.IsNaN(hunger) || double.IsInfinity(hunger) || hunger < 0d || hunger > 1d)
                throw new ArgumentOutOfRangeException(nameof(hunger));
            if (double.IsNaN(energy) || double.IsInfinity(energy) || energy < 0d || energy > 1d)
                throw new ArgumentOutOfRangeException(nameof(energy));

            CharacterId = characterId;
            Position = position;
            UnitsPerGameDay = unitsPerGameDay;
            IsMoving = isMoving;
            LocationKey = locationKey;
            ProcessedCalendarTicks = processedCalendarTicks;
            ProcessedBiologicalTicks = processedBiologicalTicks;
            StepCount = stepCount;
            Hunger = hunger;
            Energy = energy;
        }

        public StableEntityId CharacterId { get; }
        public WorldPosition Position { get; }
        public WorldPosition UnitsPerGameDay { get; }
        public bool IsMoving { get; }
        public string LocationKey { get; }
        public long ProcessedCalendarTicks { get; }
        public long ProcessedBiologicalTicks { get; }
        public uint StepCount { get; }
        public double Hunger { get; }
        public double Energy { get; }

        public CharacterRuntimeState WithProjection(WorldPosition position, WorldPosition unitsPerGameDay,
            bool isMoving, long processedCalendarTicks, long processedBiologicalTicks, uint stepCount) =>
            new CharacterRuntimeState(CharacterId, position, unitsPerGameDay, isMoving, LocationKey,
                processedCalendarTicks, processedBiologicalTicks, stepCount, Hunger, Energy);

        public CharacterRuntimeState WithTier1State(WorldPosition position, double hunger, double energy) =>
            new CharacterRuntimeState(CharacterId, position, default, false, LocationKey,
                ProcessedCalendarTicks, ProcessedBiologicalTicks, StepCount, hunger, energy);

        public bool Equals(CharacterRuntimeState other) => CharacterId == other.CharacterId &&
            Position.Equals(other.Position) && UnitsPerGameDay.Equals(other.UnitsPerGameDay) &&
            IsMoving == other.IsMoving && string.Equals(LocationKey, other.LocationKey, StringComparison.Ordinal) &&
            ProcessedCalendarTicks == other.ProcessedCalendarTicks &&
            ProcessedBiologicalTicks == other.ProcessedBiologicalTicks && StepCount == other.StepCount &&
            Hunger.Equals(other.Hunger) && Energy.Equals(other.Energy);

        public override bool Equals(object obj) => obj is CharacterRuntimeState other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CharacterId.GetHashCode();
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ UnitsPerGameDay.GetHashCode();
                hash = (hash * 397) ^ IsMoving.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(LocationKey);
                hash = (hash * 397) ^ ProcessedCalendarTicks.GetHashCode();
                hash = (hash * 397) ^ ProcessedBiologicalTicks.GetHashCode();
                hash = (hash * 397) ^ StepCount.GetHashCode();
                hash = (hash * 397) ^ Hunger.GetHashCode();
                return (hash * 397) ^ Energy.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Tier 3 named-person representation. It references the canonical Character by stable ID and
    /// stores only the lightweight state needed to materialize a detailed representation again.
    /// U14 owns advancement of this record.
    /// </summary>
    public sealed class Tier3CharacterRecord
    {
        public Tier3CharacterRecord(CharacterRuntimeState state) { State = state; }
        public StableEntityId CharacterId => State.CharacterId;
        public CharacterRuntimeState State { get; private set; }
        public void Replace(CharacterRuntimeState state)
        {
            if (state.CharacterId != CharacterId)
                throw new ArgumentException("Tier 3 replacement must preserve character identity.", nameof(state));
            State = state;
        }
    }
}
