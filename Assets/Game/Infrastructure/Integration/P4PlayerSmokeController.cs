using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player smoke for the P4 editor-to-world and Save/Load route.</summary>
    public sealed class P4PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool editor_opened;
            public bool live_preview_rendered;
            public bool identity_confirmed;
            public bool appearance_projected;
            public bool save_load_preserved;
            public string ruler_name;
            public string dynasty_name;
            public string profile_id;
            public string body_id;
            public string face_id;
            public string hair_id;
            public string clothing_id;
            public string editor_screenshot;
            public string world_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private string outputDirectory;
        private long seed;

        public static P4PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p4Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            if (!long.TryParse(ReadArgument(arguments, "-p4SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p4SmokeSeed must be a positive whole number.");
            var output = ReadArgument(arguments, "-p4SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p4SmokeOutput is required.");
            var host = new GameObject("P4 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P4PlayerSmokeController>();
            controller.menu = source;
            controller.outputDirectory = output;
            controller.seed = requestedSeed;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult { seed = seed };
            menu.OpenNewGame();
            yield return null;
            try
            {
                var seedField = menu.RootVisual.Q<TextField>("world-seed");
                var rulerField = menu.RootVisual.Q<TextField>("ruler-name");
                var dynastyField = menu.RootVisual.Q<TextField>("dynasty-name");
                var profileField = menu.RootVisual.Q<DropdownField>("appearance-profile");
                seedField.value = seed.ToString();
                rulerField.value = "Эльда";
                dynastyField.value = "Дом Каменного Берега";
                profileField.value = "Взрослая B — женщина";
                menu.RotatePreview(28f);
                menu.SetPreviewZoom(3.7f);
                result.editor_opened = menu.CurrentView == MainMenuView.NewGame &&
                    menu.RootVisual.Q<Button>("start-new-game-button") != null;
                result.live_preview_rendered = menu.HasLiveRulerPreview;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            result.editor_screenshot = Path.Combine(outputDirectory, "p4-ruler-editor.png");
            Capture(result.editor_screenshot);
            if (!menu.TryStartEditedNewGame())
            {
                result.error = "P4 ruler editor confirmation was rejected.";
                Finish(result);
                yield break;
            }
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null;
            yield return null;

            LocalSceneCompositionRoot root = null;
            Character ruler = null;
            try
            {
                root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
                if (root == null) throw new InvalidOperationException("LocalGameplay composition root is missing.");
                ruler = root.MaleDemoCharacter;
                var appearance = root.Appearances.Get(ruler.Id);
                result.ruler_name = ruler.Name.GivenName;
                result.dynasty_name = root.Dynasty.Name;
                result.profile_id = appearance.ProfileId.Value;
                result.body_id = appearance.BodyId.Value;
                result.face_id = appearance.FaceId.Value;
                result.hair_id = appearance.HairId.Value;
                result.clothing_id = appearance.ClothingId.Value;
                result.identity_confirmed = ruler.Name.GivenName == "Эльда" && ruler.Sex == CharacterSex.Female &&
                    root.Dynasty.Name == "Дом Каменного Берега";
                result.appearance_projected = appearance.ProfileId == StoneAgeAppearanceProfiles.AdultB &&
                    root.CharacterPresentations.TryGet(ruler.Id, out var presenter) &&
                    presenter.Appearance.ProfileId == appearance.ProfileId;

                root.GameplayUi.SaveSlot(1);
                ruler.Rename(new CharacterName("Временно"));
                root.GameplayUi.ConfirmLoadSlot(1);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return null;
            try
            {
                var restored = root.Appearances.Get(ruler.Id);
                CharacterPresenter restoredPresenter;
                var hasRestoredPresenter = root.CharacterPresentations.TryGet(ruler.Id, out restoredPresenter);
                result.save_load_preserved = ruler.Name.GivenName == "Эльда" &&
                    root.Dynasty.Name == "Дом Каменного Берега" &&
                    restored.ProfileId == StoneAgeAppearanceProfiles.AdultB &&
                    hasRestoredPresenter &&
                    restoredPresenter.Appearance.ProfileId == restored.ProfileId;
                root.Clock.Pause();
                if (restoredPresenter != null)
                {
                    root.CameraController.transform.position = restoredPresenter.transform.position;
                    root.CameraController.transform.rotation = Quaternion.identity;
                    root.CameraController.enabled = false;
                }
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            result.world_screenshot = Path.Combine(outputDirectory, "p4-world-after-load.png");
            Capture(result.world_screenshot);
            Finish(result);
        }

        private void Finish(SmokeResult result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.editor_opened &&
                result.live_preview_rendered && result.identity_confirmed && result.appearance_projected &&
                result.save_load_preserved && File.Exists(result.editor_screenshot ?? string.Empty) &&
                File.Exists(result.world_screenshot ?? string.Empty);
            var path = Path.Combine(outputDirectory, "p4-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P4_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
        }

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
            for (var i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
    }
}
