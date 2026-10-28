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
        private readonly Dictionary<StableEntityId,OffCameraWorkMovement> abstractMoves = new();
        private readonly HashSet<StableEntityId> knownBuildings = new();
        private long nextSourcesTick;
        public Tier1AiScheduler Scheduler { get; }

        public NeighbourEconomyRuntime(LocalSceneCompositionRoot world,float gridSize,Vector3 gridOrigin)
        {
            root=world; faction=root.StarterEnemyAi; grid=gridSize; origin=gridOrigin;
            sites=new ConstructionSiteService(root.Resources,faction.Inventories);
            EnsureCarryLoads();
            GroundStartingMaterials();
            var transfers=new ResourceTransferService(faction.Inventories);
            var query=new HaulWorldQuery(faction.Inventories,faction.WorldPiles,root.Buildings,root.HaulClaims,
                grid,new WorldPosition(origin.x,origin.y,origin.z),Ground);
            query.PositionOverride=InventoryPosition;
            var aiWorld=new Tier1AiWorld(query,root.HaulClaims,transfers,faction.Production,faction.Research,
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
            return Ground(new WorldPosition(origin.x+(building.Coordinate.X+(footprint.Width-1)*.5f)*grid,0,
                origin.z+(building.Coordinate.Z-1.5f)*grid));
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
                if(building.IsOperational && building.DefinitionId.ToString()=="primitive_crop_plot" &&
                    !faction.Production.TryGetSite(building.Id,out _))
                    faction.Production.AddSite(ProductionSite.CreateCropPlot(building.Id,
                        new ProductionRecipeId("cultivate_berries"),Approach(building),faction.Production.Settings.CropGrowthDuration));
                if(building.IsOperational && definition.Production!=null && definition.Workplace!=null)
                    faction.Production.RegisterBuildingWorkplace(building.Id,definition,Approach(building));
            }
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
            // Bounded local A* also works when distant terrain tiles have no physical NavMesh.
            const float step=2f;
            Vector2Int Cell(WorldPosition p)=>new(Mathf.RoundToInt(p.X/step),Mathf.RoundToInt(p.Z/step));
            var first=Cell(start);var last=Cell(target);
            var frontier=new SortedSet<(float score,int order,Vector2Int cell)>(Comparer<(float score,int order,Vector2Int cell)>.Create(
                (a,b)=>a.score!=b.score?a.score.CompareTo(b.score):a.order.CompareTo(b.order)));
            var costs=new Dictionary<Vector2Int,float>{{first,0}};var parents=new Dictionary<Vector2Int,Vector2Int>();
            var visited=new HashSet<Vector2Int>();var sequence=0;
            frontier.Add((0,sequence++,first));
            var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(frontier.Count>0 && visited.Count<4096)
            {
                var cell=frontier.Min.cell;frontier.Remove(frontier.Min);if(!visited.Add(cell))continue;
                if(cell==last)
                {
                    var result=new List<WorldPosition>{Ground(target)};
                    while(cell!=first){result.Add(Ground(new WorldPosition(cell.x*step,0,cell.y*step)));cell=parents[cell];}
                    result.Reverse();return result;
                }
                foreach(var direction in directions)
                {
                    var next=cell+direction;if(visited.Contains(next) || Vector2Int.Distance(next,first)>150)continue;
                    var allowed=true;
                    for(var i=0;i<=4 && allowed;i++)
                    {
                        var x=(cell.x+direction.x*i/4f)*step;var z=(cell.y+direction.y*i/4f)*step;
                        allowed=root.WorldDefinition.ContainsClosed(x,z) && root.WorldHeight.IsBuildable(x,z,30f) &&
                            !root.BuildingOccupancy.IsOccupied(new GridCoordinate(Mathf.RoundToInt((x-origin.x)/grid),Mathf.RoundToInt((z-origin.z)/grid)));
                    }
                    if(!allowed)continue;
                    var cost=costs[cell]+1;if(costs.TryGetValue(next,out var old) && old<=cost)continue;
                    costs[next]=cost;parents[next]=cell;
                    frontier.Add((cost+Math.Abs(next.x-last.x)+Math.Abs(next.y-last.y),sequence++,next));
                }
            }
            return null;
        }
    }
}
