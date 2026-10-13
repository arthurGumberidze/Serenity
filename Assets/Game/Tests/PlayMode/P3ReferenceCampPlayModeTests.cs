using System.Collections;
using Game.Domain.Weather;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P3")]
    public sealed class P3ReferenceCampPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadReferenceCamp()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CanonicalCharactersShelterAndStorageOwnTheirViews()
        {
            var root = Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.ReferenceCamp, Is.Not.Null);
            Assert.That(root.ReferenceCamp.IsInitialized, Is.True);
            Assert.That(root.ReferenceCamp.EquippedToolCount, Is.EqualTo(2));
            Assert.That(root.ReferenceCamp.CampfireEffectsActive, Is.True);
            Assert.That(root.ReferenceCamp.SpatialAudioSourceCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(root.DemoPresenters[0].CharacterId, Is.EqualTo(root.MaleDemoCharacter.Id));
            Assert.That(root.DemoPresenters[1].CharacterId, Is.EqualTo(root.FemaleDemoCharacter.Id));
            Assert.That(root.CharacterPresentations.TryGet(root.MaleDemoCharacter.Id, out var male), Is.True);
            Assert.That(male, Is.SameAs(root.DemoPresenters[0]));
            Assert.That(root.DemoShelterPresenter.BuildingId, Is.EqualTo(root.DemoShelterBuilding.Id));
            Assert.That(root.DemoStoragePresenter.BuildingId, Is.EqualTo(root.DemoStorageBuilding.Id));
            Assert.That(root.Buildings.TryGet(root.DemoShelterBuilding.Id, out _), Is.True);
            Assert.That(root.Buildings.TryGet(root.DemoStorageBuilding.Id, out _), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WeatherPresentationSupportsClearAndRainWithoutMutatingCanonicalWeather()
        {
            var root = Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            var before = root.Weather.CaptureState();
            WeatherState clear = default;
            WeatherState rain = default;
            var foundClear = false;
            var foundRain = false;
            for (var day = 0L; day < 730 && (!foundClear || !foundRain); day++)
            {
                var state = root.Weather.GetWeatherForDay(day);
                if (!foundClear && state.Precipitation == PrecipitationType.None)
                { clear = state; foundClear = true; }
                if (!foundRain && state.Precipitation == PrecipitationType.Rain)
                { rain = state; foundRain = true; }
            }
            Assert.That(foundClear && foundRain, Is.True);
            root.WeatherPresentation.ShowPreview(clear);
            Assert.That(root.WeatherPresentation.IsRaining, Is.False);
            root.WeatherPresentation.ShowPreview(rain);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(root.WeatherPresentation.IsRaining, Is.True);
            Assert.That(root.WeatherPresentation.RainParticles.gameObject.activeSelf, Is.True);
            Assert.That(root.WeatherPresentation.RainAudio.isPlaying, Is.True);
            var after = root.Weather.CaptureState();
            Assert.That(after.WorldSeed, Is.EqualTo(before.WorldSeed));
            Assert.That(after.ExposureCheckpoints, Is.EqualTo(before.ExposureCheckpoints));
        }

        [UnityTest]
        public IEnumerator CampIsReadableThroughExistingRtsAndThirdPersonCameras()
        {
            var root = Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.CameraController, Is.Not.Null);
            Assert.That(root.CameraController.CurrentZoom, Is.InRange(8f, 45f));
            var result = root.ThirdPersonControl.TryEnter(root.MaleDemoCharacter.Id);
            yield return null;
            Assert.That(result.IsEligible, Is.True);
            Assert.That(root.ThirdPersonControl.ThirdPersonCamera, Is.Not.Null);
            Assert.That(root.ThirdPersonControl.ThirdPersonCamera.Priority.Value, Is.GreaterThan(0));
            Assert.That(root.DemoPresenters[0].CharacterId, Is.EqualTo(root.MaleDemoCharacter.Id));
            root.ThirdPersonControl.Exit();
            yield return null;
            Assert.That(root.CameraController.enabled, Is.True);
        }
    }
}
