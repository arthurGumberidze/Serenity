using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.World;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.ECS.Tier2
{
    /// <summary>Wildlife adapter for the existing Tier2Runtime World; canonical state stays in Domain.</summary>
    public sealed class Tier2WildlifeMaterializer
    {
        private readonly World world;
        private readonly Dictionary<StableEntityId, Entity> entities = new Dictionary<StableEntityId, Entity>();

        public Tier2WildlifeMaterializer(World tier2World)
        {
            world = tier2World ?? throw new ArgumentNullException(nameof(tier2World));
        }

        public int EntityCount => entities.Count;
        public IReadOnlyList<StableEntityId> ActiveIds => entities.Keys.OrderBy(value => value).ToArray();
        public bool IsActive(StableEntityId id) => entities.ContainsKey(id);

        public void Materialize(WildlifeIndividualState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (entities.ContainsKey(state.Id))
                throw new InvalidOperationException("Wildlife Tier2 entity is already active.");
            var entity = world.EntityManager.CreateEntity(typeof(Tier2StableIdentity), typeof(Tier2Position),
                typeof(Tier2WildlifeState));
            world.EntityManager.SetComponentData(entity, Tier2StableIdentity.FromDomain(state.Id));
            world.EntityManager.SetComponentData(entity, new Tier2Position
                { Value = new float3(state.Position.X, state.Position.Y, state.Position.Z) });
            world.EntityManager.SetComponentData(entity, new Tier2WildlifeState
            {
                SpeciesHash = StableHash(state.SpeciesId), Sex = state.Sex, BirthDay = state.BirthDay
            });
            entities.Add(state.Id, entity);
        }

        public void Refresh(WildlifeIndividualState state)
        {
            if (!entities.TryGetValue(state.Id, out var entity))
                throw new KeyNotFoundException("Wildlife Tier2 entity is not active.");
            world.EntityManager.SetComponentData(entity, new Tier2Position
                { Value = new float3(state.Position.X, state.Position.Y, state.Position.Z) });
        }

        public void Dematerialize(StableEntityId id)
        {
            if (!entities.TryGetValue(id, out var entity)) return;
            if (world.IsCreated && world.EntityManager.Exists(entity)) world.EntityManager.DestroyEntity(entity);
            entities.Remove(id);
        }

        public bool TryGetEntity(StableEntityId id, out Entity entity) => entities.TryGetValue(id, out entity);
        public void Clear() { foreach (var id in ActiveIds) Dematerialize(id); }

        private static int StableHash(string value)
        {
            unchecked { var hash = 17; foreach (var character in value) hash = hash * 31 + character; return hash; }
        }
    }
}
