using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible 1920x1080 D3D11 proof for the normal Pause -> Codes P10.8 player route.</summary>
    public sealed class P108PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool menu_new_game;
            public bool pause_codes_route;
            public bool unknown_rejected_without_mark;
            public bool all_technology_unlocked;
            public bool exact_resources_and_repeat;
            public bool all_ranks_and_repeat_unique;
            public bool family_canonical;
            public bool fade_skip_visible;
            public bool achievements_irreversible;
            public bool save_load_v25;
            public bool save_ui_warning;
            public bool d3d11;
            public int width;
            public int height;
            public int resource_definition_count;
            public int rank_definition_count;
            public float average_fps;
            public float minimum_fps;
            public string codes_screenshot;
            public string family_screenshot;
            public string save_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private int requestedWidth;
        private int requestedHeight;

        public static P108PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p108Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p108SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p108SmokeOutput is required.");
            var host = new GameObject("P10.8 Diagnostics Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P108PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            int.TryParse(ReadArgument(args, "-p108SmokeWidth"), out controller.requestedWidth);
            int.TryParse(ReadArgument(args, "-p108SmokeHeight"), out controller.requestedHeight);
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new Result();
            yield return EnsureResolution(result);
            result.d3d11 = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11;
            Try(result, () =>
            {
                menu.OpenNewGame();
                result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
                menu.StartNewGame(new NewGameConfiguration(10818, rulerName: "Мира", dynastyName: "Дом Тихой Реки"));
            });
            var deadline = Time.realtimeSinceStartup + 45f;
            while (SceneManager.GetActiveScene().name != "LocalGameplay" && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null) { result.error = "LocalGameplay did not initialize."; Finish(result); yield break; }

            Dictionary<ResourceId, long> before = null;
            var trainingBefore = 0;
            Try(result, () =>
            {
                root.GameplayUi.SetOnboardingVisible(false);
                root.AiRuntime.SetPaused(true);
                root.GameplayUi.RefreshNow();
                root.GameplayUi.SetPanel(GameplayPanel.Codes);
                root.GameplayUi.RefreshNow();
                result.pause_codes_route = root.GameplayUi.ActivePanel == GameplayPanel.Codes &&
                    root.GameplayUi.RootVisual.Q<TextField>("codes-input") != null;
                Submit("bad_code");
                result.unknown_rejected_without_mark = !root.Diagnostics.CheatsUsed;
                before = root.Resources.All.ToDictionary(x => x.Id, x => Total(x.Id));
                result.resource_definition_count = before.Count;
                trainingBefore = root.MilitaryTraining.Records.Count;
            });
            yield return new WaitForEndOfFrame();
            result.codes_screenshot = Capture("p108-codes-panel.png");

            Try(result, () =>
            {
                Submit("serenity");
                result.all_technology_unlocked = root.Research.CompletedTechnologies.Count == root.Technologies.All.Count;
                Submit("res_serenity"); Submit("res_serenity");
                result.exact_resources_and_repeat = root.Resources.All.All(x => Total(x.Id) == before[x.Id] + 20000);
                Submit("army_serenity"); Submit("army_serenity");
                var records = root.MilitaryTraining.Records.Skip(trainingBefore).ToArray();
                result.rank_definition_count = root.MilitaryTraining.Catalog.All.Count;
                result.all_ranks_and_repeat_unique = records.Length == result.rank_definition_count * 2 &&
                    records.Select(x => x.CharacterId).Distinct().Count() == records.Length &&
                    root.MilitaryTraining.Catalog.All.All(level => records.Count(x => x.Rank == level.Rank) == 2) &&
                    records.All(x => root.Appearances.Get(x.CharacterId).ClothingId ==
                                     StoneAgeAppearanceProfiles.MilitaryClothing);
                Invoke(root.GameplayUi.RootVisual.Q<Button>("family-diagnostic"));
                var family = root.GameplayUi.LastDiagnosticResult;
                result.family_canonical = family != null && family.Succeeded && family.PregnancyId.HasValue &&
                    root.HealthFamily.Pregnancies.Any(x => x.PregnancyId == family.PregnancyId.Value) &&
                    root.Domestic.Beds.Any(x => x.Id == family.BedId.Value) &&
                    root.Characters.Get(family.FirstCharacterId.Value).SpouseId == family.SecondCharacterId;
                result.fade_skip_visible = root.GameplayUi.RootVisual.Q<Button>("family-diagnostic-skip") != null;
            });
            yield return new WaitForEndOfFrame();
            result.family_screenshot = Capture("p108-family-fade.png");
            Try(result, () => Invoke(root.GameplayUi.RootVisual.Q<Button>("family-diagnostic-skip")));

            Try(result, () =>
            {
                root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
                root.GameplayUi.RefreshNow();
                var card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-1");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Сохранить"));
                var slot = root.PlayerSaves.ListSlots().Single(x => x.Index == 1);
                result.save_ui_warning = slot.CheatsUsed && slot.Detail.Contains("достижения отключены");
                root.GameplayUi.RefreshNow();
            });
            yield return new WaitForEndOfFrame();
            result.save_screenshot = Capture("p108-save-warning.png");

            Try(result, () =>
            {
                var card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-1");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Загрузить"));
                Invoke(root.GameplayUi.RootVisual.Q<VisualElement>("confirmation-modal").Query<Button>().ToList()
                    .First(x => x.text == "Загрузить"));
                var snapshot = root.Saves.Capture();
                result.save_load_v25 = snapshot.FormatVersion == Game.Domain.SaveSnapshot.DiagnosticVersion &&
                    snapshot.Diagnostics != null && snapshot.Diagnostics.CheatsUsed;
                result.achievements_irreversible = root.Diagnostics.CheatsUsed && !root.Diagnostics.AchievementsEnabled;
                root.GameplayUi.SetPanel(GameplayPanel.Codes);
                root.GameplayUi.RefreshNow();
            });
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p108-after-load.png");

            var frameTimes = new List<float>();
            for (var i = 0; i < 120; i++)
            {
                yield return new WaitForEndOfFrame();
                if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime);
            }
            result.average_fps = frameTimes.Count == 0 ? 0f : 1f / frameTimes.Average();
            result.minimum_fps = frameTimes.Count == 0 ? 0f : 1f / frameTimes.Max();
            Finish(result);
        }

        private void Submit(string value)
        {
            var input = root.GameplayUi.RootVisual.Q<TextField>("codes-input");
            input.value = value;
            Invoke(root.GameplayUi.RootVisual.Q<Button>("codes-apply"));
        }

        private long Total(ResourceId id) => root.Inventories.All.Sum(x => x.GetAmount(id).Units);

        private IEnumerator EnsureResolution(Result result)
        {
            Screen.SetResolution(requestedWidth, requestedHeight, FullScreenMode.Windowed);
            var deadline = Time.realtimeSinceStartup + 8f;
            while ((Screen.width != requestedWidth || Screen.height != requestedHeight) && Time.realtimeSinceStartup < deadline)
                yield return null;
            result.width = Screen.width; result.height = Screen.height;
            if (result.width != requestedWidth || result.height != requestedHeight)
                result.error = "Unexpected resolution: " + result.width + "x" + result.height;
        }

        private string Capture(string name)
        {
            var path = Path.Combine(outputDirectory, name);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture); return path;
        }

        private void Finish(Result result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.pause_codes_route &&
                result.unknown_rejected_without_mark && result.all_technology_unlocked &&
                result.exact_resources_and_repeat && result.all_ranks_and_repeat_unique && result.family_canonical &&
                result.fade_skip_visible && result.achievements_irreversible && result.save_load_v25 &&
                result.save_ui_warning && result.d3d11 && result.width == requestedWidth &&
                result.height == requestedHeight && result.average_fps > 0f && File.Exists(result.codes_screenshot) &&
                File.Exists(result.family_screenshot) && File.Exists(result.save_screenshot) &&
                File.Exists(result.after_load_screenshot);
            var report = Path.Combine(outputDirectory, "p108-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P108_PLAYER_SMOKE " + (result.passed ? "PASS " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static void Invoke(Button button)
        {
            if (button == null) throw new InvalidOperationException("Expected UI button is missing.");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            if (invoke == null) throw new MissingMethodException("Clickable.Invoke");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private static void Try(Result result, Action action)
        {
            try { action(); }
            catch (Exception exception) { if (string.IsNullOrEmpty(result.error)) result.error = exception.ToString(); }
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
