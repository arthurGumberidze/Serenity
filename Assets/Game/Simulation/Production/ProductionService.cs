using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.Resources;
using Game.Simulation.Tiers;
using Game.Domain.Research;

namespace Game.Simulation.Production
{
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
        private readonly ResourceId foodResource;
        private readonly long ticksPerPersonUnit;

        public FoodConsumptionService(ResourceInventoryRegistry inventories, ResourceId foodResource,
            TimeSpan ticksPerPersonUnit)
        {
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            if (!foodResource.IsValid) throw new ArgumentException("Food resource required.", nameof(foodResource));
            if (ticksPerPersonUnit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ticksPerPersonUnit));
            this.foodResource = foodResource;
            this.ticksPerPersonUnit = ticksPerPersonUnit.Ticks;
        }

        public long RemainderTicks { get; private set; }
        public long TotalConsumed { get; private set; }
        public long TotalShortage { get; private set; }
        public bool IsShortage => TotalShortage > 0;

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
            foreach (var inventory in inventories.All.OrderBy(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage ? 0 : 1)
                         .ThenBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
            {
                var available = inventory.GetAmount(foodResource).Units;
                var take = Math.Min(available, remaining);
                if (take > 0) inventory.TryRemove(foodResource, new ResourceQuantity(take));
                remaining -= take;
                if (remaining == 0) break;
            }
            TotalConsumed = checked(TotalConsumed + due - remaining);
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
        private readonly Dictionary<StableEntityId, StableEntityId> siteClaims = new Dictionary<StableEntityId, StableEntityId>();
        private readonly Dictionary<StableEntityId, StableEntityId> workerTasks = new Dictionary<StableEntityId, StableEntityId>();
        private readonly Dictionary<ResourceId, long> targets = new Dictionary<ResourceId, long>();
        private readonly Dictionary<ResourceId, long> producedTotals = new Dictionary<ResourceId, long>();
        private readonly ResourceId foodResource = new ResourceId("berries");
        private readonly IResearchUnlockView unlocks;
        private Dictionary<ResourceId, long> stepTotals;

        public ProductionService(ProductionCatalog recipeCatalog, ResourceCatalog resourceCatalog,
            ResourceInventoryRegistry inventoryRegistry, BuildingRegistry buildingRegistry,
            ResourceTransferService transferService, ProductionSettings productionSettings = null,
            IResearchUnlockView researchUnlocks = null)
        {
            recipes = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            transfers = transferService ?? throw new ArgumentNullException(nameof(transferService));
            settings = productionSettings ?? ProductionSettings.StoneAge;
            unlocks = researchUnlocks;
            resources.Get(foodResource);
            foreach (var target in settings.Targets)
            {
                resources.Get(target.ResourceId);
                if (!targets.TryAdd(target.ResourceId, target.Target))
                    throw new ArgumentException("Duplicate production target.", nameof(productionSettings));
            }
            Food = new FoodConsumptionService(inventories, foodResource, settings.FoodTicksPerPersonUnit);
        }

        public ProductionCatalog Recipes => recipes;
        public ProductionSettings Settings => settings;
        public FoodConsumptionService Food { get; }
        public int Population { get; set; }
        public long LastCalendarTick { get; private set; }
        public IReadOnlyList<ProductionSite> Sites => sites.Values.OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<ProductionTask> ActiveTasks => tasks.Values.Where(x => !x.IsTerminal).OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<ResourceStockTarget> Targets => targets.OrderBy(x => x.Key)
            .Select(x => new ResourceStockTarget(x.Key, x.Value)).ToArray();

        public void AddSite(ProductionSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var recipe = recipes.Get(site.RecipeId);
            if (recipe.SiteKind != site.Kind) throw new ArgumentException("Site kind does not match its recipe.", nameof(site));
            if (!sites.TryAdd(site.Id, site)) throw new InvalidOperationException("Production site already exists.");
            orderedSites.Add(site);
            orderedSites.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public bool TryGetSite(StableEntityId id, out ProductionSite site) => sites.TryGetValue(id, out site);
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

        public bool IsRecipeAvailable(ProductionRecipeId id) => unlocks == null || unlocks.IsProductionRecipeUnlocked(id);

        public bool TryReserve(StableEntityId workerId, WorldPosition workerPosition, out ProductionJob job)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            job = default;
            if (workerTasks.TryGetValue(workerId, out var existingId) && tasks.TryGetValue(existingId, out var existing))
            {
                if (existing.IsTerminal) return false;
                job = new ProductionJob(existing, sites[existing.SiteId].Position);
                return true;
            }
            if (!inventories.TryGet(new InventoryOwner(InventoryOwnerKind.Character, workerId), out _)) return false;

            if (!TrySelectSite(workerId, workerPosition, out var selected, out var selectedKind)) return false;
            var selectedRecipe = recipes.Get(selected.RecipeId);
            var task = ProductionTask.Create(workerId, selected.Id, selected.RecipeId, selectedKind,
                selectedRecipe.WorkRequiredTicks);
            tasks.Add(task.Id, task);
            siteClaims.Add(selected.Id, task.Id);
            workerTasks.Add(workerId, task.Id);
            job = new ProductionJob(task, selected.Position);
            return true;
        }

        public bool HasEligibleWork(StableEntityId workerId, WorldPosition workerPosition)
        {
            if (!workerId.IsValid) return false;
            if (workerTasks.TryGetValue(workerId, out var taskId) && tasks.TryGetValue(taskId, out var task) && !task.IsTerminal)
                return true;
            // Tier 1 keeps one physical producer at a time in this MVP slice so logistics/rest work remains observable.
            // Aggregate Tier 2/Tier 3 workers use TryReserve directly and are not subject to this presentation policy.
            if (tasks.Values.Any(candidate => !candidate.IsTerminal)) return false;
            return inventories.TryGet(new InventoryOwner(InventoryOwnerKind.Character, workerId), out _) &&
                TrySelectSite(workerId, workerPosition, out _, out _);
        }

        public bool IsTaskActive(StableEntityId taskId) => tasks.TryGetValue(taskId, out var task) && !task.IsTerminal;

        public ProductionCompletion AdvanceTask(StableEntityId taskId, TimeSpan work)
        {
            if (work < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(work));
            if (!tasks.TryGetValue(taskId, out var task) || task.IsTerminal) return default;
            task.AddWork(work.Ticks);
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
                    building.ConstructionState != ConstructionState.Completed) continue;
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
            var source = new InventoryOwner(InventoryOwnerKind.Character, workerId);
            return transfers.TransferExact(source, destination, id, new ResourceQuantity(units)) == TransferFailureReason.None;
        }

        public void AdvanceTo(long targetCalendarTick, IEnumerable<StableEntityId> abstractWorkers)
        {
            if (targetCalendarTick < LastCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(targetCalendarTick), "Production time cannot move backwards.");
            var workers = (abstractWorkers ?? throw new ArgumentNullException(nameof(abstractWorkers))).Distinct().OrderBy(x => x).ToArray();
            var firstStep = LastCalendarTick / settings.FixedStep.Ticks + 1L;
            var finalStep = targetCalendarTick / settings.FixedStep.Ticks;
            for (var step = firstStep; step <= finalStep; step++)
            {
                foreach (var site in orderedSites) site.AdvanceGrowth(settings.FixedStep.Ticks);
                Food.Advance(settings.FixedStep, Population);
                stepTotals = new Dictionary<ResourceId, long>();
                foreach (var definition in resources.All) stepTotals.Add(definition.Id, GetCanonicalTotal(definition.Id));
                try
                {
                    for (var i = 0; i < workers.Length; i++) AdvanceAbstractWorker(workers[i], settings.FixedStep.Ticks);
                }
                finally { stepTotals = null; }
            }
            LastCalendarTick = targetCalendarTick;
        }

        public ProductionSaveState CaptureState() => new ProductionSaveState(LastCalendarTick,
            Food.RemainderTicks, Food.TotalConsumed, Food.TotalShortage, Targets,
            sites.Values.Select(x => x.CaptureState()), tasks.Values.Where(x => !x.IsTerminal).Select(x => x.CaptureState()),
            producedTotals.Where(x => x.Value > 0).Select(x => new ResourceAmount(x.Key, new ResourceQuantity(x.Value))),
            inventories.CaptureStates());

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
            var restoredSiteClaims = new Dictionary<StableEntityId, StableEntityId>();
            var restoredWorkerTasks = new Dictionary<StableEntityId, StableEntityId>();
            foreach (var taskState in state.Tasks)
            {
                var task = ProductionTask.Restore(taskState);
                if (task.IsTerminal || !restoredSites.TryGetValue(task.SiteId, out var taskSite) ||
                    recipes.Get(task.RecipeId).Id != task.RecipeId || taskSite.RecipeId != task.RecipeId ||
                    !restoredTasks.TryAdd(task.Id, task) ||
                    !restoredSiteClaims.TryAdd(task.SiteId, task.Id) ||
                    !restoredWorkerTasks.TryAdd(task.WorkerId, task.Id))
                    throw new ArgumentException("Invalid or duplicate production task snapshot.", nameof(state));
            }
            inventories.ReplaceAll(state.Inventories, resources);
            sites.Clear(); foreach (var pair in restoredSites) sites.Add(pair.Key, pair.Value);
            orderedSites.Clear(); orderedSites.AddRange(restoredSites.Values.OrderBy(x => x.Id));
            tasks.Clear(); foreach (var pair in restoredTasks) tasks.Add(pair.Key, pair.Value);
            siteClaims.Clear(); foreach (var pair in restoredSiteClaims) siteClaims.Add(pair.Key, pair.Value);
            workerTasks.Clear(); foreach (var pair in restoredWorkerTasks) workerTasks.Add(pair.Key, pair.Value);
            targets.Clear(); foreach (var target in state.Targets) targets.Add(target.ResourceId, target.Target);
            producedTotals.Clear(); foreach (var amount in state.ProducedTotals) producedTotals.Add(amount.ResourceId, amount.Quantity.Units);
            LastCalendarTick = state.LastCalendarTick;
            Food.Restore(state.FoodRemainderTicks, state.FoodConsumed, state.FoodShortage);
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
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)site.RemainingAmount));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)site.GrowthProgressTicks));
                DeterministicKeyedRandom.Add(ref hash, (ulong)site.CropPhase);
            }
            foreach (var definition in resources.All)
            {
                AddText(ref hash, definition.Id.ToString());
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)GetCanonicalTotal(definition.Id)));
                DeterministicKeyedRandom.Add(ref hash, unchecked((ulong)GetProducedTotal(definition.Id)));
            }
            return hash;
        }

        private void AdvanceAbstractWorker(StableEntityId workerId, long budgetTicks)
        {
            var guard = 0;
            while (budgetTicks > 0 && guard++ < 256)
            {
                if (!workerTasks.TryGetValue(workerId, out var taskId) || !tasks.TryGetValue(taskId, out var task) || task.IsTerminal)
                {
                    if (!TryReserve(workerId, default, out var job)) return;
                    task = job.Task;
                }
                var before = task.AccumulatedWorkTicks;
                var completion = AdvanceTask(task.Id, TimeSpan.FromTicks(budgetTicks));
                var applied = task.AccumulatedWorkTicks - before;
                budgetTicks -= applied;
                if (!completion.Completed) return;
                if (completion.ProducedOutput && TryFindStorage(completion.ResourceId, completion.Units, default,
                        out var destination, out _))
                    TryDeposit(workerId, destination, completion.ResourceId, completion.Units);
            }
        }

        private ProductionCompletion Complete(ProductionTask task)
        {
            var site = sites[task.SiteId];
            var recipe = recipes.Get(task.RecipeId);
            var outputOwner = new InventoryOwner(InventoryOwnerKind.Character, task.WorkerId);
            if (task.Kind != ProductionTaskKind.Plant && !TrySelectOutputOwner(task.WorkerId, recipe, out outputOwner))
                return default;
            if (!TryConsumeInputs(recipe)) return default;
            site.CompleteWork(task.Kind);
            var produced = false;
            var outputId = default(ResourceId);
            long outputUnits = 0;
            if (task.Kind != ProductionTaskKind.Plant)
            {
                var destination = inventories.Get(outputOwner);
                foreach (var output in recipe.Outputs)
                {
                    if (!destination.TryAdd(output.ResourceId, output.Quantity))
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
            siteClaims.Remove(task.SiteId);
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
                if (!site.IsWorkAvailable || siteClaims.ContainsKey(site.Id)) continue;
                var recipe = recipes.Get(site.RecipeId);
                if (!recipe.Enabled || !IsRecipeAvailable(recipe.Id)) continue;
                var kind = TaskKindFor(site);
                if (!RecipeNeeded(recipe)) continue;
                if (kind != ProductionTaskKind.Plant && !CanPlaceOutputs(workerId, recipe)) continue;
                if (!InputsAvailable(recipe)) continue;
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

        private bool CanPlaceOutputs(StableEntityId workerId, ProductionRecipe recipe) =>
            TrySelectOutputOwner(workerId, recipe, out _);

        private bool TrySelectOutputOwner(StableEntityId workerId, ProductionRecipe recipe, out InventoryOwner owner)
        {
            owner = new InventoryOwner(InventoryOwnerKind.Character, workerId);
            if (inventories.TryGet(owner, out var character) && CanAddAll(character, recipe.Outputs)) return true;
            foreach (var inventory in inventories.All.OrderBy(x => x.Owner.Id))
            {
                if (inventory.Owner.Kind != InventoryOwnerKind.BuildingStorage || !CanAddAll(inventory, recipe.Outputs)) continue;
                if (!buildings.TryGet(inventory.Owner.Id, out var building) || building.ConstructionState != ConstructionState.Completed) continue;
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

        private bool InputsAvailable(ProductionRecipe recipe)
        {
            if (recipe.Inputs.Count == 0) return true;
            return inventories.All.Any(inventory => recipe.Inputs.All(input => inventory.CanRemove(input.ResourceId, input.Quantity)));
        }

        private bool TryConsumeInputs(ProductionRecipe recipe)
        {
            if (recipe.Inputs.Count == 0) return true;
            var owner = inventories.All.OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id)
                .FirstOrDefault(inventory => recipe.Inputs.All(input => inventory.CanRemove(input.ResourceId, input.Quantity)));
            if (owner == null) return false;
            foreach (var input in recipe.Inputs) owner.TryRemove(input.ResourceId, input.Quantity);
            return true;
        }

        private long ReservedOutput(ResourceId id)
        {
            long reserved = 0;
            foreach (var task in tasks.Values)
            {
                if (task.IsTerminal || task.Kind == ProductionTaskKind.Plant) continue;
                foreach (var output in recipes.Get(task.RecipeId).Outputs)
                    if (output.ResourceId == id) reserved = checked(reserved + output.Quantity.Units);
            }
            return reserved;
        }

        private static ProductionTaskKind TaskKindFor(ProductionSite site) =>
            site.Kind == ProductionSiteKind.NaturalSource ? ProductionTaskKind.Gather :
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
