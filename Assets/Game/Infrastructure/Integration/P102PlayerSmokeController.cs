using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Research;
using Game.Domain.World;
using Game.Presentation.Environment;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible P10.2 acceptance route at the fixed 1920x1080 contract.</summary>
    public sealed class P102PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool same_world_strategic_view;
            public bool connected_navigation;
            public bool construction;
            public bool fog_initial;
            public bool fog_physical;
            public bool fog_scouting;
            public bool save_load;
            public bool rain_snow_seasons;
            public bool fps_displayed;
            public int width;
            public int height;
            public int accessible_area_m2;
            public int location_count;
            public int resource_anchor_count;
            public float frame_ms_p50;
            public float frame_ms_p95;
            public float frame_ms_p99;
            public float frame_ms_max;
            public string local_frame;
            public string strategic_frame;
            public string after_load_frame;
            public string error;
        }

        private MainMenuController menu;
        private string output;
        private readonly List<float> frameTimes = new List<float>();

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p102Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var directory = ReadArgument(args, "-p102SmokeOutput");
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("-p102SmokeOutput is required.");
            var host = new GameObject("P10.2 Connected World Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P102PlayerSmokeController>();
            controller.menu = source; controller.output = directory;
            controller.StartCoroutine(controller.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            var result = new Result { rain_snow_seasons = false, fps_displayed = false };
            var core = RunCore(result);
            while (true)
            {
                object current;
                try
                {
                    if (!core.MoveNext()) break;
                    current = core.Current;
                }
                catch (Exception ex)
                {
                    result.error = ex.ToString(); result.passed = false; break;
                }
                yield return current;
            }
            File.WriteAllText(Path.Combine(output, "p102-player-smoke.json"), JsonUtility.ToJson(result, true));
            Debug.Log("P102_PLAYER_SMOKE_RESULT " + JsonUtility.ToJson(result));
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                for (var i = 0; i < 120 && (Screen.width != 1920 || Screen.height != 1080); i++) yield return null;
                result.width = Screen.width; result.height = Screen.height;
                if (result.width != 1920 || result.height != 1080) throw new InvalidOperationException("1920x1080 was not applied.");
                menu.StartNewGame(102002);
                while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
                yield return null; yield return null;
                var root = FindAnyObjectByType<LocalSceneCompositionRoot>() ??
                    throw new InvalidOperationException("Local composition root is missing.");
                var world = FindAnyObjectByType<P102ConnectedWorldPresentation>() ??
                    throw new InvalidOperationException("Connected-world marker is missing.");
                world.ValidateConfiguration();
                root.Clock.Pause(); root.GameplayUi.SetOnboardingVisible(false); root.GameplayUi.SetDeveloperUiVisible(false);
                result.accessible_area_m2 = world.AccessibleAreaSquareMeters;
                result.location_count = world.Districts.Count;
                result.resource_anchor_count = world.CanonicalResourceAnchorCount;
                result.fog_initial = root.ConnectedWorld.IsLocationRevealed(ConnectedWorldContract.StarterSettlement) &&
                    !root.ConnectedWorld.IsLocationRevealed(new WorldLocationId("enemy_settlement"));
                root.ConnectedWorld.ReportPhysicalPresence(-132f, 62f);
                result.fog_physical = root.ConnectedWorld.IsLocationRevealed(new WorldLocationId("west_marsh"));
                var scout = DeterministicStableId.Create(102002, "p102_smoke_scout");
                root.ConnectedWorld.AssignScout(scout, new WorldLocationId("enemy_settlement"),
                    root.Clock.State.CalendarTicks, TimeSpan.TicksPerHour);
                root.Clock.Resume();
                root.Clock.Advance(TimeSpan.FromMinutes(1));
                root.Clock.Pause();
                root.ConnectedWorld.AdvanceTo(root.Clock.State.CalendarTicks);
                result.fog_scouting = root.ConnectedWorld.IsLocationRevealed(new WorldLocationId("enemy_settlement"));

                result.connected_navigation = CheckPath(root, "starter_settlement", "river_ford") &&
                    CheckPath(root, "river_ford", "enemy_settlement") && CheckPath(root, "starter_settlement", "south_field");
                var starter = world.Districts.Single(x => x.Id == "starter_settlement").Anchor.position;
                root.CameraController.transform.position = starter;
                yield return CollectFrames(90);
                yield return CaptureFrame("p102-local-connected-world.png", path => result.local_frame = path);

                root.GlobalMapBridge.RequestEnterGlobal(); yield return null;
                if (!root.GlobalMapBridge.IsGlobalSceneLoaded || SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("Strategic view did not reuse the LocalGameplay scene.");
                yield return CollectFrames(120);
                yield return CaptureFrame("p102-strategic-same-world.png", path => result.strategic_frame = path);
                result.same_world_strategic_view = root.PointerRaycaster.WorldCamera.orthographic &&
                    FindAnyObjectByType<P102ConnectedWorldPresentation>().Terrain == world.Terrain;
                root.GlobalMapBridge.RequestEnterLocation(ConnectedWorldContract.StarterSettlement); yield return null;

                CompleteTechnology(root, "shelter_construction");
                if (!root.BuildingPlacementController.TryStartPlacement(new BuildingDefinitionId("primitive_shelter")) ||
                    !root.BuildingPlacementController.TryMovePreviewToWorldPoint(starter + new Vector3(11f, 0f, 9f)))
                    throw new InvalidOperationException("Dry settlement land rejected construction.");
                result.construction = root.BuildingPlacementController.ConfirmPlacement() != null;
                root.PlayerSaves.Save(2);
                var revealCount = root.ConnectedWorld.RevealedCellCount;
                root.ConnectedWorld.ReportPhysicalPresence(202f, -54f);
                root.PlayerSaves.Load(2);
                result.save_load = root.ConnectedWorld.RevealedCellCount == revealCount &&
                    root.ConnectedWorld.IsLocationRevealed(new WorldLocationId("enemy_settlement"));
                yield return CollectFrames(90);
                yield return CaptureFrame("p102-after-load-construction.png", path => result.after_load_frame = path);
                ComputeFrameStats(result);
                result.passed = result.same_world_strategic_view && result.connected_navigation && result.construction &&
                    result.fog_initial && result.fog_physical && result.fog_scouting && result.save_load &&
                    result.width == 1920 && result.height == 1080 &&
                    result.accessible_area_m2 >= ConnectedWorldContract.MinimumAccessibleAreaSquareMeters &&
                    result.location_count >= 10 && result.resource_anchor_count == 24 &&
                    !string.IsNullOrWhiteSpace(result.local_frame) &&
                    !string.IsNullOrWhiteSpace(result.strategic_frame) &&
                    !string.IsNullOrWhiteSpace(result.after_load_frame);
        }

        private IEnumerator CollectFrames(int count)
        {
            for (var i = 0; i < count; i++) { yield return null; if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime * 1000f); }
        }

        private IEnumerator CaptureFrame(string name, Action<string> completed)
        {
            var path = Path.Combine(output, name);
            var captureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var captureMethod = captureType?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (captureMethod == null)
            {
                completed(string.Empty);
                yield break;
            }
            captureMethod.Invoke(null, new object[] { path, 1 });
            for (var i = 0; i < 120; i++)
            {
                yield return null;
                if (!File.Exists(path) || new FileInfo(path).Length == 0) continue;
                completed(path);
                yield break;
            }
            completed(string.Empty);
        }

        private static bool CheckPath(LocalSceneCompositionRoot root, string from, string to)
        {
            root.RealisticGameplayRegion.TryGetLocationAnchor(from, out var a);
            root.RealisticGameplayRegion.TryGetLocationAnchor(to, out var b);
            if (a == null || b == null || !NavMesh.SamplePosition(a.position, out var start, 5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(b.position, out var end, 5f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete;
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new TechnologyId(id); if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id); root.Research.Start(technologyId);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }

        private void ComputeFrameStats(Result result)
        {
            var values = frameTimes.OrderBy(x => x).ToArray(); if (values.Length == 0) return;
            result.frame_ms_p50 = Percentile(values, 0.50f); result.frame_ms_p95 = Percentile(values, 0.95f);
            result.frame_ms_p99 = Percentile(values, 0.99f); result.frame_ms_max = values[values.Length - 1];
        }
        private static float Percentile(float[] sorted, float value) => sorted[Mathf.Clamp(
            Mathf.CeilToInt(sorted.Length * value) - 1, 0, sorted.Length - 1)];
        private static string ReadArgument(string[] args, string name)
        { for (var i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
    }
}
