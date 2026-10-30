using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    /// <summary>One faction adapter uses the normal work/production/haul services in every representation tier.</summary>
    public sealed class NeighbourEconomyRuntime
    {
        private readonly LocalSceneCompositionRoot root;
        private readonly AiFactionRuntime faction;
        private readonly float grid;
        private readonly Vector3 origin;
        private readonly ConstructionSiteService sites;
        private readonly ConstructionClearanceCoordinator clearance;
        private readonly Tier1AiWorld aiWorld;
        private readonly Dictionary<StableEntityId,OffCameraWorkMovement> abstractMoves = new();
        private readonly HashSet<StableEntityId> knownBuildings = new();
        private long nextSourcesTick;
        public Tier1AiScheduler Scheduler { get; }
        public HaulWorldQuery HaulJobs { get; }
        public IReadOnlyDictionary<string,long> HaulExecutionFailures => aiWorld.HaulExecutionFailures;
        public bool CanAssignWork(StableEntityId worker, WorkOrder job) => CanAssign(worker, job);

        public NeighbourEconomyRuntime(LocalSceneCompositionRoot world,float gridSize,Vector3 gridOrigin)
        {
            root=world; faction=root.StarterEnemyAi; grid=gridSize; origin=gridOrigin;
            sites=new ConstructionSiteService(root.Resources,faction.Inventories);
            EnsureCarryLoads();
            GroundStartingMaterials();
            var transfers=new ResourceTransferService(faction.Inventories);
            var query=new HaulWorldQuery(faction.Inventories,faction.WorldPiles,root.Buildings,root.HaulClaims,
                grid,new WorldPosition(origin.x,origin.y,origin.z),Ground);
            HaulJobs=query;
            query.PositionOverride=InventoryPosition;
            aiWorld=new Tier1AiWorld(query,root.HaulClaims,transfers,faction.Production,faction.Research,
                root.Buildings,owner=> { if(owner.Kind==InventoryOwnerKind.ConstructionBuffer &&
                    root.Buildings.TryGet(owner.Id,out var building)) sites.NotifyDelivery(building); });
            Scheduler=new Tier1AiScheduler(root.Characters,root.AiAgents,aiWorld,workManager:root.Work,
                capabilityProvider:root.HealthFamily);
            faction.UsesWorkScheduler=true;
            faction.Production.UseGroundOutputs(faction.WorldPiles,p=>new GridCoordinate(
                Mathf.RoundToInt((p.X-origin.x)/grid),Mathf.RoundToInt((p.Z-origin.z)/grid)));
            faction.Production.WorkPosition=WorkPosition;
            faction.Construction.ConfigureLogistics(sites,root.Work,faction.WorldPiles,
                faction.Strategic.HomeSettlementId,root.HaulClaims);
            clearance=new ConstructionClearanceCoordinator(root.Buildings,faction.Production,root.Work,
                faction.Construction,id=>faction.Buildings[id],()=>faction.Inventories.All.Select(x=>x.Owner),
                Approach,grid,new WorldPosition(origin.x,origin.y,origin.z));
            root.AiRuntime.AddScheduler(Scheduler);
            root.AiRuntime.SchedulerForCharacter=id=>faction.Residents.Contains(id)?Scheduler:root.AiScheduler;
            root.AiRuntime.BeforeRegister+=BeforePhysicalRegistration;
            root.AiRuntime.BeforeSimulation+=Advance;
            root.Tiers.RuntimeProjection=Project;
            root.Work.AssignmentScope=CanAssign;
            BindSources();
        }

        public void BindActivityOwner(Game.Simulation.AI.IExternalCharacterActivityOwner owner)
            => Scheduler.BindExternalActivityOwner(owner);

        private void EnsureCarryLoads()
        {
            foreach(var id in faction.Residents)
            {
                var owner=new InventoryOwner(InventoryOwnerKind.CarryLoad,id);
                if(!faction.Inventories.TryGet(owner,out _))faction.Inventories.Add(new ResourceInventory(owner,root.Resources,32));
            }
        }
        private void GroundStartingMaterials()
        {
            // Old aggregate bootstrap kept communal building stock in the ruler's pockets.
            // Convert that same stock into haulable piles, without creating additional resources.
            foreach(var id in faction.Residents)
            {
                if(!faction.Inventories.TryGet(new InventoryOwner(InventoryOwnerKind.Character,id),out var inventory))continue;
                var position=root.Tiers.GetRuntimeState(id).Position;
                var cell=new GridCoordinate(Mathf.RoundToInt((position.X-origin.x)/grid),Mathf.RoundToInt((position.Z-origin.z)/grid));
                foreach(var resource in new[]{"wood_log","plant_fiber","stone"})
                {
                    var resourceId=new ResourceId(resource);var amount=inventory.GetAmount(resourceId);
                    if(!amount.IsPositive)continue;
                    faction.WorldPiles.Create(resourceId,amount,cell);
                    if(!inventory.TryRemove(resourceId,amount))throw new InvalidOperationException("Starting stock transfer failed.");
                }
            }
        }
        private WorldPosition Ground(WorldPosition p)=>new(p.X,root.WorldHeight.SampleHeight(p.X,p.Z)+.04f,p.Z);
        private WorldPosition Approach(Building building)
        {
            var footprint=faction.Buildings[building.DefinitionId].Footprint.Rotate(building.Orientation);
            var preferred=new WorldPosition(origin.x+(building.Coordinate.X+(footprint.Width-1)*.5f)*grid,0,
                origin.z+(building.Coordinate.Z-1.5f)*grid);
            bool Valid(WorldPosition point)
            {
                var grounded=Ground(point);
                return root.WorldDefinition.ContainsClosed(grounded.X,grounded.Z) &&
                    root.WorldHeight.IsBuildable(grounded.X,grounded.Z,30f) &&
                    !root.BuildingOccupancy.IsOccupied(new GridCoordinate(
                        Mathf.RoundToInt((grounded.X-origin.x)/grid),Mathf.RoundToInt((grounded.Z-origin.z)/grid)));
            }
            if(Valid(preferred))return Ground(preferred);
            // Authored footprints can place the nominal front point on a steep micro-cell or make it round
            // back into occupancy. Pick the nearest stable free point instead of making every route fail.
            for(var radius=1;radius<=8;radius++)
            for(var dx=-radius;dx<=radius;dx++)
            for(var dz=-radius;dz<=radius;dz++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dz))!=radius)continue;
                var candidate=new WorldPosition(preferred.X+dx*grid,0,preferred.Z+dz*grid);
                if(Valid(candidate))return Ground(candidate);
            }
            return Ground(preferred);
        }
        private WorldPosition? InventoryPosition(InventoryOwner owner)
        {
            if(owner.Kind==InventoryOwnerKind.Character || owner.Kind==InventoryOwnerKind.CarryLoad)
                return root.Tiers.TryGetTier(owner.Id,out _) ? root.Tiers.GetRuntimeState(owner.Id).Position : null;
            if(root.Buildings.TryGet(owner.Id,out var building) && building.OwnerSideId==faction.Strategic.SideId)
                return Approach(building);
            return null;
        }
        private WorldPosition WorkPosition(StableEntityId worker,ProductionSite site)
        {
            if(site.Kind!=ProductionSiteKind.NaturalSource) return site.Position;
            if(root.CharacterPresentations.TryGet(worker,out _)) return root.Production.WorkPosition?.Invoke(worker,site) ?? site.Position;
            var from=root.Tiers.GetRuntimeState(worker).Position;
            var direction=new Vector2(from.X-site.Position.X,from.Z-site.Position.Z).normalized;
            if(direction.sqrMagnitude<.1f)direction=Vector2.up;
            return Ground(new WorldPosition(site.Position.X+direction.x*1.8f,0,site.Position.Z+direction.y*1.8f));
        }
        private void BindSources()
        {
            var home=root.GlobalMap.GetLocation(new Game.Domain.World.WorldLocationId("enemy_settlement"));
            var position=new WorldPosition(home.MapX,0,home.MapY);
            faction.Production.BindSharedNaturalSites(root.Production.Sites.Where(x=>
                x.Kind==ProductionSiteKind.NaturalSource && x.RecipeId.ToString()!="fish_water" &&
                x.RecipeId.ToString()!="hunt_animal" && x.Position.DistanceSquared(position)<220f*220f));
        }
        private bool CanAssign(StableEntityId worker,WorkOrder job)
        {
            var target=job.Destination ?? job.Target;
            var enemy=false;
            if(root.Buildings.TryGet(target.EntityId,out var building)) enemy=building.OwnerSideId==faction.Strategic.SideId;
            else if(faction.Residents.Contains(target.EntityId)) enemy=true;
            else if(target.Kind==WorkTargetKind.ProductionSite)
                enemy=root.Buildings.All.Any(b=>b.OwnerSideId==faction.Strategic.SideId && !b.IsOperational &&
                    b.PlacementCells.Any(c=>Math.Abs(origin.x+c.X*grid-target.Position.X)<grid*1.5f &&
                        Math.Abs(origin.z+c.Z*grid-target.Position.Z)<grid*1.5f));
            return enemy==faction.Residents.Contains(worker);
        }
        private void BeforePhysicalRegistration(StableEntityId id)
        {
            if(!abstractMoves.Remove(id))return;
            Scheduler.Unregister(id);
        }
        private CharacterRuntimeState Project(CharacterRuntimeState state)
        {
            if(!abstractMoves.TryGetValue(state.CharacterId,out var movement) || !root.AiAgents.TryGet(state.CharacterId,out var agent))return state;
            return state.WithTier1State(movement.CurrentPosition,agent.Needs.Hunger,agent.Needs.Energy,
                agent.Needs.Mood,agent.Needs.Thirst);
        }
        private void Advance(GameTimeAdvance advance)
        {
            if(advance.CalendarDelta<=TimeSpan.Zero)return;
            if(root.Clock.State.CalendarTicks>=nextSourcesTick)
            { BindSources();nextSourcesTick=root.Clock.State.CalendarTicks+TimeSpan.TicksPerHour; }
            foreach(var id in faction.Residents)
            {
                if(!root.Characters.TryGet(id,out var character) || character.LifeState!=CharacterLifeState.Alive)
                { if(abstractMoves.Remove(id))Scheduler.Unregister(id); continue; }
                if(root.AiRuntime.TryGetMovement(id,out _))continue;
                if(!abstractMoves.TryGetValue(id,out var movement))
                {
                    var state=root.Tiers.GetRuntimeState(id);
                    movement=new OffCameraWorkMovement(id,state.Position,Scheduler.Settings.MovementSpeed,FindRoute);
                    abstractMoves.Add(id,movement);
                    Scheduler.Register(character,new Tier1AiAgentState(id,state.Position,
                        new Tier1Needs(state.Hunger,state.Energy,state.Mood,state.Thirst)),movement);
                }
                movement.Advance(advance.CalendarDelta);
            }
            foreach(var building in root.Buildings.All.Where(x=>x.OwnerSideId==faction.Strategic.SideId))
            {
                if(knownBuildings.Add(building.Id) && !building.IsOperational)
                    clearance.Register(building,faction.Production.Sites.Where(x=>!x.IsDepleted &&
                        building.PlacementCells.Any(c=>Math.Abs(origin.x+c.X*grid-x.Position.X)<grid &&
                            Math.Abs(origin.z+c.Z*grid-x.Position.Z)<grid)).Select(x=>x.Id));
                var definition=faction.Buildings[building.DefinitionId];
                if(building.IsOperational && building.DefinitionId.FamilyId.ToString()=="primitive_crop_plot" &&
                    !faction.Production.TryGetSite(building.Id,out _))
                    faction.Production.AddSite(ProductionSite.CreateCropPlot(building.Id,
                        new ProductionRecipeId("cultivate_berries"),Approach(building),faction.Production.Settings.CropGrowthDuration));
                if(building.IsOperational && definition.Production!=null && definition.Workplace!=null)
                    faction.Production.RegisterBuildingWorkplace(building.Id,definition,Approach(building));
            }
            // The player economy runs these logistics passes from LocalSceneCompositionRoot. The neighbour
            // owns a separate ProductionService, so it must maintain the same input/output routes here.
            // Without them residents gather raw food into piles, but never deliver it to the campfire or
            // move cooked meals into storage; once the starter meals run out every worker loops on Eat/Failed.
            faction.Production.MaintainContinuousDeliveries(root.Work);
            faction.Production.MaintainLivingBuildingLogistics(root.Work);
            clearance.Advance();
        }
        public void RebindAfterLoad()
        {
            foreach(var id in abstractMoves.Keys.ToArray())
                if(!root.AiRuntime.TryGetMovement(id,out _)) Scheduler.Unregister(id);
            abstractMoves.Clear(); knownBuildings.Clear(); EnsureCarryLoads(); GroundStartingMaterials(); BindSources();
            foreach(var building in root.Buildings.All.Where(x=>x.OwnerSideId==faction.Strategic.SideId))sites.RebindRestored(building);
            clearance.RebindAfterLoad(root.Buildings.All.Where(x=>x.OwnerSideId==faction.Strategic.SideId));
        }

        private IReadOnlyList<WorldPosition> FindRoute(WorldPosition start,WorldPosition target)
        {
            // This driver exists only while the resident has no physical GameObject. Tier-1 movement takes
            // over as soon as the character materializes, so running a local A* for every off-camera haul
            // adds no visible fidelity and scales poorly for large settlements. Canonical work endpoints are
            // already validated by their source/building services; interpolate between them in O(1).
            return new[]{Ground(target)};
        }
    }
}
