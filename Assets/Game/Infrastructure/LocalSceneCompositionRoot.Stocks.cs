using System;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Production;

namespace Game.Infrastructure
{
    public sealed partial class LocalSceneCompositionRoot
    {
        private long constructionStockTick = -1;
        private System.Collections.Generic.Dictionary<ResourceId,long> constructionStockTargets = new();
        private long ConstructionStockTarget(ResourceId id)
        {
            if (constructionStockTick != Clock.State.CalendarTicks)
            {
                var needs = new System.Collections.Generic.Dictionary<ResourceId,long>();
                var available = new System.Collections.Generic.Dictionary<ResourceId,long>();
                foreach(var building in Buildings.All.Where(x => x.OwnerSideId == new CombatSideId("player") && !x.IsDestroyed &&
                    !x.IsOperational && x.ConstructionPhase != ConstructionPhase.Cancelled))
                    foreach(var material in building.ConstructionMaterials) StockPlanner.Add(needs,material.ResourceId,material.RemainingUnits);
                foreach(var inventory in Inventories.All.Where(x =>
                    (x.Owner.Kind != InventoryOwnerKind.Character && x.Owner.Kind != InventoryOwnerKind.CarryLoad || CanCommandResident(x.Owner.Id)) &&
                    (!Buildings.TryGet(x.Owner.Id,out var owner) || owner.OwnerSideId == new CombatSideId("player"))))
                    foreach(var amount in inventory.CaptureState().Amounts) StockPlanner.Add(available,amount.ResourceId,amount.Quantity.Units);
                constructionStockTargets = Production.CalculateConstructionRequirements(needs,available);
                constructionStockTick = Clock.State.CalendarTicks;
            }
            return StockPlanner.Get(constructionStockTargets,id);
        }
        private StockPlanningFacts CaptureStockPlanningFacts()
        {
            var living = Characters.GetAll().Where(x => CanCommandResident(x.Id) && x.LifeState == CharacterLifeState.Alive)
                .Select(x => x.Id).ToHashSet();
            var facts = new StockPlanningFacts { Residents = living.Count, Day = Clock.State.CalendarTicks / TimeSpan.TicksPerDay };
            foreach (var inventory in Inventories.All)
            {
                if (inventory.Owner.Kind == InventoryOwnerKind.Character && !living.Contains(inventory.Owner.Id)) continue;
                if (Buildings.TryGet(inventory.Owner.Id, out var owner) && owner.OwnerSideId != new CombatSideId("player")) continue;
                if (inventory.Owner.Kind == InventoryOwnerKind.BuildingStorage) facts.Capacity += inventory.Capacity;
                foreach (var resource in Resources.All.Where(x => x.Id.ToString() != "water"))
                    StockPlanner.Add(facts.Available, resource.Id, inventory.GetAmount(resource.Id).Units);
            }
            // Material already installed is absent from both remaining bill and inventories. Materials delivered
            // to the construction owner remain in Available, so they are subtracted exactly once by the planner.
            foreach (var building in Buildings.All.Where(x => x.OwnerSideId == new CombatSideId("player") &&
                         !x.IsDestroyed && x.ConstructionPhase != ConstructionPhase.Cancelled && !x.IsOperational))
                foreach (var material in building.ConstructionMaterials)
                    StockPlanner.Add(facts.OneTimeNeeds, material.ResourceId, material.RemainingUnits);
            int Members(WorkGroupRole role) => Work.Groups.All.Where(x => x.Role == role)
                .SelectMany(x => x.Members).Where(living.Contains).Distinct().Count();
            void Need(string id, long n) => StockPlanner.Add(facts.OneTimeNeeds, new ResourceId(id), n);
            var hunters = Members(WorkGroupRole.Hunters);
            var warriors = Members(WorkGroupRole.Warriors);
            Need("combat_bow", hunters);
            Need("arrows", checked(hunters * Production.StockConfig.arrowsPerHunter));
            Need("stone_spear", warriors); Need("wooden_shield", warriors);
            Need("leather_clothing", living.Count);
            // Character inventories are included above: carried equipment reduces the total need; appearance
            // clothing is cosmetic and cannot be counted as a physical resource.
            var cases = HealthFamily.Conditions.Count(x => living.Contains(x.TargetId) &&
                x.DefinitionId.ToString() != "encouraged" && x.DefinitionId.ToString() != "distressed" &&
                x.DefinitionId.ToString() != "starvation" && x.DefinitionId.ToString() != "focused_work" &&
                (x.State == HealthConditionState.Active || x.State == HealthConditionState.Improving ||
                 x.State == HealthConditionState.Worsening || x.State == HealthConditionState.Critical));
            Need("medicine", checked(cases * Production.StockConfig.medicinesPerCase +
                (living.Count + Production.StockConfig.residentsPerReserveMedicine - 1) / Production.StockConfig.residentsPerReserveMedicine));
            var feed = Production.Animals.States.Where(x => x.Domestic && x.Alive && !x.Harvested &&
                    Buildings.TryGet(x.Home, out var pen) && pen.OwnerSideId == new CombatSideId("player"))
                .GroupBy(x => x.Home).Sum(x => (x.Count() + 2L) / 3);
            facts.DailyNeeds[new ResourceId("animal_feed")] = feed;
            var fuel = 0L;
            foreach(var building in Buildings.All.Where(x => x.IsOperational && x.OwnerSideId == new CombatSideId("player")))
            {
                var rule = Production.StockConfig.heating.FirstOrDefault(x => x.building == building.DefinitionId.ToString()) ??
                    Production.StockConfig.heating.FirstOrDefault(x => x.building == building.DefinitionId.FamilyId.ToString());
                if(rule != null) fuel += rule.logsPerDay;
            }
            facts.DailyNeeds[new ResourceId("wood_log")] = fuel;
            return facts;
        }
    }
}
