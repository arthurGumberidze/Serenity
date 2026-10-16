using System;
using System.Collections;
using System.Linq;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Production;
using Game.Presentation.Characters;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P96")]
    public sealed class P96IntegrationAcceptancePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IntegratedLocalWorldExposesTerrainBiomeSettlementResourcesCharactersAndWeather()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.RealisticGameplayRegion, Is.Not.Null);
            Assert.That(root.PopulatedBiomeWorld.CanopyTrees.Count, Is.GreaterThanOrEqualTo(170));
            Assert.That(root.PopulatedBiomeWorld.MajorRocks.Count, Is.GreaterThanOrEqualTo(90));
            Assert.That(root.PopulatedBiomeWorld.GroundCover.Count, Is.GreaterThanOrEqualTo(900));
            Assert.That(root.PopulatedBiomeWorld.ResourceFormations.Count, Is.EqualTo(24));
            Assert.That(root.StoneAgeSettlement.Worksites.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(root.CharacterPresentations.Count, Is.GreaterThan(0));
            var umaDeadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < umaDeadline)
            {
                var projections = UnityEngine.Object.FindObjectsByType<UmaAppearanceProjection>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (projections.Length >= 2 && projections.All(x => x.UsesUma)) break;
                yield return null;
            }
            var detailedCharacters = UnityEngine.Object.FindObjectsByType<UmaAppearanceProjection>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(detailedCharacters, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(detailedCharacters.All(x => x.UsesUma && x.StoneAgeWardrobeApplied), Is.True);
            var missingRuntimeScripts = Resources.FindObjectsOfTypeAll<Transform>()
                .Where(x => x != null && x.gameObject.scene.IsValid() &&
                    x.GetComponents<Component>().Any(component => component == null))
                .Select(x => $"{x.gameObject.scene.name}/{x.name}").Distinct().ToArray();
            Assert.That(missingRuntimeScripts, Is.Empty,
                "Runtime scene objects must not retain missing scripts: " + string.Join(", ", missingRuntimeScripts));
            var sources = root.InhabitableRegion.ResourceAnchors
                .Select(x => x.VisualRoot.GetComponent<ProductionSourcePresenter>()).Where(x => x != null).ToArray();
            Assert.That(sources, Has.Length.EqualTo(24));
            Assert.That(sources.All(x => x.Site.Id.IsValid && x.RemainingAmount > 0), Is.True);
            Assert.That(root.DirectWorkCommands.EnsureRoleGroups().Select(x => x.Role),
                Does.Contain(WorkGroupRole.Gatherers));
            var rain = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Rain);
            var snow = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Precipitation == PrecipitationType.Snow);
            root.WeatherPresentation.ShowPreview(rain, 12 * TimeSpan.TicksPerHour);
            Assert.That(root.WeatherPresentation.IsRaining, Is.True);
            root.WeatherPresentation.ShowPreview(snow, 23 * TimeSpan.TicksPerHour);
            Assert.That(root.WeatherPresentation.IsSnowing, Is.True);
            Assert.That(root.WeatherPresentation.CurrentVisualState.IsNight, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveLoadAndStrategicRoundTripPreserveStableIdentityAndConnectedLayout()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Clock.Pause();
            var ids = root.Characters.GetAll().Select(x => x.Id).ToArray();
            var source = root.Production.Sites.Where(x => x.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource)
                .OrderBy(x => x.Id).First();
            var amount = source.RemainingAmount;
            var mapHash = root.GlobalMap.ComputeStateHash();
            root.PlayerSaves.Save(2);
            root.PlayerSaves.Load(2);
            Assert.That(root.Characters.GetAll().Select(x => x.Id), Is.EqualTo(ids));
            Assert.That(root.Production.TryGetSite(source.Id, out var restored), Is.True);
            Assert.That(restored.RemainingAmount, Is.EqualTo(amount));
            Assert.That(root.GlobalMap.ComputeStateHash(), Is.EqualTo(mapHash));

            root.GlobalMapBridge.RequestEnterGlobal();
            for (var frame = 0; frame < 240 && !root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            var map = UnityEngine.Object.FindAnyObjectByType<Game.Presentation.Environment.P102ConnectedWorldPresentation>();
            Assert.That(map, Is.Not.Null);
            Assert.That(map.Districts.Count, Is.EqualTo(root.GlobalMap.Locations.Count));
            Assert.That(map.AccessibleAreaSquareMeters, Is.GreaterThanOrEqualTo(163840));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));

            root.GlobalMapBridge.RequestEnterLocation(new WorldLocationId("starter_settlement"));
            for (var frame = 0; frame < 240 && root.GlobalMapBridge.IsGlobalSceneLoaded; frame++) yield return null;
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.PointerRaycaster.WorldCamera.enabled, Is.True);
        }
    }
}
