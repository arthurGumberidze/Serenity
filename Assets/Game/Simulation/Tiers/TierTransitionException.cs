using System;
using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    public sealed class TierTransitionException : InvalidOperationException
    {
        public TierTransitionException(StableEntityId characterId, CharacterSimulationTier source,
            CharacterSimulationTier target, bool sourceRestored, Exception cause, Exception rollbackFailure = null)
            : base(BuildMessage(characterId, source, target, sourceRestored),
                rollbackFailure == null ? cause : new AggregateException(cause, rollbackFailure))
        {
            CharacterId = characterId;
            SourceTier = source;
            TargetTier = target;
            SourceRestored = sourceRestored;
        }

        public StableEntityId CharacterId { get; }
        public CharacterSimulationTier SourceTier { get; }
        public CharacterSimulationTier TargetTier { get; }
        public bool SourceRestored { get; }

        private static string BuildMessage(StableEntityId id, CharacterSimulationTier source,
            CharacterSimulationTier target, bool restored) => restored
                ? $"Transition {source}->{target} failed for {id}; the source representation was restored."
                : $"Transition {source}->{target} failed for {id}, and source restoration also failed.";
    }
}
