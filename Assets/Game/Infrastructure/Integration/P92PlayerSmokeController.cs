using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Research;
using Game.Domain.Weather;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player proof for the migrated P9.2 Terrain gameplay region.</summary>
    public sealed class P92PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool official_terrain;
            public bool legacy_visible_map_removed;
            public bool six_locations;
            public bool twenty_four_stable_resources;
            public bool selection;
            public bool navmesh_route;
            public bool construction;
            public bool weather_projection;
            public bool local_global_roundtrip;
            public bool save_load_rebound;
            public bool rts_rendered;
            public bool tps_rendered;
            public int terrain_layers;
            public int trees;
            public int rocks;
            public int foliage;
            public string rts_screenshot;
            public string tps_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p92Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(92002);
        }

        public static P92PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p92Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p92SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p92SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P92PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            P92RealisticGameplayRegion marker = null;
            StableEntityId selectedSiteId = default;
            try
            {
                root.Clock.Pause();
                marker = root.RealisticGameplayRegion ?? throw new InvalidOperationException("P9.2 region is missing.");
                marker.ValidateConfiguration();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                root.GameplayUi.RootVisual.style.display = DisplayStyle.None;

                result.terrain_layers = marker.Terrain.terrainData.terrainLayers.Length;
                result.trees = marker.Trees.Count;
                result.rocks = marker.Rocks.Count;
                result.foliage = marker.Foliage.Count;
                result.official_terrain = marker.SourceAsset.Contains("213197") && result.terrain_layers >= 4;
                result.legacy_visible_map_removed = GameObject.Find("P3 Reference World") == null &&
                    GameObject.Find("P3 Buildable Forest Clearing") == null &&
                    GameObject.Find("P8.1 Visual Prototype") == null &&
                    GameObject.Find("P9.1 Realistic World Benchmark") == null;
                result.six_locations = marker.Locations.Count == 6 && root.GlobalMap.Locations.Count == 6 &&
                    marker.Locations.All(x => root.GlobalMap.Locations.Any(y => y.Id.Value == x.LocationId));

                var anchors = root.InhabitableRegion.ResourceAnchors.OrderBy(x => x.StableIndex).ToArray();
                result.twenty_four_stable_resources = anchors.Length == 24 &&
                    anchors.Select(x => x.StableIndex).SequenceEqual(Enumerable.Range(0, 24)) &&
                    anchors.All(x => root.Production.TryGetSite(DeterministicStableId.Create(
                        root.ActiveNewGameConfiguration.WorldSeed, "player_source", x.StableIndex), out _));
                var selected = anchors.First(x => x.RecipeId == "gather_gold").VisualRoot
                    .GetComponent<ProductionSourcePresenter>();
                selectedSiteId = selected.Site.Id;
                root.SelectionProbe.SelectProductionSource(selected);
                result.selection = root.SelectionProbe.SelectedProductionSourcePresenter == selected &&
                    selected.GetComponentInChildren<Collider>() != null;

                var path = new NavMeshPath();
                result.navmesh_route = NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                    root.InhabitableRegion.RivalTerritoryAnchor.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2;

                CompleteTechnology(root, "shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)))
                    throw new InvalidOperationException("Terrain clearing rejected a valid shelter placement.");
                var placed = root.BuildingPlacementController.ConfirmPlacement();
                result.construction = placed != null && root.Buildings.TryGet(placed.Id, out _);

                var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Season == Season.Winter);
                root.WeatherPresentation.ShowPreview(winter);
                result.weather_projection = root.InhabitableRegion.DisplayedSeason == Season.Winter &&
                    root.InhabitableRegion.SnowCoverVisible && root.InhabitableRegion.SeasonalTerrain == marker.Terrain;

                root.PlayerSaves.Save(1);
                root.PlayerSaves.Load(1);
                result.save_load_rebound = root.Production.TryGetSite(selectedSiteId, out var restored) &&
                    root.InhabitableRegion.ResourceAnchors.Select(x => x.VisualRoot)
                        .Select(x => x.GetComponent<ProductionSourcePresenter>())
                        .Any(x => x != null && ReferenceEquals(x.Site, restored));

                root.CameraController.enabled = false;
                Frame(new Vector3(8f, marker.ProjectToGround(new Vector3(8f, 0f, 3f)).y, 3f), 34f, 58f, 72f);
            }
            catch (Exception exception) { result.error = exception.ToString(); }

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.rts_screenshot = Capture("p92-rts.png");
                    result.rts_rendered = File.Exists(result.rts_screenshot);
                    var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                    if (!enter.IsEligible) throw new InvalidOperationException("TPS entry failed on the Terrain region.");
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.tps_screenshot = Capture("p92-tps.png");
                    result.tps_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive &&
                        File.Exists(result.tps_screenshot);
                    root.ThirdPersonControl.Exit();
                    root.GlobalMapBridge.RequestEnterGlobal();
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                var deadline = Time.realtimeSinceStartup + 15f;
                while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                Try(result, () =>
                {
                    if (!root.GlobalMapBridge.IsGlobalSceneLoaded)
                        throw new InvalidOperationException("Global map did not load from the Terrain region.");
                    root.GlobalMapBridge.RequestEnterLocation(new Game.Domain.World.WorldLocationId("starter_settlement"));
                });
                deadline = Time.realtimeSinceStartup + 15f;
                while (root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                result.local_global_roundtrip = !root.GlobalMapBridge.IsGlobalSceneLoaded &&
                    root.GlobalMap.ActiveLocationId.Value == "starter_settlement";
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.official_terrain &&
                result.legacy_visible_map_removed && result.six_locations && result.twenty_four_stable_resources &&
                result.selection && result.navmesh_route && result.construction && result.weather_projection &&
                result.local_global_roundtrip && result.save_load_rebound && result.rts_rendered && result.tps_rendered;
            var resultPath = Path.Combine(outputDirectory, "p92-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P92_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
            Application.Quit(result.passed ? 0 : 2);
        }

        private void Frame(Vector3 target, float yaw, float pitch, float distance)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot composition, string id)
        {
            var technologyId = new TechnologyId(id);
            if (composition.Research.IsCompleted(technologyId)) return;
            composition.Research.TryAssignWorker(composition.MaleDemoCharacter.Id);
            if (!composition.Research.Start(technologyId))
                throw new InvalidOperationException("Research could not start: " + id);
            composition.Research.AdvanceWorkerTo(composition.MaleDemoCharacter.Id,
                composition.Research.LastProcessedTick + composition.Technologies.Get(technologyId)
                    .ResearchWorkRequiredTicks, true);
        }

        private static void Try(SmokeResult result, Action action)
        {
            try { action(); }
            catch (Exception exception) { result.error = exception.ToString(); }
        }

        private string Capture(string fileName)
        {
            var path = Path.Combine(outputDirectory, fileName);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            return path;
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
