using System;

namespace Game.Domain.Buildings
{
    public sealed class BuildingDefinition
    {
        public BuildingDefinition(BuildingDefinitionId id, string displayName, BuildingCategory category,
            BuildingFootprint footprint, BuildingPlacementMode placementMode = BuildingPlacementMode.Grid,
            bool allowsOverlap = false)
        {
            if (!id.IsValid) throw new ArgumentException("A valid definition ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
            Category = category;
            Footprint = footprint;
            PlacementMode = placementMode;
            AllowsOverlap = allowsOverlap;
        }

        public BuildingDefinitionId Id { get; }
        public string DisplayName { get; }
        public BuildingCategory Category { get; }
        public BuildingFootprint Footprint { get; }
        public BuildingPlacementMode PlacementMode { get; }
        public bool AllowsOverlap { get; }
    }
}
