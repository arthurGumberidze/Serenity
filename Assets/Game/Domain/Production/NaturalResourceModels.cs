using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Research;
using Game.Domain.Resources;

namespace Game.Domain.Production
{
    public enum NaturalResourceBiome
    {
        Forest = 0,
        Meadow = 1,
        Rocky = 2,
        Mountain = 3,
        Wetland = 4,
        Lake = 5,
        AnimalRange = 6
    }

    public enum IndustrialFacilityKind
    {
        Workshop = 0,
        Quarry = 1,
        Mine = 2,
        FishingStation = 3,
        HuntingLodge = 4
    }

    public readonly struct NaturalNodeLifecycleState
    {
        public NaturalNodeLifecycleState(StableEntityId siteId, NaturalResourceBiome biome,
            long depletedAtTick, int respawnAttempt)
        {
            if (!siteId.IsValid) throw new ArgumentException("Natural node identity required.", nameof(siteId));
            if (!Enum.IsDefined(typeof(NaturalResourceBiome), biome)) throw new ArgumentOutOfRangeException(nameof(biome));
            if (depletedAtTick < -1) throw new ArgumentOutOfRangeException(nameof(depletedAtTick));
            if (respawnAttempt < 0) throw new ArgumentOutOfRangeException(nameof(respawnAttempt));
            SiteId = siteId;
            Biome = biome;
            DepletedAtTick = depletedAtTick;
            RespawnAttempt = respawnAttempt;
        }

        public StableEntityId SiteId { get; }
        public NaturalResourceBiome Biome { get; }
        public long DepletedAtTick { get; }
        public int RespawnAttempt { get; }
    }

    public sealed class NaturalNodeRegistration
    {
        private readonly WorldPosition[] respawnCandidates;

        public NaturalNodeRegistration(ProductionSite site, NaturalResourceBiome biome,
            IEnumerable<WorldPosition> candidates)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            if (site.Kind != ProductionSiteKind.NaturalSource)
                throw new ArgumentException("Natural lifecycle requires a natural production site.", nameof(site));
            if (site.SourceCapacity < 20 || site.SourceCapacity > 50)
                throw new ArgumentOutOfRangeException(nameof(site), "Small natural node capacity must be 20-50 units.");
            if (!Enum.IsDefined(typeof(NaturalResourceBiome), biome)) throw new ArgumentOutOfRangeException(nameof(biome));
            respawnCandidates = (candidates ?? throw new ArgumentNullException(nameof(candidates))).Distinct().ToArray();
            if (respawnCandidates.Length == 0) throw new ArgumentException("At least one respawn candidate is required.", nameof(candidates));
            Biome = biome;
        }

        public ProductionSite Site { get; }
        public NaturalResourceBiome Biome { get; }
        public IReadOnlyList<WorldPosition> RespawnCandidates => Array.AsReadOnly(respawnCandidates);
    }

    public sealed class IndustrialZoneDefinition
    {
        private readonly ResourceAmount[] constructionCost;
        private readonly ResourceAmount[] outputsPerCycle;

        public IndustrialZoneDefinition(string id, IndustrialFacilityKind facilityKind,
            NaturalResourceBiome biome, IEnumerable<ResourceAmount> constructionCost,
            IEnumerable<ResourceAmount> outputsPerCycle, long capacity, int workerCapacity,
            TimeSpan cycleDuration, TechnologyId requiredTechnology,
            TechnologyId restorativeTechnology = default, long restorativeUnitsPerDay = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Industrial zone ID required.", nameof(id));
            if (!Enum.IsDefined(typeof(IndustrialFacilityKind), facilityKind)) throw new ArgumentOutOfRangeException(nameof(facilityKind));
            if (!Enum.IsDefined(typeof(NaturalResourceBiome), biome)) throw new ArgumentOutOfRangeException(nameof(biome));
            if (capacity <= 0 || workerCapacity <= 0 || cycleDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.constructionCost = (constructionCost ?? throw new ArgumentNullException(nameof(constructionCost)))
                .OrderBy(x => x.ResourceId).ToArray();
            this.outputsPerCycle = (outputsPerCycle ?? throw new ArgumentNullException(nameof(outputsPerCycle)))
                .OrderBy(x => x.ResourceId).ToArray();
            if (this.outputsPerCycle.Length == 0) throw new ArgumentException("Industrial output required.", nameof(outputsPerCycle));
            if (!requiredTechnology.IsValid) throw new ArgumentException("Required technology is invalid.", nameof(requiredTechnology));
            if (restorativeUnitsPerDay < 0) throw new ArgumentOutOfRangeException(nameof(restorativeUnitsPerDay));
            Id = id.Trim();
            FacilityKind = facilityKind;
            Biome = biome;
            Capacity = capacity;
            WorkerCapacity = workerCapacity;
            CycleTicks = cycleDuration.Ticks;
            RequiredTechnology = requiredTechnology;
            RestorativeTechnology = restorativeTechnology;
            RestorativeUnitsPerDay = restorativeUnitsPerDay;
        }

        public string Id { get; }
        public IndustrialFacilityKind FacilityKind { get; }
        public NaturalResourceBiome Biome { get; }
        public IReadOnlyList<ResourceAmount> ConstructionCost => Array.AsReadOnly(constructionCost);
        public IReadOnlyList<ResourceAmount> OutputsPerCycle => Array.AsReadOnly(outputsPerCycle);
        public long Capacity { get; }
        public int WorkerCapacity { get; }
        public long CycleTicks { get; }
        public TechnologyId RequiredTechnology { get; }
        public TechnologyId RestorativeTechnology { get; }
        public long RestorativeUnitsPerDay { get; }
    }

    public readonly struct IndustrialZoneState
    {
        public IndustrialZoneState(StableEntityId id, string definitionId, WorldPosition position,
            long remaining, bool facilityBuilt, StableEntityId storageId, bool restorativeInstalled,
            long extractionRemainder, long restorationRemainder)
        {
            if (!id.IsValid) throw new ArgumentException("Industrial zone identity required.", nameof(id));
            if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("Definition ID required.", nameof(definitionId));
            if (remaining < 0 || extractionRemainder < 0 || restorationRemainder < 0)
                throw new ArgumentOutOfRangeException(nameof(remaining));
            if ((facilityBuilt || restorativeInstalled) && !storageId.IsValid)
                throw new ArgumentException("Built industrial facilities require storage.", nameof(storageId));
            Id = id;
            DefinitionId = definitionId;
            Position = position;
            Remaining = remaining;
            FacilityBuilt = facilityBuilt;
            StorageId = storageId;
            RestorativeInstalled = restorativeInstalled;
            ExtractionRemainder = extractionRemainder;
            RestorationRemainder = restorationRemainder;
        }

        public StableEntityId Id { get; }
        public string DefinitionId { get; }
        public WorldPosition Position { get; }
        public long Remaining { get; }
        public bool FacilityBuilt { get; }
        public StableEntityId StorageId { get; }
        public bool RestorativeInstalled { get; }
        public long ExtractionRemainder { get; }
        public long RestorationRemainder { get; }
    }

    public sealed class IndustrialSaveState
    {
        public IndustrialSaveState(IEnumerable<IndustrialZoneState> zones)
        {
            Zones = Array.AsReadOnly((zones ?? Array.Empty<IndustrialZoneState>()).OrderBy(x => x.Id).ToArray());
        }

        public IReadOnlyList<IndustrialZoneState> Zones { get; }
    }
}
