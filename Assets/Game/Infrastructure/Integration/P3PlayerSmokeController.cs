using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Weather;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Infrastructure.Integration
{
    /// <summary>Command-line-only visual smoke for the P3 reference camp in the normal Player.</summary>
    public sealed class P3PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public long seed;
            public bool canonical_characters_linked;
            public bool canonical_buildings_linked;
            public bool stable_identity_preserved;
            public bool reference_camp_initialized;
            public int tree_count;
            public int rock_count;
            public int grass_cluster_count;
            public int equipped_tool_count;
            public int spatial_audio_source_count;
            public bool campfire_effects_active;
            public bool clear_preview_applied;
            public bool rain_preview_applied;
            public int rain_particles_alive;
            public bool rts_camera_rendered;
            public bool tps_camera_rendered;
            public string shelter_stable_id;
            public string storage_stable_id;
            public string rts_clear_screenshot;
            public string tps_clear_screenshot;
            public string tps_rain_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;
        private long seed;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p3Smoke", StringComparison.OrdinalIgnoreCase))) return;
            if (!long.TryParse(ReadArgument(arguments, "-p3SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p3SmokeSeed must be a positive whole number.");
            menu.StartNewGame(requestedSeed);
        }

        public static P3PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (!arguments.Any(x => string.Equals(x, "-p3Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(arguments, "-p3SmokeOutput");
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("-p3SmokeOutput is required.");
            if (!long.TryParse(ReadArgument(arguments, "-p3SmokeSeed"), out var requestedSeed) || requestedSeed <= 0)
                throw new InvalidOperationException("-p3SmokeSeed must be a positive whole number.");
            var controller = compositionRoot.gameObject.AddComponent<P3PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.seed = requestedSeed;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult { seed = seed };
            root.Clock.Pause();
            yield return null;
            try
            {
                var camp = root.ReferenceCamp;
                result.reference_camp_initialized = camp != null && camp.IsInitialized;
                result.tree_count = camp?.TreeCount ?? 0;
                result.rock_count = camp?.RockCount ?? 0;
                result.grass_cluster_count = camp?.GrassClusterCount ?? 0;
                result.equipped_tool_count = camp?.EquippedToolCount ?? 0;
                result.spatial_audio_source_count = camp?.SpatialAudioSourceCount ?? 0;
                result.campfire_effects_active = camp != null && camp.CampfireEffectsActive;

                result.canonical_characters_linked = root.DemoPresenters.Count == 2 &&
                    root.DemoPresenters[0].IsBound && root.DemoPresenters[1].IsBound &&
                    root.DemoPresenters[0].CharacterId == root.MaleDemoCharacter.Id &&
                    root.DemoPresenters[1].CharacterId == root.FemaleDemoCharacter.Id &&
                    root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var maleView) &&
                    ReferenceEquals(maleView, root.DemoPresenters[0]);
                result.canonical_buildings_linked = root.DemoShelterBuilding != null &&
                    root.DemoShelterPresenter != null && root.DemoShelterPresenter.IsBound &&
                    root.DemoShelterPresenter.BuildingId == root.DemoShelterBuilding.Id &&
                    root.Buildings.TryGet(root.DemoShelterBuilding.Id, out _) &&
                    root.DemoStorageBuilding != null && root.DemoStoragePresenter != null &&
                    root.DemoStoragePresenter.BuildingId == root.DemoStorageBuilding.Id &&
                    root.Buildings.TryGet(root.DemoStorageBuilding.Id, out _);
                result.stable_identity_preserved = result.canonical_characters_linked && result.canonical_buildings_linked;
                result.shelter_stable_id = root.DemoShelterBuilding?.Id.ToString();
                result.storage_stable_id = root.DemoStorageBuilding?.Id.ToString();

                if (root.GameplayUi != null) root.GameplayUi.RootVisual.style.display = DisplayStyle.None;
                var clear = FindWeather(PrecipitationType.None);
                root.WeatherPresentation.ShowPreview(clear);
                result.clear_preview_applied = !root.WeatherPresentation.IsRaining;
                PrepareRtsCamera();
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            try
            {
                result.rts_clear_screenshot = Path.Combine(outputDirectory, "p3-rts-clear.png");
                Capture(result.rts_clear_screenshot);
                result.rts_camera_rendered = root.CameraController.enabled;
                var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                if (!enter.IsEligible) throw new InvalidOperationException("TPS entry failed: " + enter.Failure);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(0.9f);
            yield return new WaitForEndOfFrame();
            try
            {
                result.tps_clear_screenshot = Path.Combine(outputDirectory, "p3-tps-clear.png");
                Capture(result.tps_clear_screenshot);
                result.tps_camera_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive;
                var rain = FindWeather(PrecipitationType.Rain);
                root.WeatherPresentation.ShowPreview(rain);
                result.rain_preview_applied = root.WeatherPresentation.IsRaining &&
                    root.WeatherPresentation.RainAudio != null && root.WeatherPresentation.RainAudio.isPlaying;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            if (!string.IsNullOrEmpty(result.error)) { Finish(result); yield break; }

            yield return new WaitForSecondsRealtime(1.4f);
            yield return new WaitForEndOfFrame();
            try
            {
                result.rain_particles_alive = root.WeatherPresentation.RainParticles != null
                    ? root.WeatherPresentation.RainParticles.particleCount : 0;
                result.tps_rain_screenshot = Path.Combine(outputDirectory, "p3-tps-rain.png");
                Capture(result.tps_rain_screenshot);
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            Finish(result);
        }

        private WeatherState FindWeather(PrecipitationType precipitation)
        {
            for (var day = 0L; day < 730L; day++)
            {
                var state = root.Weather.GetWeatherForDay(day);
                if (state.Precipitation == precipitation) return state;
            }
            throw new InvalidOperationException("No requested weather preview was found in two deterministic years.");
        }

        private void PrepareRtsCamera()
        {
            root.CameraController.transform.position = new Vector3(0f, 0f, 5f);
            root.CameraController.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
            root.CameraController.enabled = true;
        }

        private void Finish(SmokeResult result)
        {
            result.passed = string.IsNullOrEmpty(result.error) && result.reference_camp_initialized &&
                result.canonical_characters_linked && result.canonical_buildings_linked &&
                result.stable_identity_preserved && result.tree_count >= 14 && result.rock_count >= 8 &&
                result.grass_cluster_count >= 36 && result.equipped_tool_count == 2 &&
                result.spatial_audio_source_count >= 2 && result.campfire_effects_active &&
                result.clear_preview_applied && result.rain_preview_applied && result.rain_particles_alive > 0 &&
                result.rts_camera_rendered && result.tps_camera_rendered;
            var resultPath = Path.Combine(outputDirectory, "p3-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P3_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
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
