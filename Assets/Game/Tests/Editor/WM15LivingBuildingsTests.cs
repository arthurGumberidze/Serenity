using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.Health;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Buildings;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM15")]
    public sealed class WM15LivingBuildingsTests
    {
        private sealed class AlwaysCapable : ICharacterCapabilityProvider
        {
            public CharacterCapabilities GetCapabilities(StableEntityId characterId) =>
                new CharacterCapabilities(true, false, 1000, 1000, 1000, 1000, true);
        }

        [Test]
        public void StorageFiltersAndAbsoluteIssuanceLockRoundTrip()
        {
            var wood = new ResourceId("wood_log");
            var food = new ResourceId("cooked_meat");
            var catalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(food, "Meal", ResourceCategory.Food)
            });
            var inventory = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, Id(1)),
                catalog, 20);
            Assert.That(inventory.TryAdd(wood, new ResourceQuantity(5)), Is.True);
            inventory.ConfigureAcceptance(new[] { ResourceCategory.Food }, new[] { food });
            inventory.SetIssuanceLocked(true);

            Assert.That(inventory.TryAdd(wood, new ResourceQuantity(1)), Is.False);
            Assert.That(inventory.TryAdd(food, new ResourceQuantity(2)), Is.True);
            Assert.That(inventory.TryRemove(wood, new ResourceQuantity(1)), Is.False);

            var restored = ResourceInventory.Restore(inventory.CaptureState(), catalog);
            Assert.That(restored.IssuanceLocked, Is.True);
            Assert.That(restored.AcceptedResources.Single(), Is.EqualTo(food));
            Assert.That(restored.GetAmount(food).Units, Is.EqualTo(2));
        }

        [Test]
        public void SplitFamilyWaitsAndMovesTogetherWhenLargeEnoughHomeAppears()
        {
            var familyId = Id(90);
            var characters = new CharacterRegistry();
            for (var i = 1; i <= 5; i++) characters.Add(Person(i, familyId, Id(91)));
            var buildings = new BuildingRegistry();
            var definitions = new Dictionary<BuildingDefinitionId, BuildingDefinition>();
            AddDefinition(definitions, "stone_age_hut", 4);
            AddDefinition(definitions, "clan_house", 6);
            Complete(buildings, definitions[new BuildingDefinitionId("stone_age_hut")], 10);
            Complete(buildings, definitions[new BuildingDefinitionId("stone_age_hut")], 20);
            var service = Service(characters, buildings, definitions);

            service.ReconcileHousing();
            Assert.That(service.GetHouseholdStatus(familyId), Is.EqualTo(HouseholdHousingStatus.AwaitingFamilyHousing));
            Assert.That(service.HomeAssignments.Values.Distinct().Count(), Is.EqualTo(2));

            var large = Complete(buildings, definitions[new BuildingDefinitionId("clan_house")], 30);
            service.ReconcileHousing();
            Assert.That(service.GetHouseholdStatus(familyId), Is.EqualTo(HouseholdHousingStatus.HousedTogether));
            Assert.That(service.HomeAssignments.Values.Distinct().Single(), Is.EqualTo(large.Id));
        }

        [Test]
        public void ChiefHouseAcceptsWholeLivingRulingDynastyRegardlessOrdinaryCapacity()
        {
            var dynastyId = Id(92);
            var characters = new CharacterRegistry();
            for (var i = 1; i <= 8; i++) characters.Add(Person(i, Id(100 + i), dynastyId));
            var buildings = new BuildingRegistry();
            var definitions = new Dictionary<BuildingDefinitionId, BuildingDefinition>();
            AddDefinition(definitions, "chief_hut", 1);
            var chief = Complete(buildings, definitions[new BuildingDefinitionId("chief_hut")], 10);
            var service = Service(characters, buildings, definitions);

            service.SetRulingDynasty(dynastyId);

            Assert.That(service.HomeAssignments.Count, Is.EqualTo(8));
            Assert.That(service.HomeAssignments.Values.All(x => x == chief.Id), Is.True);
        }

        [Test]
        public void WorkGroupsUseDayAndNightShiftsAndMilitarySquadsAreCapped()
        {
            var characters = new CharacterRegistry();
            for (var i = 1; i <= 52; i++) characters.Add(Person(i, null, null));
            var manager = new WorkManager(characters);
            var day = manager.CreateGroup("Дневная смена", new[] { Id(1) });
            Assert.That(manager.IsOnShift(Id(1), TimeSpan.FromHours(10).Ticks), Is.True);
            Assert.That(manager.IsOnShift(Id(1), TimeSpan.FromHours(22).Ticks), Is.False);
            manager.SetGroupShift(day.Id, WorkShiftKind.Night);
            Assert.That(manager.IsOnShift(Id(1), TimeSpan.FromHours(22).Ticks), Is.True);

            var squad = manager.CreateGroup("Отряд", Enumerable.Range(2, 50).Select(Id), WorkGroupRole.Warriors);
            Assert.That(squad.MemberCount, Is.EqualTo(50));
            Assert.Throws<InvalidOperationException>(() => manager.AddMember(squad.Id, Id(52)));
        }

        [Test]
        public void AlliesOnlyGateAdmitsOwnAndAlliedSidesDetainsNeutralAndRejectsEnemy()
        {
            var definition = new BuildingDefinition(new BuildingDefinitionId("gate"), "Gate",
                BuildingCategory.Infrastructure, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(Array.Empty<ResourceAmount>(), TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)));
            var buildings = new BuildingRegistry();
            var gate = Complete(buildings, definition, 81);
            var pathing = new BuildingPathingService(buildings, new BuildingOccupancyGrid(), _ => definition)
            {
                AreAllied = (owner, traveler) => owner.Value == "player" && traveler.Value == "ally"
            };

            Assert.That(pathing.SetGateAccessMode(gate.Id, GateAccessMode.AlliesOnly), Is.True);
            Assert.That(pathing.EvaluatePassage(gate.Id, Id(82), new CombatSideId("player")),
                Is.EqualTo(GatePassageResult.Allowed));
            Assert.That(pathing.EvaluatePassage(gate.Id, Id(83), new CombatSideId("ally")),
                Is.EqualTo(GatePassageResult.Allowed));
            Assert.That(pathing.EvaluatePassage(gate.Id, Id(84), new CombatSideId("enemy")),
                Is.EqualTo(GatePassageResult.Denied));
            Assert.That(pathing.EvaluatePassage(gate.Id, Id(85), null),
                Is.EqualTo(GatePassageResult.DetainedAwaitingPlayer));
            Assert.That(pathing.DetainedTravelers[Id(85)], Is.EqualTo(gate.Id));
            Assert.That(pathing.ResolveDetention(Id(85), true), Is.EqualTo(GatePassageResult.Allowed));
            Assert.That(pathing.EvaluatePassage(gate.Id, Id(85), null), Is.EqualTo(GatePassageResult.Allowed));
        }

        [Test]
        public void ClinicAssignsOpposingDoctorShiftsAndNightDoctorStartsNightTreatment()
        {
            var characters = new CharacterRegistry();
            var dayDoctor = Person(86, null, null); var nightDoctor = Person(87, null, null);
            var patient = Person(88, null, null);
            characters.Add(dayDoctor); characters.Add(nightDoctor); characters.Add(patient);
            var buildings = new BuildingRegistry();
            var definition = WorkplaceDefinition("healer_hut", BuildingCategory.Medical);
            var clinic = Complete(buildings, definition, 89);
            var health = new HealthAndFamilyService(characters, HealthConditionCatalog.CreateStoneAge(), 7);
            var facilities = new SettlementFacilityService(characters, buildings, _ => definition, health);
            facilities.AssignWorkplace(dayDoctor.Id, clinic.Id);
            facilities.AssignWorkplace(nightDoctor.Id, clinic.Id);
            health.ApplyCondition(patient.Id, new HealthConditionId("minor_cut"), TimeSpan.FromHours(20).Ticks);

            Assert.That(facilities.GetDoctorShift(dayDoctor.Id, clinic.Id), Is.EqualTo(WorkShiftKind.Day));
            Assert.That(facilities.GetDoctorShift(nightDoctor.Id, clinic.Id), Is.EqualTo(WorkShiftKind.Night));
            facilities.AdvanceClinics(TimeSpan.FromHours(22).Ticks);
            Assert.That(facilities.ClinicPatients.Single().InTreatment, Is.True);
        }

        [Test]
        public void OneAdultHorseAcceleratesExactlyOneEligibleResident()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "fish",
                "cooked_meat", "cooked_fish", "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, new BuildingRegistry(), new ResourceTransferService(inventories),
                capabilityProvider: new AlwaysCapable());
            var horse = new AnimalVitalState(Id(93), "horse", 200, 200, true, home: Id(94), bornTick: 0);
            production.RestoreState(new ProductionSaveState(TimeSpan.FromDays(3).Ticks, 0, 0, 0,
                null, null, null, null, null, animals: new[] { horse }));
            var residents = new[] { Id(95), Id(96) };
            production.ReconcileHorseRiders(residents);

            Assert.That(production.GetResidentMovementPerThousand(residents[0]),
                Is.EqualTo(ProductionService.HorseMovementPerThousand));
            Assert.That(production.GetResidentMovementPerThousand(residents[1]), Is.EqualTo(1000));
        }

        [Test]
        public void PermanentWorkplaceAssignsProfessionAndOnlyExplicitReassignmentChangesIt()
        {
            var characters = new CharacterRegistry();
            var resident = Person(60, null, null);
            characters.Add(resident);
            var buildings = new BuildingRegistry();
            var definitions = new Dictionary<BuildingDefinitionId, BuildingDefinition>();
            var garden = WorkplaceDefinition("primitive_garden", BuildingCategory.Food);
            var clinic = WorkplaceDefinition("healer_hut", BuildingCategory.Medical);
            definitions.Add(garden.Id, garden);
            definitions.Add(clinic.Id, clinic);
            var gardenBuilding = Complete(buildings, garden, 61);
            var clinicBuilding = Complete(buildings, clinic, 62);
            var service = Service(characters, buildings, definitions);

            Assert.That(service.AssignWorkplace(resident.Id, gardenBuilding.Id).Succeeded, Is.True);
            Assert.That(resident.ProfessionId?.ToString(), Is.EqualTo("farmer"));
            Assert.That(service.WorkplaceAssignments[resident.Id], Is.EqualTo(gardenBuilding.Id));

            Assert.That(service.ChangeWorkplace(resident.Id, clinicBuilding.Id).Succeeded, Is.True);
            Assert.That(resident.ProfessionId?.ToString(), Is.EqualTo("healer"));
            Assert.That(service.WorkplaceAssignments[resident.Id], Is.EqualTo(clinicBuilding.Id));
        }

        [Test]
        public void TenRankCatalogHasReservedTopAndIncreasingBonuses()
        {
            var catalog = MilitaryTrainingCatalog.CreateStoneAge();
            Assert.That(catalog.All.Count, Is.EqualTo(10));
            Assert.That(catalog.All.First().Rank, Is.EqualTo(MilitaryRank.Private));
            Assert.That(catalog.All.Last().Rank, Is.EqualTo(MilitaryRank.Generalissimo));
            Assert.That(catalog.All.Select(x => x.HealthBonus), Is.Ordered);
            Assert.That(catalog.All.Select(x => x.DamagePerThousand), Is.Ordered);
            Assert.That(catalog.Get(MilitaryRank.Sergeant).MinimumServiceTicks, Is.Zero,
                "A private does not need a service delay before the next course.");
            Assert.That(catalog.Get(MilitaryRank.WarrantOfficer).MinimumServiceTicks, Is.GreaterThan(0),
                "Every promotion after sergeant requires service in the current rank.");
            Assert.That(catalog.Get(MilitaryRank.Lieutenant).RequiredBattleExperience, Is.Zero);
            Assert.That(catalog.Get(MilitaryRank.Captain).RequiredBattleExperience, Is.GreaterThan(0));
            Assert.That(MilitaryRank.Recruit, Is.EqualTo(MilitaryRank.Private));
        }

        [Test]
        public void HuntingPipelineDoesNotDuplicateHideAndFishBreedingConsumesWheat()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "hide", "carcass",
                "dressed_carcass", "raw_hide", "bone", "cured_hide", "meat", "dried_meat", "fish",
                "wheat", "cooked_meat", "cooked_fish", "cooked_berries");
            var recipes = ProductionCatalog.CreateStoneAge(resources);
            var hunt = recipes.Get(new ProductionRecipeId("hunt_animal"));
            Assert.That(hunt.Outputs.Select(x => x.ResourceId.ToString()), Is.EqualTo(new[] { "carcass" }));
            var dress = recipes.Get(new ProductionRecipeId("dress_carcass"));
            Assert.That(dress.Outputs.Select(x => x.ResourceId.ToString()),
                Is.EquivalentTo(new[] { "dressed_carcass", "raw_hide" }));
            var butcher = recipes.Get(new ProductionRecipeId("butcher_carcass"));
            Assert.That(butcher.Outputs.Any(x => x.ResourceId.ToString().Contains("hide")), Is.False);
            Assert.That(recipes.Get(new ProductionRecipeId("fish_breed")).Inputs.Single().ResourceId.ToString(),
                Is.EqualTo("wheat"));
        }

        [Test]
        public void DirectFishingSiteStaysContinuousUntilDayAccessCloses()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "fish",
                "cooked_meat", "cooked_fish", "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var production = new Game.Simulation.Production.ProductionService(
                ProductionCatalog.CreateStoneAge(resources), resources, inventories,
                new BuildingRegistry(), new Game.Simulation.Resources.ResourceTransferService(inventories));
            var site = ProductionSite.Restore(new ProductionSiteState(Id(501),
                new ProductionRecipeId("fish_water"), ProductionSiteKind.NaturalSource,
                default, 20, 20, CropPlotPhase.Unplanted, 0, 0));

            production.AddSite(site);

            Assert.That(site.Continuous, Is.True,
                "One direct fishing command must remain a shift assignment rather than produce one fish.");
        }

        [Test]
        public void HuntingRestorationRevivesOneNearbyDepletedWildPopulationPerInterval()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "hide", "carcass",
                "dressed_carcass", "raw_hide", "bone", "meat", "fish", "cooked_meat", "cooked_fish",
                "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var campDefinition = new BuildingDefinition(new BuildingDefinitionId("hunting_camp"), "Camp",
                BuildingCategory.Production, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(Array.Empty<ResourceAmount>(), TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)));
            var camp = Complete(buildings, campDefinition, 50);
            var production = new Game.Simulation.Production.ProductionService(
                ProductionCatalog.CreateStoneAge(resources), resources, inventories, buildings,
                new Game.Simulation.Resources.ResourceTransferService(inventories));
            var lodge = ProductionSite.CreateBuildingWorkplace(Id(502), camp.Id,
                new ProductionRecipeId("dress_carcass"), default, 1);
            production.AddSite(lodge);
            var animal = ProductionSite.Restore(new ProductionSiteState(Id(503),
                new ProductionRecipeId("hunt_animal"), ProductionSiteKind.NaturalSource,
                new WorldPosition(10, 0, 0), 1, 1, CropPlotPhase.Unplanted, 0, 0));
            production.AddSite(animal, "deer");
            production.Animals.Damage(animal.Id, production.Animals.Policy.Health["deer"]);
            production.Animals.Harvest(animal.Id);
            animal.CompleteWork(ProductionTaskKind.Gather, true);
            Assert.That(production.SetHuntingPolicy(camp.Id, HuntingOperationMode.RestorePopulation, 0), Is.True);

            Assert.That(production.AdvanceHuntingPopulation(TimeSpan.FromDays(3).Ticks), Is.EqualTo(1));
            Assert.That(animal.RemainingAmount, Is.EqualTo(1));
            Assert.That(production.Animals.IsAlive(animal.Id), Is.True);
        }

        [Test]
        public void ArmiesUseSeparateCommanderTenSquadLimitAndTwoArmyLimit()
        {
            var characters = new CharacterRegistry();
            for (var i = 1; i <= 15; i++) characters.Add(Person(i, null, null));
            var service = new MilitaryUnitService(characters, new AlwaysCapable());
            var side = new MilitarySideId("player");
            var squads = Enumerable.Range(1, 12).Select(i =>
                service.Create(side, new[] { Id(i) }, Id(i)).Id).ToArray();

            var first = service.CreateArmy(side, Id(13), squads.Take(10));
            var second = service.CreateArmy(side, Id(14), squads.Skip(10).Take(2));

            Assert.That(first.SquadIds.Count, Is.EqualTo(10));
            Assert.That(second.SquadIds.Count, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => service.CreateArmy(side, Id(15), new[] { squads[0] }));
            Assert.That(service.CaptureState().Armies.Count, Is.EqualTo(2));
        }

        [Test]
        public void PerishableFoodTurnsIntoInedibleSpoilage()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "fish", "spoiled_food",
                "cooked_meat", "cooked_fish", "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, Id(600));
            var stock = new ResourceInventory(owner, resources, 20);
            inventories.Add(stock);
            stock.TryAdd(new ResourceId("fish"), new ResourceQuantity(2));
            var production = new Game.Simulation.Production.ProductionService(
                ProductionCatalog.CreateStoneAge(resources), resources, inventories,
                new BuildingRegistry(), new Game.Simulation.Resources.ResourceTransferService(inventories));

            production.AdvanceTo(TimeSpan.TicksPerDay, Array.Empty<StableEntityId>());

            Assert.That(stock.GetAmount(new ResourceId("fish")).Units, Is.EqualTo(1));
            Assert.That(stock.GetAmount(new ResourceId("spoiled_food")).Units, Is.EqualTo(1));
            Assert.That(production.Food.EdibleResources.Select(x => x.ToString()),
                Does.Not.Contain("spoiled_food"));
        }

        [Test]
        public void PastureWorkerRefillsInternalWaterFromWellAndDeprivationHarmsAnimals()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "animal_feed", "carcass",
                "fish", "meat", "cooked_meat", "cooked_fish", "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var pastureId = Id(701);
            buildings.Add(Building.Restore(new BuildingState(pastureId,
                new BuildingDefinitionId("animal_pasture"), default, BuildingOrientation.North,
                ConstructionState.Completed)));
            buildings.Add(Building.Restore(new BuildingState(Id(702), new BuildingDefinitionId("well"),
                default, BuildingOrientation.North, ConstructionState.Completed)));
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            production.AddSite(ProductionSite.CreateCropPlot(pastureId,
                new ProductionRecipeId("cultivate_berries"), default, TimeSpan.FromDays(2)));
            Assert.That(production.EstablishPasture(pastureId, new CombatSideId("player"), "cow"), Is.True);
            Assert.That(production.Animals.AtHome(pastureId).All(x => !production.CanSlaughterLivestock(x.Id)),
                Is.True, "The founding two males and two females are protected from manual slaughter.");
            var penStock = inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage, pastureId));
            Assert.That(penStock.TryAdd(new ResourceId("animal_feed"), new ResourceQuantity(20)), Is.True);

            production.AdvanceTo(TimeSpan.FromDays(3).Ticks, Array.Empty<StableEntityId>());
            Assert.That(production.LivestockPolicies.Single().WaterUnits, Is.Zero);
            Assert.That(production.Animals.AtHome(pastureId).All(x => x.Health == x.Maximum), Is.True);

            production.BuildingHasAssignedWorker = _ => true;
            Assert.That(production.MaintainLivingBuildingLogistics(new WorkManager(new CharacterRegistry())),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(production.LivestockPolicies.Single().WaterUnits, Is.EqualTo(8));
            var saved = production.CaptureState();
            production.RestoreState(saved);
            Assert.That(production.LivestockPolicies.Single().WaterUnits, Is.EqualTo(8));

            production.BuildingHasAssignedWorker = _ => false;
            production.AdvanceTo(TimeSpan.FromDays(6).Ticks, Array.Empty<StableEntityId>());
            Assert.That(production.Animals.AtHome(pastureId).All(x => x.Health == x.Maximum - 5), Is.True);
        }

        [Test]
        public void BurningCampfireConsumesFuelAfterSixHoursAndProvidesBoundedHeatSource()
        {
            var resources = Catalog("wood_log", "plant_fiber", "berries", "stone", "fish", "meat",
                "cooked_meat", "cooked_fish", "cooked_berries");
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var fireId = Id(710);
            buildings.Add(Building.Restore(new BuildingState(fireId, new BuildingDefinitionId("campfire"),
                default, BuildingOrientation.North, ConstructionState.Completed)));
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            var fuel = new ResourceInventory(production.InputOwner(fireId), resources, 20);
            inventories.Add(fuel);
            fuel.TryAdd(new ResourceId("wood_log"), new ResourceQuantity(10));

            production.AdvanceFireFuel(0, -5);
            Assert.That(production.HeatedBuildings, Does.Contain(fireId));
            production.AdvanceFireFuel(TimeSpan.FromHours(6).Ticks, -5);

            Assert.That(fuel.GetAmount(new ResourceId("wood_log")).Units, Is.EqualTo(9));
            Assert.That(production.HeatedBuildings, Does.Contain(fireId));
        }

        [Test]
        public void CurrentSaveRoundTripPreservesPastureInternalWater()
        {
            var production = new ProductionSaveState(0, 0, 0, 0, null, null, null, null, null,
                livestockPolicies: new[]
                {
                    new LivestockPenPolicyState(Id(720), "sheep", LivestockOperationMode.Balanced,
                        12, TimeSpan.FromDays(2).Ticks, 17)
                });
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(721), new GameTimeState(), 400,
                production: production);
            var codec = new SaveXmlCodec();

            var restored = codec.Deserialize(codec.Serialize(snapshot));

            Assert.That(restored.Production.LivestockPolicies.Single().WaterUnits, Is.EqualTo(17));
        }

        [Test]
        public void WorldPopulationPlanningCompletesWithinTenSeconds()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var plan = new TiledWorldPopulationPlanner(world,
                new TiledWorldHeightSampler(world, 20261023L), 20261023L).Create();
            watch.Stop();

            Assert.That(plan.Resources, Is.Not.Empty);
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                "The synchronous 'Заселяем мир' planner must remain a seconds-scale stage.");
        }

        private static SettlementFacilityService Service(CharacterRegistry characters, BuildingRegistry buildings,
            IReadOnlyDictionary<BuildingDefinitionId, BuildingDefinition> definitions) =>
            new SettlementFacilityService(characters, buildings, id => definitions[id],
                new HealthAndFamilyService(characters, HealthConditionCatalog.CreateStoneAge(), 7));

        private static void AddDefinition(IDictionary<BuildingDefinitionId, BuildingDefinition> definitions,
            string id, int capacity)
        {
            var definition = new BuildingDefinition(new BuildingDefinitionId(id), id, BuildingCategory.Shelter,
                BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(Array.Empty<ResourceAmount>(), TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)), new HousingSpec(capacity));
            definitions.Add(definition.Id, definition);
        }

        private static BuildingDefinition WorkplaceDefinition(string id, BuildingCategory category) =>
            new BuildingDefinition(new BuildingDefinitionId(id), id, category, BuildingEra.StoneAge,
                string.Empty, string.Empty, null,
                new ConstructionSpec(Array.Empty<ResourceAmount>(), TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)), workplace: new WorkplaceSpec(2));

        private static Building Complete(BuildingRegistry registry, BuildingDefinition definition, int coordinate)
        {
            var building = Building.CreateNew(definition, new GridCoordinate(coordinate, 0),
                BuildingOrientation.North, new CombatSideId("player"), Id(1000 + coordinate));
            building.MarkCompleted();
            registry.Add(building);
            return building;
        }

        private static Character Person(int value, StableEntityId? familyId, StableEntityId? dynastyId) =>
            Character.Restore(new CharacterState(Id(value), new CharacterName("Person " + value),
                CharacterSex.Female, 0, CharacterLifeState.Alive, null, null, null, familyId, dynastyId, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static ResourceCatalog Catalog(params string[] ids) => new ResourceCatalog(ids.Select(id =>
            new ResourceDefinition(new ResourceId(id), id,
                id == "berries" || id == "meat" || id == "fish" || id.StartsWith("cooked_") ||
                id == "dried_meat" || id == "wheat" ? ResourceCategory.Food : ResourceCategory.Other)));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
