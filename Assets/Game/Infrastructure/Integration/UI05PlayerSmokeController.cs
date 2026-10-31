using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Combat;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible exact-binary proof for UI05 people, risk, dynasty, attention and military workspaces.</summary>
    public sealed class UI05PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1920x1080;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool people_home_family_health;
            public bool dynasty_succession;
            public bool attention_decisions_journal;
            public bool military_rosters;
            public bool military_supply_training_orders;
            public bool combat_status;
            public bool stable_selection;
            public string screenshot_people_1920x1080;
            public string screenshot_dynasty_1920x1080;
            public string screenshot_attention_1920x1080;
            public string screenshot_military_1280x720_125;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui05Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui05SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-ui05SmokeOutput is required.");
            var host = new GameObject("UI05 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI05PlayerSmokeController>();
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
            result.passed = string.IsNullOrEmpty(result.error) && result.resolution_1920x1080 &&
                result.resolution_1280x720 && result.scale_125_percent && result.people_home_family_health &&
                result.dynasty_succession && result.attention_decisions_journal && result.military_rosters &&
                result.military_supply_training_orders && result.combat_status && result.stable_selection &&
                File.Exists(result.screenshot_people_1920x1080) && File.Exists(result.screenshot_dynasty_1920x1080) &&
                File.Exists(result.screenshot_attention_1920x1080) && File.Exists(result.screenshot_military_1280x720_125);
            var report = Path.Combine(output, "ui05-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI05_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
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
            var monarchId = root.Dynasty.CurrentMonarchId.Value;
            root.SelectionProbe.SelectCharacterIds(new[] { monarchId });
            ui.SetUiScale(1f);
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;

            ui.Navigate(GameplayUiRouteId.People);
            ui.RefreshNow(); yield return null; yield return null;
            result.people_home_family_health = ui.RootVisual.Q<ListView>("people-roster") != null &&
                ui.RootVisual.Q("people-home-card") != null && ui.RootVisual.Q("people-family-card") != null &&
                Labels(ui).Any(text => text == "Состояние");
            result.stable_selection = root.SelectionProbe.SelectedCharacterId == monarchId;
            result.screenshot_people_1920x1080 = Path.Combine(output, "ui05-people-1920x1080.png");
            yield return Capture(result.screenshot_people_1920x1080);

            ui.Navigate(GameplayUiRouteId.Dynasty);
            ui.RefreshNow(); yield return null; yield return null;
            result.dynasty_succession = ui.RootVisual.Q("dynasty-family-tree") != null &&
                ui.RootVisual.Q("dynasty-succession-line") != null && ui.RootVisual.Q("dynasty-summary") != null;
            result.screenshot_dynasty_1920x1080 = Path.Combine(output, "ui05-dynasty-1920x1080.png");
            yield return Capture(result.screenshot_dynasty_1920x1080);

            ui.Navigate(GameplayUiRouteId.Attention);
            if (root.Events.PendingPlayerEvents.Count == 0)
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromMinutes(48));
            ui.RefreshNow(); yield return null; yield return null;
            result.attention_decisions_journal = ui.RootVisual.Q<Button>("attention-decisions") != null &&
                ui.RootVisual.Q<Button>("attention-journal") != null &&
                ui.RootVisual.Q("attention-decision-card") != null;
            result.screenshot_attention_1920x1080 = Path.Combine(output, "ui05-attention-1920x1080.png");
            yield return Capture(result.screenshot_attention_1920x1080);

            root.StartDemoUnitBattle(BattleMode.Abstract);
            yield return SetResolution(1280, 720);
            ui.SetUiScale(1.25f);
            ui.Navigate(GameplayUiRouteId.Military);
            ui.RefreshNow(); yield return null; yield return null;
            result.resolution_1280x720 = Screen.width == 1280 && Screen.height == 720;
            result.scale_125_percent = Mathf.Abs(ui.UiScale - 1.25f) < .01f;
            result.military_rosters = ui.RootVisual.Q<ListView>("military-unit-roster") != null &&
                ui.RootVisual.Q<ListView>("military-member-roster") != null;
            result.military_supply_training_orders = ui.RootVisual.Q("military-readiness-summary") != null &&
                ui.RootVisual.Q<Button>("military-load-supply") != null &&
                ui.RootVisual.Q<Button>("military-engage-disabled") != null;
            result.combat_status = ui.RootVisual.Q("military-combat-status") != null;
            result.screenshot_military_1280x720_125 = Path.Combine(output, "ui05-military-1280x720-125.png");
            yield return Capture(result.screenshot_military_1280x720_125);
        }

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
            for (var frame = 0; frame < 8; frame++) yield return null;
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
            if (samples == 0 || brightness / (samples * 3d) < 4d)
                throw new InvalidOperationException("Screenshot is effectively black: " + path);
        }

        private static string[] Labels(GameplayUiController ui) => ui.RootVisual.Q("main-panel")
            .Query<Label>().ToList().Select(label => label.text).ToArray();

        private static string Arg(string[] args, string key)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
