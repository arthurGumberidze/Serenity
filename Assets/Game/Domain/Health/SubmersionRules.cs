using System;
using Game.Domain.Characters;
namespace Game.Domain.Health
{
    public static class SubmersionRules
    {
        // No injury while the water is at or below the neck; damage only after the head is submerged.
        public static BodyHealth Damage(BodyHealth health, float depth, float headHeight, int points)
        {
            if (points < 0 || headHeight <= 0) throw new ArgumentOutOfRangeException(nameof(points));
            if (depth <= headHeight) return health;
            return new BodyHealth(new HealthValue(Math.Max(0,health.Head.Value-points)),
                new HealthValue(Math.Max(0,health.Torso.Value-points)),health.LeftArm,health.RightArm,
                health.LeftLeg,health.RightLeg);
        }
    }
}
