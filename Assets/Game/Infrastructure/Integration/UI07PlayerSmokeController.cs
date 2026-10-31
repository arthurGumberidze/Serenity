using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Presentation.UI;
using Game.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible exact-binary acceptance route for the complete UI02-UI07 player flow.</summary>
    public sealed class UI07PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool main_menu_actions;
            public bool ruler_editor_compact;
            public bool settings_compact;
            public bool keyboard_focus_visible;
            public bool pause_resume;
            public bool save_load;
            public bool realm_viewport_clear;
            public bool stable_routes;
            public bool resolution_1920x1080;
            public bool resolution_1280x720_125;
            public string menu_1920;
            public string ruler_1280_125;
            public string settings_1280_125;
            public string gameplay_1280_125;
            public string realm_1280_125;
            public string realm_1920;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui07Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui07SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-ui07SmokeOutput is required.");
            var host = new GameObject("UI07 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI07PlayerSmokeController>();
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
            result.passed = string.IsNullOrEmpty(result.error) && result.main_menu_actions &&
                result.ruler_editor_compact && result.settings_compact && result.keyboard_focus_visible &&
                result.pause_resume && result.save_load && result.realm_viewport_clear && result.stable_routes &&
                result.resolution_1920x1080 && result.resolution_1280x720_125 &&
                Existing(result.menu_1920, result.ruler_1280_125, result.settings_1280_125,
                    result.gameplay_1280_125, result.realm_1280_125, result.realm_1920);
            var report = Path.Combine(output, "ui07-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI07_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            yield return SetResolution(1920, 1080);
            menu.SetUiScale(1f);
            menu.ShowMain();
            yield return null; yield return null;
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.main_menu_actions = new[] { "continue-button", "new-game-button", "load-button", "settings-button", "exit-button" }
                .All(name => menu.RootVisual.Q<Button>(name) != null);
            var expectedMenuFocus = menu.ContinueAvailable ? "continue-button" : "new-game-button";
            result.keyboard_focus_visible = menu.RootVisual.panel.focusController.focusedElement ==
                                            menu.RootVisual.Q<Button>(expectedMenuFocus);
            result.menu_1920 = Path.Combine(output, "ui07-menu-1920x1080.png");
            yield return Capture(result.menu_1920);

            yield return SetResolution(1280, 720);
            menu.SetUiScale(1.25f);
            menu.OpenNewGame();
            yield return null; yield return null;
            result.resolution_1280x720_125 = Screen.width == 1280 && Screen.height == 720 &&
                                             Mathf.Abs(menu.UiScale - 1.25f) < .01f;
            result.ruler_editor_compact = menu.RootVisual.Q<TextField>("ruler-name") != null &&
                                          menu.RootVisual.Q("appearance-parts") != null &&
                                          Inside(menu.RootVisual, menu.RootVisual.Q("menu-content"));
            result.keyboard_focus_visible &= menu.RootVisual.panel.focusController.focusedElement ==
                                             menu.RootVisual.Q<TextField>("ruler-name");
            result.ruler_1280_125 = Path.Combine(output, "ui07-ruler-1280x720-125.png");
            yield return Capture(result.ruler_1280_125);

            menu.ShowMain();
            menu.OpenSettings();
            yield return null; yield return null;
            result.settings_compact = menu.RootVisual.Q<DropdownField>("settings-resolution") != null &&
                                      menu.RootVisual.Q<Button>("apply-settings-button") != null &&
                                      Inside(menu.RootVisual, menu.RootVisual.Q("menu-content"));
            result.settings_1280_125 = Path.Combine(output, "ui07-settings-1280x720-125.png");
            yield return Capture(result.settings_1280_125);

            menu.ShowMain();
            menu.OpenNewGame();
            menu.RootVisual.Q<TextField>("world-seed").value = "20261031";
            menu.RootVisual.Q<TextField>("ruler-name").value = "Арина";
            menu.RootVisual.Q<TextField>("dynasty-name").value = "Дом Тихой Реки";
            if (!menu.TryStartEditedNewGame()) throw new InvalidOperationException("Ruler editor rejected a valid draft.");
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
            ui.SetUiScale(1.25f);
            ui.RefreshNow();
            yield return null; yield return null;
            var wasPaused = root.Clock.IsPaused;
            ui.TogglePause();
            var toggled = root.Clock.IsPaused != wasPaused;
            ui.TogglePause();
            result.pause_resume = toggled && root.Clock.IsPaused == wasPaused;
            ui.SaveSlot(1);
            var session = root.Saves.SessionId;
            ui.RequestLoadSlot(1);
            yield return null;
            result.keyboard_focus_visible &= ui.RootVisual.panel.focusController.focusedElement != null;
            ui.ConfirmLoadSlot(1);
            result.save_load = root.PlayerSaves.ListSlots().Single(slot => slot.Index == 1).Exists &&
                               root.Saves.SessionId == session;
            ui.RefreshNow();
            result.gameplay_1280_125 = Path.Combine(output, "ui07-gameplay-1280x720-125.png");
            yield return Capture(result.gameplay_1280_125);

            root.ConnectedWorld.RevealAll();
            root.StartDemoUnitBattle(Game.Domain.Combat.BattleMode.Abstract);
            var unit = root.MilitaryUnits.Units.First(value => value.OwnerSideId.Value == "player");
            root.MilitaryUnits.SetAbstractLocation(unit.Id, root.GlobalMap.ActiveLocationId.Value);
            unit = root.MilitaryUnits.Get(unit.Id);
            if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                unit = root.MilitaryRepresentations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            var origin = new WorldLocationId(unit.AbstractLocationId);
            var route = root.GlobalMap.GetRoutes(origin).First();
            var destination = route.From == origin ? route.To : route.From;
            ui.Navigate(GameplayUiRouteId.Realm);
            ui.SelectMapUnit(unit.Id);
            ui.SelectMapLocation(destination);
            ui.RefreshNow();
            yield return null; yield return null;
            result.realm_viewport_clear = ViewportInsideMapCanvas(root, ui);
            result.stable_routes = GameplayUiRoutes.Ordered.All(item => ui.RootVisual.Q<Button>(item.StableElementName) != null) &&
                                   ui.RootVisual.Q("realm-map-header") != null &&
                                   ui.RootVisual.Q("realm-route-summary") != null;
            result.realm_1280_125 = Path.Combine(output, "ui07-realm-1280x720-125.png");
            yield return Capture(result.realm_1280_125);

            yield return SetResolution(1920, 1080);
            ui.SetUiScale(1f);
            ui.RefreshNow();
            yield return null; yield return null;
            result.realm_viewport_clear &= ViewportInsideMapCanvas(root, ui);
            result.realm_1920 = Path.Combine(output, "ui07-realm-1920x1080.png");
            yield return Capture(result.realm_1920);
        }

        private static bool ViewportInsideMapCanvas(LocalSceneCompositionRoot root, GameplayUiController ui)
        {
            var view = root.GetComponent<P102StrategicWorldView>();
            var overlay = ui.RootVisual.Q("realm-map-overlay");
            if (view == null || overlay == null || !view.IsActive) return false;
            var expected = new Rect(overlay.worldBound.xMin / ui.RootVisual.resolvedStyle.width,
                1f - overlay.worldBound.yMax / ui.RootVisual.resolvedStyle.height,
                overlay.worldBound.width / ui.RootVisual.resolvedStyle.width,
                overlay.worldBound.height / ui.RootVisual.resolvedStyle.height);
            return Mathf.Abs(view.CurrentViewport.x - expected.x) < .01f &&
                   Mathf.Abs(view.CurrentViewport.y - expected.y) < .01f &&
                   Mathf.Abs(view.CurrentViewport.width - expected.width) < .01f &&
                   Mathf.Abs(view.CurrentViewport.height - expected.height) < .01f;
        }

        private static bool Inside(VisualElement outer, VisualElement inner) => inner != null &&
            inner.worldBound.xMin >= outer.worldBound.xMin - 1f && inner.worldBound.yMin >= outer.worldBound.yMin - 1f &&
            inner.worldBound.xMax <= outer.worldBound.xMax + 1f && inner.worldBound.yMax <= outer.worldBound.yMax + 1f;

        private static bool Existing(params string[] paths) => paths.All(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

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
            for (var frame = 0; frame < 20; frame++) yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            var pixels = texture.GetPixels32();
            Destroy(texture);
            if (pixels.Length == 0 || pixels.Where((_, index) => index % Math.Max(1, pixels.Length / 2048) == 0)
                    .Average(pixel => (pixel.r + pixel.g + pixel.b) / 3d) < 10d)
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
