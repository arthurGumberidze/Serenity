using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Simulation.AI;
using Game.Simulation.Military;

namespace Game.Simulation.Combat
{
    public interface IBattleRepresentationPolicy
    {
        BattleMode ChooseMode(CombatEncounterState battle);
    }

    public enum BattleTransitionStatus
    {
        Changed = 1,
        AlreadyInMode = 2,
        BattleNotFound = 3,
        BattleFinished = 4,
        MissingMilitaryUnits = 5,
        BlockedByDirectControl = 6,
        FailedAndRolledBack = 7
    }

    public readonly struct BattleTransitionResult
    {
        public BattleTransitionResult(BattleTransitionStatus status, BattleMode mode, string message = null)
        {
            Status = status;
            Mode = mode;
            Message = message ?? string.Empty;
        }

        public BattleTransitionStatus Status { get; }
        public BattleMode Mode { get; }
        public string Message { get; }
        public bool Changed => Status == BattleTransitionStatus.Changed;
    }

    /// <summary>
    /// U23 orchestration boundary. CombatService remains the one mutable battle/combat authority;
    /// this service validates military context and commits representation transitions atomically.
    /// </summary>
    public sealed class BattleResolutionService
    {
        private readonly CharacterRegistry characters;
        private readonly CombatService combat;
        private readonly MilitaryUnitService units;
        private readonly MilitaryUnitRepresentationService representations;
        private readonly MilitaryUnitCombatService physicalCombat;
        private readonly IExternalCharacterActivityOwner directControl;

        public BattleResolutionService(CharacterRegistry characterRegistry, CombatService combatService,
            MilitaryUnitService militaryUnitService, MilitaryUnitRepresentationService representationService,
            MilitaryUnitCombatService physicalCombatService, IExternalCharacterActivityOwner directControlOwner = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            combat = combatService ?? throw new ArgumentNullException(nameof(combatService));
            units = militaryUnitService ?? throw new ArgumentNullException(nameof(militaryUnitService));
            representations = representationService ?? throw new ArgumentNullException(nameof(representationService));
            physicalCombat = physicalCombatService ?? throw new ArgumentNullException(nameof(physicalCombatService));
            directControl = directControlOwner;
        }

        public IReadOnlyList<CombatEncounterState> Battles => combat.Encounters;

        public CombatEncounterState StartPhysicalBattle(MilitaryUnitId firstId, MilitaryUnitId secondId,
            StableEntityId battleId, long calendarTick,
            Func<StableEntityId, CombatWeaponId> weaponSelector = null) =>
            physicalCombat.Start(firstId, secondId, battleId, calendarTick, weaponSelector);

        public CombatEncounterState StartAbstractBattle(MilitaryUnitId firstId, MilitaryUnitId secondId,
            StableEntityId battleId, long calendarTick,
            Func<StableEntityId, CombatWeaponId> weaponSelector = null)
        {
            var first = units.Get(firstId);
            var second = units.Get(secondId);
            if (first.OwnerSideId == second.OwnerSideId)
                throw new InvalidOperationException("Abstract battle requires hostile units.");
            if (first.ActiveEncounterId.HasValue || second.ActiveEncounterId.HasValue)
                throw new InvalidOperationException("A unit is already participating in another battle.");
            if (first.Representation != MilitaryUnitRepresentationMode.Aggregate ||
                second.Representation != MilitaryUnitRepresentationMode.Aggregate)
                throw new InvalidOperationException("Abstract battle requires aggregate units.");
            if (!string.Equals(first.AbstractLocationId, second.AbstractLocationId, StringComparison.Ordinal))
                throw new InvalidOperationException("Abstract battle requires compatible logical locations.");

            var firstSide = new CombatSideId(first.OwnerSideId.Value);
            var secondSide = new CombatSideId(second.OwnerSideId.Value);
            var firstMembers = units.GetCombatEligibleMembers(firstId);
            var secondMembers = units.GetCombatEligibleMembers(secondId);
            foreach (var id in firstMembers) combat.RegisterSide(id, firstSide);
            foreach (var id in secondMembers) combat.RegisterSide(id, secondSide);
            firstMembers = firstMembers.Where(combat.IsAbstractEligible).ToArray();
            secondMembers = secondMembers.Where(combat.IsAbstractEligible).ToArray();
            if (firstMembers.Count == 0 || secondMembers.Count == 0)
                throw new InvalidOperationException("Both aggregate units require combat-capable participants.");

            var battle = combat.CreateEncounter(battleId, firstSide, secondSide, firstMembers, secondMembers,
                calendarTick, weaponSelector, BattleMode.Abstract, firstId, secondId);
            try
            {
                units.AttachEncounter(firstId, battleId, secondId, BattleMode.Abstract);
                units.AttachEncounter(secondId, battleId, firstId, BattleMode.Abstract);
                return battle;
            }
            catch
            {
                units.DetachEncounter(firstId, battleId);
                units.DetachEncounter(secondId, battleId);
                combat.CancelEncounter(battleId);
                throw;
            }
        }

        public BattleTransitionResult Transition(StableEntityId battleId, BattleMode targetMode,
            long calendarTick)
        {
            if (!combat.TryGetEncounter(battleId, out var battle))
                return new BattleTransitionResult(BattleTransitionStatus.BattleNotFound, targetMode);
            if (battle.Status != CombatEncounterStatus.Active)
                return new BattleTransitionResult(BattleTransitionStatus.BattleFinished, battle.Mode);
            if (battle.Mode == targetMode)
                return new BattleTransitionResult(BattleTransitionStatus.AlreadyInMode, battle.Mode);
            if (!battle.SideAUnitId.HasValue || !battle.SideBUnitId.HasValue)
                return new BattleTransitionResult(BattleTransitionStatus.MissingMilitaryUnits, battle.Mode);
            if (targetMode == BattleMode.Abstract && directControl != null &&
                battle.Combatants.Any(x => directControl.IsExternallyControlled(x.CharacterId)))
                return new BattleTransitionResult(BattleTransitionStatus.BlockedByDirectControl, battle.Mode);

            var firstId = battle.SideAUnitId.Value;
            var secondId = battle.SideBUnitId.Value;
            var firstBefore = units.Get(firstId).Representation;
            var secondBefore = units.Get(secondId).Representation;
            try
            {
                if (targetMode == BattleMode.Abstract)
                {
                    // Release the U20 Tier1 pin without publishing a half-committed transition event.
                    combat.SetBattleMode(battleId, BattleMode.Abstract, calendarTick, false);
                    representations.Transition(firstId, MilitaryUnitRepresentationMode.Aggregate);
                    representations.Transition(secondId, MilitaryUnitRepresentationMode.Aggregate);
                    combat.RecordModeTransition(battleId, calendarTick);
                }
                else
                {
                    representations.Transition(firstId, MilitaryUnitRepresentationMode.Detailed);
                    representations.Transition(secondId, MilitaryUnitRepresentationMode.Detailed);
                    combat.SetBattleMode(battleId, BattleMode.Physical, calendarTick);
                }
                return new BattleTransitionResult(BattleTransitionStatus.Changed, targetMode);
            }
            catch (Exception exception)
            {
                TryRestoreRepresentation(secondId, secondBefore);
                TryRestoreRepresentation(firstId, firstBefore);
                if (targetMode == BattleMode.Abstract)
                {
                    try { combat.SetBattleMode(battleId, BattleMode.Physical, calendarTick, false); }
                    catch { }
                }
                return new BattleTransitionResult(BattleTransitionStatus.FailedAndRolledBack,
                    combat.TryGetEncounter(battleId, out var current) ? current.Mode : battle.Mode,
                    exception.Message);
            }
        }

        public void AdvanceTo(long calendarTick, long biologicalTick)
        {
            var captains = combat.Encounters.Where(x => x.Status == CombatEncounterStatus.Active)
                .SelectMany(x => UnitIds(x).Select(id => new CaptainBefore(x.BattleId, id,
                    units.Get(id).OwnerSideId, units.Get(id).CaptainId)))
                .ToArray();
            combat.AdvanceTo(calendarTick, biologicalTick);

            foreach (var before in captains)
            {
                if (before.CaptainId.HasValue && characters.TryGet(before.CaptainId.Value, out var captain) &&
                    captain.LifeState == CharacterLifeState.Dead)
                    combat.RecordCaptainDeath(before.BattleId, before.CaptainId.Value,
                        new CombatSideId(before.SideId.Value), calendarTick);
            }

            foreach (var battle in combat.Encounters.Where(x => x.SideAUnitId.HasValue && x.SideBUnitId.HasValue)
                         .OrderBy(x => x.BattleId))
            {
                foreach (var unitId in UnitIds(battle))
                {
                    if (units.TryGet(unitId, out var unit) && unit.ActiveEncounterId == battle.BattleId)
                        units.ReconcileEncounter(unitId, battle);
                }
            }
        }

        private void TryRestoreRepresentation(MilitaryUnitId unitId, MilitaryUnitRepresentationMode mode)
        {
            try
            {
                if (units.Get(unitId).Representation != mode) representations.Transition(unitId, mode);
            }
            catch { }
        }

        private static IEnumerable<MilitaryUnitId> UnitIds(CombatEncounterState battle)
        {
            if (battle.SideAUnitId.HasValue) yield return battle.SideAUnitId.Value;
            if (battle.SideBUnitId.HasValue) yield return battle.SideBUnitId.Value;
        }

        private readonly struct CaptainBefore
        {
            public CaptainBefore(StableEntityId battleId, MilitaryUnitId unitId,
                MilitarySideId sideId, StableEntityId? captainId)
            {
                BattleId = battleId;
                UnitId = unitId;
                SideId = sideId;
                CaptainId = captainId;
            }

            public StableEntityId BattleId { get; }
            public MilitaryUnitId UnitId { get; }
            public MilitarySideId SideId { get; }
            public StableEntityId? CaptainId { get; }
        }
    }
}
