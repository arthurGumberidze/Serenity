using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.Resources;

namespace Game.Simulation.Production
{
    public interface IResourceRespawnSurface
    {
        bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius);
    }

    public sealed class ResourceEcologyService
    {
        public static readonly TimeSpan DefaultRespawnDelay = TimeSpan.FromDays(15);
        private readonly Dictionary<StableEntityId, NaturalNodeRegistration> registrations =
            new Dictionary<StableEntityId, NaturalNodeRegistration>();
        private readonly Dictionary<StableEntityId, NaturalNodeLifecycleState> states =
            new Dictionary<StableEntityId, NaturalNodeLifecycleState>();
        private readonly long respawnDelayTicks;

        public ResourceEcologyService(TimeSpan? respawnDelay = null)
        {
            var delay = respawnDelay ?? DefaultRespawnDelay;
            if (delay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(respawnDelay));
            respawnDelayTicks = delay.Ticks;
        }

        public IReadOnlyList<NaturalNodeLifecycleState> States => states.Values.OrderBy(x => x.SiteId).ToArray();

        public void Register(NaturalNodeRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            if (!registrations.TryAdd(registration.Site.Id, registration))
                throw new InvalidOperationException("Natural node is already registered.");
            states.Add(registration.Site.Id, new NaturalNodeLifecycleState(registration.Site.Id,
                registration.Biome, registration.Site.IsDepleted ? 0 : -1, 0));
        }

        public void AdvanceTo(long calendarTick, IResourceRespawnSurface surface)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            foreach (var id in registrations.Keys.OrderBy(x => x))
            {
                var registration = registrations[id];
                var state = states[id];
                if (!registration.Site.IsDepleted)
                {
                    if (state.DepletedAtTick >= 0)
                        states[id] = new NaturalNodeLifecycleState(id, state.Biome, -1, state.RespawnAttempt);
                    continue;
                }
                if (state.DepletedAtTick < 0)
                {
                    state = new NaturalNodeLifecycleState(id, state.Biome, calendarTick, state.RespawnAttempt);
                    states[id] = state;
                }
                if (calendarTick - state.DepletedAtTick < respawnDelayTicks) continue;
                var candidates = registration.RespawnCandidates;
                var start = state.RespawnAttempt % candidates.Count;
                var placed = false;
                for (var offset = 0; offset < candidates.Count; offset++)
                {
                    var candidate = candidates[(start + offset) % candidates.Count];
                    if (!surface.IsFree(state.Biome, candidate, 1.5f)) continue;
                    registration.Site.RespawnNatural(candidate, registration.Site.SourceCapacity);
                    states[id] = new NaturalNodeLifecycleState(id, state.Biome, -1,
                        checked(state.RespawnAttempt + offset + 1));
                    placed = true;
                    break;
                }
                if (!placed)
                    states[id] = new NaturalNodeLifecycleState(id, state.Biome, state.DepletedAtTick,
                        checked(state.RespawnAttempt + 1));
            }
        }

        public NaturalNodeLifecycleState[] CaptureState() => states.Values.OrderBy(x => x.SiteId).ToArray();

        public void RebindSites(IEnumerable<ProductionSite> restoredSites)
        {
            var byId = (restoredSites ?? throw new ArgumentNullException(nameof(restoredSites)))
                .ToDictionary(site => site.Id);
            foreach (var id in registrations.Keys.ToArray())
            {
                if (!byId.TryGetValue(id, out var restored))
                    throw new ArgumentException("Restored production sites do not contain every natural node.",
                        nameof(restoredSites));
                var current = registrations[id];
                registrations[id] = new NaturalNodeRegistration(restored, current.Biome,
                    current.RespawnCandidates);
            }
        }

        public void RestoreState(IEnumerable<NaturalNodeLifecycleState> restored)
        {
            var values = (restored ?? Array.Empty<NaturalNodeLifecycleState>()).ToArray();
            if (values.Length == 0) return;
            var replacement = new Dictionary<StableEntityId, NaturalNodeLifecycleState>();
            foreach (var state in values)
            {
                if (!registrations.TryGetValue(state.SiteId, out var registration) ||
                    registration.Biome != state.Biome || !replacement.TryAdd(state.SiteId, state))
                    throw new ArgumentException("Natural lifecycle snapshot does not match registered nodes.", nameof(restored));
            }
            foreach (var id in registrations.Keys)
                if (!replacement.ContainsKey(id))
                    replacement.Add(id, new NaturalNodeLifecycleState(id, registrations[id].Biome, -1, 0));
            states.Clear();
            foreach (var pair in replacement) states.Add(pair.Key, pair.Value);
        }
    }

    public enum IndustrialCommandFailure
    {
        None = 0,
        UnknownZone = 1,
        TechnologyLocked = 2,
        InvalidPlacement = 3,
        MissingResources = 4,
        MissingStorage = 5,
        AlreadyBuilt = 6,
        FacilityRequired = 7,
        WrongFacility = 8,
        WorkerCapacity = 9,
        Depleted = 10,
        RestorativeMineOnly = 11,
        RestorativeAlreadyInstalled = 12
    }

    public readonly struct IndustrialAdvanceResult
    {
        public IndustrialAdvanceResult(long cycles, long restored, IndustrialCommandFailure failure)
        { Cycles = cycles; Restored = restored; Failure = failure; }
        public long Cycles { get; }
        public long Restored { get; }
        public IndustrialCommandFailure Failure { get; }
        public bool Succeeded => Failure == IndustrialCommandFailure.None;
    }

    public sealed class IndustrialExtractionService
    {
        private sealed class Zone
        {
            public Zone(IndustrialZoneDefinition definition, IndustrialZoneState state)
            { Definition = definition; State = state; }
            public IndustrialZoneDefinition Definition { get; }
            public IndustrialZoneState State { get; set; }
        }

        private readonly ResourceCatalog resources;
        private readonly ResourceInventoryRegistry inventories;
        private readonly Dictionary<string, IndustrialZoneDefinition> definitions;
        private readonly Dictionary<StableEntityId, Zone> zones = new Dictionary<StableEntityId, Zone>();

        public IndustrialExtractionService(ResourceCatalog resourceCatalog, ResourceInventoryRegistry inventoryRegistry,
            IEnumerable<IndustrialZoneDefinition> zoneDefinitions)
        {
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            definitions = (zoneDefinitions ?? throw new ArgumentNullException(nameof(zoneDefinitions)))
                .ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var definition in definitions.Values)
            {
                foreach (var cost in definition.ConstructionCost) resources.Get(cost.ResourceId);
                foreach (var output in definition.OutputsPerCycle) resources.Get(output.ResourceId);
            }
        }

        public IReadOnlyList<IndustrialZoneState> Zones => zones.Values.Select(x => x.State).OrderBy(x => x.Id).ToArray();

        public void AddZone(StableEntityId id, string definitionId, WorldPosition position)
        {
            if (!definitions.TryGetValue(definitionId, out var definition))
                throw new KeyNotFoundException("Unknown industrial zone definition: " + definitionId);
            var state = new IndustrialZoneState(id, definitionId, position, definition.Capacity, false, default,
                false, 0, 0);
            if (!zones.TryAdd(id, new Zone(definition, state)))
                throw new InvalidOperationException("Industrial zone already exists.");
            var owner = new InventoryOwner(InventoryOwnerKind.WorldPile, id);
            if (!inventories.TryGet(owner, out _)) inventories.Add(new ResourceInventory(owner, resources, long.MaxValue / 4));
        }

        public bool TryGetZone(StableEntityId id, out IndustrialZoneState state)
        {
            if (zones.TryGetValue(id, out var zone)) { state = zone.State; return true; }
            state = default;
            return false;
        }

        public IndustrialZoneDefinition GetDefinition(string id) => definitions.TryGetValue(id, out var definition)
            ? definition : throw new KeyNotFoundException("Unknown industrial zone definition: " + id);

        public IndustrialCommandFailure BuildFacility(StableEntityId zoneId, InventoryOwner funding,
            StableEntityId storageId, bool technologyAvailable, bool placementValid)
        {
            if (!zones.TryGetValue(zoneId, out var zone)) return IndustrialCommandFailure.UnknownZone;
            if (zone.State.FacilityBuilt) return IndustrialCommandFailure.AlreadyBuilt;
            if (!technologyAvailable) return IndustrialCommandFailure.TechnologyLocked;
            if (!placementValid) return IndustrialCommandFailure.InvalidPlacement;
            if (!storageId.IsValid || !inventories.TryGet(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storageId), out _))
                return IndustrialCommandFailure.MissingStorage;
            if (!inventories.TryGet(funding, out var source) ||
                zone.Definition.ConstructionCost.Any(x => !source.CanRemove(x.ResourceId, x.Quantity)))
                return IndustrialCommandFailure.MissingResources;
            foreach (var cost in zone.Definition.ConstructionCost)
                if (!source.TryRemove(cost.ResourceId, cost.Quantity))
                    throw new InvalidOperationException("Validated industrial construction cost changed during commit.");
            zone.State = new IndustrialZoneState(zone.State.Id, zone.State.DefinitionId, zone.State.Position,
                zone.State.Remaining, true, storageId, false, zone.State.ExtractionRemainder,
                zone.State.RestorationRemainder);
            return IndustrialCommandFailure.None;
        }

        public IndustrialCommandFailure InstallRestorativeBlock(StableEntityId zoneId, bool technologyAvailable)
        {
            if (!zones.TryGetValue(zoneId, out var zone)) return IndustrialCommandFailure.UnknownZone;
            if (!zone.State.FacilityBuilt) return IndustrialCommandFailure.FacilityRequired;
            if (zone.Definition.FacilityKind != IndustrialFacilityKind.Mine ||
                zone.Definition.RestorativeUnitsPerDay <= 0) return IndustrialCommandFailure.RestorativeMineOnly;
            if (zone.State.RestorativeInstalled) return IndustrialCommandFailure.RestorativeAlreadyInstalled;
            if (!technologyAvailable) return IndustrialCommandFailure.TechnologyLocked;
            zone.State = new IndustrialZoneState(zone.State.Id, zone.State.DefinitionId, zone.State.Position,
                zone.State.Remaining, true, zone.State.StorageId, true, zone.State.ExtractionRemainder,
                zone.State.RestorationRemainder);
            return IndustrialCommandFailure.None;
        }

        public IndustrialAdvanceResult Advance(StableEntityId zoneId, int workers, TimeSpan elapsed)
        {
            if (!zones.TryGetValue(zoneId, out var zone)) return new IndustrialAdvanceResult(0, 0, IndustrialCommandFailure.UnknownZone);
            if (!zone.State.FacilityBuilt) return new IndustrialAdvanceResult(0, 0, IndustrialCommandFailure.FacilityRequired);
            if (workers <= 0 || workers > zone.Definition.WorkerCapacity)
                return new IndustrialAdvanceResult(0, 0, IndustrialCommandFailure.WorkerCapacity);
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            var remaining = zone.State.Remaining;
            var restorationRemainder = zone.State.RestorationRemainder;
            long restored = 0;
            if (zone.State.RestorativeInstalled && elapsed > TimeSpan.Zero)
            {
                var raw = checked(restorationRemainder + elapsed.Ticks * zone.Definition.RestorativeUnitsPerDay);
                restored = Math.Min(zone.Definition.Capacity - remaining, raw / TimeSpan.TicksPerDay);
                restorationRemainder = raw % TimeSpan.TicksPerDay;
                remaining = checked(remaining + restored);
            }
            var rawWork = checked(zone.State.ExtractionRemainder + elapsed.Ticks * (long)workers);
            var cycles = Math.Min(remaining, rawWork / zone.Definition.CycleTicks);
            var extractionRemainder = rawWork % zone.Definition.CycleTicks;
            if (cycles > 0)
            {
                var output = inventories.Get(new InventoryOwner(InventoryOwnerKind.WorldPile, zone.State.Id));
                foreach (var amount in zone.Definition.OutputsPerCycle)
                {
                    var units = checked(amount.Quantity.Units * cycles);
                    if (!output.TryAdd(amount.ResourceId, new ResourceQuantity(units)))
                        throw new InvalidOperationException("Industrial output capacity changed during commit.");
                }
                remaining -= cycles;
            }
            zone.State = new IndustrialZoneState(zone.State.Id, zone.State.DefinitionId, zone.State.Position,
                remaining, true, zone.State.StorageId, zone.State.RestorativeInstalled, extractionRemainder,
                restorationRemainder);
            return new IndustrialAdvanceResult(cycles, restored,
                remaining == 0 && cycles == 0 ? IndustrialCommandFailure.Depleted : IndustrialCommandFailure.None);
        }

        public IndustrialSaveState CaptureState() => new IndustrialSaveState(Zones);

        public void RestoreState(IndustrialSaveState save)
        {
            if (save == null) return;
            var replacement = new Dictionary<StableEntityId, Zone>();
            foreach (var state in save.Zones)
            {
                if (!definitions.TryGetValue(state.DefinitionId, out var definition) ||
                    state.Remaining > definition.Capacity || !replacement.TryAdd(state.Id, new Zone(definition, state)))
                    throw new ArgumentException("Invalid industrial snapshot.", nameof(save));
            }
            zones.Clear();
            foreach (var pair in replacement)
            {
                zones.Add(pair.Key, pair.Value);
                var owner = new InventoryOwner(InventoryOwnerKind.WorldPile, pair.Key);
                if (!inventories.TryGet(owner, out _)) inventories.Add(new ResourceInventory(owner, resources, long.MaxValue / 4));
            }
        }
    }
}
