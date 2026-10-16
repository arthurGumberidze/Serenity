using System;
using Game.Domain;
using Game.Simulation.Time;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Weather;
using Game.Simulation.Combat;
using Game.Simulation.Military;
using Game.Domain.Control;
using Game.Simulation.Control;
using Game.Simulation.Buildings;
using Game.Simulation.World;
using Game.Simulation.AI;
using Game.Simulation.Events;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using Game.Simulation.Work;

namespace Game.Simulation
{
    /// <summary>Call on the simulation owner thread at a committed tick barrier. No concurrent ticks or jobs.</summary>
    public sealed class SaveCoordinator
    {
        private readonly ISaveStore store;
        private readonly ProductionService production;
        private readonly ResearchService research;
        private readonly SuccessionService succession;
        private readonly HealthAndFamilyService healthFamily;
        private readonly WeatherService weather;
        private readonly CombatService combat;
        private readonly MilitaryUnitService military;
        private readonly PlayerControlService playerControl;
        private readonly BuildingDamageService buildingDamage;
        private readonly GlobalMapService globalMap;
        private readonly AiStrategicScheduler strategicAi;
        private readonly EventService events;
        private readonly CampaignProgressService campaign;
        private readonly CharacterAppearanceService appearance;
        private readonly WorkManager work;
        private readonly ConnectedWorldService connectedWorld;
        public StableEntityId SessionId { get; private set; }
        public GameClock Clock { get; private set; }

        public SaveCoordinator(StableEntityId sessionId, GameClock clock, ISaveStore store,
            ProductionService productionService = null, ResearchService researchService = null,
            SuccessionService successionService = null, HealthAndFamilyService healthFamilyService = null,
            WeatherService weatherService = null, CombatService combatService = null,
            MilitaryUnitService militaryUnitService = null, PlayerControlService playerControlService = null,
            BuildingDamageService buildingDamageService = null, GlobalMapService globalMapService = null,
            AiStrategicScheduler strategicAiScheduler = null, EventService eventService = null,
            CampaignProgressService campaignProgressService = null,
            CharacterAppearanceService appearanceService = null, WorkManager workManager = null,
            ConnectedWorldService connectedWorldService = null)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Invalid session ID.", nameof(sessionId));
            SessionId = sessionId;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            production = productionService;
            research = researchService;
            succession = successionService;
            healthFamily = healthFamilyService;
            weather = weatherService;
            combat = combatService;
            military = militaryUnitService;
            playerControl = playerControlService;
            buildingDamage = buildingDamageService;
            globalMap = globalMapService;
            strategicAi = strategicAiScheduler;
            events = eventService;
            campaign = campaignProgressService;
            appearance = appearanceService;
            work = workManager;
            connectedWorld = connectedWorldService;
        }
        public SaveSnapshot Capture()
        {
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, SessionId, Clock.State,
                Clock.BiologicalMultiplier, production?.CaptureState(), research?.CaptureState(),
                succession?.CaptureState(), healthFamily?.CaptureState(), weather?.CaptureState(), combat?.CaptureState(),
                military?.CaptureState(), playerControl?.CaptureState(), buildingDamage?.CaptureState(),
                globalMap?.CaptureState(), strategicAi?.CaptureState(), events?.CaptureState(), campaign?.CaptureState(),
                appearance?.CaptureState(), work?.CaptureState(), connectedWorld?.CaptureState());
            CreateClock(snapshot); // Validate all mutable U03 state before exposing a checkpoint.
            return snapshot;
        }
        public void Save(StableEntityId slotId) => store.Save(slotId, Capture());
        public void Load(StableEntityId slotId)
        {
            var snapshot = store.Load(slotId);
            var restoredClock = CreateClock(snapshot);
            if (snapshot.Production != null && production == null)
                throw new InvalidOperationException("This save contains production state but no production service is attached.");
            if (snapshot.Production != null) production.RestoreState(snapshot.Production);
            if (snapshot.Research != null && research == null)
                throw new InvalidOperationException("This save contains research state but no research service is attached.");
            if (snapshot.Research != null) research.RestoreState(snapshot.Research);
            if (snapshot.DynastyWorld != null && succession == null)
                throw new InvalidOperationException("This save contains dynasty state but no succession service is attached.");
            if (snapshot.DynastyWorld != null) succession.RestoreState(snapshot.DynastyWorld);
            if (snapshot.HealthFamily != null && healthFamily == null)
                throw new InvalidOperationException("This save contains health/family state but no health/family service is attached.");
            if (snapshot.HealthFamily != null) healthFamily.RestoreState(snapshot.HealthFamily);
            if (snapshot.Weather != null && weather == null)
                throw new InvalidOperationException("This save contains weather state but no weather service is attached.");
            if (snapshot.Weather != null) weather.RestoreState(snapshot.Weather);
            if (snapshot.Combat != null && combat == null)
                throw new InvalidOperationException("This save contains combat state but no combat service is attached.");
            if (snapshot.Combat != null) combat.RestoreState(snapshot.Combat);
            if (snapshot.Military != null && military == null)
                throw new InvalidOperationException("This save contains military-unit state but no military service is attached.");
            if (snapshot.Military != null) military.RestoreState(snapshot.Military, combat);
            if (snapshot.PlayerControl != null && playerControl == null)
                throw new InvalidOperationException("This save contains player-control state but no player-control service is attached.");
            if (snapshot.PlayerControl != null) playerControl.RestoreState(snapshot.PlayerControl);
            if (snapshot.BuildingWorld != null && buildingDamage == null)
                throw new InvalidOperationException("This save contains building state but no building-damage service is attached.");
            if (snapshot.BuildingWorld != null) buildingDamage.RestoreState(snapshot.BuildingWorld);
            if (snapshot.GlobalMap != null && globalMap == null)
                throw new InvalidOperationException("This save contains global-map state but no global-map service is attached.");
            if (snapshot.GlobalMap != null) globalMap.RestoreState(snapshot.GlobalMap);
            if (snapshot.StrategicAi != null && strategicAi == null)
                throw new InvalidOperationException("This save contains strategic AI state but no strategic AI scheduler is attached.");
            if (snapshot.StrategicAi != null) strategicAi.RestoreState(snapshot.StrategicAi);
            if (snapshot.Events != null && events == null)
                throw new InvalidOperationException("This save contains event state but no event service is attached.");
            if (snapshot.Events != null)
            {
                events.RestoreState(snapshot.Events);
            }
            else if (events != null)
            {
                // v1-v13 had no event payload. Loading one must reset U28 state at the restored
                // canonical tick instead of leaking events from the session being replaced.
                events.RestoreState(new Game.Domain.Events.EventSaveState(events.WorldSeed,
                    snapshot.CalendarTicks, 0, Array.Empty<Game.Domain.Events.EventInstanceState>(),
                    Array.Empty<Game.Domain.Events.EventCooldownState>(),
                    Array.Empty<Game.Domain.Events.EventHistoryEntry>(), 0, 0));
            }
            if (snapshot.Campaign != null && campaign == null)
                throw new InvalidOperationException("This save contains campaign state but no campaign service is attached.");
            if (snapshot.Campaign != null) campaign.RestoreState(snapshot.Campaign);
            else campaign?.RestoreLegacyDefault();
            if (snapshot.Appearance != null && appearance == null)
                throw new InvalidOperationException("This save contains appearance state but no appearance service is attached.");
            if (snapshot.Appearance != null) appearance.RestoreState(snapshot.Appearance);
            else appearance?.RestoreLegacyDefaults();
            if (snapshot.Work != null && work == null)
                throw new InvalidOperationException("This save contains work state but no work manager is attached.");
            if (snapshot.Work != null) work.RestoreState(snapshot.Work);
            if (snapshot.ConnectedWorld != null && connectedWorld == null)
                throw new InvalidOperationException("This save contains connected-world state but no connected-world service is attached.");
            if (snapshot.ConnectedWorld != null) connectedWorld.RestoreState(snapshot.ConnectedWorld);
            // Commit only after all reads, parsing and validation succeed. Consumers rebind after Load.
            Clock = restoredClock;
            SessionId = snapshot.SessionId;
        }
        public static GameClock CreateClock(SaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new GameClock(snapshot.CreateTimeState(), new GameTimeSettings(snapshot.BiologicalMultiplier));
        }
    }
}
