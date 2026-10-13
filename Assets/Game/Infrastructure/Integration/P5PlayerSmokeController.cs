using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible normal-Player smoke for the P5 authored region and canonical local/global loop.</summary>
    public sealed class P5PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool region_initialized;
            public bool canonical_map_correspondence;
            public bool canonical_resources_bound;
            public bool resource_gathered;
            public bool building_placed;
            public bool navmesh_route_complete;
            public bool rts_traversal_completed;
            public bool tps_rendered;
            public bool rival_territory_visible;
            public bool global_map_rendered;
            public bool save_load_identity_preserved;
            public bool season_projection_visible;
            public float rival_distance;
            public int resource_node_count;
            public int tree_count;
            public int rock_count;
            public int route_count;
            public string ruler_stable_id;
            public string placed_building_stable_id;
            public string gathered_site_stable_id;
            public string rts_screenshot;
            public string tps_rival_screenshot;
            public string global_map_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p5Smoke", StringComparison.OrdinalIgnoreCase))) return;
            if (!long.TryParse(ReadArgument(arguments, "-p5SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p5SmokeSeed must be a positive whole number.");
            menu.StartNewGame(requestedSeed);
        }

        public static P5PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p5Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(arguments, "-p5SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p5SmokeOutput is required.");
            if (!long.TryParse(ReadArgument(arguments, "-p5SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p5SmokeSeed must be a positive whole number.");
            var controller = compositionRoot.gameObject.AddComponent<P5PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.seed = requestedSeed;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult { seed = seed };
            root.Clock.Pause();
            if (root.GameplayUi != null) root.GameplayUi.RootVisual.style.display = DisplayStyle.None;
            yield return null;
            Game.Domain.StableEntityId gatheredSiteId = default;
            Game.Domain.StableEntityId placedBuildingId = default;
            long savedRemaining = -1;
            try
            {
                var region = root.InhabitableRegion ?? throw new InvalidOperationException("P5 region is missing.");
                result.region_initialized = region.ResourceAnchors.Count >= 12 && region.BuildableAreaAnchors.Count >= 2;
                result.canonical_map_correspondence = region.HasCanonicalMapCorrespondence;
                result.resource_node_count = region.ResourceAnchors.Count;
                result.tree_count = region.TreeCount;
                result.rock_count = region.RockCount;
                result.route_count = region.RouteWaypoints.Count;
                result.canonical_resources_bound = region.ResourceAnchors.All(anchor =>
                    root.Production.TryGetSite(Game.Domain.Campaign.DeterministicStableId.Create(seed,
                        "player_source", anchor.StableIndex), out var site) &&
                    site.Position.Equals(new WorldPosition(anchor.VisualRoot.position.x,
                        anchor.VisualRoot.position.y, anchor.VisualRoot.position.z)));

                var berryAnchor = region.ResourceAnchors.First(x => x.RecipeId == "gather_berries");
                gatheredSiteId = Game.Domain.Campaign.DeterministicStableId.Create(seed,
                    "player_source", berryAnchor.StableIndex);
                var berrySite = root.Production.Sites.Single(x => x.Id == gatheredSiteId);
                var beforeRemaining = berrySite.RemainingAmount;
                var berryId = new ResourceId("berries");
                root.Production.SetTarget(berryId, root.Production.GetCanonicalTotal(berryId) + 20);
                if (!root.Production.TryReserve(root.FemaleDemoCharacter.Id, berrySite.Position, out var job))
                    throw new InvalidOperationException("Canonical berry gathering could not be reserved.");
                var completion = root.Production.AdvanceTask(job.Task.Id, TimeSpan.FromDays(30));
                result.resource_gathered = completion.Completed && completion.ProducedOutput &&
                    berrySite.RemainingAmount < beforeRemaining;

                CompleteTechnology("shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)))
                    throw new InvalidOperationException("P5 clearing rejected a valid shelter placement.");
                var placed = root.BuildingPlacementController.ConfirmPlacement();
                if (placed == null) throw new InvalidOperationException("P5 shelter placement did not commit.");
                placedBuildingId = placed.Id;
                result.building_placed = root.Buildings.TryGet(placedBuildingId, out _);

                var path = new NavMeshPath();
                result.navmesh_route_complete = NavMesh.CalculatePath(region.PlayerTerritoryAnchor.position,
                    region.RivalTerritoryAnchor.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2;
                PrepareRtsCamera();
                result.ruler_stable_id = root.MaleDemoCharacter.Id.ToString();
                result.placed_building_stable_id = placedBuildingId.ToString();
                result.gathered_site_stable_id = gatheredSiteId.ToString();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            result.rts_screenshot = Path.Combine(outputDirectory, "p5-rts-region.png");
            Capture(result.rts_screenshot);

            try
            {
                var destination = root.InhabitableRegion.RivalTerritoryAnchor.position + new Vector3(-3f, 0f, -2f);
                var move = root.ManualMoveCommands.Issue(new[] { root.MaleDemoCharacter.Id },
                    new WorldPosition(destination.x, destination.y, destination.z), WorkPriority.Urgent);
                if (move.CreatedCount != 1) throw new InvalidOperationException("RTS route command was rejected.");
                root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
                root.Clock.Resume();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            var deadline = Time.realtimeSinceStartup + 24f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var view) &&
                    Vector3.Distance(view.transform.position, root.InhabitableRegion.RivalTerritoryAnchor.position) < 7f)
                    break;
                yield return null;
            }
            try
            {
                root.Clock.Pause();
                if (!root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var presenter))
                    throw new InvalidOperationException("Ruler presentation disappeared during traversal.");
                result.rival_distance = Vector3.Distance(presenter.transform.position,
                    root.InhabitableRegion.RivalTerritoryAnchor.position);
                result.rts_traversal_completed = result.rival_distance < 7f;
                var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                if (!enter.IsEligible) throw new InvalidOperationException("TPS entry failed after traversal.");
                result.rival_territory_visible = result.rival_distance < 7f;
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            result.tps_rival_screenshot = Path.Combine(outputDirectory, "p5-tps-rival-territory.png");
            Capture(result.tps_rival_screenshot);
            result.tps_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive;
            root.ThirdPersonControl.Exit();
            yield return null;

            try
            {
                root.GlobalMapBridge.RequestEnterGlobal();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            var globalDeadline = Time.realtimeSinceStartup + 8f;
            while (string.IsNullOrEmpty(result.error) && !root.GlobalMapBridge.IsGlobalSceneLoaded &&
                   Time.realtimeSinceStartup < globalDeadline) yield return null;
            if (!root.GlobalMapBridge.IsGlobalSceneLoaded) result.error = "Global map did not load.";
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            result.global_map_screenshot = Path.Combine(outputDirectory, "p5-global-region.png");
            Capture(result.global_map_screenshot);
            result.global_map_rendered = root.GlobalMapBridge.IsGlobalSceneLoaded &&
                root.GlobalMap.Locations.Count == 6 && root.GlobalMap.Routes.Count >= 8;
            root.GlobalMapBridge.RequestEnterLocation(new Game.Domain.World.WorldLocationId("starter_settlement"));
            var localDeadline = Time.realtimeSinceStartup + 8f;
            while (root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < localDeadline) yield return null;

            try
            {
                var savedSite = root.Production.Sites.Single(x => x.Id == gatheredSiteId);
                savedRemaining = savedSite.RemainingAmount;
                root.GameplayUi.SaveSlot(1);
                root.MaleDemoCharacter.Rename(new CharacterName("Временный"));
                root.GameplayUi.ConfirmLoadSlot(1);
                result.save_load_identity_preserved = root.MaleDemoCharacter.Id.ToString() == result.ruler_stable_id &&
                    root.Buildings.TryGet(placedBuildingId, out var restoredBuilding) && restoredBuilding != null &&
                    root.Production.TryGetSite(gatheredSiteId, out var restoredSite) &&
                    restoredSite.RemainingAmount == savedRemaining && root.InhabitableRegion.HasCanonicalMapCorrespondence;
                var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Season == Game.Domain.Weather.Season.Winter);
                root.WeatherPresentation.ShowPreview(winter);
                result.season_projection_visible = root.InhabitableRegion.DisplayedSeason ==
                    Game.Domain.Weather.Season.Winter && root.InhabitableRegion.SnowCoverVisible;
                PrepareRtsCamera();
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Path.Combine(outputDirectory, "p5-after-load-winter.png");
            Capture(result.after_load_screenshot);
            Finish(result);
        }

        private void PrepareRtsCamera()
        {
            root.CameraController.transform.position = new Vector3(7f, 0f, 4f);
            root.CameraController.transform.rotation = Quaternion.Euler(0f, -28f, 0f);
            root.CameraController.enabled = true;
        }

        private void Finish(SmokeResult result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.region_initialized &&
                result.canonical_map_correspondence && result.canonical_resources_bound &&
                result.resource_gathered && result.building_placed && result.navmesh_route_complete &&
                result.rts_traversal_completed && result.tps_rendered && result.rival_territory_visible &&
                result.global_map_rendered && result.save_load_identity_preserved && result.season_projection_visible &&
                result.resource_node_count >= 12 && result.tree_count >= 32 && result.rock_count >= 18 &&
                result.route_count >= 4;
            var path = Path.Combine(outputDirectory, "p5-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P5_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
        }

        private void CompleteTechnology(string id)
        {
            var technologyId = new Game.Domain.Research.TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            if (!root.Research.Start(technologyId))
                throw new InvalidOperationException("Could not start required P5 smoke research: " + id);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
            if (!root.Research.IsCompleted(technologyId))
                throw new InvalidOperationException("Required P5 smoke research did not complete: " + id);
        }

        private static void Capture(string path)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (var i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
    }
}
