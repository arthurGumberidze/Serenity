using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Research;
using Game.Domain.Resources;

namespace Game.Domain.Buildings
{
    public sealed class BuildingDefinition
    {
        public BuildingDefinition(BuildingDefinitionId id, string displayName, BuildingCategory category,
            BuildingFootprint footprint, BuildingPlacementMode placementMode = BuildingPlacementMode.Grid,
            bool allowsOverlap = false, IEnumerable<ResourceAmount> constructionCost = null,
            long storageCapacity = 0, IEnumerable<ResourceCategory> storageCategories = null,
            int maxIntegrity = 1000, int fireDamagePerThousand = 1000,
            int combatDamagePerThousand = 1000, bool capturable = true)
            : this(id, displayName, category, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(constructionCost, TimeSpan.FromTicks(1)),
                new PlacementSpec(footprint, placementMode, allowsOverlap), null,
                storageCapacity > 0 ? new StorageSpec(storageCapacity, storageCategories ??
                    Enum.GetValues(typeof(ResourceCategory)).Cast<ResourceCategory>()) : null,
                null, null, null, null, maxIntegrity, fireDamagePerThousand,
                combatDamagePerThousand, capturable)
        {
        }

        public BuildingDefinition(BuildingDefinitionId id, string displayName, BuildingCategory category,
            BuildingEra era, string description, string iconKey, IEnumerable<TechnologyId> requiredTechnologies,
            ConstructionSpec construction, PlacementSpec placement, HousingSpec housing = null,
            StorageSpec storage = null, WorkplaceSpec workplace = null, ProductionSpec production = null,
            MaintenanceSpec maintenance = null, UpgradeSpec upgrades = null,
            int maxIntegrity = 1000, int fireDamagePerThousand = 1000,
            int combatDamagePerThousand = 1000, bool capturable = true)
        {
            if (!id.IsValid) throw new ArgumentException("A valid definition ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            if (!Enum.IsDefined(typeof(BuildingCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (!Enum.IsDefined(typeof(BuildingEra), era)) throw new ArgumentOutOfRangeException(nameof(era));
            if (construction == null) throw new ArgumentNullException(nameof(construction));
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            var technologies = (requiredTechnologies ?? Array.Empty<TechnologyId>()).OrderBy(x => x).ToArray();
            if (technologies.Any(x => !x.IsValid) || technologies.Distinct().Count() != technologies.Length)
                throw new ArgumentException("Required technologies must be valid and unique.", nameof(requiredTechnologies));
            Id = id;
            DisplayName = displayName.Trim();
            Category = category;
            Era = era;
            Description = description?.Trim() ?? string.Empty;
            IconKey = iconKey?.Trim() ?? string.Empty;
            RequiredTechnologies = Array.AsReadOnly(technologies);
            Construction = construction;
            Placement = placement;
            Housing = housing;
            Storage = storage;
            Workplace = workplace;
            Production = production;
            Maintenance = maintenance;
            Upgrades = upgrades;
            if (maxIntegrity <= 0) throw new ArgumentOutOfRangeException(nameof(maxIntegrity));
            if (fireDamagePerThousand <= 0 || fireDamagePerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(fireDamagePerThousand));
            if (combatDamagePerThousand <= 0 || combatDamagePerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(combatDamagePerThousand));
            MaxIntegrity = maxIntegrity;
            FireDamagePerThousand = fireDamagePerThousand;
            CombatDamagePerThousand = combatDamagePerThousand;
            Capturable = capturable;
        }

        public BuildingDefinitionId Id { get; }
        public string DisplayName { get; }
        public BuildingCategory Category { get; }
        public BuildingEra Era { get; }
        public string Description { get; }
        public string IconKey { get; }
        public IReadOnlyList<TechnologyId> RequiredTechnologies { get; }
        public ConstructionSpec Construction { get; }
        public PlacementSpec Placement { get; }
        public HousingSpec Housing { get; }
        public StorageSpec Storage { get; }
        public WorkplaceSpec Workplace { get; }
        public ProductionSpec Production { get; }
        public MaintenanceSpec Maintenance { get; }
        public UpgradeSpec Upgrades { get; }
        public BuildingFootprint Footprint => Placement.Footprint;
        public BuildingPlacementMode PlacementMode => Placement.Mode;
        public bool AllowsOverlap => Placement.AllowsOverlap;
        public IReadOnlyList<ResourceAmount> ConstructionCost => Construction.Cost;
        public long StorageCapacity => Storage?.Capacity ?? 0;
        public IReadOnlyList<ResourceCategory> StorageCategories => Storage?.AcceptedCategories ?? Array.Empty<ResourceCategory>();
        public int MaxIntegrity { get; }
        public int FireDamagePerThousand { get; }
        public int CombatDamagePerThousand { get; }
        public bool Capturable { get; }
    }
}
