using System;
using System.Collections.Generic;
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
            var trees = catalog.WorldMapTrees;
            var shrubs = catalog.WorldMapShrubs;
            var prototypes = trees.Concat(shrubs).Distinct().ToArray();
            data.treePrototypes = prototypes.Select(value => new TreePrototype
                { prefab = value, bendFactor = 0f }).ToArray();
            var origin = definition.TileOrigin(tile);
            var instances = new List<TreeInstance>();
            foreach (var value in plan.ForTile(tile).Where(value =>
                         value.Kind != WorldMapVegetationKind.GroundCover))
            {
                instances.Add(new TreeInstance
                {
                    position = new Vector3((value.Position.X - origin.X) / definition.TileSizeMeters,
                        value.Position.Y / data.size.y,
                        (value.Position.Z - origin.Z) / definition.TileSizeMeters),
                    rotation = value.RotationDegrees * Mathf.Deg2Rad,
                    widthScale = value.UniformScale,
                    heightScale = value.UniformScale,
                    color = Color.white,
                    lightmapColor = Color.white,
                    prototypeIndex = TreePrototypeIndex(value, trees.Length, shrubs.Length)
                });
            }
            data.SetTreeInstances(instances.ToArray(), false);

            var ground = catalog.WorldMapGroundCover
                .Concat(catalog.WorldMapSmallProps ?? Array.Empty<GameObject>()).Distinct().ToArray();
            data.SetDetailResolution(128, 16);
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

        private static int TreePrototypeIndex(WorldMapVegetationInstance value, int treeCount, int shrubCount)
        {
            var index = StableIndex(value.StableId, 100);
            return value.Kind == WorldMapVegetationKind.InteractiveTree || index < 46 || shrubCount == 0
                ? StableIndex(value.StableId, treeCount)
                : treeCount + StableIndex(value.StableId, shrubCount);
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
