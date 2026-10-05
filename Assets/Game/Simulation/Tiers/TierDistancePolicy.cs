using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    public sealed class TierDistancePolicySettings
    {
        public TierDistancePolicySettings(float tier1EnterDistance, float tier1ExitDistance,
            float tier3ExitDistance, float tier3EnterDistance, int evaluationBudget,
            int transitionBudget, int minimumResidencyEvaluations)
        {
            if (tier1EnterDistance < 0f || tier1ExitDistance <= tier1EnterDistance ||
                tier3ExitDistance <= tier1ExitDistance || tier3EnterDistance <= tier3ExitDistance)
                throw new ArgumentException("Tier thresholds must define ordered hysteresis bands.");
            if (evaluationBudget <= 0) throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
            if (transitionBudget <= 0 || transitionBudget > evaluationBudget)
                throw new ArgumentOutOfRangeException(nameof(transitionBudget));
            if (minimumResidencyEvaluations < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumResidencyEvaluations));
            Tier1EnterDistance = tier1EnterDistance;
            Tier1ExitDistance = tier1ExitDistance;
            Tier3ExitDistance = tier3ExitDistance;
            Tier3EnterDistance = tier3EnterDistance;
            EvaluationBudget = evaluationBudget;
            TransitionBudget = transitionBudget;
            MinimumResidencyEvaluations = minimumResidencyEvaluations;
        }

        public float Tier1EnterDistance { get; }
        public float Tier1ExitDistance { get; }
        public float Tier3ExitDistance { get; }
        public float Tier3EnterDistance { get; }
        public int EvaluationBudget { get; }
        public int TransitionBudget { get; }
        public int MinimumResidencyEvaluations { get; }
        public static TierDistancePolicySettings Default => new TierDistancePolicySettings(18f, 24f, 64f, 80f, 32, 4, 2);
    }

    /// <summary>Deterministic camera-distance policy. It decides tiers but never advances Tier 3 simulation.</summary>
    public sealed class TierDistancePolicy
    {
        private readonly TierManager manager;
        private readonly TierDistancePolicySettings settings;
        private readonly Dictionary<StableEntityId, int> residency = new Dictionary<StableEntityId, int>();
        private int cursor;

        public TierDistancePolicy(TierManager tierManager, TierDistancePolicySettings policySettings = null)
        {
            manager = tierManager ?? throw new ArgumentNullException(nameof(tierManager));
            settings = policySettings ?? TierDistancePolicySettings.Default;
        }

        public int LastEvaluated { get; private set; }
        public int LastTransitioned { get; private set; }
        public int LastFailures { get; private set; }

        public void Evaluate(WorldPosition focus)
        {
            LastEvaluated = LastTransitioned = LastFailures = 0;
            var ids = manager.ManagedIds;
            if (ids.Count == 0) { cursor = 0; return; }
            var count = Math.Min(settings.EvaluationBudget, ids.Count);
            for (var i = 0; i < count; i++)
            {
                var id = ids[cursor];
                cursor = (cursor + 1) % ids.Count;
                LastEvaluated++;
                residency.TryGetValue(id, out var age);
                if (age < settings.MinimumResidencyEvaluations)
                {
                    residency[id] = age + 1;
                    continue;
                }
                if (LastTransitioned >= settings.TransitionBudget) continue;
                try
                {
                    var current = manager.GetTier(id);
                    var position = manager.GetRuntimeState(id).Position;
                    var target = Choose(current, Math.Sqrt(position.DistanceSquared(focus)));
                    if (target == current) continue;
                    manager.Transition(id, target);
                    residency[id] = 0;
                    LastTransitioned++;
                }
                catch (InvalidOperationException)
                {
                    // A legacy/external presentation operation may temporarily remove a view outside
                    // TierManager. Keep the frame safe and retry deterministically on a later evaluation.
                    LastFailures++;
                }
            }
        }

        private CharacterSimulationTier Choose(CharacterSimulationTier current, double distance)
        {
            switch (current)
            {
                case CharacterSimulationTier.Tier1:
                    if (distance >= settings.Tier3EnterDistance) return CharacterSimulationTier.Tier3;
                    if (distance >= settings.Tier1ExitDistance) return CharacterSimulationTier.Tier2;
                    return current;
                case CharacterSimulationTier.Tier2:
                    if (distance <= settings.Tier1EnterDistance) return CharacterSimulationTier.Tier1;
                    if (distance >= settings.Tier3EnterDistance) return CharacterSimulationTier.Tier3;
                    return current;
                case CharacterSimulationTier.Tier3:
                    if (distance <= settings.Tier1EnterDistance) return CharacterSimulationTier.Tier1;
                    if (distance <= settings.Tier3ExitDistance) return CharacterSimulationTier.Tier2;
                    return current;
                default:
                    throw new ArgumentOutOfRangeException(nameof(current));
            }
        }
    }
}
