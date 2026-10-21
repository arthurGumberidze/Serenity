using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Combat;

namespace Game.Domain.Buildings
{
    public readonly struct BuildingState
    {
        public BuildingState(StableEntityId id, BuildingDefinitionId definitionId, GridCoordinate coordinate,
            BuildingOrientation orientation, ConstructionState constructionState)
            : this(id, definitionId, coordinate, orientation, constructionState, 1000, 1000,
                BuildingStructuralStatus.Intact, new CombatSideId("player"), BuildingFireStatus.None,
                0, 0, 0, 0, false, false, null, null)
        {
        }

        public BuildingState(StableEntityId id, BuildingDefinitionId definitionId, GridCoordinate coordinate,
            BuildingOrientation orientation, ConstructionState constructionState, int maxIntegrity,
            int currentIntegrity, BuildingStructuralStatus structuralStatus, CombatSideId ownerSideId,
            BuildingFireStatus fireStatus, int fireIntensity, long fireStartTick, long lastFireProcessedTick,
            long accumulatedFireDamage, bool destructionProcessed, bool storageSpilled,
            IEnumerable<StableEntityId> processedDamageEvents, IEnumerable<BuildingEventState> history,
            long constructionWorkTicks = 0, long constructionRequiredTicks = 0)
        {
            if (!id.IsValid) throw new ArgumentException("Building identity is required.", nameof(id));
            if (!definitionId.IsValid) throw new ArgumentException("Definition identity is required.", nameof(definitionId));
            if (!Enum.IsDefined(typeof(BuildingOrientation), orientation)) throw new ArgumentOutOfRangeException(nameof(orientation));
            if (!Enum.IsDefined(typeof(ConstructionState), constructionState)) throw new ArgumentOutOfRangeException(nameof(constructionState));
            if (maxIntegrity <= 0 || currentIntegrity < 0 || currentIntegrity > maxIntegrity)
                throw new ArgumentOutOfRangeException(nameof(currentIntegrity));
            if (!Enum.IsDefined(typeof(BuildingStructuralStatus), structuralStatus))
                throw new ArgumentOutOfRangeException(nameof(structuralStatus));
            if (StructuralStatusFor(currentIntegrity, maxIntegrity) != structuralStatus)
                throw new ArgumentException("Structural status does not match integrity.", nameof(structuralStatus));
            if (!ownerSideId.IsValid) throw new ArgumentException("Building owner side is required.", nameof(ownerSideId));
            if (!Enum.IsDefined(typeof(BuildingFireStatus), fireStatus)) throw new ArgumentOutOfRangeException(nameof(fireStatus));
            if (fireIntensity < 0 || fireIntensity > 1000) throw new ArgumentOutOfRangeException(nameof(fireIntensity));
            if (fireStartTick < 0 || lastFireProcessedTick < 0 || accumulatedFireDamage < 0 ||
                lastFireProcessedTick < fireStartTick) throw new ArgumentOutOfRangeException(nameof(lastFireProcessedTick));
            if ((fireStatus == BuildingFireStatus.Burning) != (fireIntensity > 0))
                throw new ArgumentException("Only a burning building may have positive fire intensity.", nameof(fireIntensity));
            if ((structuralStatus == BuildingStructuralStatus.Destroyed) != destructionProcessed)
                throw new ArgumentException("Destroyed state and destruction marker must agree.", nameof(destructionProcessed));
            if (storageSpilled && !destructionProcessed)
                throw new ArgumentException("Storage may be marked spilled only for a destroyed building.", nameof(storageSpilled));
            var processed = (processedDamageEvents ?? Array.Empty<StableEntityId>()).ToArray();
            if (processed.Any(x => !x.IsValid) || processed.Distinct().Count() != processed.Length)
                throw new ArgumentException("Processed damage events must be valid and unique.", nameof(processedDamageEvents));
            var eventHistory = (history ?? Array.Empty<BuildingEventState>()).ToArray();
            if (eventHistory.Any(x => x == null) || eventHistory.Select(x => x.Sequence).Distinct().Count() != eventHistory.Length ||
                !eventHistory.Select(x => x.Sequence).SequenceEqual(eventHistory.Select(x => x.Sequence).OrderBy(x => x)))
                throw new ArgumentException("Building history must contain unique ordered sequences.", nameof(history));
            Id = id;
            DefinitionId = definitionId;
            Coordinate = coordinate;
            Orientation = orientation;
            ConstructionState = constructionState;
            if (constructionWorkTicks < 0 || constructionRequiredTicks < constructionWorkTicks)
                throw new ArgumentOutOfRangeException(nameof(constructionWorkTicks));
            ConstructionWorkTicks = constructionWorkTicks;
            ConstructionRequiredTicks = constructionRequiredTicks;
            MaxIntegrity = maxIntegrity;
            CurrentIntegrity = currentIntegrity;
            StructuralStatus = structuralStatus;
            OwnerSideId = ownerSideId;
            FireStatus = fireStatus;
            FireIntensity = fireIntensity;
            FireStartTick = fireStartTick;
            LastFireProcessedTick = lastFireProcessedTick;
            AccumulatedFireDamage = accumulatedFireDamage;
            DestructionProcessed = destructionProcessed;
            StorageSpilled = storageSpilled;
            ProcessedDamageEvents = Array.AsReadOnly(processed);
            History = Array.AsReadOnly(eventHistory);
        }

        public StableEntityId Id { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridCoordinate Coordinate { get; }
        public BuildingOrientation Orientation { get; }
        public ConstructionState ConstructionState { get; }
        public long ConstructionWorkTicks { get; }
        public long ConstructionRequiredTicks { get; }
        public int MaxIntegrity { get; }
        public int CurrentIntegrity { get; }
        public BuildingStructuralStatus StructuralStatus { get; }
        public CombatSideId OwnerSideId { get; }
        public BuildingFireStatus FireStatus { get; }
        public int FireIntensity { get; }
        public long FireStartTick { get; }
        public long LastFireProcessedTick { get; }
        public long AccumulatedFireDamage { get; }
        public bool DestructionProcessed { get; }
        public bool StorageSpilled { get; }
        public IReadOnlyList<StableEntityId> ProcessedDamageEvents { get; }
        public IReadOnlyList<BuildingEventState> History { get; }

        private static BuildingStructuralStatus StructuralStatusFor(int integrity, int maximum) => integrity == 0
            ? BuildingStructuralStatus.Destroyed
            : integrity == maximum ? BuildingStructuralStatus.Intact
            : (long)integrity * 4L <= maximum ? BuildingStructuralStatus.Critical
            : BuildingStructuralStatus.Damaged;
    }

    public sealed class Building
    {
        private Building(BuildingState state)
        {
            Id = state.Id;
            DefinitionId = state.DefinitionId;
            Coordinate = state.Coordinate;
            Orientation = state.Orientation;
            ConstructionState = state.ConstructionState;
            ConstructionWorkTicks = state.ConstructionWorkTicks;
            ConstructionRequiredTicks = state.ConstructionRequiredTicks;
            MaxIntegrity = state.MaxIntegrity;
            CurrentIntegrity = state.CurrentIntegrity;
            StructuralStatus = state.StructuralStatus;
            OwnerSideId = state.OwnerSideId;
            FireStatus = state.FireStatus;
            FireIntensity = state.FireIntensity;
            FireStartTick = state.FireStartTick;
            LastFireProcessedTick = state.LastFireProcessedTick;
            AccumulatedFireDamage = state.AccumulatedFireDamage;
            DestructionProcessed = state.DestructionProcessed;
            StorageSpilled = state.StorageSpilled;
            processedDamageEvents = new HashSet<StableEntityId>(state.ProcessedDamageEvents);
            history = new List<BuildingEventState>(state.History);
            nextEventSequence = history.Count == 0 ? 1UL : checked(history[history.Count - 1].Sequence + 1UL);
        }

        private readonly HashSet<StableEntityId> processedDamageEvents;
        private readonly List<BuildingEventState> history;
        private ulong nextEventSequence;

        public StableEntityId Id { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridCoordinate Coordinate { get; }
        public BuildingOrientation Orientation { get; }
        public ConstructionState ConstructionState { get; private set; }
        public long ConstructionWorkTicks { get; private set; }
        public long ConstructionRequiredTicks { get; private set; }
        public double ConstructionProgress => ConstructionState == ConstructionState.Completed ? 1d :
            ConstructionRequiredTicks <= 0 ? 0d : (double)ConstructionWorkTicks / ConstructionRequiredTicks;

        public void RequireConstructionWork(TimeSpan duration)
        {
            if (duration.Ticks <= 0 || ConstructionState == ConstructionState.Completed)
                throw new InvalidOperationException("Only an unfinished building may require work.");
            ConstructionRequiredTicks = duration.Ticks;
        }

        public void AdvanceConstruction(TimeSpan work)
        {
            if (work.Ticks < 0) throw new ArgumentOutOfRangeException(nameof(work));
            if (IsDestroyed || IsOperational || ConstructionRequiredTicks == 0) return;
            ConstructionWorkTicks += Math.Min(work.Ticks, ConstructionRequiredTicks - ConstructionWorkTicks);
            if (ConstructionWorkTicks == ConstructionRequiredTicks) MarkCompleted();
        }
        public int MaxIntegrity { get; }
        public int CurrentIntegrity { get; private set; }
        public BuildingStructuralStatus StructuralStatus { get; private set; }
        public CombatSideId OwnerSideId { get; private set; }
        public BuildingFireStatus FireStatus { get; private set; }
        public int FireIntensity { get; private set; }
        public long FireStartTick { get; private set; }
        public long LastFireProcessedTick { get; private set; }
        public long AccumulatedFireDamage { get; private set; }
        public bool DestructionProcessed { get; private set; }
        public bool StorageSpilled { get; private set; }
        public bool IsDestroyed => StructuralStatus == BuildingStructuralStatus.Destroyed;
        public bool IsOperational => ConstructionState == ConstructionState.Completed && !IsDestroyed;
        public IReadOnlyList<BuildingEventState> History => history.AsReadOnly();

        public static Building CreateNew(BuildingDefinitionId definitionId, GridCoordinate coordinate,
            BuildingOrientation orientation) => Restore(new BuildingState(StableEntityId.NewId(), definitionId,
            coordinate, orientation, ConstructionState.Planned));

        public static Building CreateNew(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, CombatSideId ownerSideId) => definition == null
            ? throw new ArgumentNullException(nameof(definition))
            : Restore(new BuildingState(StableEntityId.NewId(), definition.Id, coordinate, orientation,
                ConstructionState.Planned, definition.MaxIntegrity, definition.MaxIntegrity,
                BuildingStructuralStatus.Intact, ownerSideId, BuildingFireStatus.None, 0, 0, 0, 0,
                false, false, null, null));

        public static Building CreateNew(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, CombatSideId ownerSideId, StableEntityId id) => definition == null
            ? throw new ArgumentNullException(nameof(definition))
            : !id.IsValid ? throw new ArgumentException("A stable building identity is required.", nameof(id))
            : Restore(new BuildingState(id, definition.Id, coordinate, orientation,
                ConstructionState.Planned, definition.MaxIntegrity, definition.MaxIntegrity,
                BuildingStructuralStatus.Intact, ownerSideId, BuildingFireStatus.None, 0, 0, 0, 0,
                false, false, null, null));

        public static Building Restore(BuildingState state) => new Building(state);

        public void MarkCompleted()
        {
            if (ConstructionState == ConstructionState.Completed) return;
            ConstructionState = ConstructionState.Completed;
        }

        public bool HasProcessedDamage(StableEntityId eventId) => processedDamageEvents.Contains(eventId);

        public int ApplyDamage(StableEntityId eventId, int amount, BuildingDamageType type, long calendarTick)
        {
            if (!eventId.IsValid) throw new ArgumentException("Damage event identity required.", nameof(eventId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!Enum.IsDefined(typeof(BuildingDamageType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (IsDestroyed || !processedDamageEvents.Add(eventId)) return 0;
            var applied = Math.Min(amount, CurrentIntegrity);
            CurrentIntegrity -= applied;
            StructuralStatus = StatusFor(CurrentIntegrity, MaxIntegrity);
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Damaged,
                eventId, type, applied));
            if (type == BuildingDamageType.Fire) AccumulatedFireDamage = checked(AccumulatedFireDamage + applied);
            if (IsDestroyed)
            {
                DestructionProcessed = true;
                FireStatus = BuildingFireStatus.BurnedOut;
                FireIntensity = 0;
                history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Destroyed));
            }
            return applied;
        }

        public void MarkStorageSpilled()
        {
            if (!IsDestroyed) throw new InvalidOperationException("Only destroyed building storage can be spilled.");
            StorageSpilled = true;
        }

        public void Ignite(int intensity, long calendarTick)
        {
            if (IsDestroyed) throw new InvalidOperationException("Destroyed buildings cannot ignite.");
            if (intensity <= 0 || intensity > 1000) throw new ArgumentOutOfRangeException(nameof(intensity));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            FireStatus = BuildingFireStatus.Burning;
            FireIntensity = intensity;
            FireStartTick = calendarTick;
            LastFireProcessedTick = calendarTick;
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Ignited));
        }

        public void AdvanceFireCheckpoint(long tick, int intensity)
        {
            if (tick < LastFireProcessedTick) throw new ArgumentOutOfRangeException(nameof(tick));
            if (intensity < 0 || intensity > 1000) throw new ArgumentOutOfRangeException(nameof(intensity));
            LastFireProcessedTick = tick;
            if (IsDestroyed || intensity == 0)
            {
                FireStatus = BuildingFireStatus.BurnedOut;
                FireIntensity = 0;
            }
            else
            {
                FireStatus = BuildingFireStatus.Burning;
                FireIntensity = intensity;
            }
        }

        public void Extinguish(long calendarTick)
        {
            if (FireStatus != BuildingFireStatus.Burning) throw new InvalidOperationException("Building is not burning.");
            if (calendarTick < LastFireProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            LastFireProcessedTick = calendarTick;
            FireStatus = BuildingFireStatus.Extinguished;
            FireIntensity = 0;
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Extinguished));
        }

        public void Capture(CombatSideId newOwnerSideId, long calendarTick)
        {
            if (!newOwnerSideId.IsValid) throw new ArgumentException("Owner side required.", nameof(newOwnerSideId));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (newOwnerSideId == OwnerSideId) throw new InvalidOperationException("Building already has this owner.");
            OwnerSideId = newOwnerSideId;
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Captured,
                ownerSideId: newOwnerSideId));
        }

        public BuildingState CaptureState() => new BuildingState(Id, DefinitionId, Coordinate, Orientation,
            ConstructionState, MaxIntegrity, CurrentIntegrity, StructuralStatus, OwnerSideId, FireStatus,
            FireIntensity, FireStartTick, LastFireProcessedTick, AccumulatedFireDamage, DestructionProcessed,
            StorageSpilled, processedDamageEvents.OrderBy(x => x), history,
            ConstructionWorkTicks, ConstructionRequiredTicks);

        private static BuildingStructuralStatus StatusFor(int integrity, int maximum) => integrity == 0
            ? BuildingStructuralStatus.Destroyed
            : integrity == maximum ? BuildingStructuralStatus.Intact
            : (long)integrity * 4L <= maximum ? BuildingStructuralStatus.Critical
            : BuildingStructuralStatus.Damaged;
    }
}
