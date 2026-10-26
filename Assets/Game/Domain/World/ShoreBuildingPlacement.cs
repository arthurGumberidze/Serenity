using System;
using Game.Domain.Buildings;

namespace Game.Domain.World
{
    /// <summary>A shore building stays on dry ground; its front faces water and its rear is a dry entrance.</summary>
    public static class ShoreBuildingPlacement
    {
        public static bool IsValid(TiledWorldHeightSampler heights, GridCoordinate anchor,
            BuildingFootprint footprint, BuildingOrientation orientation, float gridSize, float originX, float originZ)
        {
            var rotated = footprint.Rotate(orientation);
            var cx = originX + (anchor.X + (rotated.Width - 1) * .5f) * gridSize;
            var cz = originZ + (anchor.Z + (rotated.Depth - 1) * .5f) * gridSize;
            var angle = orientation.Degrees() * Math.PI / 180;
            var fx = (float)Math.Sin(angle); var fz = (float)Math.Cos(angle);
            var front = footprint.Depth * gridSize * .5f + .9f;
            // Three water samples across the working face exclude a corner merely near a pond.
            for (var side = -1; side <= 1; side++)
            {
                var x = cx + fx * front + fz * side * footprint.Width * gridSize * .25f;
                var z = cz + fz * front - fx * side * footprint.Width * gridSize * .25f;
                if (!heights.Definition.ContainsClosed(x,z) || !heights.IsWater(x,z)) return false;
            }
            var entranceX = cx - fx * (front + .6f);
            var entranceZ = cz - fz * (front + .6f);
            if (!heights.Definition.ContainsClosed(entranceX,entranceZ) ||
                !heights.IsBuildable(entranceX,entranceZ,32f)) return false;
            var low = float.MaxValue; var high = float.MinValue;
            foreach (var cell in footprint.EnumerateCells(anchor,orientation))
            {
                var x = originX + cell.X * gridSize; var z = originZ + cell.Z * gridSize;
                if (!heights.Definition.ContainsClosed(x,z) || !heights.IsBuildable(x,z,32f)) return false;
                var y = heights.SampleHeight(x,z); low = Math.Min(low,y); high = Math.Max(high,y);
            }
            return high - low <= 3f;
        }
    }
}
