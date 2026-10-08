using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Weather;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U24")]
    public sealed class BuildingDestructionFireCaptureTests
    {
        private sealed class FixedWeather : IWeatherModifierProvider
        {
            private readonly WeatherType type;
            public FixedWeather(WeatherType weatherType) { type = weatherType; }
            public WeatherState GetWeatherAtCalendarTick(long calendarTick) => new WeatherState(
                calendarTick / TimeSpan.FromDays(1).Ticks, Season.Summer, type, 200,
                type == WeatherType.Rain || type == WeatherType.HeavyRain ? PrecipitationType.Rain :
                type == WeatherType.Snow ? PrecipitationType.Snow : PrecipitationType.None,
                type == WeatherType.HeavyRain ? 900 : type == WeatherType.Rain || type == WeatherType.Snow ? 500 : 0,
                200, WeatherExposureKind.None, new WeatherModifiers(1000, 1000, 1000, 0, 1000));
        }

        private sealed class Fixture
        {
            public readonly ResourceId Berries = new ResourceId("berries");
            public readonly ResourceId Wood = new ResourceId("wood_log");
            public readonly ResourceCatalog Resources;
            public readonly ResourceInventoryRegistry Inventories = new ResourceInventoryRegistry();
            public readonly BuildingRegistry Buildings = new BuildingRegistry();
            public readonly BuildingOccupancyGrid Occupancy = new BuildingOccupancyGrid();
            public readonly WorldPileService Piles;
            public readonly ProductionService Production;
            public readonly BuildingDefinition StorageDefinition;
            public readonly BuildingDefinition CropDefinition;
            public readonly BuildingDefinition ShelterDefinition;
            public readonly BuildingDamageService Damage;

            public Fixture(WeatherType weather = WeatherType.Clear)
            {
                Resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
                    new ResourceDefinition(Berries, "Berries", ResourceCategory.Food)
                });
                StorageDefinition = new BuildingDefinition(new BuildingDefinitionId("storage_basket"), "Storage",
                    BuildingCategory.Storage, new BuildingFootprint(2, 1), storageCapacity: 100,
                    maxIntegrity: 1000, fireDamagePerThousand: 1000, combatDamagePerThousand: 800);
                CropDefinition = new BuildingDefinition(new BuildingDefinitionId("primitive_crop_plot"), "Crop",
                    BuildingCategory.Structure, new BuildingFootprint(3, 3), maxIntegrity: 600,
                    fireDamagePerThousand: 1400, combatDamagePerThousand: 1000);
                ShelterDefinition = new BuildingDefinition(new BuildingDefinitionId("primitive_shelter"), "Shelter",
                    BuildingCategory.Shelter, new BuildingFootprint(4, 3), maxIntegrity: 1200,
                    fireDamagePerThousand: 900, combatDamagePerThousand: 700);
                var transfers = new ResourceTransferService(Inventories);
                Production = new ProductionService(ProductionCatalog.CreateStoneAge(Resources), Resources,
                    Inventories, Buildings, transfers);
                Piles = new WorldPileService(Resources, Inventories);
                Damage = new BuildingDamageService(Buildings,
                    new[] { StorageDefinition, CropDefinition, ShelterDefinition }, Production, Inventories,
                    Piles, new FixedWeather(weather), occupancyGrid: Occupancy);
            }

            public Building AddBuilding(int id, BuildingDefinition definition, int x = 0, int z = 0,
                string owner = "player", bool attachStorage = false)
            {
                var buildingId = Id(id);
                var state = new BuildingState(buildingId, definition.Id, new GridCoordinate(x, z),
                    BuildingOrientation.North, ConstructionState.Completed, definition.MaxIntegrity,
                    definition.MaxIntegrity, BuildingStructuralStatus.Intact, new CombatSideId(owner),
                    BuildingFireStatus.None, 0, 0, 0, 0, false, false, null, null);
                var building = Building.Restore(state);
                Buildings.Add(building);
                Occupancy.Reserve(definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation),
                    building.Id, definition.AllowsOverlap);
                if (attachStorage)
                    Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                        building.Id), Resources, definition.StorageCapacity, definition.StorageCategories));
                return building;
            }

            public StableEntityId AddWorker(int id)
            {
                var value = Id(id);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, value),
                    Resources, 20));
                return value;
            }

            public static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        }

        [Test]
        public void StructuralDamageIsBoundedDataDrivenAndExactlyOnce()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(1, fixture.StorageDefinition, 2, 3);
            var eventId = Fixture.Id(100);
            var first = fixture.Damage.ApplyDamage(building.Id, eventId, 200, BuildingDamageType.Combat, 10);
            Assert.That(first.Applied, Is.EqualTo(160));
            Assert.That(building.CurrentIntegrity, Is.EqualTo(840));
            Assert.That(building.StructuralStatus, Is.EqualTo(BuildingStructuralStatus.Damaged));
            Assert.That(fixture.Damage.ApplyDamage(building.Id, eventId, 200, BuildingDamageType.Combat, 10).Failure,
                Is.EqualTo(BuildingDamageFailure.DuplicateEvent));
            var destroyed = fixture.Damage.ApplyDamage(building.Id, Fixture.Id(101), 5000,
                BuildingDamageType.Generic, 20);
            Assert.That(destroyed.DestroyedNow, Is.True);
            Assert.That(building.CurrentIntegrity, Is.Zero);
            Assert.That(building.Id, Is.EqualTo(Fixture.Id(1)));
            Assert.That(building.History.Count(x => x.Kind == BuildingEventKind.Destroyed), Is.EqualTo(1));
            Assert.That(fixture.Occupancy.IsOccupied(new GridCoordinate(2, 3)), Is.True, "ruins keep occupancy");
        }

        [Test]
        public void DestroyedCropCancelsActiveTaskAndStopsGrowthAndFutureWork()
        {
            var fixture = new Fixture();
            var plot = fixture.AddBuilding(2, fixture.CropDefinition);
            var site = ProductionSite.CreateCropPlot(plot.Id, new ProductionRecipeId("cultivate_berries"),
                default, TimeSpan.FromDays(2));
            fixture.Production.AddSite(site);
            var worker = fixture.AddWorker(50);
            Assert.That(fixture.Production.TryReserve(worker, default, out var job), Is.True);
            fixture.Damage.ApplyDamage(plot.Id, Fixture.Id(200), 9999, BuildingDamageType.Generic, 1);
            Assert.That(fixture.Production.IsTaskActive(job.Task.Id), Is.False);
            Assert.That(fixture.Production.IsSiteOperational(plot.Id), Is.False);
            fixture.Production.AdvanceTo(TimeSpan.FromDays(5).Ticks, new[] { worker });
            Assert.That(site.CropPhase, Is.EqualTo(CropPlotPhase.Unplanted));
            Assert.That(fixture.Production.TryReserve(worker, default, out _), Is.False);
        }

        [Test]
        public void DestroyedStorageSpillsAtomicallyWithoutDuplication()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(3, fixture.StorageDefinition, 4, 5, attachStorage: true);
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            var storage = fixture.Inventories.Get(owner);
            storage.TryAdd(fixture.Wood, new ResourceQuantity(17));
            storage.TryAdd(fixture.Berries, new ResourceQuantity(9));
            fixture.Damage.ApplyDamage(building.Id, Fixture.Id(300), 2000, BuildingDamageType.Generic, 1);
            Assert.That(storage.TotalUnits, Is.Zero);
            Assert.That(fixture.Piles.All.Count(), Is.EqualTo(2));
            Assert.That(fixture.Inventories.All.Sum(x => x.TotalUnits), Is.EqualTo(26));
            Assert.That(building.StorageSpilled, Is.True);
            fixture.Damage.ApplyDamage(building.Id, Fixture.Id(301), 1, BuildingDamageType.Generic, 2);
            Assert.That(fixture.Piles.All.Count(), Is.EqualTo(2));
            Assert.That(fixture.Inventories.All.Sum(x => x.TotalUnits), Is.EqualTo(26));
        }

        [Test]
        public void FireIgnitesProgressesAndExtinguishesOnCanonicalTicks()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(4, fixture.ShelterDefinition);
            Assert.That(fixture.Damage.IgniteBuilding(building.Id, 800, 0).Succeeded, Is.True);
            fixture.Damage.AdvanceFireTo(TimeSpan.FromHours(3).Ticks);
            Assert.That(building.CurrentIntegrity, Is.LessThan(building.MaxIntegrity));
            Assert.That(building.AccumulatedFireDamage, Is.GreaterThan(0));
            var checkpoint = building.LastFireProcessedTick;
            Assert.That(fixture.Damage.ExtinguishBuilding(building.Id, checkpoint).Succeeded, Is.True);
            var integrity = building.CurrentIntegrity;
            fixture.Damage.AdvanceFireTo(TimeSpan.FromDays(2).Ticks);
            Assert.That(building.CurrentIntegrity, Is.EqualTo(integrity));
            Assert.That(building.FireStatus, Is.EqualTo(BuildingFireStatus.Extinguished));
        }

        [Test]
        public void RainReducesFireDamageWithoutReroll()
        {
            var clear = new Fixture(WeatherType.Clear);
            var rainy = new Fixture(WeatherType.HeavyRain);
            var clearBuilding = clear.AddBuilding(5, clear.ShelterDefinition);
            var rainyBuilding = rainy.AddBuilding(5, rainy.ShelterDefinition);
            clear.Damage.IgniteBuilding(clearBuilding.Id, 900, 0);
            rainy.Damage.IgniteBuilding(rainyBuilding.Id, 900, 0);
            clear.Damage.AdvanceFireTo(TimeSpan.FromHours(3).Ticks);
            rainy.Damage.AdvanceFireTo(TimeSpan.FromHours(3).Ticks);
            Assert.That(rainyBuilding.AccumulatedFireDamage, Is.LessThan(clearBuilding.AccumulatedFireDamage));
            Assert.That(rainyBuilding.FireIntensity, Is.LessThan(clearBuilding.FireIntensity));
        }

        [Test]
        public void DirectCatchUpIncrementalAndReversedCollectionOrderMatch()
        {
            var direct = FireScenario(false);
            var incremental = FireScenario(true);
            var reversed = FireScenario(false, true);
            Assert.That(incremental, Is.EqualTo(direct));
            Assert.That(reversed, Is.EqualTo(direct));
            TestContext.Out.WriteLine("U24 fire catch-up/order hash: " + direct.ToString("X16"));
        }

        [Test]
        public void CaptureAfterVictoryChangesOnlyOwnerAndCancelsOldTask()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(6, fixture.CropDefinition, 7, 8);
            var site = ProductionSite.CreateCropPlot(building.Id, new ProductionRecipeId("cultivate_berries"),
                default, TimeSpan.FromDays(2));
            fixture.Production.AddSite(site);
            var worker = fixture.AddWorker(51);
            fixture.Production.TryReserve(worker, default, out var job);
            var before = building.CaptureState();
            var result = fixture.Damage.CaptureBuilding(building.Id, new CombatSideId("enemy"),
                FinishedBattle(600, CombatOutcome.SideBVictory), 100);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(building.OwnerSideId, Is.EqualTo(new CombatSideId("enemy")));
            Assert.That(building.Id, Is.EqualTo(before.Id));
            Assert.That(building.Coordinate, Is.EqualTo(before.Coordinate));
            Assert.That(building.CurrentIntegrity, Is.EqualTo(before.CurrentIntegrity));
            Assert.That(fixture.Production.IsTaskActive(job.Task.Id), Is.False);
            Assert.That(fixture.Production.IsSiteOperational(building.Id), Is.True,
                "captured intact advanced buildings remain usable");
            Assert.That(fixture.Damage.CaptureBuilding(building.Id, new CombatSideId("enemy"),
                FinishedBattle(601, CombatOutcome.SideBVictory), 101).Failure, Is.EqualTo(BuildingCaptureFailure.SameOwner));
        }

        [Test]
        public void DestroyedOrNonVictoriousCaptureIsRejected()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(7, fixture.ShelterDefinition);
            Assert.That(fixture.Damage.CaptureBuilding(building.Id, new CombatSideId("enemy"),
                FinishedBattle(700, CombatOutcome.SideAVictory), 10).Failure,
                Is.EqualTo(BuildingCaptureFailure.NotVictoriousSide));
            fixture.Damage.ApplyDamage(building.Id, Fixture.Id(701), 9999, BuildingDamageType.Generic, 11);
            Assert.That(fixture.Damage.CaptureBuilding(building.Id, new CombatSideId("enemy"),
                FinishedBattle(702, CombatOutcome.SideBVictory), 12).Failure, Is.EqualTo(BuildingCaptureFailure.Destroyed));
        }

        [Test]
        public void PhysicalAndAbstractCombatHooksUseIdenticalDamageAuthority()
        {
            var physical = new Fixture();
            var abstracted = new Fixture();
            var a = physical.AddBuilding(8, physical.StorageDefinition);
            var b = abstracted.AddBuilding(8, abstracted.StorageDefinition);
            var battle = Fixture.Id(800);
            var first = physical.Damage.ApplyCombatDamage(a.Id, battle, 1, 100, BattleMode.Physical, 5);
            var second = abstracted.Damage.ApplyCombatDamage(b.Id, battle, 1, 100, BattleMode.Abstract, 5);
            Assert.That(first.Applied, Is.EqualTo(second.Applied));
            Assert.That(a.CurrentIntegrity, Is.EqualTo(b.CurrentIntegrity));
            Assert.That(a.History.Single().DamageType, Is.EqualTo(BuildingDamageType.Combat));
        }

        [Test]
        public void VersionElevenRoundTripPersistsFireOwnerDamageAndSpillWhileV1ToV10Load()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(9, fixture.StorageDefinition, attachStorage: true);
            fixture.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id))
                .TryAdd(fixture.Wood, new ResourceQuantity(4));
            fixture.Damage.IgniteBuilding(building.Id, 700, 0);
            fixture.Damage.AdvanceFireTo(TimeSpan.FromHours(2).Ticks);
            var snapshot = new SaveSnapshot(SaveSnapshot.BuildingVersion, Fixture.Id(900), new GameTimeState(), 400,
                production: fixture.Production.CaptureState(), buildingWorld: fixture.Damage.CaptureState());
            var codec = new SaveXmlCodec();
            var restored = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(11));
            var state = restored.BuildingWorld.Buildings.Single();
            Assert.That(state.CurrentIntegrity, Is.EqualTo(building.CurrentIntegrity));
            Assert.That(state.FireStatus, Is.EqualTo(BuildingFireStatus.Burning));
            Assert.That(state.LastFireProcessedTick, Is.EqualTo(building.LastFireProcessedTick));
            Assert.That(state.OwnerSideId, Is.EqualTo(new CombatSideId("player")));
            for (var version = 1; version <= 10; version++)
            {
                var legacy = new SaveSnapshot(version, Fixture.Id(900 + version), new GameTimeState(), 400);
                Assert.That(codec.Deserialize(codec.Serialize(legacy)).FormatVersion, Is.EqualTo(version));
            }
        }

        [Test]
        public void OneHundredIndependentFiresAreDeterministic()
        {
            var first = ManyFireScenario(100, false, TimeSpan.FromDays(2).Ticks);
            var second = ManyFireScenario(100, true, TimeSpan.FromDays(2).Ticks);
            Assert.That(second, Is.EqualTo(first));
            TestContext.Out.WriteLine("U24 100-fire hash: " + first.ToString("X16"));
        }

        [Test]
        public void ThousandBuildingStressAndLongRunPreserveValidState()
        {
            var fixture = new Fixture();
            for (var i = 0; i < 1000; i++)
            {
                var building = fixture.AddBuilding(10000 + i, fixture.ShelterDefinition,
                    (i % 100) * 5, (i / 100) * 4);
                if (i < 100) fixture.Damage.IgniteBuilding(building.Id, 500 + i % 400, 0);
                else if (i % 7 == 0) fixture.Damage.ApplyDamage(building.Id, Fixture.Id(20000 + i),
                    100 + i % 300, BuildingDamageType.Generic, 0);
            }
            var watch = Stopwatch.StartNew();
            var before = GC.GetAllocatedBytesForCurrentThread();
            fixture.Damage.AdvanceFireTo(TimeSpan.FromDays(30).Ticks);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            watch.Stop();
            Assert.That(fixture.Buildings.Count, Is.EqualTo(1000));
            Assert.That(fixture.Buildings.All.All(x => x.CurrentIntegrity >= 0 && x.CurrentIntegrity <= x.MaxIntegrity), Is.True);
            Assert.That(fixture.Buildings.All.All(x => x.OwnerSideId.IsValid), Is.True);
            var hash = fixture.Damage.ComputeStateHash();
            TestContext.Out.WriteLine($"U24 stress: {watch.Elapsed.TotalMilliseconds:0.###} ms, {allocated} bytes, hash {hash:X16}");
        }

        [Test]
        public void MixedSettlementLongRunIsDeterministicWithoutResourceDuplicationOrResurrection()
        {
            var first = MixedLongRun();
            var second = MixedLongRun();
            Assert.That(second, Is.EqualTo(first));
            TestContext.Out.WriteLine("U24 mixed long-run: " + first);
        }

        [Test]
        public void InvalidCommandsFailCleanly()
        {
            var fixture = new Fixture();
            var building = fixture.AddBuilding(10, fixture.ShelterDefinition);
            Assert.That(fixture.Damage.ApplyDamage(Fixture.Id(999), Fixture.Id(1), 10,
                BuildingDamageType.Generic, 0).Failure, Is.EqualTo(BuildingDamageFailure.MissingBuilding));
            Assert.That(fixture.Damage.ApplyDamage(building.Id, Fixture.Id(2), -1,
                BuildingDamageType.Generic, 0).Failure, Is.EqualTo(BuildingDamageFailure.InvalidAmount));
            Assert.That(fixture.Damage.IgniteBuilding(building.Id, 0, 0).Failure,
                Is.EqualTo(BuildingFireFailure.InvalidIntensity));
            Assert.That(fixture.Damage.ExtinguishBuilding(building.Id, 0).Failure,
                Is.EqualTo(BuildingFireFailure.NotBurning));
        }

        private static ulong FireScenario(bool incremental, bool reverse = false)
        {
            var fixture = new Fixture();
            var ids = reverse ? new[] { 13, 12, 11 } : new[] { 11, 12, 13 };
            foreach (var id in ids)
            {
                var building = fixture.AddBuilding(id, fixture.ShelterDefinition, id * 5, 0);
                fixture.Damage.IgniteBuilding(building.Id, 850 - id, 0);
            }
            if (incremental)
                for (var hour = 1; hour <= 18; hour++) fixture.Damage.AdvanceFireTo(TimeSpan.FromHours(hour).Ticks);
            else fixture.Damage.AdvanceFireTo(TimeSpan.FromHours(18).Ticks);
            return fixture.Damage.ComputeStateHash();
        }

        private static ulong ManyFireScenario(int count, bool reverse, long ticks)
        {
            var fixture = new Fixture();
            var indexes = Enumerable.Range(0, count).ToArray();
            if (reverse) Array.Reverse(indexes);
            foreach (var index in indexes)
            {
                var building = fixture.AddBuilding(30000 + index, fixture.ShelterDefinition, index * 5, 0);
                fixture.Damage.IgniteBuilding(building.Id, 500 + index % 400, 0);
            }
            fixture.Damage.AdvanceFireTo(ticks);
            return fixture.Damage.ComputeStateHash();
        }

        private static string MixedLongRun()
        {
            var fixture = new Fixture(WeatherType.Rain);
            var storage = fixture.AddBuilding(40000, fixture.StorageDefinition, 0, 0, attachStorage: true);
            var inventory = fixture.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storage.Id));
            inventory.TryAdd(fixture.Wood, new ResourceQuantity(30));
            inventory.TryAdd(fixture.Berries, new ResourceQuantity(20));
            var plot = fixture.AddBuilding(40001, fixture.CropDefinition, 5, 0);
            fixture.Production.AddSite(ProductionSite.CreateCropPlot(plot.Id,
                new ProductionRecipeId("cultivate_berries"), default, TimeSpan.FromDays(2)));
            var worker = fixture.AddWorker(40002);
            for (var i = 0; i < 12; i++)
            {
                var building = fixture.AddBuilding(40100 + i, fixture.ShelterDefinition, i * 5, 8);
                if (i < 4) fixture.Damage.IgniteBuilding(building.Id, 500 + i * 50, 0);
                else if (i % 3 == 0) fixture.Damage.ApplyDamage(building.Id, Fixture.Id(41000 + i), 250,
                    BuildingDamageType.Generic, 0);
            }
            fixture.Production.TryReserve(worker, default, out _);
            fixture.Damage.CaptureBuilding(plot.Id, new CombatSideId("enemy"),
                FinishedBattle(42000, CombatOutcome.SideBVictory), TimeSpan.FromDays(1).Ticks);
            var before = fixture.Inventories.All.Sum(x => x.TotalUnits);
            fixture.Damage.AdvanceFireTo(TimeSpan.FromDays(180).Ticks);
            fixture.Production.AdvanceTo(TimeSpan.FromDays(180).Ticks, new[] { worker });
            var after = fixture.Inventories.All.Sum(x => x.TotalUnits);
            Assert.That(after, Is.GreaterThanOrEqualTo(before));
            Assert.That(fixture.Buildings.All.All(x => !x.IsDestroyed || x.CurrentIntegrity == 0), Is.True);
            Assert.That(plot.OwnerSideId, Is.EqualTo(new CombatSideId("enemy")));
            return $"hash={fixture.Damage.ComputeStateHash():X16};resources={after};piles={fixture.Piles.All.Count()};produced={fixture.Production.GetProducedTotal(fixture.Berries)}";
        }

        private static CombatEncounterState FinishedBattle(int id, CombatOutcome outcome)
        {
            var player = new CombatSideId("player");
            var enemy = new CombatSideId("enemy");
            return new CombatEncounterState(Fixture.Id(id), player, enemy, 0, 100,
                CombatEncounterStatus.Finished, outcome, new[]
                {
                    new CombatantCombatState(Fixture.Id(id + 10000), player, new CombatWeaponId("unarmed"),
                        CombatantState.Disengaged, null, null, 0, 0, 500, default, null, false),
                    new CombatantCombatState(Fixture.Id(id + 20000), enemy, new CombatWeaponId("unarmed"),
                        CombatantState.Fighting, null, null, 0, 0, 500, default, null, false)
                }, Array.Empty<CombatAttackRecordState>());
        }
    }
}
