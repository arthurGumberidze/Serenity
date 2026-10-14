using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Characters;
using Game.Presentation.Characters;
using Game.Simulation.Tiers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Player proof for P8.4 editor, UMA generation, Tier rematerialization, and Save/Load.</summary>
    public sealed class P84PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public bool passed;
            public bool editor_controls;
            public bool preview_uma_generated;
            public bool world_state_equal;
            public bool tier_state_equal;
            public bool save_load_equal;
            public int preview_generation_count;
            public long preview_generation_ms;
            public string profile_id;
            public string editor_screenshot;
            public string world_screenshot;
            public string error;
        }

        private MainMenuController menu;
        private string output;
        private long seed;

        public static P84PlayerSmokeController LaunchFromMenuIfRequested(MainMenuController source)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p84Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            if (!long.TryParse(Arg(args, "-p84SmokeSeed"), out var requested) || requested <= 0)
                throw new InvalidOperationException("-p84SmokeSeed must be positive.");
            var target = Arg(args, "-p84SmokeOutput");
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("-p84SmokeOutput is required.");
            var host = new GameObject("P8.4 Player Smoke");
            DontDestroyOnLoad(host);
            var controller = host.AddComponent<P84PlayerSmokeController>();
            controller.menu = source; controller.output = target; controller.seed = requested;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(output);
            var result = new Result();
            menu.OpenNewGame();
            yield return null;
            try
            {
                menu.RootVisual.Q<TextField>("world-seed").value = seed.ToString();
                menu.RootVisual.Q<TextField>("ruler-name").value = "Мира";
                menu.RootVisual.Q<TextField>("dynasty-name").value = "Дом Ясного Огня";
                menu.RootVisual.Q<DropdownField>("appearance-profile").value = "Человек — женщина";
                menu.SetAppearanceOption("skin", "skin_deep");
                menu.SetAppearanceOption("hair", "hair_messy");
                menu.SetAppearanceOption("hair_color", "hair_auburn");
                menu.SetAppearanceOption("eye_color", "eye_green");
                menu.SetAppearanceOption("age", "age_aged");
                menu.SetAppearanceParameter(StoneAgeAppearanceProfiles.Nose, 810);
                menu.SetAppearanceParameter(StoneAgeAppearanceProfiles.Height, 690);
                result.editor_controls = new[] { "appearance-skin", "appearance-hair", "appearance-eye-color",
                    "appearance-age", "appearance-nose_shape", "appearance-height" }
                    .All(name => menu.RootVisual.Q(name) != null);
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            var previewDeadline = Time.realtimeSinceStartup + 15f;
            UmaAppearanceProjection preview = null;
            while (Time.realtimeSinceStartup < previewDeadline)
            {
                preview = FindObjectsByType<UmaAppearanceProjection>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None).FirstOrDefault();
                if (preview != null && preview.GenerationCount > 0) break;
                yield return null;
            }
            result.preview_uma_generated = preview != null && preview.UsesUma;
            result.preview_generation_count = preview?.GenerationCount ?? 0;
            result.preview_generation_ms = preview?.LastGenerationMilliseconds ?? -1;
            yield return new WaitForEndOfFrame();
            result.editor_screenshot = Path.Combine(output, "p84-editor.png");
            Capture(result.editor_screenshot);
            if (!menu.TryStartEditedNewGame())
            {
                result.error = "Editor confirmation failed."; Finish(result); yield break;
            }
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null; yield return null;
            try
            {
                var root = FindAnyObjectByType<LocalSceneCompositionRoot>();
                var ruler = root.MaleDemoCharacter;
                var state = root.Appearances.Get(ruler.Id);
                result.profile_id = state.ProfileId.Value;
                result.world_state_equal = Matches(state) && root.CharacterPresentations.TryGet(ruler.Id, out var presenter) &&
                    Matches(presenter.Appearance);
                root.Tiers.Transition(ruler.Id, CharacterSimulationTier.Tier2);
                root.Tiers.Transition(ruler.Id, CharacterSimulationTier.Tier1);
                result.tier_state_equal = root.CharacterPresentations.TryGet(ruler.Id, out var rematerialized) &&
                    Matches(rematerialized.Appearance);
                root.GameplayUi.SaveSlot(1);
                root.Appearances.Set(StoneAgeAppearanceProfiles.CreateCatalog().DefaultFor(ruler.Sex).CreateState(ruler.Id));
                root.GameplayUi.ConfirmLoadSlot(1);
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }
            yield return null;
            var gameplay = FindAnyObjectByType<LocalSceneCompositionRoot>();
            result.save_load_equal = Matches(gameplay.Appearances.Get(gameplay.MaleDemoCharacter.Id));
            gameplay.Clock.Pause();
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            result.world_screenshot = Path.Combine(output, "p84-world.png");
            Capture(result.world_screenshot);
            Finish(result);
        }

        private void Finish(Result result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.editor_controls &&
                result.preview_uma_generated && result.world_state_equal && result.tier_state_equal &&
                result.save_load_equal && File.Exists(result.editor_screenshot ?? string.Empty) &&
                File.Exists(result.world_screenshot ?? string.Empty);
            var report = Path.Combine(output, "p84-player-smoke.json");
            File.WriteAllText(report, JsonUtility.ToJson(result, true));
            Debug.Log("P84_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + report);
            Application.Quit(result.passed ? 0 : 2);
        }

        private static bool Matches(AppearanceState state) => state.ProfileId == StoneAgeAppearanceProfiles.UmaFemale &&
            state.SkinId.Value == "skin_deep" && state.HairId.Value == "hair_messy" &&
            state.HairColorId.Value == "hair_auburn" && state.EyeColorId.Value == "eye_green" &&
            state.AgeId.Value == "age_aged" && state.Parameter(StoneAgeAppearanceProfiles.Nose) == 810 &&
            state.Parameter(StoneAgeAppearanceProfiles.Height) == 690;

        private static void Capture(string path)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture);
        }
        private static string Arg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
