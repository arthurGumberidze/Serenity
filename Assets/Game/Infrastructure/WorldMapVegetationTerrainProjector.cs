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
            data.treePrototypes = Array.Empty<TreePrototype>();
            data.detailPrototypes = Array.Empty<DetailPrototype>();
            // WorldMapVegetationRenderer projects the complete canonical plan using GPU batches.
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
