using System;
using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Infrastructure;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("P109Stage2")]
    public sealed class P109TiledWorldStage2PlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualRingOwnsOneViewPerCanonicalNodeAndOldRouteStripsStayHidden()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.WorldPopulation, Is.Not.Null);
            Assert.That(root.WorldPopulation.Plan.Resources.Count, Is.GreaterThanOrEqualTo(720));
            var expected = root.WorldPopulation.Plan.Resources.Count(node =>
                root.TiledWorld.VisualTiles.Contains(node.Tile));
            Assert.That(root.WorldPopulation.ActiveResourceViewCount, Is.EqualTo(expected));
            Assert.That(Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Where(value => value.transform.parent != null &&
                        value.transform.parent.name.StartsWith("P10.9 Population "))
                .Select(value => value.Site.Id).Distinct().Count(), Is.EqualTo(expected));
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where(value => value.name.StartsWith("P10.2 Physical Route "))
                .All(value => !value.gameObject.activeSelf), Is.True);
            var waterNode = root.WorldPopulation.Plan.Resources.First(value => value.RecipeId == "fish_water");
            root.CameraController.transform.position = new Vector3(waterNode.Position.X, waterNode.Position.Y, waterNode.Position.Z);
            root.TiledWorld.RefreshNow(waterNode.Position, Array.Empty<WorldPosition>(),
                Array.Empty<Tuple<WorldPosition, WorldPosition>>());
            yield return null;
            Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Any(value => value.name.StartsWith("P10.9 Local Water ")), Is.True);
        }

        [UnityTest]
        public IEnumerator StreamingFarTileKeepsCanonicalDepletionWhileReplacingOnlyViews()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var node = root.WorldPopulation.Plan.Resources.First(value => value.Tile.X == 0 && value.Tile.Z == 0);
            root.CameraController.transform.position = new Vector3(node.Position.X, node.Position.Y, node.Position.Z);
            var id = root.WorldPopulation.ResourceId(node);
            Assert.That(root.Production.TryGetSite(id, out var site), Is.True);
            site.CompleteWork(ProductionTaskKind.Gather);
            var remaining = site.RemainingAmount;

            root.TiledWorld.RefreshNow(node.Position, Array.Empty<WorldPosition>(),
                Array.Empty<Tuple<WorldPosition, WorldPosition>>());
            yield return null;
            Assert.That(root.TiledWorld.VisualTiles, Does.Contain(node.Tile));
            Assert.That(root.Production.TryGetSite(id, out var afterStream), Is.True);
            Assert.That(afterStream, Is.SameAs(site));
            Assert.That(afterStream.RemainingAmount, Is.EqualTo(remaining));
            Assert.That(Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).SingleOrDefault(value => value.IsBound && value.Site.Id == id),
                Is.Not.Null);
        }
    }
}
