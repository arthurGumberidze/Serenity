using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible normal-Player proof for the P9.3 populated biome world.</summary>
    public sealed class P93PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool distinct_biomes;
            public bool believable_density;
            public bool deterministic_scatter;
            public bool lod_policy;
            public bool collider_policy;
            public bool resource_formations;
            public bool resource_interaction;
            public bool navmesh_routes;
            public bool construction;
            public bool weather_projection;
            public bool save_load_rebound;
            public bool rts_rendered;
            public bool tps_rendered;
            public int terrain_layers;
            public int trees;
            public int rocks;
            public int ground_cover;
            public int formations;
            public float coverage_ratio;
            public string layout_hash;
            public string rts_screenshot;
            public string tps_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p93Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(93003);
        }

        public static P93PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p93Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p93SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p93SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P93PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            P93PopulatedBiomeWorld marker = null;
            Game.Domain.StableEntityId selectedSiteId = default;
            Try(result, () =>
            {
                root.Clock.Pause();
                marker = root.PopulatedBiomeWorld ?? throw new InvalidOperationException("P9.3 biome world is missing.");
                marker.ValidateConfiguration();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                root.GameplayUi.RootVisual.style.display = DisplayStyle.None;

                result.terrain_layers = marker.BaseRegion.Terrain.terrainData.terrainLayers.Length;
                result.trees = marker.CanopyTrees.Count;
                result.rocks = marker.MajorRocks.Count;
                result.ground_cover = marker.GroundCover.Count;
                result.formations = marker.ResourceFormations.Count;
                result.coverage_ratio = marker.CoverageRatio;
                result.layout_hash = marker.LayoutHash;
                result.distinct_biomes = marker.Zones.Select(x => x.Kind).Distinct().Count() == 6 &&
                    result.terrain_layers >= 7;
                result.believable_density = result.trees >= 170 && result.rocks >= 90 &&
                    result.ground_cover >= 900 && result.coverage_ratio >= 0.92f;
                result.deterministic_scatter = marker.ScatterSeed == 93031 &&
                    string.Equals(marker.LayoutHash, marker.ComputeLayoutHash(), StringComparison.Ordinal);
                result.lod_policy = marker.CanopyTrees.All(x => x.GetComponentInChildren<LODGroup>(true) != null) &&
                    marker.MajorRocks.All(x => x.GetComponentInChildren<LODGroup>(true) != null);
                result.collider_policy = marker.CanopyTrees.All(x => x.GetComponentInChildren<Collider>(true) != null) &&
                    marker.MajorRocks.All(x => x.GetComponentInChildren<Collider>(true) != null) &&
                    marker.GroundCover.All(x => x.GetComponentInChildren<Collider>(true) == null);
                result.resource_formations = marker.ResourceFormations.Count == 24 &&
                    marker.ResourceFormations.Select(x => x.StableIndex).SequenceEqual(Enumerable.Range(0, 24)) &&
                    marker.ResourceFormations.All(x => x.DecorativeSatellites.Count >= 4 &&
                        x.ClickableRoot.GetComponentInChildren<Collider>(true) != null);

                var selected = root.InhabitableRegion.ResourceAnchors.Single(x => x.StableIndex == 20)
                    .VisualRoot.GetComponent<ProductionSourcePresenter>();
                selectedSiteId = selected.Site.Id;
                root.SelectionProbe.SelectProductionSource(selected);
                var groups = root.DirectWorkCommands.EnsureRoleGroups();
                var gatherers = groups.Single(x => x.Role == WorkGroupRole.Gatherers);
                var worker = root.MaleDemoCharacter.Id;
                if (root.Work.Groups.TryGetForMember(worker, out var prior) && prior.Id != gatherers.Id)
                    root.Work.RemoveMember(prior.Id, worker);
                if (!gatherers.Contains(worker)) root.Work.AddMember(gatherers.Id, worker);
                var order = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                    WorkTarget.ForEntity(WorkTargetKind.ProductionSite, selected.Site.Id, selected.Site.Position),
                    WorkPriority.High);
                result.resource_interaction = order.Target.EntityId == selected.Site.Id &&
                    order.Priority == WorkPriority.High;
                root.Work.Cancel(order.Id);

                var start = root.InhabitableRegion.PlayerTerritoryAnchor.position;
                result.navmesh_routes = marker.BaseRegion.Locations.All(location =>
                {
                    if (!NavMesh.SamplePosition(location.Anchor.position, out var destination,
                            3f, NavMesh.AllAreas) ||
                        Vector3.Distance(destination.position, location.Anchor.position) > 3f) return false;
                    var path = new NavMeshPath();
                    return NavMesh.CalculatePath(start, destination.position, NavMesh.AllAreas, path) &&
                           path.status == NavMeshPathStatus.PathComplete;
                });

                CompleteTechnology(root, "shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)))
                    throw new InvalidOperationException("P9.3 buildable clearing rejected shelter placement.");
                var building = root.BuildingPlacementController.ConfirmPlacement();
                result.construction = building != null && root.Buildings.TryGet(building.Id, out _);

                var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Season == Season.Winter);
                root.WeatherPresentation.ShowPreview(winter);
                result.weather_projection = root.InhabitableRegion.DisplayedSeason == Season.Winter &&
                    root.InhabitableRegion.SnowCoverVisible &&
                    root.InhabitableRegion.SeasonalTerrain == marker.BaseRegion.Terrain;

                root.PlayerSaves.Save(1);
                root.PlayerSaves.Load(1);
                result.save_load_rebound = root.Production.TryGetSite(selectedSiteId, out var restored) &&
                    root.InhabitableRegion.ResourceAnchors.Select(x => x.VisualRoot)
                        .Select(x => x.GetComponent<ProductionSourcePresenter>())
                        .Any(x => x != null && ReferenceEquals(x.Site, restored));

                root.CameraController.enabled = false;
                var target = marker.BaseRegion.ProjectToGround(new Vector3(11f, 0f, 5f));
                Frame(target, 32f, 58f, 72f);
            });

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.rts_screenshot = Capture("p93-rts.png");
                    result.rts_rendered = File.Exists(result.rts_screenshot);
                    var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                    if (!enter.IsEligible) throw new InvalidOperationException("TPS entry failed in the P9.3 world.");
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.tps_screenshot = Capture("p93-tps.png");
                    result.tps_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive &&
                        File.Exists(result.tps_screenshot);
                    root.ThirdPersonControl.Exit();
                });
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.distinct_biomes &&
                result.believable_density && result.deterministic_scatter && result.lod_policy &&
                result.collider_policy && result.resource_formations && result.resource_interaction &&
                result.navmesh_routes && result.construction && result.weather_projection &&
                result.save_load_rebound && result.rts_rendered && result.tps_rendered;
            var resultPath = Path.Combine(outputDirectory, "p93-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P93_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
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
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
