using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    /// <summary>
    /// Structural boundary for one representation tier. ValidateMaterialization must not activate
    /// a representation; TierManager calls it before releasing the current source.
    /// </summary>
    public interface ICharacterTierAdapter
    {
        CharacterSimulationTier Tier { get; }
        bool IsActive(StableEntityId characterId);
        CharacterRuntimeState Capture(StableEntityId characterId);
        void ValidateMaterialization(Character character, CharacterRuntimeState state);
        void Materialize(Character character, CharacterRuntimeState state);
        void Dematerialize(StableEntityId characterId);
    }
}
