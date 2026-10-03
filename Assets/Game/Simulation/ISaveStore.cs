using Game.Domain;

namespace Game.Simulation
{
    /// <summary>Synchronous checkpoint port. Writes replace a slot atomically or throw; reads never mutate runtime.</summary>
    public interface ISaveStore
    {
        void Save(StableEntityId slotId, SaveSnapshot snapshot);
        SaveSnapshot Load(StableEntityId slotId);
    }
}
