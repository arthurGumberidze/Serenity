using System;
using Game.Domain.Resources;

namespace Game.Domain.Buildings
{
    public enum ConstructionPhase
    {
        LegacyPlanned = 0,
        AwaitingMaterials = 1,
        ReadyForWork = 2,
        Building = 3,
        Completed = 4,
        Cancelled = 5
    }

    public readonly struct ConstructionMaterialProgress
    {
        public ConstructionMaterialProgress(ResourceId resourceId, long requiredUnits, long installedUnits)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Resource identity is required.", nameof(resourceId));
            if (requiredUnits <= 0) throw new ArgumentOutOfRangeException(nameof(requiredUnits));
            if (installedUnits < 0 || installedUnits > requiredUnits)
                throw new ArgumentOutOfRangeException(nameof(installedUnits));
            ResourceId = resourceId;
            RequiredUnits = requiredUnits;
            InstalledUnits = installedUnits;
        }

        public ResourceId ResourceId { get; }
        public long RequiredUnits { get; }
        public long InstalledUnits { get; }
        public long RemainingUnits => RequiredUnits - InstalledUnits;
    }
}
