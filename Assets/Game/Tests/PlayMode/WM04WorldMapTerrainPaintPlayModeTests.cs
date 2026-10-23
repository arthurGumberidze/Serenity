using System.Collections;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM04")]
    public sealed class WM04WorldMapTerrainPaintPlayModeTests
    {
        [UnityTest]
        public IEnumerator AdmittedLayersAcceptTheCanonicalPaintMask()
        {
            var catalog = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.WorldMapGroundLayers, Has.Length.EqualTo(WorldMapTerrainPaintSampler.LayerCount));
            Assert.That(catalog.WorldMapGroundLayers.Select(value => value.name), Is.EqualTo(new[]
            {
                "Grass_A", "Grass_Moss_A", "Cliff_Mossy_E", "Pebbles_B", "Grass_Soil_A", "Heather_A",
                "Tidal_Pools_B"
            }));

            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var paint = new WorldMapTerrainPaintSampler(world, new TiledWorldHeightSampler(world, 20261023L));
            var data = new TerrainData { alphamapResolution = 16, terrainLayers = catalog.WorldMapGroundLayers };
            try
            {
                var mask = paint.CreateTileWeights(new WorldTileId(0, 0), data.alphamapWidth);
                data.SetAlphamaps(0, 0, mask);
                var applied = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                Assert.That(applied.GetLength(2), Is.EqualTo(WorldMapTerrainPaintSampler.LayerCount));
                foreach (var x in Enumerable.Range(0, data.alphamapWidth))
                    Assert.That(Enumerable.Range(0, WorldMapTerrainPaintSampler.LayerCount)
                        .Sum(layer => applied[0, x, layer]), Is.EqualTo(1f).Within(0.01f), $"alphamap column {x}");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
            yield return null;
        }
    }
}
