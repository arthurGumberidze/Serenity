using System;
using System.Collections.Generic;
using Game.Domain.AI;

namespace Game.Simulation.AI
{
    public readonly struct UtilityScore : IComparable<UtilityScore>, IEquatable<UtilityScore>
    {
        public UtilityScore(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Utility must be finite.");
            Value = value <= 0d ? 0d : value >= 1d ? 1d : value;
        }

        public double Value { get; }
        public int CompareTo(UtilityScore other) => Value.CompareTo(other.Value);
        public bool Equals(UtilityScore other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is UtilityScore other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct UtilityCandidate
    {
        public UtilityCandidate(UtilityActionKind action, UtilityScore score, bool available, int tiePriority)
        {
            if (!Enum.IsDefined(typeof(UtilityActionKind), action)) throw new ArgumentOutOfRangeException(nameof(action));
            Action = action;
            Score = score;
            Available = available;
            TiePriority = tiePriority;
        }

        public UtilityActionKind Action { get; }
        public UtilityScore Score { get; }
        public bool Available { get; }
        public int TiePriority { get; }
    }

    public static class UtilitySelector
    {
        public static UtilityCandidate Select(IReadOnlyList<UtilityCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var found = false;
            var best = default(UtilityCandidate);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Available) continue;
                if (!found || IsBetter(candidate, best))
                {
                    found = true;
                    best = candidate;
                }
            }

            return found ? best : new UtilityCandidate(UtilityActionKind.Idle, new UtilityScore(0d), true, int.MaxValue);
        }

        public static UtilityCandidate Select(UtilityCandidate first, UtilityCandidate second, UtilityCandidate third)
        {
            var best = first.Available ? first : second.Available ? second : third;
            if (second.Available && IsBetter(second, best)) best = second;
            if (third.Available && IsBetter(third, best)) best = third;
            return best.Available ? best : new UtilityCandidate(UtilityActionKind.Idle, new UtilityScore(0d), true, int.MaxValue);
        }

        public static bool ShouldSwitch(UtilityCandidate current, UtilityCandidate proposed, double margin,
            bool currentValid, bool currentInterruptible)
        {
            if (!currentValid) return true;
            if (!currentInterruptible || proposed.Action == current.Action) return false;
            return proposed.Score.Value > current.Score.Value + margin;
        }

        private static bool IsBetter(UtilityCandidate candidate, UtilityCandidate best) =>
            candidate.Score.Value > best.Score.Value ||
            (candidate.Score.Value.Equals(best.Score.Value) && candidate.TiePriority < best.TiePriority) ||
            (candidate.Score.Value.Equals(best.Score.Value) && candidate.TiePriority == best.TiePriority &&
             candidate.Action < best.Action);
    }
}
