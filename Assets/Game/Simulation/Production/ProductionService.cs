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
using Game.Domain.World;
using Game.Domain.Campaign;

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
            TimeSpan.FromDays(2), TimeSpan.FromHours(12), new[]
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
    public sealed partial class ProductionService
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
        private readonly ResourceId[] preparedFoodResources;
        private readonly List<StableEntityId> orderedWorkerBuffer = new List<StableEntityId>();
        private readonly IResearchUnlockView unlocks;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly IWeatherModifierProvider weather;
        private readonly Dictionary<StableEntityId, LivestockPenPolicyState> livestockPolicies = new();
        private readonly Dictionary<StableEntityId, HuntingLodgePolicyState> huntingPolicies = new();
        private readonly Dictionary<Tuple<InventoryOwner, ResourceId>, long> foodSpoilage = new();
        private readonly Dictionary<StableEntityId, long> lastFireFuelTick = new();
        private readonly HashSet<StableEntityId> heatedBuildings = new();
        private readonly HashSet<StableEntityId> mountedResidents = new();
        private static readonly IReadOnlyDictionary<string, int> ShelfLifeDays = new Dictionary<string, int>
        {
            ["fish"] = 1, ["meat"] = 1, ["milk"] = 1, ["cooked_fish"] = 2,
            ["cooked_meat"] = 3, ["cooked_berries"] = 4, ["berries"] = 5,
            ["mushrooms"] = 3, ["eggs"] = 7, ["cabbage"] = 10, ["carrot"] = 14,
            ["dried_meat"] = 30
        };
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
            var edible = new[] { "cooked_berries", "cooked_fish", "cooked_meat", "dried_meat" }
                .Select(id => new ResourceId(id)).Where(resources.Contains).ToArray();
            preparedFoodResources = edible;
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
        public const int HorseMovementPerThousand = 1400;

        /// <summary>
        /// Living adult horses are automatically equipped one-to-one to the deterministic resident roster.
        /// The assignment is derived from saved people/animals, so death, birth and load cannot duplicate a mount.
        /// </summary>
        public void ReconcileHorseRiders(IEnumerable<StableEntityId> eligibleResidents)
        {
            var riders = (eligibleResidents ?? Array.Empty<StableEntityId>()).Distinct().OrderBy(x => x)
                .Where(x => capabilities == null ||
                    capabilities.GetCapabilities(x).IsAdult && !capabilities.GetCapabilities(x).IsIncapacitated)
                .ToArray();
            var horses = Animals.States.Count(x => x.Domestic && x.Alive && x.Species == "horse" &&
                LastCalendarTick - x.BornTick >= TimeSpan.TicksPerDay * Animals.Policy.LivestockMaturityDays);
            mountedResidents.Clear();
            for (var i = 0; i < Math.Min(horses, riders.Length); i++) mountedResidents.Add(riders[i]);
        }

        public int GetResidentMovementPerThousand(StableEntityId residentId) =>
            mountedResidents.Contains(residentId) ? HorseMovementPerThousand : 1000;
        public long PreparedFoodTarget => checked((long)Population * 2L * 30L);
        public long PreparedFoodTotal => preparedFoodResources.Sum(GetCanonicalTotal);
        public IReadOnlyList<LivestockPenPolicyState> LivestockPolicies => livestockPolicies.Values
            .OrderBy(x => x.BuildingId).ToArray();
        public IReadOnlyList<HuntingLodgePolicyState> HuntingPolicies => huntingPolicies.Values
            .OrderBy(x => x.BuildingId).ToArray();
        public IReadOnlyCollection<StableEntityId> HeatedBuildings => heatedBuildings;

        public InventoryOwner InputOwner(StableEntityId buildingId) =>
            new InventoryOwner(InventoryOwnerKind.ProductionInput, buildingId);
        public InventoryOwner OutputOwner(StableEntityId buildingId) =>
            new InventoryOwner(InventoryOwnerKind.ProductionOutput, buildingId);

        public void AttachNaturalResources(ResourceEcologyService ecology, IndustrialExtractionService industry)
        {
            Ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
            Industry = industry ?? throw new ArgumentNullException(nameof(industry));
        }

        public AnimalHealthRegistry Animals { get; } = new AnimalHealthRegistry();
        public Func<StableEntityId, ProductionSite, WorldPosition> WorkPosition { get; set; }
        public Func<StableEntityId, ProductionSite, WorldPosition> OutputPosition { get; set; }
        public Func<StableEntityId,bool> ManualFishingAccess { get; set; }
        public Func<StableEntityId, long, bool> BuildingWorkAllowed { get; set; }
        public Func<StableEntityId, int> BuildingWorkPriority { get; set; }
        public Func<StableEntityId, bool> BuildingHasAssignedWorker { get; set; }
        public Func<StableEntityId, StableEntityId?> AssignedWorkplace { get; set; }
        private ProductionJob CreateJob(ProductionTask task, ProductionSite site) =>
            new ProductionJob(task, WorkPosition != null ? WorkPosition(task.WorkerId, site) : site.Position);

        // Each completed pen starts empty. The player chooses exactly one founding species;
        // its saved identities make the choice permanent for that particular pen.
        public bool EstablishPasture(StableEntityId id, Game.Domain.Combat.CombatSideId requester, string species)
        {
            if (!Animals.Policy.DomesticSpecies.Contains(species) || Animals.AtHome(id).Count > 0 ||
                !buildings.TryGet(id, out var building) ||
                building.DefinitionId.FamilyId.ToString()!="animal_pasture" ||
                building.OwnerSideId != requester || !building.IsOperational ||
                !sites.TryGetValue(id, out var site) || site.Kind != ProductionSiteKind.PrimitiveCropPlot ||
                site.CropPhase != CropPlotPhase.Unplanted || tasks.Values.Any(x => !x.IsTerminal && x.SiteId == id))
                return false;
            EnsurePastureInventory(id);
            AddLivestock(id,site.Position,species,true,LastCalendarTick,0);
            AddLivestock(id,site.Position,species,true,LastCalendarTick,1);
            AddLivestock(id,site.Position,species,false,LastCalendarTick,2);
            AddLivestock(id,site.Position,species,false,LastCalendarTick,3);
            var capacity = PastureCapacity(building);
            livestockPolicies[id] = new LivestockPenPolicyState(id, species,
                LivestockOperationMode.Balanced, capacity, LastCalendarTick, 8);
            return true;
        }

        public bool SetLivestockPolicy(StableEntityId buildingId, LivestockOperationMode mode, int populationLimit)
        {
            if (!livestockPolicies.TryGetValue(buildingId, out var current) ||
                !buildings.TryGet(buildingId, out var building)) return false;
            var maximum = PastureCapacity(building);
            livestockPolicies[buildingId] = new LivestockPenPolicyState(buildingId, current.Species, mode,
                Math.Max(4, Math.Min(maximum, populationLimit)), current.LastProductTick, current.WaterUnits);
            return true;
        }

        public bool ChangeLivestockSpecies(StableEntityId buildingId, string species)
        {
            if (!livestockPolicies.TryGetValue(buildingId, out var current) ||
                !Animals.Policy.DomesticSpecies.Contains(species) || current.Species == species ||
                !buildings.TryGet(buildingId, out var building) || !building.IsOperational ||
                !sites.TryGetValue(buildingId, out var homeSite)) return false;
            if (Animals.AtHome(buildingId).Any(x => x.Alive && x.Species == "horse"))
                return false; // Horses are never slaughtered merely to change a pen's species.
            foreach (var animal in Animals.AtHome(buildingId).Where(x => x.Alive).OrderBy(x => x.Id).ToArray())
                SlaughterLivestock(animal.Id, true);
            AddLivestock(buildingId, homeSite.Position, species, true, LastCalendarTick, 0);
            AddLivestock(buildingId, homeSite.Position, species, true, LastCalendarTick, 1);
            AddLivestock(buildingId, homeSite.Position, species, false, LastCalendarTick, 2);
            AddLivestock(buildingId, homeSite.Position, species, false, LastCalendarTick, 3);
            livestockPolicies[buildingId] = new LivestockPenPolicyState(buildingId, species, current.Mode,
                Math.Min(current.PopulationLimit, PastureCapacity(building)), LastCalendarTick, current.WaterUnits);
            return true;
        }

        public bool CanSlaughterLivestock(StableEntityId animalId)
        {
            if (!Animals.TryGet(animalId, out var animal) || !animal.Domestic || !animal.Alive ||
                animal.Species == "horse") return false;
            return !ProtectedBreedingAnimals(animal.Home).Contains(animalId);
        }

        public bool SlaughterLivestock(StableEntityId animalId) => SlaughterLivestock(animalId, false);

        private bool SlaughterLivestock(StableEntityId animalId, bool replaceWholeHerd)
        {
            if (!Animals.TryGet(animalId, out var animal) || !animal.Domestic || !animal.Alive ||
                animal.Species == "horse" || !replaceWholeHerd && !CanSlaughterLivestock(animalId)) return false;
            var inventory = EnsurePastureInventory(animal.Home);
            Animals.Damage(animalId, animal.Health);
            Animals.Harvest(animalId);
            AddIfPresent(inventory, "dressed_carcass", LivestockMeatCarcasses(animal.Species));
            if (new[] { "sheep", "cow", "pig" }.Contains(animal.Species)) AddIfPresent(inventory, "raw_hide", 1);
            if (new[] { "goat", "cow", "pig", "sheep" }.Contains(animal.Species)) AddIfPresent(inventory, "bone", 1);
            return true;
        }

        public bool SetHuntingPolicy(StableEntityId buildingId, HuntingOperationMode mode, long targetCarcasses)
        {
            if (!buildings.TryGet(buildingId, out var building) ||
                building.DefinitionId.FamilyId.ToString() != "hunting_camp") return false;
            var last = huntingPolicies.TryGetValue(buildingId, out var current)
                ? current.LastRestorationTick : LastCalendarTick;
            huntingPolicies[buildingId] = new HuntingLodgePolicyState(buildingId, mode, targetCarcasses, last);
            return true;
        }

        public int AdvanceHuntingPopulation(long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var restored = 0;
            foreach (var current in huntingPolicies.Values.OrderBy(x => x.BuildingId).ToArray())
            {
                if (current.Mode != HuntingOperationMode.RestorePopulation ||
                    calendarTick - current.LastRestorationTick < TimeSpan.FromDays(3).Ticks) continue;
                var lodge = orderedSites.Where(x => x.OwnerBuildingId == current.BuildingId)
                    .OrderBy(x => x.Id).FirstOrDefault();
                if (lodge == null) continue;
                var candidate = orderedSites.Where(x => x.RecipeId.ToString() == "hunt_animal" &&
                        x.Position.DistanceSquared(lodge.Position) <= 40000f &&
                        Animals.TryGet(x.Id, out var animal) && !animal.Domestic && !animal.Alive)
                    .OrderBy(x => x.Position.DistanceSquared(lodge.Position)).ThenBy(x => x.Id).FirstOrDefault();
                if (candidate != null && Animals.RestoreWildPopulation(candidate.Id))
                {
                    if (candidate.IsDepleted) candidate.RespawnNatural(candidate.Position, 1);
                    restored++;
                }
                huntingPolicies[current.BuildingId] = new HuntingLodgePolicyState(current.BuildingId,
                    current.Mode, current.TargetCarcasses, calendarTick);
            }
            return restored;
        }

        public bool IsPastureSite(StableEntityId id) => buildings.TryGet(id, out var building) &&
            building.DefinitionId.FamilyId.ToString() == "animal_pasture";

        private void AddLivestock(StableEntityId home, WorldPosition position, string species, bool male, long tick, int sequence)
        {
            var id = DeterministicStableId.Create(0, "livestock:" + home, sequence);
            while (Animals.TryGet(id, out _))
                id = DeterministicStableId.Create(0, "livestock:" + home, ++sequence);
            Animals.RegisterDomestic(id, species, home, male, tick);
            var site = ProductionSite.Restore(new ProductionSiteState(id, new ProductionRecipeId("hunt_animal"),
                ProductionSiteKind.NaturalSource, position, 1, 1, CropPlotPhase.Unplanted, 0, 0));
            AddSite(site, species, true);
        }

        private void AdvanceLivestock(long tick)
        {
            foreach (var home in Animals.Pastures)
            {
                if (!buildings.TryGet(home, out var building) || !building.IsOperational ||
                    !sites.TryGetValue(home, out var site)) continue;
                var herd = Animals.AtHome(home);
                var alive = herd.Where(x => x.Alive).ToArray();
                var capacity = PastureCapacity(building);
                if (!livestockPolicies.TryGetValue(home, out var policy))
                {
                    var species = alive.Select(x => x.Species).FirstOrDefault();
                    if (species == null) continue;
                    policy = new LivestockPenPolicyState(home, species, LivestockOperationMode.Balanced,
                        capacity, tick, 0);
                    livestockPolicies.Add(home, policy);
                }
                var dayElapsed = tick - policy.LastProductTick >= TimeSpan.TicksPerDay;
                var fed = !dayElapsed || ConsumeLivestockFeed(home, alive.Length);
                var watered = !dayElapsed || policy.WaterUnits >= alive.Length;
                if (dayElapsed)
                {
                    if (fed && watered && policy.Mode != LivestockOperationMode.Breeding)
                        ProduceLivestockProducts(home, policy.Species, alive.Length);
                    if (!fed || !watered)
                        foreach (var animal in alive)
                            Animals.Damage(animal.Id, Math.Min(5, animal.Health));
                    policy = new LivestockPenPolicyState(home, policy.Species, policy.Mode,
                        Math.Min(policy.PopulationLimit, capacity), tick,
                        watered ? policy.WaterUnits - alive.Length : policy.WaterUnits);
                    livestockPolicies[home] = policy;
                }
                if (policy.Mode == LivestockOperationMode.Balanced && alive.Length > policy.PopulationLimit)
                    AutoSlaughter(home, alive, policy.PopulationLimit, tick);
                alive = Animals.AtHome(home).Where(x => x.Alive).ToArray();
                if (!fed || !watered || alive.Length >= policy.PopulationLimit || alive.Length == 0) continue;
                if (policy.Mode == LivestockOperationMode.Products && alive.Length >= 4) continue;
                foreach(var group in alive.GroupBy(x=>x.Species))
                {
                    var groupAnimals=group.ToArray();
                    if(tick-groupAnimals.Max(x=>x.BornTick)<TimeSpan.TicksPerDay*Animals.Policy.LivestockBirthDays)continue;
                    var mature=groupAnimals.Where(x=>tick-x.BornTick>=TimeSpan.TicksPerDay*Animals.Policy.LivestockMaturityDays).ToArray();
                    if(!mature.Any(x=>x.Male)||!mature.Any(x=>!x.Male))continue;
                    var sequence=herd.Count;
                    AddLivestock(home,site.Position,group.Key,groupAnimals.Length%2==0,tick,sequence);
                    break;
                }
            }
        }

        private int PastureCapacity(Building building) =>
            Math.Min(Animals.Policy.PastureCapacity, Math.Max(4, building.PlacementCells.Count / 8));

        private ResourceInventory EnsurePastureInventory(StableEntityId buildingId)
        {
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, buildingId);
            if (inventories.TryGet(owner, out var existing)) return existing;
            var created = new ResourceInventory(owner, resources, 256);
            inventories.Add(created);
            return created;
        }

        private bool ConsumeLivestockFeed(StableEntityId home, int animals)
        {
            if (animals <= 0) return true;
            var inventory = EnsurePastureInventory(home);
            var concentrated = new ResourceId("animal_feed");
            var normalNeeded = animals;
            if (resources.Contains(concentrated))
            {
                var feed = Math.Min((animals + 2) / 3, inventory.GetAmount(concentrated).Units);
                if (feed > 0) inventory.TryRemove(concentrated, new ResourceQuantity(feed));
                normalNeeded = Math.Max(0, animals - (int)feed * 3);
            }
            foreach (var id in new[] { "wheat", "plant_fiber", "berries" })
            {
                var resource = new ResourceId(id);
                if (!resources.Contains(resource) || normalNeeded == 0) continue;
                var take = Math.Min(normalNeeded, inventory.GetAmount(resource).Units);
                if (take > 0) inventory.TryRemove(resource, new ResourceQuantity(take));
                normalNeeded -= (int)take;
            }
            return normalNeeded == 0;
        }

        private void ProduceLivestockProducts(StableEntityId home, string species, int animals)
        {
            if (animals <= 0) return;
            var inventory = EnsurePastureInventory(home);
            switch (species)
            {
                case "sheep": AddIfPresent(inventory, "wool", Math.Max(1, animals / 2)); break;
                case "cow": AddIfPresent(inventory, "milk", Math.Max(1, animals)); break;
                case "goat":
                    AddIfPresent(inventory, "milk", Math.Max(1, animals / 2));
                    AddIfPresent(inventory, "bone", Math.Max(1, animals / 4));
                    break;
                case "chicken": AddIfPresent(inventory, "eggs", Math.Max(1, animals)); break;
                case "duck":
                    AddIfPresent(inventory, "eggs", Math.Max(1, animals / 2));
                    AddIfPresent(inventory, "feathers", Math.Max(1, animals / 2));
                    break;
            }
        }

        private void AutoSlaughter(StableEntityId home, IReadOnlyCollection<AnimalVitalState> alive,
            int target, long tick)
        {
            var protectedIds = ProtectedBreedingAnimals(home);
            var removable = alive.Where(x => !protectedIds.Contains(x.Id) && x.Species != "horse" &&
                    tick - x.BornTick >= TimeSpan.TicksPerDay * Animals.Policy.LivestockMaturityDays)
                .OrderBy(x => (double)x.Health / x.Maximum).ThenBy(x => x.BornTick)
                .ThenByDescending(x => x.Male).ThenBy(x => x.Id).ToArray();
            var excess = Math.Max(0, alive.Count - Math.Max(4, target));
            for (var i = 0; i < Math.Min(excess, removable.Length); i++) SlaughterLivestock(removable[i].Id);
        }

        private HashSet<StableEntityId> ProtectedBreedingAnimals(StableEntityId home)
        {
            var alive = Animals.AtHome(home).Where(x => x.Alive).ToArray();
            return new HashSet<StableEntityId>(alive.Where(x => x.Male).OrderBy(x => x.BornTick).ThenBy(x => x.Id)
                .Take(2).Concat(alive.Where(x => !x.Male).OrderBy(x => x.BornTick).ThenBy(x => x.Id).Take(2))
                .Select(x => x.Id));
        }

        private static int LivestockMeatCarcasses(string species) => species switch
        {
            "pig" => 3, "cow" => 2, "sheep" => 2, "goat" => 1,
            "chicken" => 1, "duck" => 1, _ => 1
        };

        private void AddIfPresent(ResourceInventory inventory, string resourceId, long units)
        {
            var id = new ResourceId(resourceId);
            if (units > 0 && resources.Contains(id)) inventory.TryAdd(id, new ResourceQuantity(units));
        }

        public void AddSite(ProductionSite site, string animalSpecies = "deer", bool domesticAnimal = false)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            var recipe = recipes.Get(site.RecipeId);
            if (recipe.SiteKind != site.Kind) throw new ArgumentException("Site kind does not match its recipe.", nameof(site));
            if (!sites.TryAdd(site.Id, site)) throw new InvalidOperationException("Production site already exists.");
            orderedSites.Add(site);
            orderedSites.Sort((left, right) => left.Id.CompareTo(right.Id));
            if (site.RecipeId.ToString() == "hunt_animal") Animals.Register(site.Id, animalSpecies, domesticAnimal);
            if (site.RecipeId.ToString() == "fish_water") site.SetContinuous(true);
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
            inventories.Get(inputOwner).EnsureCapacity(definition.Production.InputCapacity);
            inventories.Get(outputOwner).EnsureCapacity(definition.Production.OutputCapacity);
            var result = new List<ProductionSite>();
            foreach (var recipeId in definition.Production.RecipeIds)
            {
                var id = WorkplaceSiteId(buildingId, recipeId);
                if (!sites.TryGetValue(id, out var site))
                {
                    site = ProductionSite.CreateBuildingWorkplace(id, buildingId, recipeId, position,
                        definition.Workplace.MaximumWorkers);
                    AddSite(site);
                    if (new[] { "fish_camp", "dress_carcass", "butcher_carcass", "dry_meat", "cure_hide",
                            "cook_meat", "cook_fish", "cook_berries" }.Contains(recipeId.ToString()))
                        site.SetContinuous(true);
                    if (recipeId.ToString() == "fish_breed") { site.SetContinuous(true); site.SetPaused(true); }
                }
                site.IncreaseWorkplaceCapacity(definition.Workplace.MaximumWorkers);
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

        public int MaintainContinuousDeliveries(WorkManager work, WorkPriority priority = WorkPriority.High)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            var created = 0;
            foreach (var site in orderedSites.Where(x => x.Kind == ProductionSiteKind.BuildingWorkplace &&
                         x.Continuous && !x.Paused).OrderBy(x => x.OwnerBuildingId).ThenBy(x => x.Id))
            {
                var recipe = recipes.Get(site.RecipeId);
                var destination = InputOwner(site.OwnerBuildingId);
                if (!inventories.TryGet(destination, out var input)) continue;
                foreach (var amount in recipe.Inputs)
                {
                    var pendingDestination = work.Jobs.Where(job => !job.IsTerminal && job.Type == WorkJobType.Haul &&
                        job.ResourceId == amount.ResourceId && job.Destination.HasValue &&
                        job.Destination.Value.Kind == WorkTargetKind.ProductionInput &&
                        job.Destination.Value.EntityId == site.OwnerBuildingId).Sum(job => job.Quantity.Units);
                    var missing = Math.Max(0, amount.Quantity.Units - input.GetAmount(amount.ResourceId).Units - pendingDestination);
                    foreach (var source in inventories.All.OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
                    {
                        if (missing == 0) break;
                        if (source.IssuanceLocked || source.Owner.Kind != InventoryOwnerKind.BuildingStorage &&
                            source.Owner.Kind != InventoryOwnerKind.WorldPile) continue;
                        var pendingSource = work.Jobs.Where(job => !job.IsTerminal && job.Type == WorkJobType.Haul &&
                            job.ResourceId == amount.ResourceId && job.Target.EntityId == source.Owner.Id)
                            .Sum(job => job.Quantity.Units);
                        var units = Math.Min(missing, Math.Max(0,
                            source.GetAmount(amount.ResourceId).Units - pendingSource));
                        if (units <= 0) continue;
                        work.CreateHaulJob(null, source.Owner, destination, amount.ResourceId,
                            new ResourceQuantity(units), priority);
                        missing -= units;
                        created++;
                    }
                }
            }
            return created;
        }

        public int MaintainLivingBuildingLogistics(WorkManager work, WorkPriority priority = WorkPriority.High)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            var created = 0;
            var hasWell = buildings.All.Any(x => x.IsOperational && x.DefinitionId.FamilyId.ToString() == "well");
            foreach (var policy in livestockPolicies.Values.OrderBy(x => x.BuildingId).ToArray())
            {
                var destinationOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, policy.BuildingId);
                var destination = EnsurePastureInventory(policy.BuildingId);
                var herd = Math.Max(4, Animals.AtHome(policy.BuildingId).Count(x => x.Alive));
                var feedTargets = new[]
                {
                    Tuple.Create(new ResourceId("animal_feed"), (long)Math.Max(2, (herd + 2) / 3 * 3)),
                    Tuple.Create(new ResourceId("wheat"), (long)herd * 2),
                    Tuple.Create(new ResourceId("plant_fiber"), (long)herd * 2),
                    Tuple.Create(new ResourceId("berries"), (long)herd)
                };
                foreach (var target in feedTargets.Where(x => resources.Contains(x.Item1)))
                    created += CreateDeliveryToTarget(work, destinationOwner, destination, target.Item1,
                        target.Item2, priority);
                if (hasWell && (BuildingHasAssignedWorker?.Invoke(policy.BuildingId) ?? false))
                {
                    var targetWater = Math.Max(8L, herd * 2L);
                    if (policy.WaterUnits < targetWater)
                    {
                        livestockPolicies[policy.BuildingId] = new LivestockPenPolicyState(policy.BuildingId,
                            policy.Species, policy.Mode, policy.PopulationLimit, policy.LastProductTick, targetWater);
                        created++;
                    }
                }
            }

            foreach (var source in inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.ProductionOutput ||
                         x.Owner.Kind == InventoryOwnerKind.BuildingStorage && livestockPolicies.ContainsKey(x.Owner.Id))
                         .OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
            {
                foreach (var amount in source.CaptureState().Amounts.Where(x => x.Quantity.IsPositive)
                             .OrderBy(x => x.ResourceId))
                {
                    if (IsLivestockFeed(amount.ResourceId)) continue;
                    var destination = inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                            x.Owner != source.Owner && !livestockPolicies.ContainsKey(x.Owner.Id) &&
                            !x.IssuanceLocked && x.CanAdd(amount.ResourceId, new ResourceQuantity(1)))
                        .OrderBy(x => x.Owner.Id).FirstOrDefault();
                    if (destination == null) continue;
                    var alreadyPending = work.Jobs.Any(job => !job.IsTerminal && job.Type == WorkJobType.Haul &&
                        job.Target.EntityId == source.Owner.Id && job.ResourceId == amount.ResourceId);
                    if (alreadyPending) continue;
                    var units = Math.Min(amount.Quantity.Units, 8L);
                    work.CreateHaulJob(null, source.Owner, destination.Owner, amount.ResourceId,
                        new ResourceQuantity(units), priority);
                    created++;
                }
            }
            return created;
        }

        public void AdvanceFireFuel(long calendarTick, double temperatureCelsius)
        {
            heatedBuildings.Clear();
            var hour = (calendarTick % TimeSpan.TicksPerDay) / (double)TimeSpan.TicksPerHour;
            var required = temperatureCelsius < 10d || hour >= 18d || hour < 6d;
            foreach (var building in buildings.All.Where(x => x.IsOperational &&
                         (x.DefinitionId.FamilyId.ToString() == "campfire" || x.DefinitionId.FamilyId.ToString() == "stone_age_kitchen"))
                         .OrderBy(x => x.Id))
            {
                var owner = InputOwner(building.Id);
                if (!inventories.TryGet(owner, out var fuel)) continue;
                if (!required) continue;
                if (!lastFireFuelTick.TryGetValue(building.Id, out var last))
                {
                    last = calendarTick;
                    lastFireFuelTick[building.Id] = last;
                }
                var consumed = false;
                if (calendarTick - last >= TimeSpan.FromHours(6).Ticks)
                {
                    var wood = new ResourceId("wood_log");
                    if (!fuel.TryRemove(wood, new ResourceQuantity(1))) continue;
                    lastFireFuelTick[building.Id] = calendarTick;
                    consumed = true;
                }
                if (fuel.GetAmount(new ResourceId("wood_log")).Units > 0 || consumed)
                    heatedBuildings.Add(building.Id);
            }
        }

        private int CreateDeliveryToTarget(WorkManager work, InventoryOwner destinationOwner,
            ResourceInventory destination, ResourceId resourceId, long target, WorkPriority priority)
        {
            var pending = work.Jobs.Where(job => !job.IsTerminal && job.Type == WorkJobType.Haul &&
                job.ResourceId == resourceId && job.Destination.HasValue &&
                job.Destination.Value.EntityId == destinationOwner.Id).Sum(job => job.Quantity.Units);
            var missing = Math.Max(0, target - destination.GetAmount(resourceId).Units - pending);
            if (missing == 0) return 0;
            foreach (var source in inventories.All.Where(x => x.Owner != destinationOwner && !x.IssuanceLocked &&
                         (x.Owner.Kind == InventoryOwnerKind.BuildingStorage || x.Owner.Kind == InventoryOwnerKind.WorldPile))
                         .OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
            {
                var units = Math.Min(missing, source.GetAmount(resourceId).Units);
                if (units <= 0) continue;
                work.CreateHaulJob(null, source.Owner, destinationOwner, resourceId,
                    new ResourceQuantity(units), priority);
                return 1;
            }
            return 0;
        }

        private static bool IsLivestockFeed(ResourceId id) =>
            new[] { "animal_feed", "wheat", "plant_fiber", "berries" }.Contains(id.ToString());

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
            if (BuildingWorkAllowed != null && !BuildingWorkAllowed(site.OwnerBuildingId, LastCalendarTick))
                return ProductionWaitingReason.Paused;
            if (site.Paused) return ProductionWaitingReason.Paused;
            if (!site.Continuous && site.QueuedBatches == 0) return ProductionWaitingReason.QueueEmpty;
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
        public void BindSharedNaturalSites(IEnumerable<ProductionSite> canonicalSites)
        {
            var shared = canonicalSites.Where(x=>x.Kind==ProductionSiteKind.NaturalSource &&
                x.RecipeId.ToString()!="hunt_animal").ToDictionary(x=>x.Id);
            foreach(var old in sites.Values.Where(x=>x.Kind==ProductionSiteKind.NaturalSource && !shared.ContainsKey(x.Id)).ToArray())
            { CancelTasksForSite(old.Id); sites.Remove(old.Id); }
            foreach(var site in shared.Values) sites[site.Id]=site;
            orderedSites.Clear();
            orderedSites.AddRange(sites.Values.OrderBy(x => x.Id));
        }


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
            if (Animals.IsPasture(id)) return false;
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

        public bool IsRecipeAvailable(ProductionRecipeId id, bool directOrder = false)
        {
            if (unlocks != null && !unlocks.IsProductionRecipeUnlocked(id)) return false;
            if (id.ToString() == "hunt_animal" && resources.Contains(new ResourceId("carcass")) &&
                !buildings.All.Any(building => building.IsOperational &&
                    building.DefinitionId.FamilyId == new BuildingDefinitionId("hunting_camp"))) return false;
            // Explicit shore fishing remains available without a camp. Organized hunting starts at its station;
            // an ordinary resident killing wildlife uses the separate lower-yield field-dressing route.
            if (directOrder) return true;
            if (id.ToString() == "fish_water" &&
                !buildings.All.Any(building => building.IsOperational &&
                    building.DefinitionId.FamilyId == new BuildingDefinitionId("fishing_camp"))) return false;
            return true;
        }

        public bool SelectCrop(StableEntityId siteId, ProductionRecipeId recipeId,
            Game.Domain.Combat.CombatSideId requestingSide)
        {
            if (Animals.IsPasture(siteId)) return false;
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
                job = CreateJob(existing, sites[existing.SiteId]);
                return true;
            }
            if (!inventories.TryGet(inventories.WorkerOwner(workerId), out _)) return false;

            if (!TrySelectSite(workerId, workerPosition, out var selected, out var selectedKind)) return false;
            var selectedRecipe = recipes.Get(selected.RecipeId);
            var task = ProductionTask.Create(workerId, selected.Id, selected.RecipeId, selectedKind,
                selectedRecipe.WorkRequiredTicks);
            tasks.Add(task.Id, task);
            workerTasks.Add(workerId, task.Id);
            job = CreateJob(task, selected);
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
                job = CreateJob(existing, sites[siteId]);
                return true;
            }
            if (!inventories.TryGet(inventories.WorkerOwner(workerId), out _) ||
                !sites.TryGetValue(siteId, out var site) || !site.IsWorkAvailable ||
                !IsSiteOperational(siteId)) return false;
            var recipe = recipes.Get(site.RecipeId);
            if (!recipe.Enabled || !IsRecipeAvailable(recipe.Id, true)) return false;
            if(site.RecipeId.ToString()=="fish_water" && ManualFishingAccess!=null && !ManualFishingAccess(site.Id))return false;
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
            job = CreateJob(task, site);
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
                job = CreateJob(existing, existingSite);
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
            var site = sites[task.SiteId];
            if (site.RecipeId.ToString() == "fish_water" && ManualFishingAccess != null && !ManualFishingAccess(site.Id))
            { Cancel(task.Id); return default; }
            if (site.Continuous)
            {
                var recipe = recipes.Get(site.RecipeId);
                // Keep the arrived worker/task while full; no extra output or work accumulates during that wait.
                if (site.Paused || !CanPlaceOutputs(task.WorkerId, recipe, site)) return default;
                while (workTicks > 0 && CanPlaceOutputs(task.WorkerId, recipe, site))
                {
                    workTicks -= task.AddWork(workTicks);
                    if (task.AccumulatedWorkTicks < task.RequiredWorkTicks) break;
                    var cycle = Complete(task);
                    if (!cycle.ProducedOutput && task.RecipeId.ToString() != "fish_breed") break;
                }
                return default;
            }
            if (task.RecipeId.ToString() == "hunt_animal" && Animals.TryGet(task.SiteId, out var animal))
            {
                if (animal.Harvested) { Cancel(task.Id); return default; }
                if (animal.Alive)
                {
                    Animals.AdvanceHunt(task.SiteId, task.WorkerId, work.Ticks);
                    return default;
                }
            }
            task.AddWork(workTicks);
            if (task.AccumulatedWorkTicks < task.RequiredWorkTicks) return default;
            return Complete(task);
        }

        public bool Cancel(StableEntityId taskId)
        {
            if (!tasks.TryGetValue(taskId, out var task) || task.IsTerminal) return false;
            if (task.RecipeId.ToString() == "hunt_animal") Animals.CancelShot(task.SiteId);
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
            if (firstStep > finalStep) { AdvanceCropGrowth(LastCalendarTick,targetCalendarTick); LastCalendarTick = targetCalendarTick; return; }
            orderedWorkerBuffer.Clear();
            orderedWorkerBuffer.AddRange(abstractWorkers);
            orderedWorkerBuffer.Sort();
            RemoveAdjacentDuplicates(orderedWorkerBuffer);
            var growthCursor=LastCalendarTick;
            for (var step = firstStep; step <= finalStep; step++)
            {
                var boundary=checked(step*settings.FixedStep.Ticks);
                AdvanceCropGrowth(growthCursor,boundary);growthCursor=boundary;
                var stepStartTick = checked((step - 1L) * settings.FixedStep.Ticks);
                AdvanceLivestock(stepStartTick);
                Food.Advance(settings.FixedStep, Population);
                AdvanceFoodSpoilage(stepStartTick, boundary);
                stepTotals = new Dictionary<ResourceId, long>();
                foreach (var definition in resources.All) stepTotals.Add(definition.Id, GetCanonicalTotal(definition.Id));
                try
                {
                    for (var i = 0; i < orderedWorkerBuffer.Count; i++)
                        AdvanceAbstractWorker(orderedWorkerBuffer[i], settings.FixedStep.Ticks, stepStartTick);
                }
                finally { stepTotals = null; }
            }
            AdvanceCropGrowth(growthCursor,targetCalendarTick);
            LastCalendarTick = targetCalendarTick;
        }

        private void AdvanceCropGrowth(long from,long target)
        {
            // Interleave growth with abstract work, so a planted/harvested field changes on the same
            // boundary whether the caller advances one day or several days in a single call.
            while(from<target)
            {
                var end=Math.Min(target,(from/TimeSpan.TicksPerDay+1)*TimeSpan.TicksPerDay);
                var modifier=weather==null?1000:weather.GetWeatherAtCalendarTick(from).Modifiers.CropGrowthPerThousand;
                var growth=checked((end-from)*modifier/1000L);
                foreach(var site in orderedSites) if(IsSiteOperational(site.Id)) site.AdvanceGrowth(growth);
                from=end;
            }
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
            inventories.CaptureStates(), Ecology?.CaptureState(), Industry?.CaptureState(), Animals.States,
            livestockPolicies.Values, huntingPolicies.Values,
            foodSpoilage.Select(x => new FoodSpoilageState(x.Key.Item1, x.Key.Item2, x.Value)));

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
            Animals.Restore(state.Animals);
            livestockPolicies.Clear();
            foreach (var policy in state.LivestockPolicies) livestockPolicies.Add(policy.BuildingId, policy);
            huntingPolicies.Clear();
            foreach (var policy in state.HuntingPolicies) huntingPolicies.Add(policy.BuildingId, policy);
            foodSpoilage.Clear();
            foreach (var item in state.FoodSpoilage)
                foodSpoilage.Add(Tuple.Create(item.Owner, item.ResourceId), item.AccumulatedTicks);
            foreach (var site in restoredSites.Values.Where(x => x.RecipeId.ToString() == "hunt_animal"))
            {
                Animals.Register(site.Id, "deer"); // Legacy snapshots had no persisted animal HP.
                if (site.IsDepleted && Animals.TryGet(site.Id, out var animal) && !animal.Harvested)
                { Animals.Damage(site.Id, animal.Maximum); Animals.Harvest(site.Id); }
            }
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

        private void AdvanceFoodSpoilage(long fromTick, long toTick)
        {
            if (toTick <= fromTick || !resources.Contains(new ResourceId("spoiled_food"))) return;
            var elapsed = toTick - fromTick;
            if (weather != null && weather.GetWeatherAtCalendarTick(fromTick).TemperatureCelsius < 0d)
                elapsed /= 2;
            if (elapsed <= 0) return;
            var spoiled = new ResourceId("spoiled_food");
            foreach (var inventory in inventories.All.OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id))
            foreach (var shelf in ShelfLifeDays.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var resource = new ResourceId(shelf.Key);
                if (!resources.Contains(resource)) continue;
                var amount = inventory.GetAmount(resource).Units;
                var key = Tuple.Create(inventory.Owner, resource);
                if (amount <= 0) { foodSpoilage.Remove(key); continue; }
                var accumulated = foodSpoilage.TryGetValue(key, out var old) ? checked(old + elapsed) : elapsed;
                var shelfTicks = checked(TimeSpan.TicksPerDay * shelf.Value);
                var expired = Math.Min(amount, accumulated / shelfTicks);
                if (expired > 0 && inventory.TryRemove(resource, new ResourceQuantity(expired), true))
                {
                    inventory.TryAdd(spoiled, new ResourceQuantity(expired), true);
                    accumulated -= expired * shelfTicks;
                }
                foodSpoilage[key] = accumulated;
            }
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var animal in Animals.States)
            {
                AddId(ref hash, animal.Id);
                AddText(ref hash, animal.Species);
                DeterministicKeyedRandom.Add(ref hash, (ulong)animal.Health);
                DeterministicKeyedRandom.Add(ref hash, (ulong)animal.Maximum);
                DeterministicKeyedRandom.Add(ref hash, (ulong)animal.ShotTicks);
                DeterministicKeyedRandom.Add(ref hash, (ulong)animal.ShotSequence);
                DeterministicKeyedRandom.Add(ref hash, animal.Domestic ? 1UL : 0UL);
                DeterministicKeyedRandom.Add(ref hash, animal.Harvested ? 1UL : 0UL);
                if (animal.Home.IsValid) AddId(ref hash, animal.Home);
                DeterministicKeyedRandom.Add(ref hash, animal.Male ? 1UL : 0UL);
                DeterministicKeyedRandom.Add(ref hash, (ulong)animal.BornTick);
            }
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
                DeterministicKeyedRandom.Add(ref hash, site.Continuous ? 1UL : 0UL);
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
                if (sites[task.SiteId].Continuous)
                {
                    AdvanceTaskAt(task.Id, TimeSpan.FromTicks(budgetTicks), calendarTick);
                    return;
                }
                if (task.RecipeId.ToString() == "hunt_animal")
                {
                    if (Animals.CanHunt != null && !Animals.CanHunt(workerId)) { Cancel(task.Id); return; }
                    AdvanceTaskAt(task.Id, TimeSpan.FromTicks(budgetTicks), calendarTick);
                    return;
                }
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
            var fishStock = FishStockFor(site);
            if (task.RecipeId.ToString() == "fish_camp" && (fishStock == null || fishStock.IsDepleted)) return default;
            if (task.RecipeId.ToString() == "fish_breed")
            {
                if (fishStock == null || fishStock.RemainingAmount >= fishStock.SourceCapacity) return default;
                site.CompleteWork(task.Kind);
                fishStock.RecoverNatural(outputs.Sum(value=>value.Quantity.Units));
                if (site.Continuous) task.BeginNextCycle(); else { task.Complete(); Release(task); }
                return default;
            }
            if (task.RecipeId.ToString() == "hunt_animal" && Animals.TryGet(site.Id, out var hunted) &&
                (hunted.Alive || hunted.Harvested)) return default;
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
            if (task.RecipeId.ToString() == "fish_camp") fishStock.CompleteWork(ProductionTaskKind.Gather);
            site.CompleteWork(task.Kind, recipe.HarvestEntireSource || task.RecipeId.ToString() == "hunt_animal");
            if (task.RecipeId.ToString() == "hunt_animal") Animals.Harvest(site.Id);
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
                            outputCoordinate(OutputPosition != null ? OutputPosition(task.WorkerId, site) : site.Position));
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
            if (site.Continuous) task.BeginNextCycle();
            else { task.Complete(); Release(task); }
            return new ProductionCompletion(!site.Continuous, produced, outputId, outputUnits, outputOwner);
        }

        private void Release(ProductionTask task)
        {
            workerTasks.Remove(task.WorkerId);
        }

        private bool RecipeNeeded(ProductionRecipe recipe)
        {
            if (recipe.Outputs.Any(x => preparedFoodResources.Contains(x.ResourceId)))
            {
                var currentPrepared = preparedFoodResources.Sum(id =>
                    stepTotals != null ? stepTotals[id] : GetCanonicalTotal(id));
                var reservedPrepared = preparedFoodResources.Sum(ReservedOutput);
                if (checked(currentPrepared + reservedPrepared) < PreparedFoodTarget) return true;
            }
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
            var bestPriority = int.MinValue;
            var assignedWorkplace = AssignedWorkplace?.Invoke(workerId);
            foreach (var site in orderedSites)
            {
                if (!site.IsWorkAvailable || !IsSiteOperational(site.Id)) continue;
                if (site.Kind == ProductionSiteKind.BuildingWorkplace && BuildingWorkAllowed != null &&
                    !BuildingWorkAllowed(site.OwnerBuildingId, LastCalendarTick)) continue;
                if(site.RecipeId.ToString()=="fish_water" && ManualFishingAccess!=null && !ManualFishingAccess(site.Id))continue;
                if (Animals.IsPasture(site.Id)) continue;
                if (site.RecipeId.ToString() == "hunt_animal" &&
                    (Animals.CanHunt != null && !Animals.CanHunt(workerId) ||
                     Animals.TryGet(site.Id, out var domestic) && domestic.Domestic)) continue;
                var recipe = recipes.Get(site.RecipeId);
                if (!recipe.Enabled || !IsRecipeAvailable(recipe.Id)) continue;
                if (recipe.HarvestEntireSource && tasks.Values.Any(x => !x.IsTerminal && x.SiteId == site.Id)) continue;
                  if (!HasNaturalSourceCapacity(site)) continue;
                if (site.Kind == ProductionSiteKind.BuildingWorkplace &&
                    ActiveWorkersAt(site.OwnerBuildingId) >= site.MaximumWorkers) continue;
                var kind = TaskKindFor(site);
                if (site.Kind != ProductionSiteKind.BuildingWorkplace && !RecipeNeeded(recipe)) continue;
                if (site.Kind == ProductionSiteKind.BuildingWorkplace && site.Continuous &&
                    recipe.Outputs.Any(x => preparedFoodResources.Contains(x.ResourceId)) && !RecipeNeeded(recipe))
                    continue;
                if (kind != ProductionTaskKind.Plant && !CanPlaceOutputs(workerId, recipe, site)) continue;
                if (!InputsAvailable(recipe, site)) continue;
                var distance = workerPosition.DistanceSquared(site.Position);
                var priority = site.Kind == ProductionSiteKind.BuildingWorkplace
                    ? BuildingWorkPriority?.Invoke(site.OwnerBuildingId) ?? 0 : 0;
                if (assignedWorkplace.HasValue && site.Kind == ProductionSiteKind.BuildingWorkplace &&
                    site.OwnerBuildingId == assignedWorkplace.Value) priority += 1000;
                if (selected == null || priority > bestPriority || priority == bestPriority &&
                    (distance < bestDistance || distance.Equals(bestDistance) && site.Id.CompareTo(selected.Id) < 0))
                {
                    selected = site;
                    selectedKind = kind;
                    bestDistance = distance;
                    bestPriority = priority;
                }
            }
            return selected != null;
        }

          private bool HasNaturalSourceCapacity(ProductionSite site)
          {
              if (site.Kind != ProductionSiteKind.NaturalSource) return true;
              var reservedUnits = tasks.Values.LongCount(task => !task.IsTerminal && task.SiteId == site.Id);
              if (site.RecipeId.ToString() == "hunt_animal")
                  return reservedUnits == 0 && (!Animals.TryGet(site.Id, out var animal) || !animal.Harvested);
              return reservedUnits < site.RemainingAmount;
          }

        private bool CanPlaceOutputs(StableEntityId workerId, ProductionRecipe recipe, ProductionSite site)
        {
            if (recipe.Id.ToString() == "fish_breed")
            { var stock = FishStockFor(site); return stock != null && stock.RemainingAmount < stock.SourceCapacity; }
            if (recipe.Id.ToString() == "fish_camp" && FishStockFor(site)?.RemainingAmount is not > 0) return false;
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
