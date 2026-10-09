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
using Game.Simulation.World;
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
        Economy = 8
    }

    public sealed class GameplaySaveSlotInfo
    {
        public GameplaySaveSlotInfo(int index, string label, bool exists, bool isCorrupt, string detail)
        {
            Index = index;
            Label = label ?? string.Empty;
            Exists = exists;
            IsCorrupt = isCorrupt;
            Detail = detail ?? string.Empty;
        }

        public int Index { get; }
        public string Label { get; }
        public bool Exists { get; }
        public bool IsCorrupt { get; }
        public string Detail { get; }
    }

    public interface IGameplaySaveFacade
    {
        IReadOnlyList<GameplaySaveSlotInfo> ListSlots();
        void Save(int slotIndex);
        void Load(int slotIndex);
    }

    public sealed class GameplayUiContext
    {
        public GameplayUiContext(Func<GameClock> clock, CharacterRegistry characters,
            SelectionProbe selection, ResourceCatalog resources, ResourceInventoryRegistry inventories,
            SettlementResourceView storedResources, ProductionService production, ResearchService research,
            Tier1AiAgentRegistry aiAgents, WorkManager work, CharacterPresentationRegistry presentations,
            ManualMoveCommandService manualMoveCommands, WorldPileService worldPiles, BuildingRegistry buildings, BuildingPresentationCatalog buildingCatalog,
            BuildingPlacementController buildingPlacement, HealthAndFamilyService health,
            WeatherService weather, DynastyState dynasty, FamilyTreeQuery family, GameRunState gameRun,
            MilitaryUnitService military, MilitaryUnitRepresentationService militaryRepresentations,
            MilitaryUnitCommandService militaryCommands, BattleResolutionService battles, CombatService combat,
            PlayerControlService playerControl, ThirdPersonControlController thirdPerson,
            GlobalMapService globalMap, IGameplaySaveFacade saves,
            Action<bool> setPaused, Action<Game.Simulation.Time.GameSpeed> setSpeed,
            Action requestEnterGlobal, Action<WorldLocationId> requestEnterLocation,
            Action<StableEntityId> requestEnterBattle, Action<bool> setDeveloperUiVisible,
            IEnumerable<Behaviour> legacyDebugOverlays)
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
            Work = work ?? throw new ArgumentNullException(nameof(work));
            Presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            ManualMoveCommands = manualMoveCommands ?? throw new ArgumentNullException(nameof(manualMoveCommands));
            WorldPiles = worldPiles ?? throw new ArgumentNullException(nameof(worldPiles));
            Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            BuildingCatalog = buildingCatalog ?? throw new ArgumentNullException(nameof(buildingCatalog));
            BuildingPlacement = buildingPlacement ?? throw new ArgumentNullException(nameof(buildingPlacement));
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
            Dynasty = dynasty ?? throw new ArgumentNullException(nameof(dynasty));
            Family = family ?? throw new ArgumentNullException(nameof(family));
            GameRun = gameRun ?? throw new ArgumentNullException(nameof(gameRun));
            Military = military ?? throw new ArgumentNullException(nameof(military));
            MilitaryRepresentations = militaryRepresentations ?? throw new ArgumentNullException(nameof(militaryRepresentations));
            MilitaryCommands = militaryCommands ?? throw new ArgumentNullException(nameof(militaryCommands));
            Battles = battles ?? throw new ArgumentNullException(nameof(battles));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
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
        public WorkManager Work { get; }
        public CharacterPresentationRegistry Presentations { get; }
        public ManualMoveCommandService ManualMoveCommands { get; }
        public WorldPileService WorldPiles { get; }
        public BuildingRegistry Buildings { get; }
        public BuildingPresentationCatalog BuildingCatalog { get; }
        public BuildingPlacementController BuildingPlacement { get; }
        public HealthAndFamilyService Health { get; }
        public WeatherService Weather { get; }
        public DynastyState Dynasty { get; }
        public FamilyTreeQuery Family { get; }
        public GameRunState GameRun { get; }
        public MilitaryUnitService Military { get; }
        public MilitaryUnitRepresentationService MilitaryRepresentations { get; }
        public MilitaryUnitCommandService MilitaryCommands { get; }
        public BattleResolutionService Battles { get; }
        public CombatService Combat { get; }
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
    }
}
