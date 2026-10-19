using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.World;
using Game.Domain.Work;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Integration
{
    /// <summary>Normal Windows Player evidence for the P10.9 tiled-world Stage 1 contract.</summary>
    public sealed class P109Stage1PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool menu_new_game;
            public bool tiled_geometry;
            public bool deterministic_seam;
            public bool bounded_visuals;
            public bool far_route_retained;
            public bool strategic_full_world;
            public bool save_generation_v2;
            public bool d3d11;
            public int width;
            public int height;
            public int visual_tiles;
            public int collision_tiles_after_route;
            public string generation_id;
            public string local_screenshot;
            public string strategic_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-p109Stage1Smoke", StringComparison.OrdinalIgnoreCase)))
                return;
            var output = ReadArgument(args, "-p109Stage1SmokeOutput");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("-p109Stage1SmokeOutput is required.");
            var host = new GameObject("P10.9 Tiled World Stage 1 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P109Stage1PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
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
                try
                {
                    if (!core.MoveNext()) break;
                    current = core.Current;
                }
                catch (Exception exception)
                {
                    result.error = exception.ToString();
                    break;
                }
                yield return current;
            }
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.tiled_geometry &&
                result.deterministic_seam && result.bounded_visuals && result.far_route_retained &&
                result.strategic_full_world && result.save_generation_v2 && result.d3d11 &&
                result.width == 1920 && result.height == 1080 &&
                !string.IsNullOrEmpty(result.local_screenshot) && !string.IsNullOrEmpty(result.strategic_screenshot);
            File.WriteAllText(Path.Combine(outputDirectory, "p109-stage1-player-smoke.json"),
                JsonUtility.ToJson(result, true));
            Debug.Log("P109_STAGE1_PLAYER_SMOKE_RESULT " + JsonUtility.ToJson(result));
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            for (var i = 0; i < 120 && (Screen.width != 1920 || Screen.height != 1080); i++) yield return null;
            result.width = Screen.width;
            result.height = Screen.height;
            result.d3d11 = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            menu.OpenNewGame();
            result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
            menu.StartNewGame(1092026);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null;
            yield return null;
            var root = FindAnyObjectByType<LocalSceneCompositionRoot>() ??
                throw new InvalidOperationException("LocalGameplay did not initialize.");
            root.Clock.Pause();
            root.GameplayUi.SetOnboardingVisible(false);
            root.GameplayUi.SetDeveloperUiVisible(false);
            result.generation_id = root.WorldDefinition.GenerationId;
            result.visual_tiles = root.TiledWorld.LoadedVisualTileCount;
            result.tiled_geometry = root.WorldDefinition.Columns == 12 && root.WorldDefinition.Rows == 12 &&
                root.WorldDefinition.TileSizeMeters == 1000 && root.WorldDefinition.Tiles.Count == 144 &&
                root.RealisticGameplayRegion != null && !root.RealisticGameplayRegion.Terrain.enabled;
            result.bounded_visuals = result.visual_tiles <= 9;
            var left = root.WorldHeight.CreateNormalizedHeights(new WorldTileId(5, 4), 65);
            var right = root.WorldHeight.CreateNormalizedHeights(new WorldTileId(6, 4), 65);
            result.deterministic_seam = Enumerable.Range(0, 65).All(z => left[z, 64] == right[z, 0]);
            yield return Capture("p109-stage1-local.png", value => result.local_screenshot = value);

            var far = root.WorldDefinition.Zones.Single(value => value.Id == "northwest_settlement").Center;
            var move = root.ManualMoveCommands.Issue(new[] { root.MaleDemoCharacter.Id }, far,
                WorkPriority.Immediate);
            root.WorldDefinition.TryGetTile(far.X, far.Z, out var destination);
            result.collision_tiles_after_route = root.TiledWorld.LoadedCollisionTileCount;
            result.far_route_retained = move.CreatedCount == 1 &&
                root.TiledWorld.LoadedTiles.Contains(destination) && result.collision_tiles_after_route > 9 &&
                root.TiledWorld.LoadedVisualTileCount <= 9;
            root.PlayerSaves.Save(1);
            result.save_generation_v2 = root.Saves.Capture().ConnectedWorld.GenerationId ==
                TiledWorldDefinition.ReferenceGenerationId;

            root.GlobalMapBridge.RequestEnterGlobal();
            yield return null;
            var camera = root.PointerRaycaster.WorldCamera;
            result.strategic_full_world = root.GlobalMapBridge.IsGlobalSceneLoaded &&
                root.TiledWorld.StrategicOverviewVisible && camera.orthographic && camera.orthographicSize >= 6240f;
            yield return Capture("p109-stage1-strategic.png", value => result.strategic_screenshot = value);
            root.GlobalMapBridge.RequestEnterLocation(ConnectedWorldContract.StarterSettlement);
            yield return null;
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
                completed(path);
                yield break;
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
