using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Domain.Campaign;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Resources;
using Game.Simulation.Tiers;
using Game.Simulation.Weather;
using Game.Simulation.World;

namespace Game.Infrastructure
{
    /// <summary>Deterministic normal-session bootstrap for the U26 NPC state.</summary>
    public static class AiRuntimeBootstrap
    {
        public static AiFactionRuntime CreateStarterEnemy(long worldSeed, long calendarTick, long biologicalTick,
            CharacterRegistry characters, HealthAndFamilyService health, TierManager tiers,
            GlobalMapService map, BuildingRegistry buildings, BuildingPlacementService placement,
            ResourceCatalog resources, ProductionCatalog recipes, TechnologyCatalog technologies,
            IEnumerable<BuildingDefinition> definitions, WeatherService weather)
        {
            if (characters == null || health == null || tiers == null || map == null || buildings == null ||
                placement == null || resources == null || recipes == null || technologies == null)
                throw new ArgumentNullException("AI bootstrap dependencies are required.");
            var settlement = map.Settlements.Single(x => x.LocationId == new WorldLocationId("enemy_settlement"));
            var settlementLocation = map.GetLocation(settlement.LocationId);
            // P10.9 uses one physical tiled world, so abstract residents materialize at the canonical
            // settlement coordinates when the player enters that location.
            var offCameraPosition = new WorldPosition(settlementLocation.MapX, 0f, settlementLocation.MapY);
            var side = settlement.OwnerSideId;
            var dynastyId = DeterministicStableId.Create(worldSeed, "enemy_dynasty");
            var residentIds = Enumerable.Range(0, 6).Select(i => DeterministicStableId.Create(worldSeed, "enemy_character", i)).ToArray();
            var names = new[] { "Varek", "Sorin", "Marek", "Tovan", "Ilya", "Boran" };
            for (var i = 0; i < residentIds.Length; i++)
            {
                var character = Character.Restore(new CharacterState(residentIds[i], new CharacterName(names[i]),
                    i == 4 ? CharacterSex.Female : CharacterSex.Male, 0, CharacterLifeState.Alive, null,
                    null, null, dynastyId, dynastyId, i < 4 ? (ProfessionId?)new ProfessionId("worker") : null,
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("steady"), new TraitId("curious") },
                    new[] { new SkillState(new SkillId("combat"), new SkillLevel(35 + i)),
                        new SkillState(new SkillId("research"), new SkillLevel(25 + i)) },
                    new CharacterAttributes(new CharacterAttributeValue(45 + i), new CharacterAttributeValue(55 - i)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(i == 0 ? 100 : 10 - i), null));
                characters.Add(character);
                tiers.Materialize(character, CharacterSimulationTier.Tier3,
                    new CharacterRuntimeState(character.Id, offCameraPosition, default, false, settlement.LocationId.Value,
                        calendarTick, biologicalTick));
            }

            var dynasty = DynastyState.Create(dynastyId, "House Varek", residentIds[0], residentIds[0],
                residentIds);
            var succession = new SuccessionService(characters, dynasty, new PlayerRealmState(residentIds[0]),
                new GameRunState());
            var inventories = new ResourceInventoryRegistry();
            foreach (var id in residentIds)
                inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, id),
                    resources, 128));
            Seed(inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, residentIds[0])), "berries", 40);
            Seed(inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, residentIds[0])), "wood_log", 36);
            Seed(inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, residentIds[0])), "plant_fiber", 24);
            Seed(inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, residentIds[0])), "stone", 20);

            var research = new ResearchService(technologies, new CharacterResearchRateProvider(characters, health));
            var transfers = new ResourceTransferService(inventories);
            var worldPiles = new WorldPileService(resources, inventories);
            var production = new ProductionService(recipes, resources, inventories, buildings, transfers,
                researchUnlocks: research, capabilityProvider: health, weatherModifierProvider: weather);
            AddSource(production, worldSeed, "gather_berries", 0, 160, new WorldPosition(22, 0, 18));
            AddSource(production, worldSeed, "gather_fiber", 1, 120, new WorldPosition(24, 0, 18));
            AddSource(production, worldSeed, "chop_wood", 2, 160, new WorldPosition(26, 0, 18));
            AddSource(production, worldSeed, "gather_stone", 3, 120, new WorldPosition(28, 0, 18));
            var construction = new ConstructionFundingService(placement, inventories, resources,
                new StorageService(inventories, resources), research);
            var state = AiStrategicState.Create(new AiStateId(DeterministicStableId.Create(worldSeed, "enemy_state")),
                "enemy_state", side, settlement.Id, residentIds[0], calendarTick);
            map.SynchronizeSettlement(settlement.Id, settlement.LocationId, side, settlement.BuildingIds, residentIds);
            return new AiFactionRuntime(state, dynasty, succession, research, production, inventories, worldPiles,
                construction, definitions, residentIds);
        }

        private static void AddSource(ProductionService production, long seed, string recipe, int index,
            long amount, WorldPosition position) => production.AddSite(ProductionSite.Restore(
                new ProductionSiteState(DeterministicStableId.Create(seed, "enemy_source", index), new ProductionRecipeId(recipe),
                    ProductionSiteKind.NaturalSource, position, amount, amount, CropPlotPhase.Unplanted, 0, 0)));

        private static void Seed(ResourceInventory inventory, string resourceId, long amount)
        {
            if (!inventory.TryAdd(new ResourceId(resourceId), new ResourceQuantity(amount)))
                throw new InvalidOperationException("AI bootstrap inventory capacity is invalid.");
        }

    }
}
