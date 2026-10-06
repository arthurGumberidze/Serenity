using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    public readonly struct SimulationTimePoint
    {
        public SimulationTimePoint(long calendarTicks, long biologicalTicks)
        {
            if (calendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(calendarTicks));
            if (biologicalTicks < 0) throw new ArgumentOutOfRangeException(nameof(biologicalTicks));
            CalendarTicks = calendarTicks;
            BiologicalTicks = biologicalTicks;
        }

        public long CalendarTicks { get; }
        public long BiologicalTicks { get; }
    }

    public sealed class OffCameraSimulationSettings
    {
        public OffCameraSimulationSettings(long worldSeed, TimeSpan fixedStep)
        {
            if (fixedStep <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            WorldSeed = worldSeed;
            FixedStep = fixedStep;
        }

        public long WorldSeed { get; }
        public TimeSpan FixedStep { get; }
        public static OffCameraSimulationSettings ForWorld(long worldSeed) =>
            new OffCameraSimulationSettings(worldSeed, TimeSpan.FromDays(1));
    }

    public readonly struct OffCameraSimulationResult
    {
        public OffCameraSimulationResult(long startCalendarTick, long endCalendarTick, int processedEntities,
            ulong processedSteps, ulong stateHash, long activityProgressAdded)
        {
            StartCalendarTick = startCalendarTick;
            EndCalendarTick = endCalendarTick;
            ProcessedEntities = processedEntities;
            ProcessedSteps = processedSteps;
            StateHash = stateHash;
            ActivityProgressAdded = activityProgressAdded;
        }

        public long StartCalendarTick { get; }
        public long EndCalendarTick { get; }
        public int ProcessedEntities { get; }
        public ulong ProcessedSteps { get; }
        public ulong StateHash { get; }
        public long ActivityProgressAdded { get; }
    }

    /// <summary>
    /// Counter-keyed deterministic random source. A sample belongs to one world, character,
    /// absolute simulation step and stream, so collection traversal cannot reroll another character.
    /// </summary>
    public static class DeterministicKeyedRandom
    {
        public const uint AbstractActivityStream = 0x55313401u;

        public static ulong Sample64(long worldSeed, StableEntityId characterId, ulong absoluteStep, uint stream)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            return Sample64(worldSeed, IdentityKey(characterId), absoluteStep, stream);
        }

        internal static ulong IdentityKey(StableEntityId characterId)
        {
            var bytes = characterId.ToGuid().ToByteArray();
            var hash = 14695981039346656037UL;
            for (var i = 0; i < bytes.Length; i++) AddByte(ref hash, bytes[i]);
            return hash;
        }

        internal static ulong Sample64(long worldSeed, ulong identityKey, ulong absoluteStep, uint stream)
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, unchecked((ulong)worldSeed));
            Add(ref hash, identityKey);
            Add(ref hash, absoluteStep);
            Add(ref hash, stream);
            return Mix(hash);
        }

        internal static ulong Mix(ulong value)
        {
            value += 0x9e3779b97f4a7c15UL;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }

        internal static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(value >> shift));
        }

        internal static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
    }

    /// <summary>
    /// Engine-free batch/catch-up service for named Tier 3 characters. It advances only explicit
    /// long-horizon continuity; physical movement, jobs, resources, health and family remain canonical elsewhere.
    /// </summary>
    public sealed class OffCameraSimulationService
    {
        private readonly OffCameraSimulationSettings settings;

        public OffCameraSimulationService(OffCameraSimulationSettings simulationSettings)
        {
            settings = simulationSettings ?? throw new ArgumentNullException(nameof(simulationSettings));
        }

        public long WorldSeed => settings.WorldSeed;
        public TimeSpan FixedStep => settings.FixedStep;

        public CharacterRuntimeState AdvanceState(CharacterRuntimeState state, SimulationTimePoint target)
        {
            ValidateTarget(state, target);
            var firstStep = state.LastSimulationCalendarTick / settings.FixedStep.Ticks + 1L;
            var finalStep = target.CalendarTicks / settings.FixedStep.Ticks;
            var count = finalStep >= firstStep ? checked((ulong)(finalStep - firstStep + 1L)) : 0UL;
            var stepCount = checked(state.OffCameraStepCount + count);
            var accumulator = state.DeterministicAccumulator;
            var activity = state.AbstractActivityProgress;
            var identityKey = DeterministicKeyedRandom.IdentityKey(state.CharacterId);
            for (var step = firstStep; step <= finalStep; step++)
            {
                var sample = DeterministicKeyedRandom.Sample64(settings.WorldSeed, identityKey,
                    unchecked((ulong)step), DeterministicKeyedRandom.AbstractActivityStream);
                accumulator = DeterministicKeyedRandom.Mix(accumulator ^ sample ^ unchecked((ulong)step));
                activity = checked(activity + 1L + (long)(sample % 4UL));
            }
            return state.WithOffCameraSimulation(target.CalendarTicks, target.BiologicalTicks,
                stepCount, accumulator, activity);
        }

        public OffCameraSimulationResult Advance(IEnumerable<Tier3CharacterRecord> source,
            SimulationTimePoint target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var records = source.ToArray();
            Array.Sort(records, (left, right) => left.CharacterId.CompareTo(right.CharacterId));
            var seen = new HashSet<StableEntityId>();
            var start = records.Length == 0 ? target.CalendarTicks : long.MaxValue;
            ulong steps = 0;
            long activity = 0;
            var advanced = new CharacterRuntimeState[records.Length];
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i] ?? throw new ArgumentException("Tier 3 record cannot be null.", nameof(source));
                if (!seen.Add(record.CharacterId))
                    throw new InvalidOperationException("Duplicate Tier 3 character identity in simulation batch.");
                var before = record.State;
                if (before.LastSimulationCalendarTick < start) start = before.LastSimulationCalendarTick;
                var after = AdvanceState(before, target);
                steps = checked(steps + after.OffCameraStepCount - before.OffCameraStepCount);
                activity = checked(activity + after.AbstractActivityProgress - before.AbstractActivityProgress);
                advanced[i] = after;
            }
            for (var i = 0; i < records.Length; i++) records[i].Replace(advanced[i]);
            return new OffCameraSimulationResult(start, target.CalendarTicks, records.Length, steps,
                ComputeStateHash(advanced), activity);
        }

        public void ValidateTarget(CharacterRuntimeState state, SimulationTimePoint target)
        {
            if (target.CalendarTicks < state.LastSimulationCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(target), "Calendar simulation time cannot move backwards.");
            if (target.BiologicalTicks < state.LastSimulationBiologicalTick)
                throw new ArgumentOutOfRangeException(nameof(target), "Biological simulation time cannot move backwards.");
        }

        public ulong ComputeStateHash(IEnumerable<CharacterRuntimeState> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var states = source.OrderBy(x => x.CharacterId).ToArray();
            var hash = 14695981039346656037UL;
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)settings.WorldSeed));
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                var id = state.CharacterId.ToGuid().ToByteArray();
                for (var j = 0; j < id.Length; j++) DeterministicKeyedRandom.AddByte(ref hash, id[j]);
                for (var j = 0; j < state.LocationKey.Length; j++)
                    DeterministicKeyedRandom.Add(ref hash, state.LocationKey[j]);
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)state.LastSimulationCalendarTick));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)state.LastSimulationBiologicalTick));
                DeterministicKeyedRandom.Add(ref hash, state.OffCameraStepCount);
                DeterministicKeyedRandom.Add(ref hash, state.DeterministicAccumulator);
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)state.AbstractActivityProgress));
            }
            return hash;
        }
    }
}
