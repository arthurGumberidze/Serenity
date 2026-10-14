using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEngine;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible Windows Player proof for the P9.1 realistic-world benchmark.</summary>
    public sealed class P91PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool benchmark_present;
            public bool official_source_recorded;
            public bool terrain_layers_present;
            public bool environment_density_present;
            public bool shaders_valid;
            public int terrain_layers;
            public int trees;
            public int rocks;
            public int foliage;
            public string rts_screenshot;
            public string tps_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p91Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(91001);
        }

        public static P91PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p91Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p91SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p91SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P91PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            P91RealisticWorldBenchmark benchmark = null;
            try
            {
                root.Clock.Pause();
                benchmark = FindAnyObjectByType<P91RealisticWorldBenchmark>();
                if (benchmark == null) throw new InvalidOperationException("P9.1 benchmark is missing.");
                benchmark.ValidateConfiguration();
                result.benchmark_present = true;
                result.official_source_recorded = benchmark.SourceAsset.Contains("213197");
                result.terrain_layers = benchmark.Terrain.terrainData.terrainLayers.Length;
                result.trees = benchmark.Trees.Count;
                result.rocks = benchmark.Rocks.Count;
                result.foliage = benchmark.Foliage.Count;
                result.terrain_layers_present = result.terrain_layers >= 4;
                result.environment_density_present = result.trees >= 24 && result.rocks >= 12 && result.foliage >= 48;
                result.shaders_valid = Valid(benchmark.Trees.Concat(benchmark.Rocks).Concat(benchmark.Foliage)) &&
                                       Valid(new[] { benchmark.WaterRenderer.gameObject });
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                root.GameplayUi.RootVisual.style.display = UnityEngine.UIElements.DisplayStyle.None;
                root.CameraController.enabled = false;
                Frame(benchmark.BenchmarkCenter.position + new Vector3(2f, 0f, 0f), 32f, 34f, 70f);
            }
            catch (Exception exception) { result.error = exception.ToString(); }

            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(3f);
                yield return new WaitForEndOfFrame();
                Try(result, () => result.rts_screenshot = Capture("p91-rts.png"));
            }
            if (string.IsNullOrEmpty(result.error))
            {
                Frame(benchmark.BenchmarkCenter.position + new Vector3(0f, -0.65f, 0f), 90f, 12f, 18f);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () => result.tps_screenshot = Capture("p91-tps.png"));
            }

            result.passed = string.IsNullOrEmpty(result.error) && result.benchmark_present &&
                            result.official_source_recorded && result.terrain_layers_present &&
                            result.environment_density_present && result.shaders_valid &&
                            File.Exists(result.rts_screenshot) && File.Exists(result.tps_screenshot);
            var path = Path.Combine(outputDirectory, "p91-player-smoke.json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("P91_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + path);
            Application.Quit(result.passed ? 0 : 2);
        }

        private void Frame(Vector3 target, float yaw, float pitch, float distance)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static bool Valid(System.Collections.Generic.IEnumerable<GameObject> objects)
        {
            return objects.Where(x => x != null).SelectMany(x => x.GetComponentsInChildren<Renderer>(true))
                .All(x => x.sharedMaterials.Length > 0 && x.sharedMaterials.All(material => material != null &&
                    material.shader != null && material.shader.name != "Hidden/InternalErrorShader"));
        }

        private static void Try(SmokeResult result, Action action)
        {
            try { action(); }
            catch (Exception exception) { result.error = exception.ToString(); }
        }

        private string Capture(string fileName)
        {
            var path = Path.Combine(outputDirectory, fileName);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            return path;
        }

        private static string ReadArgument(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
