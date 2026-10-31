using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.UI;
using Game.Simulation.Work;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible exact-binary proof for UI04 management workspaces and canonical group projections.</summary>
    public sealed class UI04PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1920x1080;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool settlement_workspace;
            public bool people_virtualized_roster;
            public bool profession_groups;
            public bool construction_compact;
            public bool research_categories;
            public bool canonical_adult_assignment;
            public bool world_center_visible;
            public string screenshot_groups_1920x1080;
            public string screenshot_research_1920x1080;
            public string screenshot_construction_1280x720_125;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui04Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui04SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-ui04SmokeOutput is required.");
            var host = new GameObject("UI04 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI04PlayerSmokeController>();
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
                result.resolution_1280x720 && result.scale_125_percent && result.settlement_workspace &&
                result.people_virtualized_roster && result.profession_groups && result.construction_compact &&
                result.research_categories && result.canonical_adult_assignment && result.world_center_visible &&
                File.Exists(result.screenshot_groups_1920x1080) && File.Exists(result.screenshot_research_1920x1080) &&
                File.Exists(result.screenshot_construction_1280x720_125);
            var report = Path.Combine(output, "ui04-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI04_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
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
            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.canonical_adult_assignment = root.PlayerCharacterIds.All(id =>
            {
                var character = root.Characters.Get(id);
                return character.AgeInCompletedYears(root.Clock.State.BiologicalTicks) < 16 ||
                    root.Work.Groups.TryGetForMember(id, out var group) &&
                    group.Role == ProfessionGroupService.RoleForProfession(character.ProfessionId);
            });

            ui.Navigate(GameplayUiRouteId.Settlement);
            ui.RefreshNow(); yield return null;
            result.settlement_workspace = Labels(ui).Any(text => text == "Ресурсы и склады") &&
                Labels(ui).Any(text => text == "Производство") && Labels(ui).Any(text => text == "Рабочая сила");

            ui.Navigate(GameplayUiRouteId.People);
            ui.RefreshNow(); yield return null;
            result.people_virtualized_roster = ui.RootVisual.Q<ListView>("people-roster") != null;

            ui.Navigate(GameplayUiRouteId.WorkGroups);
            ui.RefreshNow(); yield return null; yield return null;
            result.profession_groups = root.Work.Groups.Count >= 11 &&
                ui.RootVisual.Q<Button>("work-create-profession-group") != null;
            result.screenshot_groups_1920x1080 = Path.Combine(output, "ui04-groups-1920x1080.png");
            yield return Capture(result.screenshot_groups_1920x1080);

            ui.Navigate(GameplayUiRouteId.Knowledge);
            ui.RefreshNow(); yield return null; yield return null;
            result.research_categories = ui.RootVisual.Q<Button>("research-category-survival") != null &&
                ui.RootVisual.Q<Button>("research-category-production") != null &&
                ui.RootVisual.Q<Button>("research-category-settlement") != null;
            result.screenshot_research_1920x1080 = Path.Combine(output, "ui04-research-1920x1080.png");
            yield return Capture(result.screenshot_research_1920x1080);

            yield return SetResolution(1280, 720);
            ui.SetUiScale(1.25f);
            ui.Navigate(GameplayUiRouteId.Construction);
            ui.RefreshNow(); yield return null; yield return null;
            result.resolution_1280x720 = Screen.width == 1280 && Screen.height == 720;
            result.scale_125_percent = Mathf.Abs(ui.UiScale - 1.25f) < .01f;
            result.construction_compact = ui.RootVisual.Q("main-panel").resolvedStyle.height <= 293f;
            result.world_center_visible = !ui.IsGuiPointBlocked(new Vector2(Screen.width * .5f, Screen.height * .72f));
            result.screenshot_construction_1280x720_125 = Path.Combine(output, "ui04-construction-1280x720-125.png");
            yield return Capture(result.screenshot_construction_1280x720_125);
        }

        private static string[] Labels(GameplayUiController ui) => ui.RootVisual.Q("main-panel")
            .Query<Label>().ToList().Select(label => label.text).ToArray();

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

        private static string Arg(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
