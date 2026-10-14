using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Research;
using Game.Domain.Weather;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P92")]
    public sealed class P92RealisticGameplayRegionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TerrainNavMeshConnectsSettlementsAndAllResourcesKeepCanonicalStableIds()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.RealisticGameplayRegion, Is.Not.Null);
            Assert.DoesNotThrow(root.RealisticGameplayRegion.ValidateConfiguration);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                root.InhabitableRegion.RivalTerritoryAnchor.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(path.corners.Length, Is.GreaterThanOrEqualTo(2));
            foreach (var anchor in root.InhabitableRegion.ResourceAnchors)
            {
                var id = DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                    "player_source", anchor.StableIndex);
                Assert.That(root.Production.TryGetSite(id, out var site), Is.True);
                var presenter = anchor.VisualRoot.GetComponent<ProductionSourcePresenter>();
                Assert.That(presenter.Site, Is.SameAs(site));
                root.SelectionProbe.SelectProductionSource(presenter);
                Assert.That(root.SelectionProbe.SelectedProductionSourcePresenter, Is.SameAs(presenter));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TerrainHeightDrivesConstructionAndWeatherWithoutChangingCanonicalAuthority()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "shelter_construction");
            Assert.That(root.BuildingPlacementController.TryStartPlacement(
                new BuildingDefinitionId("primitive_shelter")), Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)), Is.True);
            var building = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            var presenter = root.BuildingPlacementController.LastPlacedPresenter;
            var terrainY = root.RealisticGameplayRegion.ProjectToGround(presenter.transform.position).y;
            Assert.That(Mathf.Abs(presenter.transform.position.y - terrainY), Is.LessThan(0.15f));

            var weatherBefore = root.Weather.CaptureState();
            var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Season == Season.Winter);
            root.WeatherPresentation.ShowPreview(winter);
            Assert.That(root.InhabitableRegion.DisplayedSeason, Is.EqualTo(Season.Winter));
            Assert.That(root.InhabitableRegion.SnowCoverVisible, Is.True);
            Assert.That(root.InhabitableRegion.SeasonalTerrain, Is.SameAs(root.RealisticGameplayRegion.Terrain));
            Assert.That(root.Weather.CaptureState().WorldSeed, Is.EqualTo(weatherBefore.WorldSeed));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveLoadAndLocalGlobalRoundtripRebindTheSameTerrainAnchors()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var anchor = root.InhabitableRegion.ResourceAnchors.Single(x => x.StableIndex == 20);
            var id = DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                "player_source", anchor.StableIndex);
            root.PlayerSaves.Save(1);
            root.PlayerSaves.Load(1);
            Assert.That(root.Production.TryGetSite(id, out var restored), Is.True);
            Assert.That(anchor.VisualRoot.GetComponent<ProductionSourcePresenter>().Site, Is.SameAs(restored));
            Assert.That(root.InhabitableRegion.HasCanonicalMapCorrespondence, Is.True);
            Assert.That(root.RealisticGameplayRegion.Locations.All(location =>
                root.GlobalMap.Locations.Any(x => x.Id.Value == location.LocationId)), Is.True);

            root.GlobalMapBridge.RequestEnterGlobal();
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            root.GlobalMapBridge.RequestEnterLocation(new Game.Domain.World.WorldLocationId("starter_settlement"));
            deadline = Time.realtimeSinceStartup + 15f;
            while (root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.GlobalMap.ActiveLocationId.Value, Is.EqualTo("starter_settlement"));
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(technologyId), Is.True);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }
    }
}
