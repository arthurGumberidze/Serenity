using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible exact-binary proof for UI06 local and strategic map projections.</summary>
    public sealed class UI06PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1920x1080;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool local_minimap_known_world;
            public bool minimap_camera_focus;
            public bool realm_layers_legend;
            public bool known_locations_armies_routes;
            public bool route_duration_weather_supply_risk;
            public bool canonical_fog_scouting;
            public bool diplomacy_honest_unavailable;
            public bool stable_selection;
            public bool local_camera_restored;
            public string screenshot_local_1920x1080;
            public string screenshot_realm_1920x1080;
            public string screenshot_realm_1280x720_125;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui06Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui06SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-ui06SmokeOutput is required.");
            var host = new GameObject("UI06 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI06PlayerSmokeController>();
            controller.menu = source;
            controller.output = Path.GetFullPath(target);
            Application.runInBackground = true;
            controller.StartCoroutine(controller.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            var result = new Result();
            var route = RunCore(result);
            while (true)
            {
                object current;
                try
                {
                    if (!route.MoveNext()) break;
                    current = route.Current;
                }
                catch (Exception exception)
                {
                    result.error = exception.ToString();
                    break;
                }
                yield return current;
            }
            result.passed = string.IsNullOrEmpty(result.error) && result.resolution_1920x1080 &&
                result.resolution_1280x720 && result.scale_125_percent && result.local_minimap_known_world &&
                result.minimap_camera_focus && result.realm_layers_legend && result.known_locations_armies_routes &&
                result.route_duration_weather_supply_risk && result.canonical_fog_scouting &&
                result.diplomacy_honest_unavailable && result.stable_selection && result.local_camera_restored &&
                File.Exists(result.screenshot_local_1920x1080) && File.Exists(result.screenshot_realm_1920x1080) &&
                File.Exists(result.screenshot_realm_1280x720_125);
            var report = Path.Combine(output, "ui06-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI06_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            yield return SetResolution(1920, 1080);
            menu.StartNewGame(20261031);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            var deadline = Time.realtimeSinceStartup + 600f;
            LocalSceneCompositionRoot root = null;
            while (root == null && Time.realtimeSinceStartup < deadline)
            {
                root = FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null || root.GameplayUi == null) { root = null; yield return null; }
            }
            if (root == null) throw new TimeoutException("Local gameplay did not initialize within 600 seconds.");
            while (FindAnyObjectByType<WorldLoadingOverlay>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;

            var ui = root.GameplayUi;
            ui.SetUiScale(1f);
            ui.RefreshNow();
            yield return null; yield return null;
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.local_minimap_known_world = ui.RootVisual.Q("local-mini-map") != null &&
                ui.RootVisual.Q("local-mini-map-grid") != null && root.ConnectedWorld.RevealedCellCount > 0;
            var active = root.GlobalMap.GetLocation(root.GlobalMap.ActiveLocationId);
            var layout = root.ConnectedWorld.Layout;
            result.minimap_camera_focus = ui.FocusLocalMiniMap((active.MapX - layout.OriginX) / layout.WidthMeters,
                (active.MapY - layout.OriginZ) / layout.DepthMeters);
            ui.RefreshNow(); yield return null;
            result.screenshot_local_1920x1080 = Path.Combine(output, "ui06-local-minimap-1920x1080.png");
            yield return Capture(result.screenshot_local_1920x1080);

            var selectedId = root.Dynasty.CurrentMonarchId.Value;
            root.SelectionProbe.SelectCharacterIds(new[] { selectedId });
            var localPosition = root.CameraController.transform.position;
            var localRotation = root.CameraController.transform.rotation;
            var localZoom = root.CameraController.CurrentZoom;
            root.ConnectedWorld.RevealAll();
            root.StartDemoUnitBattle(Game.Domain.Combat.BattleMode.Abstract);
            var unit = root.MilitaryUnits.Units.First(value => value.OwnerSideId.Value == "player");
            root.MilitaryUnits.SetAbstractLocation(unit.Id, root.GlobalMap.ActiveLocationId.Value);
            unit = root.MilitaryUnits.Get(unit.Id);
            if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                unit = root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            var origin = new WorldLocationId(unit.AbstractLocationId);
            var direct = root.GlobalMap.GetRoutes(origin).First();
            var destination = direct.From == origin ? direct.To : direct.From;
            ui.Navigate(GameplayUiRouteId.Realm);
            ui.SelectMapUnit(unit.Id);
            ui.SelectMapLocation(destination);
            ui.RefreshNow();
            yield return null; yield return null;

            var labels = Labels(ui);
            result.realm_layers_legend = ui.RootVisual.Q("realm-layers") != null &&
                labels.Any(value => value.Contains("Слои и легенда")) && labels.Any(value => value.Contains("Туман и разведка"));
            result.known_locations_armies_routes = ui.RootVisual.Q("realm-map-overlay") != null &&
                ui.RootVisual.Q("realm-map-location-" + destination.Value) != null &&
                ui.RootVisual.Q("realm-map-unit-" + unit.Id) != null;
            result.route_duration_weather_supply_risk = labels.Any(value => value == "Маршрут") &&
                labels.Any(value => value == "Погода") && labels.Any(value => value == "Снабжение") &&
                labels.Any(value => value == "Риск");
            result.canonical_fog_scouting = root.GlobalMapBridge.IsGlobalSceneLoaded &&
                root.ConnectedWorld.RevealedCellCount == root.ConnectedWorld.Layout.Columns * root.ConnectedWorld.Layout.Rows;
            var diplomacy = ui.RootVisual.Query<Button>("diplomacy-unavailable").ToList();
            result.diplomacy_honest_unavailable = diplomacy.Count == 3 && diplomacy.All(value => !value.enabledSelf) &&
                labels.Any(value => value.Contains("пока не моделируются"));
            result.stable_selection = root.SelectionProbe.SelectedCharacterId == selectedId;
            result.screenshot_realm_1920x1080 = Path.Combine(output, "ui06-realm-1920x1080.png");
            yield return Capture(result.screenshot_realm_1920x1080);

            yield return SetResolution(1280, 720);
            ui.SetUiScale(1.25f);
            ui.RefreshNow(); yield return null; yield return null;
            result.resolution_1280x720 = Screen.width == 1280 && Screen.height == 720;
            result.scale_125_percent = Mathf.Abs(ui.UiScale - 1.25f) < .01f;
            result.screenshot_realm_1280x720_125 = Path.Combine(output, "ui06-realm-1280x720-125.png");
            yield return Capture(result.screenshot_realm_1280x720_125);

            ui.SetPanel(GameplayPanel.Global);
            ui.RefreshNow(); yield return null; yield return null;
            result.local_camera_restored = root.GlobalMap.Mode == GameWorldMode.Local &&
                !root.GlobalMapBridge.IsGlobalSceneLoaded &&
                Vector3.Distance(root.CameraController.transform.position, localPosition) < .01f &&
                Quaternion.Angle(root.CameraController.transform.rotation, localRotation) < .01f &&
                Mathf.Abs(root.CameraController.CurrentZoom - localZoom) < .01f;
        }

        private static string[] Labels(GameplayUiController ui) => ui.RootVisual.Query<Label>().ToList()
            .Select(value => value.text ?? string.Empty).ToArray();

        private static IEnumerator SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, false);
            var deadline = Time.realtimeSinceStartup + 15f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
        }

        private static IEnumerator Capture(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            for (var frame = 0; frame < 30; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32();
            long brightness = 0;
            var samples = 0;
            var stride = Math.Max(1, pixels.Length / 2048);
            for (var index = 0; index < pixels.Length; index += stride)
            {
                brightness += pixels[index].r + pixels[index].g + pixels[index].b;
                samples++;
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            if (samples == 0 || brightness / (samples * 3d) < 10d)
                throw new InvalidOperationException("Screenshot is effectively black: " + path);
        }

        private static string Arg(string[] args, string key)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
