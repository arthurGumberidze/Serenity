using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Health;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.ECS.Tier2;
using Game.Infrastructure;
using Game.Simulation.Health;
using Game.Simulation.Production;
using Game.Simulation.Time;
using Game.Simulation.Tiers;
using Game.Simulation.Weather;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.Editor
{
    internal static class WeatherTestSupport
    {
        public static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        public static WeatherState Find(WeatherService service, Func<WeatherState, bool> predicate, int limit = 10000)
        {
            for (var day = 0; day < limit; day++)
            {
                var state = service.GetWeatherForDay(day);
                if (predicate(state)) return state;
            }
            throw new AssertionException("Requested deterministic weather state was not found in the bounded sample.");
        }

        public static Character CreatePerson(int id, string name = "Worker") => Character.Restore(new CharacterState(
            Id(id), new CharacterName(name), CharacterSex.Male, 0, CharacterLifeState.Alive, null, null, null,
            null, null, null, new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        public sealed class FixedWeather : IWeatherModifierProvider
        {
            private readonly WeatherState state;
            public FixedWeather(int crop, int work = 1000, int movement = 1000, int wild = 1000)
            {
                state = new WeatherState(0, Season.Spring, WeatherType.Clear, 150, PrecipitationType.None, 0, 100,
                    WeatherExposureKind.None, new WeatherModifiers(crop, work, movement, 0, wild));
            }
            public WeatherState GetWeatherAtCalendarTick(long calendarTick) => state;
        }

        public sealed class ProductionFixture
        {
            public readonly ResourceId Berries = new ResourceId("berries");
            public readonly ResourceId Wood = new ResourceId("wood_log");
            public readonly ResourceCatalog Resources;
            public readonly ProductionCatalog Recipes;
            public readonly ResourceInventoryRegistry Inventories = new ResourceInventoryRegistry();
            public readonly BuildingRegistry Buildings = new BuildingRegistry();
            public readonly Game.Simulation.Resources.ResourceTransferService Transfers;
            public readonly ProductionService Production;

            public ProductionFixture(IWeatherModifierProvider weather, TimeSpan? growth = null,
                IEnumerable<ResourceStockTarget> targets = null, long storageCapacity = 100000)
            {
                var stone = new ResourceId("stone");
                var fiber = new ResourceId("plant_fiber");
                Resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                    new ResourceDefinition(stone, "Stone", ResourceCategory.Construction),
                    new ResourceDefinition(fiber, "Fiber", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
                    new ResourceDefinition(Berries, "Berries", ResourceCategory.Food)
                });
                Recipes = ProductionCatalog.CreateStoneAge(Resources);
                Transfers = new Game.Simulation.Resources.ResourceTransferService(Inventories);
                var storageId = Id(2);
                Buildings.Add(Building.Restore(new BuildingState(storageId, new BuildingDefinitionId("storage_basket"),
                    new GridCoordinate(5, 5), BuildingOrientation.North, ConstructionState.Completed)));
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storageId),
                    Resources, storageCapacity));
                var configuredTargets = targets ?? new[] { new ResourceStockTarget(Berries, 10000) };
                var settings = new ProductionSettings(TimeSpan.FromDays(1), growth ?? TimeSpan.FromDays(2),
                    TimeSpan.FromDays(15), configuredTargets);
                Production = new ProductionService(Recipes, Resources, Inventories, Buildings, Transfers, settings,
                    weatherModifierProvider: weather);
            }

            public StableEntityId AddWorker(int id)
            {
                var value = Id(id);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, value), Resources, 10000));
                return value;
            }

            public ProductionSite AddPlot(int id)
            {
                var site = ProductionSite.CreateCropPlot(Id(id), new ProductionRecipeId("cultivate_berries"), default,
                    Production.Settings.CropGrowthDuration);
                Production.AddSite(site);
                return site;
            }

            public ProductionSite AddNatural(string recipe, int id, long amount)
            {
                var site = ProductionSite.Restore(new ProductionSiteState(Id(id), new ProductionRecipeId(recipe),
                    ProductionSiteKind.NaturalSource, default, amount, amount, CropPlotPhase.Unplanted, 0, 0));
                Production.AddSite(site);
                return site;
            }
        }
    }

    [TestFixture, Category("U19")]
    public sealed class SeasonCalendarTests
    {
        [Test]
        public void SeasonBoundariesAndFullYearSequenceUseCanonicalCalendar()
        {
            var calendar = SeasonCalendar.EqualSegments365;
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(0).Ticks), Is.EqualTo(Season.Spring));
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(90).Ticks), Is.EqualTo(Season.Spring));
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(91).Ticks), Is.EqualTo(Season.Summer));
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(182).Ticks), Is.EqualTo(Season.Autumn));
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(273).Ticks), Is.EqualTo(Season.Winter));
            Assert.That(calendar.GetSeason(TimeSpan.FromDays(365).Ticks), Is.EqualTo(Season.Spring));
            Assert.That(Enumerable.Range(1, 365).Select(calendar.GetSeasonForDayOfYear).Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void SameTickAlwaysProducesSameSeasonAndInvalidMappingsFail()
        {
            var tick = TimeSpan.FromDays(200).Ticks + TimeSpan.FromHours(13).Ticks;
            Assert.That(SeasonCalendar.EqualSegments365.GetSeason(tick),
                Is.EqualTo(SeasonCalendar.EqualSegments365.GetSeason(tick)));
            Assert.Throws<ArgumentException>(() => new SeasonCalendar(2, 92, 183, 274));
            Assert.Throws<ArgumentException>(() => new SeasonCalendar(1, 92, 92, 274));
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherGenerationTests
    {
        [Test]
        public void SameSeedAndDayMatchWhileGeneratedStatesRemainValid()
        {
            var first = new WeatherService(781, ClimateProfile.Temperate);
            var second = new WeatherService(781, ClimateProfile.Temperate);
            for (var day = 0; day < 730; day++)
            {
                var left = first.GetWeatherForDay(day);
                var right = second.GetWeatherForDay(day);
                Assert.That(right.Type, Is.EqualTo(left.Type));
                Assert.That(right.TemperatureDeciCelsius, Is.EqualTo(left.TemperatureDeciCelsius));
                Assert.That(left.TemperatureDeciCelsius, Is.InRange(-1000, 1000));
                Assert.That(left.PrecipitationPermille, Is.InRange(0, 1000));
                Assert.That(left.WindPermille, Is.InRange(0, 1000));
                if (left.Snowing)
                    Assert.That(left.TemperatureDeciCelsius, Is.LessThanOrEqualTo(first.Climate.SnowThresholdDeciCelsius));
            }
        }

        [Test]
        public void DayLookupIsCollectionOrderIndependent()
        {
            var service = new WeatherService(83, ClimateProfile.Temperate);
            var ordered = Enumerable.Range(0, 365).Select(x => service.GetWeatherForDay(x).Type).ToArray();
            var shuffled = Enumerable.Range(0, 365).Reverse().ToDictionary(x => x, x => service.GetWeatherForDay(x).Type);
            Assert.That(ordered, Is.EqualTo(Enumerable.Range(0, 365).Select(x => shuffled[x]).ToArray()));
        }

        [Test]
        public void InvalidClimateDataFailsCleanly()
        {
            Assert.Throws<ArgumentException>(() => new SeasonalClimate(Season.Spring, 100, 50, 1000, 1000,
                new[] { new WeatherWeight(WeatherType.Clear, 0) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherWeight(WeatherType.Clear, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClimateProfile(new ClimateProfileId("bad"),
                SeasonCalendar.EqualSegments365, new[]
                {
                    new SeasonalClimate(Season.Spring, 0, 0, 1000, 1000, new[] { new WeatherWeight(WeatherType.Clear, 1) })
                }, 200, -100, 350));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherEffect(WeatherType.Rain, 0,
                PrecipitationType.Rain, 800, 700, 0, 100, 1000, 1000, 1000));
            Assert.Throws<ArgumentException>(() => new ClimateProfileCatalog(new[]
                { ClimateProfile.Temperate, ClimateProfile.Temperate }));
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherModifierTests
    {
        [Test]
        public void ClearRainHeavyRainSnowAndAnomaliesHaveMeasurableModifiers()
        {
            var service = new WeatherService(20261007, ClimateProfile.Temperate);
            var clear = WeatherTestSupport.Find(service, x => x.Type == WeatherType.Clear);
            var rain = WeatherTestSupport.Find(service, x => x.Type == WeatherType.Rain);
            var heavy = WeatherTestSupport.Find(service, x => x.Type == WeatherType.HeavyRain);
            var snow = WeatherTestSupport.Find(service, x => x.Type == WeatherType.Snow);
            var anomaly = WeatherTestSupport.Find(service, x => x.Type == WeatherType.ColdSnap || x.Type == WeatherType.HeatWave);
            Assert.That(clear.Modifiers.OutdoorWorkPerThousand, Is.EqualTo(1000));
            Assert.That(rain.Modifiers.OutdoorWorkPerThousand, Is.EqualTo(920));
            Assert.That(heavy.Modifiers.MovementPerThousand, Is.EqualTo(900));
            Assert.That(snow.Modifiers.CropGrowthPerThousand, Is.Zero);
            Assert.That(snow.Modifiers.OutdoorWorkPerThousand, Is.EqualTo(750));
            Assert.That(anomaly.Modifiers.HealthExposureRiskPermille, Is.GreaterThan(0));
        }

        [Test]
        public void TierTwoMovementUsesTheCentralWeatherModifier()
        {
            var clear = new Tier2TransferState(WeatherTestSupport.Id(29), CharacterSex.Male, 0,
                CharacterLifeState.Alive, null, float3.zero, new float3(10, 0, 0), true);
            using (var runtime = new Tier2Runtime("U19 movement modifier"))
            {
                runtime.Materializer.Materialize(clear);
                runtime.Step(new GameTimeAdvance(TimeSpan.FromDays(1), TimeSpan.Zero), 500);
                Assert.That(runtime.Extract(clear.CharacterId).Position.x, Is.EqualTo(5f).Within(0.0001f));
            }
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherFarmingTests
    {
        [Test]
        public void WinterStopsGrowthAndRainCanIncreaseGrowth()
        {
            var winter = new WeatherTestSupport.ProductionFixture(new WeatherTestSupport.FixedWeather(0));
            var winterWorker = winter.AddWorker(10);
            var winterPlot = winter.AddPlot(20);
            Assert.That(winter.Production.TryReserve(winterWorker, default, out var plant), Is.True);
            winter.Production.AdvanceTask(plant.Task.Id, TimeSpan.FromDays(1));
            winter.Production.AdvanceTo(TimeSpan.FromDays(5).Ticks, Array.Empty<StableEntityId>());
            Assert.That(winterPlot.CropPhase, Is.EqualTo(CropPlotPhase.Growing));
            Assert.That(winterPlot.GrowthProgressTicks, Is.Zero);

            var rain = new WeatherTestSupport.ProductionFixture(new WeatherTestSupport.FixedWeather(1100));
            var rainWorker = rain.AddWorker(11);
            var rainPlot = rain.AddPlot(21);
            rain.Production.TryReserve(rainWorker, default, out var rainPlant);
            rain.Production.AdvanceTask(rainPlant.Task.Id, TimeSpan.FromDays(1));
            rain.Production.AdvanceTo(TimeSpan.FromDays(1).Ticks, Array.Empty<StableEntityId>());
            Assert.That(rainPlot.GrowthProgressTicks, Is.EqualTo(TimeSpan.FromDays(1).Ticks * 1100L / 1000L));
        }

        [Test]
        public void SaveLoadAndSplitCatchUpKeepSameCropOutcome()
        {
            var weather = new WeatherService(91, ClimateProfile.Temperate);
            var continuous = GrowingPlot(weather, out var continuousPlot);
            continuous.Production.AdvanceTo(TimeSpan.FromDays(365).Ticks, Array.Empty<StableEntityId>());

            var split = GrowingPlot(weather, out _);
            split.Production.AdvanceTo(TimeSpan.FromDays(170).Ticks, Array.Empty<StableEntityId>());
            var saved = split.Production.CaptureState();
            var restored = new WeatherTestSupport.ProductionFixture(new WeatherService(91, ClimateProfile.Temperate),
                TimeSpan.FromDays(500));
            restored.Production.RestoreState(saved);
            restored.Production.AdvanceTo(TimeSpan.FromDays(365).Ticks, Array.Empty<StableEntityId>());
            var restoredPlot = restored.Production.Sites.Single(x => x.Kind == ProductionSiteKind.PrimitiveCropPlot);
            Assert.That(restoredPlot.GrowthProgressTicks, Is.EqualTo(continuousPlot.GrowthProgressTicks));
            Assert.That(restored.Production.ComputeStateHash(), Is.EqualTo(continuous.Production.ComputeStateHash()));
        }

        private static WeatherTestSupport.ProductionFixture GrowingPlot(IWeatherModifierProvider weather,
            out ProductionSite plot)
        {
            var fixture = new WeatherTestSupport.ProductionFixture(weather, TimeSpan.FromDays(500));
            var worker = fixture.AddWorker(12);
            plot = fixture.AddPlot(22);
            fixture.Production.TryReserve(worker, default, out var plant);
            fixture.Production.AdvanceTask(plant.Task.Id, TimeSpan.FromDays(2));
            return fixture;
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherProductionTests
    {
        [Test]
        public void OutdoorWorkSlowsWithoutChangingOutputOwnershipOrDuplicatingOutput()
        {
            var fixture = new WeatherTestSupport.ProductionFixture(new WeatherTestSupport.FixedWeather(1000, 500),
                targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var worker = fixture.AddWorker(30);
            fixture.AddNatural("chop_wood", 31, 1);
            fixture.Production.TryReserve(worker, default, out var job);
            Assert.That(fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8)).Completed, Is.False);
            var completion = fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8));
            Assert.That(completion.Completed, Is.True);
            Assert.That(completion.OutputOwner.Kind, Is.EqualTo(InventoryOwnerKind.Character));
            Assert.That(fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8)).Completed, Is.False);
            Assert.That(fixture.Production.GetProducedTotal(fixture.Wood), Is.EqualTo(4));
        }

        [Test]
        public void AggregateWorkersSpendRawTimeInsteadOfCancellingTheWeatherSlowdown()
        {
            var storm = new WeatherTestSupport.ProductionFixture(new WeatherTestSupport.FixedWeather(1000, 500),
                targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var clear = new WeatherTestSupport.ProductionFixture(new WeatherTestSupport.FixedWeather(1000, 1000),
                targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var stormWorker = storm.AddWorker(32);
            var clearWorker = clear.AddWorker(33);
            storm.AddNatural("chop_wood", 34, 100);
            clear.AddNatural("chop_wood", 35, 100);

            storm.Production.AdvanceTo(TimeSpan.FromDays(1).Ticks, new[] { stormWorker });
            clear.Production.AdvanceTo(TimeSpan.FromDays(1).Ticks, new[] { clearWorker });

            Assert.That(storm.Production.GetProducedTotal(storm.Wood), Is.EqualTo(4));
            Assert.That(clear.Production.GetProducedTotal(clear.Wood), Is.EqualTo(12));
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherHealthTests
    {
        [TestCase(false), TestCase(true), Category("U30")]
        public void NewOutdoorArrivalNeverBackdatesHealthAfterAuthorityAdvanced(bool restore)
        {
            var registry = new CharacterRegistry();
            var person = WeatherTestSupport.CreatePerson(40);
            registry.Add(person);
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 444);
            var weather = new WeatherService(444, ClimateProfile.Temperate, health);
            var watermark = TimeSpan.FromDays(365).Ticks + TimeSpan.FromHours(1).Ticks;
            health.AdvanceTo(watermark, 0, false);
            if (restore)
            {
                health.RestoreState(health.CaptureState());
                weather.RestoreState(weather.CaptureState());
            }

            var caughtUp = weather.AdvanceHealthExposureTo(watermark, new[] { person.Id, person.Id });
            Assert.That(caughtUp.ProcessedPersonDays, Is.EqualTo(365));
            Assert.That(caughtUp.ConditionsApplied, Is.Zero);
            Assert.That(health.Conditions, Is.Empty, "Unobserved historical exposure must not create retroactive illness.");
            Assert.That(weather.AdvanceHealthExposureTo(watermark, new[] { person.Id }).ProcessedPersonDays, Is.Zero);
            var future = weather.AdvanceHealthExposureTo(TimeSpan.FromDays(730).Ticks, new[] { person.Id });
            Assert.That(future.ConditionsApplied, Is.GreaterThan(0), "The fix must not disable future weather illness.");
            Assert.That(health.Conditions.All(x => x.StartCalendarTick >= watermark), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                health.ApplyCondition(person.Id, new HealthConditionId("sprain"), watermark - 1));
        }

        [Test, Category("U30")]
        public void DeadOutdoorResidentsAdvanceTheirExposureCursorWithoutIllnessAcrossRestore()
        {
            var registry = new CharacterRegistry();
            var dead = WeatherTestSupport.CreatePerson(940);
            dead.MarkDead(0);
            var living = WeatherTestSupport.CreatePerson(941);
            registry.Add(dead);
            registry.Add(living);
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 3030);
            var weather = new WeatherService(3030, ClimateProfile.Temperate, health);
            var tick = TimeSpan.FromDays(35).Ticks;
            Assert.DoesNotThrow(() => weather.AdvanceHealthExposureTo(tick, new[] { dead.Id, living.Id, dead.Id }));
            Assert.That(health.Conditions.Any(x => x.TargetId == dead.Id), Is.False);
            Assert.That(weather.ExposureCheckpoints.Single(x => x.CharacterId == dead.Id).LastProcessedDay, Is.EqualTo(34));
            var saved = weather.CaptureState();
            weather.RestoreState(saved);
            Assert.That(weather.AdvanceHealthExposureTo(tick, new[] { dead.Id, living.Id }).ProcessedPersonDays, Is.Zero);
            Assert.DoesNotThrow(() => weather.AdvanceHealthExposureTo(TimeSpan.FromDays(365).Ticks, new[] { dead.Id }));
            Assert.That(health.Conditions.Any(x => x.TargetId == dead.Id), Is.False);
            Assert.Throws<InvalidOperationException>(() => health.ApplyCondition(dead.Id, new HealthConditionId("cold_stress"), tick));
        }

        [Test, Category("U30")]
        public void UnknownOutdoorIdentityIsRejectedBeforeAnyWeatherOrHealthMutation()
        {
            var registry = new CharacterRegistry();
            var living = WeatherTestSupport.CreatePerson(942);
            registry.Add(living);
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 3030);
            var weather = new WeatherService(3030, ClimateProfile.Temperate, health);
            var before = weather.ComputeStateHash();
            Assert.Catch(() => weather.AdvanceHealthExposureTo(TimeSpan.FromDays(35).Ticks,
                new[] { living.Id, WeatherTestSupport.Id(943) }));
            Assert.That(weather.ComputeStateHash(), Is.EqualTo(before));
            Assert.That(health.Conditions, Is.Empty);
        }

        [Test, Category("U29")]
        public void ExposureSkipsSettledPeopleButCatchesNewArrivalsAndEarlierRestore()
        {
            var first = WeatherTestSupport.Id(901);
            var second = WeatherTestSupport.Id(902);
            var weather = new WeatherService(444, ClimateProfile.Temperate);
            var day = TimeSpan.FromDays(10).Ticks;
            Assert.That(weather.AdvanceHealthExposureTo(day, new[] { first }, false).ProcessedPersonDays, Is.EqualTo(10));
            var checkpoint = weather.CaptureState();
            Assert.That(weather.AdvanceHealthExposureTo(day, new[] { first, first }, false).ProcessedPersonDays, Is.Zero);
            Assert.That(weather.AdvanceHealthExposureTo(day, new[] { second, first, second }, false).ProcessedPersonDays, Is.EqualTo(10));
            var reference = new WeatherService(444, ClimateProfile.Temperate);
            reference.AdvanceHealthExposureTo(day, new[] { first, second });
            Assert.That(weather.ComputeStateHash(), Is.EqualTo(reference.ComputeStateHash()));
            var beforeInvalid = weather.ComputeStateHash();
            Assert.Throws<ArgumentException>(() => weather.AdvanceHealthExposureTo(day + TimeSpan.TicksPerDay,
                new[] { first, default(StableEntityId) }, false));
            Assert.That(weather.ComputeStateHash(), Is.EqualTo(beforeInvalid), "Input validation must remain atomic.");
            weather.AdvanceHealthExposureTo(day + TimeSpan.TicksPerDay, new[] { first, second });
            weather.RestoreState(checkpoint);
            Assert.That(weather.AdvanceHealthExposureTo(day, new[] { first, second }, false).ProcessedPersonDays, Is.EqualTo(10));
            Assert.That(weather.ComputeStateHash(), Is.EqualTo(reference.ComputeStateHash()));
        }

        [Test, Category("U29")]
        public void CachedWeatherMatchesIndependentQueriesIncludingBackwardsDays()
        {
            var weather = new WeatherService(444, ClimateProfile.Temperate);
            foreach (var day in new long[] { 0, 0, 364, 364, 120, 0, 365, 365, 1 })
                Assert.That(weather.GetWeatherForDay(day), Is.EqualTo(new WeatherService(444, ClimateProfile.Temperate).GetWeatherForDay(day)));
            Assert.Throws<ArgumentOutOfRangeException>(() => weather.GetWeatherForDay(-1));
        }

        [Test]
        public void ColdHeatExposureIsDeterministicExactlyOnceAndRestorable()
        {
            var registry = new CharacterRegistry();
            var person = WeatherTestSupport.CreatePerson(40);
            registry.Add(person);
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 444);
            var weather = new WeatherService(444, ClimateProfile.Temperate, health);
            var target = TimeSpan.FromDays(365).Ticks;
            var first = weather.AdvanceHealthExposureTo(target, new[] { person.Id });
            var second = weather.AdvanceHealthExposureTo(target, new[] { person.Id, person.Id });
            Assert.That(first.ProcessedPersonDays, Is.EqualTo(365));
            Assert.That(first.ConditionsApplied, Is.GreaterThan(0));
            Assert.That(second.ProcessedPersonDays, Is.Zero);

            var restoredRegistry = new CharacterRegistry();
            restoredRegistry.Add(WeatherTestSupport.CreatePerson(40));
            var restoredHealth = new HealthAndFamilyService(restoredRegistry, HealthConditionCatalog.CreateStoneAge(), 444);
            restoredHealth.RestoreState(health.CaptureState());
            var restoredWeather = new WeatherService(444, ClimateProfile.Temperate, restoredHealth);
            restoredWeather.RestoreState(weather.CaptureState());
            var afterLoad = restoredWeather.AdvanceHealthExposureTo(target, new[] { person.Id });
            Assert.That(afterLoad.ProcessedPersonDays, Is.Zero);
            Assert.That(restoredHealth.Conditions.Count, Is.EqualTo(health.Conditions.Count));
        }
    }

    [TestFixture, Category("U19")]
    public sealed class SaveLoadWeatherTests
    {
        [Test]
        public void VersionSixPreservesClimateMarkersAndDoesNotRerollCurrentDay()
        {
            var weather = new WeatherService(52, ClimateProfile.Temperate);
            var id = WeatherTestSupport.Id(50);
            weather.AdvanceHealthExposureTo(TimeSpan.FromDays(10).Ticks, new[] { id });
            var time = new GameTimeState(TimeSpan.FromDays(9).Ticks, 0, 1, false);
            var snapshot = new SaveSnapshot(SaveSnapshot.WeatherVersion, WeatherTestSupport.Id(51), time, 400,
                weather: weather.CaptureState());
            var decoded = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var restored = new WeatherService(52, ClimateProfile.Temperate);
            restored.RestoreState(decoded.Weather);
            Assert.That(decoded.FormatVersion, Is.EqualTo(6));
            Assert.That(decoded.Weather.ClimateProfileId, Is.EqualTo(new ClimateProfileId("temperate")));
            Assert.That(restored.GetWeatherAtCalendarTick(time.CalendarTicks).Type,
                Is.EqualTo(weather.GetWeatherAtCalendarTick(time.CalendarTicks).Type));
            Assert.That(restored.ComputeStateHash(), Is.EqualTo(weather.ComputeStateHash()));
        }

        [Test]
        public void LegacySnapshotsVersionsOneThroughFiveRemainReadable()
        {
            var codec = new SaveXmlCodec();
            for (var version = 1; version <= 5; version++)
            {
                var snapshot = new SaveSnapshot(version, WeatherTestSupport.Id(60 + version), new GameTimeState(), 400);
                Assert.That(codec.Deserialize(codec.Serialize(snapshot)).FormatVersion, Is.EqualTo(version));
            }
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherCatchUpEquivalenceTests
    {
        [Test]
        public void ThreeHundredSixtyFiveDayCatchUpEqualsDayByDay()
        {
            var direct = Scenario(new[] { 365 });
            var daily = Scenario(Enumerable.Range(1, 365).ToArray());
            Assert.That(direct, Is.EqualTo(daily));
        }

        private static ulong Scenario(IEnumerable<int> days)
        {
            var weather = new WeatherService(77, ClimateProfile.Temperate);
            var fixture = new WeatherTestSupport.ProductionFixture(weather,
                targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 10000) });
            var worker = fixture.AddWorker(70);
            fixture.AddNatural("chop_wood", 71, 1000);
            foreach (var day in days) fixture.Production.AdvanceTo(TimeSpan.FromDays(day).Ticks, new[] { worker });
            return fixture.Production.ComputeStateHash();
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherTierRouteEquivalenceTests
    {
        [Test]
        public void ActualTierThreeTierTwoAndTierOneRoutesConverge()
        {
            var t3 = Scenario(null);
            var t3t2t3 = Scenario(CharacterSimulationTier.Tier2);
            var t3t1t3 = Scenario(CharacterSimulationTier.Tier1);
            Assert.That(t3t2t3, Is.EqualTo(t3));
            Assert.That(t3t1t3, Is.EqualTo(t3));
        }

        private static ulong Scenario(CharacterSimulationTier? intermediate)
        {
            var weather = new WeatherService(88, ClimateProfile.Temperate);
            var fixture = new WeatherTestSupport.ProductionFixture(weather,
                targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 10000) });
            var person = WeatherTestSupport.CreatePerson(80);
            var registry = new CharacterRegistry();
            registry.Add(person);
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
            tier3.Seed(new CharacterRuntimeState(person.Id, default, default, false, "weather-test"));
            var manager = new TierManager(registry, tier1, tier2, tier3);
            manager.RegisterExisting(person.Id, CharacterSimulationTier.Tier3);
            var worker = fixture.AddWorker(80);
            fixture.AddNatural("chop_wood", 82, 2000);
            fixture.Production.AdvanceTo(TimeSpan.FromDays(100).Ticks, new[] { worker });
            if (intermediate.HasValue) manager.Transition(person.Id, intermediate.Value);
            fixture.Production.AdvanceTo(TimeSpan.FromDays(240).Ticks, new[] { worker });
            if (intermediate.HasValue) manager.Transition(person.Id, CharacterSimulationTier.Tier3);
            fixture.Production.AdvanceTo(TimeSpan.FromDays(365).Ticks, new[] { worker });
            return fixture.Production.ComputeStateHash();
        }

        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) { Tier = tier; }
            public CharacterSimulationTier Tier { get; }
            public bool IsActive(StableEntityId characterId) => states.ContainsKey(characterId);
            public void ValidateMaterialization(Character character, CharacterRuntimeState state) { }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public CharacterRuntimeState Capture(StableEntityId characterId) => states[characterId];
            public void Dematerialize(StableEntityId characterId) => states.Remove(characterId);
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
        }
    }

    [TestFixture, Category("U19")]
    public sealed class CollectionOrderIndependenceTests
    {
        [Test]
        public void WeatherAndExposureInputsIgnoreEnumerationOrder()
        {
            var first = new WeatherService(606, ClimateProfile.Temperate);
            var second = new WeatherService(606, ClimateProfile.Temperate);
            var ids = new[] { WeatherTestSupport.Id(91), WeatherTestSupport.Id(92), WeatherTestSupport.Id(93) };
            first.AdvanceHealthExposureTo(TimeSpan.FromDays(30).Ticks, ids);
            second.AdvanceHealthExposureTo(TimeSpan.FromDays(30).Ticks, ids.Reverse());
            Assert.That(second.ComputeStateHash(), Is.EqualTo(first.ComputeStateHash()));
        }
    }

    [TestFixture, Category("U19")]
    public sealed class WeatherDeterminismTests
    {
        [Test]
        public void TenThousandDailyQueriesHaveNoHotLoopAllocation()
        {
            var weather = new WeatherService(902, ClimateProfile.Temperate);
            weather.GetWeatherForDay(0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            ulong hash = 0;
            for (var day = 0; day < 10000; day++)
            {
                var state = weather.GetWeatherForDay(day);
                hash ^= unchecked((ulong)state.TemperatureDeciCelsius) + (ulong)state.Type;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"U19 weather generation: 10000 days, allocatedBytes={allocated}, hash={hash:X16}");
            Assert.That(allocated, Is.LessThan(1024));
        }

        [Test]
        public void FiveYearWeatherFarmingFoodBaselineIsRepeatableAndCheap()
        {
            var stopwatch = Stopwatch.StartNew();
            var first = FiveYear(901);
            var second = FiveYear(901);
            stopwatch.Stop();
            Assert.That(second.Hash, Is.EqualTo(first.Hash));
            Assert.That(second.Food, Is.EqualTo(first.Food));
            Assert.That(first.Shortage, Is.GreaterThanOrEqualTo(0));
            Assert.That(first.Food, Is.GreaterThanOrEqualTo(0));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(30000));
            TestContext.WriteLine($"U19 five-year hash {first.Hash:X16}; food {first.Food}; shortage {first.Shortage}; " +
                $"two runs {stopwatch.ElapsedMilliseconds} ms.");
        }

        private static (ulong Hash, long Food, long Shortage) FiveYear(long seed)
        {
            var weather = new WeatherService(seed, ClimateProfile.Temperate);
            var fixture = new WeatherTestSupport.ProductionFixture(weather,
                targets: new[] { new ResourceStockTarget(new ResourceId("berries"), 4000) }, storageCapacity: 100000);
            var workers = Enumerable.Range(100, 20).Select(fixture.AddWorker).ToArray();
            fixture.AddNatural("gather_berries", 200, 10000);
            fixture.AddPlot(201);
            fixture.Production.Population = 20;
            fixture.Production.AdvanceTo(TimeSpan.FromDays(365 * 5).Ticks, workers);
            var state = weather.GetWeatherForDay(365 * 5 - 1);
            Assert.That(state.TemperatureDeciCelsius, Is.InRange(-1000, 1000));
            return (fixture.Production.ComputeStateHash(), fixture.Production.GetCanonicalTotal(fixture.Berries),
                fixture.Production.Food.TotalShortage);
        }
    }
}
