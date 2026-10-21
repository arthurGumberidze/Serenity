using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;

namespace Game.Domain.Buildings
{
    public sealed class ConstructionSpec
    {
        private readonly ResourceAmount[] cost;

        public ConstructionSpec(IEnumerable<ResourceAmount> cost, TimeSpan workRequired, int stageCount = 1)
        {
            if (workRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(workRequired));
            if (stageCount <= 0 || stageCount > 32) throw new ArgumentOutOfRangeException(nameof(stageCount));
            this.cost = (cost ?? Array.Empty<ResourceAmount>()).OrderBy(x => x.ResourceId).ToArray();
            if (this.cost.Select(x => x.ResourceId).Distinct().Count() != this.cost.Length)
                throw new ArgumentException("Construction cost has duplicate resource IDs.", nameof(cost));
            WorkRequiredTicks = workRequired.Ticks;
            StageCount = stageCount;
        }

        public IReadOnlyList<ResourceAmount> Cost => Array.AsReadOnly(cost);
        public long WorkRequiredTicks { get; }
        public int StageCount { get; }
    }

    public sealed class PlacementSpec
    {
        public PlacementSpec(BuildingFootprint footprint, BuildingPlacementMode mode = BuildingPlacementMode.Grid,
            bool allowsOverlap = false, BuildingSpatialForm spatialForm = BuildingSpatialForm.Structure,
            BuildingPlacementConstraint constraints = BuildingPlacementConstraint.ReachableApproach,
            int maximumHeightDeltaMillimetres = 750, int approachClearanceMillimetres = 750)
        {
            if (!Enum.IsDefined(typeof(BuildingPlacementMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(BuildingSpatialForm), spatialForm)) throw new ArgumentOutOfRangeException(nameof(spatialForm));
            const BuildingPlacementConstraint all = BuildingPlacementConstraint.OwnedTerritory |
                BuildingPlacementConstraint.ReachableApproach | BuildingPlacementConstraint.Shoreline |
                BuildingPlacementConstraint.Groundwater;
            if ((constraints & ~all) != 0) throw new ArgumentOutOfRangeException(nameof(constraints));
            if (maximumHeightDeltaMillimetres < 0) throw new ArgumentOutOfRangeException(nameof(maximumHeightDeltaMillimetres));
            if (approachClearanceMillimetres < 0) throw new ArgumentOutOfRangeException(nameof(approachClearanceMillimetres));
            Footprint = footprint;
            Mode = mode;
            AllowsOverlap = allowsOverlap;
            SpatialForm = spatialForm;
            Constraints = constraints;
            MaximumHeightDeltaMillimetres = maximumHeightDeltaMillimetres;
            ApproachClearanceMillimetres = approachClearanceMillimetres;
        }

        public BuildingFootprint Footprint { get; }
        public BuildingPlacementMode Mode { get; }
        public bool AllowsOverlap { get; }
        public BuildingSpatialForm SpatialForm { get; }
        public BuildingPlacementConstraint Constraints { get; }
        public int MaximumHeightDeltaMillimetres { get; }
        public int ApproachClearanceMillimetres { get; }
    }

    public sealed class HousingSpec
    {
        public HousingSpec(int residentCapacity)
        {
            if (residentCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(residentCapacity));
            ResidentCapacity = residentCapacity;
        }

        public int ResidentCapacity { get; }
    }

    public sealed class WorkplaceSpec
    {
        public WorkplaceSpec(int maximumWorkers, ProfessionId? requiredProfession = null,
            SkillId? governingSkill = null, int minimumSkillLevel = 0)
        {
            if (maximumWorkers <= 0) throw new ArgumentOutOfRangeException(nameof(maximumWorkers));
            if (requiredProfession.HasValue && !requiredProfession.Value.IsValid)
                throw new ArgumentException("Required profession must be valid.", nameof(requiredProfession));
            if (governingSkill.HasValue && !governingSkill.Value.IsValid)
                throw new ArgumentException("Governing skill must be valid.", nameof(governingSkill));
            if (minimumSkillLevel < SkillLevel.Minimum || minimumSkillLevel > SkillLevel.Maximum)
                throw new ArgumentOutOfRangeException(nameof(minimumSkillLevel));
            MaximumWorkers = maximumWorkers;
            RequiredProfession = requiredProfession;
            GoverningSkill = governingSkill;
            MinimumSkillLevel = minimumSkillLevel;
        }

        public int MaximumWorkers { get; }
        public ProfessionId? RequiredProfession { get; }
        public SkillId? GoverningSkill { get; }
        public int MinimumSkillLevel { get; }
    }

    public sealed class StorageSpec
    {
        private readonly ResourceCategory[] acceptedCategories;

        public StorageSpec(long capacity, IEnumerable<ResourceCategory> acceptedCategories)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.acceptedCategories = (acceptedCategories ?? throw new ArgumentNullException(nameof(acceptedCategories)))
                .Distinct().OrderBy(x => x).ToArray();
            if (this.acceptedCategories.Length == 0 || this.acceptedCategories.Any(x => !Enum.IsDefined(typeof(ResourceCategory), x)))
                throw new ArgumentException("Storage requires valid accepted categories.", nameof(acceptedCategories));
            Capacity = capacity;
        }

        public long Capacity { get; }
        public IReadOnlyList<ResourceCategory> AcceptedCategories => Array.AsReadOnly(acceptedCategories);
    }

    public sealed class ProductionSpec
    {
        private readonly ProductionRecipeId[] recipeIds;

        public ProductionSpec(IEnumerable<ProductionRecipeId> recipeIds, long inputCapacity, long outputCapacity)
        {
            this.recipeIds = (recipeIds ?? throw new ArgumentNullException(nameof(recipeIds))).OrderBy(x => x).ToArray();
            if (this.recipeIds.Length == 0 || this.recipeIds.Any(x => !x.IsValid) ||
                this.recipeIds.Distinct().Count() != this.recipeIds.Length)
                throw new ArgumentException("Production recipes must be valid and unique.", nameof(recipeIds));
            if (inputCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(inputCapacity));
            if (outputCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(outputCapacity));
            InputCapacity = inputCapacity;
            OutputCapacity = outputCapacity;
        }

        public IReadOnlyList<ProductionRecipeId> RecipeIds => Array.AsReadOnly(recipeIds);
        public long InputCapacity { get; }
        public long OutputCapacity { get; }
    }

    public sealed class MaintenanceSpec
    {
        private readonly ResourceAmount[] repairCost;
        private readonly ResourceAmount[] upkeepCost;

        public MaintenanceSpec(long wearIntervalTicks, int wearPerInterval, IEnumerable<ResourceAmount> repairCost,
            TimeSpan repairWorkRequired, IEnumerable<ResourceAmount> upkeepCost = null,
            bool canDemolish = true, int demolitionReturnPerThousand = 500)
        {
            if (wearIntervalTicks < 0) throw new ArgumentOutOfRangeException(nameof(wearIntervalTicks));
            if (wearPerInterval < 0) throw new ArgumentOutOfRangeException(nameof(wearPerInterval));
            if ((wearIntervalTicks == 0) != (wearPerInterval == 0))
                throw new ArgumentException("Wear interval and amount must both be zero or both be positive.");
            if (repairWorkRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(repairWorkRequired));
            if (demolitionReturnPerThousand < 0 || demolitionReturnPerThousand > 1000)
                throw new ArgumentOutOfRangeException(nameof(demolitionReturnPerThousand));
            this.repairCost = CopyAmounts(repairCost, nameof(repairCost));
            this.upkeepCost = CopyAmounts(upkeepCost, nameof(upkeepCost));
            WearIntervalTicks = wearIntervalTicks;
            WearPerInterval = wearPerInterval;
            RepairWorkRequiredTicks = repairWorkRequired.Ticks;
            CanDemolish = canDemolish;
            DemolitionReturnPerThousand = demolitionReturnPerThousand;
        }

        public long WearIntervalTicks { get; }
        public int WearPerInterval { get; }
        public IReadOnlyList<ResourceAmount> RepairCost => Array.AsReadOnly(repairCost);
        public long RepairWorkRequiredTicks { get; }
        public IReadOnlyList<ResourceAmount> UpkeepCost => Array.AsReadOnly(upkeepCost);
        public bool CanDemolish { get; }
        public int DemolitionReturnPerThousand { get; }

        internal static ResourceAmount[] CopyAmounts(IEnumerable<ResourceAmount> amounts, string parameterName)
        {
            var result = (amounts ?? Array.Empty<ResourceAmount>()).OrderBy(x => x.ResourceId).ToArray();
            if (result.Select(x => x.ResourceId).Distinct().Count() != result.Length)
                throw new ArgumentException("Resource amounts must have unique resource IDs.", parameterName);
            return result;
        }
    }

    public sealed class BuildingUpgradeOption
    {
        private readonly ResourceAmount[] cost;

        public BuildingUpgradeOption(BuildingDefinitionId targetDefinitionId, IEnumerable<ResourceAmount> cost,
            TimeSpan workRequired)
        {
            if (!targetDefinitionId.IsValid) throw new ArgumentException("Upgrade target required.", nameof(targetDefinitionId));
            if (workRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(workRequired));
            TargetDefinitionId = targetDefinitionId;
            this.cost = MaintenanceSpec.CopyAmounts(cost, nameof(cost));
            WorkRequiredTicks = workRequired.Ticks;
        }

        public BuildingDefinitionId TargetDefinitionId { get; }
        public IReadOnlyList<ResourceAmount> Cost => Array.AsReadOnly(cost);
        public long WorkRequiredTicks { get; }
    }

    public sealed class UpgradeSpec
    {
        private readonly BuildingUpgradeOption[] options;

        public UpgradeSpec(IEnumerable<BuildingUpgradeOption> options)
        {
            this.options = (options ?? throw new ArgumentNullException(nameof(options))).ToArray();
            if (this.options.Length == 0 || this.options.Any(x => x == null) ||
                this.options.Select(x => x.TargetDefinitionId).Distinct().Count() != this.options.Length)
                throw new ArgumentException("Upgrade options must be non-null with unique targets.", nameof(options));
        }

        public IReadOnlyList<BuildingUpgradeOption> Options => Array.AsReadOnly(options);
    }

    public sealed class BuildingDefinitionCatalog
    {
        private readonly Dictionary<BuildingDefinitionId, BuildingDefinition> definitions;
        private readonly BuildingDefinition[] ordered;

        public BuildingDefinitionCatalog(IEnumerable<BuildingDefinition> definitions, ResourceCatalog resources = null,
            ProductionCatalog recipes = null, TechnologyCatalog technologies = null)
        {
            this.definitions = new Dictionary<BuildingDefinitionId, BuildingDefinition>();
            foreach (var definition in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null || !this.definitions.TryAdd(definition.Id, definition))
                    throw new ArgumentException("Building definitions must be non-null with unique IDs.", nameof(definitions));
            }
            if (this.definitions.Count == 0) throw new ArgumentException("Building catalog cannot be empty.", nameof(definitions));
            ordered = this.definitions.Values.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal).ToArray();
            foreach (var definition in ordered) ValidateReferences(definition, resources, recipes, technologies);
            ValidateUpgradeGraph();
        }

        public IReadOnlyList<BuildingDefinition> All => Array.AsReadOnly(ordered);
        public BuildingDefinition Get(BuildingDefinitionId id) => definitions.TryGetValue(id, out var definition)
            ? definition : throw new KeyNotFoundException("Unknown building definition: " + id);

        private void ValidateReferences(BuildingDefinition definition, ResourceCatalog resources,
            ProductionCatalog recipes, TechnologyCatalog technologies)
        {
            if (resources != null)
            {
                ValidateAmounts(definition.Construction.Cost, resources);
                if (definition.Maintenance != null)
                {
                    ValidateAmounts(definition.Maintenance.RepairCost, resources);
                    ValidateAmounts(definition.Maintenance.UpkeepCost, resources);
                }
                if (definition.Upgrades != null)
                    foreach (var option in definition.Upgrades.Options) ValidateAmounts(option.Cost, resources);
            }
            if (recipes != null && definition.Production != null)
                foreach (var recipeId in definition.Production.RecipeIds) recipes.Get(recipeId);
            if (technologies != null)
                foreach (var technologyId in definition.RequiredTechnologies) technologies.Get(technologyId);
            if (definition.Upgrades == null) return;
            foreach (var option in definition.Upgrades.Options)
            {
                if (option.TargetDefinitionId == definition.Id)
                    throw new ArgumentException("A building cannot upgrade to itself: " + definition.Id);
                if (!definitions.ContainsKey(option.TargetDefinitionId))
                    throw new ArgumentException("Unknown building upgrade target: " + option.TargetDefinitionId);
            }
        }

        private static void ValidateAmounts(IEnumerable<ResourceAmount> amounts, ResourceCatalog resources)
        {
            foreach (var amount in amounts) resources.Get(amount.ResourceId);
        }

        private void ValidateUpgradeGraph()
        {
            var visiting = new HashSet<BuildingDefinitionId>();
            var visited = new HashSet<BuildingDefinitionId>();
            foreach (var definition in ordered) Visit(definition.Id, visiting, visited);
        }

        private void Visit(BuildingDefinitionId id, HashSet<BuildingDefinitionId> visiting,
            HashSet<BuildingDefinitionId> visited)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Building upgrade graph contains a cycle at " + id + ".");
            var definition = definitions[id];
            if (definition.Upgrades != null)
                foreach (var option in definition.Upgrades.Options) Visit(option.TargetDefinitionId, visiting, visited);
            visiting.Remove(id);
            visited.Add(id);
        }
    }
}
