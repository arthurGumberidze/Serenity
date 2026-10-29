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
        private readonly MilitaryUnitRepresentationMode newUnitRepresentation;
        private readonly string newUnitLocation;
        private readonly List<MilitaryTrainingRecordState> records = new List<MilitaryTrainingRecordState>();
        private ulong sequence;
        public Func<MilitaryTrainingRecordState, bool> IsAtBarracks { get; set; }
        public Func<StableEntityId, bool> IsRuler { get; set; }

        public MilitaryTrainingService(CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider, BuildingRegistry buildingRegistry,
            ResourceInventoryRegistry inventoryRegistry, MilitaryUnitService militaryUnits,
            CharacterAppearanceService appearanceService, long seed, MilitaryTrainingCatalog trainingCatalog = null,
            MilitaryUnitRepresentationMode newUnitRepresentation = MilitaryUnitRepresentationMode.Detailed,
            string newUnitLocation = "local")
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            appearances = appearanceService ?? throw new ArgumentNullException(nameof(appearanceService));
            worldSeed = seed;
            catalog = trainingCatalog ?? MilitaryTrainingCatalog.CreateStoneAge();
            this.newUnitRepresentation = newUnitRepresentation;
            this.newUnitLocation = newUnitLocation;
        }

        public long LastProcessedTick { get; private set; }
        public IReadOnlyList<MilitaryTrainingRecordState> Records => records.OrderBy(x => x.Sequence).ToArray();
        public IReadOnlyList<MilitaryTrainingRecordState> Active => records.Where(x => !x.Completed)
            .OrderBy(x => x.CompletionTick).ThenBy(x => x.CharacterId).ToArray();
        public MilitaryTrainingCatalog Catalog => catalog;

        public MilitaryRank? GetRank(StableEntityId characterId)
        {
            if (IsRuler?.Invoke(characterId) == true) return MilitaryRank.Generalissimo;
            var completed = records.Where(x => x.CharacterId == characterId && x.Completed)
                .OrderByDescending(x => x.Rank).FirstOrDefault();
            return completed?.Rank;
        }

        public MilitaryTrainingLevelDefinition GetNextLevel(StableEntityId characterId) =>
            catalog.TryGetNext(GetRank(characterId), out var value) ? value : null;

        public int GetMaximumHealth(StableEntityId characterId)
        {
            var rank = GetRank(characterId);
            var own = rank.HasValue ? catalog.Get(rank.Value).HealthBonus : 0;
            var command = CommanderLevel(characterId);
            return 100 + own + (command?.HealthBonus ?? 0) / 2 + CommanderCombatSkill(characterId) / 10;
        }

        public int GetDamagePerThousand(StableEntityId characterId)
        {
            var rank = GetRank(characterId);
            var own = rank.HasValue ? catalog.Get(rank.Value).DamagePerThousand : 1000;
            var command = CommanderLevel(characterId);
            return Math.Min(2000, own + ((command?.DamagePerThousand ?? 1000) - 1000) / 2 +
                CommanderCombatSkill(characterId) * 2);
        }

        private MilitaryTrainingLevelDefinition CommanderLevel(StableEntityId characterId)
        {
            if (!units.TryGetForMember(characterId, out var unit) || !unit.CaptainId.HasValue ||
                !characters.TryGet(unit.CaptainId.Value, out var captain) ||
                captain.LifeState != CharacterLifeState.Alive || !capabilities.GetCapabilities(captain.Id).CanFight)
                return null;
            var rank = GetRank(captain.Id);
            return rank.HasValue && rank.Value >= MilitaryRank.Lieutenant ? catalog.Get(rank.Value) : null;
        }

        private int CommanderCombatSkill(StableEntityId characterId)
        {
            if (CommanderLevel(characterId) == null || !units.TryGetForMember(characterId, out var unit) ||
                !unit.CaptainId.HasValue || !characters.TryGet(unit.CaptainId.Value, out var captain)) return 0;
            return captain.TryGetSkill(CombatSkillId, out var skill) ? skill.Level.Value : 0;
        }

        public bool IsExternallyControlled(StableEntityId characterId) =>
            records.Any(x => x.CharacterId == characterId && !x.Completed);

        public MilitaryTrainingStartResult TryStart(StableEntityId characterId, StableEntityId barracksId,
            CombatSideId sideId, long calendarTick, IEnumerable<InventoryOwner> fundingSources)
        {
            var sources = (fundingSources ?? throw new ArgumentNullException(nameof(fundingSources))).ToArray();
            var failure = CheckStart(characterId, barracksId, sideId, calendarTick, sources);
            if (failure != MilitaryTrainingStartFailure.None) return Fail(failure);
            catalog.TryGetNext(GetRank(characterId), out var definition);
            if (!TryPay(definition.Cost, sources))
                return Fail(MilitaryTrainingStartFailure.InsufficientResources);
            if (!GetRank(characterId).HasValue)
                characters.Get(characterId).AssignProfession(new ProfessionId("military"));
            var record = new MilitaryTrainingRecordState(characterId, barracksId, sideId, definition.Rank,
                calendarTick, checked(calendarTick + definition.DurationTicks), false, ++sequence);
            records.Add(record);
            if (calendarTick > LastProcessedTick) LastProcessedTick = calendarTick;
            return new MilitaryTrainingStartResult(MilitaryTrainingStartFailure.None, record);
        }

        public MilitaryTrainingStartFailure CheckStart(StableEntityId characterId, StableEntityId barracksId,
            CombatSideId sideId, long calendarTick, IEnumerable<InventoryOwner> fundingSources)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                return MilitaryTrainingStartFailure.MissingCharacter;
            var capability = capabilities.GetCapabilities(characterId);
            if (!capability.IsAdult) return MilitaryTrainingStartFailure.NotAdult;
            if (capability.IsIncapacitated) return MilitaryTrainingStartFailure.Incapacitated;
            if (!buildings.TryGet(barracksId, out var barracks) || barracks.DefinitionId != BarracksDefinitionId)
                return MilitaryTrainingStartFailure.MissingBarracks;
            if (!barracks.IsOperational) return MilitaryTrainingStartFailure.BarracksInactive;
            if (!sideId.IsValid || barracks.OwnerSideId != sideId)
                return MilitaryTrainingStartFailure.WrongOwner;
            if (IsExternallyControlled(characterId)) return MilitaryTrainingStartFailure.AlreadyTraining;
            if (!catalog.TryGetNext(GetRank(characterId), out var definition))
                return MilitaryTrainingStartFailure.MaximumRank;
            if (records.Count(x => !x.Completed && x.BarracksId == barracksId) >= 6)
                return MilitaryTrainingStartFailure.BarracksFull;
            if (definition.Rank == MilitaryRank.Generalissimo)
                return MilitaryTrainingStartFailure.ReservedForRuler;
            if (definition.Rank == MilitaryRank.ColonelGeneral &&
                records.Count(x => x.SideId == sideId && x.Rank == MilitaryRank.ColonelGeneral) >= 2)
                return MilitaryTrainingStartFailure.RankLimitReached;
            var currentRank = GetRank(characterId);
            if (currentRank.HasValue)
            {
                var lastPromotion = records.Where(x => x.CharacterId == characterId && x.Completed)
                    .OrderByDescending(x => x.CompletionTick).ThenByDescending(x => x.Sequence).First();
                if (calendarTick - lastPromotion.CompletionTick < definition.MinimumServiceTicks)
                    return MilitaryTrainingStartFailure.InsufficientService;
                var requiredCombatSkill = Math.Min(SkillLevel.Maximum,
                    catalog.Get(currentRank.Value).CombatSkillLevel + definition.RequiredBattleExperience);
                if (CharacterSkillRules.Level(character, CombatSkillId) < requiredCombatSkill)
                    return MilitaryTrainingStartFailure.InsufficientBattleExperience;
            }
            return CanPay(definition.Cost, fundingSources)
                ? MilitaryTrainingStartFailure.None
                : MilitaryTrainingStartFailure.InsufficientResources;
        }

        public int AdvanceTo(long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var completed = 0;
            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Completed) continue;
                if (!characters.TryGet(record.CharacterId, out var trainee) ||
                    trainee.LifeState != CharacterLifeState.Alive)
                {
                    // Death is the only terminal interruption: the paid course is lost and grants no rank.
                    records.RemoveAt(i--);
                    continue;
                }
                if (IsAtBarracks != null && !IsAtBarracks(record))
                {
                    var delay = Math.Max(0, calendarTick - Math.Max(LastProcessedTick, record.StartTick));
                    records[i] = new MilitaryTrainingRecordState(record.CharacterId, record.BarracksId,
                        record.SideId, record.Rank, checked(record.StartTick + delay),
                        checked(record.CompletionTick + delay), false, record.Sequence);
                    continue;
                }
                if (record.CompletionTick > calendarTick) continue;
                Complete(record);
                records[i] = new MilitaryTrainingRecordState(record.CharacterId, record.BarracksId,
                    record.SideId, record.Rank, record.StartTick, record.CompletionTick, true, record.Sequence);
                completed++;
            }
            LastProcessedTick = calendarTick;
            return completed;
        }

        /// <summary>
        /// Registers a diagnostic graduate through the same rank, skill, clothing and roster authorities as
        /// ordinary course completion. It never advances or substitutes the normal training workflow.
        /// </summary>
        public MilitaryTrainingRecordState RegisterDiagnosticGraduate(StableEntityId characterId,
            StableEntityId barracksId, CombatSideId sideId, MilitaryRank rank, long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!characters.TryGet(characterId, out var character) || character.LifeState != CharacterLifeState.Alive)
                throw new InvalidOperationException("Diagnostic graduate must be a living canonical Character.");
            var capability = capabilities.GetCapabilities(characterId);
            if (!capability.IsAdult || capability.IsIncapacitated)
                throw new InvalidOperationException("Diagnostic graduate must be an available adult.");
            if (!buildings.TryGet(barracksId, out var barracks) || barracks.DefinitionId != BarracksDefinitionId ||
                !barracks.IsOperational || barracks.OwnerSideId != sideId)
                throw new InvalidOperationException("Diagnostic graduate requires an operational same-side barracks.");
            if (records.Any(x => x.CharacterId == characterId) || units.TryGetForMember(characterId, out _))
                throw new InvalidOperationException("Diagnostic graduate is already trained or rostered.");
            catalog.Get(rank);
            var record = new MilitaryTrainingRecordState(characterId, barracksId, sideId, rank,
                calendarTick, calendarTick, true, ++sequence);
            records.Add(record);
            Complete(record);
            if (calendarTick > LastProcessedTick) LastProcessedTick = calendarTick;
            return record;
        }

        public MilitaryTrainingSaveState CaptureState() =>
            new MilitaryTrainingSaveState(records, sequence, LastProcessedTick);

        public void RestoreState(MilitaryTrainingSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var record in state.Records)
            {
                if (!characters.TryGet(record.CharacterId, out _) || !buildings.TryGet(record.BarracksId, out _))
                    throw new ArgumentException("Training save references unavailable canonical state.", nameof(state));
            }
            records.Clear(); records.AddRange(state.Records); sequence = state.Sequence;
            LastProcessedTick = state.LastProcessedTick;
        }

        /// <summary>Removes the former side's rank authority before a captured soldier joins another side.</summary>
        public int RemoveMilitaryHistory(IEnumerable<StableEntityId> characterIds)
        {
            var ids = new HashSet<StableEntityId>(characterIds ?? Array.Empty<StableEntityId>());
            return records.RemoveAll(x => ids.Contains(x.CharacterId));
        }

        /// <summary>
        /// Registers surviving captured soldiers as privates of the winning side. Combat skill is retained:
        /// captivity removes command rank, not learned battle experience.
        /// </summary>
        public int RegisterCapturedPrivates(IEnumerable<StableEntityId> characterIds, StableEntityId barracksId,
            CombatSideId newSide, long calendarTick)
        {
            if (!barracksId.IsValid || !newSide.IsValid) throw new ArgumentException("Capture authority is required.");
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var added = 0;
            foreach (var id in (characterIds ?? Array.Empty<StableEntityId>()).Distinct().OrderBy(x => x))
            {
                if (!characters.TryGet(id, out var character) || character.LifeState != CharacterLifeState.Alive) continue;
                records.RemoveAll(x => x.CharacterId == id);
                records.Add(new MilitaryTrainingRecordState(id, barracksId, newSide, MilitaryRank.Private,
                    calendarTick, calendarTick, true, ++sequence));
                character.AssignProfession(new ProfessionId("military"));
                appearances.SetClothing(id, StoneAgeAppearanceProfiles.MilitaryClothing);
                added++;
            }
            LastProcessedTick = Math.Max(LastProcessedTick, calendarTick);
            return added;
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
            character.AssignProfession(new ProfessionId("military"));
            character.SetSkill(new SkillState(CombatSkillId, new SkillLevel(Math.Max(
                CharacterSkillRules.Level(character, CombatSkillId), level.CombatSkillLevel))));
            appearances.SetClothing(record.CharacterId, StoneAgeAppearanceProfiles.MilitaryClothing);
            var side = new MilitarySideId(record.SideId.Value);
            if (units.TryGetForMember(record.CharacterId, out _))
            {
                ReconcileArmies(side, record);
                return;
            }
            var target = units.Units.FirstOrDefault(x => x.OwnerSideId == side &&
                x.Status != MilitaryUnitStatus.Disbanded && !x.ActiveEncounterId.HasValue &&
                x.ActiveMemberIds.Count < MilitaryUnitService.MaximumSquadMembers);
            if (target != null) units.AddMember(target.Id, record.CharacterId);
            else if (record.Rank >= MilitaryRank.Lieutenant)
            {
                var unit = units.Create(new MilitaryUnitId(DeterministicStableId.Create(worldSeed,
                        "trained_unit:" + record.CharacterId)), side,
                    new[] { record.CharacterId }, record.CharacterId, newUnitRepresentation, newUnitLocation);
                foreach (var graduate in records.Where(x => x.Completed && x.SideId == record.SideId &&
                             x.CharacterId != record.CharacterId && x.Rank < MilitaryRank.Lieutenant)
                             .OrderBy(x => x.Sequence).Take(MilitaryUnitService.MaximumSquadMembers - 1))
                    if (!units.TryGetForMember(graduate.CharacterId, out _))
                        units.AddMember(unit.Id, graduate.CharacterId);
            }
            ReconcileArmies(side, record);
        }

        private void ReconcileArmies(MilitarySideId side, MilitaryTrainingRecordState completing)
        {
            var colonelGenerals = records.Where(x => (x.Completed || x.Sequence == completing.Sequence) &&
                    x.SideId.Value == side.Value &&
                    x.Rank == MilitaryRank.ColonelGeneral)
                .OrderBy(x => x.Sequence).Select(x => x.CharacterId).Take(2).ToArray();
            foreach (var commander in colonelGenerals)
                if (units.TryGetForMember(commander, out var squad) && !squad.ActiveEncounterId.HasValue)
                    units.RemoveMember(squad.Id, commander);
            var availableSquads = units.Units.Where(x => x.OwnerSideId == side &&
                    x.Status != MilitaryUnitStatus.Disbanded &&
                    !units.Armies.SelectMany(a => a.SquadIds).Contains(x.Id))
                .OrderBy(x => x.Id).ToArray();
            var armies = units.Armies.Where(x => x.OwnerSideId == side).OrderBy(x => x.Id).ToArray();
            if (armies.Length > 0 && colonelGenerals.Length > 0 &&
                GetRank(armies[0].CommanderId) != MilitaryRank.ColonelGeneral)
                units.AssignArmyCommander(armies[0].Id, colonelGenerals[0]);
            for (var index = 0; index < availableSquads.Length; index++)
            {
                armies = units.Armies.Where(x => x.OwnerSideId == side).OrderBy(x => x.Id).ToArray();
                var open = armies.FirstOrDefault(x => x.SquadIds.Count < 10);
                if (open != null) { units.AddSquadToArmy(open.Id, availableSquads[index].Id); continue; }
                var armyIndex = armies.Length;
                StableEntityId commander;
                if (armyIndex < colonelGenerals.Length) commander = colonelGenerals[armyIndex];
                else if (armyIndex == 0)
                {
                    commander = characters.GetAll().Where(x => IsRuler?.Invoke(x.Id) == true)
                        .Select(x => x.Id).FirstOrDefault();
                    if (!commander.IsValid) break;
                }
                else break;
                units.CreateArmy(side, commander, new[] { availableSquads[index].Id });
            }
        }

        private bool TryPay(IReadOnlyList<ResourceAmount> cost, IEnumerable<InventoryOwner> sources)
        {
            var inventoriesInOrder = (sources ?? throw new ArgumentNullException(nameof(sources))).Distinct()
                .OrderBy(x => x.Kind).ThenBy(x => x.Id).Select(x => inventories.TryGet(x, out var found) ? found : null)
                .Where(x => x != null && !x.IssuanceLocked).ToArray();
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

        private bool CanPay(IReadOnlyList<ResourceAmount> cost, IEnumerable<InventoryOwner> sources)
        {
            var available = (sources ?? throw new ArgumentNullException(nameof(sources))).Distinct()
                .Select(x => inventories.TryGet(x, out var found) ? found : null)
                .Where(x => x != null && !x.IssuanceLocked).ToArray();
            return cost.All(amount => available.Sum(x => x.GetAmount(amount.ResourceId).Units) >= amount.Quantity.Units);
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
