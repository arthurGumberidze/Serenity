using System;
using System.Collections.Generic;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public sealed class WildlifeEscapePolicy
    {
        public WildlifeEscapePolicy(float speed, float distance, long durationTicks)
        {
            if (speed <= 0 || distance <= 0 || durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed; Distance = distance; DurationTicks = durationTicks;
        }
        public float Speed { get; }
        public float Distance { get; }
        public long DurationTicks { get; }
    }

    public static class WildlifeEscape
    {
        // Stable seeded variation is reproducible across saves, but changes with every hit/replan.
        public static IEnumerable<WorldPosition> Candidates(WildlifeIndividualState animal,
            WildlifeEscapePolicy policy, int attempt)
        {
            uint seed = unchecked((uint)(animal.FleeSequence * 7919 + attempt * 104729));
            foreach (var b in animal.Id.ToGuid().ToByteArray()) seed = unchecked((seed ^ b) * 16777619u);
            var random = new Random(unchecked((int)seed));
            var away = Math.Atan2(animal.Position.Z-animal.ThreatPosition.Z, animal.Position.X-animal.ThreatPosition.X);
            for (var i=0; i<12; i++)
            {
                var cone = i<8 ? Math.PI * .85 : Math.PI * 2;
                var angle = away + (random.NextDouble()-.5)*cone;
                var distance = policy.Distance * (.55 + random.NextDouble()*.45);
                yield return new WorldPosition(animal.Position.X+(float)(Math.Cos(angle)*distance), animal.Position.Y,
                    animal.Position.Z+(float)(Math.Sin(angle)*distance));
            }
        }
    }
}
