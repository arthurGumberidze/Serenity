using UnityEngine;
using Game.Domain.World;

namespace Game.Presentation.Environment
{
    [DisallowMultipleComponent]
    public sealed class P102WorldPlacementSurface : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private float waterLevel = 0.15f;
        [SerializeField] private float maximumSlopeDegrees = 32f;
        private TiledWorldHeightSampler tiledWorld;
        public void Configure(Terrain value, float water, float maximumSlope)
        { terrain = value; tiledWorld = null; waterLevel = water; maximumSlopeDegrees = maximumSlope; }
        public void Configure(TiledWorldHeightSampler value, float maximumSlope = 32f)
        {
            tiledWorld = value;
            terrain = null;
            waterLevel = TiledWorldHeightSampler.WaterLevelMeters;
            maximumSlopeDegrees = maximumSlope;
        }
        public bool IsSuitable(Vector3 worldPoint)
        {
            if (tiledWorld != null) return tiledWorld.IsBuildable(worldPoint.x, worldPoint.z, maximumSlopeDegrees);
            if (terrain == null || terrain.terrainData == null) return false;
            var local = worldPoint - terrain.transform.position;
            var u = local.x / terrain.terrainData.size.x;
            var v = local.z / terrain.terrainData.size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
            var height = terrain.SampleHeight(worldPoint) + terrain.transform.position.y;
            return height > waterLevel + 0.25f && terrain.terrainData.GetSteepness(u, v) <= maximumSlopeDegrees;
        }
    }
}
