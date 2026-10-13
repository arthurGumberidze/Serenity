using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Weather;
using Game.Infrastructure;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P5")]
    public sealed class P5InhabitableRegionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredNodesBindToCanonicalFiniteProductionSites()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.InhabitableRegion.HasCanonicalMapCorrespondence, Is.True);
            Assert.That(root.Production.Sites.Count(x => x.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource),
                Is.EqualTo(root.InhabitableRegion.ResourceAnchors.Count));
            foreach (var anchor in root.InhabitableRegion.ResourceAnchors)
            {
                var id = DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                    "player_source", anchor.StableIndex);
                Assert.That(root.Production.TryGetSite(id, out var site), Is.True);
                Assert.That(site.RemainingAmount, Is.EqualTo(anchor.Amount));
                Assert.That(anchor.VisualRoot.TryGetComponent<ProductionSourcePresenter>(out var presenter), Is.True);
                Assert.That(presenter.Site, Is.SameAs(site));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegionSupportsNavMeshAndConstructionAfterArtFill()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var path = new NavMeshPath();
            Assert.That(NavMesh.SamplePosition(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                out var start, 6f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(root.InhabitableRegion.RivalTerritoryAnchor.position,
                out var end, 8f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(path.corners.Length, Is.GreaterThanOrEqualTo(2));

            CompleteTechnology(root, "shelter_construction");
            Assert.That(root.BuildingPlacementController.TryStartPlacement(
                new BuildingDefinitionId("primitive_shelter")), Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)),
                Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(60f, 0f, 60f)),
                Is.False);
            root.BuildingPlacementController.CancelPlacement();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeasonsAndSaveLoadPreserveCanonicalIdentityAndResourceBindings()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var weatherBefore = root.Weather.CaptureState();
            var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Season == Season.Winter);
            root.WeatherPresentation.ShowPreview(winter);
            Assert.That(root.InhabitableRegion.DisplayedSeason, Is.EqualTo(Season.Winter));
            Assert.That(root.InhabitableRegion.SnowCoverVisible, Is.True);
            Assert.That(root.Weather.CaptureState().WorldSeed, Is.EqualTo(weatherBefore.WorldSeed));

            var rulerId = root.MaleDemoCharacter.Id;
            var shelterId = root.DemoShelterBuilding.Id;
            var anchor = root.InhabitableRegion.ResourceAnchors.First();
            var siteId = DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                "player_source", anchor.StableIndex);
            root.PlayerSaves.Save(1);
            root.PlayerSaves.Load(1);
            Assert.That(root.MaleDemoCharacter.Id, Is.EqualTo(rulerId));
            Assert.That(root.Buildings.TryGet(shelterId, out _), Is.True);
            Assert.That(root.Production.TryGetSite(siteId, out var restoredSite), Is.True);
            Assert.That(anchor.VisualRoot.GetComponent<ProductionSourcePresenter>().Site, Is.SameAs(restoredSite));
            Assert.That(root.Appearances.Get(rulerId).CharacterId, Is.EqualTo(rulerId));
            yield return null;
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new Game.Domain.Research.TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(technologyId), Is.True);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
            Assert.That(root.Research.IsCompleted(technologyId), Is.True);
        }
    }
}
