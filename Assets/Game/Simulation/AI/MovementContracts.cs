using Game.Domain;
using Game.Domain.AI;

namespace Game.Simulation.AI
{
    public enum MovementState
    {
        Idle = 0,
        Moving = 1,
        Arrived = 2,
        Failed = 3
    }

    public interface ITier1MovementDriver
    {
        StableEntityId CharacterId { get; }
        WorldPosition CurrentPosition { get; }
        MovementState State { get; }
        bool IsAvailable { get; }
        void MoveTo(WorldPosition target, float stoppingDistance);
        void Stop();
    }
}
