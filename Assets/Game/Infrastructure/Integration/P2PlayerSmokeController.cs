using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Presentation.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Command-line-only smoke for the real menu, New Game and Continue routes.</summary>
    public sealed class P2PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool russian_main_menu;
            public bool continue_initially_disabled;
            public bool settings_back_path;
            public bool new_game_loaded;
            public bool developer_ui_hidden;
            public bool unsaved_changes_detected;
            public bool unsaved_warning_shown;
            public bool save_created;
            public bool returned_to_menu;
            public bool continue_enabled_after_save;
            public bool continue_loaded_saved_session;
            public string main_menu_screenshot;
            public string gameplay_screenshot;
            public string error;
        }

        private MainMenuController initialMenu;
        private string outputDirectory;
        private long seed;

        public static P2PlayerSmokeController AttachIfRequested(MainMenuController menu)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p2Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var existing = FindFirstObjectByType<P2PlayerSmokeController>();
            if (existing != null) return existing;
            var output = ReadArgument(arguments, "-p2SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p2SmokeOutput is required.");
            if (!long.TryParse(ReadArgument(arguments, "-p2SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p2SmokeSeed must be a positive whole number.");

            var host = new GameObject("P2 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P2PlayerSmokeController>();
            controller.initialMenu = menu;
            controller.outputDirectory = output;
            controller.seed = requestedSeed;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult { seed = seed };
            StableEntityId savedSessionId = default;
            yield return null;
            try
            {
                result.russian_main_menu = HasText(initialMenu.RootVisual, RussianTextCatalog.Get("menu.continue")) &&
                    HasText(initialMenu.RootVisual, RussianTextCatalog.Get("menu.new_game")) &&
                    HasText(initialMenu.RootVisual, RussianTextCatalog.Get("menu.load")) &&
                    HasText(initialMenu.RootVisual, RussianTextCatalog.Get("menu.settings")) &&
                    HasText(initialMenu.RootVisual, RussianTextCatalog.Get("menu.exit"));
                result.continue_initially_disabled = !initialMenu.ContinueAvailable;

                initialMenu.OpenSettings();
                result.settings_back_path = initialMenu.CurrentView == MainMenuView.Settings;
                initialMenu.ShowMain();
                result.settings_back_path &= initialMenu.CurrentView == MainMenuView.Main;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForEndOfFrame();
            try
            {
                result.main_menu_screenshot = Path.Combine(outputDirectory, "p2-main-menu.png");
                Capture(result.main_menu_screenshot);
                initialMenu.OpenNewGame();
                initialMenu.StartNewGame(seed);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return WaitForScene("LocalGameplay", 20f);
            try
            {
                var root = FindFirstObjectByType<LocalSceneCompositionRoot>();
                if (root == null) throw new InvalidOperationException("Local gameplay composition did not load.");
                result.new_game_loaded = root.ActiveNewGameConfiguration.WorldSeed == seed;
                result.developer_ui_hidden = root.GameplayUi != null && !root.GameplayUi.DeveloperUiVisible;
                result.unsaved_changes_detected = root.PlayerSaves.HasUnsavedChanges;
                root.GameplayUi.RequestReturnToMainMenu();
                result.unsaved_warning_shown = root.GameplayUi.RootVisual.Q<Button>("confirm-main-menu") != null &&
                    root.GameplayUi.RootVisual.Q<Button>("cancel-main-menu") != null;
                root.GameplayUi.CancelModal();
                savedSessionId = root.Saves.SessionId;
                root.GameplayUi.SaveSlot(1);
                result.save_created = root.PlayerSaves.ListSlots().Single(x => x.Index == 1).Exists &&
                    !root.PlayerSaves.HasUnsavedChanges;
                root.GameplayUi.RequestReturnToMainMenu();
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return WaitForScene("MainMenu", 20f);
            try
            {
                var returnedMenu = FindFirstObjectByType<MainMenuController>();
                if (returnedMenu == null) throw new InvalidOperationException("Main menu did not load after leaving the game.");
                result.returned_to_menu = true;
                result.continue_enabled_after_save = returnedMenu.ContinueAvailable;
                returnedMenu.ContinueGame();
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return WaitForScene("LocalGameplay", 20f);
            try
            {
                var loadedRoot = FindFirstObjectByType<LocalSceneCompositionRoot>();
                if (loadedRoot == null) throw new InvalidOperationException("Continue did not load gameplay.");
                // The live clock can advance on the first rendered frame after restore, making the
                // world legitimately dirty again. The startup contract is the restored session identity.
                result.continue_loaded_saved_session = loadedRoot.Saves.SessionId == savedSessionId;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForEndOfFrame();
            try
            {
                result.gameplay_screenshot = Path.Combine(outputDirectory, "p2-loaded-game.png");
                Capture(result.gameplay_screenshot);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            Finish(result);
        }

        private void Finish(SmokeResult result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.russian_main_menu &&
                result.continue_initially_disabled && result.settings_back_path && result.new_game_loaded &&
                result.developer_ui_hidden && result.unsaved_changes_detected && result.save_created &&
                result.unsaved_warning_shown && result.returned_to_menu && result.continue_enabled_after_save &&
                result.continue_loaded_saved_session;
            var resultPath = Path.Combine(outputDirectory, "p2-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P2_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException("Timed out waiting for scene " + sceneName + ".");
                yield return null;
            }
            yield return null;
        }

        private static bool HasText(VisualElement root, string text) => root.Query<Label>().ToList()
            .Any(label => string.Equals(label.text, text, StringComparison.Ordinal)) || root.Query<Button>().ToList()
            .Any(button => string.Equals(button.text, text, StringComparison.Ordinal));

        private static void Capture(string path)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase)) return arguments[index + 1];
            return null;
        }
    }
}
