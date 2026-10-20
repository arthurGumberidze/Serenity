using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Simulation.Combat;
using Game.Simulation.Control;
using Game.Simulation.Military;
using Game.Simulation.Tiers;

namespace Game.Simulation.World
{
    public interface IGameModePlayerControl
    {
        PlayerControlMode Mode { get; }
        StableEntityId? ControlledCharacterId { get; }
        bool Exit();
        bool TryEnter(StableEntityId characterId);
    }

    public sealed class PlayerControlModeBoundary : IGameModePlayerControl
    {
        private readonly PlayerControlService service;
        public PlayerControlModeBoundary(PlayerControlService value) =>
            service = value ?? throw new ArgumentNullException(nameof(value));
        public PlayerControlMode Mode => service.State.Mode;
        public StableEntityId? ControlledCharacterId => service.State.ControlledCharacterId;
        public bool Exit() => service.ExitThirdPerson();
        public bool TryEnter(StableEntityId characterId) => service.TryEnterThirdPerson(characterId).IsEligible;
    }

    public enum GameModeTransitionStatus
    {
        Changed = 1,
        AlreadyInMode = 2,
        UnknownLocation = 3,
        LocationIsNotSettlement = 4,
        BattleNotFound = 5,
        BattleNotAtLocation = 6,
        FailedAndRolledBack = 7
    }

    public readonly struct GameModeTransitionResult
    {
        public GameModeTransitionResult(GameModeTransitionStatus status, GameWorldMode mode, string message = null)
        {
            Status = status;
            Mode = mode;
            Message = message ?? string.Empty;
        }
        public GameModeTransitionStatus Status { get; }
        public GameWorldMode Mode { get; }
        public string Message { get; }
        public bool Changed => Status == GameModeTransitionStatus.Changed;
    }

    /// <summary>
    /// Validates and commits Local/Global representation changes. Mode is changed last; failed tier/battle
    /// transitions are compensated before a failure is returned.
    /// </summary>
    public sealed class GameModeTransitionService
    {
        private readonly GlobalMapService map;
        private readonly TierManager tiers;
        private readonly MilitaryUnitService units;
        private readonly MilitaryUnitRepresentationService representations;
        private readonly BattleResolutionService battles;
        private readonly IGameModePlayerControl playerControl;

        public GameModeTransitionService(GlobalMapService globalMap, TierManager tierManager,
            MilitaryUnitService militaryUnits, MilitaryUnitRepresentationService militaryRepresentations,
            BattleResolutionService battleResolution, PlayerControlService controlService)
            : this(globalMap, tierManager, militaryUnits, militaryRepresentations, battleResolution,
                new PlayerControlModeBoundary(controlService)) { }

        public GameModeTransitionService(GlobalMapService globalMap, TierManager tierManager,
            MilitaryUnitService militaryUnits, MilitaryUnitRepresentationService militaryRepresentations,
            BattleResolutionService battleResolution, IGameModePlayerControl controlBoundary)
        {
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            tiers = tierManager ?? throw new ArgumentNullException(nameof(tierManager));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            representations = militaryRepresentations ?? throw new ArgumentNullException(nameof(militaryRepresentations));
            battles = battleResolution ?? throw new ArgumentNullException(nameof(battleResolution));
            playerControl = controlBoundary ?? throw new ArgumentNullException(nameof(controlBoundary));
        }

        public GameModeTransitionResult EnterGlobal(long calendarTick)
        {
            if (map.Mode == GameWorldMode.Global)
                return new GameModeTransitionResult(GameModeTransitionStatus.AlreadyInMode, map.Mode);
            var changedTiers = new List<Tuple<StableEntityId, CharacterSimulationTier>>();
            var abstractedBattles = new List<StableEntityId>();
            var aggregatedUnits = new List<MilitaryUnitId>();
            var controlled = playerControl.ControlledCharacterId;
            try
            {
                if (playerControl.Mode == PlayerControlMode.ThirdPerson) playerControl.Exit();

                foreach (var battle in battles.Battles.Where(x => x.Status == CombatEncounterStatus.Active &&
                             x.Mode == BattleMode.Physical && x.SideAUnitId.HasValue && x.SideBUnitId.HasValue)
                         .OrderBy(x => x.BattleId))
                {
                    var result = battles.Transition(battle.BattleId, BattleMode.Abstract, calendarTick);
                    if (!result.Changed && result.Status != BattleTransitionStatus.AlreadyInMode)
                        throw new InvalidOperationException(result.Message.Length == 0
                            ? "A physical battle could not become abstract." : result.Message);
                    if (result.Changed) abstractedBattles.Add(battle.BattleId);
                }

                foreach (var unit in units.Units.Where(x => x.Representation == MilitaryUnitRepresentationMode.Detailed &&
                             !x.ActiveEncounterId.HasValue).OrderBy(x => x.Id))
                {
                    representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
                    aggregatedUnits.Add(unit.Id);
                }

                foreach (var id in tiers.ManagedIds)
                {
                    var before = tiers.GetTier(id);
                    if (before == CharacterSimulationTier.Tier3) continue;
                    tiers.Transition(id, CharacterSimulationTier.Tier3);
                    changedTiers.Add(Tuple.Create(id, before));
                }
                map.CommitMode(GameWorldMode.Global, map.ActiveLocationId);
                return new GameModeTransitionResult(GameModeTransitionStatus.Changed, map.Mode);
            }
            catch (Exception exception)
            {
                for (var i = changedTiers.Count - 1; i >= 0; i--) TryTier(changedTiers[i].Item1, changedTiers[i].Item2);
                for (var i = aggregatedUnits.Count - 1; i >= 0; i--)
                    TryUnit(aggregatedUnits[i], MilitaryUnitRepresentationMode.Detailed);
                for (var i = abstractedBattles.Count - 1; i >= 0; i--)
                    battles.Transition(abstractedBattles[i], BattleMode.Physical, calendarTick);
                if (controlled.HasValue && playerControl.Mode == PlayerControlMode.Rts)
                    playerControl.TryEnter(controlled.Value);
                return new GameModeTransitionResult(GameModeTransitionStatus.FailedAndRolledBack,
                    GameWorldMode.Local, exception.Message);
            }
        }

        public GameModeTransitionResult EnterLocation(WorldLocationId locationId, long calendarTick)
        {
            WorldLocationState location;
            try { location = map.GetLocation(locationId); }
            catch (KeyNotFoundException)
            {
                return new GameModeTransitionResult(GameModeTransitionStatus.UnknownLocation, map.Mode);
            }
            if (map.Mode == GameWorldMode.Local && map.ActiveLocationId == locationId)
                return new GameModeTransitionResult(GameModeTransitionStatus.AlreadyInMode, map.Mode);

            var settlement = map.GetSettlementAt(locationId);
            var changed = new List<Tuple<StableEntityId, CharacterSimulationTier>>();
            var detailedUnits = new List<MilitaryUnitId>();
            try
            {
                if (settlement != null)
                    foreach (var id in settlement.ResidentCharacterIds.OrderBy(x => x))
                    {
                        if (!tiers.TryGetTier(id, out var before) || before == CharacterSimulationTier.Tier1) continue;
                        tiers.Transition(id, CharacterSimulationTier.Tier1);
                        changed.Add(Tuple.Create(id, before));
                    }
                foreach (var unit in map.GetMilitaryPresence(locationId).Where(x =>
                             x.Representation == MilitaryUnitRepresentationMode.Aggregate &&
                             !x.ActiveEncounterId.HasValue && (settlement == null ||
                             x.OwnerSideId.Value == settlement.OwnerSideId.Value)))
                {
                    representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Detailed);
                    detailedUnits.Add(unit.Id);
                }
                map.CommitMode(GameWorldMode.Local, locationId);
                return new GameModeTransitionResult(GameModeTransitionStatus.Changed, map.Mode);
            }
            catch (Exception exception)
            {
                for (var i = detailedUnits.Count - 1; i >= 0; i--)
                    TryUnit(detailedUnits[i], MilitaryUnitRepresentationMode.Aggregate);
                for (var i = changed.Count - 1; i >= 0; i--) TryTier(changed[i].Item1, changed[i].Item2);
                return new GameModeTransitionResult(GameModeTransitionStatus.FailedAndRolledBack,
                    GameWorldMode.Global, exception.Message);
            }
        }

        public GameModeTransitionResult EnterBattle(StableEntityId battleId, long calendarTick)
        {
            var battle = battles.Battles.SingleOrDefault(x => x.BattleId == battleId);
            if (battle == null)
                return new GameModeTransitionResult(GameModeTransitionStatus.BattleNotFound, map.Mode);
            if (!battle.SideAUnitId.HasValue || !battle.SideBUnitId.HasValue)
                return new GameModeTransitionResult(GameModeTransitionStatus.BattleNotAtLocation, map.Mode);
            var first = units.Get(battle.SideAUnitId.Value);
            var second = units.Get(battle.SideBUnitId.Value);
            if (!string.Equals(first.AbstractLocationId, second.AbstractLocationId, StringComparison.Ordinal))
                return new GameModeTransitionResult(GameModeTransitionStatus.BattleNotAtLocation, map.Mode);
            WorldLocationId location;
            try { location = new WorldLocationId(first.AbstractLocationId); map.GetLocation(location); }
            catch (ArgumentException) { return new GameModeTransitionResult(GameModeTransitionStatus.BattleNotAtLocation, map.Mode); }
            catch (KeyNotFoundException) { return new GameModeTransitionResult(GameModeTransitionStatus.BattleNotAtLocation, map.Mode); }
            var transition = battles.Transition(battleId, BattleMode.Physical, calendarTick);
            if (!transition.Changed && transition.Status != BattleTransitionStatus.AlreadyInMode)
                return new GameModeTransitionResult(GameModeTransitionStatus.FailedAndRolledBack, map.Mode,
                    transition.Message);
            map.CommitMode(GameWorldMode.Local, location);
            return new GameModeTransitionResult(GameModeTransitionStatus.Changed, map.Mode);
        }

        private void TryTier(StableEntityId id, CharacterSimulationTier tier)
        {
            try { if (tiers.GetTier(id) != tier) tiers.Transition(id, tier); }
            catch { }
        }

        private void TryUnit(MilitaryUnitId id, MilitaryUnitRepresentationMode mode)
        {
            try { if (units.Get(id).Representation != mode) representations.Transition(id, mode); }
            catch { }
        }
    }
}
