using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    /// <summary>
    /// Structural boundary for one representation tier. ValidateMaterialization must not activate
    /// a representation; TierManager calls it before releasing the current source.
    /// </summary>
    // Save restoration installs an exact checkpoint before the session clock is rebound.
    // Adapters that normally catch up must not advance against the departing session's clock.
    public interface ICharacterCheckpointAdapter
    {
        CharacterRuntimeState CaptureCheckpoint(StableEntityId characterId);
        void ValidateCheckpoint(Character character, CharacterRuntimeState state);
        void MaterializeCheckpoint(Character character, CharacterRuntimeState state);
    }

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
