using System;
using System.Linq;
using Game.Domain;
using Game.Domain.World;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Disposable Terrain projection of the canonical WM05 plan; owns no mutable vegetation state.</summary>
    public static class WorldMapVegetationTerrainProjector
    {
        public static void Apply(TerrainData data, WorldTileId tile, TiledWorldDefinition definition,
            WorldMapVegetationPlan plan, StarterNatureCatalog catalog)
        {
            if (data == null || definition == null || plan == null || catalog == null)
                throw new ArgumentNullException(nameof(data));
            // Imported LODGroup prefabs are streamed as bounded GameObject views by TiledTerrainWorld.
            // Unity Terrain tree prototypes do not reliably render these multi-LOD prefabs in Player.
            data.treePrototypes = Array.Empty<TreePrototype>();
            var origin = definition.TileOrigin(tile);

            var ground = catalog.WorldMapGroundCover
                .Concat(catalog.WorldMapSmallProps ?? Array.Empty<GameObject>()).Distinct().ToArray();
            data.SetDetailResolution(128, 16);
            if (!Application.isEditor)
            {
                // These audited source meshes/textures are intentionally non-readable. Unity's Player terrain
                // detail backend rejects them and opens the Development Console, while tree/resource projection
                // already supplies the visible V2 vegetation. Keep the invalid detail route Editor-only.
                data.detailPrototypes = Array.Empty<DetailPrototype>();
                return;
            }
            data.detailPrototypes = ground.Select(value => new DetailPrototype
            {
                prototype = value,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = 0.72f,
                maxWidth = 1.18f,
                minHeight = 0.72f,
                maxHeight = 1.18f,
                noiseSpread = 0.12f
            }).ToArray();
            var maps = Enumerable.Range(0, ground.Length)
                .Select(_ => new int[data.detailResolution, data.detailResolution]).ToArray();
            foreach (var value in plan.ForTile(tile).Where(value =>
                         value.Kind == WorldMapVegetationKind.GroundCover))
            {
                var x = Mathf.Clamp(Mathf.FloorToInt((value.Position.X - origin.X) /
                    definition.TileSizeMeters * data.detailResolution), 0, data.detailResolution - 1);
                var z = Mathf.Clamp(Mathf.FloorToInt((value.Position.Z - origin.Z) /
                    definition.TileSizeMeters * data.detailResolution), 0, data.detailResolution - 1);
                var prototype = StableIndex(value.StableId, ground.Length);
                maps[prototype][z, x] = Math.Min(4, maps[prototype][z, x] + 1);
            }
            for (var index = 0; index < maps.Length; index++) data.SetDetailLayer(0, 0, index, maps[index]);
        }

        private static int StableIndex(StableEntityId id, int count)
        {
            if (count <= 0) return 0;
            var bytes = id.ToGuid().ToByteArray();
            var hash = 17;
            foreach (var value in bytes) hash = unchecked(hash * 31 + value);
            return (hash & int.MaxValue) % count;
        }
    }
}
