using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Simulation.AI;
using Game.Simulation.Combat;
using Game.Simulation.Military;
using Game.Simulation.World;

namespace Game.Simulation.Campaign
{
    /// <summary>Canonical minimal knowledge and post-victory continuation state for the Stone Age campaign.</summary>
    public sealed class CampaignProgressService
    {
        private readonly GlobalMapService map;
        private readonly MilitaryUnitService units;
        private readonly AiStrategicScheduler strategicAi;
        private readonly BattleResolutionService battles;
        private readonly GameRunState playerGameRun;
        private readonly AiFactionRuntime rivalFaction;
        private CampaignProgressSaveState state;

        public CampaignProgressService(NewGameConfiguration configuration, CombatSideId playerSideId,
            AiFactionRuntime rival, GlobalMapService globalMap, MilitaryUnitService militaryUnits,
            AiStrategicScheduler scheduler, BattleResolutionService battleService, long initialTick,
            GameRunState gameRunState = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            strategicAi = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            battles = battleService ?? throw new ArgumentNullException(nameof(battleService));
            playerGameRun = gameRunState;
            if (rival == null) throw new ArgumentNullException(nameof(rival));
            rivalFaction = rival;
            state = new CampaignProgressSaveState(configuration.WorldSeed, configuration.ConfigurationId,
                playerSideId, rival.Strategic.Id, rival.Strategic.HomeSettlementId,
                configuration.RivalKnownAtStart, configuration.RivalKnownAtStart ? initialTick : (long?)null,
                false, null, null, null, null, configuration.RivalKnownAtStart ? 1UL : 0UL);
        }

        public CampaignProgressSaveState State => state;
        public bool CanRevealSettlement(SettlementId id) => id != state.RivalHomeSettlementId || state.RivalDiscovered;

        public void AdvanceTo(long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!state.RivalDiscovered && HasCanonicalContact()) MarkDiscovered(calendarTick);
            TryCaptureAfterPlayerVictory(calendarTick);
            if (!rivalFaction.Strategic.IsDefeated &&
                StateDefeatRule.IsDefeated(map, rivalFaction.Strategic.SideId))
                rivalFaction.Strategic.MarkDefeated(AiDefeatReason.NoControlledSettlement);
            var rival = strategicAi.Factions.Single(x => x.Strategic.Id == state.RivalStateId);
            if (!state.MilitaryVictory && rival.Strategic.IsDefeated &&
                rival.Strategic.DefeatReason == AiDefeatReason.NoControlledSettlement)
            {
                var battle = FindPlayerVictoryBattle();
                var occupation = FindPlayerOccupationVictory();
                if (battle != null || occupation != null)
                {
                    if (!state.RivalDiscovered) MarkDiscovered(calendarTick);
                    state = new CampaignProgressSaveState(state.WorldSeed, state.ConfigurationId, state.PlayerSideId,
                        state.RivalStateId, state.RivalHomeSettlementId, true, state.DiscoveredTick, true,
                        calendarTick, state.RivalStateId, state.RivalHomeSettlementId, battle?.BattleId,
                        state.EventSequence + 1, occupation?.OccupationId);
                }
            }
            if (playerGameRun != null && playerGameRun.Status == GameRunStatus.Running &&
                StateDefeatRule.IsDefeated(map, state.PlayerSideId))
                playerGameRun.End(GameOverReason.NoControlledSettlement);
        }

        public CampaignProgressSaveState CaptureState() => state;

        public void RestoreState(CampaignProgressSaveState saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (saved.WorldSeed != state.WorldSeed || saved.ConfigurationId != state.ConfigurationId ||
                saved.PlayerSideId != state.PlayerSideId || saved.RivalStateId != state.RivalStateId ||
                saved.RivalHomeSettlementId != state.RivalHomeSettlementId)
                throw new ArgumentException("Campaign identity does not match this session.", nameof(saved));
            if (map.Settlements.All(x => x.Id != saved.RivalHomeSettlementId))
                throw new ArgumentException("Campaign references an unavailable settlement.", nameof(saved));
            state = saved;
        }

        public void RestoreLegacyDefault()
        {
            state = new CampaignProgressSaveState(state.WorldSeed, state.ConfigurationId, state.PlayerSideId,
                state.RivalStateId, state.RivalHomeSettlementId, false, null, false, null, null, null, null, 0);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, state.WorldSeed.ToString()); Add(ref hash, state.ConfigurationId);
            Add(ref hash, state.PlayerSideId.Value); Add(ref hash, state.RivalStateId.ToString());
            Add(ref hash, state.RivalHomeSettlementId.ToString()); Add(ref hash, state.RivalDiscovered ? "1" : "0");
            Add(ref hash, state.DiscoveredTick?.ToString() ?? string.Empty); Add(ref hash, state.MilitaryVictory ? "1" : "0");
            Add(ref hash, state.VictoryTick?.ToString() ?? string.Empty); Add(ref hash, state.SourceBattleId?.ToString() ?? string.Empty);
            Add(ref hash, state.SourceOccupationId?.ToString() ?? string.Empty);
            Add(ref hash, state.EventSequence.ToString());
            return hash;
        }

        private bool HasCanonicalContact()
        {
            var home = map.Settlements.Single(x => x.Id == state.RivalHomeSettlementId);
            var playerUnits = units.Units.Where(x => x.OwnerSideId.Value == state.PlayerSideId.Value).ToArray();
            if (playerUnits.Any(x => x.AbstractLocationId == home.LocationId.Value)) return true;
            var rivalLocations = new System.Collections.Generic.HashSet<string>(units.Units
                .Where(x => x.OwnerSideId.Value != state.PlayerSideId.Value)
                .Select(x => x.AbstractLocationId), StringComparer.Ordinal);
            if (playerUnits.Any(x => rivalLocations.Contains(x.AbstractLocationId))) return true;
            return strategicAi.Factions.Single(x => x.Strategic.Id == state.RivalStateId).Strategic.KnownMilitaryUnits
                .Any(id => playerUnits.Any(player => player.ActiveEncounterId.HasValue &&
                    units.TryGet(id, out var enemy) && enemy.ActiveEncounterId == player.ActiveEncounterId));
        }

        private void MarkDiscovered(long tick)
        {
            state = new CampaignProgressSaveState(state.WorldSeed, state.ConfigurationId, state.PlayerSideId,
                state.RivalStateId, state.RivalHomeSettlementId, true, tick, false, null, null, null, null,
                state.EventSequence + 1);
        }

        private void TryCaptureAfterPlayerVictory(long tick)
        {
            if (state.MilitaryVictory) return;
            var home = map.Settlements.SingleOrDefault(x => x.Id == state.RivalHomeSettlementId);
            if (home == null || home.OwnerSideId == state.PlayerSideId) return;
            var battle = FindPlayerVictoryBattle();
            if (battle == null) return;
            strategicAi.CaptureSettlement(home.Id, state.PlayerSideId, battle.BattleId, tick);
        }

        private CombatEncounterState FindPlayerVictoryBattle()
        {
            var home = map.Settlements.SingleOrDefault(x => x.Id == state.RivalHomeSettlementId);
            if (home == null) return null;
            return FindBattleFromUnits(home);
        }

        private SettlementOccupationEventState FindPlayerOccupationVictory() => map.OccupationEvents
            .Where(x => x.Kind == SettlementOccupationEventKind.Completed &&
                        x.SettlementId == state.RivalHomeSettlementId &&
                        x.ClaimantSideId == state.PlayerSideId)
            .OrderByDescending(x => x.CalendarTick).ThenByDescending(x => x.Sequence).FirstOrDefault();

        private CombatEncounterState FindBattleFromUnits(SettlementState home)
        {
            var battleIds = new System.Collections.Generic.HashSet<StableEntityId>(units.Units
                .Where(x => x.AbstractLocationId == home.LocationId.Value && x.ActiveEncounterId.HasValue)
                .Select(x => x.ActiveEncounterId.Value));
            var candidates = battles.Battles.Where(x => x.Status == CombatEncounterStatus.Finished &&
                x.SideAUnitId.HasValue && x.SideBUnitId.HasValue &&
                (battleIds.Count == 0 || battleIds.Contains(x.BattleId))).OrderByDescending(x => x.LastProcessedCalendarTick)
                .ThenByDescending(x => x.BattleId).ToArray();
            foreach (var battle in candidates)
            {
                var victor = battle.Outcome == CombatOutcome.SideAVictory ? battle.SideA :
                    battle.Outcome == CombatOutcome.SideBVictory ? battle.SideB : default;
                if (victor != state.PlayerSideId) continue;
                if (!units.TryGet(battle.SideAUnitId.Value, out var first) ||
                    !units.TryGet(battle.SideBUnitId.Value, out var second)) continue;
                if (first.AbstractLocationId == home.LocationId.Value &&
                    second.AbstractLocationId == home.LocationId.Value) return battle;
            }
            return null;
        }

        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }
}
