using System;

namespace Game.Simulation.Time
{
    public sealed class GameTimeSettings
    {
        public const int DefaultBiologicalMultiplier = 400;

        public int BiologicalMultiplier { get; }

        public GameTimeSettings(int biologicalMultiplier = DefaultBiologicalMultiplier)
        {
            if (biologicalMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(biologicalMultiplier));

            BiologicalMultiplier = biologicalMultiplier;
        }
    }
}
