using System;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.PlayMode
{
    [Category("WM05")]
    public sealed class WM05WorldMapVegetationPlayModeTests
    {
        [Test]
        public void AuditedCatalogUsesMeshLodTreesWithoutAuthoredCollidersOrBillboards()
        {
            var catalog = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.WorldMapTrees, Has.Length.EqualTo(4));
            Assert.That(catalog.WorldMapShrubs, Has.Length.EqualTo(3));
            Assert.That(catalog.WorldMapGroundCover, Has.Length.EqualTo(4));
            Assert.That(catalog.WorldMapSmallProps, Has.Length.EqualTo(2));
            foreach (var tree in catalog.WorldMapTrees)
            {
                Assert.That(tree, Is.Not.Null);
                Assert.That(tree.GetComponentsInChildren<Collider>(true), Is.Empty, tree.name);
                Assert.That(tree.GetComponentsInChildren<BillboardRenderer>(true), Is.Empty, tree.name);
                var lod = tree.GetComponent<LODGroup>();
                Assert.That(lod, Is.Not.Null, tree.name);
                Assert.That(lod.GetLODs().Count(value => value.renderers.Any(renderer => renderer != null)),
                    Is.GreaterThanOrEqualTo(2), tree.name);
            }
        }

        [Test]
        public void ProposedWorldProjectsOneTileAndBoundsDisposableTreeColliderSelection()
        {
            var definition = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var plan = new WorldMapVegetationPlanner(definition,
                new TiledWorldHeightSampler(definition, 20261023L), 20261023L).Create();
            var catalog = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            var tile = definition.Tiles.OrderByDescending(value => plan.ForTile(value).Count(instance =>
                instance.Kind != WorldMapVegetationKind.GroundCover)).First();
            var data = new TerrainData { size = new Vector3(definition.TileSizeMeters, 120f,
                definition.TileSizeMeters) };
            try
            {
                WorldMapVegetationTerrainProjector.Apply(data, tile, definition, plan, catalog);
                Assert.That(definition.GenerationId,
                    Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
                Assert.That(data.treeInstanceCount, Is.GreaterThan(0));
                Assert.That(data.treePrototypes, Has.Length.EqualTo(7));
                Assert.That(data.detailPrototypes, Has.Length.EqualTo(6));

                var streaming = new TiledWorldStreamingPlanner(definition);
                var start = streaming.CreatePlan(definition.PlayerStart, Array.Empty<WorldPosition>(),
                    Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                var far = new WorldPosition(definition.Origin.X + definition.WidthMeters - 5f, 0f,
                    definition.Origin.Z + 5f);
                var moved = streaming.CreatePlan(far, Array.Empty<WorldPosition>(),
                    Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                Assert.That(start.VisualTiles, Is.Not.Empty.And.Count.LessThan(definition.Tiles.Count));
                Assert.That(moved.VisualTiles, Is.Not.EqualTo(start.VisualTiles));

                var maximum = (int)definition.MapData.Vegetation.Budget.ActiveTreeColliders.Maximum;
                var near = plan.SelectInteractiveColliders(start.VisualTiles, definition.PlayerStart, 225f, maximum);
                var farSelection = plan.SelectInteractiveColliders(moved.VisualTiles, far, 225f, maximum);
                Assert.That(near, Is.Not.Empty);
                Assert.That(near, Has.Count.LessThanOrEqualTo(maximum));
                Assert.That(near.Select(value => value.StableId).Distinct().Count(), Is.EqualTo(near.Count));
                Assert.That(near.All(value => start.VisualTiles.Contains(value.Tile)), Is.True);
                Assert.That(farSelection.Select(value => value.StableId),
                    Is.Not.EqualTo(near.Select(value => value.StableId)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
