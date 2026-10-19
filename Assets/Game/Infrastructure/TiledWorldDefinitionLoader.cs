using System;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure
{
    public static class TiledWorldDefinitionLoader
    {
        [Serializable] private sealed class LayoutData
        {
            public string generationId;
            public int columns, rows, tileSizeMeters;
            public float originX, originZ, playerStartX, playerStartZ;
            public ZoneData[] zones;
            public PointData[] river;
        }
        [Serializable] private sealed class ZoneData
        {
            public string id, uses;
            public float x, z, radiusX, radiusZ;
            public bool ellipse;
        }
        [Serializable] private sealed class PointData { public float x, z; }

        public static TiledWorldDefinition LoadReference()
        {
            var asset = UnityEngine.Resources.Load<TextAsset>("World/P109TiledWorld");
            if (asset == null) throw new InvalidOperationException("The tiled-world definition is missing.");
            return Parse(asset.text);
        }

        public static TiledWorldDefinition Parse(string json)
        {
            var data = JsonUtility.FromJson<LayoutData>(json);
            if (data == null || data.generationId != TiledWorldDefinition.ReferenceGenerationId ||
                data.columns != 12 || data.rows != 12 || data.tileSizeMeters != 1000 || data.zones == null || data.river == null)
                throw new InvalidOperationException("P10.9 requires the versioned 12 × 12 grid of 1000 m tiles.");
            return new TiledWorldDefinition(data.generationId, data.columns, data.rows, data.tileSizeMeters,
                data.originX, data.originZ, data.zones.Select(zone => new WorldZoneDefinition(zone.id,
                    (WorldZoneUse)Enum.Parse(typeof(WorldZoneUse), zone.uses, false), zone.x, zone.z,
                    zone.radiusX, zone.radiusZ, zone.ellipse)),
                data.river.Select(point => new WorldPosition(point.x, 0f, point.z)),
                new WorldPosition(data.playerStartX, 0f, data.playerStartZ));
        }
    }
}
