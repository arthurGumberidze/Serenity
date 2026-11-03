using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Domain.World;

namespace Game.Infrastructure
{
    /// <summary>
    /// Immutable presentation cache for expensive terrain samples. The cache is keyed by the complete
    /// generation contract and never stores canonical gameplay or save state.
    /// </summary>
    public sealed class WorldTilePreparationCache
    {
        private const int TileMagic = 0x574D5431; // WMT1
        private const int TileFormatVersion = 1;
        private const int OverviewMagic = 0x574D4F31; // WMO1
        private const int PreparationAlgorithmVersion = 3; // DEV01: camp relief, worn paths and meadow patches invalidate cached terrain.
        private readonly string directory;
        private readonly int heightResolution;
        private readonly int paintResolution;
        private readonly int paintLayers;

        public WorldTilePreparationCache(string persistentDataPath, TiledWorldDefinition definition,
            TiledWorldHeightSampler sampler, int expectedPaintResolution, int expectedPaintLayers)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Persistent-data root is required.", nameof(persistentDataPath));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (sampler == null) throw new ArgumentNullException(nameof(sampler));
            if (!ReferenceEquals(definition, sampler.Definition))
                throw new ArgumentException("Sampler must use the cached world definition.", nameof(sampler));
            if (expectedPaintResolution < 0 || expectedPaintLayers < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedPaintResolution));

            heightResolution = sampler.HeightmapResolution;
            paintResolution = expectedPaintResolution;
            paintLayers = expectedPaintLayers;
            var contract = string.Join("|", new[]
            {
                PreparationAlgorithmVersion.ToString(CultureInfo.InvariantCulture),
                definition.GenerationId,
                definition.MapData?.SchemaVersion.ToString(CultureInfo.InvariantCulture) ?? "legacy",
                definition.MapData?.MapVersion ?? "legacy",
                sampler.WorldSeed.ToString(CultureInfo.InvariantCulture),
                definition.Columns.ToString(CultureInfo.InvariantCulture),
                definition.Rows.ToString(CultureInfo.InvariantCulture),
                definition.TileSizeMeters.ToString(CultureInfo.InvariantCulture),
                heightResolution.ToString(CultureInfo.InvariantCulture),
                paintResolution.ToString(CultureInfo.InvariantCulture),
                paintLayers.ToString(CultureInfo.InvariantCulture)
            });
            directory = Path.Combine(persistentDataPath, "Serenity", "WorldTileCache",
                "v" + PreparationAlgorithmVersion + "-" + StableHash(contract).ToString("x16"));
        }

        public string DirectoryPath => directory;
        public string CompletionMarkerPath => Path.Combine(directory, "complete.marker");
        private string OverviewPath => Path.Combine(directory, "overview-rgba.bin");

        public bool IsComplete(IReadOnlyList<WorldTileId> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            return File.Exists(CompletionMarkerPath) && File.Exists(OverviewPath) && AllTileFilesExist(tiles);
        }

        public bool TryRead(WorldTileId tile, out float[,] heights, out float[,,] paint)
        {
            heights = null;
            paint = null;
            try
            {
                var path = TilePath(tile);
                if (!File.Exists(path)) return false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                           64 * 1024, FileOptions.SequentialScan))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (reader.ReadInt32() != TileMagic || reader.ReadInt32() != TileFormatVersion ||
                        reader.ReadInt32() != tile.X || reader.ReadInt32() != tile.Z)
                        return false;
                    var heightRows = reader.ReadInt32();
                    var heightColumns = reader.ReadInt32();
                    var paintRows = reader.ReadInt32();
                    var paintColumns = reader.ReadInt32();
                    var layers = reader.ReadInt32();
                    if (heightRows != heightResolution || heightColumns != heightResolution ||
                        paintRows != paintResolution || paintColumns != paintResolution ||
                        layers != paintLayers)
                        return false;

                    var loadedHeights = new float[heightRows, heightColumns];
                    for (var z = 0; z < heightRows; z++)
                    for (var x = 0; x < heightColumns; x++)
                        loadedHeights[z, x] = reader.ReadSingle();
                    float[,,] loadedPaint = null;
                    if (paintRows > 0 && paintColumns > 0 && layers > 0)
                    {
                        loadedPaint = new float[paintRows, paintColumns, layers];
                        for (var z = 0; z < paintRows; z++)
                        for (var x = 0; x < paintColumns; x++)
                        for (var layer = 0; layer < layers; layer++)
                            loadedPaint[z, x, layer] = reader.ReadSingle();
                    }
                    if (stream.Position != stream.Length) return false;
                    heights = loadedHeights;
                    paint = loadedPaint;
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public void Write(WorldTileId tile, float[,] heights, float[,,] paint)
        {
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (heights.GetLength(0) != heightResolution || heights.GetLength(1) != heightResolution)
                throw new ArgumentException("Unexpected height dimensions.", nameof(heights));
            if (paintResolution > 0 && paintLayers > 0 && (paint == null ||
                paint.GetLength(0) != paintResolution || paint.GetLength(1) != paintResolution ||
                paint.GetLength(2) != paintLayers))
                throw new ArgumentException("Unexpected paint dimensions.", nameof(paint));

            Directory.CreateDirectory(directory);
            var path = TilePath(tile);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                           64 * 1024, FileOptions.SequentialScan))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    writer.Write(TileMagic);
                    writer.Write(TileFormatVersion);
                    writer.Write(tile.X);
                    writer.Write(tile.Z);
                    writer.Write(heights.GetLength(0));
                    writer.Write(heights.GetLength(1));
                    writer.Write(paint?.GetLength(0) ?? 0);
                    writer.Write(paint?.GetLength(1) ?? 0);
                    writer.Write(paint?.GetLength(2) ?? 0);
                    for (var z = 0; z < heights.GetLength(0); z++)
                    for (var x = 0; x < heights.GetLength(1); x++)
                        writer.Write(heights[z, x]);
                    if (paint != null)
                    {
                        for (var z = 0; z < paint.GetLength(0); z++)
                        for (var x = 0; x < paint.GetLength(1); x++)
                        for (var layer = 0; layer < paint.GetLength(2); layer++)
                            writer.Write(paint[z, x, layer]);
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public bool TryReadOverview(int resolution, out byte[] rgba)
        {
            rgba = null;
            try
            {
                if (!File.Exists(OverviewPath)) return false;
                using (var stream = new FileStream(OverviewPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (reader.ReadInt32() != OverviewMagic || reader.ReadInt32() != resolution) return false;
                    var length = reader.ReadInt32();
                    if (length != checked(resolution * resolution * 4)) return false;
                    var bytes = reader.ReadBytes(length);
                    if (bytes.Length != length || stream.Position != stream.Length) return false;
                    rgba = bytes;
                    return true;
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public void WriteOverview(int resolution, byte[] rgba)
        {
            if (resolution <= 0 || rgba == null || rgba.Length != checked(resolution * resolution * 4))
                throw new ArgumentException("Unexpected overview data.", nameof(rgba));
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(OverviewPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
            {
                writer.Write(OverviewMagic);
                writer.Write(resolution);
                writer.Write(rgba.Length);
                writer.Write(rgba);
            }
        }

        public void MarkComplete(IReadOnlyList<WorldTileId> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (!File.Exists(OverviewPath) || !AllTileFilesExist(tiles))
                throw new InvalidOperationException("Every prepared tile and the overview must be cached before completion.");
            Directory.CreateDirectory(directory);
            File.WriteAllText(CompletionMarkerPath, "complete", Encoding.UTF8);
        }

        private bool AllTileFilesExist(IEnumerable<WorldTileId> tiles) =>
            System.Linq.Enumerable.All(tiles, tile => File.Exists(TilePath(tile)));

        private string TilePath(WorldTileId tile) => Path.Combine(directory,
            "tile-" + tile.X.ToString("D2", CultureInfo.InvariantCulture) + "-" +
            tile.Z.ToString("D2", CultureInfo.InvariantCulture) + ".bin");

        private static ulong StableHash(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= prime;
            }
            return hash;
        }
    }
}
