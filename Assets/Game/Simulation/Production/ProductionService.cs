using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Work;
using Game.Simulation.Resources;
using Game.Simulation.Tiers;
using Game.Domain.Research;
using Game.Domain.Health;
using Game.Domain.Weather;

namespace Game.Simulation.Production
{
    public enum ProductionWaitingReason
    {
        None = 0, QueueEmpty = 1, Paused = 2, MissingInputs = 3,
        OutputFull = 4, WorkerSlotFull = 5, BuildingUnavailable = 6
    }

    public sealed class ProductionSettings
    {
        public ProductionSettings(TimeSpan fixedStep, TimeSpan cropGrowthDuration,
            TimeSpan foodTicksPerPersonUnit, IEnumerable<ResourceStockTarget> targets)
        {
            if (fixedStep <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            if (cropGrowthDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cropGrowthDuration));
            if (foodTicksPerPersonUnit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(foodTicksPerPersonUnit));
            FixedStep = fixedStep;
            CropGrowthDuration = cropGrowthDuration;
            FoodTicksPerPersonUnit = foodTicksPerPersonUnit;
            Targets = Array.AsReadOnly((targets ?? throw new ArgumentNullException(nameof(targets)))
                .OrderBy(x => x.ResourceId).ToArray());
        }

        public TimeSpan FixedStep { get; }
        public TimeSpan CropGrowthDuration { get; }
        public TimeSpan FoodTicksPerPersonUnit { get; }
        public IReadOnlyList<ResourceStockTarget> Targets { get; }

        public static ProductionSettings StoneAge => new ProductionSettings(TimeSpan.FromDays(1),
            TimeSpan.FromDays(2), TimeSpan.FromDays(15), new[]
            {
                new ResourceStockTarget(new ResourceId("berries"), 40),
                new ResourceStockTarget(new ResourceId("plant_fiber"), 20),
                new ResourceStockTarget(new ResourceId("stone"), 20),
                new ResourceStockTarget(new ResourceId("wood_log"), 30)
            });
    }

    public sealed class FoodConsumptionService
    {
        private readonly ResourceInventoryRegistry inventories;
        private readonly ResourceId[] edibleResources;
        private readonly long ticksPerPersonUnit;

        public FoodConsumptionService(ResourceInventoryRegistry inventories, IEnumerable<ResourceId> edibleResourceIds,
            TimeSpan ticksPerPersonUnit)
        {
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            edibleResources = (edibleResourceIds ?? throw new ArgumentNullException(nameof(edibleResourceIds)))
                .OrderBy(x => x).Distinct().ToArray();
            if (edibleResources.Any(x => !x.IsValid))
                throw new ArgumentException("Edible resource identities must be valid.", nameof(edibleResourceIds));
            if (ticksPerPersonUnit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ticksPerPersonUnit));
            this.ticksPerPersonUnit = ticksPerPersonUnit.Ticks;
        }

        public long RemainderTicks { get; private set; }
        public long TotalConsumed { get; private set; }
        public long TotalShortage { get; private set; }
        public bool IsShortage => TotalShortage > 0;
        public IReadOnlyList<ResourceId> EdibleResources => Array.AsReadOnly(edibleResources);

        public bool TryConsumeMeal()
        {
            foreach (var inventory in inventories.All
                         .Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage ||
                             x.Owner.Kind == InventoryOwnerKind.Character)
                         .OrderBy(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage ? 0 : 1)
                         .ThenBy(x => x.Owner.Id))
            foreach (var resourceId in edibleResources)
            {
                if (!inventory.TryRemove(resourceId, new ResourceQuantity(1))) continue;
                TotalConsumed = checked(TotalConsumed + 1);
                return true;
            }
            return false;
        }

        public void Advance(TimeSpan elapsed, int population)
        {
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
            if (elapsed == TimeSpan.Zero || population == 0) return;
            var accumulated = checked(RemainderTicks + checked(elapsed.Ticks * (long)population));
            var due = accumulated / ticksPerPersonUnit;
            RemainderTicks = accumulated % ticksPerPersonUnit;
            if (due == 0) return;
            var remaining = due;
            while (remaining > 0 && TryConsumeMeal()) remaining--;
            TotalShortage = checked(TotalShortage + remaining);
        }

        public void Restore(long remainderTicks, long consumed, long shortage)
        {
            if (remainderTicks < 0 || remainderTicks >= ticksPerPersonUnit || consumed < 0 || shortage < 0)
                throw new ArgumentOutOfRangeException(nameof(remainderTicks));
            RemainderTicks = remainderTicks;
            TotalConsumed = consumed;
            TotalShortage = shortage;
        }
    }

    public readonly struct ProductionJob
    {
        public ProductionJob(ProductionTask task, WorldPosition sourcePosition)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            SourcePosition = sourcePosition;
        }
        public ProductionTask Task { get; }
        public WorldPosition SourcePosition { get; }
    }

    public readonly struct ProductionCompletion
    {
        public ProductionCompletion(bool completed, bool producedOutput, ResourceId resourceId, long units,
            InventoryOwner outputOwner)
        {
            Completed = completed;
            ProducedOutput = producedOutput;
            ResourceId = resourceId;
            Units = units;
            OutputOwner = outputOwner;
        }
        public bool Completed { get; }
        public bool ProducedOutput { get; }
        public ResourceId ResourceId { get; }
        public long Units { get; }
        public InventoryOwner OutputOwner { get; }
    }

    /// <summary>
    /// Canonical engine-free production authority. Natural sources, crop progress, task reservations,
    /// recipe commits, food consumption and target-stock policy all advance from explicit game time.
    /// </summary>
    public sealed class ProductionService
    {
        private readonly ProductionCatalog recipes;
        private readonly ResourceCatalog resources;
        private readonly ResourceInventoryRegistry inventories;
        private readonly BuildingRegistry buildings;
        private readonly ResourceTransferService transfers;
        private readonly ProductionSettings settings;
        private readonly Dictionary<StableEntityId, ProductionSite> sites = new Dictionary<StableEntityId, ProductionSite>();
        private readonly List<ProductionSite> orderedSites = new List<ProductionSite>();
        private readonly Dictionary<StableEntityId, ProductionTask> tasks = new Dictionary<StableEntityId, ProductionTask>();
        private readonly Dictionary<StableEntityId, StableEntityId> workerTasks = new Dictionary<StableEntityId, StableEntityId>();
        private readonly Dictionary<ResourceId, long> targets = new Dictionary<ResourceId, long>();
        private readonly Dictionary<ResourceId, long> producedTotals = new Dictionary<ResourceId, long>();
        private readonly List<StableEntityId> orderedWorkerBuffer = new List<StableEntityId>();
        private readonly IResearchUnlockView unlocks;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly IWeatherModifierProvider weather;
        private Dictionary<ResourceId, long> stepTotals;
        private WorldPileService outputPiles;
        private Func<WorldPosition, GridCoordinate> outputCoordinate;
        public bool UsesGroundOutputs => outputPiles != null;

        public void UseGroundOutputs(WorldPileService piles, Func<WorldPosition, GridCoordinate> coordinate)
        {
            outputPiles = piles ?? throw new ArgumentNullException(nameof(piles));
            outputCoordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
        }

        public ProductionService(ProductionCatalog recipeCatalog, ResourceCatalog resourceCatalog,
            ResourceInventoryRegistry inventoryRegistry, BuildingRegistry buildingRegistry,
            ResourceTransferService transferService, ProductionSettings productionSettings = null,
            IResearchUnlockView researchUnlocks = null, ICharacterCapabilityProvider capabilityProvider = null,
            IWeatherModifierProvider weatherModifierProvider = null)
        {
            recipes = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            transfers = transferService ?? throw new ArgumentNullException(nameof(transferService));
            settings = productionSettings ?? ProductionSettings.StoneAge;
            unlocks = researchUnlocks;
            capabilities = capabilityProvider;
            weather = weatherModifierProvider;
            foreach (var target in settings.Targets)
            {
                resources.Get(target.ResourceId);
                if (!targets.TryAdd(target.ResourceId, target.Target))
                    throw new ArgumentException("Duplicate production target.", nameof(productionSettings));
            }
            var carcass = new ResourceId("carcass");
            if (resources.Contains(carcass) && !targets.ContainsKey(carcass)) targets.Add(carcass, 4);
            foreach (var crop in new[] { "carrot", "cabbage" })
                if (resources.Contains(new ResourceId(crop))) targets.TryAdd(new ResourceId(crop), 20);
            var edible = new[] { "cooked_berries", "cooked_fish", "cooked_meat", "dried_meat", "carrot", "cabbage" }
                .Select(id => new ResourceId(id)).Where(resources.Contains).ToArray();
            Food = new FoodConsumptionService(inventories, edible, settings.FoodTicksPerPersonUnit);
        }

        public ProductionCatalog Recipes => recipes;
        public ProductionSettings Settings => settings;
        public FoodConsumptionService Food { get; }
        public ResourceEcologyService Ecology { get; private set; }
        public IndustrialExtractionService Industry { get; private set; }
        public int Population { get; set; }
        public long LastCalendarTick { get; private set; }
        public IReadOnlyList<ProductionSite> Sites => sites.Values.OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<ProductionTask> ActiveTasks => tasks.Values.Where(x => !x.IsTerminal).OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<ResourceStockTarget> Targets => targets.OrderBy(x => x.Key)
            .Select(x => new ResourceStockTarget(x.Key, x.Value)).ToArray();

        public InventoryOwner InputOwner(StableEntityId buildingId) =>
            new InventoryOwner(InventoryOwnerKind.ProductionInput, buildingId);
        public InventoryOwner OutputOwner(StableEntityId buildingId) =>
            new InventoryOwner(InventoryOwnerKind.ProductionOutput, buildingId);

        public void AttachNaturalResources(ResourceEcologyService ecology, IndustrialExtractionService industry)
        {
            Ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
            Industry = industry ?? throw new ArgumentNullException(nameof(industry));
        }

        public void AddSite(ProductionSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var recipe = recipes.Get(site.RecipeId);
            if (recipe.SiteKind != site.Kind) throw new ArgumentException("Site kind does not match its recipe.", nameof(site));
            if (!sites.TryAdd(site.Id, site)) throw new InvalidOperationException("Production site already exists.");
            orderedSites.Add(site);
            orderedSites.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public IReadOnlyList<ProductionSite> RegisterBuildingWorkplace(StableEntityId buildingId,
            BuildingDefinition definition, WorldPosition position)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building identity required.", nameof(buildingId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Workplace == null || definition.Production == null)
                throw new ArgumentException("Building definition requires workplace and production specs.", nameof(definition));
            var inputOwner = InputOwner(buildingId);
            var outputOwner = OutputOwner(buildingId);
            if (!inventories.TryGet(inputOwner, out _))
                inventories.Add(new ResourceInventory(inputOwner, resources, definition.Production.InputCapacity));
            if (!inventories.TryGet(outputOwner, out _))
                inventories.Add(new ResourceInventory(outputOwner, resources, definition.Production.OutputCapacity));
            var result = new List<ProductionSite>();
            foreach (var recipeId in definition.Production.RecipeIds)
            {
                var id = WorkplaceSiteId(buildingId, recipeId);
                if (!sites.TryGetValue(id, out var site))
                {
                    site = ProductionSite.CreateBuildingWorkplace(id, buildingId, recipeId, position,
                        definition.Workplace.MaximumWorkers);
                    AddSite(site);
                }
                result.Add(site);
            }
            return result;
        }

        public bool QueueBatches(StableEntityId buildingId, ProductionRecipeId recipeId, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var id = WorkplaceSiteId(buildingId, recipeId);
            if (!sites.TryGetValue(id, out var site) || site.Kind != ProductionSiteKind.BuildingWorkplace) return false;
            site.QueueBatches(count);
            return true;
        }

        public int QueueBatchesWithDeliveries(StableEntityId buildingId, ProductionRecipeId recipeId, int count,
            WorkManager work, WorkPriority priority = WorkPriority.High)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (!QueueBatches(buildingId, recipeId, count)) return 0;
            var site = sites[WorkplaceSiteId(buildingId, recipeId)];
            var recipe = recipes.Get(recipeId);
            var destination = InputOwner(buildingId);
            var input = inventories.Get(destination);
            var created = 0;
            foreach (var amount in recipe.Inputs)
            {
                var required = checked(amount.Quantity.Units * site.QueuedBatches);
                var pendingDestination = work.Jobs.Where(job => !job.IsTerminal &&
                    job.Type == WorkJobType.Haul && job.ResourceId == amount.ResourceId &&
                    job.Destination.HasValue && job.Destination.Value.Kind == WorkTargetKind.ProductionInput &&
                    job.Destination.Value.EntityId == buildingId).Sum(job => job.Quantity.Units);
                var missing = Math.Max(0, required - input.GetAmount(amount.ResourceId).Units - pendingDestination);
                foreach (var source in inventories.All.OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
                {
                    if (missing == 0) break;
                    if (source.Owner.Kind != InventoryOwnerKind.WorldPile &&
                        source.Owner.Kind != InventoryOwnerKind.BuildingStorage) continue;
                    var pendingSource = work.Jobs.Where(job => !job.IsTerminal &&
                        job.Type == WorkJobType.Haul && job.ResourceId == amount.ResourceId &&
                        job.Target.EntityId == source.Owner.Id).Sum(job => job.Quantity.Units);
                    var units = Math.Min(missing,
                        Math.Max(0, source.GetAmount(amount.ResourceId).Units - pendingSource));
                    if (units <= 0) continue;
                    work.CreateHaulJob(null, source.Owner, destination, amount.ResourceId,
                        new ResourceQuantity(units), priority);
                    missing -= units;
                    created++;
                }
            }
            return created;
        }

        public bool SetWorkplacePaused(StableEntityId buildingId, ProductionRecipeId recipeId, bool paused)
        {
            var id = WorkplaceSiteId(buildingId, recipeId);
            if (!sites.TryGetValue(id, out var site) || site.Kind != ProductionSiteKind.BuildingWorkplace) return false;
            site.SetPaused(paused);
            return true;
        }

        public ProductionWaitingReason GetWaitingReason(ProductionSite site)
        {
            if (site == null || site.Kind != ProductionSiteKind.BuildingWorkplace)
                throw new ArgumentException("Building workplace required.", nameof(site));
            if (!IsSiteOperational(site.Id)) return ProductionWaitingReason.BuildingUnavailable;
            if (site.Paused) return ProductionWaitingReason.Paused;
            if (site.QueuedBatches == 0) return ProductionWaitingReason.QueueEmpty;
            var recipe = recipes.Get(site.RecipeId);
            if (!InputsAvailable(recipe, site)) return ProductionWaitingReason.MissingInputs;
            if (!CanPlaceOutputs(default, recipe, site)) return ProductionWaitingReason.OutputFull;
            if (ActiveWorkersAt(site.OwnerBuildingId) >= site.MaximumWorkers &&
                !tasks.Values.Any(task => !task.IsTerminal && task.SiteId == site.Id))
                return ProductionWaitingReason.WorkerSlotFull;
            return ProductionWaitingReason.None;
        }

        public static StableEntityId WorkplaceSiteId(StableEntityId buildingId, ProductionRecipeId recipeId)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building identity required.", nameof(buildingId));
            if (!recipeId.IsValid) throw new ArgumentException("Recipe identity required.", nameof(recipeId));
            ulong first = 14695981039346656037UL;
            ulong second = 1099511628211UL;
            foreach (var value in buildingId.ToGuid().ToByteArray())
            {
                first = (first ^ value) * 1099511628211UL;
                second = (second ^ (byte)(value + 31)) * 14695981039346656037UL;
            }
            var text = recipeId.ToString();
            for (var i = 0; i < text.Length; i++)
            {
                first = (first ^ text[i]) * 1099511628211UL;
                second = (second ^ text[text.Length - i - 1]) * 1099511628211UL;
            }
            return StableEntityId.FromDeterministicHash(first, second);
        }

        public bool TryGetSite(StableEntityId id, out ProductionSite site) => sites.TryGetValue(id, out site);

        public bool TryGetActiveWorkplace(StableEntityId workerId, out Building building)
        {
            building = null;
            if (!workerTasks.TryGetValue(workerId, out var taskId) ||
                !tasks.TryGetValue(taskId, out var task) || task.IsTerminal ||
                !sites.TryGetValue(task.SiteId, out var site) ||
                site.Kind != ProductionSiteKind.BuildingWorkplace || !site.OwnerBuildingId.IsValid)
                return false;
            return buildings.TryGet(site.OwnerBuildingId, out building) && building.IsOperational;
        }

        public bool TryGetActiveTask(StableEntityId workerId, out ProductionTask task, out ProductionSite site)
        {
            task = null;
            site = null;
            return workerTasks.TryGetValue(workerId, out var taskId) && tasks.TryGetValue(taskId, out task) &&
                !task.IsTerminal && sites.TryGetValue(task.SiteId, out site);
        }
        public bool IsSiteOperational(StableEntityId id)
        {
            if (!sites.TryGetValue(id, out var site)) return false;
            if (site.Kind == ProductionSiteKind.NaturalSource) return true;
            var ownerId = site.Kind == ProductionSiteKind.BuildingWorkplace ? site.OwnerBuildingId : id;
            return !buildings.TryGet(ownerId, out var building) || building.IsOperational;
        }

        public int CancelTasksForSite(StableEntityId siteId)
        {
            var active = tasks.Values.Where(x => !x.IsTerminal && x.SiteId == siteId)
                .OrderBy(x => x.Id).Select(x => x.Id).ToArray();
            for (var i = 0; i < active.Length; i++) Cancel(active[i]);
            return active.Length;
        }
        public long GetTarget(ResourceId id) => targets.TryGetValue(id, out var value) ? value : 0;
        public void SetTarget(ResourceId id, long target)
        {
            resources.Get(id);
            if (target < 0) throw new ArgumentOutOfRangeException(nameof(target));
            targets[id] = target;
        }

        public long GetCanonicalTotal(ResourceId id)
        {
            resources.Get(id);
            long total = 0;
            foreach (var inventory in inventories.All) total = checked(total + inventory.GetAmount(id).Units);
            return total;
        }

        public long GetProducedTotal(ResourceId id)
        {
            resources.Get(id);
            return producedTotals.TryGetValue(id, out var total) ? total : 0;
        }

        public bool NeedsProduction(ResourceId id)
        {
            if (!targets.TryGetValue(id, out var target) || target <= 0) return false;
            return checked(GetCanonicalTotal(id) + ReservedOutput(id)) < target;
        }

        public bool IsRecipeAvailable(ProductionRecipeId id)
        {
            if (unlocks != null && !unlocks.IsProductionRecipeUnlocked(id)) return false;
            if (id.ToString() == "hunt_animal" && resources.Contains(new ResourceId("carcass")) &&
                !buildings.All.Any(building => building.IsOperational &&
                    building.DefinitionId == new BuildingDefinitionId("hunting_camp"))) return false;
            if (id.ToString() == "fish_water" &&
                !buildings.All.Any(building => building.IsOperational &&
                    building.DefinitionId == new BuildingDefinitionId("fishing_camp"))) return false;
            return true;
        }

        public bool SelectCrop(StableEntityId siteId, ProductionRecipeId recipeId,
            Game.Domain.Combat.CombatSideId requestingSide)
        {
            if (!sites.TryGetValue(siteId, out var site) || site.Kind != ProductionSiteKind.PrimitiveCropPlot ||
                site.CropPhase != CropPlotPhase.Unplanted || !buildings.TryGet(siteId, out var building) ||
                building.OwnerSideId != requestingSide || !building.IsOperational ||
                ActiveTasks.Any(task => task.SiteId == siteId) || !IsRecipeAvailable(recipeId)) return false;
            var recipe = recipes.All.FirstOrDefault(value => value.Id == recipeId);
            if (recipe == null || recipe.SiteKind != ProductionSiteKind.PrimitiveCropPlot) return false;
            site.SelectCrop(recipe);
            return true;
        }

        public bool TryReserve(StableEntityId workerId, WorldPosition workerPosition, out ProductionJob job)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            job = default;
            if (capabilities != null && !capabilities.GetCapabilities(workerId).CanPerformOrdinaryWork) return false;
            if (workerTasks.TryGetValue(workerId, out var existingId) && tasks.TryGetValue(existingId, out var existing))
            {
                if (existing.IsTerminal) return false;
                if (!IsSiteOperational(existing.SiteId)) { Cancel(existing.Id); return false; }
                job = new ProductionJob(existing, sites[existing.SiteId].Position);
                return true;
            }
            if (!inventories.TryGet(inventories.WorkerOwner(workerId), out _)) return false;

            if (!TrySelectSite(workerId, workerPosition, out var selected, out var selectedKind)) return false;
            var selectedRecipe = recipes.Get(selected.RecipeId);
            var task = ProductionTask.Create(workerId, selected.Id, selected.RecipeId, selectedKind,
                selectedRecipe.WorkRequiredTicks);
            tasks.Add(task.Id, task);
            workerTasks.Add(workerId, task.Id);
            job = new ProductionJob(task, selected.Position);
            return true;
        }

        /// <summary>Reserves the exact player-selected site, bypassing only automatic economic target selection.</summary>
        public bool TryReserveAtSite(StableEntityId workerId, StableEntityId siteId, out ProductionJob job)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            if (!siteId.IsValid) throw new ArgumentException("Site identity required.", nameof(siteId));
            job = default;
            if (capabilities != null && !capabilities.GetCapabilities(workerId).CanPerformOrdinaryWork) return false;
            if (workerTasks.TryGetValue(workerId, out var existingId) && tasks.TryGetValue(existingId, out var existing))
            {
                if (existing.IsTerminal || existing.SiteId != siteId || !IsSiteOperational(siteId)) return false;
                job = new ProductionJob(existing, sites[siteId].Position);
                return true;
            }
            if (!inventories.TryGet(inventories.WorkerOwner(workerId), out _) ||
                !sites.TryGetValue(siteId, out var site) || !site.IsWorkAvailable ||
                !IsSiteOperational(siteId)) return false;
            var recipe = recipes.Get(site.RecipeId);
            if (!recipe.Enabled || !IsRecipeAvailable(recipe.Id)) return false;
            if (recipe.HarvestEntireSource && tasks.Values.Any(x => !x.IsTerminal && x.SiteId == siteId)) return false;
              if (!HasNaturalSourceCapacity(site)) return false;
            if (site.Kind == ProductionSiteKind.BuildingWorkplace && ActiveWorkersAt(site.OwnerBuildingId) >= site.MaximumWorkers)
                return false;
            var kind = TaskKindFor(site);
            if (kind != ProductionTaskKind.Plant && !CanPlaceOutputs(workerId, recipe, site)) return false;
            if (!InputsAvailable(recipe, site)) return false;
            var task = ProductionTask.Create(workerId, site.Id, site.RecipeId, kind, recipe.WorkRequiredTicks);
            tasks.Add(task.Id, task);
            workerTasks.Add(workerId, task.Id);
            job = new ProductionJob(task, site.Position);
            return true;
        }

        public bool TryReserveAtBuilding(StableEntityId workerId, StableEntityId buildingId, out ProductionJob job)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building identity required.", nameof(buildingId));
            job = default;
            if (workerTasks.TryGetValue(workerId, out var existingId) &&
                tasks.TryGetValue(existingId, out var existing) && !existing.IsTerminal &&
                sites.TryGetValue(existing.SiteId, out var existingSite) &&
                existingSite.OwnerBuildingId == buildingId)
            {
                job = new ProductionJob(existing, existingSite.Position);
                return true;
            }
            foreach (var site in orderedSites.Where(x => x.Kind == ProductionSiteKind.BuildingWorkplace &&
                         x.OwnerBuildingId == buildingId && x.IsWorkAvailable).OrderBy(x => x.Id))
                if (TryReserveAtSite(workerId, site.Id, out job)) return true;
            return false;
        }

        public bool HasEligibleWork(StableEntityId workerId, WorldPosition workerPosition)
        {
            if (!workerId.IsValid) return false;
            if (workerTasks.TryGetValue(workerId, out var taskId) && tasks.TryGetValue(taskId, out var task) && !task.IsTerminal)
                return true;
            // Tier 1 keeps one physical producer at a time in this MVP slice so logistics/rest work remains observable.
            // Aggregate Tier 2/Tier 3 workers use TryReserve directly and are not subject to this presentation policy.
            if (tasks.Values.Any(candidate => !candidate.IsTerminal)) return false;
            return inventories.TryGet(inventories.WorkerOwner(workerId), out _) &&
                TrySelectSite(workerId, workerPosition, out _, out _);
        }

        public bool IsTaskActive(StableEntityId taskId) => tasks.TryGetValue(taskId, out var task) && !task.IsTerminal;

        public void CollectActiveWorkerIds(List<StableEntityId> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            foreach (var task in tasks.Values)
                if (!task.IsTerminal) destination.Add(task.WorkerId);
        }

        public ProductionCompletion AdvanceTask(StableEntityId taskId, TimeSpan work)
            => AdvanceTaskAt(taskId, work, LastCalendarTick);

        private ProductionCompletion AdvanceTaskAt(StableEntityId taskId, TimeSpan work, long calendarTick)
        {
            if (work < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(work));
            if (!tasks.TryGetValue(taskId, out var task) || task.IsTerminal) return default;
            if (!IsSiteOperational(task.SiteId)) { Cancel(task.Id); return default; }
            var workTicks = GetEffectiveWorkTicks(task, work.Ticks, calendarTick);
            task.AddWork(workTicks);
            if (task.AccumulatedWorkTicks < task.RequiredWorkTicks) return default;
            return Complete(task);
        }

        public bool Cancel(StableEntityId taskId)
        {
            if (!tasks.TryGetValue(taskId, out var task) || task.IsTerminal) return false;
            task.Cancel();
            Release(task);
            return true;
        }

        public bool TryFindStorage(ResourceId id, long units, WorldPosition from,
            out InventoryOwner owner, out WorldPosition position)
        {
            owner = default;
            position = default;
            var found = false;
            var bestDistance = 0d;
            foreach (var inventory in inventories.All.OrderBy(x => x.Owner.Id))
            {
                if (inventory.Owner.Kind != InventoryOwnerKind.BuildingStorage ||
                    !inventory.CanAdd(id, new ResourceQuantity(units)) ||
                    !buildings.TryGet(inventory.Owner.Id, out var building) ||
                    !building.IsOperational) continue;
                var candidate = new WorldPosition(building.Coordinate.X, building.Coordinate.Level, building.Coordinate.Z);
                var distance = from.DistanceSquared(candidate);
                if (!found || distance < bestDistance ||
                    (distance.Equals(bestDistance) && inventory.Owner.Id.CompareTo(owner.Id) < 0))
                {
                    found = true;
                    bestDistance = distance;
                    owner = inventory.Owner;
                    position = candidate;
                }
            }
            return found;
        }

        public bool TryDeposit(StableEntityId workerId, InventoryOwner destination, ResourceId id, long units)
        {
            if (destination.Kind == InventoryOwnerKind.BuildingStorage &&
                (!buildings.TryGet(destination.Id, out var building) || !building.IsOperational)) return false;
            var source = inventories.WorkerOwner(workerId);
            return transfers.TransferExact(source, destination, id, new ResourceQuantity(units)) == TransferFailureReason.None;
        }

        public void AdvanceTo(long targetCalendarTick, IEnumerable<StableEntityId> abstractWorkers)
        {
            if (targetCalendarTick < LastCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(targetCalendarTick), "Production time cannot move backwards.");
            if (abstractWorkers == null) throw new ArgumentNullException(nameof(abstractWorkers));
            var firstStep = LastCalendarTick / settings.FixedStep.Ticks + 1L;
            var finalStep = targetCalendarTick / settings.FixedStep.Ticks;
            // No production boundary was crossed. Preserve the checkpoint, without preparing an unused roster.
            if (firstStep > finalStep) { LastCalendarTick = targetCalendarTick; return; }
            orderedWorkerBuffer.Clear();
            orderedWorkerBuffer.AddRange(abstractWorkers);
            orderedWorkerBuffer.Sort();
            RemoveAdjacentDuplicates(orderedWorkerBuffer);
            for (var step = firstStep; step <= finalStep; step++)
            {
                var stepStartTick = checked((step - 1L) * settings.FixedStep.Ticks);
                var cropModifier = weather == null ? 1000 :
                    weather.GetWeatherAtCalendarTick(stepStartTick).Modifiers.CropGrowthPerThousand;
                var growthTicks = checked(settings.FixedStep.Ticks * cropModifier / 1000L);
                foreach (var site in orderedSites)
                    if (IsSiteOperational(site.Id)) site.AdvanceGrowth(growthTicks);
                Food.Advance(settings.FixedStep, Population);
                stepTotals = new Dictionary<ResourceId, long>();
                foreach (var definition in resources.All) stepTotals.Add(definition.Id, GetCanonicalTotal(definition.Id));
                try
                {
                    for (var i = 0; i < orderedWorkerBuffer.Count; i++)
                        AdvanceAbstractWorker(orderedWorkerBuffer[i], settings.FixedStep.Ticks, stepStartTick);
                }
                finally { stepTotals = null; }
            }
            LastCalendarTick = targetCalendarTick;
        }

        private static void RemoveAdjacentDuplicates(List<StableEntityId> values)
        {
            if (values.Count < 2) return;
            var write = 1;
            for (var read = 1; read < values.Count; read++)
                if (values[read] != values[write - 1]) values[write++] = values[read];
            if (write < values.Count) values.RemoveRange(write, values.Count - write);
        }

        public ProductionSaveState CaptureState() => new ProductionSaveState(LastCalendarTick,
            Food.RemainderTicks, Food.TotalConsumed, Food.TotalShortage, Targets,
            sites.Values.Select(x => x.CaptureState()), tasks.Values.Where(x => !x.IsTerminal).Select(x => x.CaptureState()),
            producedTotals.Where(x => x.Value > 0).Select(x => new ResourceAmount(x.Key, new ResourceQuantity(x.Value))),
            inventories.CaptureStates(), Ecology?.CaptureState(), Industry?.CaptureState());

        public void RestoreState(ProductionSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var restoredSites = new Dictionary<StableEntityId, ProductionSite>();
            foreach (var siteState in state.Sites)
            {
                var site = ProductionSite.Restore(siteState);
                if (recipes.Get(site.RecipeId).SiteKind != site.Kind || !restoredSites.TryAdd(site.Id, site))
                    throw new ArgumentException("Invalid production site snapshot.", nameof(state));
            }
            var restoredTasks = new Dictionary<StableEntityId, ProductionTask>();
            var restoredWorkerTasks = new Dictionary<StableEntityId, StableEntityId>();
            foreach (var taskState in state.Tasks)
            {
                var task = ProductionTask.Restore(taskState);
                if (task.IsTerminal || !restoredSites.TryGetValue(task.SiteId, out var taskSite) ||
                    recipes.Get(task.RecipeId).Id != task.RecipeId || taskSite.RecipeId != task.RecipeId ||
                    !restoredTasks.TryAdd(task.Id, task) ||
                    !restoredWorkerTasks.TryAdd(task.WorkerId, task.Id))
                    throw new ArgumentException("Invalid or duplicate production task snapshot.", nameof(state));
            }
            inventories.ReplaceAll(state.Inventories, resources);
            sites.Clear(); foreach (var pair in restoredSites) sites.Add(pair.Key, pair.Value);
            orderedSites.Clear(); orderedSites.AddRange(restoredSites.Values.OrderBy(x => x.Id));
            tasks.Clear(); foreach (var pair in restoredTasks) tasks.Add(pair.Key, pair.Value);
            workerTasks.Clear(); foreach (var pair in restoredWorkerTasks) workerTasks.Add(pair.Key, pair.Value);
            targets.Clear(); foreach (var target in state.Targets) targets.Add(target.ResourceId, target.Target);
            foreach (var crop in new[] { "carrot", "cabbage" })
                if (resources.Contains(new ResourceId(crop))) targets.TryAdd(new ResourceId(crop), 20);
            var carcass = new ResourceId("carcass");
            if (resources.Contains(carcass) && !targets.ContainsKey(carcass)) targets.Add(carcass, 4);
            producedTotals.Clear(); foreach (var amount in state.ProducedTotals) producedTotals.Add(amount.ResourceId, amount.Quantity.Units);
            LastCalendarTick = state.LastCalendarTick;
            Food.Restore(state.FoodRemainderTicks, state.FoodConsumed, state.FoodShortage);
            Ecology?.RebindSites(orderedSites.Where(site => site.Kind == ProductionSiteKind.NaturalSource));
            Ecology?.RestoreState(state.NaturalLifecycles, state.LastCalendarTick);
            Industry?.RestoreState(state.Industry);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)LastCalendarTick));
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)Food.RemainderTicks));
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)Food.TotalConsumed));
            DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)Food.TotalShortage));
            foreach (var site in sites.Values.OrderBy(x => x.Id))
            {
                AddId(ref hash, site.Id);
                AddText(ref hash, site.RecipeId.ToString());
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)site.RemainingAmount));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)site.GrowthProgressTicks));
                DeterministicKeyedRandom.Add(ref hash, (ulong)site.CropPhase);
                if (site.OwnerBuildingId.IsValid) AddId(ref hash, site.OwnerBuildingId);
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)site.QueuedBatches));
                DeterministicKeyedRandom.Add(ref hash, site.Paused ? 1UL : 0UL);
            }
            foreach (var definition in resources.All)
            {
                AddText(ref hash, definition.Id.ToString());
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)GetCanonicalTotal(definition.Id)));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)GetProducedTotal(definition.Id)));
            }
            return hash;
        }

        private void AdvanceAbstractWorker(StableEntityId workerId, long budgetTicks, long calendarTick)
        {
            var guard = 0;
            while (budgetTicks > 0 && guard++ < 256)
            {
                if (!workerTasks.TryGetValue(workerId, out var taskId) || !tasks.TryGetValue(taskId, out var task) || task.IsTerminal)
                {
                    if (!TryReserve(workerId, default, out var job)) return;
                    task = job.Task;
                }
                var remainingWorkTicks = task.RequiredWorkTicks - task.AccumulatedWorkTicks;
                var effectiveBudgetTicks = GetEffectiveWorkTicks(task, budgetTicks, calendarTick);
                if (effectiveBudgetTicks < remainingWorkTicks)
                {
                    task.AddWork(effectiveBudgetTicks);
                    return;
                }

                var rawTicksUsed = GetRawWorkTicksRequired(task, remainingWorkTicks, calendarTick);
                var completion = AdvanceTaskAt(task.Id, TimeSpan.FromTicks(rawTicksUsed), calendarTick);
                if (!completion.Completed) return;
                budgetTicks -= rawTicksUsed;
                if (completion.ProducedOutput && !UsesGroundOutputs && TryFindStorage(completion.ResourceId, completion.Units, default,
                        out var destination, out _))
                    TryDeposit(workerId, destination, completion.ResourceId, completion.Units);
            }
        }

        private long GetEffectiveWorkTicks(ProductionTask task, long rawTicks, long calendarTick)
        {
            var workTicks = rawTicks;
            if (capabilities != null)
                workTicks = ApplyPerThousand(workTicks, capabilities.GetCapabilities(task.WorkerId).WorkPerThousand);
            var recipe = recipes.Get(task.RecipeId);
            if (weather != null && recipe.WeatherExposure == ProductionWeatherExposure.Outdoor)
            {
                var modifiers = weather.GetWeatherAtCalendarTick(calendarTick).Modifiers;
                workTicks = ApplyPerThousand(workTicks, modifiers.OutdoorWorkPerThousand);
                if (recipe.SeasonalWildSource)
                    workTicks = ApplyPerThousand(workTicks, modifiers.WildFoodGatheringPerThousand);
            }
            return workTicks;
        }

        private long GetRawWorkTicksRequired(ProductionTask task, long effectiveTicks, long calendarTick)
        {
            if (effectiveTicks == 0) return 0;
            var rawTicks = effectiveTicks;
            var recipe = recipes.Get(task.RecipeId);
            if (weather != null && recipe.WeatherExposure == ProductionWeatherExposure.Outdoor)
            {
                var modifiers = weather.GetWeatherAtCalendarTick(calendarTick).Modifiers;
                if (recipe.SeasonalWildSource)
                    rawTicks = InvertPerThousand(rawTicks, modifiers.WildFoodGatheringPerThousand);
                rawTicks = InvertPerThousand(rawTicks, modifiers.OutdoorWorkPerThousand);
            }
            if (capabilities != null)
                rawTicks = InvertPerThousand(rawTicks, capabilities.GetCapabilities(task.WorkerId).WorkPerThousand);
            return rawTicks;
        }

        private static long ApplyPerThousand(long ticks, int modifier) => checked(ticks * modifier / 1000L);

        private static long InvertPerThousand(long ticks, int modifier)
        {
            if (modifier <= 0) throw new InvalidOperationException("Zero-rate work cannot complete within a finite budget.");
            return checked((ticks * 1000L + modifier - 1L) / modifier);
        }

        private ProductionCompletion Complete(ProductionTask task)
        {
            var site = sites[task.SiteId];
            if (site.Kind == ProductionSiteKind.NaturalSource && site.IsDepleted)
            {
                task.Cancel();
                Release(task);
                return default;
            }
            var recipe = recipes.Get(task.RecipeId);
            var outputs = recipe.OutputsFor(site);
            var outputOwner = site.Kind == ProductionSiteKind.BuildingWorkplace
                ? OutputOwner(site.OwnerBuildingId) : inventories.WorkerOwner(task.WorkerId);
            if (site.Kind == ProductionSiteKind.BuildingWorkplace &&
                (!inventories.TryGet(outputOwner, out var workplaceOutput) || !CanAddAll(workplaceOutput, outputs)))
                return default;
            if (site.Kind != ProductionSiteKind.BuildingWorkplace &&
                task.Kind != ProductionTaskKind.Plant && !UsesGroundOutputs &&
                !TrySelectOutputOwner(task.WorkerId, outputs, out outputOwner))
                return default;
            if (!TryConsumeInputs(recipe, site)) return default;
            site.CompleteWork(task.Kind, recipe.HarvestEntireSource);
            var produced = false;
            var outputId = default(ResourceId);
            long outputUnits = 0;
            if (task.Kind != ProductionTaskKind.Plant)
            {
                foreach (var output in outputs)
                {
                    if (site.Kind == ProductionSiteKind.BuildingWorkplace)
                    {
                        if (!inventories.Get(outputOwner).TryAdd(output.ResourceId, output.Quantity))
                            throw new InvalidOperationException("Production output buffer changed during a committed batch.");
                    }
                    else if (UsesGroundOutputs)
                    {
                        var pile = outputPiles.Create(output.ResourceId, output.Quantity,
                            outputCoordinate(site.Position));
                        if (!produced) outputOwner = pile.InventoryOwner;
                    }
                    else if (!inventories.Get(outputOwner).TryAdd(output.ResourceId, output.Quantity))
                        throw new InvalidOperationException("Production output owner changed during a committed batch.");
                    producedTotals.TryGetValue(output.ResourceId, out var old);
                    producedTotals[output.ResourceId] = checked(old + output.Quantity.Units);
                    if (stepTotals != null)
                        stepTotals[output.ResourceId] = checked(stepTotals[output.ResourceId] + output.Quantity.Units);
                    if (!produced)
                    {
                        produced = true;
                        outputId = output.ResourceId;
                        outputUnits = output.Quantity.Units;
                    }
                }
            }
            task.Complete();
            Release(task);
            return new ProductionCompletion(true, produced, outputId, outputUnits, outputOwner);
        }

        private void Release(ProductionTask task)
        {
            workerTasks.Remove(task.WorkerId);
        }

        private bool RecipeNeeded(ProductionRecipe recipe)
        {
            foreach (var output in recipe.Outputs)
            {
                if (!targets.TryGetValue(output.ResourceId, out var target) || target <= 0) continue;
                var current = stepTotals != null ? stepTotals[output.ResourceId] : GetCanonicalTotal(output.ResourceId);
                if (checked(current + ReservedOutput(output.ResourceId)) < target) return true;
            }
            return false;
        }

        private bool TrySelectSite(StableEntityId workerId, WorldPosition workerPosition,
            out ProductionSite selected, out ProductionTaskKind selectedKind)
        {
            selected = null;
            selectedKind = default;
            double bestDistance = 0d;
            foreach (var site in orderedSites)
            {
                if (!site.IsWorkAvailable || !IsSiteOperational(site.Id)) continue;
                var recipe = recipes.Get(site.RecipeId);
                if (!recipe.Enabled || !IsRecipeAvailable(recipe.Id)) continue;
                if (recipe.HarvestEntireSource && tasks.Values.Any(x => !x.IsTerminal && x.SiteId == site.Id)) continue;
                  if (!HasNaturalSourceCapacity(site)) continue;
                if (site.Kind == ProductionSiteKind.BuildingWorkplace &&
                    ActiveWorkersAt(site.OwnerBuildingId) >= site.MaximumWorkers) continue;
                var kind = TaskKindFor(site);
                if (site.Kind != ProductionSiteKind.BuildingWorkplace && !RecipeNeeded(recipe)) continue;
                if (kind != ProductionTaskKind.Plant && !CanPlaceOutputs(workerId, recipe, site)) continue;
                if (!InputsAvailable(recipe, site)) continue;
                var distance = workerPosition.DistanceSquared(site.Position);
                if (selected == null || distance < bestDistance ||
                    (distance.Equals(bestDistance) && site.Id.CompareTo(selected.Id) < 0))
                {
                    selected = site;
                    selectedKind = kind;
                    bestDistance = distance;
                }
            }
            return selected != null;
        }

          private bool HasNaturalSourceCapacity(ProductionSite site)
          {
              if (site.Kind != ProductionSiteKind.NaturalSource) return true;
              var reservedUnits = tasks.Values.LongCount(task => !task.IsTerminal && task.SiteId == site.Id);
              return reservedUnits < site.RemainingAmount;
          }

        private bool CanPlaceOutputs(StableEntityId workerId, ProductionRecipe recipe, ProductionSite site)
        {
            if (site.Kind == ProductionSiteKind.BuildingWorkplace)
                return inventories.TryGet(OutputOwner(site.OwnerBuildingId), out var output) &&
                    CanAddAll(output, recipe.OutputsFor(site));
            return UsesGroundOutputs || TrySelectOutputOwner(workerId, recipe.OutputsFor(site), out _);
        }

        private bool TrySelectOutputOwner(StableEntityId workerId, IReadOnlyList<ResourceAmount> outputs, out InventoryOwner owner)
        {
            owner = inventories.WorkerOwner(workerId);
            if (inventories.TryGet(owner, out var character) && CanAddAll(character, outputs)) return true;
            foreach (var inventory in inventories.All.OrderBy(x => x.Owner.Id))
            {
                if (inventory.Owner.Kind != InventoryOwnerKind.BuildingStorage || !CanAddAll(inventory, outputs)) continue;
                if (!buildings.TryGet(inventory.Owner.Id, out var building) || !building.IsOperational) continue;
                owner = inventory.Owner;
                return true;
            }
            owner = default;
            return false;
        }

        private static bool CanAddAll(ResourceInventory inventory, IReadOnlyList<ResourceAmount> outputs)
        {
            long total = 0;
            foreach (var output in outputs)
            {
                if (!inventory.CanAdd(output.ResourceId, output.Quantity)) return false;
                total = checked(total + output.Quantity.Units);
            }
            return total <= inventory.Capacity - inventory.TotalUnits;
        }

        private bool InputsAvailable(ProductionRecipe recipe, ProductionSite site)
        {
            if (recipe.Inputs.Count == 0) return true;
            if (site.Kind == ProductionSiteKind.BuildingWorkplace)
                return inventories.TryGet(InputOwner(site.OwnerBuildingId), out var input) &&
                    recipe.Inputs.All(amount => input.CanRemove(amount.ResourceId, amount.Quantity));
            return inventories.All.Any(inventory => recipe.Inputs.All(input => inventory.CanRemove(input.ResourceId, input.Quantity)));
        }

        private bool TryConsumeInputs(ProductionRecipe recipe, ProductionSite site)
        {
            if (recipe.Inputs.Count == 0) return true;
            var owner = site.Kind == ProductionSiteKind.BuildingWorkplace
                ? (inventories.TryGet(InputOwner(site.OwnerBuildingId), out var workplaceInput) ? workplaceInput : null)
                : inventories.All.OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id)
                .FirstOrDefault(inventory => recipe.Inputs.All(amount => inventory.CanRemove(amount.ResourceId, amount.Quantity)));
            if (owner == null || !recipe.Inputs.All(amount => owner.CanRemove(amount.ResourceId, amount.Quantity))) return false;
            foreach (var input in recipe.Inputs) owner.TryRemove(input.ResourceId, input.Quantity);
            return true;
        }

        private long ReservedOutput(ResourceId id)
        {
            long reserved = 0;
            foreach (var task in tasks.Values)
            {
                if (task.IsTerminal || task.Kind == ProductionTaskKind.Plant) continue;
                foreach (var output in recipes.Get(task.RecipeId).OutputsFor(sites[task.SiteId]))
                    if (output.ResourceId == id) reserved = checked(reserved + output.Quantity.Units);
            }
            return reserved;
        }

        private int ActiveWorkersAt(StableEntityId buildingId) => tasks.Values.Count(task => !task.IsTerminal &&
            sites.TryGetValue(task.SiteId, out var site) && site.Kind == ProductionSiteKind.BuildingWorkplace &&
            site.OwnerBuildingId == buildingId);

        private static ProductionTaskKind TaskKindFor(ProductionSite site) =>
            site.Kind == ProductionSiteKind.NaturalSource ? ProductionTaskKind.Gather :
            site.Kind == ProductionSiteKind.BuildingWorkplace ? ProductionTaskKind.Craft :
            site.CropPhase == CropPlotPhase.Unplanted ? ProductionTaskKind.Plant : ProductionTaskKind.Harvest;

        private static void AddId(ref ulong hash, StableEntityId id)
        {
            var bytes = id.ToGuid().ToByteArray();
            for (var i = 0; i < bytes.Length; i++) DeterministicKeyedRandom.AddByte(ref hash, bytes[i]);
        }

        private static void AddText(ref ulong hash, string text)
        {
            for (var i = 0; i < text.Length; i++) DeterministicKeyedRandom.Add(ref hash, text[i]);
        }
    }
}
