using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.AI;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P10.9")]
    public sealed class P109FoodIntegrationTests
    {
        [Test]
        public void AggregateConsumptionUsesOnlyCookedFoodAndChargesExactlyOnce()
        {
            var fixture = new Fixture(cookedUnits: 2);
            fixture.Production.Food.Advance(TimeSpan.FromDays(15), 2);

            Assert.That(fixture.Storage.GetAmount(fixture.Raw).Units, Is.EqualTo(5));
            Assert.That(fixture.Storage.GetAmount(fixture.Cooked).Units, Is.Zero);
            Assert.That(fixture.Production.Food.TotalConsumed, Is.EqualTo(2));
            Assert.That(fixture.Production.Food.TotalShortage, Is.Zero);
        }

        [Test]
        public void Tier1EatConsumesOneCookedMealBeforeRecoveringHunger()
        {
            var fixture = new Fixture(cookedUnits: 2);
            var agent = fixture.AddHungryTier1();
            for (var i = 0; i < 6; i++)
                fixture.Scheduler.Advance(new Game.Simulation.Time.GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.Zero));

            Assert.That(agent.Needs.Hunger, Is.LessThanOrEqualTo(Tier1AiScheduler.CriticalHungerCompletion));
            Assert.That(fixture.Storage.GetAmount(fixture.Cooked).Units, Is.EqualTo(1));
            Assert.That(fixture.Storage.GetAmount(fixture.Raw).Units, Is.EqualTo(5));
            Assert.That(fixture.Production.Food.TotalConsumed, Is.EqualTo(1));
        }

        [Test]
        public void Tier1CannotRecoverHungerFromRawFoodWhenCookedFoodIsMissing()
        {
            var fixture = new Fixture(cookedUnits: 0);
            var agent = fixture.AddHungryTier1();
            for (var i = 0; i < 4; i++)
                fixture.Scheduler.Advance(new Game.Simulation.Time.GameTimeAdvance(TimeSpan.FromMinutes(1), TimeSpan.Zero));

            Assert.That(agent.Needs.Hunger, Is.EqualTo(0.95d));
            Assert.That(fixture.Storage.GetAmount(fixture.Raw).Units, Is.EqualTo(5));
            Assert.That(fixture.Production.Food.TotalConsumed, Is.Zero);
        }

        private sealed class Fixture
        {
            public readonly ResourceId Raw = new ResourceId("berries");
            public readonly ResourceId Cooked = new ResourceId("cooked_berries");
            public readonly ResourceCatalog Resources;
            public readonly ResourceInventoryRegistry Inventories = new ResourceInventoryRegistry();
            public readonly BuildingRegistry Buildings = new BuildingRegistry();
            public readonly ResourceInventory Storage;
            public readonly ProductionService Production;
            public readonly CharacterRegistry Characters = new CharacterRegistry();
            public readonly Tier1AiScheduler Scheduler;

            public Fixture(long cookedUnits)
            {
                Resources = new ResourceCatalog(new[]
                {
                    Definition("wood_log", ResourceCategory.Construction),
                    Definition("stone", ResourceCategory.Construction),
                    Definition("plant_fiber", ResourceCategory.Construction),
                    Definition("berries", ResourceCategory.Food),
                    Definition("cooked_berries", ResourceCategory.Food)
                });
                var storageBuilding = Building.CreateNew(new BuildingDefinitionId("storage_basket"),
                    new GridCoordinate(2, 0), BuildingOrientation.North);
                storageBuilding.MarkCompleted();
                Buildings.Add(storageBuilding);
                Storage = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                    storageBuilding.Id), Resources, 32);
                Storage.TryAdd(Raw, new ResourceQuantity(5));
                if (cookedUnits > 0) Storage.TryAdd(Cooked, new ResourceQuantity(cookedUnits));
                Inventories.Add(Storage);
                var transfers = new ResourceTransferService(Inventories);
                Production = new ProductionService(ProductionCatalog.CreateStoneAge(Resources), Resources,
                    Inventories, Buildings, transfers, new ProductionSettings(TimeSpan.FromDays(1),
                        TimeSpan.FromDays(2), TimeSpan.FromDays(15), Array.Empty<ResourceStockTarget>()));
                var piles = new WorldPileService(Resources, Inventories);
                var claims = new HaulClaimRegistry();
                var world = new Tier1AiWorld(new HaulWorldQuery(Inventories, piles, Buildings, claims), claims,
                    transfers, Production, buildings: Buildings);
                Scheduler = new Tier1AiScheduler(Characters, new Tier1AiAgentRegistry(), world,
                    new Tier1AiSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30),
                        1, 1, 1, 1, 2f, 4, 0.1d, 0d, 0d, 60d));
            }

            public Tier1AiAgentState AddHungryTier1()
            {
                var character = Character.CreateNew(new CharacterCreationData(new CharacterName("Hungry"),
                    CharacterSex.Female, 0, null, null, null, null, null,
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                    null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
                Characters.Add(character);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, character.Id),
                    Resources, 4));
                var movement = new Movement(character.Id);
                var agent = new Tier1AiAgentState(character.Id, default, new Tier1Needs(0.95d, 1d));
                Scheduler.Register(character, agent, movement);
                return agent;
            }

            private static ResourceDefinition Definition(string id, ResourceCategory category) =>
                new ResourceDefinition(new ResourceId(id), id, category);
        }

        private sealed class Movement : ITier1MovementDriver
        {
            public Movement(StableEntityId id) { CharacterId = id; }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition => default;
            public MovementState State { get; private set; }
            public bool IsAvailable => true;
            public void MoveTo(WorldPosition destination, float stoppingDistance) => State = MovementState.Moving;
            public void Stop() => State = MovementState.Idle;
        }
    }
}
