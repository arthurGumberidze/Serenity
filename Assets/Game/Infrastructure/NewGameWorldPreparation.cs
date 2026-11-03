using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>Immutable terrain preparation shared by the character editor and the world loader.</summary>
    public static class NewGameWorldPreparation
    {
        private sealed class Session
        {
            public TiledWorldHeightSampler Sampler;
            public Task<TiledWorldPopulationPlan> Population;
            public readonly ConcurrentDictionary<WorldTileId, (float[,] heights, float[,,] paint)> Tiles = new();
            public readonly ConcurrentDictionary<WorldTileId, Lazy<Vector4[,]>> Water = new();
        }
        private static volatile Session current;

        public static void Begin(long seed)
        {
            if (current?.Sampler.WorldSeed == seed) return;
            var definition = TiledWorldDefinitionLoader.LoadActive();
            var session = new Session { Sampler = new TiledWorldHeightSampler(definition, seed) };
            current = session;
            session.Population=Task.Run(async () =>
            {
                await Task.Delay(350);
                if(!ReferenceEquals(current,session))return null;
                return new TiledWorldPopulationPlanner(definition,session.Sampler,seed).Create();
            });
            var cache = new WorldTilePreparationCache(Application.persistentDataPath, definition,
                session.Sampler, 64, WorldMapTerrainPaintSampler.LayerCount);
            var paintSampler = new WorldMapTerrainPaintSampler(definition, session.Sampler);
            _ = Task.Run(async () =>
            {
                // Seed editing must not launch expensive work for every partially typed value.
                await Task.Delay(350);
                try
                {
                    foreach (var tile in definition.Tiles.OrderBy(tile =>
                        definition.TileOrigin(tile).DistanceSquared(definition.PlayerStart)))
                    {
                        if (!ReferenceEquals(current, session)) return;
                        if (!cache.TryRead(tile, out var heights, out var paint))
                        {
                            heights = session.Sampler.CreateNormalizedHeights(tile, session.Sampler.HeightmapResolution);
                            paint = paintSampler.CreateTileWeights(tile, 64, heights);
                            cache.Write(tile, heights, paint);
                        }
                        session.Tiles[tile] = (heights, paint);
                        _ = session.Water.GetOrAdd(tile, id => new Lazy<Vector4[,]>(() => BuildWaterSamples(session.Sampler, id))).Value;
                    }
                }
                catch (Exception) { /* Optional warmup: the normal loader retains its independent fallback. */ }
            });
        }

        public static Vector4[,] PrepareWater(TiledWorldHeightSampler sampler, WorldTileId tile)
        {
            var session = current;
            if (session != null && session.Sampler.WorldSeed == sampler.WorldSeed &&
                session.Sampler.Definition.GenerationId == sampler.Definition.GenerationId &&
                session.Sampler.Landscape?.revision == sampler.Landscape?.revision)
                return session.Water.GetOrAdd(tile, id => new Lazy<Vector4[,]>(() => BuildWaterSamples(sampler, id))).Value;
            return BuildWaterSamples(sampler, tile);
        }

        private static Vector4[,] BuildWaterSamples(TiledWorldHeightSampler sampler, WorldTileId tile)
        {
            var resolution = sampler.HeightmapResolution - 1;
            var samples = new Vector4[resolution + 1, resolution + 1];
            var origin = sampler.Definition.TileOrigin(tile);
            var step = sampler.Definition.TileSizeMeters / (float)resolution;
            for (var z = 0; z <= resolution; z++)
            for (var x = 0; x <= resolution; x++)
            {
                var distance = sampler.WaterBoundaryDistance(origin.X + x * step, origin.Z + z * step, out var surface);
                samples[x, z] = new Vector4(x * step, surface, z * step, distance);
            }
            return samples;
        }

        public static bool TryGet(TiledWorldHeightSampler sampler, WorldTileId tile,
            out (float[,] heights, float[,,] paint) data)
        {
            data = default;
            var session = current;
            return session != null && session.Sampler.WorldSeed == sampler.WorldSeed &&
                session.Sampler.Definition.GenerationId == sampler.Definition.GenerationId &&
                session.Sampler.Landscape?.revision == sampler.Landscape?.revision && session.Tiles.TryGetValue(tile, out data);
        }

        public static Task<TiledWorldPopulationPlan> Population(TiledWorldHeightSampler sampler)
        {
            var session=current;
            if(session!=null && session.Sampler.WorldSeed==sampler.WorldSeed &&
               session.Sampler.Definition.GenerationId==sampler.Definition.GenerationId &&
               session.Sampler.Landscape?.revision==sampler.Landscape?.revision)return session.Population;
            return Task.Run(()=>new TiledWorldPopulationPlanner(sampler.Definition,sampler,sampler.WorldSeed).Create());
        }

        public static bool TryHeight(TiledWorldHeightSampler sampler, float x, float z, out float height)
        {
            height = 0;
            if (!sampler.Definition.TryGetTile(x, z, out var tile) || !TryGet(sampler, tile, out var data)) return false;
            var origin = sampler.Definition.TileOrigin(tile);
            var values = data.heights;
            var last = values.GetLength(0) - 1;
            var fx = Mathf.Clamp((x - origin.X) / sampler.Definition.TileSizeMeters * last, 0, last);
            var fz = Mathf.Clamp((z - origin.Z) / sampler.Definition.TileSizeMeters * last, 0, last);
            var ix = Math.Min((int)fx, last - 1); var iz = Math.Min((int)fz, last - 1);
            height = Mathf.Lerp(Mathf.Lerp(values[iz, ix], values[iz, ix + 1], fx - ix),
                Mathf.Lerp(values[iz + 1, ix], values[iz + 1, ix + 1], fx - ix), fz - iz) * sampler.MaximumTerrainHeightMeters;
            return true;
        }
    }
}
