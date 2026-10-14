using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible proof of the P8.5 menu, editor, RTS, global-map and TPS presentation.</summary>
    public sealed class P85PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool main_menu;
            public bool ruler_editor;
            public bool local_rts;
            public bool global_map;
            public bool tps;
            public bool stable_test_hooks;
            public string main_menu_screenshot;
            public string editor_screenshot;
            public string local_screenshot;
            public string global_screenshot;
            public string tps_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p85Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-p85SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-p85SmokeOutput is required.");
            var host = new GameObject("P8.5 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P85PlayerSmokeController>();
            controller.menu = source;
            controller.output = target;
            controller.StartCoroutine(controller.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
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
            Finish(result);
        }

        private IEnumerator RunCore(Result result)
        {
            yield return EnsureAcceptanceResolution();
            menu.SetUiScale(1.25f);
            yield return null; yield return null;
            result.resolution_1280x720 = IsAcceptanceResolution();
            result.scale_125_percent = Mathf.Abs(menu.UiScale - 1.25f) < 0.01f;
            result.main_menu = HasHooks(menu.RootVisual, "menu-content", "new-game-button", "settings-button") &&
                               HasVisualLanguage(menu.RootVisual);
            result.main_menu_screenshot = Path.Combine(output, "p85-main-menu.png");
            yield return Capture(result.main_menu_screenshot);

            menu.OpenNewGame();
            yield return null; yield return null;
            result.ruler_editor = HasHooks(menu.RootVisual, "ruler-editor-grid", "ruler-identity-column",
                "ruler-appearance-column", "start-new-game-button") &&
                menu.RootVisual.Q("ruler-editor-grid").ClassListContains("serenity-editor-grid");
            result.stable_test_hooks = HasHooks(menu.RootVisual, "world-seed", "ruler-name", "dynasty-name",
                "appearance-profile", "appearance-skin", "appearance-hair");
            menu.RootVisual.Q<TextField>("world-seed").value = "85005";
            menu.RootVisual.Q<TextField>("ruler-name").value = "Велемира";
            menu.RootVisual.Q<TextField>("dynasty-name").value = "Дом Тихой Сосны";
            result.editor_screenshot = Path.Combine(output, "p85-ruler-editor.png");
            yield return Capture(result.editor_screenshot);
            if (!menu.TryStartEditedNewGame()) throw new InvalidOperationException("P8.5 new-game confirmation failed.");

            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return EnsureAcceptanceResolution();
            yield return null; yield return null;
            var root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null) throw new InvalidOperationException("Local composition root is missing.");
            var ui = root.GameplayUi;
            ui.SetUiScale(1.25f);
            root.SelectionProbe.SelectCharacters(new[] { root.DemoPresenters[0] });
            ui.SetPanel(GameplayPanel.Economy);
            ui.RefreshNow();
            yield return null; yield return null;
            result.resolution_1280x720 &= IsAcceptanceResolution();
            result.scale_125_percent &= Mathf.Abs(ui.UiScale - 1.25f) < 0.01f;
            result.local_rts = HasHooks(ui.RootVisual, "top-hud", "navigation", "main-panel",
                "selection-inspector", "status-bar") && HasVisualLanguage(ui.RootVisual) &&
                ui.ActivePanel == GameplayPanel.Economy;
            result.stable_test_hooks &= ui.RootVisual.Q<Button>("pause-button") != null &&
                                        ui.RootVisual.Q<Button>("main-menu-button") != null;
            result.local_screenshot = Path.Combine(output, "p85-local-rts.png");
            yield return Capture(result.local_screenshot);

            ui.SetPanel(GameplayPanel.Global);
            root.GlobalMapBridge.RequestEnterGlobal();
            var globalDeadline = Time.realtimeSinceStartup + 10f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < globalDeadline)
                yield return null;
            if (!root.GlobalMapBridge.IsGlobalSceneLoaded) throw new InvalidOperationException("Global map did not load.");
            yield return EnsureAcceptanceResolution();
            ui.RefreshNow();
            yield return new WaitForSecondsRealtime(0.8f);
            result.resolution_1280x720 &= IsAcceptanceResolution();
            result.global_map = root.GlobalMapBridge.IsGlobalSceneLoaded && ui.ActivePanel == GameplayPanel.Global;
            result.global_screenshot = Path.Combine(output, "p85-global-map.png");
            yield return Capture(result.global_screenshot);

            root.GlobalMapBridge.RequestEnterLocation(root.GlobalMap.ActiveLocationId);
            var localDeadline = Time.realtimeSinceStartup + 10f;
            while (root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < localDeadline)
                yield return null;
            if (root.GlobalMapBridge.IsGlobalSceneLoaded) throw new InvalidOperationException("Local scene did not return.");
            yield return EnsureAcceptanceResolution();
            var entered = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
            if (!entered.IsEligible) throw new InvalidOperationException("TPS entry is not eligible: " + entered.Failure);
            yield return new WaitForSecondsRealtime(0.8f);
            ui.RefreshNow();
            result.resolution_1280x720 &= IsAcceptanceResolution();
            result.tps = root.ThirdPersonControl.IsThirdPersonCameraActive &&
                         ui.RootVisual.Q("tps-hud").resolvedStyle.display == DisplayStyle.Flex;
            result.tps_screenshot = Path.Combine(output, "p85-tps.png");
            yield return Capture(result.tps_screenshot);
            root.ThirdPersonControl.Exit();
        }

        private void Finish(Result result)
        {
            var screenshots = new[] { result.main_menu_screenshot, result.editor_screenshot, result.local_screenshot,
                result.global_screenshot, result.tps_screenshot };
            result.passed = string.IsNullOrEmpty(result.error) && result.resolution_1280x720 &&
                result.scale_125_percent && result.main_menu && result.ruler_editor && result.local_rts &&
                result.global_map && result.tps && result.stable_test_hooks &&
                screenshots.All(path => !string.IsNullOrEmpty(path) && File.Exists(path));
            var report = Path.Combine(output, "p85-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P85_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static bool HasHooks(VisualElement root, params string[] names) =>
            root != null && names.All(name => root.Q(name) != null);

        private static bool HasVisualLanguage(VisualElement root) =>
            root != null && root.ClassListContains(SerenityUiDesign.RootClass) &&
            root.Query<Button>().ToList().All(button => button.ClassListContains(SerenityUiDesign.ButtonClass));

        private static IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
        }

        private static IEnumerator EnsureAcceptanceResolution()
        {
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 8f;
            while (!IsAcceptanceResolution() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!IsAcceptanceResolution())
                throw new InvalidOperationException($"Expected 1280x720, got {Screen.width}x{Screen.height}.");
        }

        private static bool IsAcceptanceResolution() => Screen.width == 1280 && Screen.height == 720;

        private static string Arg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
