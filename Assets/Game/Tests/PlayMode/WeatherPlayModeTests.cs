using System;
using System.Collections;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Presentation.Interaction;
using Game.Presentation.Weather;
using Game.Simulation.Time;
using Game.Simulation.Weather;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U19")]
    public sealed class WeatherPlayModeTests
    {
        [UnityTest]
        public IEnumerator DebugPanelRegistersAsWorldPointerBlocker()
        {
            var root = new GameObject("weather-ui-test");
            var raycaster = root.AddComponent<WorldPointerRaycaster>();
            var overlay = root.AddComponent<WeatherDebugOverlay>();
            overlay.Initialize(new WeatherService(1, ClimateProfile.Temperate),
                new GameClock(new GameTimeState()), raycaster);
            yield return null;
            Assert.That(overlay.TryGetUiBlockingRect(out var rect), Is.True);
            Assert.That(rect.width, Is.GreaterThan(0));
            UnityEngine.Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator PresentationRefreshUsesCanonicalWeatherWithoutOwningState()
        {
            var root = new GameObject("weather-presentation-test");
            var presentation = root.AddComponent<WeatherPresentationController>();
            var weather = new WeatherService(3, ClimateProfile.Temperate);
            presentation.Initialize(weather);
            presentation.Refresh(TimeSpan.FromDays(20).Ticks);
            yield return null;
            Assert.That(weather.GetWeatherForDay(20).AbsoluteDay, Is.EqualTo(20));
            Assert.That(root.GetComponents<MonoBehaviour>().Length, Is.EqualTo(1));
            UnityEngine.Object.Destroy(root);
        }
    }
}
