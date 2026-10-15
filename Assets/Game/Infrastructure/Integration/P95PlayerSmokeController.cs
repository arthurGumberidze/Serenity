using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Weather;
using Game.Presentation.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure.Integration
{
    /// <summary>Visible normal-Player proof for P9.5 local atmosphere and natural global map.</summary>
    public sealed class P95PlayerSmokeController : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public bool six_locations;
            public bool day_night;
            public bool rain_snow_seasons;
            public bool water_collision;
            public bool navigation;
            public bool save_load_identity;
            public bool rts_rendered;
            public bool tps_rendered;
            public bool global_rendered;
            public string layout_hash;
            public string rts_screenshot;
            public string tps_screenshot;
            public string global_screenshot;
            public string error;
        }

        private LocalSceneCompositionRoot root;
        private string outputDirectory;

        public static void LaunchFromMenuIfRequested(MainMenuController menu)
        {
            if (Environment.GetCommandLineArgs().Any(x =>
                    string.Equals(x, "-p95Smoke", StringComparison.OrdinalIgnoreCase)))
                menu.StartNewGame(95005);
        }

        public static P95PlayerSmokeController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Any(x => string.Equals(x, "-p95Smoke", StringComparison.OrdinalIgnoreCase))) return null;
            var output = ReadArgument(args, "-p95SmokeOutput");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("-p95SmokeOutput is required.");
            var controller = compositionRoot.gameObject.AddComponent<P95PlayerSmokeController>();
            controller.root = compositionRoot;
            controller.outputDirectory = output;
            controller.StartCoroutine(controller.Run());
            return controller;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            var result = new SmokeResult();
            Try(result, () =>
            {
                root.Clock.Pause();
                root.GameplayUi.SetOnboardingVisible(false);
                root.GameplayUi.SetDeveloperUiVisible(false);
                root.NaturalAtmosphereProfile.ValidateConfiguration();
                result.layout_hash = root.NaturalAtmosphereProfile.ComputeLayoutHash();
                result.six_locations = root.GlobalMap.Locations.Count == 6 &&
                    root.NaturalAtmosphereProfile.Locations.Count == 6;

                var dry = root.Weather.GetWeatherForDay(0);
                root.WeatherPresentation.ShowPreview(dry, 12 * TimeSpan.TicksPerHour);
                var day = root.WeatherPresentation.CurrentVisualState;
                root.WeatherPresentation.ShowPreview(dry, 23 * TimeSpan.TicksPerHour);
                var night = root.WeatherPresentation.CurrentVisualState;
                result.day_night = !day.IsNight && night.IsNight && day.LightIntensity > night.LightIntensity * 2f;
                var rain = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Precipitation == PrecipitationType.Rain);
                var snow = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                    .First(x => x.Precipitation == PrecipitationType.Snow);
                root.WeatherPresentation.ShowPreview(rain);
                var rainVisible = root.WeatherPresentation.IsRaining &&
                    root.WeatherPresentation.RainParticles.gameObject.activeSelf;
                root.WeatherPresentation.ShowPreview(snow);
                result.rain_snow_seasons = rainVisible && root.WeatherPresentation.IsSnowing &&
                    root.WeatherPresentation.SnowParticles.gameObject.activeSelf &&
                    root.InhabitableRegion.DisplayedSeason == Season.Winter &&
                    root.InhabitableRegion.SnowCoverVisible;
                result.water_collision = root.InhabitableRegion.WaterRenderer != null &&
                    root.InhabitableRegion.WaterRenderer.GetComponent<Collider>() == null;
                var path = new NavMeshPath();
                result.navigation = NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                    root.StoneAgeSettlement.Worksites[0].VisualRoot.transform.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete;

                var character = root.MaleDemoCharacter;
                var canonicalHash = root.GlobalMap.ComputeStateHash();
                root.PlayerSaves.Save(2);
                root.PlayerSaves.Load(2);
                result.save_load_identity = ReferenceEquals(character, root.Characters.Get(character.Id)) &&
                    root.GlobalMap.ComputeStateHash() == canonicalHash &&
                    root.NaturalAtmosphereProfile.ComputeLayoutHash() == result.layout_hash;

                root.WeatherPresentation.ShowPreview(dry, 12 * TimeSpan.TicksPerHour);
                root.CameraController.enabled = false;
                var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(4f, 0f, 2f));
                Frame(focus, 34f, 56f, 58f);
            });
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.rts_screenshot = Capture("p95-local-rts-day.png");
                    result.rts_rendered = File.Exists(result.rts_screenshot);
                    root.AiRuntime.enabled = false;
                    var enter = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
                    if (!enter.IsEligible) throw new InvalidOperationException("P9.5 TPS entry failed.");
                });
            }
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    result.tps_screenshot = Capture("p95-local-tps-day.png");
                    result.tps_rendered = root.ThirdPersonControl.IsThirdPersonCameraActive &&
                        File.Exists(result.tps_screenshot);
                    root.ThirdPersonControl.Exit();
                    root.WeatherPresentation.ClearPreview(root.Clock.State.CalendarTicks);
                    root.GlobalMapBridge.RequestEnterGlobal();
                });
            }
            for (var frame = 0; frame < 240 && string.IsNullOrEmpty(result.error) &&
                 !root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            if (string.IsNullOrEmpty(result.error))
            {
                yield return new WaitForSecondsRealtime(2f);
                yield return new WaitForEndOfFrame();
                Try(result, () =>
                {
                    var map = FindAnyObjectByType<GlobalMapPresentationController>() ??
                        throw new InvalidOperationException("P9.5 global map presentation did not load.");
                    if (!map.NaturalMapReady || map.NaturalLocationCount != 6 ||
                        map.NaturalLayoutHash != result.layout_hash)
                        throw new InvalidOperationException("P9.5 natural global map validation failed.");
                    result.global_screenshot = Capture("p95-global-map.png");
                    result.global_rendered = File.Exists(result.global_screenshot);
                });
            }
            result.passed = string.IsNullOrEmpty(result.error) && result.six_locations && result.day_night &&
                result.rain_snow_seasons && result.water_collision && result.navigation &&
                result.save_load_identity && result.rts_rendered && result.tps_rendered && result.global_rendered;
            var resultPath = Path.Combine(outputDirectory, "p95-player-smoke.json");
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            Debug.Log("P95_PLAYER_SMOKE " + (result.passed ? "PASSED " : "FAILED ") + resultPath);
            Application.Quit(result.passed ? 0 : 2);
        }

        private void Frame(Vector3 target, float yaw, float pitch, float distance)
        {
            root.CameraController.transform.SetPositionAndRotation(target, Quaternion.Euler(0f, yaw, 0f));
            root.CameraController.CameraMount.localPosition = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back * distance;
            root.CameraController.CameraMount.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            return null;
        }
    }
}
