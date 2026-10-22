using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.Buildings;
using Game.Simulation.Buildings;
using Game.Simulation.Health;
using Game.Simulation.Resources;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Editor
{
    [Category("P10.9")]
    public sealed class P109ExtendedBuildingsTests
    {
        [Test]
        public void CatalogAuthorsEveryEFacilityWithCapabilitiesAndRealWaterOutput()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var ids = new[] { "clan_house", "healer_hut", "shrine", "chief_hut", "meeting_area",
                "training_ground", "fishing_camp", "gathering_area", "primitive_garden", "well",
                "palisade", "gate", "watchtower", "primitive_path" };
            CollectionAssert.IsSubsetOf(ids, catalog.Entries.Select(x => x.DefinitionId).ToArray());
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("clan_house")).Housing.ResidentCapacity,
                Is.EqualTo(10));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("healer_hut")).Workplace.MaximumWorkers,
                Is.EqualTo(2));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("fishing_camp")).Placement.Constraints
                .HasFlag(BuildingPlacementConstraint.Shoreline), Is.True);
            var well = catalog.ResolveDefinition(new BuildingDefinitionId("well"));
            Assert.That(well.Placement.Constraints.HasFlag(BuildingPlacementConstraint.Groundwater), Is.True);
            Assert.That(well.Production.RecipeIds.Single().ToString(), Is.EqualTo("draw_water"));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("primitive_path")).AllowsOverlap, Is.True);
            var hut = catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut"));
            Assert.That(hut.Upgrades.Options.Single().TargetDefinitionId.ToString(), Is.EqualTo("clan_house"));
            Assert.That(ids.All(id => catalog.ResolveDefinition(new BuildingDefinitionId(id)).Maintenance != null), Is.True);
        }

        [Test]
        public void FacilityAssignmentsAreExclusiveCapacityBoundAndDriveMedicineAndTraining()
        {
            var characters = new CharacterRegistry();
            var healer = Person(1, "Healer"); var patient = Person(2, "Patient"); var trainee = Person(3, "Trainee");
            characters.Add(healer); characters.Add(patient); characters.Add(trainee);
            var buildings = new BuildingRegistry();
            var definitions = new[]
            {
                Definition("healer_hut", housing: 1, workers: 1),
                Definition("training_ground", workers: 1),
                Definition("clan_house", housing: 2)
            }.ToDictionary(x => x.Id);
            var healerHut = Complete(buildings, definitions[new BuildingDefinitionId("healer_hut")], 10);
            var training = Complete(buildings, definitions[new BuildingDefinitionId("training_ground")], 20);
            var clan = Complete(buildings, definitions[new BuildingDefinitionId("clan_house")], 30);
            var health = new HealthAndFamilyService(characters, HealthConditionCatalog.CreateStoneAge(), 42);
            var service = new SettlementFacilityService(characters, buildings, id => definitions[id], health);

            Assert.That(service.AssignHome(healer.Id, healerHut.Id).Succeeded, Is.True);
            Assert.That(service.AssignHome(patient.Id, healerHut.Id).Failure, Is.EqualTo(FacilityAssignmentFailure.CapacityReached));
            Assert.That(service.AssignHome(healer.Id, clan.Id).Failure, Is.EqualTo(FacilityAssignmentFailure.CharacterAlreadyAssigned));
            Assert.That(service.AssignWorkplace(healer.Id, healerHut.Id).Succeeded, Is.True);
            Assert.That(service.AssignWorkplace(patient.Id, healerHut.Id).Failure, Is.EqualTo(FacilityAssignmentFailure.CapacityReached));
            health.ApplyCondition(patient.Id, new HealthConditionId("minor_cut"), 0);
            Assert.That(service.Treat(healer.Id, patient.Id), Is.EqualTo(1));
            Assert.That(CharacterSkillRules.Level(healer, CharacterSkillRules.Medicine), Is.EqualTo(1));
            Assert.That(service.AssignWorkplace(trainee.Id, training.Id).Succeeded, Is.True);
            Assert.That(service.Train(trainee.Id), Is.True);
            Assert.That(CharacterSkillRules.Level(trainee, CharacterSkillRules.Combat), Is.EqualTo(1));
        }

        [Test]
        public void GateOpeningAndDestructionUpdateTheSharedOccupancyGrid()
        {
            var gate = Definition("gate", maintenance: true);
            var registry = new BuildingRegistry(); var grid = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(registry, grid, new BuildingGridBounds(-10, -10, 10, 10));
            var building = placement.Confirm(gate, new GridCoordinate(0, 0), BuildingOrientation.North);
            building.MarkCompleted();
            var pathing = new BuildingPathingService(registry, grid, _ => gate);
            Assert.That(grid.IsOccupied(new GridCoordinate(0, 0)), Is.True);
            Assert.That(pathing.SetGateOpen(building.Id, true), Is.True);
            Assert.That(grid.IsOccupied(new GridCoordinate(0, 0)), Is.False);
            Assert.That(pathing.SetGateOpen(building.Id, false), Is.True);
            Assert.That(grid.IsOccupied(new GridCoordinate(0, 0)), Is.True);
            building.ApplyDamage(Id(99), building.MaxIntegrity, BuildingDamageType.Generic, 1);
            pathing.Reconcile(building);
            Assert.That(grid.IsOccupied(new GridCoordinate(0, 0)), Is.False);
        }

        [Test]
        public void WearRepairUpgradeAndDemolitionConserveResourcesExactlyOnce()
        {
            var wood = new ResourceId("wood_log"); var stone = new ResourceId("stone");
            var resourceCatalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(stone, "Stone", ResourceCategory.Construction)
            });
            var upgraded = Definition("clan_house", maintenance: true, construction: Amount(wood, 20));
            var source = Definition("stone_age_hut", maintenance: true, construction: Amount(wood, 10),
                upgrades: new UpgradeSpec(new[] { new BuildingUpgradeOption(upgraded.Id,
                    new[] { Amount(stone, 3) }, TimeSpan.FromHours(2)) }));
            var definitions = new[] { source, upgraded }.ToDictionary(x => x.Id);
            var registry = new BuildingRegistry(); var occupancy = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(registry, occupancy, new BuildingGridBounds(-10, -10, 10, 10));
            var building = placement.Confirm(source, new GridCoordinate(0, 0), BuildingOrientation.North);
            building.MarkCompleted();
            var inventories = new ResourceInventoryRegistry(); var piles = new WorldPileService(resourceCatalog, inventories);
            var funds = piles.Create(wood, new ResourceQuantity(20), new GridCoordinate(2, 0));
            inventories.Get(funds.InventoryOwner).TryAdd(stone, new ResourceQuantity(3));
            var storage = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id),
                resourceCatalog, 20);
            storage.TryAdd(stone, new ResourceQuantity(2)); inventories.Add(storage);
            var damage = new BuildingDamageService(registry, definitions.Values, occupancyGrid: occupancy);
            var lifecycle = new BuildingLifecycleService(registry, id => definitions[id], damage, inventories, piles,
                occupancy);

            Assert.That(lifecycle.AdvanceWearTo(TimeSpan.FromHours(24).Ticks), Is.EqualTo(1));
            Assert.That(lifecycle.AdvanceWearTo(TimeSpan.FromHours(24).Ticks), Is.EqualTo(0));
            Assert.That(building.CurrentIntegrity, Is.EqualTo(995));
            var repairId = Id(100);
            Assert.That(lifecycle.Repair(building.Id, repairId, TimeSpan.FromMinutes(30), 2,
                new[] { funds.InventoryOwner }).Succeeded, Is.True);
            Assert.That(inventories.Get(funds.InventoryOwner).GetAmount(wood).Units, Is.EqualTo(18));
            Assert.That(lifecycle.Repair(building.Id, repairId, TimeSpan.FromMinutes(30), 3,
                new[] { funds.InventoryOwner }).Failure, Is.EqualTo(BuildingLifecycleFailure.DuplicateOperation));
            Assert.That(inventories.Get(funds.InventoryOwner).GetAmount(wood).Units, Is.EqualTo(18));
            var stableId = building.Id;
            Assert.That(lifecycle.Upgrade(building.Id, upgraded.Id, Id(101), TimeSpan.FromHours(2), 4,
                new[] { funds.InventoryOwner }).Succeeded, Is.True);
            Assert.That(building.Id, Is.EqualTo(stableId));
            Assert.That(building.DefinitionId, Is.EqualTo(upgraded.Id));
            Assert.That(inventories.Get(funds.InventoryOwner).GetAmount(stone).Units, Is.EqualTo(0));
            Assert.That(lifecycle.Demolish(building.Id, 5).Succeeded, Is.True);
            Assert.That(registry.TryGet(stableId, out _), Is.False);
            Assert.That(occupancy.IsOccupied(new GridCoordinate(0, 0)), Is.False);
            Assert.That(inventories.All.Sum(x => x.GetAmount(stone).Units), Is.EqualTo(2), "Stored cargo must be spilled.");
            Assert.That(inventories.All.Sum(x => x.GetAmount(wood).Units), Is.EqualTo(28), "Half of upgrade target construction cost is returned once.");
        }

        private static BuildingDefinition Definition(string id, int housing = 0, int workers = 0,
            bool maintenance = false, ResourceAmount? construction = null, UpgradeSpec upgrades = null)
        {
            var cost = construction.HasValue ? new[] { construction.Value } : Array.Empty<ResourceAmount>();
            return new BuildingDefinition(new BuildingDefinitionId(id), id, BuildingCategory.Structure,
                BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(cost, TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)),
                housing > 0 ? new HousingSpec(housing) : null, null,
                workers > 0 ? new WorkplaceSpec(workers) : null, null,
                maintenance ? new MaintenanceSpec(TimeSpan.FromHours(24).Ticks, 5,
                    new[] { Amount(new ResourceId("wood_log"), 2) }, TimeSpan.FromMinutes(30),
                    demolitionReturnPerThousand: 500) : null,
                upgrades);
        }

        private static Building Complete(BuildingRegistry registry, BuildingDefinition definition, int coordinate)
        {
            var building = Building.CreateNew(definition, new GridCoordinate(coordinate, 0),
                BuildingOrientation.North, new CombatSideId("player"));
            building.MarkCompleted(); registry.Add(building); return building;
        }

        private static Character Person(int id, string name) => Character.Restore(new CharacterState(Id(id),
            new CharacterName(name), CharacterSex.Female, 0, CharacterLifeState.Alive, null, null, null, null, null,
            null, new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static ResourceAmount Amount(ResourceId id, long units) =>
            new ResourceAmount(id, new ResourceQuantity(units));
        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
