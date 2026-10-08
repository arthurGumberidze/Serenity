using System;
using Game.Domain;
using Game.Domain.AI;
using Game.Presentation.AI;
using Game.Simulation.Combat;

namespace Game.Presentation.Combat
{
    /// <summary>Disposable Tier 1 physical adapter; canonical combat never stores a NavMeshAgent or GameObject.</summary>
    public sealed class Tier1CombatPositionProvider : ICombatPositionProvider
    {
        private readonly Tier1AiRuntimeDriver runtime;

        public Tier1CombatPositionProvider(Tier1AiRuntimeDriver aiRuntime)
        {
            runtime = aiRuntime != null ? aiRuntime : throw new ArgumentNullException(nameof(aiRuntime));
        }

        public bool TryGetPosition(StableEntityId characterId, out WorldPosition position)
        {
            if (runtime.TryGetMovement(characterId, out var movement) && movement.IsAvailable)
            {
                position = movement.CurrentPosition;
                return true;
            }
            position = default;
            return false;
        }

        public void MoveTo(StableEntityId characterId, WorldPosition target, float stoppingDistance)
        {
            if (runtime.TryGetMovement(characterId, out var movement)) movement.MoveTo(target, stoppingDistance);
        }

        public void Stop(StableEntityId characterId)
        {
            if (runtime.TryGetMovement(characterId, out var movement)) movement.Stop();
        }
    }
}
