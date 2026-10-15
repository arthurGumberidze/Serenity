using System;
using System.Linq;
using Game.Domain.Weather;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P95")]
    public sealed class P95AtmosphereAndGlobalMapTests
    {
        [Test]
        public void DefinitionOwnsExactlySixStableLocationVisuals()
        {
            var profile = ScriptableObject.CreateInstance<P95NaturalAtmosphereProfile>();
            profile.ConfigureDefaults();
            Assert.DoesNotThrow(profile.ValidateConfiguration);
            Assert.That(profile.Locations.Count, Is.EqualTo(6));
            Assert.That(profile.Locations.Select(x => x.LocationId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(profile.ComputeLayoutHash(), Is.EqualTo("84BE8C806F7B2B61"));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void SameCanonicalWeatherProjectsReadableDayAndNightWithoutMutation()
        {
            var profile = ScriptableObject.CreateInstance<P95NaturalAtmosphereProfile>();
            profile.ConfigureDefaults();
            var weather = new WeatherState(0, Season.Summer, WeatherType.Clear, 210,
                PrecipitationType.None, 0, 120, WeatherExposureKind.None,
                new WeatherModifiers(1000, 1000, 1000, 0, 1000));
            var day = profile.Evaluate(12 * TimeSpan.TicksPerHour, weather);
            var night = profile.Evaluate(23 * TimeSpan.TicksPerHour, weather);
            Assert.That(day.IsNight, Is.False);
            Assert.That(night.IsNight, Is.True);
            Assert.That(day.LightIntensity, Is.GreaterThan(night.LightIntensity * 3f));
            Assert.That(day.WaterColor, Is.Not.EqualTo(night.WaterColor));
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }
}
