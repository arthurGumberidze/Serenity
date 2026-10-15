using System;
using System.Collections;
using System.Linq;
using Game.Domain.Weather;
using Game.Infrastructure;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P95")]
    public sealed class P95AtmosphereAndGlobalMapPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalAtmosphereProjectsDayNightRainSnowSeasonWaterAndSoftShadows()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.NaturalAtmosphereProfile, Is.Not.Null);
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), 12 * TimeSpan.TicksPerHour);
            var day = root.WeatherPresentation.CurrentVisualState;
            root.WeatherPresentation.ShowPreview(root.Weather.GetWeatherForDay(0), 23 * TimeSpan.TicksPerHour);
            var night = root.WeatherPresentation.CurrentVisualState;
            Assert.That(day.IsNight, Is.False);
            Assert.That(night.IsNight, Is.True);
            Assert.That(day.LightIntensity, Is.GreaterThan(night.LightIntensity));
            Assert.That(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                .Where(x => x.type == LightType.Directional).All(x => x.shadows == LightShadows.Soft), Is.True);

            var rainy = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Rain);
            root.WeatherPresentation.ShowPreview(rainy);
            Assert.That(root.WeatherPresentation.IsRaining, Is.True);
            Assert.That(root.WeatherPresentation.RainParticles.gameObject.activeSelf, Is.True);
            var snowy = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Snow);
            root.WeatherPresentation.ShowPreview(snowy);
            Assert.That(root.WeatherPresentation.IsSnowing, Is.True);
            Assert.That(root.WeatherPresentation.SnowParticles.gameObject.activeSelf, Is.True);
            Assert.That(root.InhabitableRegion.SnowCoverVisible, Is.True);
            Assert.That(root.InhabitableRegion.WaterRenderer.GetComponent<Collider>(), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalMapShowsSixNaturalLocationsAndPreservesCanonicalSession()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Clock.Pause();
            var character = root.MaleDemoCharacter;
            root.PlayerSaves.Save(2);
            root.GlobalMapBridge.RequestEnterGlobal();
            for (var frame = 0; frame < 180 && !root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            yield return null;
            var map = UnityEngine.Object.FindAnyObjectByType<GlobalMapPresentationController>();
            Assert.That(map, Is.Not.Null);
            Assert.That(map.NaturalMapReady, Is.True);
            Assert.That(map.NaturalLocationCount, Is.EqualTo(6));
            Assert.That(map.NaturalLayoutHash, Is.EqualTo(root.NaturalAtmosphereProfile.ComputeLayoutHash()));
            Assert.That(GameObject.Find("P9.5 Natural Land Relief"), Is.Not.Null);
            Assert.That(GameObject.Find("P9.5 River"), Is.Not.Null);
            Assert.That(map.LocationMarkerCount, Is.EqualTo(root.GlobalMap.Locations.Count));

            root.PlayerSaves.Load(2);
            Assert.That(root.Characters.Get(character.Id), Is.SameAs(character));
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                root.StoneAgeSettlement.Worksites[0].VisualRoot.transform.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

            root.GlobalMapBridge.RequestEnterLocation(new Game.Domain.World.WorldLocationId("starter_settlement"));
            for (var frame = 0; frame < 180 && root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.PointerRaycaster.WorldCamera.enabled, Is.True);
            Assert.That(root.WeatherPresentation.CurrentVisualState.CalendarTick,
                Is.EqualTo(root.Clock.State.CalendarTicks));
        }
    }
}
