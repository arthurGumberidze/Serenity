using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.AI;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Control;
using Game.Presentation.Interaction;
using Game.Simulation.Combat;
using Game.Simulation.Control;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using Game.Simulation.Weather;
using Game.Simulation.Work;
using Game.Simulation.AI;
using Game.Simulation.Tiers;
using Game.Simulation.World;
using Game.Simulation.Events;
using Game.Simulation.Campaign;
using Game.Simulation.Domestic;
using Game.Simulation.Diagnostics;
using UnityEngine;

namespace Game.Presentation.UI
{
    public enum GameplayPanel
    {
        None = 0,
        Build = 1,
        Research = 2,
        Work = 3,
        Dynasty = 4,
        Military = 5,
        SaveLoad = 6,
        Global = 7,
        Economy = 8,
        Events = 9,
        Domestic = 10,
        Codes = 11
    }

    public sealed class GameplaySaveSlotInfo
    {
        public GameplaySaveSlotInfo(int index, string label, bool exists, bool isCorrupt, string detail,
            long? worldSeed = null, bool cheatsUsed = false)
        {
            Index = index;
            Label = label ?? string.Empty;
            Exists = exists;
            IsCorrupt = isCorrupt;
            Detail = detail ?? string.Empty;
            WorldSeed = worldSeed;
            CheatsUsed = cheatsUsed;
        }

        public int Index { get; }
        public string Label { get; }
        public bool Exists { get; }
        public bool IsCorrupt { get; }
        public string Detail { get; }
        public long? WorldSeed { get; }
        public bool CheatsUsed { get; }
    }

    public interface IGameplaySaveFacade
    {
        IReadOnlyList<GameplaySaveSlotInfo> ListSlots();
        bool HasUnsavedChanges { get; }
        void Save(int slotIndex);
        void Load(int slotIndex);
    }

    public sealed class GameplayUiContext
    {
        public GameplayUiContext(Func<GameClock> clock, CharacterRegistry characters,
            SelectionProbe selection, ResourceCatalog resources, ResourceInventoryRegistry inventories,
            SettlementResourceView storedResources, ProductionService production, ResearchService research,
            Tier1AiAgentRegistry aiAgents, Tier1AiScheduler aiScheduler, WorkManager work,
            DirectWorkCommandService directWork, ContextCommandService contextCommands, TierManager tiers,
            CharacterPresentationRegistry presentations,
            ManualMoveCommandService manualMoveCommands, WorldPileService worldPiles, BuildingRegistry buildings, BuildingPresentationCatalog buildingCatalog,
            BuildingPlacementController buildingPlacement, HealthAndFamilyService health, DomesticService domestic,
            WeatherService weather, DynastyState dynasty, FamilyTreeQuery family, GameRunState gameRun,
            MilitaryUnitService military, MilitaryUnitRepresentationService militaryRepresentations,
            MilitaryUnitCommandService militaryCommands, MilitaryTrainingService militaryTraining,
            MilitaryAttackCommandService militaryAttack, SettlementFlagCaptureService settlementCapture,
            BattleResolutionService battles, CombatService combat,
            DiagnosticService diagnostics,
            PlayerControlService playerControl, ThirdPersonControlController thirdPerson,
            GlobalMapService globalMap, IGameplaySaveFacade saves,
            Action<bool> setPaused, Action<Game.Simulation.Time.GameSpeed> setSpeed,
            Action requestEnterGlobal, Action<WorldLocationId> requestEnterLocation,
            Action<StableEntityId> requestEnterBattle, Action<bool> setDeveloperUiVisible,
            IEnumerable<Behaviour> legacyDebugOverlays, EventService events = null,
            CampaignProgressService campaign = null, Action<long> startNewGame = null,
            Action requestMainMenu = null, Func<IReadOnlyList<StableEntityId>> playerCharacterIds = null)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Characters = characters ?? throw new ArgumentNullException(nameof(characters));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            StoredResources = storedResources ?? throw new ArgumentNullException(nameof(storedResources));
            Production = production ?? throw new ArgumentNullException(nameof(production));
            Research = research ?? throw new ArgumentNullException(nameof(research));
            AiAgents = aiAgents ?? throw new ArgumentNullException(nameof(aiAgents));
            AiScheduler = aiScheduler ?? throw new ArgumentNullException(nameof(aiScheduler));
            Work = work ?? throw new ArgumentNullException(nameof(work));
            DirectWork = directWork ?? throw new ArgumentNullException(nameof(directWork));
            ContextCommands = contextCommands ?? throw new ArgumentNullException(nameof(contextCommands));
            Tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            Presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            ManualMoveCommands = manualMoveCommands ?? throw new ArgumentNullException(nameof(manualMoveCommands));
            WorldPiles = worldPiles ?? throw new ArgumentNullException(nameof(worldPiles));
            Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            BuildingCatalog = buildingCatalog ?? throw new ArgumentNullException(nameof(buildingCatalog));
            BuildingPlacement = buildingPlacement ?? throw new ArgumentNullException(nameof(buildingPlacement));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Domestic = domestic ?? throw new ArgumentNullException(nameof(domestic));
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            Family = family ?? throw new ArgumentNullException(nameof(family));
            GameRun = gameRun ?? throw new ArgumentNullException(nameof(gameRun));
            Military = military ?? throw new ArgumentNullException(nameof(military));
            MilitaryRepresentations = militaryRepresentations ?? throw new ArgumentNullException(nameof(militaryRepresentations));
            MilitaryCommands = militaryCommands ?? throw new ArgumentNullException(nameof(militaryCommands));
            MilitaryTraining = militaryTraining ?? throw new ArgumentNullException(nameof(militaryTraining));
            MilitaryAttack = militaryAttack ?? throw new ArgumentNullException(nameof(militaryAttack));
            SettlementCapture = settlementCapture ?? throw new ArgumentNullException(nameof(settlementCapture));
            Battles = battles ?? throw new ArgumentNullException(nameof(battles));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            PlayerControl = playerControl ?? throw new ArgumentNullException(nameof(playerControl));
            ThirdPerson = thirdPerson ?? throw new ArgumentNullException(nameof(thirdPerson));
            GlobalMap = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            SetPaused = setPaused ?? throw new ArgumentNullException(nameof(setPaused));
            SetSpeed = setSpeed ?? throw new ArgumentNullException(nameof(setSpeed));
            RequestEnterGlobal = requestEnterGlobal ?? throw new ArgumentNullException(nameof(requestEnterGlobal));
            RequestEnterLocation = requestEnterLocation ?? throw new ArgumentNullException(nameof(requestEnterLocation));
            RequestEnterBattle = requestEnterBattle ?? throw new ArgumentNullException(nameof(requestEnterBattle));
            SetDeveloperUiVisible = setDeveloperUiVisible ?? throw new ArgumentNullException(nameof(setDeveloperUiVisible));
            LegacyDebugOverlays = new List<Behaviour>(legacyDebugOverlays ?? Array.Empty<Behaviour>()).AsReadOnly();
            Events = events;
            Campaign = campaign;
            StartNewGame = startNewGame;
            RequestMainMenu = requestMainMenu;
            PlayerCharacterIds = playerCharacterIds ?? (() => Array.Empty<StableEntityId>());
        }

        public Func<GameClock> Clock { get; }
        public CharacterRegistry Characters { get; }
        public SelectionProbe Selection { get; }
        public ResourceCatalog Resources { get; }
        public ResourceInventoryRegistry Inventories { get; }
        public SettlementResourceView StoredResources { get; }
        public ProductionService Production { get; }
        public ResearchService Research { get; }
        public Tier1AiAgentRegistry AiAgents { get; }
        public Tier1AiScheduler AiScheduler { get; }
        public WorkManager Work { get; }
        public DirectWorkCommandService DirectWork { get; }
        public ContextCommandService ContextCommands { get; }
        public TierManager Tiers { get; }
        public CharacterPresentationRegistry Presentations { get; }
        public ManualMoveCommandService ManualMoveCommands { get; }
        public WorldPileService WorldPiles { get; }
        public BuildingRegistry Buildings { get; }
        public BuildingPresentationCatalog BuildingCatalog { get; }
        public BuildingPlacementController BuildingPlacement { get; }
        public HealthAndFamilyService Health { get; }
        public DomesticService Domestic { get; }
        public WeatherService Weather { get; }
        public DynastyState Dynasty { get; }
        public FamilyTreeQuery Family { get; }
        public GameRunState GameRun { get; }
        public MilitaryUnitService Military { get; }
        public MilitaryUnitRepresentationService MilitaryRepresentations { get; }
        public MilitaryUnitCommandService MilitaryCommands { get; }
        public MilitaryTrainingService MilitaryTraining { get; }
        public MilitaryAttackCommandService MilitaryAttack { get; }
        public SettlementFlagCaptureService SettlementCapture { get; }
        public BattleResolutionService Battles { get; }
        public CombatService Combat { get; }
        public DiagnosticService Diagnostics { get; }
        public PlayerControlService PlayerControl { get; }
        public ThirdPersonControlController ThirdPerson { get; }
        public GlobalMapService GlobalMap { get; }
        public IGameplaySaveFacade Saves { get; }
        public Action<bool> SetPaused { get; }
        public Action<Game.Simulation.Time.GameSpeed> SetSpeed { get; }
        public Action RequestEnterGlobal { get; }
        public Action<WorldLocationId> RequestEnterLocation { get; }
        public Action<StableEntityId> RequestEnterBattle { get; }
        public Action<bool> SetDeveloperUiVisible { get; }
        public IReadOnlyList<Behaviour> LegacyDebugOverlays { get; }
        public EventService Events { get; }
        public CampaignProgressService Campaign { get; }
        public Action<long> StartNewGame { get; }
        public Action RequestMainMenu { get; }
        public Func<IReadOnlyList<StableEntityId>> PlayerCharacterIds { get; }
    }
}
