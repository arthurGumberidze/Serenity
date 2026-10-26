using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class WM15MorningFeedbackTests
    {
        private static StableEntityId Id(int value)=>StableEntityId.Parse(value.ToString("x32"));
        [Test]
        public void RebindingSharedSourcesReplacesAutomaticSelectionCache()
        {
            var resources=new ResourceCatalog(new[]{"wood_log","stone","plant_fiber","berries","fish"}.Select(
                x=>new ResourceDefinition(new ResourceId(x),x,ResourceCategory.Food)));
            var inventories=new ResourceInventoryRegistry();var worker=Id(90);
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,worker),resources,32));
            var service=new ProductionService(ProductionCatalog.CreateStoneAge(resources),resources,inventories,
                new BuildingRegistry(),new ResourceTransferService(inventories));
            var old=ProductionSite.CreateNatural(new ProductionRecipeId("gather_berries"),default,20);
            var actual=ProductionSite.CreateNatural(new ProductionRecipeId("gather_berries"),new WorldPosition(3,0,0),20);
            service.AddSite(old);service.BindSharedNaturalSites(new[]{actual});
            Assert.That(service.TryReserve(worker,default,out var job),Is.True);
            Assert.That(job.Task.SiteId,Is.EqualTo(actual.Id));
            service.AdvanceTask(job.Task.Id,TimeSpan.FromDays(1));
            Assert.That(actual.RemainingAmount,Is.LessThan(20));
            Assert.That(old.RemainingAmount,Is.EqualTo(20));
        }

        [Test]
        public void FishingCampCapsAtThirtyResumesAfterTransferAndPersistsContinuousWork()
        {
            var fish=new ResourceId("fish");
            var resources=new ResourceCatalog(new[]{"wood_log","stone","plant_fiber","berries","fish"}.Select(
                x=>new ResourceDefinition(new ResourceId(x),x,x=="fish" || x=="berries"?ResourceCategory.Food:ResourceCategory.Construction)));
            var inventories=new ResourceInventoryRegistry();var buildings=new BuildingRegistry();
            var definition=new BuildingDefinition(new BuildingDefinitionId("fishing_camp"),"Fishing",BuildingCategory.Food,
                BuildingEra.StoneAge,"","",null,new ConstructionSpec(null,TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(8,6)),workplace:new WorkplaceSpec(1),
                production:new ProductionSpec(new[]{new ProductionRecipeId("fish_camp")},1,30));
            var building=Building.Restore(new BuildingState(Id(1),definition.Id,new GridCoordinate(0,0),BuildingOrientation.North,ConstructionState.Completed));
            buildings.Add(building);
            var worker=Id(2);inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,worker),resources,10));
            var transfers=new ResourceTransferService(inventories);
            var production=new ProductionService(ProductionCatalog.CreateStoneAge(resources),resources,inventories,buildings,transfers);
            production.RegisterBuildingWorkplace(building.Id,definition,new WorldPosition(0,0,-4));
            Assert.That(production.TryReserveAtBuilding(worker,building.Id,out var job),Is.True);
            production.AdvanceTask(job.Task.Id,TimeSpan.FromDays(1));
            var output=inventories.Get(production.OutputOwner(building.Id));
            Assert.That(output.GetAmount(fish).Units,Is.EqualTo(30));
            Assert.That(job.Task.IsTerminal,Is.False);
            production.AdvanceTask(job.Task.Id,TimeSpan.FromDays(1));
            Assert.That(output.TotalUnits,Is.EqualTo(30));
            Assert.That(output.TryRemove(fish,new ResourceQuantity(7)),Is.True);
            production.AdvanceTask(job.Task.Id,TimeSpan.FromHours(1));
            Assert.That(output.TotalUnits,Is.EqualTo(30));
            production.SetWorkplacePaused(building.Id,new ProductionRecipeId("fish_camp"),true);
            output.TryRemove(fish,new ResourceQuantity(1));
            production.AdvanceTask(job.Task.Id,TimeSpan.FromHours(1));
            Assert.That(output.TotalUnits,Is.EqualTo(29));
            var snapshot=new SaveSnapshot(SaveSnapshot.CurrentVersion,Id(99),new GameTimeState(),400,production.CaptureState());
            var decoded=new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            production.RestoreState(decoded.Production);
            Assert.That(production.Sites.Single().Continuous,Is.True);
            Assert.That(production.Sites.Single().Paused,Is.True);
        }

        [Test]
        public void GateCutsOnlyItsThreeWallCellsAndFailedTransactionRestoresWall()
        {
            var registry=new BuildingRegistry();var occupancy=new BuildingOccupancyGrid();
            var placement=new BuildingPlacementService(registry,occupancy,new BuildingGridBounds(-50,-50,50,50));
            var wallDefinition=new BuildingDefinition(new BuildingDefinitionId("palisade"),"Wall",BuildingCategory.Military,new BuildingFootprint(9,1));
            var wall=placement.Confirm(wallDefinition,new GridCoordinate(0,0),BuildingOrientation.North);wall.MarkCompleted();
            var gateDefinition=new BuildingDefinition(new BuildingDefinitionId("gate"),"Gate",BuildingCategory.Military,new BuildingFootprint(3,1));
            Assert.That(placement.Evaluate(gateDefinition,new GridCoordinate(3,0),BuildingOrientation.North).IsValid,Is.True);
            var gate=placement.Confirm(gateDefinition,new GridCoordinate(3,0),BuildingOrientation.North);
            Assert.That(wall.PlacementCells.Select(x=>x.X),Is.EqualTo(new[]{0,1,2,6,7,8}));
            Assert.That(occupancy.OccupiedCellCount,Is.EqualTo(9));
            var saved=Building.Restore(wall.CaptureState());
            Assert.That(saved.PlacementCells.Count,Is.EqualTo(6));
            placement.Rollback(gate,gateDefinition);
            Assert.That(wall.PlacementCells.Count,Is.EqualTo(9));
            Assert.That(occupancy.OccupiedCellCount,Is.EqualTo(9));
        }

        [Test]
        public void EscapeVariesBetweenHitsPreservesTierStateAndPrefersAwayFromThreat()
        {
            var animal=new WildlifeIndividualState(Id(7),"deer",WildlifeSex.Female,0,"home",default,CharacterSimulationTier.Tier1);
            var policy=new WildlifeEscapePolicy(4,20,TimeSpan.FromMinutes(6).Ticks);
            var hit=animal.Escape(new WorldPosition(-5,0,0),1000);
            var candidates=WildlifeEscape.Candidates(hit,policy,0).ToArray();
            Assert.That(candidates.Take(8).All(x=>x.X>0),Is.True);
            Assert.That(WildlifeEscape.Candidates(hit.Escape(new WorldPosition(-5,0,0),1200),policy,0),Is.Not.EqualTo(candidates));
            var far=hit.InTier(CharacterSimulationTier.Tier3).At("home",new WorldPosition(2,0,1));
            Assert.That(far.IsFleeing(999),Is.True);Assert.That(far.IsFleeing(1000),Is.False);
            Assert.That(far.FleeSequence,Is.EqualTo(hit.FleeSequence));
            var registry=new AnimalHealthRegistry();registry.Register(animal.Id,"deer");var events=0;
            registry.Damaged+=_=>events++;
            registry.Damage(animal.Id,10);registry.Damage(animal.Id,999);registry.Damage(animal.Id,10);
            Assert.That(events,Is.EqualTo(2));Assert.That(registry.IsAlive(animal.Id),Is.False);
        }

        [Test]
        public void AutomaticFishHaulingUsesEntrancesAndReservesOnlyRealStock()
        {
            var fish=new ResourceId("fish");
            var resources=new ResourceCatalog(new[]{new ResourceDefinition(fish,"Fish",ResourceCategory.Food)});
            var inventories=new ResourceInventoryRegistry();var buildings=new BuildingRegistry();
            foreach(var id in new[]{Id(40),Id(41)}) buildings.Add(Building.Restore(new BuildingState(id,
                new BuildingDefinitionId(id==Id(40)?"fishing_camp":"storage_basket"),new GridCoordinate(0,0),
                BuildingOrientation.North,ConstructionState.Completed)));
            var output=new ResourceInventory(new InventoryOwner(InventoryOwnerKind.ProductionOutput,Id(40)),resources,30);
            var storage=new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,Id(41)),resources,100);
            inventories.Add(output);inventories.Add(storage);output.TryAdd(fish,new ResourceQuantity(30));
            foreach(var id in new[]{Id(42),Id(43)})inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.CarryLoad,id),resources,20));
            var claims=new HaulClaimRegistry();
            var query=new HaulWorldQuery(inventories,new WorldPileService(resources,inventories),buildings,claims);
            query.PositionOverride=owner=>owner.Id==Id(40)?new WorldPosition(1,0,-5):new WorldPosition(20,0,-2);
            Assert.That(query.TryClaim(new Tier1AiAgentState(Id(42),default),20,out var first,out var claim),Is.True);
            Assert.That(first.SourcePosition,Is.EqualTo(new WorldPosition(1,0,-5)));
            Assert.That(first.DestinationPosition,Is.EqualTo(new WorldPosition(20,0,-2)));
            Assert.That(query.TryClaim(new Tier1AiAgentState(Id(43),default),20,out var second,out var otherClaim),Is.True);
            Assert.That(first.Quantity.Units+second.Quantity.Units,Is.EqualTo(30));
            claims.Release(claim);claims.Release(otherClaim);
            Assert.That(output.TotalUnits,Is.EqualTo(30));Assert.That(storage.TotalUnits,Is.Zero);
        }

        [Test]
        public void AbstractWorkerFollowsApprovedRouteAndCannotTeleportAcrossRejectedWater()
        {
            var movement=new OffCameraWorkMovement(Id(3),default,3,(from,to)=>to.X<0?null:new[]{new WorldPosition(0,0,3),to});
            movement.MoveTo(new WorldPosition(3,0,3),0);
            movement.Advance(TimeSpan.FromSeconds(60));
            Assert.That(movement.CurrentPosition,Is.EqualTo(new WorldPosition(0,0,3)));
            movement.Advance(TimeSpan.FromSeconds(60));
            Assert.That(movement.State,Is.EqualTo(MovementState.Arrived));
            movement.MoveTo(new WorldPosition(-10,0,0),0);
            movement.Advance(TimeSpan.FromHours(2));
            Assert.That(movement.State,Is.EqualTo(MovementState.Failed));
            Assert.That(movement.CurrentPosition,Is.EqualTo(new WorldPosition(3,0,3)));
        }
    }
}


