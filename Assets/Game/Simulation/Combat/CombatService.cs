using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Simulation.AI;
using Game.Simulation.Health;
using Game.Simulation.Tiers;

namespace Game.Simulation.Combat
{
    public interface ICombatPositionProvider
    {
        bool TryGetPosition(StableEntityId characterId, out WorldPosition position);
        void MoveTo(StableEntityId characterId, WorldPosition target, float stoppingDistance);
        void Stop(StableEntityId characterId);
    }

    public readonly struct CombatStats
    {
        public CombatStats(int attackPower, int defense, long attackIntervalTicks, int rangeMillimetres,
            int movementPerThousand, int moraleInfluence)
        {
            if (attackPower < 0 || defense < 0 || attackIntervalTicks <= 0 || rangeMillimetres <= 0 ||
                movementPerThousand < 0 || movementPerThousand > 2000 || moraleInfluence < 0)
                throw new ArgumentOutOfRangeException(nameof(attackPower));
            AttackPower = attackPower;
            Defense = defense;
            AttackIntervalTicks = attackIntervalTicks;
            RangeMillimetres = rangeMillimetres;
            MovementPerThousand = movementPerThousand;
            MoraleInfluence = moraleInfluence;
        }

        public int AttackPower { get; }
        public int Defense { get; }
        public long AttackIntervalTicks { get; }
        public int RangeMillimetres { get; }
        public float RangeMetres => RangeMillimetres / 1000f;
        public int MovementPerThousand { get; }
        public int MoraleInfluence { get; }
    }

    /// <summary>Pure parameters and calculations shared by physical and abstract battle resolution.</summary>
    public static class CombatRules
    {
        public const int BaselineMorale = 1000;
        public const int BreakThreshold = 300;
        public const int AllyCasualtyMoraleLoss = 260;
        public const int SevereInjuryMoraleLoss = 350;
        public static readonly TimeSpan RetreatDuration = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan AbstractStep = TimeSpan.FromMinutes(1);

        public static int CalculateImpact(CombatStats attacker, CombatStats defender) =>
            Math.Max(1, attacker.AttackPower - defender.Defense / 2);

        public static HealthConditionId SelectInjury(int impact, HealthConditionId minimum)
        {
            var computed = impact >= 500 ? "serious_wound" : impact >= 350 ? "broken_bone" :
                impact >= 220 ? "sprain" : "minor_cut";
            var order = new[] { "minor_cut", "sprain", "broken_bone", "serious_wound" };
            var minimumIndex = Array.IndexOf(order, minimum.Value);
            var computedIndex = Array.IndexOf(order, computed);
            return new HealthConditionId(order[Math.Max(minimumIndex, computedIndex)]);
        }
    }

    public sealed class CombatStatQuery
    {
        private static readonly SkillId CombatSkill = new SkillId("combat");
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly CombatWeaponCatalog weapons;

        public CombatStatQuery(CharacterRegistry characterRegistry, ICharacterCapabilityProvider capabilityProvider,
            CombatWeaponCatalog weaponCatalog)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            weapons = weaponCatalog ?? throw new ArgumentNullException(nameof(weaponCatalog));
        }

        public CombatStats Get(StableEntityId characterId, CombatWeaponId weaponId)
        {
            var character = characters.Get(characterId);
            var capability = capabilities.GetCapabilities(characterId);
            var weapon = weapons.Get(weaponId);
            var skill = character.TryGetSkill(CombatSkill, out var state) ? state.Level.Value : 0;
            var strength = character.Attributes.PhysicalStrength.Value;
            var attack = (100 + strength * 3 + skill * 2 + weapon.AttackBonus) * capability.CombatPerThousand / 1000;
            var defense = (100 + strength * 2 + skill + weapon.DefenseBonus) * capability.CombatPerThousand / 1000;
            return new CombatStats(attack, defense, weapon.AttackIntervalTicks, weapon.RangeMillimetres,
                capability.MovementPerThousand, 100 + character.Influence.Value / 5);
        }
    }

    public sealed class CombatTargetingService
    {
        private readonly CharacterRegistry characters;
        private readonly ICharacterCapabilityProvider capabilities;
        private readonly ICombatPositionProvider positions;

        public CombatTargetingService(CharacterRegistry characterRegistry,
            ICharacterCapabilityProvider capabilityProvider, ICombatPositionProvider positionProvider)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            capabilities = capabilityProvider ?? throw new ArgumentNullException(nameof(capabilityProvider));
            positions = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        }

        public StableEntityId? Select(CombatantCombatState attacker,
            IEnumerable<CombatantCombatState> combatants)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (combatants == null) throw new ArgumentNullException(nameof(combatants));
            var candidates = combatants.Where(x => x.SideId != attacker.SideId && IsValidTarget(x)).ToArray();
            if (attacker.AssignedTargetId.HasValue &&
                candidates.Any(x => x.CharacterId == attacker.AssignedTargetId.Value))
                return attacker.AssignedTargetId;
            if (!positions.TryGetPosition(attacker.CharacterId, out var origin)) return null;
            return candidates.Where(x => positions.TryGetPosition(x.CharacterId, out _))
                .OrderBy(x =>
                {
                    positions.TryGetPosition(x.CharacterId, out var target);
                    return origin.DistanceSquared(target);
                })
                .ThenBy(x => x.CharacterId.ToString(), StringComparer.Ordinal)
                .Select(x => (StableEntityId?)x.CharacterId).FirstOrDefault();
        }

        public bool IsValidTarget(CombatantCombatState combatant)
        {
            if (combatant == null || combatant.State == CombatantState.Dead ||
                combatant.State == CombatantState.Incapacitated || combatant.State == CombatantState.Disengaged)
                return false;
            return characters.TryGet(combatant.CharacterId, out var character) &&
                character.LifeState == CharacterLifeState.Alive && capabilities.GetCapabilities(combatant.CharacterId).CanFight;
        }
    }

    /// <summary>
    /// Canonical U20/U23 cross-mode combat authority. It is engine independent, stable-ID based and batched.
    /// NavMesh is accessed only through ICombatPositionProvider.
    /// </summary>
    public sealed class CombatService : ICharacterTierTransitionGuard, IExternalCharacterActivityOwner
    {
        public const int BaselineMorale = CombatRules.BaselineMorale;
        public const int BreakThreshold = CombatRules.BreakThreshold;
        public const int AllyCasualtyMoraleLoss = CombatRules.AllyCasualtyMoraleLoss;
        public const int SevereInjuryMoraleLoss = CombatRules.SevereInjuryMoraleLoss;
        public static readonly TimeSpan RetreatDuration = CombatRules.RetreatDuration;
        public static readonly TimeSpan AbstractStep = CombatRules.AbstractStep;
        private const float RetreatDistance = 8f;
        private const float FormationSpacing = 0.5f;

        private sealed class Encounter
        {
            public StableEntityId Id;
            public CombatSideId SideA;
            public CombatSideId SideB;
            public long StartTick;
            public long LastTick;
            public CombatEncounterStatus Status;
            public CombatOutcome Outcome;
            public BattleMode Mode;
            public MilitaryUnitId? SideAUnitId;
            public MilitaryUnitId? SideBUnitId;
            public readonly Dictionary<StableEntityId, CombatantCombatState> Combatants =
                new Dictionary<StableEntityId, CombatantCombatState>();
            public readonly List<CombatAttackRecordState> History = new List<CombatAttackRecordState>();
            public readonly List<BattleEventState> BattleHistory = new List<BattleEventState>();
            public ulong NextBattleEventSequence = 1;
        }

        private readonly CharacterRegistry characters;
        private readonly HealthAndFamilyService health;
        private readonly ICombatPositionProvider positions;
        private readonly ICharacterTierLookup tiers;
        private readonly CombatWeaponCatalog weapons;
        private readonly CombatStatQuery stats;
        private readonly CombatTargetingService targeting;
        private readonly Dictionary<StableEntityId, CombatSideId> roster = new Dictionary<StableEntityId, CombatSideId>();
        private readonly Dictionary<StableEntityId, Encounter> encounters = new Dictionary<StableEntityId, Encounter>();
        private readonly Dictionary<StableEntityId, StableEntityId> activeEncounterByCharacter =
            new Dictionary<StableEntityId, StableEntityId>();
        private readonly long worldSeed;
        private IExternalCharacterActivityOwner movementSuppression;
        private Func<StableEntityId, long, int> combatModifier;

        public CombatService(CharacterRegistry characterRegistry, HealthAndFamilyService healthService,
            ICombatPositionProvider positionProvider, long simulationWorldSeed,
            ICharacterTierLookup tierLookup = null, CombatWeaponCatalog weaponCatalog = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            health = healthService ?? throw new ArgumentNullException(nameof(healthService));
            positions = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
            tiers = tierLookup;
            weapons = weaponCatalog ?? CombatWeaponCatalog.CreateStoneAge();
            stats = new CombatStatQuery(characters, health, weapons);
            targeting = new CombatTargetingService(characters, health, positions);
            worldSeed = simulationWorldSeed;
        }

        public long WorldSeed => worldSeed;
        public long LastCalendarTick { get; private set; }
        public CombatWeaponCatalog Weapons => weapons;
        public IReadOnlyList<CombatEncounterState> Encounters => encounters.Values.OrderBy(x => x.Id)
            .Select(Capture).ToArray();

        public void BindMovementSuppression(IExternalCharacterActivityOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (movementSuppression != null && !ReferenceEquals(movementSuppression, owner))
                throw new InvalidOperationException("Combat movement suppression is already bound.");
            movementSuppression = owner;
        }

        public void BindCombatModifier(Func<StableEntityId, long, int> modifier)
        {
            if (modifier == null) throw new ArgumentNullException(nameof(modifier));
            if (combatModifier != null && combatModifier != modifier)
                throw new InvalidOperationException("Combat modifier is already bound.");
            combatModifier = modifier;
        }

        public void RegisterSide(StableEntityId characterId, CombatSideId sideId)
        {
            characters.Get(characterId);
            if (!sideId.IsValid) throw new ArgumentException("Combat side ID required.", nameof(sideId));
            if (roster.TryGetValue(characterId, out var existing) && existing != sideId)
                throw new InvalidOperationException("Character is already registered to another combat side.");
            roster[characterId] = sideId;
        }

        public bool TryGetSide(StableEntityId characterId, out CombatSideId sideId) =>
            roster.TryGetValue(characterId, out sideId);

        public IReadOnlyList<StableEntityId> GetSideMembers(CombatSideId sideId) => roster
            .Where(x => x.Value == sideId).Select(x => x.Key)
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();

        public bool AreHostile(StableEntityId first, StableEntityId second) =>
            roster.TryGetValue(first, out var a) && roster.TryGetValue(second, out var b) && a != b;

        public bool IsEligible(StableEntityId characterId)
        {
            if (!IsAbstractEligible(characterId)) return false;
            return tiers == null || tiers.TryGetTier(characterId, out var tier) && tier == CharacterSimulationTier.Tier1;
        }

        public bool IsAbstractEligible(StableEntityId characterId) =>
            roster.ContainsKey(characterId) && characters.TryGet(characterId, out var character) &&
            character.LifeState == CharacterLifeState.Alive && health.GetCapabilities(characterId).CanFight;

        public CombatEncounterState CreateEncounter(StableEntityId encounterId, CombatSideId sideA,
            CombatSideId sideB, IEnumerable<StableEntityId> sideACharacters,
            IEnumerable<StableEntityId> sideBCharacters, long startCalendarTick,
            Func<StableEntityId, CombatWeaponId> weaponSelector = null, BattleMode mode = BattleMode.Physical,
            MilitaryUnitId? sideAUnitId = null, MilitaryUnitId? sideBUnitId = null)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter ID required.", nameof(encounterId));
            if (!sideA.IsValid || !sideB.IsValid || sideA == sideB) throw new ArgumentException("Two distinct hostile sides are required.");
            if (startCalendarTick < LastCalendarTick) throw new ArgumentOutOfRangeException(nameof(startCalendarTick));
            if (!Enum.IsDefined(typeof(BattleMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (sideAUnitId.HasValue && sideBUnitId.HasValue && sideAUnitId.Value == sideBUnitId.Value)
                throw new ArgumentException("Battle units must be distinct.");
            if (encounters.ContainsKey(encounterId)) throw new InvalidOperationException("Encounter ID is already registered.");
            var a = OrderedDistinct(sideACharacters);
            var b = OrderedDistinct(sideBCharacters);
            if (a.Length == 0 || b.Length == 0) throw new ArgumentException("Both sides require participants.");
            var all = a.Concat(b).ToArray();
            if (all.Distinct().Count() != all.Length) throw new ArgumentException("A participant cannot be on both sides.");
            foreach (var id in a) ValidateParticipant(id, sideA, mode);
            foreach (var id in b) ValidateParticipant(id, sideB, mode);

            var encounter = new Encounter
            {
                Id = encounterId,
                SideA = sideA,
                SideB = sideB,
                StartTick = startCalendarTick,
                LastTick = startCalendarTick,
                Status = CombatEncounterStatus.Active,
                Outcome = CombatOutcome.None,
                Mode = mode,
                SideAUnitId = sideAUnitId,
                SideBUnitId = sideBUnitId
            };
            AddBattleEvent(encounter, startCalendarTick, BattleEventKind.Started, mode: mode);
            AddCombatants(encounter, a, sideA, b, weaponSelector);
            AddCombatants(encounter, b, sideB, a, weaponSelector);
            encounters.Add(encounterId, encounter);
            foreach (var id in all) activeEncounterByCharacter.Add(id, encounterId);
            LastCalendarTick = Math.Max(LastCalendarTick, startCalendarTick);
            return Capture(encounter);
        }

        public bool AssignTarget(StableEntityId attackerId, StableEntityId targetId)
        {
            if (!activeEncounterByCharacter.TryGetValue(attackerId, out var encounterId) ||
                !activeEncounterByCharacter.TryGetValue(targetId, out var targetEncounterId) || encounterId != targetEncounterId)
                return false;
            var encounter = encounters[encounterId];
            var attacker = encounter.Combatants[attackerId];
            var target = encounter.Combatants[targetId];
            if (attacker.SideId == target.SideId || !targeting.IsValidTarget(target)) return false;
            encounter.Combatants[attackerId] = attacker.With(assignedTargetId: targetId, targetId: targetId);
            return true;
        }

        public ManualCombatAttackStatus TryManualAttack(StableEntityId attackerId, StableEntityId targetId,
            long calendarTick, long biologicalTick)
        {
            if (calendarTick < LastCalendarTick) return ManualCombatAttackStatus.Invalid;
            if (!activeEncounterByCharacter.TryGetValue(attackerId, out var encounterId) ||
                !activeEncounterByCharacter.TryGetValue(targetId, out var targetEncounterId) || encounterId != targetEncounterId)
                return ManualCombatAttackStatus.Invalid;
            var encounter = encounters[encounterId];
            if (encounter.Status != CombatEncounterStatus.Active || encounter.Mode != BattleMode.Physical)
                return ManualCombatAttackStatus.Invalid;
            var attacker = RefreshEligibility(encounter, encounter.Combatants[attackerId]);
            var target = RefreshEligibility(encounter, encounter.Combatants[targetId]);
            if (!IsActiveCombatant(attacker) || !targeting.IsValidTarget(target) || attacker.SideId == target.SideId)
                return ManualCombatAttackStatus.Invalid;
            encounter.Combatants[attackerId] = attacker.With(assignedTargetId: targetId, targetId: targetId,
                state: CombatantState.Attacking);
            if (!positions.TryGetPosition(attackerId, out var attackerPosition) ||
                !positions.TryGetPosition(targetId, out var targetPosition)) return ManualCombatAttackStatus.Invalid;
            var attackerStats = EffectiveStats(attackerId, attacker.WeaponId, calendarTick);
            if (attackerPosition.DistanceSquared(targetPosition) > attackerStats.RangeMetres * attackerStats.RangeMetres)
                return ManualCombatAttackStatus.OutOfRange;
            if (calendarTick < attacker.NextAttackTick) return ManualCombatAttackStatus.Cooldown;
            ResolveAttack(encounter, attacker, targetId, attackerStats, calendarTick, biologicalTick);
            encounter.LastTick = Math.Max(encounter.LastTick, calendarTick);
            LastCalendarTick = Math.Max(LastCalendarTick, calendarTick);
            ResolveOutcome(encounter);
            return ManualCombatAttackStatus.Hit;
        }

        public void AdvanceTo(long calendarTick, long biologicalTick)
        {
            if (calendarTick < LastCalendarTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (biologicalTick < health.LastBiologicalTick) throw new ArgumentOutOfRangeException(nameof(biologicalTick));
            foreach (var encounter in encounters.Values.Where(x => x.Status == CombatEncounterStatus.Active)
                         .OrderBy(x => x.Id).ToArray())
            {
                if (encounter.Mode == BattleMode.Physical)
                {
                    AdvancePhysicalEncounter(encounter, calendarTick, biologicalTick);
                    encounter.LastTick = calendarTick;
                }
                else
                {
                    AdvanceAbstractEncounter(encounter, calendarTick, biologicalTick);
                }
            }
            LastCalendarTick = calendarTick;
        }

        public bool CancelEncounter(StableEntityId encounterId)
        {
            if (!encounters.TryGetValue(encounterId, out var encounter) || encounter.Status != CombatEncounterStatus.Active)
                return false;
            encounter.Status = CombatEncounterStatus.Cancelled;
            encounter.Outcome = CombatOutcome.Cancelled;
            AddBattleEvent(encounter, encounter.LastTick, BattleEventKind.Finished);
            Release(encounter);
            return true;
        }

        public bool OrderRetreat(StableEntityId characterId, long calendarTick)
        {
            if (!activeEncounterByCharacter.TryGetValue(characterId, out var encounterId)) return false;
            var encounter = encounters[encounterId];
            var combatant = encounter.Combatants[characterId];
            if (combatant.State == CombatantState.Dead || combatant.State == CombatantState.Incapacitated ||
                combatant.State == CombatantState.Disengaged) return false;
            encounter.Combatants[characterId] = combatant.With(state: CombatantState.Breaking, morale: 0,
                clearTarget: true, nextAttackTick: checked(calendarTick + RetreatDuration.Ticks));
            return true;
        }

        public bool IsPinnedToTier1(StableEntityId characterId) =>
            activeEncounterByCharacter.TryGetValue(characterId, out var encounterId) &&
            encounters[encounterId].Mode == BattleMode.Physical;

        public bool CanTransition(StableEntityId characterId, CharacterSimulationTier targetTier) =>
            targetTier == CharacterSimulationTier.Tier1 || !IsPinnedToTier1(characterId);

        public bool IsExternallyControlled(StableEntityId characterId) => IsPinnedToTier1(characterId);

        public bool TryGetCombatant(StableEntityId characterId, out CombatantCombatState state)
        {
            state = null;
            return activeEncounterByCharacter.TryGetValue(characterId, out var encounterId) &&
                   encounters[encounterId].Combatants.TryGetValue(characterId, out state);
        }

        public bool TryGetLatestCombatant(StableEntityId characterId, out CombatantCombatState state)
        {
            if (TryGetCombatant(characterId, out state)) return true;
            state = encounters.Values.OrderByDescending(x => x.StartTick).ThenByDescending(x => x.Id)
                .Select(x => x.Combatants.TryGetValue(characterId, out var value) ? value : null)
                .FirstOrDefault(x => x != null);
            return state != null;
        }

        public CombatStats GetStats(StableEntityId characterId, CombatWeaponId weaponId) => stats.Get(characterId, weaponId);

        public bool TryGetEncounter(StableEntityId battleId, out CombatEncounterState state)
        {
            state = null;
            if (!encounters.TryGetValue(battleId, out var encounter)) return false;
            state = Capture(encounter);
            return true;
        }

        public CombatEncounterState SetBattleMode(StableEntityId battleId, BattleMode mode, long calendarTick,
            bool recordTransition = true)
        {
            if (!Enum.IsDefined(typeof(BattleMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!encounters.TryGetValue(battleId, out var encounter))
                throw new KeyNotFoundException("Unknown battle: " + battleId);
            if (encounter.Status != CombatEncounterStatus.Active)
                throw new InvalidOperationException("A terminal battle cannot change representation mode.");
            if (calendarTick < encounter.LastTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (encounter.Mode == mode) return Capture(encounter);
            if (mode == BattleMode.Physical)
                foreach (var combatant in encounter.Combatants.Values.Where(IsActiveCombatant))
                    ValidatePhysicalPresence(combatant.CharacterId);
            encounter.Mode = mode;
            if (recordTransition)
                AddBattleEvent(encounter, calendarTick, BattleEventKind.ModeChanged, mode: mode);
            return Capture(encounter);
        }

        public void RecordModeTransition(StableEntityId battleId, long calendarTick)
        {
            if (!encounters.TryGetValue(battleId, out var encounter))
                throw new KeyNotFoundException("Unknown battle: " + battleId);
            AddBattleEvent(encounter, calendarTick, BattleEventKind.ModeChanged, mode: encounter.Mode);
        }

        public void RecordCaptainDeath(StableEntityId battleId, StableEntityId captainId,
            CombatSideId sideId, long calendarTick)
        {
            if (!encounters.TryGetValue(battleId, out var encounter))
                throw new KeyNotFoundException("Unknown battle: " + battleId);
            if (encounter.BattleHistory.Any(x => x.Kind == BattleEventKind.CaptainDeath &&
                x.CharacterId == captainId)) return;
            AddBattleEvent(encounter, calendarTick, BattleEventKind.CaptainDeath, captainId, sideId);
        }

        public CombatSaveState CaptureState() => new CombatSaveState(worldSeed, LastCalendarTick, Encounters);

        public void RestoreState(CombatSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.WorldSeed != worldSeed) throw new InvalidOperationException("Combat save world seed does not match this session.");
            var restored = new Dictionary<StableEntityId, Encounter>();
            var active = new Dictionary<StableEntityId, StableEntityId>();
            var restoredRoster = new Dictionary<StableEntityId, CombatSideId>();
            foreach (var saved in state.Encounters)
            {
                var encounter = Restore(saved);
                if (!restored.TryAdd(encounter.Id, encounter)) throw new ArgumentException("Duplicate saved encounter ID.");
                foreach (var combatant in encounter.Combatants.Values)
                {
                    characters.Get(combatant.CharacterId);
                    weapons.Get(combatant.WeaponId);
                    if (combatant.TargetId.HasValue && !encounter.Combatants.ContainsKey(combatant.TargetId.Value))
                        throw new ArgumentException("Saved combat target is outside the encounter.");
                    if (restoredRoster.TryGetValue(combatant.CharacterId, out var known) && known != combatant.SideId)
                        throw new ArgumentException("Saved character has conflicting combat sides.");
                    restoredRoster[combatant.CharacterId] = combatant.SideId;
                    if (encounter.Status == CombatEncounterStatus.Active && !active.TryAdd(combatant.CharacterId, encounter.Id))
                        throw new ArgumentException("Saved character is active in multiple encounters.");
                }
            }
            encounters.Clear(); foreach (var pair in restored) encounters.Add(pair.Key, pair.Value);
            activeEncounterByCharacter.Clear(); foreach (var pair in active) activeEncounterByCharacter.Add(pair.Key, pair.Value);
            roster.Clear(); foreach (var pair in restoredRoster) roster.Add(pair.Key, pair.Value);
            LastCalendarTick = state.LastCalendarTick;
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, unchecked((ulong)worldSeed));
            Add(ref hash, unchecked((ulong)LastCalendarTick));
            foreach (var encounter in Encounters)
            {
                AddId(ref hash, encounter.EncounterId); AddText(ref hash, encounter.SideA.Value); AddText(ref hash, encounter.SideB.Value);
                Add(ref hash, unchecked((ulong)encounter.StartCalendarTick));
                Add(ref hash, unchecked((ulong)encounter.LastProcessedCalendarTick));
                Add(ref hash, (ulong)encounter.Status); Add(ref hash, (ulong)encounter.Outcome);
                foreach (var combatant in encounter.Combatants)
                {
                    AddId(ref hash, combatant.CharacterId); AddText(ref hash, combatant.SideId.Value);
                    AddText(ref hash, combatant.WeaponId.Value);
                    Add(ref hash, (ulong)combatant.State); Add(ref hash, combatant.AttackSequence);
                    Add(ref hash, unchecked((ulong)combatant.NextAttackTick)); Add(ref hash, (ulong)combatant.Morale);
                    if (combatant.TargetId.HasValue) AddId(ref hash, combatant.TargetId.Value);
                    if (combatant.AssignedTargetId.HasValue) AddId(ref hash, combatant.AssignedTargetId.Value);
                    AddText(ref hash, combatant.RecentInjury);
                    Add(ref hash, combatant.CasualtyPenaltyApplied ? 1UL : 0UL);
                }
                foreach (var attack in encounter.AttackHistory)
                {
                    AddId(ref hash, attack.AttackerId); AddId(ref hash, attack.DefenderId);
                    Add(ref hash, attack.AttackSequence); AddText(ref hash, attack.InjuryId.ToString());
                    Add(ref hash, unchecked((ulong)attack.CalendarTick));
                    Add(ref hash, attack.Applied ? 1UL : 0UL); Add(ref hash, attack.Fatal ? 1UL : 0UL);
                }
                Add(ref hash, (ulong)encounter.Mode);
                if (encounter.SideAUnitId.HasValue) AddId(ref hash, encounter.SideAUnitId.Value.Value);
                if (encounter.SideBUnitId.HasValue) AddId(ref hash, encounter.SideBUnitId.Value.Value);
                foreach (var battleEvent in encounter.BattleHistory)
                {
                    Add(ref hash, battleEvent.Sequence); Add(ref hash, unchecked((ulong)battleEvent.CalendarTick));
                    Add(ref hash, (ulong)battleEvent.Kind);
                    if (battleEvent.CharacterId.HasValue) AddId(ref hash, battleEvent.CharacterId.Value);
                    if (battleEvent.SideId.HasValue) AddText(ref hash, battleEvent.SideId.Value.Value);
                    if (battleEvent.Mode.HasValue) Add(ref hash, (ulong)battleEvent.Mode.Value);
                }
            }
            return hash;
        }

        private void AdvancePhysicalEncounter(Encounter encounter, long calendarTick, long biologicalTick)
        {
            encounter.LastTick = calendarTick;
            foreach (var id in encounter.Combatants.Keys.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray())
            {
                var combatant = RefreshEligibility(encounter, encounter.Combatants[id]);
                encounter.Combatants[id] = combatant;
                if (combatant.State == CombatantState.Dead || combatant.State == CombatantState.Incapacitated ||
                    combatant.State == CombatantState.Disengaged) continue;

                if (combatant.Morale < BreakThreshold && combatant.State != CombatantState.Retreating)
                {
                    combatant = combatant.With(state: CombatantState.Breaking, clearTarget: true,
                        nextAttackTick: checked(calendarTick + RetreatDuration.Ticks));
                    encounter.Combatants[id] = combatant;
                    RecordSideBroken(encounter, combatant.SideId, calendarTick);
                }
                if (combatant.State == CombatantState.Breaking)
                {
                    MoveToUnlessSuppressed(id, combatant.RetreatPoint, 0.5f);
                    combatant = combatant.With(state: CombatantState.Retreating);
                    encounter.Combatants[id] = combatant;
                    continue;
                }
                if (combatant.State == CombatantState.Retreating)
                {
                    MoveToUnlessSuppressed(id, combatant.RetreatPoint, 0.5f);
                    if (calendarTick >= combatant.NextAttackTick || IsWithin(id, combatant.RetreatPoint, 0.75f))
                    {
                        StopUnlessSuppressed(id);
                        encounter.Combatants[id] = combatant.With(state: CombatantState.Disengaged, clearTarget: true);
                    }
                    continue;
                }

                var targetId = targeting.Select(combatant, encounter.Combatants.Values);
                if (!targetId.HasValue)
                {
                    StopUnlessSuppressed(id);
                    encounter.Combatants[id] = combatant.With(state: CombatantState.Fighting, clearTarget: true);
                    continue;
                }
                combatant = combatant.With(targetId: targetId.Value);
                encounter.Combatants[id] = combatant;
                var attackerStats = EffectiveStats(id, combatant.WeaponId, calendarTick);
                if (!positions.TryGetPosition(id, out var attackerPosition) ||
                    !positions.TryGetPosition(targetId.Value, out var targetPosition)) continue;
                var range = attackerStats.RangeMetres;
                var distanceSquared = attackerPosition.DistanceSquared(targetPosition);
                if (distanceSquared > range * range)
                {
                    var destination = distanceSquared > (range + 2f) * (range + 2f)
                        ? FormationApproach(encounter, combatant, targetPosition) : targetPosition;
                    MoveToUnlessSuppressed(id, destination, Math.Max(0.3f, range * 0.7f));
                    encounter.Combatants[id] = combatant.With(state: CombatantState.Approaching);
                    continue;
                }
                StopUnlessSuppressed(id);
                if (IsMovementSuppressed(id))
                {
                    encounter.Combatants[id] = combatant.With(state: CombatantState.Attacking);
                    continue;
                }
                if (calendarTick < combatant.NextAttackTick)
                {
                    encounter.Combatants[id] = combatant.With(state: CombatantState.Attacking);
                    continue;
                }
                ResolveAttack(encounter, combatant, targetId.Value, attackerStats, calendarTick, biologicalTick);
            }
            ResolveOutcome(encounter);
        }

        private void AdvanceAbstractEncounter(Encounter encounter, long calendarTick, long biologicalTick)
        {
            var nextStep = checked(encounter.LastTick + AbstractStep.Ticks);
            while (nextStep <= calendarTick && encounter.Status == CombatEncounterStatus.Active)
            {
                foreach (var id in encounter.Combatants.Keys.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray())
                {
                    var combatant = RefreshEligibility(encounter, encounter.Combatants[id]);
                    encounter.Combatants[id] = combatant;
                    if (!IsActiveCombatant(combatant)) continue;

                    if (combatant.Morale < BreakThreshold && combatant.State != CombatantState.Retreating)
                    {
                        combatant = combatant.With(state: CombatantState.Breaking, clearTarget: true,
                            nextAttackTick: checked(nextStep + RetreatDuration.Ticks));
                        encounter.Combatants[id] = combatant;
                        RecordSideBroken(encounter, combatant.SideId, nextStep);
                    }
                    if (combatant.State == CombatantState.Breaking)
                    {
                        encounter.Combatants[id] = combatant.With(state: CombatantState.Retreating);
                        continue;
                    }
                    if (combatant.State == CombatantState.Retreating)
                    {
                        if (nextStep >= combatant.NextAttackTick)
                            encounter.Combatants[id] = combatant.With(state: CombatantState.Disengaged,
                                clearTarget: true);
                        continue;
                    }

                    var targetId = SelectAbstractTarget(encounter, combatant);
                    if (!targetId.HasValue)
                    {
                        encounter.Combatants[id] = combatant.With(state: CombatantState.Fighting,
                            clearTarget: true);
                        continue;
                    }
                    combatant = combatant.With(state: CombatantState.Attacking, targetId: targetId.Value);
                    encounter.Combatants[id] = combatant;
                    if (nextStep < combatant.NextAttackTick) continue;
                    ResolveAttack(encounter, combatant, targetId.Value,
                        EffectiveStats(id, combatant.WeaponId, nextStep), nextStep, biologicalTick);
                }
                encounter.LastTick = nextStep;
                ResolveOutcome(encounter);
                nextStep = checked(nextStep + AbstractStep.Ticks);
            }
        }

        private StableEntityId? SelectAbstractTarget(Encounter encounter, CombatantCombatState attacker)
        {
            var candidates = encounter.Combatants.Values.Where(x => x.SideId != attacker.SideId &&
                IsValidAbstractTarget(x)).OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            if (candidates.Length == 0) return null;
            if (attacker.AssignedTargetId.HasValue &&
                candidates.Any(x => x.CharacterId == attacker.AssignedTargetId.Value))
                return attacker.AssignedTargetId;
            if (attacker.TargetId.HasValue && candidates.Any(x => x.CharacterId == attacker.TargetId.Value))
                return attacker.TargetId;
            // Opposing stable formation edges are the position-free equivalent of U20's nearest-then-ID rule.
            return attacker.SideId == encounter.SideA
                ? candidates[0].CharacterId
                : candidates[candidates.Length - 1].CharacterId;
        }

        private bool IsValidAbstractTarget(CombatantCombatState combatant) =>
            combatant != null && combatant.State != CombatantState.Dead &&
            combatant.State != CombatantState.Incapacitated && combatant.State != CombatantState.Disengaged &&
            characters.TryGet(combatant.CharacterId, out var character) &&
            character.LifeState == CharacterLifeState.Alive && health.GetCapabilities(combatant.CharacterId).CanFight;

        private void ResolveAttack(Encounter encounter, CombatantCombatState attacker, StableEntityId defenderId,
            CombatStats attackerStats, long calendarTick, long biologicalTick)
        {
            var defender = encounter.Combatants[defenderId];
            var defenderStats = stats.Get(defenderId, defender.WeaponId);
            var impact = CombatRules.CalculateImpact(attackerStats, defenderStats);
            var requested = CombatRules.SelectInjury(impact, weapons.Get(attacker.WeaponId).MinimumInjury);
            var application = health.ApplyCombatInjury(defenderId, requested, calendarTick, biologicalTick);
            var sequence = checked(attacker.AttackSequence + 1UL);
            encounter.Combatants[attacker.CharacterId] = attacker.With(state: CombatantState.Attacking,
                nextAttackTick: checked(calendarTick + attackerStats.AttackIntervalTicks), attackSequence: sequence,
                recentInjury: application.ConditionId.ToString());
            encounter.History.Add(new CombatAttackRecordState(attacker.CharacterId, defenderId, sequence,
                calendarTick, application.ConditionId, application.Applied, application.Fatal));

            var targetMorale = Math.Max(0, defender.Morale -
                (application.Incapacitated || application.Fatal ? SevereInjuryMoraleLoss : SevereInjuryMoraleLoss / 3));
            var targetState = application.Fatal ? CombatantState.Dead :
                application.Incapacitated ? CombatantState.Incapacitated : CombatantState.Fighting;
            defender = defender.With(state: targetState, morale: targetMorale,
                recentInjury: application.ConditionId.ToString(), clearTarget: application.Incapacitated || application.Fatal);
            encounter.Combatants[defenderId] = defender;
            if ((application.Fatal || application.Incapacitated) && !defender.CasualtyPenaltyApplied)
                ApplyCasualtyMorale(encounter, defenderId);
        }

        private CombatantCombatState RefreshEligibility(Encounter encounter, CombatantCombatState combatant)
        {
            var character = characters.Get(combatant.CharacterId);
            var capability = health.GetCapabilities(combatant.CharacterId);
            if (character.LifeState == CharacterLifeState.Dead)
            {
                var updated = combatant.With(state: CombatantState.Dead, clearTarget: true);
                encounter.Combatants[combatant.CharacterId] = updated;
                if (!updated.CasualtyPenaltyApplied) ApplyCasualtyMorale(encounter, updated.CharacterId);
                return encounter.Combatants[combatant.CharacterId];
            }
            if (!capability.CanFight)
            {
                var updated = combatant.With(state: CombatantState.Incapacitated, clearTarget: true);
                encounter.Combatants[combatant.CharacterId] = updated;
                if (!updated.CasualtyPenaltyApplied) ApplyCasualtyMorale(encounter, updated.CharacterId);
                return encounter.Combatants[combatant.CharacterId];
            }
            return combatant;
        }

        private void ApplyCasualtyMorale(Encounter encounter, StableEntityId casualtyId)
        {
            var casualty = encounter.Combatants[casualtyId];
            if (casualty.CasualtyPenaltyApplied) return;
            encounter.Combatants[casualtyId] = casualty.With(casualtyPenaltyApplied: true);
            foreach (var ally in encounter.Combatants.Values.Where(x => x.SideId == casualty.SideId &&
                         x.CharacterId != casualtyId && x.State != CombatantState.Dead &&
                         x.State != CombatantState.Incapacitated && x.State != CombatantState.Disengaged).ToArray())
                encounter.Combatants[ally.CharacterId] = ally.With(morale: Math.Max(0, ally.Morale - AllyCasualtyMoraleLoss));
        }

        private void ResolveOutcome(Encounter encounter)
        {
            if (HasPendingRetreat(encounter, encounter.SideA) || HasPendingRetreat(encounter, encounter.SideB)) return;
            var a = HasActiveFighter(encounter, encounter.SideA);
            var b = HasActiveFighter(encounter, encounter.SideB);
            if (a && b) return;
            encounter.Status = CombatEncounterStatus.Finished;
            encounter.Outcome = a ? CombatOutcome.SideAVictory : b ? CombatOutcome.SideBVictory : CombatOutcome.MutualDisengagement;
            AddBattleEvent(encounter, encounter.LastTick, BattleEventKind.Finished);
            Release(encounter);
        }

        private static bool HasActiveFighter(Encounter encounter, CombatSideId side) => encounter.Combatants.Values.Any(x =>
            x.SideId == side && x.State != CombatantState.Dead && x.State != CombatantState.Incapacitated &&
            x.State != CombatantState.Disengaged && x.State != CombatantState.Breaking && x.State != CombatantState.Retreating);

        private static bool HasPendingRetreat(Encounter encounter, CombatSideId side) => encounter.Combatants.Values.Any(x =>
            x.SideId == side && (x.State == CombatantState.Breaking || x.State == CombatantState.Retreating));

        private void Release(Encounter encounter)
        {
            foreach (var combatant in encounter.Combatants.Values)
            {
                activeEncounterByCharacter.Remove(combatant.CharacterId);
                StopUnlessSuppressed(combatant.CharacterId);
            }
        }

        private void ValidateParticipant(StableEntityId id, CombatSideId side, BattleMode mode)
        {
            if (!roster.TryGetValue(id, out var registered) || registered != side)
                throw new InvalidOperationException("Combat participant must belong to the requested side.");
            if (mode == BattleMode.Physical && !IsEligible(id))
                throw new InvalidOperationException("Dead, child, incapacitated or non-Tier1 character cannot enter physical combat.");
            if (mode == BattleMode.Abstract && !IsAbstractEligible(id))
                throw new InvalidOperationException("Dead, child or incapacitated character cannot enter abstract combat.");
            if (activeEncounterByCharacter.ContainsKey(id)) throw new InvalidOperationException("Character is already in an active encounter.");
            if (mode == BattleMode.Physical && !positions.TryGetPosition(id, out _))
                throw new InvalidOperationException("Physical combat requires a local position.");
        }

        private void ValidatePhysicalPresence(StableEntityId id)
        {
            if (tiers != null && (!tiers.TryGetTier(id, out var tier) || tier != CharacterSimulationTier.Tier1))
                throw new InvalidOperationException("Physical battle requires every active combatant in Tier1.");
            if (!positions.TryGetPosition(id, out _))
                throw new InvalidOperationException("Physical battle requires a local position for every active combatant.");
        }

        private void AddCombatants(Encounter encounter, StableEntityId[] own, CombatSideId side,
            StableEntityId[] enemies, Func<StableEntityId, CombatWeaponId> weaponSelector)
        {
            var ownCenter = Center(own);
            var enemyCenter = Center(enemies);
            var dx = ownCenter.X - enemyCenter.X;
            var dz = ownCenter.Z - enemyCenter.Z;
            var length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.001d) { dx = side == encounter.SideA ? -1f : 1f; dz = 0f; length = 1d; }
            var retreat = new WorldPosition(ownCenter.X + (float)(dx / length * RetreatDistance), ownCenter.Y,
                ownCenter.Z + (float)(dz / length * RetreatDistance));
            foreach (var id in own)
            {
                var weaponId = weaponSelector == null ? new CombatWeaponId("unarmed") : weaponSelector(id);
                weapons.Get(weaponId);
                encounter.Combatants.Add(id, new CombatantCombatState(id, side, weaponId,
                    CombatantState.Fighting, null, null, encounter.StartTick, 0, BaselineMorale,
                    retreat, string.Empty, false));
            }
        }

        private WorldPosition Center(IEnumerable<StableEntityId> ids)
        {
            double x = 0, y = 0, z = 0;
            var count = 0;
            foreach (var id in ids)
            {
                if (!positions.TryGetPosition(id, out var value)) continue;
                x += value.X; y += value.Y; z += value.Z; count++;
            }
            if (count == 0) return default;
            return new WorldPosition((float)(x / count), (float)(y / count), (float)(z / count));
        }

        private static WorldPosition FormationApproach(Encounter encounter, CombatantCombatState combatant,
            WorldPosition target)
        {
            var ordered = encounter.Combatants.Values.Where(x => x.SideId == combatant.SideId)
                .OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            var index = Array.FindIndex(ordered, x => x.CharacterId == combatant.CharacterId);
            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(ordered.Length)));
            var row = index / columns;
            var column = index % columns;
            var rowCount = Math.Min(columns, ordered.Length - row * columns);
            return new WorldPosition(target.X + (column - (rowCount - 1) * 0.5f) * FormationSpacing,
                target.Y, target.Z + (row + 1) * FormationSpacing);
        }

        private bool IsWithin(StableEntityId id, WorldPosition target, float distance) =>
            positions.TryGetPosition(id, out var position) && position.DistanceSquared(target) <= distance * distance;

        private bool IsMovementSuppressed(StableEntityId characterId) =>
            movementSuppression != null && movementSuppression.IsExternallyControlled(characterId);

        private CombatStats EffectiveStats(StableEntityId characterId, CombatWeaponId weaponId, long calendarTick)
        {
            var value = stats.Get(characterId, weaponId);
            var modifier = combatModifier?.Invoke(characterId, calendarTick) ?? 1000;
            if (modifier < 1 || modifier > 1000)
                throw new InvalidOperationException("Combat modifier must be in 1..1000 per-thousand.");
            return modifier == 1000 ? value : new CombatStats(
                Math.Max(0, value.AttackPower * modifier / 1000), value.Defense,
                value.AttackIntervalTicks, value.RangeMillimetres, value.MovementPerThousand,
                value.MoraleInfluence);
        }

        private void MoveToUnlessSuppressed(StableEntityId characterId, WorldPosition target, float stoppingDistance)
        {
            if (!IsMovementSuppressed(characterId)) positions.MoveTo(characterId, target, stoppingDistance);
        }

        private void StopUnlessSuppressed(StableEntityId characterId)
        {
            if (!IsMovementSuppressed(characterId)) positions.Stop(characterId);
        }

        private static StableEntityId[] OrderedDistinct(IEnumerable<StableEntityId> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var result = ids.ToArray();
            if (result.Any(x => !x.IsValid)) throw new ArgumentException("Participant IDs must be valid.", nameof(ids));
            if (result.Distinct().Count() != result.Length) throw new ArgumentException("Duplicate participant.", nameof(ids));
            return result.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        }

        private static CombatEncounterState Capture(Encounter encounter) => new CombatEncounterState(encounter.Id,
            encounter.SideA, encounter.SideB, encounter.StartTick, encounter.LastTick, encounter.Status,
            encounter.Outcome, encounter.Combatants.Values, encounter.History, encounter.Mode,
            encounter.SideAUnitId, encounter.SideBUnitId, encounter.BattleHistory);

        private static Encounter Restore(CombatEncounterState state)
        {
            var result = new Encounter
            {
                Id = state.EncounterId, SideA = state.SideA, SideB = state.SideB,
                StartTick = state.StartCalendarTick, LastTick = state.LastProcessedCalendarTick,
                Status = state.Status, Outcome = state.Outcome, Mode = state.Mode,
                SideAUnitId = state.SideAUnitId, SideBUnitId = state.SideBUnitId,
                NextBattleEventSequence = state.BattleHistory.Count == 0 ? 1 :
                    checked(state.BattleHistory.Max(x => x.Sequence) + 1UL)
            };
            foreach (var combatant in state.Combatants) result.Combatants.Add(combatant.CharacterId, combatant);
            result.History.AddRange(state.AttackHistory);
            result.BattleHistory.AddRange(state.BattleHistory);
            return result;
        }

        private static bool IsActiveCombatant(CombatantCombatState value) => value.State != CombatantState.Dead &&
            value.State != CombatantState.Incapacitated && value.State != CombatantState.Disengaged;

        private static void AddBattleEvent(Encounter encounter, long calendarTick, BattleEventKind kind,
            StableEntityId? characterId = null, CombatSideId? sideId = null, BattleMode? mode = null) =>
            encounter.BattleHistory.Add(new BattleEventState(encounter.NextBattleEventSequence++, calendarTick,
                kind, characterId, sideId, mode));

        private static void RecordSideBroken(Encounter encounter, CombatSideId sideId, long calendarTick)
        {
            if (encounter.BattleHistory.Any(x => x.Kind == BattleEventKind.SideBroken && x.SideId == sideId)) return;
            AddBattleEvent(encounter, calendarTick, BattleEventKind.SideBroken, sideId: sideId);
        }

        private static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) { hash ^= (byte)(value >> shift); hash *= 1099511628211UL; }
        }
        private static void AddId(ref ulong hash, StableEntityId id)
        {
            foreach (var value in id.ToGuid().ToByteArray()) { hash ^= value; hash *= 1099511628211UL; }
        }
        private static void AddText(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) Add(ref hash, value[i]);
        }
    }

    public enum ManualCombatAttackStatus { Hit = 1, OutOfRange = 2, Cooldown = 3, Invalid = 4 }

    public enum CombatCommandStatus { Created = 1, Assigned = 2, InvalidSelection = 3, NotHostile = 4, Ineligible = 5 }

    public readonly struct CombatCommandResult
    {
        public CombatCommandResult(CombatCommandStatus status, StableEntityId? encounterId, int participantCount)
        {
            Status = status; EncounterId = encounterId; ParticipantCount = participantCount;
        }
        public CombatCommandStatus Status { get; }
        public StableEntityId? EncounterId { get; }
        public int ParticipantCount { get; }
    }

    public sealed class CombatCommandService
    {
        private readonly CombatService combat;
        public CombatCommandService(CombatService combatService) =>
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));

        public CombatCommandResult IssueAttack(IEnumerable<StableEntityId> selectedIds, StableEntityId targetId,
            long calendarTick, StableEntityId? encounterId = null)
        {
            var selected = (selectedIds ?? Array.Empty<StableEntityId>()).Where(x => x.IsValid).Distinct()
                .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
            if (selected.Length == 0 || !targetId.IsValid) return new CombatCommandResult(CombatCommandStatus.InvalidSelection, null, 0);
            if (!combat.TryGetSide(selected[0], out var attackerSide) || !combat.TryGetSide(targetId, out var targetSide) || attackerSide == targetSide)
                return new CombatCommandResult(CombatCommandStatus.NotHostile, null, 0);
            if (selected.Any(x => !combat.TryGetSide(x, out var side) || side != attackerSide || !combat.IsEligible(x)) ||
                !combat.IsEligible(targetId))
                return new CombatCommandResult(CombatCommandStatus.Ineligible, null, 0);
            if (combat.TryGetCombatant(targetId, out var targetCombatant))
            {
                var assigned = 0;
                foreach (var id in selected) if (combat.AssignTarget(id, targetId)) assigned++;
                return new CombatCommandResult(CombatCommandStatus.Assigned,
                    targetCombatant == null ? (StableEntityId?)null : combat.Encounters.First(x =>
                        x.Combatants.Any(c => c.CharacterId == targetId)).EncounterId, assigned);
            }
            var defenders = combat.GetSideMembers(targetSide).Where(combat.IsEligible).ToArray();
            if (defenders.Length == 0) return new CombatCommandResult(CombatCommandStatus.Ineligible, null, 0);
            var idValue = encounterId ?? StableEntityId.NewId();
            var state = combat.CreateEncounter(idValue, attackerSide, targetSide, selected, defenders, calendarTick,
                id => id == targetId ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear"));
            foreach (var id in selected) combat.AssignTarget(id, targetId);
            return new CombatCommandResult(CombatCommandStatus.Created, state.EncounterId, state.Combatants.Count);
        }
    }
}
