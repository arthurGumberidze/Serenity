using UnityEngine;

namespace Game.Presentation.Environment
{
    [DisallowMultipleComponent]
    public sealed class P102WorldPlacementSurface : MonoBehaviour
    {
        [SerializeField] private Terrain terrain;
        [SerializeField] private float waterLevel = 0.15f;
        [SerializeField] private float maximumSlopeDegrees = 32f;
        public void Configure(Terrain value, float water, float maximumSlope)
        { terrain = value; waterLevel = water; maximumSlopeDegrees = maximumSlope; }
        public bool IsSuitable(Vector3 worldPoint)
        {
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
