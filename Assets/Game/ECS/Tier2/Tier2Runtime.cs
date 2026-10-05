using System;
using Game.Domain;
using Game.Simulation.Time;
using Unity.Entities;

namespace Game.ECS.Tier2
{
    /// <summary>
    /// U12-owned explicit world bootstrap. It is intentionally not attached to the default player loop;
    /// U13 will own automatic tier transitions and runtime composition.
    /// </summary>
    public sealed class Tier2Runtime : IDisposable
    {
        private readonly Entity stepEntity;
        private readonly Tier2SimulationSystem simulationSystem;
        private uint sequence;
        private bool disposed;

        public World World { get; }
        public Tier2Materializer Materializer { get; }
        public int LastScheduledEntityCount => simulationSystem.LastScheduledEntityCount;

        public Tier2Runtime(string worldName = "Serenity Tier 2")
        {
            World = new World(worldName);
            stepEntity = World.EntityManager.CreateEntity(typeof(Tier2SimulationStep));
            simulationSystem = World.GetOrCreateSystemManaged<Tier2SimulationSystem>();
            Materializer = new Tier2Materializer(World);
        }

        public void Step(GameTimeAdvance advance)
        {
            ThrowIfDisposed();
            if (advance.CalendarDelta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(advance));
            if (advance.BiologicalDelta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(advance));
            sequence++;
            World.EntityManager.SetComponentData(stepEntity, new Tier2SimulationStep
            {
                CalendarDeltaTicks = advance.CalendarDelta.Ticks,
                BiologicalDeltaTicks = advance.BiologicalDelta.Ticks,
                Sequence = sequence
            });
            simulationSystem.Update();
            World.EntityManager.CompleteAllTrackedJobs();
            World.EntityManager.SetComponentData(stepEntity, new Tier2SimulationStep { Sequence = sequence });
        }

        public Tier2TransferState Extract(StableEntityId characterId)
        {
            ThrowIfDisposed();
            World.EntityManager.CompleteAllTrackedJobs();
            return Materializer.Extract(characterId);
        }

        public void Dematerialize(StableEntityId characterId)
        {
            ThrowIfDisposed();
            World.EntityManager.CompleteAllTrackedJobs();
            Materializer.Dematerialize(characterId);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (World.IsCreated) World.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(Tier2Runtime));
        }
    }
}
