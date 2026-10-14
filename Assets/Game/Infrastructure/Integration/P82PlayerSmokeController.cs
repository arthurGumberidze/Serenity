using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.Production;
using Game.Presentation.World;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Windows Player proof for the complete P8.2 local/global resource loop.</summary>
    public sealed class P82PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool finite_boundary;
            public bool all_local_resources_clickable;
            public bool all_global_resources_clickable;
            public bool stable_domain_links;
            public bool navmesh_present;
            public int local_resource_sites;
            public int global_resource_markers;
            public string local_map_screenshot;
            public string selected_resource_screenshot;
            public string global_map_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p82Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(82002);
        }

        public static P82PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p82Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p82SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p82SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P82PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            ProductionSourcePresenter[] presenters = null;
            Try(result, () =>
            {
                root.Clock.Pause();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                var region = root.InhabitableRegion;
                region.ValidateConfiguration();
                region.ValidateGeography();
                result.finite_boundary = region.LandBoundary.Count >= 8 &&
                    region.AuthoredPlacementAnchors.All(x => region.ContainsLandPoint(x.position));
                presenters = FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                    .Where(x => x.IsBound).ToArray();
                var expected = new[] { "berries", "plant_fiber", "wood_log", "stone", "iron_ore", "gold_ore" };
                var projected = presenters.Select(x => root.Production.Recipes.Get(x.Site.RecipeId).Outputs[0].ResourceId.ToString())
                    .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
                result.local_resource_sites = presenters.Length;
                result.all_local_resources_clickable = expected.All(projected.Contains) &&
                    presenters.All(x => x.GetComponentInChildren<Collider>() != null);
                result.stable_domain_links = presenters.All(x => root.Production.TryGetSite(x.Site.Id, out var site) &&
                    ReferenceEquals(site, x.Site));
                result.navmesh_present = root.NavigationSurface != null && root.NavigationSurface.navMeshData != null;
                root.CameraController.enabled = false;
                root.CameraController.transform.SetPositionAndRotation(new Vector3(13f, 0f, 0f), Quaternion.identity);
                root.CameraController.CameraMount.localPosition = Quaternion.Euler(57f, 0f, 0f) * Vector3.back * 82f;
                root.CameraController.CameraMount.localRotation = Quaternion.Euler(57f, 0f, 0f);
            });
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.local_map_screenshot = Capture("p82-local-map.png");
                    var selected = presenters.First(x => root.Production.Recipes.Get(x.Site.RecipeId).Outputs[0].ResourceId.ToString() == "gold_ore");
                    root.SelectionProbe.SelectProductionSource(selected);
                    root.GameplayUi.RefreshNow();
                    root.CameraController.transform.position = selected.transform.position;
                    root.CameraController.CameraMount.localPosition = Quaternion.Euler(45f, 0f, 0f) * Vector3.back * 19f;
                    root.CameraController.CameraMount.localRotation = Quaternion.Euler(45f, 0f, 0f);
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.selected_resource_screenshot = Capture("p82-selected-resource.png");
                    root.GlobalMapBridge.RequestEnterGlobal();
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                var deadline = Time.realtimeSinceStartup + 20f;
                while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(1.5f);
                Try(result, () =>
                {
                    var global = FindAnyObjectByType<GlobalMapPresentationController>();
                    if (global == null) throw new InvalidOperationException("Global map presentation did not load.");
                    var markers = global.GetComponentsInChildren<GlobalMapPresentationController.GlobalResourceMarker>(true);
                    var expected = new[] { "berries", "plant_fiber", "wood_log", "stone", "iron_ore", "gold_ore" };
                    result.global_resource_markers = markers.Length;
                    result.all_global_resources_clickable = expected.All(id => markers.Any(x => x.ResourceId.ToString() == id)) &&
                        markers.All(x => x.GetComponent<Collider>() != null && x.SiteIds.Count > 0);
                    global.SelectResourceMarker(markers.First(x => x.ResourceId.ToString() == "iron_ore"));
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForEndOfFrame();
                Try(result, () => result.global_map_screenshot = Capture("p82-global-map.png"));
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.finite_boundary &&
                result.all_local_resources_clickable && result.all_global_resources_clickable &&
                result.stable_domain_links && result.navmesh_present &&
                File.Exists(result.local_map_screenshot) && File.Exists(result.selected_resource_screenshot) &&
                File.Exists(result.global_map_screenshot);
            var path = Path.Combine(outputDirectory, "p82-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P82_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
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
