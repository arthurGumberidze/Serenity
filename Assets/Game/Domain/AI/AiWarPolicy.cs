using System;

namespace Game.Domain.AI
{
    /// <summary>Strategic constraints, independent of presentation and the decision interval.</summary>
    public sealed class AiWarPolicy
    {
        public AiWarPolicy(long developmentTicks, long warningTicks, double minimumStrengthRatio,
            int hostileRelationThreshold, int civilianReserve, bool requireTraining = true)
        {
            if (developmentTicks < 0 || warningTicks < 0 || minimumStrengthRatio <= 0 ||
                double.IsNaN(minimumStrengthRatio) || double.IsInfinity(minimumStrengthRatio) ||
                hostileRelationThreshold < -100 || hostileRelationThreshold > 100 || civilianReserve < 0)
                throw new ArgumentOutOfRangeException();
            DevelopmentTicks = developmentTicks; WarningTicks = warningTicks;
            MinimumStrengthRatio = minimumStrengthRatio; HostileRelationThreshold = hostileRelationThreshold;
            CivilianReserve = civilianReserve; RequireTraining = requireTraining;
        }
        public long DevelopmentTicks { get; }
        public long WarningTicks { get; }
        public double MinimumStrengthRatio { get; }
        public int HostileRelationThreshold { get; }
        public int CivilianReserve { get; }
        public bool RequireTraining { get; }
        public static AiWarPolicy SettlementFirst => new AiWarPolicy(TimeSpan.FromDays(7).Ticks,
            TimeSpan.FromDays(1).Ticks, 1.25, -35, 3);
    }
}
