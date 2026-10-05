using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Characters;
using Unity.Collections;
using Unity.Entities;

namespace Game.ECS.Tier2
{
    /// <summary>
    /// The only U12 creation/extraction boundary. The managed index is outside hot ECS components
    /// and enforces one active Tier 2 projection for each canonical stable identity.
    /// </summary>
    public sealed class Tier2Materializer
    {
        private readonly EntityManager entityManager;
        private readonly EntityArchetype archetype;
        private readonly EntityQuery identityQuery;
        private readonly Dictionary<StableEntityId, Entity> entitiesById = new Dictionary<StableEntityId, Entity>();

        public Tier2Materializer(World world)
        {
            if (world == null || !world.IsCreated) throw new ArgumentException("A created ECS world is required.", nameof(world));
            entityManager = world.EntityManager;
            archetype = entityManager.CreateArchetype(
                typeof(Tier2StableIdentity), typeof(Tier2BiologicalState), typeof(Tier2Position),
                typeof(Tier2Movement), typeof(Tier2SimulationProgress));
            identityQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Tier2StableIdentity>());
            IndexExistingEntities();
        }

        public int EntityCount => identityQuery.CalculateEntityCount();

        public Entity Materialize(in Tier2TransferState state)
        {
            ValidateNewIdentity(state.CharacterId);
            var entity = entityManager.CreateEntity(archetype);
            Write(entity, state);
            entitiesById.Add(state.CharacterId, entity);
            return entity;
        }

        public void Materialize(IReadOnlyList<Tier2TransferState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var pending = new HashSet<StableEntityId>();
            for (var i = 0; i < states.Count; i++)
            {
                ValidateNewIdentity(states[i].CharacterId);
                if (!pending.Add(states[i].CharacterId))
                    throw new InvalidOperationException("A Tier 2 batch cannot contain duplicate stable identities.");
            }

            using (var entities = new NativeArray<Entity>(states.Count, Allocator.Temp))
            {
                entityManager.CreateEntity(archetype, entities);
                for (var i = 0; i < states.Count; i++)
                {
                    Write(entities[i], states[i]);
                    entitiesById.Add(states[i].CharacterId, entities[i]);
                }
            }
        }

        public bool TryGetEntity(StableEntityId characterId, out Entity entity)
        {
            if (entitiesById.TryGetValue(characterId, out entity) && entityManager.Exists(entity)) return true;
            entitiesById.Remove(characterId);
            entity = Entity.Null;
            return false;
        }

        public Tier2TransferState Extract(StableEntityId characterId)
        {
            if (!TryGetEntity(characterId, out var entity))
                throw new KeyNotFoundException("No active Tier 2 projection exists for the character ID.");
            return Extract(entity);
        }

        public Tier2TransferState Extract(Entity entity)
        {
            if (!entityManager.Exists(entity) || !entityManager.HasComponent<Tier2StableIdentity>(entity))
                throw new ArgumentException("Entity is not an active Tier 2 character projection.", nameof(entity));
            var identity = entityManager.GetComponentData<Tier2StableIdentity>(entity).ToDomain();
            var biology = entityManager.GetComponentData<Tier2BiologicalState>(entity);
            var position = entityManager.GetComponentData<Tier2Position>(entity);
            var movement = entityManager.GetComponentData<Tier2Movement>(entity);
            var progress = entityManager.GetComponentData<Tier2SimulationProgress>(entity);
            return new Tier2TransferState(identity, biology.Sex, biology.BirthTick, biology.LifeState,
                biology.LifeState == CharacterLifeState.Dead ? biology.DeathTick : (long?)null,
                position.Value, movement.UnitsPerGameDay, movement.IsMoving != 0,
                progress.ProcessedCalendarTicks, progress.ProcessedBiologicalTicks, progress.StepCount);
        }

        public void Dematerialize(StableEntityId characterId)
        {
            if (!TryGetEntity(characterId, out var entity))
                throw new KeyNotFoundException("No active Tier 2 projection exists for the character ID.");
            entityManager.DestroyEntity(entity);
            entitiesById.Remove(characterId);
        }

        private void ValidateNewIdentity(StableEntityId id)
        {
            if (!id.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(id));
            if (entitiesById.ContainsKey(id))
                throw new InvalidOperationException("A Tier 2 projection already exists for this stable identity.");
        }

        private void Write(Entity entity, in Tier2TransferState state)
        {
            entityManager.SetComponentData(entity, Tier2StableIdentity.FromDomain(state.CharacterId));
            entityManager.SetComponentData(entity, new Tier2BiologicalState
            {
                BirthTick = state.BiologicalBirthTick,
                DeathTick = state.BiologicalDeathTick.GetValueOrDefault(),
                Sex = state.Sex,
                LifeState = state.LifeState
            });
            entityManager.SetComponentData(entity, new Tier2Position { Value = state.Position });
            entityManager.SetComponentData(entity, new Tier2Movement
            {
                UnitsPerGameDay = state.UnitsPerGameDay,
                IsMoving = state.IsMoving ? (byte)1 : (byte)0
            });
            entityManager.SetComponentData(entity, new Tier2SimulationProgress
            {
                ProcessedCalendarTicks = state.ProcessedCalendarTicks,
                ProcessedBiologicalTicks = state.ProcessedBiologicalTicks,
                StepCount = state.StepCount
            });
        }

        private void IndexExistingEntities()
        {
            using (var entities = identityQuery.ToEntityArray(Allocator.Temp))
            using (var identities = identityQuery.ToComponentDataArray<Tier2StableIdentity>(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var id = identities[i].ToDomain();
                    if (entitiesById.ContainsKey(id))
                        throw new InvalidOperationException("The ECS world already contains duplicate Tier 2 identities.");
                    entitiesById.Add(id, entities[i]);
                }
            }
        }
    }
}
