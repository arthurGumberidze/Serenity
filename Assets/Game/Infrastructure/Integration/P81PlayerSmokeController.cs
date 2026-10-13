using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Windows Player proof for the P8.1 visual benchmark.</summary>
    public sealed class P81PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool benchmark_present;
            public bool natural_materials_present;
            public bool brown_trunks_present;
            public bool stable_appearance_present;
            public bool character_visible;
            public string character_provider;
            public string stable_character_id;
            public string screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p81Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(81001);
        }

        public static P81PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p81Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p81SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p81SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P81PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            P81VisualPrototypePresentation prototype = null;
            try
            {
                root.Clock.Pause();
                prototype = FindAnyObjectByType<P81VisualPrototypePresentation>();
                if (prototype == null) throw new InvalidOperationException("P8.1 visual prototype is missing.");
                prototype.ValidateConfiguration();
                result.benchmark_present = prototype != null;
                result.stable_character_id = prototype.CanonicalAppearance.CharacterId.ToString();
                result.stable_appearance_present = prototype.CanonicalAppearance.ProfileId.Value == "uma_human_male_30";
                result.natural_materials_present = prototype.EnvironmentRenderers.Count >= 8 &&
                    prototype.EnvironmentRenderers.All(x => x.sharedMaterial != null &&
                        x.sharedMaterial.shader != null && x.sharedMaterial.shader.name.Contains("Lit"));
                result.brown_trunks_present = prototype.TrunkRenderers.Count >= 3 &&
                    prototype.TrunkRenderers.All(x => x.sharedMaterial != null &&
                        x.sharedMaterial.color.r > x.sharedMaterial.color.b);
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                root.GameplayUi.RootVisual.style.display = UnityEngine.UIElements.DisplayStyle.None;
                root.CameraController.enabled = false;
                root.CameraController.transform.SetPositionAndRotation(
                    prototype.BenchmarkCenter.position + new Vector3(0f, 0f, -1f), Quaternion.Euler(0f, 24f, 0f));
                root.CameraController.CameraMount.localPosition =
                    Quaternion.Euler(32f, 0f, 0f) * Vector3.back * 10.5f;
                root.CameraController.CameraMount.localRotation = Quaternion.Euler(32f, 0f, 0f);
            }
            catch (Exception exception) { result.error = exception.ToString(); }

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(9f);
                try
                {
                    result.character_visible = prototype.HasVisibleCharacter;
                    result.character_provider = prototype.ActiveProvider;
                }
                catch (Exception exception) { result.error = exception.ToString(); }
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    result.screenshot = Path.Combine(outputDirectory, "p81-visual-prototype.png");
                    Capture(result.screenshot);
                }
                catch (Exception exception) { result.error = exception.ToString(); }
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.benchmark_present &&
                result.natural_materials_present && result.brown_trunks_present &&
                result.stable_appearance_present && result.character_visible &&
                File.Exists(result.screenshot);
            var path = Path.Combine(outputDirectory, "p81-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P81_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
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

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
