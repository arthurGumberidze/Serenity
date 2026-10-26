using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Resources;
using Game.Domain.World;

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
            long constructionWorkTicks = 0, long constructionRequiredTicks = 0,
            SettlementId? settlementId = null, ConstructionPhase? constructionPhase = null,
            int constructionStageIndex = 0, int constructionStageCount = 0,
            IEnumerable<ConstructionMaterialProgress> constructionMaterials = null,
            IEnumerable<GridCoordinate> placementCells = null)
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
            if (settlementId.HasValue && !settlementId.Value.IsValid)
                throw new ArgumentException("Settlement identity must be valid.", nameof(settlementId));
            var phase = constructionPhase ?? (constructionState == ConstructionState.Completed
                ? ConstructionPhase.Completed : ConstructionPhase.LegacyPlanned);
            if (!Enum.IsDefined(typeof(ConstructionPhase), phase)) throw new ArgumentOutOfRangeException(nameof(constructionPhase));
            if (constructionStageCount < 0 || constructionStageIndex < 0 || constructionStageIndex > constructionStageCount)
                throw new ArgumentOutOfRangeException(nameof(constructionStageIndex));
            if (constructionPhase.HasValue && phase != ConstructionPhase.LegacyPlanned &&
                phase != ConstructionPhase.Completed && constructionStageCount <= 0)
                throw new ArgumentException("A staged construction phase requires at least one stage.", nameof(constructionStageCount));
            var materials = (constructionMaterials ?? Array.Empty<ConstructionMaterialProgress>())
                .OrderBy(x => x.ResourceId).ToArray();
            if (materials.Select(x => x.ResourceId).Distinct().Count() != materials.Length)
                throw new ArgumentException("Construction materials must have unique resource IDs.", nameof(constructionMaterials));
            if (phase == ConstructionPhase.Completed && (constructionState != ConstructionState.Completed ||
                constructionStageIndex != constructionStageCount || materials.Any(x => x.RemainingUnits != 0)))
                throw new ArgumentException("Completed construction must have every stage and material installed.", nameof(constructionPhase));
            if (constructionState == ConstructionState.Completed && phase != ConstructionPhase.Completed)
                throw new ArgumentException("Legacy and detailed construction state disagree.", nameof(constructionPhase));
            SettlementId = settlementId;
            ConstructionPhase = phase;
            ConstructionStageIndex = constructionStageIndex;
            ConstructionStageCount = constructionStageCount;
            ConstructionMaterials = Array.AsReadOnly(materials);
            var placed = (placementCells ?? Array.Empty<GridCoordinate>()).ToArray();
            if (placed.Distinct().Count() != placed.Length)
                throw new ArgumentException("Placement cells must be unique.", nameof(placementCells));
            PlacementCells = Array.AsReadOnly(placed);
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
        public SettlementId? SettlementId { get; }
        public ConstructionPhase ConstructionPhase { get; }
        public int ConstructionStageIndex { get; }
        public int ConstructionStageCount { get; }
        public IReadOnlyList<ConstructionMaterialProgress> ConstructionMaterials { get; }
        public IReadOnlyList<GridCoordinate> PlacementCells { get; }
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
            SettlementId = state.SettlementId;
            ConstructionPhase = state.ConstructionPhase;
            ConstructionStageIndex = state.ConstructionStageIndex;
            ConstructionStageCount = state.ConstructionStageCount;
            constructionMaterials = state.ConstructionMaterials.ToList();
            placementCells = state.PlacementCells.ToList();
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
        private readonly List<ConstructionMaterialProgress> constructionMaterials;
        private readonly List<GridCoordinate> placementCells;
        private ulong nextEventSequence;

        public StableEntityId Id { get; }
        public BuildingDefinitionId DefinitionId { get; private set; }
        public GridCoordinate Coordinate { get; }
        public BuildingOrientation Orientation { get; }
        public ConstructionState ConstructionState { get; private set; }
        public long ConstructionWorkTicks { get; private set; }
        public long ConstructionRequiredTicks { get; private set; }
        public SettlementId? SettlementId { get; private set; }
        public ConstructionPhase ConstructionPhase { get; private set; }
        public int ConstructionStageIndex { get; private set; }
        public int ConstructionStageCount { get; private set; }
        public IReadOnlyList<ConstructionMaterialProgress> ConstructionMaterials => constructionMaterials.AsReadOnly();
        public IReadOnlyList<GridCoordinate> PlacementCells => placementCells.AsReadOnly();
        public void ReplacePalisadeCells(IEnumerable<GridCoordinate> cells)
        {
            if (DefinitionId.ToString() != "palisade") throw new InvalidOperationException("Only modular palisades can be trimmed.");
            var values=cells.Distinct().ToArray(); placementCells.Clear(); placementCells.AddRange(values);
        }

        public InventoryOwner ConstructionBufferOwner => new InventoryOwner(InventoryOwnerKind.ConstructionBuffer, Id);
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
            if (ConstructionPhase != ConstructionPhase.LegacyPlanned && ConstructionPhase != ConstructionPhase.Building)
                return;
            ConstructionWorkTicks += Math.Min(work.Ticks, ConstructionRequiredTicks - ConstructionWorkTicks);
            if (ConstructionWorkTicks == ConstructionRequiredTicks) MarkCompleted();
        }

        public void InitializeConstruction(BuildingDefinition definition, SettlementId settlementId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Id != DefinitionId) throw new ArgumentException("Definition does not match building.", nameof(definition));
            if (!settlementId.IsValid) throw new ArgumentException("Settlement identity is required.", nameof(settlementId));
            if (ConstructionState == ConstructionState.Completed || ConstructionPhase != ConstructionPhase.LegacyPlanned)
                throw new InvalidOperationException("Construction is already initialized or complete.");
            AssignSettlement(settlementId);
            ConstructionRequiredTicks = definition.Construction.WorkRequiredTicks;
            ConstructionStageCount = definition.Construction.StageCount;
            ConstructionStageIndex = 0;
            constructionMaterials.Clear();
            constructionMaterials.AddRange(definition.Construction.Cost.Select(amount =>
                new ConstructionMaterialProgress(amount.ResourceId, amount.Quantity.Units, 0)));
            if (constructionMaterials.Count == 0)
            {
                ConstructionStageIndex = ConstructionStageCount;
                ConstructionPhase = ConstructionPhase.Building;
            }
            else ConstructionPhase = ConstructionPhase.AwaitingMaterials;
        }

        public IReadOnlyList<ResourceAmount> NextStageMaterialCost()
        {
            if (ConstructionStageIndex >= ConstructionStageCount || ConstructionStageCount <= 0)
                return Array.Empty<ResourceAmount>();
            var nextStage = ConstructionStageIndex + 1;
            return constructionMaterials.Select(item =>
            {
                var installedTarget = item.RequiredUnits / ConstructionStageCount * nextStage +
                    item.RequiredUnits % ConstructionStageCount * nextStage / ConstructionStageCount;
                var delta = installedTarget - item.InstalledUnits;
                return delta > 0 ? new ResourceAmount(item.ResourceId, new ResourceQuantity(delta)) : (ResourceAmount?)null;
            }).Where(x => x.HasValue).Select(x => x.Value).ToArray();
        }

        public void SetNextStageReady(bool ready)
        {
            if (ConstructionPhase == ConstructionPhase.Completed || ConstructionPhase == ConstructionPhase.Cancelled ||
                ConstructionPhase == ConstructionPhase.LegacyPlanned) return;
            ConstructionPhase = ready ? ConstructionPhase.ReadyForWork : ConstructionPhase.AwaitingMaterials;
        }

        public void InstallNextStageMaterials(IReadOnlyList<ResourceAmount> installed)
        {
            if (ConstructionPhase != ConstructionPhase.ReadyForWork)
                throw new InvalidOperationException("The next construction stage is not ready.");
            var expected = NextStageMaterialCost();
            if (installed == null || installed.Count != expected.Count ||
                expected.Where((value, index) => value.ResourceId != installed[index].ResourceId ||
                    value.Quantity != installed[index].Quantity).Any())
                throw new ArgumentException("Installed materials do not match the next stage.", nameof(installed));
            foreach (var amount in installed)
            {
                var index = constructionMaterials.FindIndex(x => x.ResourceId == amount.ResourceId);
                var old = constructionMaterials[index];
                constructionMaterials[index] = new ConstructionMaterialProgress(old.ResourceId, old.RequiredUnits,
                    checked(old.InstalledUnits + amount.Quantity.Units));
            }
            ConstructionStageIndex++;
            ConstructionPhase = ConstructionStageIndex == ConstructionStageCount
                ? ConstructionPhase.Building : ConstructionPhase.AwaitingMaterials;
        }
        public int MaxIntegrity { get; private set; }
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

        public static Building CreateNew(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, CombatSideId ownerSideId, IEnumerable<GridCoordinate> placementCells,
            SettlementId? settlementId = null, StableEntityId? id = null) => definition == null
            ? throw new ArgumentNullException(nameof(definition))
            : id.HasValue && !id.Value.IsValid
                ? throw new ArgumentException("A stable building identity is required.", nameof(id))
                : Restore(new BuildingState(id ?? StableEntityId.NewId(), definition.Id, coordinate, orientation,
                    ConstructionState.Planned, definition.MaxIntegrity, definition.MaxIntegrity,
                    BuildingStructuralStatus.Intact, ownerSideId, BuildingFireStatus.None, 0, 0, 0, 0,
                    false, false, null, null, settlementId: settlementId, placementCells: placementCells));

        public static Building Restore(BuildingState state) => new Building(state);

        public void AssignSettlement(SettlementId settlementId)
        {
            if (!settlementId.IsValid) throw new ArgumentException("Settlement identity is required.", nameof(settlementId));
            if (SettlementId.HasValue && SettlementId.Value != settlementId)
                throw new InvalidOperationException("Building is already assigned to another settlement.");
            SettlementId = settlementId;
        }

        public void MarkCompleted()
        {
            if (ConstructionState == ConstructionState.Completed) return;
            if (ConstructionPhase != ConstructionPhase.LegacyPlanned &&
                (ConstructionStageIndex != ConstructionStageCount || constructionMaterials.Any(x => x.RemainingUnits != 0)))
                throw new InvalidOperationException("Construction cannot complete before every material stage is installed.");
            ConstructionState = ConstructionState.Completed;
            ConstructionPhase = ConstructionPhase.Completed;
        }

        public void CancelConstruction()
        {
            if (IsOperational) throw new InvalidOperationException("Completed buildings cannot be cancelled as construction.");
            ConstructionPhase = ConstructionPhase.Cancelled;
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

        public int Repair(StableEntityId eventId, int amount, long calendarTick)
        {
            if (!eventId.IsValid) throw new ArgumentException("Repair event identity required.", nameof(eventId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (IsDestroyed || !IsOperational || CurrentIntegrity >= MaxIntegrity || !processedDamageEvents.Add(eventId))
                return 0;
            var applied = Math.Min(amount, MaxIntegrity - CurrentIntegrity);
            CurrentIntegrity += applied;
            StructuralStatus = StatusFor(CurrentIntegrity, MaxIntegrity);
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Repaired,
                eventId, amount: applied));
            return applied;
        }

        public bool UpgradeTo(StableEntityId eventId, BuildingDefinition target, long calendarTick)
        {
            if (!eventId.IsValid) throw new ArgumentException("Upgrade event identity required.", nameof(eventId));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (IsDestroyed || !IsOperational || target.Id == DefinitionId || !processedDamageEvents.Add(eventId)) return false;
            var oldMaximum = MaxIntegrity;
            CurrentIntegrity = Math.Max(1, (int)Math.Min(target.MaxIntegrity,
                ((long)CurrentIntegrity * target.MaxIntegrity + oldMaximum - 1L) / oldMaximum));
            DefinitionId = target.Id;
            MaxIntegrity = target.MaxIntegrity;
            StructuralStatus = StatusFor(CurrentIntegrity, MaxIntegrity);
            history.Add(new BuildingEventState(nextEventSequence++, calendarTick, BuildingEventKind.Upgraded,
                eventId));
            return true;
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
            ConstructionWorkTicks, ConstructionRequiredTicks, SettlementId,
            ConstructionPhase == ConstructionPhase.LegacyPlanned || ConstructionStageCount > 0
                ? (ConstructionPhase?)ConstructionPhase : null,
            ConstructionStageIndex, ConstructionStageCount, constructionMaterials, placementCells);

        private static BuildingStructuralStatus StatusFor(int integrity, int maximum) => integrity == 0
            ? BuildingStructuralStatus.Destroyed
            : integrity == maximum ? BuildingStructuralStatus.Intact
            : (long)integrity * 4L <= maximum ? BuildingStructuralStatus.Critical
            : BuildingStructuralStatus.Damaged;
    }
}
