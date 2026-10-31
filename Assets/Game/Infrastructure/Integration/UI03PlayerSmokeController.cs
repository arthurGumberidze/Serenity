using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible exact-binary proof for the UI03 world HUD and StableEntityId selection surfaces.</summary>
    public sealed class UI03PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool resolution_1920x1080;
            public bool resolution_1280x720;
            public bool scale_125_percent;
            public bool single_selection;
            public bool multiple_selection;
            public bool stable_selection_after_command;
            public bool canonical_context_command;
            public bool resident_inspector;
            public bool building_inspector;
            public bool command_ribbon;
            public bool queue_projection;
            public bool attention_entry;
            public bool world_center_visible;
            public string screenshot_resident_1920x1080;
            public string screenshot_building_1920x1080;
            public string screenshot_multiple_1280x720_125;
            public string error;
        }

        private MainMenuController menu;
        private string output;

        public static void LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(value => string.Equals(value, "-ui03Smoke", StringComparison.OrdinalIgnoreCase))) return;
            var target = Arg(args, "-ui03SmokeOutput");
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("-ui03SmokeOutput is required.");
            var host = new GameObject("UI03 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<UI03PlayerSmokeController>();
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
                result.resolution_1280x720 && result.scale_125_percent && result.single_selection &&
                result.multiple_selection && result.stable_selection_after_command &&
                result.canonical_context_command && result.resident_inspector && result.building_inspector &&
                result.command_ribbon && result.queue_projection && result.attention_entry &&
                result.world_center_visible && File.Exists(result.screenshot_resident_1920x1080) &&
                File.Exists(result.screenshot_building_1920x1080) &&
                File.Exists(result.screenshot_multiple_1280x720_125);
            var report = Path.Combine(output, "ui03-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("UI03_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
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
            if (FindAnyObjectByType<WorldLoadingOverlay>() != null)
                throw new TimeoutException("World loading did not finish within 600 seconds.");

            var ui = root.GameplayUi;
            ui.SetPanel(GameplayPanel.None);
            ui.SetUiScale(1f);
            var presenter = FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude)
                .Where(value => value.IsBound && root.PlayerCharacterIds.Contains(value.CharacterId))
                .OrderBy(value => value.CharacterId).FirstOrDefault();
            if (presenter == null) throw new InvalidOperationException("A bound player resident is required.");
            var selectedId = presenter.CharacterId;
            root.SelectionProbe.SelectCharacterIds(new[] { selectedId });
            var position = presenter.transform.position;
            ui.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Ground,
                new WorldPosition(position.x + 2f, position.y, position.z + 2f)), Vector2.zero);
            var command = ui.ExecuteContextAction(ContextActionKind.Move, false);
            ui.RefreshNow();
            yield return null; yield return null;

            result.resolution_1920x1080 = Screen.width == 1920 && Screen.height == 1080;
            result.single_selection = root.SelectionProbe.SelectedCharacterIds.Count == 1;
            result.stable_selection_after_command = root.SelectionProbe.SelectedCharacterIds.Contains(selectedId);
            result.canonical_context_command = command.Orders.Count == 1 &&
                root.Work.GetQueue(selectedId).Any(order => order.Id == command.Orders[0].Id);
            result.command_ribbon = ui.RootVisual.Q("context-command-ribbon").resolvedStyle.display == DisplayStyle.Flex;
            result.queue_projection = ui.RootVisual.Q<Label>("selection-queue-label").text.Contains("Очередь") &&
                ui.RootVisual.Q("selection-inspector").Query<Label>().ToList()
                    .Any(label => label.ClassListContains("serenity-queue-order"));
            result.resident_inspector = ui.RootVisual.Q<Label>("inspector-kind")?.text == "Житель" &&
                ui.RootVisual.Q<Label>("inspector-title")?.text.Length > 0;
            result.attention_entry = ui.RootVisual.Q<Button>("attention-button") != null;
            result.world_center_visible = !ui.IsGuiPointBlocked(new Vector2(Screen.width * .5f, Screen.height * .5f));
            result.screenshot_resident_1920x1080 = Path.Combine(output, "ui03-resident-1920x1080.png");
            yield return Capture(result.screenshot_resident_1920x1080);

            var buildingPresenter = FindObjectsByType<BuildingPresenter>(FindObjectsInactive.Exclude)
                .FirstOrDefault(value => value.IsBound);
            if (buildingPresenter == null) throw new InvalidOperationException("A bound building presenter is required.");
            root.SelectionProbe.SelectBuilding(buildingPresenter);
            ui.RefreshNow();
            yield return null; yield return null;
            result.building_inspector = ui.RootVisual.Q<Label>("inspector-kind")?.text == "Здание" &&
                ui.RootVisual.Q<Label>("inspector-title")?.text.Length > 0;
            result.screenshot_building_1920x1080 = Path.Combine(output, "ui03-building-1920x1080.png");
            yield return Capture(result.screenshot_building_1920x1080);

            yield return SetResolution(1280, 720);
            ui.SetUiScale(1.25f);
            root.SelectionProbe.SelectCharacterIds(root.PlayerCharacterIds.Take(3));
            ui.RefreshNow();
            yield return null; yield return null;
            result.resolution_1280x720 = Screen.width == 1280 && Screen.height == 720;
            result.scale_125_percent = Mathf.Abs(ui.UiScale - 1.25f) < .01f;
            result.multiple_selection = root.SelectionProbe.SelectedCharacterIds.Count == 3 &&
                ui.RootVisual.Q<Label>("inspector-kind")?.text == "Группа выбора";
            result.world_center_visible &= !ui.IsGuiPointBlocked(new Vector2(Screen.width * .5f, Screen.height * .5f));
            result.screenshot_multiple_1280x720_125 = Path.Combine(output, "ui03-multiple-1280x720-125.png");
            yield return Capture(result.screenshot_multiple_1280x720_125);
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
