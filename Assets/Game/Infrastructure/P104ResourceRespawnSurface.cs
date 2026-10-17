using Game.Domain.AI;
using Game.Domain.Production;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using Game.Presentation.Interaction;
using Game.Presentation.Production;
using Game.Simulation.Production;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Unity-side spatial query only; canonical respawn timing and choice stay in Simulation.</summary>
    public sealed class P104ResourceRespawnSurface : IResourceRespawnSurface
    {
        private readonly P102WorldPlacementSurface surface;

        public P104ResourceRespawnSurface(P102WorldPlacementSurface placementSurface)
        {
            surface = placementSurface;
        }

        public bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius)
        {
            var point = new Vector3(position.X, position.Y, position.Z);
            if (biome != NaturalResourceBiome.Lake && surface != null && !surface.IsSuitable(point)) return false;
            var overlaps = Physics.OverlapSphere(point + Vector3.up * 0.5f, footprintRadius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var collider = overlaps[i];
                if (collider is TerrainCollider || collider.GetComponentInParent<BuildableGround>() != null) continue;
                if (collider.GetComponentInParent<ProductionSourcePresenter>() != null ||
                    collider.GetComponentInParent<BuildingPresenter>() != null) return false;
            }
            return true;
        }
    }
}
