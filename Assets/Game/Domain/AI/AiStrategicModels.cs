using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Domain.AI
{
    public readonly struct AiStateId : IEquatable<AiStateId>, IComparable<AiStateId>
    {
        public AiStateId(StableEntityId value)
        {
            if (!value.IsValid) throw new ArgumentException("AI state ID must be valid.", nameof(value));
            Value = value;
        }
        public StableEntityId Value { get; }
        public bool IsValid => Value.IsValid;
        public int CompareTo(AiStateId other) => Value.CompareTo(other.Value);
        public bool Equals(AiStateId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is AiStateId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(AiStateId left, AiStateId right) => left.Equals(right);
        public static bool operator !=(AiStateId left, AiStateId right) => !left.Equals(right);
    }

    public enum AiStrategicGoal
    {
        StabilizeEconomy = 1, AdvanceResearch = 2, DevelopSettlement = 3, FormArmy = 4,
        AttackSettlement = 5, CaptureSettlement = 6, Recover = 7, Defeated = 8
    }

    public enum AiDecisionKind
    {
        None = 0, Produce = 1, StartResearch = 2, ConstructBuilding = 3, FormMilitaryUnit = 4,
        AssignCaptain = 5, Travel = 6, AwaitBattle = 7, Capture = 8, Recover = 9, Defeated = 10,
        Occupy = 11
    }

    public enum AiDefeatReason { None = 0, NoControlledSettlement = 1, DynastyExtinct = 2 }

    public sealed class AiStrategicSettings
    {
        public AiStrategicSettings(long decisionIntervalTicks, int minimumArmySize, int foodTarget,
            int materialTarget, IEnumerable<string> buildingPriorities = null)
        {
            if (decisionIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(decisionIntervalTicks));
            if (minimumArmySize <= 0 || foodTarget < 0 || materialTarget < 0)
                throw new ArgumentOutOfRangeException();
            DecisionIntervalTicks = decisionIntervalTicks;
            MinimumArmySize = minimumArmySize;
            FoodTarget = foodTarget;
            MaterialTarget = materialTarget;
            BuildingPriorities = Array.AsReadOnly((buildingPriorities ?? new[]
                { "primitive_shelter", "storage_basket", "primitive_crop_plot" }).ToArray());
        }

        public long DecisionIntervalTicks { get; }
        public int MinimumArmySize { get; }
        public int FoodTarget { get; }
        public int MaterialTarget { get; }
        public IReadOnlyList<string> BuildingPriorities { get; }
        public static AiStrategicSettings StoneAge => new AiStrategicSettings(
            TimeSpan.FromHours(6).Ticks, 3, 24, 16);
    }

    public readonly struct AiDecision
    {
        public AiDecision(long sequence, long tick, AiDecisionKind kind, AiStrategicGoal goal, int score,
            string reason, SettlementId? targetSettlementId = null, WorldLocationId? targetLocationId = null,
            MilitaryUnitId? unitId = null)
        {
            if (sequence <= 0 || tick < 0 || !Enum.IsDefined(typeof(AiDecisionKind), kind) ||
                !Enum.IsDefined(typeof(AiStrategicGoal), goal)) throw new ArgumentOutOfRangeException();
            Sequence = sequence; Tick = tick; Kind = kind; Goal = goal; Score = score;
            Reason = reason ?? string.Empty; TargetSettlementId = targetSettlementId;
            TargetLocationId = targetLocationId; UnitId = unitId;
        }
        public long Sequence { get; }
        public long Tick { get; }
        public AiDecisionKind Kind { get; }
        public AiStrategicGoal Goal { get; }
        public int Score { get; }
        public string Reason { get; }
        public SettlementId? TargetSettlementId { get; }
        public WorldLocationId? TargetLocationId { get; }
        public MilitaryUnitId? UnitId { get; }
    }

    public readonly struct AiPendingOrder
    {
        public AiPendingOrder(long sequence, AiDecisionKind kind, long issuedTick, MilitaryUnitId? unitId,
            SettlementId? targetSettlementId, WorldLocationId? targetLocationId)
        {
            if (sequence <= 0 || issuedTick < 0 || kind == AiDecisionKind.None)
                throw new ArgumentOutOfRangeException();
            Sequence = sequence; Kind = kind; IssuedTick = issuedTick; UnitId = unitId;
            TargetSettlementId = targetSettlementId; TargetLocationId = targetLocationId;
        }
        public long Sequence { get; }
        public AiDecisionKind Kind { get; }
        public long IssuedTick { get; }
        public MilitaryUnitId? UnitId { get; }
        public SettlementId? TargetSettlementId { get; }
        public WorldLocationId? TargetLocationId { get; }
    }

    public sealed class AiStrategicStateSnapshot
    {
        public AiStrategicStateSnapshot(AiStateId id, string ownerId, Combat.CombatSideId sideId,
            SettlementId homeSettlementId, StableEntityId rulerCharacterId,
            IEnumerable<SettlementId> controlledSettlements, IEnumerable<MilitaryUnitId> knownMilitaryUnits,
            AiStrategicGoal goal, SettlementId? targetSettlementId, WorldLocationId? targetLocationId,
            long lastDecisionTick, long nextDecisionTick, long decisionSequence,
            IEnumerable<AiPendingOrder> pendingOrders, IEnumerable<AiDecision> recentDecisions,
            bool isDefeated, AiDefeatReason defeatReason)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(ownerId) || ownerId != ownerId.Trim() || !sideId.IsValid ||
                !homeSettlementId.IsValid || !rulerCharacterId.IsValid || lastDecisionTick < 0 ||
                nextDecisionTick < lastDecisionTick || decisionSequence < 0 ||
                !Enum.IsDefined(typeof(AiStrategicGoal), goal) ||
                !Enum.IsDefined(typeof(AiDefeatReason), defeatReason) ||
                targetSettlementId.HasValue && !targetSettlementId.Value.IsValid ||
                targetLocationId.HasValue && !targetLocationId.Value.IsValid ||
                isDefeated != (goal == AiStrategicGoal.Defeated) ||
                isDefeated != (defeatReason != AiDefeatReason.None))
                throw new ArgumentException("Invalid AI strategic snapshot.");
            Id = id; OwnerId = ownerId; SideId = sideId; HomeSettlementId = homeSettlementId;
            RulerCharacterId = rulerCharacterId;
            ControlledSettlements = OrderedUnique(controlledSettlements, x => x.IsValid, "settlements");
            KnownMilitaryUnits = OrderedUnique(knownMilitaryUnits, x => x.IsValid, "units");
            Goal = goal; TargetSettlementId = targetSettlementId; TargetLocationId = targetLocationId;
            LastDecisionTick = lastDecisionTick; NextDecisionTick = nextDecisionTick;
            DecisionSequence = decisionSequence;
            var orders = (pendingOrders ?? Array.Empty<AiPendingOrder>()).OrderBy(x => x.Sequence).ToArray();
            var decisions = (recentDecisions ?? Array.Empty<AiDecision>()).OrderBy(x => x.Sequence).ToArray();
            if (orders.Select(x => x.Sequence).Distinct().Count() != orders.Length ||
                decisions.Select(x => x.Sequence).Distinct().Count() != decisions.Length ||
                orders.Any(x => x.Sequence > decisionSequence) ||
                decisions.Any(x => x.Sequence > decisionSequence || x.Tick > lastDecisionTick) ||
                decisions.Length > 32)
                throw new ArgumentException("AI orders and decision history are incoherent.");
            PendingOrders = Array.AsReadOnly(orders);
            RecentDecisions = Array.AsReadOnly(decisions);
            IsDefeated = isDefeated; DefeatReason = defeatReason;
        }

        public AiStateId Id { get; }
        public string OwnerId { get; }
        public Combat.CombatSideId SideId { get; }
        public SettlementId HomeSettlementId { get; }
        public StableEntityId RulerCharacterId { get; }
        public IReadOnlyList<SettlementId> ControlledSettlements { get; }
        public IReadOnlyList<MilitaryUnitId> KnownMilitaryUnits { get; }
        public AiStrategicGoal Goal { get; }
        public SettlementId? TargetSettlementId { get; }
        public WorldLocationId? TargetLocationId { get; }
        public long LastDecisionTick { get; }
        public long NextDecisionTick { get; }
        public long DecisionSequence { get; }
        public IReadOnlyList<AiPendingOrder> PendingOrders { get; }
        public IReadOnlyList<AiDecision> RecentDecisions { get; }
        public bool IsDefeated { get; }
        public AiDefeatReason DefeatReason { get; }

        private static IReadOnlyList<T> OrderedUnique<T>(IEnumerable<T> source, Func<T, bool> valid, string name)
            where T : struct, IComparable<T>
        {
            var values = (source ?? Array.Empty<T>()).ToArray();
            if (values.Any(x => !valid(x)) || values.Distinct().Count() != values.Length)
                throw new ArgumentException("AI " + name + " must be valid and unique.");
            return Array.AsReadOnly(values.OrderBy(x => x).ToArray());
        }
    }

    public sealed class AiStrategicState
    {
        private readonly SortedSet<SettlementId> settlements = new SortedSet<SettlementId>();
        private readonly SortedSet<MilitaryUnitId> units = new SortedSet<MilitaryUnitId>();
        private readonly List<AiPendingOrder> orders = new List<AiPendingOrder>();
        private readonly List<AiDecision> decisions = new List<AiDecision>();

        public AiStrategicState(AiStrategicStateSnapshot snapshot) { Restore(snapshot); }
        public AiStateId Id { get; private set; }
        public string OwnerId { get; private set; }
        public Combat.CombatSideId SideId { get; private set; }
        public SettlementId HomeSettlementId { get; private set; }
        public StableEntityId RulerCharacterId { get; private set; }
        public IReadOnlyList<SettlementId> ControlledSettlements => settlements.ToArray();
        public IReadOnlyList<MilitaryUnitId> KnownMilitaryUnits => units.ToArray();
        public AiStrategicGoal Goal { get; private set; }
        public SettlementId? TargetSettlementId { get; private set; }
        public WorldLocationId? TargetLocationId { get; private set; }
        public long LastDecisionTick { get; private set; }
        public long NextDecisionTick { get; private set; }
        public long DecisionSequence { get; private set; }
        public IReadOnlyList<AiPendingOrder> PendingOrders => orders.AsReadOnly();
        public IReadOnlyList<AiDecision> RecentDecisions => decisions.AsReadOnly();
        public bool IsDefeated { get; private set; }
        public AiDefeatReason DefeatReason { get; private set; }

        public static AiStrategicState Create(AiStateId id, string ownerId, Combat.CombatSideId sideId,
            SettlementId homeSettlementId, StableEntityId rulerId, long initialTick)
            => new AiStrategicState(new AiStrategicStateSnapshot(id, ownerId, sideId, homeSettlementId, rulerId,
                new[] { homeSettlementId }, null, AiStrategicGoal.StabilizeEconomy, null, null,
                initialTick, initialTick, 0, null, null, false, AiDefeatReason.None));

        public void SetRuler(StableEntityId id) { if (!id.IsValid) throw new ArgumentException(); RulerCharacterId = id; }
        public void AddSettlement(SettlementId id) { if (!id.IsValid) throw new ArgumentException(); settlements.Add(id); }
        public void RemoveSettlement(SettlementId id) => settlements.Remove(id);
        public void AddUnit(MilitaryUnitId id) { if (!id.IsValid) throw new ArgumentException(); units.Add(id); }
        public void RemoveUnit(MilitaryUnitId id) => units.Remove(id);
        public void SetObjective(AiStrategicGoal goal, SettlementId? settlementId, WorldLocationId? locationId)
        { Goal = goal; TargetSettlementId = settlementId; TargetLocationId = locationId; }
        public void ClearOrders() => orders.Clear();
        public void AddOrder(AiPendingOrder order) { orders.RemoveAll(x => x.Sequence == order.Sequence); orders.Add(order); orders.Sort((a,b) => a.Sequence.CompareTo(b.Sequence)); }
        public void CompleteOrders(AiDecisionKind kind) => orders.RemoveAll(x => x.Kind == kind);
        public void MarkDefeated(AiDefeatReason reason)
        { if (reason == AiDefeatReason.None) throw new ArgumentOutOfRangeException(nameof(reason)); IsDefeated = true; DefeatReason = reason; Goal = AiStrategicGoal.Defeated; orders.Clear(); }
        public void RecordDecision(long tick, long interval, AiDecisionKind kind, AiStrategicGoal goal, int score,
            string reason, SettlementId? settlementId = null, WorldLocationId? locationId = null,
            MilitaryUnitId? unitId = null)
        {
            if (tick < LastDecisionTick || interval <= 0) throw new ArgumentOutOfRangeException();
            var decision = new AiDecision(++DecisionSequence, tick, kind, goal, score, reason,
                settlementId, locationId, unitId);
            decisions.Add(decision); if (decisions.Count > 32) decisions.RemoveAt(0);
            Goal = goal; TargetSettlementId = settlementId; TargetLocationId = locationId;
            LastDecisionTick = tick; NextDecisionTick = checked(tick + interval);
        }

        public AiStrategicStateSnapshot CaptureState() => new AiStrategicStateSnapshot(Id, OwnerId, SideId,
            HomeSettlementId, RulerCharacterId, settlements, units, Goal, TargetSettlementId, TargetLocationId,
            LastDecisionTick, NextDecisionTick, DecisionSequence, orders, decisions, IsDefeated, DefeatReason);

        public void Restore(AiStrategicStateSnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Id = state.Id; OwnerId = state.OwnerId; SideId = state.SideId; HomeSettlementId = state.HomeSettlementId;
            RulerCharacterId = state.RulerCharacterId; Goal = state.Goal; TargetSettlementId = state.TargetSettlementId;
            TargetLocationId = state.TargetLocationId; LastDecisionTick = state.LastDecisionTick;
            NextDecisionTick = state.NextDecisionTick; DecisionSequence = state.DecisionSequence;
            IsDefeated = state.IsDefeated; DefeatReason = state.DefeatReason;
            settlements.Clear(); foreach (var value in state.ControlledSettlements) settlements.Add(value);
            units.Clear(); foreach (var value in state.KnownMilitaryUnits) units.Add(value);
            orders.Clear(); orders.AddRange(state.PendingOrders); decisions.Clear(); decisions.AddRange(state.RecentDecisions);
        }
    }

    public sealed class AiFactionSaveState
    {
        public AiFactionSaveState(AiStrategicStateSnapshot strategic, DynastyStateSnapshot dynasty,
            ResearchSaveState research, ProductionSaveState production,
            IEnumerable<WorldResourcePileState> worldPiles = null)
        {
            Strategic = strategic ?? throw new ArgumentNullException(nameof(strategic));
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            Research = research ?? throw new ArgumentNullException(nameof(research));
            Production = production ?? throw new ArgumentNullException(nameof(production));
            var values = (worldPiles ?? Array.Empty<WorldResourcePileState>()).OrderBy(x => x.Id).ToArray();
            if (values.Select(x => x.Id).Distinct().Count() != values.Length)
                throw new ArgumentException("AI world piles must have unique identities.", nameof(worldPiles));
            WorldPiles = Array.AsReadOnly(values);
        }
        public AiStrategicStateSnapshot Strategic { get; }
        public DynastyStateSnapshot Dynasty { get; }
        public ResearchSaveState Research { get; }
        public ProductionSaveState Production { get; }
        public IReadOnlyList<WorldResourcePileState> WorldPiles { get; }
    }

    public sealed class AiStrategicSaveState
    {
        public AiStrategicSaveState(long worldSeed, long lastProcessedTick, IEnumerable<AiFactionSaveState> factions)
        {
            if (lastProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedTick));
            WorldSeed = worldSeed; LastProcessedTick = lastProcessedTick;
            var values = (factions ?? Array.Empty<AiFactionSaveState>()).OrderBy(x => x.Strategic.Id).ToArray();
            if (values.Any(x => x == null) || values.Select(x => x.Strategic.Id).Distinct().Count() != values.Length)
                throw new ArgumentException("AI faction snapshots must be unique.", nameof(factions));
            Factions = new ReadOnlyCollection<AiFactionSaveState>(values);
        }
        public long WorldSeed { get; }
        public long LastProcessedTick { get; }
        public IReadOnlyList<AiFactionSaveState> Factions { get; }
    }
}
