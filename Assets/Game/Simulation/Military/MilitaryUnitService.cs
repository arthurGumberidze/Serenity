using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Work;
using Game.Domain.Resources;
using Game.Simulation.Combat;
using Game.Simulation.Tiers;
using Game.Simulation.Work;

namespace Game.Simulation.Military
{
    public sealed class MilitarySupplySettings
    {
        public MilitarySupplySettings(ResourceId foodResourceId, long initialFoodPerMember,
            long travelFoodPerMember, long combatFoodPerMember, int shortageMovementDurationPerThousand,
            int shortageCombatPerThousand, int shortageMoraleLoss)
        {
            if (!foodResourceId.IsValid) throw new ArgumentException("Food resource is required.", nameof(foodResourceId));
            if (initialFoodPerMember < 0 || travelFoodPerMember < 0 || combatFoodPerMember < 0)
                throw new ArgumentOutOfRangeException();
            if (shortageMovementDurationPerThousand < 1000 || shortageMovementDurationPerThousand > 10000 ||
                shortageCombatPerThousand < 1 || shortageCombatPerThousand > 1000 ||
                shortageMoraleLoss < 0 || shortageMoraleLoss > 1000)
                throw new ArgumentOutOfRangeException();
            FoodResourceId = foodResourceId; InitialFoodPerMember = initialFoodPerMember;
            TravelFoodPerMember = travelFoodPerMember; CombatFoodPerMember = combatFoodPerMember;
            ShortageMovementDurationPerThousand = shortageMovementDurationPerThousand;
            ShortageCombatPerThousand = shortageCombatPerThousand; ShortageMoraleLoss = shortageMoraleLoss;
        }
        public ResourceId FoodResourceId { get; }
        public long InitialFoodPerMember { get; }
        public long TravelFoodPerMember { get; }
        public long CombatFoodPerMember { get; }
        public int ShortageMovementDurationPerThousand { get; }
        public int ShortageCombatPerThousand { get; }
        public int ShortageMoraleLoss { get; }
        public static MilitarySupplySettings StoneAge { get; } = new MilitarySupplySettings(
            new ResourceId("berries"), 6, 1, 1, 1500, 700, 75);
    }

    public readonly struct MilitarySupplyConsumptionResult
    {
        public MilitarySupplyConsumptionResult(bool processed, long required, long consumed, int modifierPerThousand)
        { Processed = processed; Required = required; Consumed = consumed; ModifierPerThousand = modifierPerThousand; }
        public bool Processed { get; }
        public long Required { get; }
        public long Consumed { get; }
        public int ModifierPerThousand { get; }
        public bool FullySupplied => Consumed >= Required;
    }

    public enum CommanderAssignmentStatus
    {
        Assigned = 1,
        UnitNotFound = 2,
        NotMember = 3,
        Ineligible = 4
    }

    public readonly struct MilitaryUnitRosterSummary
    {
        public MilitaryUnitRosterSummary(int historical, int active, int alive, int combatCapable,
            int injured, int incapacitated)
        {
            Historical = historical;
            Active = active;
            Alive = alive;
            CombatCapable = combatCapable;
            Injured = injured;
            Incapacitated = incapacitated;
        }

        public int Historical { get; }
        public int Active { get; }
        public int Alive { get; }
        public int CombatCapable { get; }
        public int Injured { get; }
        public int Incapacitated { get; }
    }

    /// <summary>
    /// Canonical U21 unit authority. It owns membership and command state, never Character copies,
    /// health, physical combat or tier representations.
    /// </summary>
    public sealed class MilitaryUnitService
    {
        private sealed class Unit
        {
            public MilitaryUnitId Id;
            public MilitarySideId Side;
            public StableEntityId? CaptainId;
            public readonly Dictionary<StableEntityId, MilitaryUnitMemberState> Roster =
                new Dictionary<StableEntityId, MilitaryUnitMemberState>();
            public MilitaryUnitStatus Status;
            public MilitaryUnitRepresentationMode Representation;
            public string Location;
            public int Morale;
            public int Cohesion;
            public MilitaryUnitOrderState Order;
            public StableEntityId? EncounterId;
            public ulong Revision;
            public readonly Dictionary<ResourceId, long> Supply = new Dictionary<ResourceId, long>();
            public readonly Dictionary<string, MilitarySupplyCheckpointState> SupplyCheckpoints =
                new Dictionary<string, MilitarySupplyCheckpointState>(StringComparer.Ordinal);
            public long ConsumedSupplyUnits;
            public bool SupplyInitialized;
        }

        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly Dictionary<MilitaryUnitId, Unit> units = new Dictionary<MilitaryUnitId, Unit>();
        private readonly Dictionary<StableEntityId, MilitaryUnitId> activeMembership =
            new Dictionary<StableEntityId, MilitaryUnitId>();
        private readonly MilitarySupplySettings supplySettings;

        public MilitaryUnitService(CharacterRegistry characterRegistry, ICharacterCapabilityProvider capabilityProvider,
            MilitarySupplySettings settings = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            supplySettings = settings ?? MilitarySupplySettings.StoneAge;
        }

        public MilitarySupplySettings SupplySettings => supplySettings;

        public IReadOnlyList<MilitaryUnitState> Units => units.Values.OrderBy(x => x.Id).Select(Capture).ToArray();

        public MilitaryUnitState Create(MilitarySideId sideId, IEnumerable<StableEntityId> roster,
            StableEntityId captainId) => Create(MilitaryUnitId.NewId(), sideId, roster, captainId);

        public MilitaryUnitState Create(MilitaryUnitId id, MilitarySideId sideId,
            IEnumerable<StableEntityId> roster, StableEntityId captainId,
            MilitaryUnitRepresentationMode representation = MilitaryUnitRepresentationMode.Detailed,
            string abstractLocationId = "local")
        {
            if (!id.IsValid) throw new ArgumentException("Military unit ID required.", nameof(id));
            if (!sideId.IsValid) throw new ArgumentException("Military side ID required.", nameof(sideId));
            if (units.ContainsKey(id)) throw new InvalidOperationException("Military unit ID is already registered.");
            if (!Enum.IsDefined(typeof(MilitaryUnitRepresentationMode), representation))
                throw new ArgumentOutOfRangeException(nameof(representation));
            var members = OrderedDistinctRequired(roster);
            if (members.Length == 0) throw new ArgumentException("A military unit requires at least one member.", nameof(roster));
            foreach (var memberId in members)
            {
                characters.Get(memberId);
                if (activeMembership.ContainsKey(memberId))
                    throw new InvalidOperationException("A character cannot belong to two active military units.");
            }
            if (!members.Contains(captainId)) throw new ArgumentException("Captain must be in the roster.", nameof(captainId));
            if (!IsCaptainEligible(captainId)) throw new ArgumentException("Captain is not eligible.", nameof(captainId));

            var unit = new Unit
            {
                Id = id,
                Side = sideId,
                CaptainId = captainId,
                Status = MilitaryUnitStatus.Ready,
                Representation = representation,
                Location = ValidateLocation(abstractLocationId),
                Morale = 1000,
                Cohesion = 1000,
                Order = MilitaryUnitOrderState.Idle(),
                Revision = 1
            };
            ulong sequence = 1;
            foreach (var memberId in members)
            {
                unit.Roster.Add(memberId, new MilitaryUnitMemberState(memberId, MilitaryUnitMemberRole.Soldier,
                    MilitaryUnitMemberStatus.Active, sequence++));
                activeMembership.Add(memberId, id);
            }
            unit.Revision = sequence;
            units.Add(id, unit);
            return Capture(unit);
        }

        public MilitaryUnitState Get(MilitaryUnitId id) => units.TryGetValue(id, out var unit)
            ? Capture(unit) : throw new KeyNotFoundException("Unknown military unit: " + id);

        public bool TryGet(MilitaryUnitId id, out MilitaryUnitState state)
        {
            state = null;
            if (!units.TryGetValue(id, out var unit)) return false;
            state = Capture(unit);
            return true;
        }

        public bool TryGetForMember(StableEntityId characterId, out MilitaryUnitState state)
        {
            state = null;
            return activeMembership.TryGetValue(characterId, out var unitId) && TryGet(unitId, out state);
        }

        public long LoadInitialSupply(MilitaryUnitId id, ResourceInventoryRegistry inventories,
            IEnumerable<InventoryOwner> sourceOwners)
        {
            if (inventories == null) throw new ArgumentNullException(nameof(inventories));
            var unit = Require(id);
            var wasInitialized = unit.SupplyInitialized;
            var desired = checked(unit.Roster.Values.Count(x => x.IsActive) * supplySettings.InitialFoodPerMember);
            var current = unit.Supply.TryGetValue(supplySettings.FoodResourceId, out var amount) ? amount : 0L;
            var remaining = Math.Max(0L, desired - current);
            if (remaining == 0)
            {
                if (!wasInitialized)
                {
                    unit.SupplyInitialized = true;
                    unit.Revision++;
                }
                return 0;
            }
            var sources = (sourceOwners ?? throw new ArgumentNullException(nameof(sourceOwners))).Distinct()
                .OrderBy(x => x.Kind).ThenBy(x => x.Id).Select(inventories.Get).ToArray();
            var deductions = new List<Tuple<ResourceInventory, long>>();
            foreach (var source in sources)
            {
                var take = Math.Min(remaining, source.GetAmount(supplySettings.FoodResourceId).Units);
                if (take > 0) deductions.Add(Tuple.Create(source, take));
                remaining -= take;
                if (remaining == 0) break;
            }
            var loaded = desired - current - remaining;
            var charged = new List<Tuple<ResourceInventory, long>>();
            try
            {
                foreach (var deduction in deductions)
                {
                    if (!deduction.Item1.TryRemove(supplySettings.FoodResourceId, new ResourceQuantity(deduction.Item2)))
                        throw new InvalidOperationException("Supply funding changed during transfer.");
                    charged.Add(deduction);
                }
                if (loaded > 0)
                    unit.Supply[supplySettings.FoodResourceId] = checked(current + loaded);
                unit.SupplyInitialized = true;
                if (!wasInitialized || loaded > 0)
                    unit.Revision++;
                return loaded;
            }
            catch
            {
                for (var i = charged.Count - 1; i >= 0; i--)
                    charged[i].Item1.TryAdd(supplySettings.FoodResourceId, new ResourceQuantity(charged[i].Item2));
                throw;
            }
        }

        public MilitarySupplyConsumptionResult ConsumeSupplyCheckpoint(MilitaryUnitId id,
            MilitarySupplyCheckpointKind kind, long calendarTick, string key)
        {
            var unit = Require(id);
            if (!unit.SupplyInitialized)
                return new MilitarySupplyConsumptionResult(false, 0, 0, 1000);
            if (unit.SupplyCheckpoints.TryGetValue(key, out var prior))
                return new MilitarySupplyConsumptionResult(false, prior.RequiredUnits, prior.ConsumedUnits,
                    prior.ModifierPerThousand);
            var active = unit.Roster.Values.Count(x => x.IsActive);
            var perMember = kind == MilitarySupplyCheckpointKind.Travel
                ? supplySettings.TravelFoodPerMember : supplySettings.CombatFoodPerMember;
            var required = checked(active * perMember);
            var available = unit.Supply.TryGetValue(supplySettings.FoodResourceId, out var value) ? value : 0L;
            var consumed = Math.Min(required, available);
            if (consumed > 0)
            {
                var next = available - consumed;
                if (next == 0) unit.Supply.Remove(supplySettings.FoodResourceId);
                else unit.Supply[supplySettings.FoodResourceId] = next;
                unit.ConsumedSupplyUnits = checked(unit.ConsumedSupplyUnits + consumed);
            }
            if (consumed < required) unit.Morale = Math.Max(0, unit.Morale - supplySettings.ShortageMoraleLoss);
            var modifier = consumed >= required ? 1000 : kind == MilitarySupplyCheckpointKind.Travel
                ? supplySettings.ShortageMovementDurationPerThousand : supplySettings.ShortageCombatPerThousand;
            var marker = new MilitarySupplyCheckpointState(kind, calendarTick, key, required, consumed, modifier);
            unit.SupplyCheckpoints.Add(marker.Key, marker);
            unit.Revision++;
            return new MilitarySupplyConsumptionResult(true, required, consumed, modifier);
        }

        public int GetTravelDurationModifier(MilitaryUnitId id) =>
            SupplyModifier(Require(id), MilitarySupplyCheckpointKind.Travel);

        public int GetCombatModifier(StableEntityId characterId)
        {
            if (!activeMembership.TryGetValue(characterId, out var unitId)) return 1000;
            return SupplyModifier(Require(unitId), MilitarySupplyCheckpointKind.Combat);
        }

        public int ConsumeCombatSupplyForCharacter(StableEntityId characterId, long calendarTick)
        {
            if (!activeMembership.TryGetValue(characterId, out var unitId)) return 1000;
            var bucket = calendarTick / TimeSpan.TicksPerHour;
            return ConsumeSupplyCheckpoint(unitId, MilitarySupplyCheckpointKind.Combat,
                checked(bucket * TimeSpan.TicksPerHour), "combat:" + unitId + ":" + bucket).ModifierPerThousand;
        }

        public CommanderAssignmentStatus AssignCaptain(MilitaryUnitId id, StableEntityId captainId)
        {
            if (!units.TryGetValue(id, out var unit)) return CommanderAssignmentStatus.UnitNotFound;
            if (!unit.Roster.TryGetValue(captainId, out var member) || !member.IsActive)
                return CommanderAssignmentStatus.NotMember;
            if (!IsCaptainEligible(captainId)) return CommanderAssignmentStatus.Ineligible;
            unit.CaptainId = captainId;
            unit.Status = unit.EncounterId.HasValue ? MilitaryUnitStatus.Engaged : MilitaryUnitStatus.Ready;
            unit.Revision++;
            return CommanderAssignmentStatus.Assigned;
        }

        public void AddMember(MilitaryUnitId id, StableEntityId characterId)
        {
            var unit = Require(id);
            characters.Get(characterId);
            if (activeMembership.ContainsKey(characterId))
                throw new InvalidOperationException("A character cannot belong to two active military units.");
            unit.Revision++;
            if (unit.Roster.TryGetValue(characterId, out var historical))
            {
                unit.Roster[characterId] = new MilitaryUnitMemberState(characterId, historical.Role,
                    MilitaryUnitMemberStatus.Active, unit.Revision);
            }
            else
            {
                unit.Roster.Add(characterId, new MilitaryUnitMemberState(characterId,
                    MilitaryUnitMemberRole.Soldier, MilitaryUnitMemberStatus.Active, unit.Revision));
            }
            activeMembership.Add(characterId, id);
            RefreshCaptain(unit);
        }

        public void RemoveMember(MilitaryUnitId id, StableEntityId characterId)
        {
            var unit = Require(id);
            if (unit.EncounterId.HasValue) throw new InvalidOperationException("Cannot change roster during physical combat.");
            if (!unit.Roster.TryGetValue(characterId, out var member) || !member.IsActive)
                throw new InvalidOperationException("Character is not an active member of this unit.");
            unit.Revision++;
            unit.Roster[characterId] = new MilitaryUnitMemberState(characterId, member.Role,
                MilitaryUnitMemberStatus.Removed, member.JoinedSequence, unit.Revision);
            activeMembership.Remove(characterId);
            if (unit.CaptainId == characterId) unit.CaptainId = null;
            RefreshCaptain(unit);
            if (!unit.Roster.Values.Any(x => x.IsActive)) unit.Status = MilitaryUnitStatus.Disbanded;
        }

        public void TransferMember(MilitaryUnitId sourceId, MilitaryUnitId targetId, StableEntityId characterId)
        {
            if (sourceId == targetId) throw new ArgumentException("Source and target units must differ.");
            var source = Require(sourceId);
            var target = Require(targetId);
            if (source.EncounterId.HasValue || target.EncounterId.HasValue)
                throw new InvalidOperationException("Cannot transfer members during physical combat.");
            if (source.Side != target.Side) throw new InvalidOperationException("Cross-side transfer is not permitted.");
            RemoveMember(sourceId, characterId);
            AddMember(targetId, characterId);
        }

        public void TransferOwnership(MilitaryUnitId id, MilitarySideId newOwnerSideId)
        {
            if (!newOwnerSideId.IsValid) throw new ArgumentException("Military side ID required.", nameof(newOwnerSideId));
            var unit = Require(id);
            if (unit.EncounterId.HasValue)
                throw new InvalidOperationException("Cannot transfer a unit during physical combat.");
            if (unit.Side == newOwnerSideId) return;
            unit.Side = newOwnerSideId;
            unit.Revision++;
        }

        public StableEntityId? RefreshCaptain(MilitaryUnitId id) => RefreshCaptain(Require(id));

        public MilitaryUnitRosterSummary GetRosterSummary(MilitaryUnitId id)
        {
            var unit = Require(id);
            var active = unit.Roster.Values.Where(x => x.IsActive).ToArray();
            var alive = 0;
            var combatCapable = 0;
            var injured = 0;
            var incapacitated = 0;
            foreach (var member in active)
            {
                var character = characters.Get(member.CharacterId);
                if (character.LifeState != CharacterLifeState.Alive) continue;
                alive++;
                var value = capabilities.GetCapabilities(member.CharacterId);
                if (value.IsIncapacitated) incapacitated++;
                if (value.WorkPerThousand < 1000 || value.MovementPerThousand < 1000 ||
                    value.ResearchPerThousand < 1000 || value.CombatPerThousand < 1000) injured++;
                if (value.CanFight) combatCapable++;
            }
            return new MilitaryUnitRosterSummary(unit.Roster.Count, active.Length, alive, combatCapable,
                injured, incapacitated);
        }

        public IReadOnlyList<StableEntityId> GetCombatEligibleMembers(MilitaryUnitId id) => Require(id).Roster.Values
            .Where(x => x.IsActive && IsCombatEligible(x.CharacterId))
            .Select(x => x.CharacterId).OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();

        public void SetOrder(MilitaryUnitId id, MilitaryUnitOrderKind kind, WorldPosition target = default,
            bool hasTarget = false, MilitaryUnitId? targetUnitId = null)
        {
            var unit = Require(id);
            unit.Revision++;
            unit.Order = new MilitaryUnitOrderState(kind, target, hasTarget, targetUnitId, unit.Revision);
        }

        public void SetAbstractLocation(MilitaryUnitId id, string locationId)
        {
            var unit = Require(id);
            unit.Location = ValidateLocation(locationId);
            unit.Revision++;
        }

        internal void SetRepresentation(MilitaryUnitId id, MilitaryUnitRepresentationMode mode,
            bool activeBattleAllowsAggregate = false)
        {
            var unit = Require(id);
            if (unit.EncounterId.HasValue && mode == MilitaryUnitRepresentationMode.Aggregate &&
                !activeBattleAllowsAggregate)
                throw new InvalidOperationException("A unit in physical combat cannot become aggregate.");
            unit.Representation = mode;
            unit.Revision++;
        }

        internal void AttachEncounter(MilitaryUnitId id, StableEntityId encounterId, MilitaryUnitId enemyId,
            BattleMode mode = BattleMode.Physical)
        {
            var unit = Require(id);
            if (mode == BattleMode.Physical && unit.Representation != MilitaryUnitRepresentationMode.Detailed)
                throw new InvalidOperationException("Only a detailed unit can enter physical combat.");
            if (mode == BattleMode.Abstract && unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                throw new InvalidOperationException("Only an aggregate unit can enter abstract combat.");
            if (unit.EncounterId.HasValue) throw new InvalidOperationException("Unit is already in physical combat.");
            unit.EncounterId = encounterId;
            unit.Status = MilitaryUnitStatus.Engaged;
            unit.Revision++;
            unit.Order = new MilitaryUnitOrderState(MilitaryUnitOrderKind.Engage, default, false,
                enemyId, unit.Revision);
        }

        internal void DetachEncounter(MilitaryUnitId id, StableEntityId encounterId)
        {
            var unit = Require(id);
            if (unit.EncounterId != encounterId) return;
            unit.EncounterId = null;
            unit.Order = MilitaryUnitOrderState.Idle(++unit.Revision);
            unit.Status = MilitaryUnitStatus.Ready;
            RefreshCaptain(unit);
        }

        internal void ReconcileEncounter(MilitaryUnitId id, CombatEncounterState encounter)
        {
            var unit = Require(id);
            if (!unit.EncounterId.HasValue || unit.EncounterId.Value != encounter.EncounterId)
                throw new InvalidOperationException("Encounter link does not match the unit.");
            var memberStates = encounter.Combatants.Where(x => unit.Roster.ContainsKey(x.CharacterId)).ToArray();
            if (memberStates.Length > 0)
            {
                unit.Morale = Math.Max(0, Math.Min(1000, (int)memberStates.Average(x => x.Morale)));
                var summary = GetRosterSummary(id);
                unit.Cohesion = summary.Active == 0 ? 0 : summary.CombatCapable * 1000 / summary.Active;
            }
            if (encounter.Status != CombatEncounterStatus.Active)
            {
                unit.EncounterId = null;
                unit.Order = MilitaryUnitOrderState.Idle(++unit.Revision);
                unit.Status = MilitaryUnitStatus.Ready;
            }
            RefreshCaptain(unit);
        }

        public MilitaryUnitSaveState CaptureState()
        {
            foreach (var unit in units.Values.OrderBy(x => x.Id)) RefreshCaptain(unit);
            return new MilitaryUnitSaveState(Units);
        }

        public void RestoreState(MilitaryUnitSaveState state, CombatService combat = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var restored = new Dictionary<MilitaryUnitId, Unit>();
            var memberships = new Dictionary<StableEntityId, MilitaryUnitId>();
            foreach (var saved in state.Units)
            {
                foreach (var member in saved.Roster)
                {
                    characters.Get(member.CharacterId);
                    if (member.IsActive && !memberships.TryAdd(member.CharacterId, saved.Id))
                        throw new ArgumentException("A saved character is active in multiple military units.");
                }
                if (saved.CaptainId.HasValue && !IsCaptainEligible(saved.CaptainId.Value))
                    throw new ArgumentException("Saved captain is no longer eligible.");
                if (saved.ActiveEncounterId.HasValue)
                {
                    if (combat == null) throw new InvalidOperationException("Saved active unit encounter requires CombatService.");
                    var encounter = combat.Encounters.SingleOrDefault(x => x.EncounterId == saved.ActiveEncounterId.Value);
                    if (encounter == null || encounter.Status != CombatEncounterStatus.Active)
                        throw new ArgumentException("Saved active military encounter is missing or terminal.");
                    var linkedCombatants = encounter.Combatants.Where(x =>
                        saved.ActiveMemberIds.Contains(x.CharacterId)).ToArray();
                    if (linkedCombatants.Length == 0)
                        throw new ArgumentException("Saved encounter contains no active unit member.");
                    if (linkedCombatants.Any(x => !string.Equals(x.SideId.Value, saved.OwnerSideId.Value,
                        StringComparison.Ordinal)))
                        throw new ArgumentException("Saved unit owner does not match its encounter side.");
                    if (encounter.Mode == BattleMode.Physical &&
                        saved.Representation != MilitaryUnitRepresentationMode.Detailed)
                        throw new ArgumentException("Saved physical battle requires detailed units.");
                    if (encounter.Mode == BattleMode.Abstract &&
                        saved.Representation != MilitaryUnitRepresentationMode.Aggregate)
                        throw new ArgumentException("Saved abstract battle requires aggregate units.");
                }
                var unit = Restore(saved);
                if (!restored.TryAdd(unit.Id, unit)) throw new ArgumentException("Duplicate saved military unit ID.");
            }
            units.Clear();
            foreach (var pair in restored) units.Add(pair.Key, pair.Value);
            activeMembership.Clear();
            foreach (var pair in memberships) activeMembership.Add(pair.Key, pair.Value);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var unit in Units)
            {
                Add(ref hash, unit.Id.ToString());
                Add(ref hash, unit.OwnerSideId.Value);
                Add(ref hash, unit.CaptainId?.ToString() ?? string.Empty);
                Add(ref hash, ((int)unit.Status).ToString());
                Add(ref hash, ((int)unit.Representation).ToString());
                Add(ref hash, unit.AbstractLocationId);
                Add(ref hash, unit.MoraleSummary.ToString());
                Add(ref hash, unit.Cohesion.ToString());
                Add(ref hash, ((int)unit.Order.Kind).ToString());
                Add(ref hash, unit.Order.HasTarget ? "1" : "0");
                Add(ref hash, BitConverter.ToUInt32(BitConverter.GetBytes(unit.Order.Target.X), 0).ToString());
                Add(ref hash, BitConverter.ToUInt32(BitConverter.GetBytes(unit.Order.Target.Y), 0).ToString());
                Add(ref hash, BitConverter.ToUInt32(BitConverter.GetBytes(unit.Order.Target.Z), 0).ToString());
                Add(ref hash, unit.Order.TargetUnitId?.ToString() ?? string.Empty);
                Add(ref hash, unit.Order.Sequence.ToString());
                Add(ref hash, unit.ActiveEncounterId?.ToString() ?? string.Empty);
                Add(ref hash, unit.Revision.ToString());
                Add(ref hash, unit.Supply.ConsumedUnits.ToString());
                Add(ref hash, unit.Supply.Initialized ? "1" : "0");
                foreach (var amount in unit.Supply.Amounts)
                { Add(ref hash, amount.ResourceId.ToString()); Add(ref hash, amount.Quantity.Units.ToString()); }
                foreach (var checkpoint in unit.Supply.Checkpoints)
                {
                    Add(ref hash, ((int)checkpoint.Kind).ToString()); Add(ref hash, checkpoint.CalendarTick.ToString());
                    Add(ref hash, checkpoint.Key); Add(ref hash, checkpoint.RequiredUnits.ToString());
                    Add(ref hash, checkpoint.ConsumedUnits.ToString()); Add(ref hash, checkpoint.ModifierPerThousand.ToString());
                }
                foreach (var member in unit.Roster)
                {
                    Add(ref hash, member.CharacterId.ToString());
                    Add(ref hash, ((int)member.Role).ToString());
                    Add(ref hash, ((int)member.Status).ToString());
                    Add(ref hash, member.JoinedSequence.ToString());
                    Add(ref hash, member.RemovedSequence?.ToString() ?? string.Empty);
                }
            }
            return hash;
        }

        private StableEntityId? RefreshCaptain(Unit unit)
        {
            if (unit.CaptainId.HasValue && unit.Roster.TryGetValue(unit.CaptainId.Value, out var current) &&
                current.IsActive && IsCaptainEligible(unit.CaptainId.Value)) return unit.CaptainId;
            var oldCaptain = unit.CaptainId;
            var oldStatus = unit.Status;
            unit.CaptainId = unit.Roster.Values.Where(x => x.IsActive && IsCaptainEligible(x.CharacterId))
                .Select(x => characters.Get(x.CharacterId))
                .OrderByDescending(x => x.Influence.Value)
                .ThenBy(x => x.Id.ToString(), StringComparer.Ordinal)
                .Select(x => (StableEntityId?)x.Id).FirstOrDefault();
            if (!unit.EncounterId.HasValue)
                unit.Status = unit.CaptainId.HasValue ? MilitaryUnitStatus.Ready : MilitaryUnitStatus.Leaderless;
            if (oldCaptain != unit.CaptainId || oldStatus != unit.Status) unit.Revision++;
            return unit.CaptainId;
        }

        private bool IsCaptainEligible(StableEntityId id) => characters.TryGet(id, out var character) &&
            character.LifeState == CharacterLifeState.Alive && capabilities.GetCapabilities(id).CanFight;

        private bool IsCombatEligible(StableEntityId id) => IsCaptainEligible(id);

        private Unit Require(MilitaryUnitId id) => units.TryGetValue(id, out var unit)
            ? unit : throw new KeyNotFoundException("Unknown military unit: " + id);

        private static MilitaryUnitState Capture(Unit unit) => new MilitaryUnitState(unit.Id, unit.Side,
            unit.CaptainId, unit.Roster.Values, unit.Status, unit.Representation, unit.Location, unit.Morale,
            unit.Cohesion, unit.Order, unit.EncounterId, unit.Revision,
            new MilitarySupplyState(unit.Supply.Select(x => new ResourceAmount(x.Key, new ResourceQuantity(x.Value))),
                unit.SupplyCheckpoints.Values, unit.ConsumedSupplyUnits, unit.SupplyInitialized));

        private static Unit Restore(MilitaryUnitState state)
        {
            var unit = new Unit
            {
                Id = state.Id,
                Side = state.OwnerSideId,
                CaptainId = state.CaptainId,
                Status = state.Status,
                Representation = state.Representation,
                Location = state.AbstractLocationId,
                Morale = state.MoraleSummary,
                Cohesion = state.Cohesion,
                Order = state.Order,
                EncounterId = state.ActiveEncounterId,
                Revision = state.Revision
            };
            foreach (var member in state.Roster) unit.Roster.Add(member.CharacterId, member);
            foreach (var amount in state.Supply.Amounts) unit.Supply.Add(amount.ResourceId, amount.Quantity.Units);
            foreach (var checkpoint in state.Supply.Checkpoints) unit.SupplyCheckpoints.Add(checkpoint.Key, checkpoint);
            unit.ConsumedSupplyUnits = state.Supply.ConsumedUnits;
            unit.SupplyInitialized = state.Supply.Initialized;
            return unit;
        }

        private int SupplyModifier(Unit unit, MilitarySupplyCheckpointKind kind)
        {
            var available = unit.Supply.TryGetValue(supplySettings.FoodResourceId, out var value) ? value : 0L;
            if (!unit.SupplyInitialized) return 1000;
            if (available > 0) return 1000;
            return kind == MilitarySupplyCheckpointKind.Travel
                ? supplySettings.ShortageMovementDurationPerThousand : supplySettings.ShortageCombatPerThousand;
        }

        private static StableEntityId[] OrderedDistinctRequired(IEnumerable<StableEntityId> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var values = ids.ToArray();
            if (values.Any(x => !x.IsValid) || values.Distinct().Count() != values.Length)
                throw new ArgumentException("Roster IDs must be valid and unique.", nameof(ids));
            return values.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        }

        private static string ValidateLocation(string value)
        {
            if (value == null || value.Length > 128 || value != value.Trim())
                throw new ArgumentException("Abstract location must be trimmed and at most 128 characters.", nameof(value));
            return value;
        }

        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }

    public sealed class MilitaryUnitRepresentationService
    {
        private readonly MilitaryUnitService units;
        private readonly TierManager tiers;
        private readonly CombatService combat;
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;

        public MilitaryUnitRepresentationService(MilitaryUnitService unitService, TierManager tierManager,
            CombatService combatService, CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider)
        {
            units = unitService ?? throw new ArgumentNullException(nameof(unitService));
            tiers = tierManager ?? throw new ArgumentNullException(nameof(tierManager));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
        }

        public MilitaryUnitState Transition(MilitaryUnitId id, MilitaryUnitRepresentationMode target,
            CharacterSimulationTier aggregateTier = CharacterSimulationTier.Tier3)
        {
            var before = units.Get(id);
            if (before.Representation == target) return before;
            var aggregateBattleAllowed = false;
            if (before.ActiveEncounterId.HasValue && combat.TryGetEncounter(before.ActiveEncounterId.Value,
                out var linkedBattle))
                aggregateBattleAllowed = linkedBattle.Mode == BattleMode.Abstract;
            if (target == MilitaryUnitRepresentationMode.Aggregate &&
                ((!aggregateBattleAllowed && before.ActiveEncounterId.HasValue) ||
                 before.ActiveMemberIds.Any(combat.IsPinnedToTier1)))
                throw new InvalidOperationException("Active physical combat pins the unit to detailed representation.");
            if (target == MilitaryUnitRepresentationMode.Aggregate && aggregateTier == CharacterSimulationTier.Tier1)
                throw new ArgumentException("Aggregate transition target must be Tier2 or Tier3.", nameof(aggregateTier));

            var changed = new List<Tuple<StableEntityId, CharacterSimulationTier>>();
            try
            {
                foreach (var memberId in before.ActiveMemberIds.OrderBy(x => x.ToString(), StringComparer.Ordinal))
                {
                    if (!characters.TryGet(memberId, out var character) || character.LifeState != CharacterLifeState.Alive ||
                        capabilities.GetCapabilities(memberId).IsIncapacitated || !tiers.TryGetTier(memberId, out var current))
                        continue;
                    var desired = target == MilitaryUnitRepresentationMode.Detailed
                        ? CharacterSimulationTier.Tier1 : aggregateTier;
                    if (current == desired) continue;
                    tiers.Transition(memberId, desired);
                    changed.Add(Tuple.Create(memberId, current));
                }
                units.SetRepresentation(id, target, aggregateBattleAllowed);
                return units.Get(id);
            }
            catch
            {
                for (var i = changed.Count - 1; i >= 0; i--)
                    if (tiers.GetTier(changed[i].Item1) != changed[i].Item2)
                        tiers.Transition(changed[i].Item1, changed[i].Item2);
                throw;
            }
        }
    }

    public sealed class MilitaryUnitCommandService
    {
        private readonly MilitaryUnitService units;
        private readonly ManualMoveCommandService movement;

        public MilitaryUnitCommandService(MilitaryUnitService unitService, ManualMoveCommandService movementService)
        {
            units = unitService ?? throw new ArgumentNullException(nameof(unitService));
            movement = movementService ?? throw new ArgumentNullException(nameof(movementService));
        }

        public ManualMoveCommandResult Move(MilitaryUnitId id, WorldPosition target,
            WorkPriority priority = WorkPriority.Normal)
        {
            var unit = units.Get(id);
            units.SetOrder(id, MilitaryUnitOrderKind.Move, target, true);
            if (unit.Representation != MilitaryUnitRepresentationMode.Detailed)
                return new ManualMoveCommandResult(Array.Empty<ManualMoveCommandEntry>());
            return movement.Issue(units.GetCombatEligibleMembers(id), target, priority);
        }

        public void Hold(MilitaryUnitId id) => units.SetOrder(id, MilitaryUnitOrderKind.Hold);
    }

    public sealed class MilitaryUnitCombatService
    {
        private readonly MilitaryUnitService units;
        private readonly CombatService combat;

        public MilitaryUnitCombatService(MilitaryUnitService unitService, CombatService combatService)
        {
            units = unitService ?? throw new ArgumentNullException(nameof(unitService));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
        }

        public CombatEncounterState Start(MilitaryUnitId firstId, MilitaryUnitId secondId,
            StableEntityId encounterId, long calendarTick,
            Func<StableEntityId, CombatWeaponId> weaponSelector = null)
        {
            var first = units.Get(firstId);
            var second = units.Get(secondId);
            if (first.OwnerSideId == second.OwnerSideId) throw new InvalidOperationException("Units are not hostile.");
            if (first.ActiveEncounterId.HasValue || second.ActiveEncounterId.HasValue)
                throw new InvalidOperationException("A unit is already linked to physical combat.");
            if (first.Representation != MilitaryUnitRepresentationMode.Detailed ||
                second.Representation != MilitaryUnitRepresentationMode.Detailed)
                throw new InvalidOperationException("Physical combat requires detailed units.");
            var firstSide = new CombatSideId(first.OwnerSideId.Value);
            var secondSide = new CombatSideId(second.OwnerSideId.Value);
            var firstCandidates = units.GetCombatEligibleMembers(firstId);
            var secondCandidates = units.GetCombatEligibleMembers(secondId);
            foreach (var id in firstCandidates) combat.RegisterSide(id, firstSide);
            foreach (var id in secondCandidates) combat.RegisterSide(id, secondSide);
            var firstMembers = firstCandidates.Where(combat.IsEligible).ToArray();
            var secondMembers = secondCandidates.Where(combat.IsEligible).ToArray();
            if (firstMembers.Length == 0 || secondMembers.Length == 0)
                throw new InvalidOperationException("Both units require local Tier1 combat-capable members.");
            var encounter = combat.CreateEncounter(encounterId, firstSide, secondSide, firstMembers, secondMembers,
                calendarTick, weaponSelector, BattleMode.Physical, firstId, secondId);
            units.AttachEncounter(firstId, encounterId, secondId, BattleMode.Physical);
            units.AttachEncounter(secondId, encounterId, firstId, BattleMode.Physical);
            return encounter;
        }

        public void Reconcile(MilitaryUnitId id)
        {
            var unit = units.Get(id);
            if (!unit.ActiveEncounterId.HasValue)
            {
                units.RefreshCaptain(id);
                return;
            }
            var encounter = combat.Encounters.SingleOrDefault(x => x.EncounterId == unit.ActiveEncounterId.Value);
            if (encounter == null) throw new InvalidOperationException("Linked physical encounter is missing.");
            units.ReconcileEncounter(id, encounter);
        }

        public void ReconcileAll()
        {
            foreach (var unit in units.Units) Reconcile(unit.Id);
        }
    }
}
