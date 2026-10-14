using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Production;

namespace Game.Domain.Research
{
    internal static class ResearchKey
    {
        public static void Validate(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                throw new ArgumentException("Definition key must contain 1-64 characters.", parameterName);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new ArgumentException("Definition key must use lowercase ASCII, digits, '_' or '-'.", parameterName);
            }
        }
    }

    public readonly struct TechnologyId : IEquatable<TechnologyId>, IComparable<TechnologyId>
    {
        private readonly string value;

        public TechnologyId(string value)
        {
            ResearchKey.Validate(value, nameof(value));
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        public int CompareTo(TechnologyId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(TechnologyId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is TechnologyId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(TechnologyId left, TechnologyId right) => left.Equals(right);
        public static bool operator !=(TechnologyId left, TechnologyId right) => !left.Equals(right);
    }

    public readonly struct CapabilityId : IEquatable<CapabilityId>, IComparable<CapabilityId>
    {
        private readonly string value;

        public CapabilityId(string value)
        {
            ResearchKey.Validate(value, nameof(value));
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        public int CompareTo(CapabilityId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(CapabilityId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is CapabilityId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(CapabilityId left, CapabilityId right) => left.Equals(right);
        public static bool operator !=(CapabilityId left, CapabilityId right) => !left.Equals(right);
    }

    public enum TechnologyEra { StoneAge = 0 }
    public enum TechnologyUnlockKind { Building = 0, ProductionRecipe = 1, Capability = 2 }

    public readonly struct TechnologyUnlock : IEquatable<TechnologyUnlock>
    {
        private TechnologyUnlock(TechnologyUnlockKind kind, string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId)) throw new ArgumentException("Unlock content ID required.", nameof(contentId));
            Kind = kind;
            ContentId = contentId;
        }

        public TechnologyUnlockKind Kind { get; }
        public string ContentId { get; }
        public static TechnologyUnlock Building(BuildingDefinitionId id) =>
            new TechnologyUnlock(TechnologyUnlockKind.Building, id.ToString());
        public static TechnologyUnlock Recipe(ProductionRecipeId id) =>
            new TechnologyUnlock(TechnologyUnlockKind.ProductionRecipe, id.ToString());
        public static TechnologyUnlock Capability(CapabilityId id) =>
            new TechnologyUnlock(TechnologyUnlockKind.Capability, id.ToString());
        public bool Equals(TechnologyUnlock other) => Kind == other.Kind &&
            StringComparer.Ordinal.Equals(ContentId, other.ContentId);
        public override bool Equals(object obj) => obj is TechnologyUnlock other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(ContentId);
    }

    public sealed class TechnologyDefinition
    {
        private readonly TechnologyId[] prerequisites;
        private readonly TechnologyUnlock[] unlocks;

        public TechnologyDefinition(TechnologyId id, string displayName, IEnumerable<TechnologyId> prerequisites,
            TimeSpan researchWorkRequired, IEnumerable<TechnologyUnlock> unlocks,
            TechnologyEra era = TechnologyEra.StoneAge, string description = null, bool startingKnowledge = false)
        {
            if (!id.IsValid) throw new ArgumentException("Technology identity required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Technology name required.", nameof(displayName));
            if (researchWorkRequired <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(researchWorkRequired));
            if (!Enum.IsDefined(typeof(TechnologyEra), era)) throw new ArgumentOutOfRangeException(nameof(era));
            this.prerequisites = (prerequisites ?? Array.Empty<TechnologyId>()).OrderBy(x => x).ToArray();
            this.unlocks = (unlocks ?? Array.Empty<TechnologyUnlock>())
                .OrderBy(x => x.Kind).ThenBy(x => x.ContentId, StringComparer.Ordinal).ToArray();
            if (this.prerequisites.Any(x => !x.IsValid) || this.prerequisites.Distinct().Count() != this.prerequisites.Length)
                throw new ArgumentException("Prerequisites must be valid and unique.", nameof(prerequisites));
            if (this.unlocks.Distinct().Count() != this.unlocks.Length)
                throw new ArgumentException("Unlocks must be unique.", nameof(unlocks));
            Id = id;
            DisplayName = displayName.Trim();
            ResearchWorkRequiredTicks = researchWorkRequired.Ticks;
            Era = era;
            Description = description?.Trim() ?? string.Empty;
            StartingKnowledge = startingKnowledge;
        }

        public TechnologyId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<TechnologyId> Prerequisites => Array.AsReadOnly(prerequisites);
        public long ResearchWorkRequiredTicks { get; }
        public IReadOnlyList<TechnologyUnlock> Unlocks => Array.AsReadOnly(unlocks);
        public TechnologyEra Era { get; }
        public string Description { get; }
        public bool StartingKnowledge { get; }
    }

    public sealed class TechnologyUnlockTargets
    {
        private readonly HashSet<string> buildings;
        private readonly HashSet<string> recipes;
        private readonly HashSet<string> capabilities;

        public TechnologyUnlockTargets(IEnumerable<BuildingDefinitionId> buildingIds,
            IEnumerable<ProductionRecipeId> recipeIds, IEnumerable<CapabilityId> capabilityIds)
        {
            buildings = new HashSet<string>((buildingIds ?? Array.Empty<BuildingDefinitionId>()).Select(x => x.ToString()), StringComparer.Ordinal);
            recipes = new HashSet<string>((recipeIds ?? Array.Empty<ProductionRecipeId>()).Select(x => x.ToString()), StringComparer.Ordinal);
            capabilities = new HashSet<string>((capabilityIds ?? Array.Empty<CapabilityId>()).Select(x => x.ToString()), StringComparer.Ordinal);
        }

        public bool Contains(TechnologyUnlock unlock) => unlock.Kind == TechnologyUnlockKind.Building
            ? buildings.Contains(unlock.ContentId)
            : unlock.Kind == TechnologyUnlockKind.ProductionRecipe
                ? recipes.Contains(unlock.ContentId)
                : capabilities.Contains(unlock.ContentId);
    }

    public sealed class TechnologyCatalog
    {
        private readonly Dictionary<TechnologyId, TechnologyDefinition> definitions;
        private readonly TechnologyDefinition[] ordered;

        public TechnologyCatalog(IEnumerable<TechnologyDefinition> definitions, TechnologyUnlockTargets targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            this.definitions = new Dictionary<TechnologyId, TechnologyDefinition>();
            foreach (var definition in definitions ?? throw new ArgumentNullException(nameof(definitions)))
            {
                if (definition == null || !this.definitions.TryAdd(definition.Id, definition))
                    throw new ArgumentException("Technologies must be non-null with unique IDs.", nameof(definitions));
            }
            if (this.definitions.Count == 0) throw new ArgumentException("Technology catalog cannot be empty.", nameof(definitions));
            foreach (var definition in this.definitions.Values)
            {
                foreach (var prerequisite in definition.Prerequisites)
                {
                    if (prerequisite == definition.Id) throw new ArgumentException("Technology cannot depend on itself.", nameof(definitions));
                    if (!this.definitions.ContainsKey(prerequisite))
                        throw new ArgumentException("Technology prerequisite does not exist: " + prerequisite, nameof(definitions));
                }
                foreach (var unlock in definition.Unlocks)
                    if (!targets.Contains(unlock))
                        throw new ArgumentException("Technology unlock does not resolve: " + unlock.ContentId, nameof(definitions));
            }
            ValidateAcyclic();
            ordered = this.definitions.Values.OrderBy(x => x.Id).ToArray();
        }

        public IReadOnlyList<TechnologyDefinition> All => Array.AsReadOnly(ordered);
        public IReadOnlyList<TechnologyDefinition> StartingKnowledge =>
            ordered.Where(x => x.StartingKnowledge).ToArray();
        public TechnologyDefinition Get(TechnologyId id) => definitions.TryGetValue(id, out var definition)
            ? definition : throw new KeyNotFoundException("Unknown technology: " + id);

        private void ValidateAcyclic()
        {
            var visiting = new HashSet<TechnologyId>();
            var visited = new HashSet<TechnologyId>();
            foreach (var id in definitions.Keys) Visit(id, visiting, visited);
        }

        private void Visit(TechnologyId id, HashSet<TechnologyId> visiting, HashSet<TechnologyId> visited)
        {
            if (visited.Contains(id)) return;
            if (!visiting.Add(id)) throw new ArgumentException("Technology graph contains a cycle.");
            foreach (var prerequisite in definitions[id].Prerequisites) Visit(prerequisite, visiting, visited);
            visiting.Remove(id);
            visited.Add(id);
        }

        public static TechnologyCatalog CreateStoneAge(ProductionCatalog production,
            IEnumerable<BuildingDefinitionId> buildingIds)
        {
            if (production == null) throw new ArgumentNullException(nameof(production));
            var capabilities = new[]
            {
                new CapabilityId("basic_survival"), new CapabilityId("controlled_fire"),
                new CapabilityId("fiber_working"), new CapabilityId("hunting"),
                new CapabilityId("food_preservation"), new CapabilityId("organized_labor"),
                new CapabilityId("weaving"), new CapabilityId("advanced_stonecraft"),
                new CapabilityId("stone_age_completed")
            };
            TechnologyId T(string id) => new TechnologyId(id);
            TechnologyUnlock C(string id) => TechnologyUnlock.Capability(new CapabilityId(id));
            var stoneToolUnlocks = new List<TechnologyUnlock>
            {
                TechnologyUnlock.Recipe(new ProductionRecipeId("gather_stone")),
                TechnologyUnlock.Recipe(new ProductionRecipeId("chop_wood"))
            };
            if (production.All.Any(x => x.Id == new ProductionRecipeId("gather_iron")))
                stoneToolUnlocks.Add(TechnologyUnlock.Recipe(new ProductionRecipeId("gather_iron")));
            if (production.All.Any(x => x.Id == new ProductionRecipeId("gather_gold")))
                stoneToolUnlocks.Add(TechnologyUnlock.Recipe(new ProductionRecipeId("gather_gold")));
            var definitions = new[]
            {
                new TechnologyDefinition(T("basic_survival"), "Basic Survival", null, TimeSpan.FromDays(1),
                    new[] { C("basic_survival") }, description: "Shared survival practices.", startingKnowledge: true),
                new TechnologyDefinition(T("gathering"), "Gathering", new[] { T("basic_survival") }, TimeSpan.FromDays(1),
                    new[] { TechnologyUnlock.Recipe(new ProductionRecipeId("gather_berries")),
                        TechnologyUnlock.Recipe(new ProductionRecipeId("gather_fiber")) }, startingKnowledge: true),
                new TechnologyDefinition(T("controlled_fire"), "Controlled Fire", new[] { T("basic_survival") }, TimeSpan.FromDays(2),
                    new[] { C("controlled_fire") }),
                new TechnologyDefinition(T("shelter_construction"), "Shelter Construction", new[] { T("basic_survival"), T("gathering") }, TimeSpan.FromDays(2),
                    new[] { TechnologyUnlock.Building(new BuildingDefinitionId("primitive_shelter")) }),
                new TechnologyDefinition(T("stone_tools"), "Stone Tools", new[] { T("gathering") }, TimeSpan.FromDays(3),
                    stoneToolUnlocks),
                new TechnologyDefinition(T("fiber_working"), "Fiber Working", new[] { T("gathering") }, TimeSpan.FromDays(2),
                    new[] { C("fiber_working") }),
                new TechnologyDefinition(T("woodworking"), "Woodworking", new[] { T("stone_tools") }, TimeSpan.FromDays(3),
                    new[] { TechnologyUnlock.Building(new BuildingDefinitionId("storage_basket")) }),
                new TechnologyDefinition(T("hunting"), "Organized Hunting", new[] { T("stone_tools") }, TimeSpan.FromDays(3),
                    new[] { C("hunting") }),
                new TechnologyDefinition(T("food_preservation"), "Food Preservation", new[] { T("controlled_fire"), T("hunting") }, TimeSpan.FromDays(4),
                    new[] { C("food_preservation") }),
                new TechnologyDefinition(T("organized_labor"), "Organized Labor", new[] { T("shelter_construction"), T("woodworking") }, TimeSpan.FromDays(4),
                    new[] { C("organized_labor") }),
                new TechnologyDefinition(T("primitive_agriculture"), "Primitive Agriculture",
                    new[] { T("controlled_fire"), T("fiber_working"), T("stone_tools") }, TimeSpan.FromDays(5),
                    new[] { TechnologyUnlock.Building(new BuildingDefinitionId("primitive_crop_plot")),
                        TechnologyUnlock.Recipe(new ProductionRecipeId("cultivate_berries")) }),
                new TechnologyDefinition(T("weaving"), "Weaving", new[] { T("fiber_working"), T("organized_labor") }, TimeSpan.FromDays(4),
                    new[] { C("weaving") }),
                new TechnologyDefinition(T("advanced_stonecraft"), "Advanced Stonecraft",
                    new[] { T("stone_tools"), T("woodworking"), T("hunting") }, TimeSpan.FromDays(5),
                    new[] { C("advanced_stonecraft") }),
                new TechnologyDefinition(T("stone_age_mastery"), "Stone Age Mastery",
                    new[] { T("food_preservation"), T("primitive_agriculture"), T("weaving"), T("advanced_stonecraft") },
                    TimeSpan.FromDays(6), new[] { C("stone_age_completed") })
            };
            return new TechnologyCatalog(definitions, new TechnologyUnlockTargets(buildingIds,
                production.All.Select(x => x.Id), capabilities));
        }
    }

    public interface IResearchUnlockView
    {
        bool IsBuildingUnlocked(BuildingDefinitionId id);
        bool IsProductionRecipeUnlocked(ProductionRecipeId id);
        bool HasCapability(CapabilityId id);
    }

    public sealed class ResearchUnlockSet : IResearchUnlockView
    {
        private readonly HashSet<string> buildings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> recipes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> capabilities = new HashSet<string>(StringComparer.Ordinal);

        public ResearchUnlockSet(TechnologyCatalog catalog, IEnumerable<TechnologyId> completed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (var technologyId in completed ?? throw new ArgumentNullException(nameof(completed)))
                foreach (var unlock in catalog.Get(technologyId).Unlocks)
                {
                    if (unlock.Kind == TechnologyUnlockKind.Building) buildings.Add(unlock.ContentId);
                    else if (unlock.Kind == TechnologyUnlockKind.ProductionRecipe) recipes.Add(unlock.ContentId);
                    else capabilities.Add(unlock.ContentId);
                }
        }

        public bool IsBuildingUnlocked(BuildingDefinitionId id) => buildings.Contains(id.ToString());
        public bool IsProductionRecipeUnlocked(ProductionRecipeId id) => recipes.Contains(id.ToString());
        public bool HasCapability(CapabilityId id) => capabilities.Contains(id.ToString());
    }

    public readonly struct ResearchProjectState
    {
        public ResearchProjectState(TechnologyId technologyId, long accumulatedWorkTicks,
            long requiredWorkTicks, long startTick)
        {
            if (!technologyId.IsValid) throw new ArgumentException("Technology identity required.", nameof(technologyId));
            if (requiredWorkTicks <= 0 || accumulatedWorkTicks < 0 || accumulatedWorkTicks >= requiredWorkTicks)
                throw new ArgumentOutOfRangeException(nameof(accumulatedWorkTicks));
            if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick));
            TechnologyId = technologyId;
            AccumulatedWorkTicks = accumulatedWorkTicks;
            RequiredWorkTicks = requiredWorkTicks;
            StartTick = startTick;
        }

        public TechnologyId TechnologyId { get; }
        public long AccumulatedWorkTicks { get; }
        public long RequiredWorkTicks { get; }
        public long StartTick { get; }
    }

    public readonly struct ResearchWorkerState
    {
        public ResearchWorkerState(StableEntityId workerId, long lastProcessedTick, int fractionalRemainder)
        {
            if (!workerId.IsValid) throw new ArgumentException("Worker identity required.", nameof(workerId));
            if (lastProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedTick));
            if (fractionalRemainder < 0 || fractionalRemainder >= 1000)
                throw new ArgumentOutOfRangeException(nameof(fractionalRemainder));
            WorkerId = workerId;
            LastProcessedTick = lastProcessedTick;
            FractionalRemainder = fractionalRemainder;
        }

        public StableEntityId WorkerId { get; }
        public long LastProcessedTick { get; }
        public int FractionalRemainder { get; }
    }

    public sealed class ResearchSaveState
    {
        public ResearchSaveState(long lastProcessedTick, IEnumerable<TechnologyId> completed,
            ResearchProjectState? activeProject, IEnumerable<ResearchWorkerState> workers)
        {
            if (lastProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedTick));
            LastProcessedTick = lastProcessedTick;
            Completed = Array.AsReadOnly((completed ?? Array.Empty<TechnologyId>()).Distinct().OrderBy(x => x).ToArray());
            ActiveProject = activeProject;
            Workers = Array.AsReadOnly((workers ?? Array.Empty<ResearchWorkerState>()).OrderBy(x => x.WorkerId).ToArray());
        }

        public long LastProcessedTick { get; }
        public IReadOnlyList<TechnologyId> Completed { get; }
        public ResearchProjectState? ActiveProject { get; }
        public IReadOnlyList<ResearchWorkerState> Workers { get; }
    }
}
