using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Simulation.AI;
using Game.Simulation.Characters;
using Game.Simulation.Resources;

namespace Game.Simulation.Military
{
    public readonly struct MilitaryTrainingStartResult
    {
        public MilitaryTrainingStartResult(MilitaryTrainingStartFailure failure,
            MilitaryTrainingRecordState record = null)
        { Failure = failure; Record = record; }
        public MilitaryTrainingStartFailure Failure { get; }
        public MilitaryTrainingRecordState Record { get; }
        public bool Started => Failure == MilitaryTrainingStartFailure.None;
    }

    /// <summary>Canonical, ticked barracks training. It upgrades existing residents and never creates a Character.</summary>
    public sealed class MilitaryTrainingService : IExternalCharacterActivityOwner
    {
        public static readonly BuildingDefinitionId BarracksDefinitionId = new BuildingDefinitionId("military_barracks");
        public static readonly SkillId CombatSkillId = new SkillId("combat");

        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly BuildingRegistry buildings;
        private readonly ResourceInventoryRegistry inventories;
        private readonly MilitaryUnitService units;
        private readonly CharacterAppearanceService appearances;
        private readonly MilitaryTrainingCatalog catalog;
        private readonly long worldSeed;
        private readonly List<MilitaryTrainingRecordState> records = new List<MilitaryTrainingRecordState>();
        private ulong sequence;

        public MilitaryTrainingService(CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider, BuildingRegistry buildingRegistry,
            ResourceInventoryRegistry inventoryRegistry, MilitaryUnitService militaryUnits,
            CharacterAppearanceService appearanceService, long seed, MilitaryTrainingCatalog trainingCatalog = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            appearances = appearanceService ?? throw new ArgumentNullException(nameof(appearanceService));
            worldSeed = seed;
            catalog = trainingCatalog ?? MilitaryTrainingCatalog.CreateStoneAge();
        }

        public long LastProcessedTick { get; private set; }
        public IReadOnlyList<MilitaryTrainingRecordState> Records => records.OrderBy(x => x.Sequence).ToArray();
        public IReadOnlyList<MilitaryTrainingRecordState> Active => records.Where(x => !x.Completed)
            .OrderBy(x => x.CompletionTick).ThenBy(x => x.CharacterId).ToArray();

        public MilitaryRank? GetRank(StableEntityId characterId)
        {
            var completed = records.Where(x => x.CharacterId == characterId && x.Completed)
                .OrderByDescending(x => x.Rank).FirstOrDefault();
            return completed?.Rank;
        }

        public MilitaryTrainingLevelDefinition GetNextLevel(StableEntityId characterId) =>
            catalog.TryGetNext(GetRank(characterId), out var value) ? value : null;

        public bool IsExternallyControlled(StableEntityId characterId) =>
            records.Any(x => x.CharacterId == characterId && !x.Completed);

        public MilitaryTrainingStartResult TryStart(StableEntityId characterId, StableEntityId barracksId,
            CombatSideId sideId, long calendarTick, IEnumerable<InventoryOwner> fundingSources)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return Fail(MilitaryTrainingStartFailure.MissingCharacter);
            var capability = capabilities.GetCapabilities(characterId);
            if (!capability.IsAdult) return Fail(MilitaryTrainingStartFailure.NotAdult);
            if (capability.IsIncapacitated) return Fail(MilitaryTrainingStartFailure.Incapacitated);
            if (!buildings.TryGet(barracksId, out var barracks) || barracks.DefinitionId != BarracksDefinitionId)
                return Fail(MilitaryTrainingStartFailure.MissingBarracks);
            if (!barracks.IsOperational) return Fail(MilitaryTrainingStartFailure.BarracksInactive);
            if (!sideId.IsValid || barracks.OwnerSideId != sideId)
                return Fail(MilitaryTrainingStartFailure.WrongOwner);
            if (IsExternallyControlled(characterId)) return Fail(MilitaryTrainingStartFailure.AlreadyTraining);
            if (!catalog.TryGetNext(GetRank(characterId), out var definition))
                return Fail(MilitaryTrainingStartFailure.MaximumRank);
            if (!TryPay(definition.Cost, fundingSources))
                return Fail(MilitaryTrainingStartFailure.InsufficientResources);
            var record = new MilitaryTrainingRecordState(characterId, barracksId, sideId, definition.Rank,
                calendarTick, checked(calendarTick + definition.DurationTicks), false, ++sequence);
            records.Add(record);
            if (calendarTick > LastProcessedTick) LastProcessedTick = calendarTick;
            return new MilitaryTrainingStartResult(MilitaryTrainingStartFailure.None, record);
        }

        public int AdvanceTo(long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var completed = 0;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Completed || record.CompletionTick > calendarTick) continue;
                Complete(record);
                records[i] = new MilitaryTrainingRecordState(record.CharacterId, record.BarracksId,
                    record.SideId, record.Rank, record.StartTick, record.CompletionTick, true, record.Sequence);
                completed++;
            }
            LastProcessedTick = calendarTick;
            return completed;
        }

        public MilitaryTrainingSaveState CaptureState() =>
            new MilitaryTrainingSaveState(records, sequence, LastProcessedTick);

        public void RestoreState(MilitaryTrainingSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var record in state.Records)
            {
                if (!characters.TryGet(record.CharacterId, out _) || !buildings.TryGet(record.BarracksId, out var barracks) ||
                    barracks.DefinitionId != BarracksDefinitionId)
                    throw new ArgumentException("Training save references unavailable canonical state.", nameof(state));
            }
            records.Clear(); records.AddRange(state.Records); sequence = state.Sequence;
            LastProcessedTick = state.LastProcessedTick;
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var record in Records)
            {
                Add(ref hash, record.CharacterId.ToString()); Add(ref hash, record.BarracksId.ToString());
                Add(ref hash, record.SideId.Value); Add(ref hash, ((int)record.Rank).ToString());
                Add(ref hash, record.StartTick.ToString()); Add(ref hash, record.CompletionTick.ToString());
                Add(ref hash, record.Completed ? "1" : "0"); Add(ref hash, record.Sequence.ToString());
            }
            Add(ref hash, LastProcessedTick.ToString()); return hash;
        }

        private void Complete(MilitaryTrainingRecordState record)
        {
            var character = characters.Get(record.CharacterId);
            var level = catalog.Get(record.Rank);
            character.SetSkill(new SkillState(CombatSkillId, new SkillLevel(level.CombatSkillLevel)));
            appearances.SetClothing(record.CharacterId, StoneAgeAppearanceProfiles.MilitaryClothing);
            if (units.TryGetForMember(record.CharacterId, out _)) return;
            var side = new MilitarySideId(record.SideId.Value);
            var target = units.Units.FirstOrDefault(x => x.OwnerSideId == side &&
                x.Status != MilitaryUnitStatus.Disbanded && !x.ActiveEncounterId.HasValue);
            if (target != null) units.AddMember(target.Id, record.CharacterId);
            else units.Create(new MilitaryUnitId(DeterministicStableId.Create(worldSeed,
                    "trained_unit:" + record.CharacterId)), side,
                new[] { record.CharacterId }, record.CharacterId);
        }

        private bool TryPay(IReadOnlyList<ResourceAmount> cost, IEnumerable<InventoryOwner> sources)
        {
            var inventoriesInOrder = (sources ?? throw new ArgumentNullException(nameof(sources))).Distinct()
                .OrderBy(x => x.Kind).ThenBy(x => x.Id).Select(x => inventories.TryGet(x, out var found) ? found : null)
                .Where(x => x != null).ToArray();
            foreach (var amount in cost)
                if (inventoriesInOrder.Sum(x => x.GetAmount(amount.ResourceId).Units) < amount.Quantity.Units) return false;
            var charged = new List<Tuple<ResourceInventory, ResourceId, long>>();
            try
            {
                foreach (var amount in cost)
                {
                    var remaining = amount.Quantity.Units;
                    foreach (var inventory in inventoriesInOrder)
                    {
                        var take = Math.Min(remaining, inventory.GetAmount(amount.ResourceId).Units);
                        if (take == 0) continue;
                        if (!inventory.TryRemove(amount.ResourceId, new ResourceQuantity(take)))
                            throw new InvalidOperationException("Training funding changed during commit.");
                        charged.Add(Tuple.Create(inventory, amount.ResourceId, take)); remaining -= take;
                        if (remaining == 0) break;
                    }
                }
                return true;
            }
            catch
            {
                for (var i = charged.Count - 1; i >= 0; i--)
                    charged[i].Item1.TryAdd(charged[i].Item2, new ResourceQuantity(charged[i].Item3));
                throw;
            }
        }

        private static MilitaryTrainingStartResult Fail(MilitaryTrainingStartFailure failure) =>
            new MilitaryTrainingStartResult(failure);
        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }
}
