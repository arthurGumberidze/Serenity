using System;
using System.Collections.Generic;
using System.Linq;
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
        {
            if (!id.IsValid) throw new ArgumentException("A valid definition ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
            Category = category;
            Footprint = footprint;
            PlacementMode = placementMode;
            AllowsOverlap = allowsOverlap;
            if (storageCapacity < 0) throw new ArgumentOutOfRangeException(nameof(storageCapacity));
            var cost = (constructionCost ?? Array.Empty<ResourceAmount>()).ToArray();
            if (cost.Select(x => x.ResourceId).Distinct().Count() != cost.Length)
                throw new ArgumentException("Construction cost has duplicate resource IDs.", nameof(constructionCost));
            ConstructionCost = Array.AsReadOnly(cost);
            StorageCapacity = storageCapacity;
            StorageCategories = Array.AsReadOnly((storageCategories ?? Enum.GetValues(typeof(ResourceCategory))
                .Cast<ResourceCategory>()).Distinct().OrderBy(x => x).ToArray());
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
        public BuildingFootprint Footprint { get; }
        public BuildingPlacementMode PlacementMode { get; }
        public bool AllowsOverlap { get; }
        public IReadOnlyList<ResourceAmount> ConstructionCost { get; }
        public long StorageCapacity { get; }
        public IReadOnlyList<ResourceCategory> StorageCategories { get; }
        public int MaxIntegrity { get; }
        public int FireDamagePerThousand { get; }
        public int CombatDamagePerThousand { get; }
        public bool Capturable { get; }
    }
}
