using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Simulation.Work;

namespace Game.Simulation.Buildings
{
    public readonly struct ConstructionClearanceStatus
    {
        public ConstructionClearanceStatus(int total, int remaining, WorkPriority priority,
            bool deliveriesScheduled, bool buildScheduled)
        {
            Total = total;
            Remaining = remaining;
            Priority = priority;
            DeliveriesScheduled = deliveriesScheduled;
            BuildScheduled = buildScheduled;
        }

        public int Total { get; }
        public int Remaining { get; }
        public int Completed => Math.Max(0, Total - Remaining);
        public WorkPriority Priority { get; }
        public bool DeliveriesScheduled { get; }
        public bool BuildScheduled { get; }
        public bool IsClearing => Remaining > 0;
    }

    /// <summary>
    /// Derivable coordination over existing buildings, production sites and work orders. It owns no resources:
    /// removing a blocker is an ordinary production job and material delivery remains ConstructionFundingService.
    /// </summary>
    public sealed class ConstructionClearanceCoordinator
    {
        private sealed class Plan
        {
            public readonly HashSet<StableEntityId> Sources = new HashSet<StableEntityId>();
            public int Total;
            public WorkPriority Priority;
            public bool DeliveriesScheduled;
            public bool BuildScheduled;
        }

        private readonly Dictionary<StableEntityId, Plan> plans = new Dictionary<StableEntityId, Plan>();
        private readonly BuildingRegistry buildings;
        private readonly ProductionService production;
        private readonly WorkManager work;
        private readonly ConstructionFundingService construction;
        private readonly Func<BuildingDefinitionId, BuildingDefinition> definitionResolver;
        private readonly Func<IEnumerable<InventoryOwner>> fundingSources;
        private readonly Func<Building, WorldPosition> buildTarget;
        private readonly float gridSize;
        private readonly WorldPosition gridOrigin;
        private readonly Func<ProductionSite, bool> clearablePolicy;

        public ConstructionClearanceCoordinator(BuildingRegistry buildingRegistry, ProductionService productionService,
            WorkManager workManager, ConstructionFundingService constructionFunding,
            Func<BuildingDefinitionId, BuildingDefinition> resolveDefinition,
            Func<IEnumerable<InventoryOwner>> fundingSourceProvider, Func<Building, WorldPosition> buildTargetProvider,
            float buildingGridSize, WorldPosition buildingGridOrigin,
            Func<ProductionSite, bool> isClearablePolicy = null)
        {
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            production = productionService ?? throw new ArgumentNullException(nameof(productionService));
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            construction = constructionFunding ?? throw new ArgumentNullException(nameof(constructionFunding));
            definitionResolver = resolveDefinition ?? throw new ArgumentNullException(nameof(resolveDefinition));
            fundingSources = fundingSourceProvider ?? throw new ArgumentNullException(nameof(fundingSourceProvider));
            buildTarget = buildTargetProvider ?? throw new ArgumentNullException(nameof(buildTargetProvider));
            if (buildingGridSize <= 0f) throw new ArgumentOutOfRangeException(nameof(buildingGridSize));
            gridSize = buildingGridSize;
            gridOrigin = buildingGridOrigin;
            clearablePolicy = isClearablePolicy ?? IsLegacyClearable;
        }

        public int ActiveSiteCount => plans.Count;

        public void Register(Building building, IEnumerable<StableEntityId> sourceIds,
            WorkPriority priority = WorkPriority.Normal)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (building.IsOperational) return;
            var sources = (sourceIds ?? Array.Empty<StableEntityId>()).Where(x => x.IsValid).Distinct().ToArray();
            var plan = new Plan { Priority = priority };
            foreach (var source in sources)
            {
                if (!production.TryGetSite(source, out var site) || site.IsDepleted) continue;
                if (!clearablePolicy(site))
                    throw new InvalidOperationException("Construction clearance cannot remove a hard blocker.");
                plan.Sources.Add(source);
            }
            plan.Total = plan.Sources.Count;
            plans[building.Id] = plan;
            AdvancePlan(building, plan);
        }

        public void RebindAfterLoad(IEnumerable<Building> restoredBuildings)
        {
            plans.Clear();
            foreach (var building in (restoredBuildings ?? Array.Empty<Building>())
                         .Where(x => x != null && !x.IsOperational &&
                                     x.ConstructionPhase != ConstructionPhase.Cancelled))
                Register(building, FindSources(building), FindPriority(building.Id));
        }

        public void Advance()
        {
            foreach (var entry in plans.ToArray())
            {
                if (!buildings.TryGet(entry.Key, out var building) || building.IsOperational ||
                    building.ConstructionPhase == ConstructionPhase.Cancelled)
                {
                    plans.Remove(entry.Key);
                    continue;
                }
                AdvancePlan(building, entry.Value);
            }
        }

        public bool TryGetStatus(StableEntityId buildingId, out ConstructionClearanceStatus status)
        {
            status = default;
            if (!plans.TryGetValue(buildingId, out var plan)) return false;
            PruneCleared(plan);
            status = new ConstructionClearanceStatus(plan.Total, plan.Sources.Count, plan.Priority,
                plan.DeliveriesScheduled, plan.BuildScheduled);
            return true;
        }

        public bool SetPriority(StableEntityId buildingId, WorkPriority priority)
        {
            if (!plans.TryGetValue(buildingId, out var plan)) return false;
            plan.Priority = priority;
            foreach (var job in LinkedOpenJobs(buildingId, plan)) work.SetPriority(job.Id, priority);
            return true;
        }

        public void Cancel(StableEntityId buildingId)
        {
            if (!plans.TryGetValue(buildingId, out var plan)) return;
            foreach (var job in LinkedOpenJobs(buildingId, plan).ToArray()) work.Cancel(job.Id);
            plans.Remove(buildingId);
        }

        private void AdvancePlan(Building building, Plan plan)
        {
            PruneCleared(plan);
            if (plan.Sources.Count > 0)
            {
                EnsureClearanceJobs(plan);
                return;
            }
            var definition = definitionResolver(building.DefinitionId);
            plan.DeliveriesScheduled = construction.TryScheduleDeliveries(building, definition,
                fundingSources(), plan.Priority) || HasDeliveryJob(building.Id) ||
                building.ConstructionMaterials.All(x => x.RemainingUnits == 0);
            if (!plan.DeliveriesScheduled) return;
            // Scheduling transport is not the same as installing its cargo. Starting a builder
            // before the final stage is committed only plays a work animation while the domain
            // correctly rejects progress. Wait for the construction phase to become workable.
            if (building.ConstructionPhase != ConstructionPhase.Building) return;
            if (!HasBuildJob(building.Id))
            {
                work.CreateDirectJob(WorkJobType.Build, null,
                    WorkTarget.ForEntity(WorkTargetKind.Building, building.Id, buildTarget(building)),
                    plan.Priority);
                plan.BuildScheduled = true;
            }
            else if (HasBuildJob(building.Id)) plan.BuildScheduled = true;
        }

        private void EnsureClearanceJobs(Plan plan)
        {
            foreach (var sourceId in plan.Sources.OrderBy(x => x))
            {
                if (!production.TryGetSite(sourceId, out var site) || site.IsDepleted) continue;
                var target = WorkTarget.ForEntity(WorkTargetKind.ProductionSite, sourceId, site.Position);
                if (work.TryGetOpenJobForTarget(target, out var existing))
                {
                    work.SetPriority(existing.Id, plan.Priority);
                    continue;
                }
                work.CreateDirectJob(JobType(site), null, target, plan.Priority, exclusiveTarget: true);
            }
        }

        private void PruneCleared(Plan plan)
        {
            plan.Sources.RemoveWhere(id => !production.TryGetSite(id, out var site) || site.IsDepleted);
        }

        private IEnumerable<StableEntityId> FindSources(Building building)
        {
            var cells = new HashSet<GridCoordinate>(building.PlacementCells);
            if (cells.Count == 0) cells.UnionWith(definitionResolver(building.DefinitionId).Footprint
                .EnumerateCells(building.Coordinate, building.Orientation));
            foreach (var site in production.Sites.Where(x => x.Kind == ProductionSiteKind.NaturalSource &&
                                                              !x.IsDepleted && clearablePolicy(x)))
            {
                var cell = new GridCoordinate(
                    (int)Math.Round((site.Position.X - gridOrigin.X) / gridSize),
                    (int)Math.Round((site.Position.Z - gridOrigin.Z) / gridSize));
                if (cells.Any(x => Math.Abs(x.X - cell.X) <= 1 && Math.Abs(x.Z - cell.Z) <= 1))
                    yield return site.Id;
            }
        }

        private WorkPriority FindPriority(StableEntityId buildingId)
        {
            var jobs = work.OpenJobs.Where(x =>
                ((x.Target.Kind == WorkTargetKind.Building && x.Target.EntityId == buildingId) ||
                 (x.Destination.HasValue && x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
                  x.Destination.Value.EntityId == buildingId))).ToArray();
            return jobs.Length == 0 ? WorkPriority.Normal : jobs.OrderByDescending(x => x.Priority).First().Priority;
        }

        private IEnumerable<WorkOrder> LinkedOpenJobs(StableEntityId buildingId, Plan plan) => work.OpenJobs.Where(x =>
            (x.Target.Kind == WorkTargetKind.ProductionSite && plan.Sources.Contains(x.Target.EntityId)) ||
                              (x.Target.Kind == WorkTargetKind.Building && x.Target.EntityId == buildingId) ||
                              (x.Destination.HasValue && x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
                               x.Destination.Value.EntityId == buildingId));

        private bool HasDeliveryJob(StableEntityId buildingId) => work.OpenJobs.Any(x =>
            x.Type == WorkJobType.Haul && x.Destination.HasValue &&
            x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
            x.Destination.Value.EntityId == buildingId);

        private bool HasBuildJob(StableEntityId buildingId) => work.OpenJobs.Any(x =>
            x.Type == WorkJobType.Build && x.Target.Kind == WorkTargetKind.Building &&
            x.Target.EntityId == buildingId);

        private static WorkJobType JobType(ProductionSite site)
        {
            var recipe = site.RecipeId.ToString();
            if (recipe == "chop_wood") return WorkJobType.Chop;
            if (recipe == "gather_stone" || recipe == "gather_iron" || recipe == "gather_gold")
                return WorkJobType.Mine;
            return WorkJobType.Gather;
        }

        private static bool IsLegacyClearable(ProductionSite site)
        {
            if (site == null || site.Kind != ProductionSiteKind.NaturalSource || site.IsDepleted) return false;
            var recipe = site.RecipeId.ToString();
            return recipe != "fish_water" && recipe != "hunt_animal";
        }
    }
}
