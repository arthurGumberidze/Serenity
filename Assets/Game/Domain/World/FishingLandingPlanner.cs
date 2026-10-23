using System;
using System.Collections.Generic;
using Game.Domain.Buildings;

namespace Game.Domain.World
{
    /// <summary>Dry, level 4x3 landing candidates, evaluated using the same height authority as placement.</summary>
    public static class FishingLandingPlanner
    {
        public static IEnumerable<GridCoordinate> Candidates(TiledWorldHeightSampler heights, WorldZoneDefinition lake,
            float gridSize = 1f, float originX = 0f, float originZ = 0f)
        {
            var seen = new HashSet<GridCoordinate>();
            for (var distance = 10; distance <= 24; distance += 2)
            for (var angle = 0; angle < 360; angle += 10)
            {
                var radians = angle * Math.PI / 180;
                var x = lake.Center.X + (float)Math.Cos(radians) * (lake.RadiusX + distance);
                var z = lake.Center.Z + (float)Math.Sin(radians) * (lake.RadiusZ + distance);
                var anchor = new GridCoordinate((int)Math.Round((x - originX) / gridSize - 1.5f),
                    (int)Math.Round((z - originZ) / gridSize - 1f));
                if (seen.Add(anchor) && IsLevelDryFootprint(heights, anchor, gridSize, originX, originZ)) yield return anchor;
            }
        }

        public static bool IsLevelDryFootprint(TiledWorldHeightSampler heights, GridCoordinate anchor,
            float gridSize = 1f, float originX = 0f, float originZ = 0f)
        {
            var low = float.MaxValue; var high = float.MinValue;
            for (var x = 0; x <= 8; x++)
            for (var z = 0; z <= 6; z++)
            {
                var wx = originX + (anchor.X - .5f + x * .5f) * gridSize;
                var wz = originZ + (anchor.Z - .5f + z * .5f) * gridSize;
                if (!heights.IsBuildable(wx, wz)) return false;
                var y = heights.SampleHeight(wx, wz);
                low = Math.Min(low, y); high = Math.Max(high, y);
            }
            return high - low <= .75f;
        }

        public static bool IsNearShore(TiledWorldHeightSampler heights, float x, float z)
        {
            if (heights.IsWater(x, z)) return false;
            for (var distance = 2; distance <= 26; distance += 2)
            for (var angle = 0; angle < 360; angle += 15)
            {
                var radians = angle * Math.PI / 180;
                if (heights.IsWater(x + (float)Math.Cos(radians) * distance,
                    z + (float)Math.Sin(radians) * distance)) return true;
            }
            return false;
        }
    }
}
