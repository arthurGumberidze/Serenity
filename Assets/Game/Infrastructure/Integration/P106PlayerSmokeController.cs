using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Domestic;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible normal-menu/UI proof for P10.6 domestic, consent, fade and save continuity.</summary>
    public sealed class P106PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool menu_new_game;
            public bool domestic_ui;
            public bool assigned_bed;
            public bool sleep_started;
            public bool outdoor_bed_denied;
            public bool one_sided_consent_denied;
            public bool mutual_consent;
            public bool implicit_fade_and_skip;
            public bool pregnancy_created;
            public bool save_load_continuity;
            public bool format_v23;
            public bool generation_id;
            public int width;
            public int height;
            public string domestic_screenshot;
            public string fade_screenshot;
            public string after_load_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private int requestedWidth;
        private int requestedHeight;

        public static P106PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p106Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p106SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p106SmokeOutput is required.");
            var host = new GameObject("P10.6 Domestic Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P106PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            int.TryParse(ReadArgument(args, "-p106SmokeWidth"), out controller.requestedWidth);
            int.TryParse(ReadArgument(args, "-p106SmokeHeight"), out controller.requestedHeight);
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new Result();
            yield return EnsureResolution(result);
            Try(result, () =>
            {
                menu.OpenNewGame();
                result.menu_new_game = menu.CurrentView == MainMenuView.NewGame;
                menu.StartNewGame(new NewGameConfiguration(10618, rulerName: "Арен",
                    dynastyName: "Дом Тихой Реки"));
            });
            var deadline = Time.realtimeSinceStartup + 45f;
            while (SceneManager.GetActiveScene().name != "LocalGameplay" && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            root = FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root == null) { result.error = "LocalGameplay did not initialize."; Finish(result); yield break; }

            var first = root.MaleDemoCharacter.Id;
            var second = root.FemaleDemoCharacter.Id;
            Try(result, () =>
            {
                root.GameplayUi.SetOnboardingVisible(false);
                Invoke(root.GameplayUi.RootVisual.Q<Button>("nav-domestic"));
                result.domestic_ui = root.GameplayUi.ActivePanel == GameplayPanel.Domestic &&
                    root.GameplayUi.RootVisual.Q<Button>("domestic-sleep") != null;
                result.assigned_bed = root.Domestic.EvaluateSleep(first).Allowed;
                Invoke(root.GameplayUi.RootVisual.Q<Button>("domestic-sleep"));
                result.sleep_started = root.AiAgents.TryGet(first, out var agent) &&
                    agent.CurrentAction == UtilityActionKind.Rest;
                var outdoor = new BedState(DeterministicStableId.Create(10618, "outside_bed"),
                    root.DemoShelterBuilding.Id, new GridCoordinate(100, 100), new WorldPosition(100, 0, 100));
                root.Domestic.RegisterBed(outdoor);
                result.outdoor_bed_denied = root.Domestic.EvaluateSleepAtBed(second, outdoor.Id).Reason ==
                    SleepDenialReason.BedOutsideHome;
            });
            yield return new WaitForEndOfFrame();
            result.domestic_screenshot = Capture("p106-domestic-sleep.png");

            Try(result, () =>
            {
                Invoke(root.GameplayUi.RootVisual.Q<Button>("domestic-consent-first"));
                result.one_sided_consent_denied = root.Domestic.EvaluateFamilyInteraction(first, second,
                    root.Clock.State.BiologicalTicks).Reason == FamilyInteractionDenialReason.ConsentMissing;
                Invoke(root.GameplayUi.RootVisual.Q<Button>("domestic-consent-second"));
                result.mutual_consent = root.Domestic.EvaluateFamilyInteraction(first, second,
                    root.Clock.State.BiologicalTicks).Allowed;
                Invoke(root.GameplayUi.RootVisual.Q<Button>("domestic-family-event"));
                result.implicit_fade_and_skip = root.GameplayUi.RootVisual.Q<Button>("domestic-fade-skip") != null;
                result.pregnancy_created = root.HealthFamily.Pregnancies.Any(x =>
                    x.Status == Game.Domain.Health.PregnancyStatus.Active);
            });
            yield return new WaitForEndOfFrame();
            result.fade_screenshot = Capture("p106-family-fade.png");
            Try(result, () => Invoke(root.GameplayUi.RootVisual.Q<Button>("domestic-fade-skip")));

            Try(result, () =>
            {
                Invoke(root.GameplayUi.RootVisual.Q<Button>("nav-saveload"));
                var card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-3");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Сохранить"));
                root.Domestic.SetConsent(first, second, false);
                card = root.GameplayUi.RootVisual.Q<VisualElement>("save-slot-3");
                Invoke(card.Query<Button>().ToList().First(x => x.text == "Загрузить"));
                var modal = root.GameplayUi.RootVisual.Q<VisualElement>("confirmation-modal");
                Invoke(modal.Query<Button>().ToList().First(x => x.text == "Загрузить"));
            });
            yield return null;
            Try(result, () =>
            {
                var snapshot = root.Saves.Capture();
                result.format_v23 = snapshot.FormatVersion >= SaveSnapshot.DomesticVersion;
                result.generation_id = snapshot.ConnectedWorld.GenerationId == "p10-connected-world-v1";
                result.save_load_continuity = snapshot.Domestic.Consents.Single().IsMutual &&
                    snapshot.HealthFamily.Pregnancies.Any(x => x.Status == Game.Domain.Health.PregnancyStatus.Active) &&
                    snapshot.CharacterRuntime.Entries.Any(x => x.State.CharacterId == first) &&
                    snapshot.Appearance != null && snapshot.Work != null;
                root.GameplayUi.SetPanel(GameplayPanel.Domestic);
            });
            yield return new WaitForEndOfFrame();
            result.after_load_screenshot = Capture("p106-after-load.png");
            Finish(result);
        }

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
            result.passed = string.IsNullOrEmpty(result.error) && result.menu_new_game && result.domestic_ui &&
                result.assigned_bed && result.sleep_started && result.outdoor_bed_denied &&
                result.one_sided_consent_denied && result.mutual_consent && result.implicit_fade_and_skip &&
                result.pregnancy_created && result.save_load_continuity && result.format_v23 && result.generation_id &&
                result.width == requestedWidth && result.height == requestedHeight &&
                File.Exists(result.domestic_screenshot) && File.Exists(result.fade_screenshot) &&
                File.Exists(result.after_load_screenshot);
            var report = Path.Combine(outputDirectory, "p106-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P106_PLAYER_SMOKE " + (result.passed ? "PASS " : "FAILED ") + report);
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
        { try { action(); } catch (Exception exception) { result.error = exception.ToString(); } }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
