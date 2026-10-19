using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Domain.Weather;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Integration
{
    /// <summary>Normal Player evidence for P10.9 Stage 2 population and presentation.</summary>
    public sealed class P109Stage2PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed, menu_new_game, d3d11, canonical_population, bounded_views,
                legacy_routes_hidden, far_ore_visible, strategic_zones_visible, save_load_continuity;
            public int width, height, resource_nodes, industry_zones, active_resource_views;
            public string starter_screenshot, far_screenshot, strategic_screenshot, error;
        }

        private MainMenuController menu;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-p109Stage2Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var output = ReadArgument(args, "-p109Stage2SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p109Stage2SmokeOutput is required.");
            var host = new GameObject("P10.9 Tiled World Stage 2 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P109Stage2PlayerSmokeController>();
            controller.menu = source; controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new Result();
            var core = RunCore(result);
            while (true)
            {
                object current;
                try { if (!core.MoveNext()) break; current = core.Current; }
                catch (Exception exception) { result.error = exception.ToString(); break; }
                yield return current;
            }
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.d3d11 &&
                result.width == 1920 && result.height == 1080 && result.canonical_population &&
                result.bounded_views && result.legacy_routes_hidden && result.far_ore_visible &&
                result.strategic_zones_visible && result.save_load_continuity &&
                File.Exists(result.starter_screenshot) && File.Exists(result.far_screenshot) &&
                File.Exists(result.strategic_screenshot);
            File.WriteAllText(Path.Combine(outputDirectory, "p109-stage2-player-smoke.json"),
                JsonUtility.ToJson(result, true));
            Debug.Log("P109_STAGE2_PLAYER_SMOKE_RESULT " + JsonUtility.ToJson(result));
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            for (var i = 0; i < 120 && (Screen.width != 1920 || Screen.height != 1080); i++) yield return null;
            result.width = Screen.width; result.height = Screen.height;
            result.d3d11 = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            menu.OpenNewGame(); result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
            menu.StartNewGame(1092026);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null; yield return null;
            var root = FindAnyObjectByType<LocalSceneCompositionRoot>() ??
                throw new InvalidOperationException("LocalGameplay did not initialize.");
            root.Clock.Pause(); root.GameplayUi.SetOnboardingVisible(false); root.GameplayUi.SetDeveloperUiVisible(false);
            var dry = Enumerable.Range(0, 1460).Select(day => root.Weather.GetWeatherForDay(day))
                .First(value => value.Precipitation == PrecipitationType.None);
            root.WeatherPresentation.ShowPreview(dry, 12 * TimeSpan.TicksPerHour);
            root.CameraController.enabled = false;
            var worldCamera = Camera.main ?? root.CameraController.GetComponentInChildren<Camera>(true);
            if (worldCamera == null) throw new InvalidOperationException("Gameplay camera is unavailable.");
            var brain = worldCamera.GetComponents<Behaviour>().FirstOrDefault(value =>
                value.GetType().FullName == "Unity.Cinemachine.CinemachineBrain");
            if (brain != null) brain.enabled = false;
            result.resource_nodes = root.WorldPopulation.Plan.Resources.Count;
            result.industry_zones = root.WorldPopulation.Plan.Industry.Count;
            result.active_resource_views = root.WorldPopulation.ActiveResourceViewCount;
            result.canonical_population = result.resource_nodes >= 720 && result.industry_zones > 0 &&
                root.Production.Sites.Count >= result.resource_nodes;
            result.bounded_views = root.WorldPopulation.ActiveTileViewCount <= 9 &&
                result.active_resource_views == root.WorldPopulation.Plan.Resources.Count(node =>
                    root.TiledWorld.VisualTiles.Contains(node.Tile));
            result.legacy_routes_hidden = FindObjectsByType<LineRenderer>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(value => value.name.StartsWith("P10.2 Physical Route "))
                .All(value => !value.gameObject.activeSelf);
            var starterNode = root.WorldPopulation.Plan.Resources.Where(node => node.RecipeId == "chop_wood").OrderBy(node =>
            {
                var dx = node.Position.X - root.WorldDefinition.PlayerStart.X;
                var dz = node.Position.Z - root.WorldDefinition.PlayerStart.Z;
                return dx * dx + dz * dz;
            }).First();
            root.TiledWorld.RefreshNow(starterNode.Position, Array.Empty<WorldPosition>(),
                Array.Empty<Tuple<WorldPosition, WorldPosition>>());
            yield return null; yield return null;
            Frame(root, PresenterCenter(root, root.WorldPopulation.ResourceId(starterNode), starterNode.Position),
                32f, 30f, 16f);
            yield return null;
            yield return Capture("p109-stage2-starter.png", value => result.starter_screenshot = value);

            var ore = root.WorldDefinition.Zones.First(value => value.Uses.HasFlag(WorldZoneUse.Ore));
            var oreNode = root.WorldPopulation.Plan.Resources.First(node => node.SourceZoneId == ore.Id &&
                (node.RecipeId == "gather_iron" || node.RecipeId == "gather_gold"));
            root.TiledWorld.RefreshNow(oreNode.Position, Array.Empty<WorldPosition>(),
                Array.Empty<Tuple<WorldPosition, WorldPosition>>());
            yield return null; yield return null;
            Frame(root, PresenterCenter(root, root.WorldPopulation.ResourceId(oreNode), oreNode.Position),
                38f, 30f, 12f);
            yield return null;
            result.far_ore_visible = root.WorldDefinition.TryGetTile(ore.Center.X, ore.Center.Z, out var oreTile) &&
                root.TiledWorld.VisualTiles.Contains(oreTile) && root.WorldPopulation.Plan.Resources.Any(node =>
                    node.Tile.Equals(oreTile) && (node.RecipeId == "gather_iron" || node.RecipeId == "gather_gold"));
            yield return Capture("p109-stage2-far-ore.png", value => result.far_screenshot = value);

            var tracked = root.WorldPopulation.Plan.Resources.First(node => node.Tile.Equals(oreTile));
            var trackedId = root.WorldPopulation.ResourceId(tracked);
            root.Production.TryGetSite(trackedId, out var before);
            var remaining = before.RemainingAmount;
            root.PlayerSaves.Save(2); root.PlayerSaves.Load(2);
            result.save_load_continuity = root.Production.TryGetSite(trackedId, out var after) &&
                after.RemainingAmount == remaining;

            var centerX = root.WorldDefinition.Origin.X + root.WorldDefinition.WidthMeters * 0.5f;
            var centerZ = root.WorldDefinition.Origin.Z + root.WorldDefinition.DepthMeters * 0.5f;
            root.ConnectedWorld.RevealAround(centerX, centerZ, root.WorldDefinition.WidthMeters);
            root.GlobalMapBridge.RequestEnterGlobal(); yield return null;
            result.strategic_zones_visible = root.TiledWorld.StrategicOverviewVisible &&
                FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Count(value => value.name.StartsWith("Zone ")) >= root.WorldDefinition.Zones.Count -
                    root.WorldDefinition.Zones.Count(value => value.Uses.HasFlag(WorldZoneUse.Fishing));
            yield return Capture("p109-stage2-strategic.png", value => result.strategic_screenshot = value);
            root.GlobalMapBridge.RequestEnterLocation(root.GlobalMap.ActiveLocationId);
            yield return null;
        }

        private static WorldPosition PresenterCenter(LocalSceneCompositionRoot root, StableEntityId id,
            WorldPosition fallback)
        {
            var presenter = FindObjectsByType<Game.Presentation.Production.ProductionSourcePresenter>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(value => value.IsBound && value.Site.Id == id);
            if (presenter == null) return fallback;
            var renderers = presenter.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new WorldPosition(presenter.transform.position.x,
                presenter.transform.position.y, presenter.transform.position.z);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return new WorldPosition(bounds.center.x, bounds.center.y, bounds.center.z);
        }

        private static void Frame(LocalSceneCompositionRoot root, WorldPosition target,
            float yaw, float pitch, float distance)
        {
            var camera = Camera.main ?? root.CameraController.GetComponentInChildren<Camera>(true);
            var center = new Vector3(target.X, target.Y, target.Z);
            var offset = Quaternion.Euler(0f, yaw, 0f) *
                         (Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance);
            camera.transform.SetPositionAndRotation(center + offset,
                Quaternion.LookRotation(center - (center + offset), Vector3.up));
        }

        private IEnumerator Capture(string fileName, Action<string> completed)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(outputDirectory, fileName);
            var captureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var method = captureType?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (method == null) { completed(string.Empty); yield break; }
            method.Invoke(null, new object[] { path, 1 });
            for (var i = 0; i < 120; i++)
            {
                yield return null;
                if (!File.Exists(path) || new FileInfo(path).Length == 0) continue;
                completed(path); yield break;
            }
            completed(string.Empty);
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
