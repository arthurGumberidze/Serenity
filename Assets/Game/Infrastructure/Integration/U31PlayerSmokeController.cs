using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>
    /// Command-line acceptance adapter for a normal Windows Player composition. It drives the
    /// public U27 UI commands and records evidence; it owns no gameplay state.
    /// </summary>
    public sealed class U31PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public int player_character_count;
            public int ai_character_count;
            public int composition_root_count;
            public int gameplay_ui_count;
            public int ui_subscription_count;
            public bool scene_loaded;
            public bool ui_available;
            public bool new_game_confirmed;
            public bool save_created;
            public bool load_completed;
            public bool rival_hidden_before_discovery;
            public string screenshot_path;
            public string error;
        }

        private static bool newGameRequested;
        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;

        public static U31PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-u31Smoke", StringComparison.OrdinalIgnoreCase)))
                return null;
            var controller = compositionRoot.gameObject.AddComponent<U31PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = ReadArgument(arguments, "-u31SmokeOutput");
            if (string.IsNullOrWhiteSpace(controller.outputDirectory))
                throw new InvalidOperationException("-u31SmokeOutput is required.");
            var seedText = ReadArgument(arguments, "-u31SmokeSeed");
            if (!long.TryParse(seedText, out controller.seed))
                throw new InvalidOperationException("-u31SmokeSeed must be a whole number.");
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            yield return null;
            Directory.CreateDirectory(outputDirectory);
            if (!newGameRequested)
            {
                newGameRequested = true;
                root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
                root.GameplayUi.RefreshNow();
                root.GameplayUi.RequestNewGame(seed);
                root.GameplayUi.ConfirmNewGame(seed);
                yield break;
            }

            var result = new SmokeResult { seed = seed };
            try
            {
                result.scene_loaded = root.gameObject.scene.name == "LocalGameplay";
                result.ui_available = root.GameplayUi != null &&
                    root.GameplayUi.RootVisual.Q("top-hud") != null &&
                    root.GameplayUi.RootVisual.Q("main-panel") != null;
                result.new_game_confirmed = root.ActiveNewGameConfiguration.WorldSeed == seed;
                result.player_character_count = root.PlayerCharacterIds.Count;
                result.ai_character_count = root.StarterEnemyAi.Residents.Count;
                result.composition_root_count = FindObjectsByType<LocalSceneCompositionRoot>().Length;
                result.gameplay_ui_count = FindObjectsByType<GameplayUiController>(FindObjectsInactive.Include).Length;
                result.ui_subscription_count = root.GameplayUi.SubscriptionCount;
                result.rival_hidden_before_discovery = !root.CampaignProgress.State.RivalDiscovered &&
                    !root.CampaignProgress.CanRevealSettlement(root.CampaignProgress.State.RivalHomeSettlementId);

                root.GameplayUi.SetPanel(GameplayPanel.SaveLoad);
                root.GameplayUi.SaveSlot(1);
                result.save_created = root.PlayerSaves.ListSlots().Single(x => x.Index == 1).Exists;
                root.GameplayUi.RequestLoadSlot(1);
                root.GameplayUi.ConfirmLoadSlot(1);
                result.load_completed = root.ActiveNewGameConfiguration.WorldSeed == seed &&
                    root.GameplayUi.SubscriptionCount == 4;

            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    result.screenshot_path = Path.Combine(outputDirectory, "u31-player-smoke.png");
                    var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(result.screenshot_path, texture.EncodeToPNG());
                    Destroy(texture);
                }
                catch (Exception exception)
                {
                    result.error = exception.ToString();
                }
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.scene_loaded && result.ui_available &&
                result.new_game_confirmed && result.player_character_count == 10 && result.ai_character_count == 6 &&
                result.composition_root_count == 1 && result.gameplay_ui_count == 1 &&
                result.ui_subscription_count == 4 && result.rival_hidden_before_discovery &&
                result.save_created && result.load_completed;

            var resultPath = Path.Combine(outputDirectory, "u31-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("U31_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
            yield return null;
            Application.Quit(result.passed ? 0 : 2);
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (var i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
    }
}
