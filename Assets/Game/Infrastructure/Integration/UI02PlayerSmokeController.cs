using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player proof for the UI02 shell at both required acceptance resolutions.</summary>
    public sealed class UI02PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1920x1080;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool primary_routes;
            public bool button_input;
            public bool stable_compatibility_names;
            public bool world_center_visible;
            public bool attention_entry;
            public string interface_font;
            public string display_font;
            public string screenshot_1920x1080;
            public string screenshot_1280x720_125;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui02Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui02SmokeOutput");
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("-ui02SmokeOutput is required.");
            var host = new GameObject("UI02 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI02PlayerSmokeController>();
            controller.menu = source;
            controller.output = Path.GetFullPath(target);
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

            result.interface_font = SerenityUiTypography.InterfaceFamily;
            result.display_font = SerenityUiTypography.DisplayFamily;
            result.passed = string.IsNullOrEmpty(result.error) && result.resolution_1920x1080 &&
                result.resolution_1280x720 && result.scale_125_percent && result.primary_routes &&
                result.button_input && result.stable_compatibility_names && result.world_center_visible &&
                result.attention_entry && File.Exists(result.screenshot_1920x1080) &&
                File.Exists(result.screenshot_1280x720_125);
            var report = Path.Combine(output, "ui02-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI02_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private IEnumerator RunCore(Result result)
        {
            yield return SetResolution(1920, 1080);
            menu.StartNewGame(20261031);
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            var deadline = Time.realtimeSinceStartup + 600f;
            GameplayUiController ui = null;
            while (ui == null && Time.realtimeSinceStartup < deadline)
            {
                ui = FindAnyObjectByType<LocalSceneCompositionRoot>()?.GameplayUi;
                if (ui == null) yield return null;
            }
            if (ui == null) throw new TimeoutException("Gameplay UI did not initialize within 600 seconds.");
            while (FindAnyObjectByType<WorldLoadingOverlay>() != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (FindAnyObjectByType<WorldLoadingOverlay>() != null)
                throw new TimeoutException("World loading did not finish within 600 seconds.");
            yield return null;

            ui.SetUiScale(1f);
            ui.Navigate(GameplayUiRouteId.Settlement);
            ui.RefreshNow();
            yield return null; yield return null;
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.screenshot_1920x1080 = Path.Combine(output, "ui02-1920x1080.png");
            yield return Capture(result.screenshot_1920x1080);

            var navigation = ui.RootVisual.Q("navigation");
            result.primary_routes = GameplayUiRoutes.Ordered.All(route =>
                navigation.Q<Button>(route.StableElementName) != null);
            result.stable_compatibility_names = ui.RootVisual.Q<Button>("nav-build") != null &&
                ui.RootVisual.Q<Button>("nav-events") != null && ui.RootVisual.Q<Button>("nav-codes") != null &&
                ui.RootVisual.Q<Button>("main-menu-button") != null;
            ui.SetPanel(GameplayPanel.None);
            result.button_input = true;
            foreach (var route in GameplayUiRoutes.Ordered)
            {
                Submit(navigation.Q<Button>(route.StableElementName));
                yield return null;
                if (ui.ActiveRoute != route.Id) result.button_input = false;
            }
            ui.SetPanel(GameplayPanel.None);
            ui.RefreshNow();
            result.world_center_visible = !ui.IsGuiPointBlocked(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return SetResolution(1280, 720);
            ui.SetUiScale(1.25f);
            Submit(ui.RootVisual.Q<Button>("nav-events"));
            ui.RefreshNow();
            yield return null; yield return null;
            result.resolution_1280x720 = Screen.width == 1280 && Screen.height == 720;
            result.scale_125_percent = Mathf.Abs(ui.UiScale - 1.25f) < 0.01f;
            var attentionText = string.Join("\n", ui.RootVisual.Q("main-panel").Query<Label>().ToList()
                .Select(label => label.text));
            result.attention_entry = ui.ActiveRoute == GameplayUiRouteId.Attention &&
                attentionText.Contains("Центр внимания") && !attentionText.Contains("тестовый контракт");
            result.screenshot_1280x720_125 = Path.Combine(output, "ui02-1280x720-125.png");
            yield return Capture(result.screenshot_1280x720_125);
        }

        private static IEnumerator SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 10f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (Screen.width != width || Screen.height != height)
                throw new InvalidOperationException($"Expected {width}x{height}, got {Screen.width}x{Screen.height}.");
        }

        private static IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
        }

        private static void Submit(Button button)
        {
            if (button == null) throw new InvalidOperationException("Expected smoke-route button is missing.");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            if (invoke == null) throw new MissingMethodException("UI Toolkit Clickable.Invoke contract changed.");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static string Arg(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
