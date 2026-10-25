using System;
using UnityEngine;

namespace Game.Presentation.Environment
{
    // Shared presentation palette. Coordinates are in the authored 0..1000m map, not Unity instance IDs.
    [Serializable]
    public sealed class WorldMapArtDirection
    {
        [Serializable] public sealed class Region
        {
            public string id;
            public Vector2 center, radius;
            public Color foliage;
            public float haze;
        }
        public Region[] regions;
        public Vector4[] blossomGroves;
        public Color[] terrainTints;
        public Color shallowWater, deepWater, marshWater;
        public static WorldMapArtDirection Load()
        {
            var asset = UnityEngine.Resources.Load<TextAsset>("World/WorldMapArtDirection");
            if (asset == null) throw new InvalidOperationException("Missing map art direction.");
            var result = JsonUtility.FromJson<WorldMapArtDirection>(asset.text);
            if (result.regions == null || result.regions.Length != 8 ||
                result.blossomGroves == null || result.blossomGroves.Length != 3 ||
                result.terrainTints == null || result.terrainTints.Length != 7)
                throw new InvalidOperationException("Invalid map art direction.");
            return result;
        }
        public void ApplyShaderGlobals(Vector4 worldToDesign)
        {
            var shapes = new Vector4[8]; var colors = new Vector4[8];
            for (var i = 0; i < 8; i++)
            {
                var region = regions[i];
                shapes[i] = new Vector4(region.center.x, region.center.y, region.radius.x, region.radius.y);
                colors[i] = region.foliage;
            }
            Shader.SetGlobalVector("_WorldMapToDesign", worldToDesign);
            Shader.SetGlobalVectorArray("_WorldMapBiomeShapes", shapes);
            Shader.SetGlobalVectorArray("_WorldMapBiomeColors", colors);
            Shader.SetGlobalVectorArray("_WorldMapBlossomGroves", blossomGroves);
            Shader.SetGlobalColor("_WorldMapShallowWater", shallowWater);
            Shader.SetGlobalColor("_WorldMapDeepWater", deepWater);
            Shader.SetGlobalColor("_WorldMapMarshWater", marshWater);
            Shader.SetGlobalFloat("_WorldMapArtEnabled", 1);
        }
        public float Haze(Vector2 point)
        {
            float total = 0, haze = 0;
            foreach (var region in regions)
            {
                var delta = new Vector2((point.x - region.center.x) / region.radius.x,
                    (point.y - region.center.y) / region.radius.y);
                var weight = Mathf.Exp(-delta.sqrMagnitude * 2f);
                total += weight; haze += weight * region.haze;
            }
            return total > 0 ? haze / total : 1f;
        }
    }
}
