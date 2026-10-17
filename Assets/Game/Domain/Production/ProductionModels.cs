using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Resources;

namespace Game.Domain.Production
{
    public readonly struct ProductionRecipeId : IEquatable<ProductionRecipeId>, IComparable<ProductionRecipeId>
    {
        private readonly string value;

        public ProductionRecipeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                throw new ArgumentException("Production recipe ID must contain 1-64 characters.", nameof(value));
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new ArgumentException("Production recipe ID must use lowercase ASCII, digits, '_' or '-'.", nameof(value));
            }
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        public int CompareTo(ProductionRecipeId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(ProductionRecipeId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is ProductionRecipeId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(ProductionRecipeId left, ProductionRecipeId right) => left.Equals(right);
        public static bool operator !=(ProductionRecipeId left, ProductionRecipeId right) => !left.Equals(right);
    }

    public enum ProductionSiteKind { NaturalSource = 0, PrimitiveCropPlot = 1 }
    public enum CropPlotPhase { Unplanted = 0, Growing = 1, Ready = 2 }
    public enum ProductionTaskKind { Gather = 0, Plant = 1, Harvest = 2 }
    public enum ProductionTaskStatus { Active = 0, Completed = 1, Cancelled = 2 }
    public enum ProductionWeatherExposure { Sheltered = 0, Outdoor = 1 }

    public sealed class ProductionRecipe
    {
        private readonly ResourceAmount[] inputs;
        private readonly ResourceAmount[] outputs;

        public ProductionRecipe(ProductionRecipeId id, string displayName, IEnumerable<ResourceAmount> inputs,
            IEnumerable<ResourceAmount> outputs, TimeSpan workRequired, ProductionSiteKind siteKind,
            bool enabled = true, ProductionWeatherExposure weatherExposure = ProductionWeatherExposure.Sheltered,
            bool seasonalWildSource = false)
        {
            if (!id.IsValid) throw new ArgumentException("Recipe identity required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Recipe name required.", nameof(displayName));
            if (workRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(workRequired));
            if (!Enum.IsDefined(typeof(ProductionSiteKind), siteKind)) throw new ArgumentOutOfRangeException(nameof(siteKind));
            if (!Enum.IsDefined(typeof(ProductionWeatherExposure), weatherExposure))
                throw new ArgumentOutOfRangeException(nameof(weatherExposure));
            this.inputs = (inputs ?? Array.Empty<ResourceAmount>()).OrderBy(x => x.ResourceId).ToArray();
            this.outputs = (outputs ?? throw new ArgumentNullException(nameof(outputs))).OrderBy(x => x.ResourceId).ToArray();
            if (this.outputs.Length == 0) throw new ArgumentException("Recipe requires at least one output.", nameof(outputs));
            if (this.inputs.Select(x => x.ResourceId).Distinct().Count() != this.inputs.Length ||
                this.outputs.Select(x => x.ResourceId).Distinct().Count() != this.outputs.Length)
                throw new ArgumentException("Recipe inputs and outputs must have unique resource IDs.");
            Id = id;
            DisplayName = displayName;
            WorkRequiredTicks = workRequired.Ticks;
            SiteKind = siteKind;
            Enabled = enabled;
            WeatherExposure = weatherExposure;
            SeasonalWildSource = seasonalWildSource;
        }

        public ProductionRecipeId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceAmount> Inputs => Array.AsReadOnly(inputs);
        public IReadOnlyList<ResourceAmount> Outputs => Array.AsReadOnly(outputs);
        public long WorkRequiredTicks { get; }
        public ProductionSiteKind SiteKind { get; }
        public bool Enabled { get; }
        public ProductionWeatherExposure WeatherExposure { get; }
        public bool SeasonalWildSource { get; }
    }

    public sealed class ProductionCatalog
    {
        private readonly Dictionary<ProductionRecipeId, ProductionRecipe> recipes;
        private readonly ProductionRecipe[] ordered;

        public ProductionCatalog(IEnumerable<ProductionRecipe> recipes, ResourceCatalog resources)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            this.recipes = new Dictionary<ProductionRecipeId, ProductionRecipe>();
            foreach (var recipe in recipes ?? throw new ArgumentNullException(nameof(recipes)))
            {
                if (recipe == null || !this.recipes.TryAdd(recipe.Id, recipe))
                    throw new ArgumentException("Recipes must be non-null with unique IDs.", nameof(recipes));
                foreach (var amount in recipe.Inputs) resources.Get(amount.ResourceId);
                foreach (var amount in recipe.Outputs) resources.Get(amount.ResourceId);
            }
            ordered = this.recipes.Values.OrderBy(x => x.Id).ToArray();
        }

        public IReadOnlyList<ProductionRecipe> All => Array.AsReadOnly(ordered);
        public ProductionRecipe Get(ProductionRecipeId id) => recipes.TryGetValue(id, out var recipe)
            ? recipe : throw new KeyNotFoundException("Unknown production recipe: " + id);

        public static ProductionCatalog CreateStoneAge(ResourceCatalog resources)
        {
            var berries = new ResourceId("berries");
            var fiber = new ResourceId("plant_fiber");
            var wood = new ResourceId("wood_log");
            var stone = new ResourceId("stone");
            var iron = new ResourceId("iron_ore");
            var gold = new ResourceId("gold_ore");
            var hide = new ResourceId("hide");
            var meat = new ResourceId("meat");
            var fish = new ResourceId("fish");
            var recipes = new List<ProductionRecipe>
            {
                new ProductionRecipe(new ProductionRecipeId("gather_berries"), "Gather Berries", null,
                    new[] { new ResourceAmount(berries, new ResourceQuantity(4)) }, TimeSpan.FromHours(4), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor, seasonalWildSource: true),
                new ProductionRecipe(new ProductionRecipeId("gather_fiber"), "Gather Plant Fiber", null,
                    new[] { new ResourceAmount(fiber, new ResourceQuantity(4)) }, TimeSpan.FromHours(6), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor, seasonalWildSource: true),
                new ProductionRecipe(new ProductionRecipeId("chop_wood"), "Chop Wood", null,
                    new[] { new ResourceAmount(wood, new ResourceQuantity(4)) }, TimeSpan.FromHours(8), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor),
                new ProductionRecipe(new ProductionRecipeId("gather_stone"), "Gather Stone", null,
                    new[] { new ResourceAmount(stone, new ResourceQuantity(4)) }, TimeSpan.FromHours(10), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor),
                new ProductionRecipe(new ProductionRecipeId("cultivate_berries"), "Cultivate Berries", null,
                    new[] { new ResourceAmount(berries, new ResourceQuantity(6)) }, TimeSpan.FromHours(8), ProductionSiteKind.PrimitiveCropPlot,
                    weatherExposure: ProductionWeatherExposure.Outdoor)
            };
            if (resources.Contains(iron))
                recipes.Add(new ProductionRecipe(new ProductionRecipeId("gather_iron"), "Gather Iron Ore", null,
                    new[] { new ResourceAmount(iron, new ResourceQuantity(3)) }, TimeSpan.FromHours(12), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor));
            if (resources.Contains(gold))
                recipes.Add(new ProductionRecipe(new ProductionRecipeId("gather_gold"), "Gather Gold Ore", null,
                    new[] { new ResourceAmount(gold, new ResourceQuantity(2)) }, TimeSpan.FromHours(16), ProductionSiteKind.NaturalSource,
                    weatherExposure: ProductionWeatherExposure.Outdoor));
            if (resources.Contains(hide) && resources.Contains(meat))
                recipes.Add(new ProductionRecipe(new ProductionRecipeId("hunt_animal"), "Hunt Animal", null,
                    new[] { new ResourceAmount(hide, new ResourceQuantity(2)),
                        new ResourceAmount(meat, new ResourceQuantity(4)) }, TimeSpan.FromHours(12),
                    ProductionSiteKind.NaturalSource, weatherExposure: ProductionWeatherExposure.Outdoor));
            if (resources.Contains(fish))
                recipes.Add(new ProductionRecipe(new ProductionRecipeId("fish_water"), "Fish Water", null,
                    new[] { new ResourceAmount(fish, new ResourceQuantity(4)) }, TimeSpan.FromHours(8),
                    ProductionSiteKind.NaturalSource, weatherExposure: ProductionWeatherExposure.Outdoor));
            return new ProductionCatalog(recipes, resources);
        }
    }

    public readonly struct ProductionSiteState
    {
        public ProductionSiteState(StableEntityId id, ProductionRecipeId recipeId, ProductionSiteKind kind,
            WorldPosition position, long remainingAmount, long sourceCapacity, CropPlotPhase cropPhase,
            long growthProgressTicks, long growthRequiredTicks)
        {
            if (!id.IsValid) throw new ArgumentException("Production site identity required.", nameof(id));
            if (!recipeId.IsValid) throw new ArgumentException("Production recipe identity required.", nameof(recipeId));
            if (!Enum.IsDefined(typeof(ProductionSiteKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(CropPlotPhase), cropPhase)) throw new ArgumentOutOfRangeException(nameof(cropPhase));
            if (remainingAmount < 0 || sourceCapacity < remainingAmount) throw new ArgumentOutOfRangeException(nameof(remainingAmount));
            if (growthProgressTicks < 0 || growthRequiredTicks < 0 || growthProgressTicks > growthRequiredTicks)
                throw new ArgumentOutOfRangeException(nameof(growthProgressTicks));
            Id = id;
            RecipeId = recipeId;
            Kind = kind;
            Position = position;
            RemainingAmount = remainingAmount;
            SourceCapacity = sourceCapacity;
            CropPhase = cropPhase;
            GrowthProgressTicks = growthProgressTicks;
            GrowthRequiredTicks = growthRequiredTicks;
        }

        public StableEntityId Id { get; }
        public ProductionRecipeId RecipeId { get; }
        public ProductionSiteKind Kind { get; }
        public WorldPosition Position { get; }
        public long RemainingAmount { get; }
        public long SourceCapacity { get; }
        public CropPlotPhase CropPhase { get; }
        public long GrowthProgressTicks { get; }
        public long GrowthRequiredTicks { get; }
    }

    public sealed class ProductionSite
    {
        private ProductionSite(ProductionSiteState state)
        {
            Id = state.Id;
            RecipeId = state.RecipeId;
            Kind = state.Kind;
            Position = state.Position;
            RemainingAmount = state.RemainingAmount;
            SourceCapacity = state.SourceCapacity;
            CropPhase = state.CropPhase;
            GrowthProgressTicks = state.GrowthProgressTicks;
            GrowthRequiredTicks = state.GrowthRequiredTicks;
        }

        public StableEntityId Id { get; }
        public ProductionRecipeId RecipeId { get; }
        public ProductionSiteKind Kind { get; }
        public WorldPosition Position { get; private set; }
        public long RemainingAmount { get; private set; }
        public long SourceCapacity { get; }
        public CropPlotPhase CropPhase { get; private set; }
        public long GrowthProgressTicks { get; private set; }
        public long GrowthRequiredTicks { get; }
        public bool IsDepleted => Kind == ProductionSiteKind.NaturalSource && RemainingAmount == 0;
        public bool IsWorkAvailable => Kind == ProductionSiteKind.NaturalSource ? RemainingAmount > 0 :
            CropPhase == CropPlotPhase.Unplanted || CropPhase == CropPlotPhase.Ready;

        public static ProductionSite CreateNatural(ProductionRecipeId recipeId, WorldPosition position, long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return Restore(new ProductionSiteState(StableEntityId.NewId(), recipeId, ProductionSiteKind.NaturalSource,
                position, amount, amount, CropPlotPhase.Unplanted, 0, 0));
        }

        public static ProductionSite CreateCropPlot(StableEntityId siteId, ProductionRecipeId recipeId,
            WorldPosition position, TimeSpan growthRequired)
        {
            if (growthRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(growthRequired));
            return Restore(new ProductionSiteState(siteId, recipeId, ProductionSiteKind.PrimitiveCropPlot,
                position, 0, 0, CropPlotPhase.Unplanted, 0, growthRequired.Ticks));
        }

        public static ProductionSite Restore(ProductionSiteState state) => new ProductionSite(state);

        public void CompleteWork(ProductionTaskKind taskKind)
        {
            if (Kind == ProductionSiteKind.NaturalSource)
            {
                if (taskKind != ProductionTaskKind.Gather || RemainingAmount <= 0)
                    throw new InvalidOperationException("Natural production source cannot complete this task.");
                RemainingAmount--;
                return;
            }
            if (taskKind == ProductionTaskKind.Plant && CropPhase == CropPlotPhase.Unplanted)
            {
                CropPhase = CropPlotPhase.Growing;
                GrowthProgressTicks = 0;
                return;
            }
            if (taskKind == ProductionTaskKind.Harvest && CropPhase == CropPlotPhase.Ready)
            {
                CropPhase = CropPlotPhase.Unplanted;
                GrowthProgressTicks = 0;
                return;
            }
            throw new InvalidOperationException("Crop plot cannot complete this task in its current phase.");
        }

        public void AdvanceGrowth(long ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (Kind != ProductionSiteKind.PrimitiveCropPlot || CropPhase != CropPlotPhase.Growing || ticks == 0) return;
            GrowthProgressTicks = Math.Min(GrowthRequiredTicks, checked(GrowthProgressTicks + ticks));
            if (GrowthProgressTicks >= GrowthRequiredTicks) CropPhase = CropPlotPhase.Ready;
        }

        public void RespawnNatural(WorldPosition position, long amount)
        {
            if (Kind != ProductionSiteKind.NaturalSource)
                throw new InvalidOperationException("Only natural sources can respawn.");
            if (amount <= 0 || amount > SourceCapacity) throw new ArgumentOutOfRangeException(nameof(amount));
            Position = position;
            RemainingAmount = amount;
        }

        public ProductionSiteState CaptureState() => new ProductionSiteState(Id, RecipeId, Kind, Position,
            RemainingAmount, SourceCapacity, CropPhase, GrowthProgressTicks, GrowthRequiredTicks);
    }

    public readonly struct ProductionTaskState
    {
        public ProductionTaskState(StableEntityId id, StableEntityId workerId, StableEntityId siteId,
            ProductionRecipeId recipeId, ProductionTaskKind kind, long accumulatedWorkTicks,
            long requiredWorkTicks, ProductionTaskStatus status)
        {
            if (!id.IsValid || !workerId.IsValid || !siteId.IsValid) throw new ArgumentException("Task identities must be valid.");
            if (!recipeId.IsValid) throw new ArgumentException("Recipe identity required.", nameof(recipeId));
            if (!Enum.IsDefined(typeof(ProductionTaskKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(ProductionTaskStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (requiredWorkTicks <= 0 || accumulatedWorkTicks < 0 || accumulatedWorkTicks > requiredWorkTicks)
                throw new ArgumentOutOfRangeException(nameof(accumulatedWorkTicks));
            Id = id;
            WorkerId = workerId;
            SiteId = siteId;
            RecipeId = recipeId;
            Kind = kind;
            AccumulatedWorkTicks = accumulatedWorkTicks;
            RequiredWorkTicks = requiredWorkTicks;
            Status = status;
        }

        public StableEntityId Id { get; }
        public StableEntityId WorkerId { get; }
        public StableEntityId SiteId { get; }
        public ProductionRecipeId RecipeId { get; }
        public ProductionTaskKind Kind { get; }
        public long AccumulatedWorkTicks { get; }
        public long RequiredWorkTicks { get; }
        public ProductionTaskStatus Status { get; }
    }

    public sealed class ProductionTask
    {
        private ProductionTask(ProductionTaskState state)
        {
            Id = state.Id;
            WorkerId = state.WorkerId;
            SiteId = state.SiteId;
            RecipeId = state.RecipeId;
            Kind = state.Kind;
            AccumulatedWorkTicks = state.AccumulatedWorkTicks;
            RequiredWorkTicks = state.RequiredWorkTicks;
            Status = state.Status;
        }

        public StableEntityId Id { get; }
        public StableEntityId WorkerId { get; }
        public StableEntityId SiteId { get; }
        public ProductionRecipeId RecipeId { get; }
        public ProductionTaskKind Kind { get; }
        public long AccumulatedWorkTicks { get; private set; }
        public long RequiredWorkTicks { get; }
        public ProductionTaskStatus Status { get; private set; }
        public bool IsTerminal => Status != ProductionTaskStatus.Active;

        public static ProductionTask Create(StableEntityId workerId, StableEntityId siteId,
            ProductionRecipeId recipeId, ProductionTaskKind kind, long requiredWorkTicks) =>
            Restore(new ProductionTaskState(StableEntityId.NewId(), workerId, siteId, recipeId, kind,
                0, requiredWorkTicks, ProductionTaskStatus.Active));

        public static ProductionTask Restore(ProductionTaskState state) => new ProductionTask(state);

        public long AddWork(long ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (Status != ProductionTaskStatus.Active || ticks == 0) return 0;
            var applied = Math.Min(ticks, RequiredWorkTicks - AccumulatedWorkTicks);
            AccumulatedWorkTicks += applied;
            return applied;
        }

        public void Complete()
        {
            if (Status != ProductionTaskStatus.Active || AccumulatedWorkTicks != RequiredWorkTicks)
                throw new InvalidOperationException("Only a fully worked active task can complete.");
            Status = ProductionTaskStatus.Completed;
        }

        public void Cancel()
        {
            if (Status == ProductionTaskStatus.Active) Status = ProductionTaskStatus.Cancelled;
        }

        public ProductionTaskState CaptureState() => new ProductionTaskState(Id, WorkerId, SiteId, RecipeId,
            Kind, AccumulatedWorkTicks, RequiredWorkTicks, Status);
    }

    public readonly struct ResourceStockTarget
    {
        public ResourceStockTarget(ResourceId resourceId, long target)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Resource identity required.", nameof(resourceId));
            if (target < 0) throw new ArgumentOutOfRangeException(nameof(target));
            ResourceId = resourceId;
            Target = target;
        }
        public ResourceId ResourceId { get; }
        public long Target { get; }
    }

    public sealed class ProductionSaveState
    {
        public ProductionSaveState(long lastCalendarTick, long foodRemainderTicks, long foodConsumed,
            long foodShortage, IEnumerable<ResourceStockTarget> targets, IEnumerable<ProductionSiteState> sites,
            IEnumerable<ProductionTaskState> tasks, IEnumerable<ResourceAmount> producedTotals,
            IEnumerable<ResourceInventoryState> inventories,
            IEnumerable<NaturalNodeLifecycleState> naturalLifecycles = null,
            IndustrialSaveState industry = null)
        {
            if (lastCalendarTick < 0 || foodRemainderTicks < 0 || foodConsumed < 0 || foodShortage < 0)
                throw new ArgumentOutOfRangeException(nameof(lastCalendarTick));
            LastCalendarTick = lastCalendarTick;
            FoodRemainderTicks = foodRemainderTicks;
            FoodConsumed = foodConsumed;
            FoodShortage = foodShortage;
            Targets = Array.AsReadOnly((targets ?? Array.Empty<ResourceStockTarget>()).OrderBy(x => x.ResourceId).ToArray());
            Sites = Array.AsReadOnly((sites ?? Array.Empty<ProductionSiteState>()).OrderBy(x => x.Id).ToArray());
            Tasks = Array.AsReadOnly((tasks ?? Array.Empty<ProductionTaskState>()).OrderBy(x => x.Id).ToArray());
            ProducedTotals = Array.AsReadOnly((producedTotals ?? Array.Empty<ResourceAmount>()).OrderBy(x => x.ResourceId).ToArray());
            Inventories = Array.AsReadOnly((inventories ?? Array.Empty<ResourceInventoryState>())
                .OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id).ToArray());
            NaturalLifecycles = Array.AsReadOnly((naturalLifecycles ?? Array.Empty<NaturalNodeLifecycleState>())
                .OrderBy(x => x.SiteId).ToArray());
            Industry = industry;
        }

        public long LastCalendarTick { get; }
        public long FoodRemainderTicks { get; }
        public long FoodConsumed { get; }
        public long FoodShortage { get; }
        public IReadOnlyList<ResourceStockTarget> Targets { get; }
        public IReadOnlyList<ProductionSiteState> Sites { get; }
        public IReadOnlyList<ProductionTaskState> Tasks { get; }
        public IReadOnlyList<ResourceAmount> ProducedTotals { get; }
        public IReadOnlyList<ResourceInventoryState> Inventories { get; }
        public IReadOnlyList<NaturalNodeLifecycleState> NaturalLifecycles { get; }
        public IndustrialSaveState Industry { get; }
    }
}
