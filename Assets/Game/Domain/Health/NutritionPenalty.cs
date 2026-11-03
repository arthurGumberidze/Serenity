namespace Game.Domain.Health
{
    /// <summary>Balance rows are ordered from the most severe satiety threshold.</summary>
    public readonly struct NutritionPenalty
    {
        public NutritionPenalty(double satietyBelow, int work, int movement)
        { SatietyBelow = satietyBelow; WorkPerThousand = work; MovementPerThousand = movement; }
        public double SatietyBelow { get; }
        public int WorkPerThousand { get; }
        public int MovementPerThousand { get; }
        private static readonly NutritionPenalty[] Rules =
        {
            new NutritionPenalty(.10, 250, 800),
            new NutritionPenalty(.20, 500, 800),
            new NutritionPenalty(.30, 750, 1000)
        };
        public static NutritionPenalty ForHunger(double hunger)
        {
            foreach (var rule in Rules)
                if (hunger > 1d - rule.SatietyBelow + 1e-9) return rule;
            return new NutritionPenalty(0, 1000, 1000);
        }
    }
}
