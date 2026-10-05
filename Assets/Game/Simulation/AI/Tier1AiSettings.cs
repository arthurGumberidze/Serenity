using System;

namespace Game.Simulation.AI
{
    public sealed class Tier1AiSettings
    {
        public Tier1AiSettings(TimeSpan thinkInterval, TimeSpan needUpdateInterval, TimeSpan movementTimeout,
            int thinkBatchSize, int needBatchSize, int actionBatchSize, int staggerSlots,
            float movementSpeed, long maxHaulUnits, double switchMargin,
            double hungerIncreasePerDay, double energyDecreasePerDay, double restRecoveryPerDay,
            double restCompletionEnergy = 0.95d)
        {
            if (thinkInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(thinkInterval));
            if (needUpdateInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(needUpdateInterval));
            if (movementTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(movementTimeout));
            if (thinkBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(thinkBatchSize));
            if (needBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(needBatchSize));
            if (actionBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(actionBatchSize));
            if (staggerSlots <= 0) throw new ArgumentOutOfRangeException(nameof(staggerSlots));
            if (movementSpeed <= 0f || float.IsNaN(movementSpeed) || float.IsInfinity(movementSpeed))
                throw new ArgumentOutOfRangeException(nameof(movementSpeed));
            if (maxHaulUnits <= 0) throw new ArgumentOutOfRangeException(nameof(maxHaulUnits));
            ValidateUnit(switchMargin, nameof(switchMargin));
            ValidateRate(hungerIncreasePerDay, nameof(hungerIncreasePerDay));
            ValidateRate(energyDecreasePerDay, nameof(energyDecreasePerDay));
            ValidateRate(restRecoveryPerDay, nameof(restRecoveryPerDay));
            ValidateUnit(restCompletionEnergy, nameof(restCompletionEnergy));

            ThinkInterval = thinkInterval;
            NeedUpdateInterval = needUpdateInterval;
            MovementTimeout = movementTimeout;
            ThinkBatchSize = thinkBatchSize;
            NeedBatchSize = needBatchSize;
            ActionBatchSize = actionBatchSize;
            StaggerSlots = staggerSlots;
            MovementSpeed = movementSpeed;
            MaxHaulUnits = maxHaulUnits;
            SwitchMargin = switchMargin;
            HungerIncreasePerDay = hungerIncreasePerDay;
            EnergyDecreasePerDay = energyDecreasePerDay;
            RestRecoveryPerDay = restRecoveryPerDay;
            RestCompletionEnergy = restCompletionEnergy;
        }

        public TimeSpan ThinkInterval { get; }
        public TimeSpan NeedUpdateInterval { get; }
        public TimeSpan MovementTimeout { get; }
        public int ThinkBatchSize { get; }
        public int NeedBatchSize { get; }
        public int ActionBatchSize { get; }
        public int StaggerSlots { get; }
        public float MovementSpeed { get; }
        public long MaxHaulUnits { get; }
        public double SwitchMargin { get; }
        public double HungerIncreasePerDay { get; }
        public double EnergyDecreasePerDay { get; }
        public double RestRecoveryPerDay { get; }
        public double RestCompletionEnergy { get; }

        public static Tier1AiSettings Default => new Tier1AiSettings(
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(15),
            5, 20, 25, 100, 2.5f, 4, 0.15d,
            0.35d, 0.45d, 360d);

        private static void ValidateUnit(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidateRate(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
