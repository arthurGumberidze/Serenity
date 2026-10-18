using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Buildings;
using Game.Domain.AI;
using Game.Domain.Time;
using Game.Presentation.AI;
using Game.Presentation.Buildings;
using Game.Presentation.CameraControl;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Resources;
using Game.Simulation.AI;
using Game.Simulation.Time;
using Game.Presentation.Resources;
using Game.Presentation.Work;
using Game.Simulation.Work;
using Game.Simulation.Tiers;
using Game.Domain.Production;
using Game.Simulation.Production;
using Game.Presentation.Production;
using Game.Domain.Research;
using Game.Simulation.Research;
using Game.Presentation.Research;
using Game.Domain.Dynasties;
using Game.Simulation.Dynasties;
using Game.Presentation.Dynasties;
using Game.Domain.Health;
using Game.Simulation.Health;
using Game.Presentation.Health;
using Game.Domain.Weather;
using Game.Simulation.Weather;
using Game.Presentation.Weather;
using Game.Domain.Combat;
using Game.Simulation.Combat;
using Game.Presentation.Combat;
using Game.Domain.Military;
using Game.Simulation.Military;
using Game.Presentation.Military;
using Game.Domain.Control;
using Game.Simulation.Control;
using Game.Presentation.Control;
using Game.Presentation.Environment;
using Game.Presentation.UI;
using Game.Domain.World;
using Game.Domain.Events;
using Game.Domain.Campaign;
using Game.Simulation;
using Game.Simulation.World;
using Game.Simulation.Events;
using Game.Simulation.Campaign;
using Game.Simulation.Characters;
using Game.Domain.Domestic;
using Game.Simulation.Domestic;
using Game.Simulation.Diagnostics;
using Game.ECS.Tier2;
using Game.Infrastructure.Performance;
using Game.Infrastructure.Reliability;
using Game.Infrastructure.Integration;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure
{
    public sealed class LocalSceneCompositionRoot : MonoBehaviour
    {
        private const float TierPolicyRefreshIntervalSeconds = 0.5f;
        private const long WeatherRosterRefreshIntervalTicks = TimeSpan.TicksPerMinute;
        [SerializeField] private LocalGameplayInputSource input;
        [SerializeField] private RtsCameraController cameraController;
        [SerializeField] private WorldPointerRaycaster pointerRaycaster;
        [SerializeField] private SelectionProbe selectionProbe;
        [SerializeField] private CharacterPresentationCatalog characterCatalog;
        [SerializeField] private BuildingPresentationCatalog buildingCatalog;
        [SerializeField] private BuildingPlacementController buildingPlacementController;
        [SerializeField] private P95NaturalAtmosphereProfile naturalAtmosphereProfile;
        [SerializeField, Min(0.1f)] private float buildingGridSize = 1f;
        [SerializeField] private Vector3 buildingGridOrigin;
        [SerializeField] private long developmentWorldSimulationSeed = 20261006L;

        private readonly List<CharacterPresenter> demoPresenters = new List<CharacterPresenter>();

        public LocalGameplayInputSource Input => input;
        public RtsCameraController CameraController => cameraController;
        public WorldPointerRaycaster PointerRaycaster => pointerRaycaster;
        public SelectionProbe SelectionProbe => selectionProbe;
        public P95NaturalAtmosphereProfile NaturalAtmosphereProfile => naturalAtmosphereProfile;
        public CharacterRegistry Characters { get; private set; }
        public CharacterPresentationRegistry CharacterPresentations { get; private set; }
        public CharacterPresentationSpawner CharacterSpawner { get; private set; }
        public CharacterAppearanceService Appearances { get; private set; }
        public Character MaleDemoCharacter { get; private set; }
        public Character FemaleDemoCharacter { get; private set; }
        public Character FirstDemoHeir { get; private set; }
        public Character SecondDemoHeir { get; private set; }
        public Tier1CharacterAdapter Tier1Adapter { get; private set; }
        public IReadOnlyList<CharacterPresenter> DemoPresenters => demoPresenters;
        public BuildingRegistry Buildings { get; private set; }
        public BuildingOccupancyGrid BuildingOccupancy { get; private set; }
        public BuildingPlacementService BuildingPlacementService { get; private set; }
        public BuildingPresentationRegistry BuildingPresentations { get; private set; }
        public BuildingPresentationSpawner BuildingSpawner { get; private set; }
        public BuildingPresentationCatalog BuildingCatalog => buildingCatalog;
        public BuildingPlacementController BuildingPlacementController => buildingPlacementController;
        public LocalInteractionMode InteractionMode { get; private set; }
        public ResourceCatalog Resources { get; private set; }
        public ResourceInventoryRegistry Inventories { get; private set; }
        public ResourceTransferService ResourceTransfers { get; private set; }
        public StorageService Storage { get; private set; }
        public WorldPileService WorldPiles { get; private set; }
        public SettlementResourceView StoredResources { get; private set; }
        public ConstructionFundingService Construction { get; private set; }
        public Building DemoStorageBuilding { get; private set; }
        public BuildingPresenter DemoStoragePresenter { get; private set; }
        public Building DemoShelterBuilding { get; private set; }
        public BuildingPresenter DemoShelterPresenter { get; private set; }
        public Building DemoBarracksBuilding { get; private set; }
        public BuildingPresenter DemoBarracksPresenter { get; private set; }
        public StoneAgeReferenceCampPresentation ReferenceCamp { get; private set; }
        public P5InhabitableRegionPresentation InhabitableRegion { get; private set; }
        public NavMeshSurface NavigationSurface { get; private set; }
        public GameClock Clock { get; private set; }
        public Tier1AiAgentRegistry AiAgents { get; private set; }
        public HaulClaimRegistry HaulClaims { get; private set; }
        public Tier1AiScheduler AiScheduler { get; private set; }
        public Tier1AiRuntimeDriver AiRuntime { get; private set; }
        public WorkManager Work { get; private set; }
        public DirectWorkCommandService DirectWorkCommands { get; private set; }
        public ContextCommandService ContextCommands { get; private set; }
        public WorkDebugOverlay WorkDebug { get; private set; }
        public ManualMoveCommandService ManualMoveCommands { get; private set; }
        public ManualMoveInputController ManualMoveInput { get; private set; }
        public Tier2Runtime Tier2Runtime { get; private set; }
        public Tier3CharacterRegistry Tier3Characters { get; private set; }
        public TierManager Tiers { get; private set; }
        public TierDistancePolicy TierPolicy { get; private set; }
        public OffCameraSimulationService OffCameraSimulation { get; private set; }
        public Tier3CharacterAdapter Tier3Adapter { get; private set; }
        public ProductionService Production { get; private set; }
        public ResourceEcologyService ResourceEcology { get; private set; }
        public IndustrialExtractionService Industry { get; private set; }
        public ProductionCatalog ProductionRecipes { get; private set; }
        public TechnologyCatalog Technologies { get; private set; }
        public ResearchService Research { get; private set; }
        public ResearchDebugOverlay ResearchDebug { get; private set; }
        public BuildingDebugSelector BuildingDebug { get; private set; }
        public TimeDebugOverlay TimeDebug { get; private set; }
        public DynastyState Dynasty { get; private set; }
        public PlayerRealmState PlayerRealm { get; private set; }
        public GameRunState GameRun { get; private set; }
        public SuccessionService Succession { get; private set; }
        public DynastyDebugOverlay DynastyDebug { get; private set; }
        public HealthAndFamilyService HealthFamily { get; private set; }
        public DomesticService Domestic { get; private set; }
        public HealthFamilyDebugOverlay HealthFamilyDebug { get; private set; }
        public WeatherService Weather { get; private set; }
        public WeatherDebugOverlay WeatherDebug { get; private set; }
        public WeatherPresentationController WeatherPresentation { get; private set; }
        public CombatService Combat { get; private set; }
        public CombatCommandService CombatCommands { get; private set; }
        public CombatDebugOverlay CombatDebug { get; private set; }
        public MilitaryUnitService MilitaryUnits { get; private set; }
        public MilitaryUnitRepresentationService MilitaryRepresentations { get; private set; }
        public MilitaryUnitCommandService MilitaryCommands { get; private set; }
        public MilitaryUnitCombatService MilitaryCombat { get; private set; }
        public MilitaryTrainingService MilitaryTraining { get; private set; }
        public MilitaryAttackCommandService MilitaryAttack { get; private set; }
        public SettlementFlagCaptureService SettlementCapture { get; private set; }
        public DiagnosticService Diagnostics { get; private set; }
        public BattleResolutionService Battles { get; private set; }
        public BuildingDamageService BuildingDamage { get; private set; }
        public BuildingDamageDebugOverlay BuildingDamageDebug { get; private set; }
        public MilitaryUnitDebugOverlay MilitaryDebug { get; private set; }
        public PlayerControlService PlayerControl { get; private set; }
        public ThirdPersonControlController ThirdPersonControl { get; private set; }
        public GlobalMapService GlobalMap { get; private set; }
        public ConnectedWorldService ConnectedWorld { get; private set; }
        public AiStrategicScheduler StrategicAi { get; private set; }
        public EventService Events { get; private set; }
        public AiFactionRuntime StarterEnemyAi { get; private set; }
        public GameModeTransitionService ModeTransitions { get; private set; }
        public SettlementId StarterSettlementId { get; private set; }
        public GlobalMapRuntimeBridge GlobalMapBridge { get; private set; }
        public SaveCoordinator Saves { get; private set; }
        public LocalGameplaySaveFacade PlayerSaves { get; private set; }
        public GameplayUiController GameplayUi { get; private set; }
        public U29BenchmarkController PerformanceBenchmark { get; private set; }
        public U30SoakController SoakValidation { get; private set; }
        public U31PlayerSmokeController PlayerSmoke { get; private set; }
        public P3PlayerSmokeController P3PlayerSmoke { get; private set; }
        public P5PlayerSmokeController P5PlayerSmoke { get; private set; }
        public P6PlayerSmokeController P6PlayerSmoke { get; private set; }
        public P7PlayerSmokeController P7PlayerSmoke { get; private set; }
        public P81PlayerSmokeController P81PlayerSmoke { get; private set; }
        public P82PlayerSmokeController P82PlayerSmoke { get; private set; }
        public P83PlayerSmokeController P83PlayerSmoke { get; private set; }
        public P91PlayerSmokeController P91PlayerSmoke { get; private set; }
        public P92PlayerSmokeController P92PlayerSmoke { get; private set; }
        public P93PlayerSmokeController P93PlayerSmoke { get; private set; }
        public P94PlayerSmokeController P94PlayerSmoke { get; private set; }
        public P95PlayerSmokeController P95PlayerSmoke { get; private set; }
        public P92RealisticGameplayRegion RealisticGameplayRegion { get; private set; }
        public P93PopulatedBiomeWorld PopulatedBiomeWorld { get; private set; }
        public P94StoneAgeSettlement StoneAgeSettlement { get; private set; }
        public P94WorkOrderIndicatorSystem WorkOrderIndicators { get; private set; }
        public NewGameConfiguration ActiveNewGameConfiguration { get; private set; }
        public NewGameBootstrapResult NewGame { get; private set; }
        public CampaignProgressService CampaignProgress { get; private set; }
        public IReadOnlyList<StableEntityId> PlayerCharacterIds => NewGame.PlayerCharacters.Select(x => x.Id).ToArray();
        public Building DemoCropPlotBuilding { get; private set; }
        public BuildingPresenter DemoCropPlotPresenter { get; private set; }
        public IReadOnlyList<WorldResourcePilePresenter> DemoPilePresenters => demoPilePresenters;
        private readonly List<WorldResourcePilePresenter> demoPilePresenters = new List<WorldResourcePilePresenter>();
        private readonly List<ProductionSourcePresenter> productionPresenters = new List<ProductionSourcePresenter>();
        private readonly List<StableEntityId> abstractWorkerBuffer = new List<StableEntityId>();
        private readonly List<StableEntityId> tier2WorkerBuffer = new List<StableEntityId>();
        private readonly List<StableEntityId> tier3WorkerCache = new List<StableEntityId>();
        private readonly List<StableEntityId> outdoorWorkerBuffer = new List<StableEntityId>();
        private long tier3WorkerCacheRevision = -1;
        private long nextWeatherRosterRefreshTick;
        private float nextTierPolicyRefreshTime;
        private P104ResourceRespawnSurface resourceRespawnSurface;

        public void Configure(LocalGameplayInputSource inputSource, RtsCameraController controller,
            WorldPointerRaycaster raycaster, SelectionProbe selection, CharacterPresentationCatalog catalog,
            BuildingPresentationCatalog configuredBuildingCatalog, BuildingPlacementController placementController,
            float gridSize = 1f, Vector3 gridOrigin = default)
        {
            input = inputSource;
            cameraController = controller;
            pointerRaycaster = raycaster;
            selectionProbe = selection;
            characterCatalog = catalog;
            buildingCatalog = configuredBuildingCatalog;
            buildingPlacementController = placementController;
            buildingGridSize = gridSize;
            buildingGridOrigin = gridOrigin;
        }

        public IEnumerable<InventoryOwner> FundingSources()
        {
            // Explicit physical owners. The ordering is deterministic within this development session.
            foreach (var pile in WorldPiles.All.OrderBy(x => x.ResourceId).ThenBy(x => x.Id.ToString(), StringComparer.Ordinal))
                yield return pile.InventoryOwner;
            foreach (var inventory in Inventories.All.OrderBy(x => x.Owner.Id.ToString(), StringComparer.Ordinal))
                if (inventory.Owner.Kind == InventoryOwnerKind.BuildingStorage) yield return inventory.Owner;
        }

        private void Awake()
        {
            if (input == null || cameraController == null || pointerRaycaster == null || selectionProbe == null ||
                characterCatalog == null || buildingCatalog == null || buildingPlacementController == null)
                throw new InvalidOperationException("Local scene composition references must be assigned explicitly.");

            var requestedConfiguration = NewGameLaunchState.Consume(developmentWorldSimulationSeed);
            var resolvedWorldSeed = U30SoakConfig.ResolveWorldSeed(Environment.GetCommandLineArgs(),
                requestedConfiguration.WorldSeed);
            ActiveNewGameConfiguration = resolvedWorldSeed == requestedConfiguration.WorldSeed
                ? requestedConfiguration
                : new NewGameConfiguration(resolvedWorldSeed, requestedConfiguration.ConfigurationId,
                    requestedConfiguration.RivalKnownAtStart, requestedConfiguration.RulerName,
                    requestedConfiguration.DynastyName, requestedConfiguration.RulerSex,
                    requestedConfiguration.RulerAppearanceProfileId, requestedConfiguration.RulerAppearance);
            var appearanceCatalog = characterCatalog.CreateDefinitionCatalog();
            NewGame = NewGameBootstrap.Create(ActiveNewGameConfiguration, appearanceCatalog);
            var worldSeed = ActiveNewGameConfiguration.WorldSeed;
            Characters = new CharacterRegistry();
            Appearances = new CharacterAppearanceService(Characters, appearanceCatalog, worldSeed);
            HealthFamily = new HealthAndFamilyService(Characters, HealthConditionCatalog.CreateStoneAge(),
                worldSeed);
            Weather = new WeatherService(worldSeed, ClimateProfile.Temperate, HealthFamily);
            CharacterPresentations = new CharacterPresentationRegistry();
            CharacterSpawner = new CharacterPresentationSpawner(characterCatalog, CharacterPresentations, Appearances,
                presentationPositionProjector: ProjectToGameplayGround);

            InteractionMode = new LocalInteractionMode();
            Buildings = new BuildingRegistry();
            BuildingOccupancy = new BuildingOccupancyGrid();
            BuildingPlacementService = new BuildingPlacementService(Buildings, BuildingOccupancy,
                new BuildingGridBounds(-246, -246, 246, 246));
            if (buildingCatalog.ResourceCatalog == null) throw new InvalidOperationException("Building catalog has no resource catalog.");
            Resources = buildingCatalog.ResourceCatalog.CreateCatalog();
            InhabitableRegion = GetComponent<P5InhabitableRegionPresentation>();
            InhabitableRegion?.ValidateConfiguration();
            RealisticGameplayRegion = FindAnyObjectByType<P92RealisticGameplayRegion>();
            RealisticGameplayRegion?.ValidateConfiguration();
            PopulatedBiomeWorld = FindAnyObjectByType<P93PopulatedBiomeWorld>();
            PopulatedBiomeWorld?.ValidateConfiguration();
            if (PopulatedBiomeWorld != null)
                gameObject.AddComponent<StableEnvironmentLodController>().Initialize(PopulatedBiomeWorld.CanopyTrees);
            StoneAgeSettlement = FindAnyObjectByType<P94StoneAgeSettlement>();
            StoneAgeSettlement?.ValidateConfiguration();
            Inventories = new ResourceInventoryRegistry();
            ResourceTransfers = new ResourceTransferService(Inventories);
            Storage = new StorageService(Inventories, Resources);
            WorldPiles = new WorldPileService(Resources, Inventories);
            StoredResources = new SettlementResourceView(Inventories);
            ProductionRecipes = ProductionCatalog.CreateStoneAge(Resources);
            Technologies = TechnologyCatalog.CreateStoneAge(ProductionRecipes,
                buildingCatalog.Entries.Select(entry => new BuildingDefinitionId(entry.DefinitionId)));
            Research = new ResearchService(Technologies, new CharacterResearchRateProvider(Characters, HealthFamily));
            Construction = new ConstructionFundingService(BuildingPlacementService, Inventories, Resources, Storage,
                Research);
            gameObject.AddComponent<ResourceDebugOverlay>().Initialize(Inventories, Resources, pointerRaycaster);
            BuildingPresentations = new BuildingPresentationRegistry();
            BuildingSpawner = new BuildingPresentationSpawner(buildingCatalog, BuildingPresentations,
                buildingGridSize, buildingGridOrigin, null, Inventories,
                RealisticGameplayRegion == null ? null : position =>
                    RealisticGameplayRegion.ProjectToGround(position).y);
            selectionProbe.Configure(input, pointerRaycaster, InteractionMode);
            buildingPlacementController.Initialize(BuildingPlacementService, BuildingSpawner, InteractionMode,
                Construction, FundingSources);
            buildingPlacementController.BuildingPlaced += OnBuildingPlaced;

            // DEV_BOOTSTRAP_ONLY: finite canonical piles for the U09 LocalGameplay slice.
            SpawnDemoPile("wood_log", 30, new GridCoordinate(-8, -2), new Color(0.45f, 0.25f, 0.1f), 0);
            SpawnDemoPile("plant_fiber", 20, new GridCoordinate(-6, -2), new Color(0.45f, 0.7f, 0.2f), 1);
            SpawnDemoPile("stone", 10, new GridCoordinate(-4, -2), new Color(0.45f, 0.45f, 0.45f), 2);
            SpawnDemoPile("hide", 5, new GridCoordinate(-2, -2), new Color(0.7f, 0.45f, 0.25f), 3);

            SpawnDemoStorage();
            SpawnDemoShelter();
            SpawnDemoBarracks();
            BuildNavigationSurface();

            var playerCharacters = NewGame.PlayerCharacters.Select(Character.Restore).ToArray();
            foreach (var character in playerCharacters)
            {
                Characters.Add(character);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, character.Id), Resources, 128));
            }
            Appearances.RestoreState(NewGame.Appearances);
            MaleDemoCharacter = Characters.Get(NewGame.MonarchId);
            FemaleDemoCharacter = Characters.Get(NewGame.SpouseId);
            FirstDemoHeir = Characters.Get(NewGame.FirstHeirId);
            SecondDemoHeir = Characters.Get(NewGame.SecondHeirId);
            Domestic = new DomesticService(Characters, Buildings, HealthFamily);
            RegisterStarterDomestic();
            Inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, MaleDemoCharacter.Id))
                .TryAdd(new ResourceId("berries"), new ResourceQuantity(60));
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter,
                ProjectToGameplayGround(new Vector3(-3f, 0f, 0f)), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter,
                ProjectToGameplayGround(new Vector3(3f, 0f, 0f)), Quaternion.identity));
            ReferenceCamp = GetComponent<StoneAgeReferenceCampPresentation>();
            ReferenceCamp?.Initialize(demoPresenters[0], demoPresenters[1], DemoShelterPresenter, DemoStoragePresenter);

            Production = new ProductionService(ProductionRecipes, Resources, Inventories, Buildings, ResourceTransfers,
                researchUnlocks: Research, capabilityProvider: HealthFamily, weatherModifierProvider: Weather);
            Production.Population = Characters.Count;
            ResourceEcology = new ResourceEcologyService();
            Industry = new IndustrialExtractionService(Resources, Inventories, CreateIndustrialDefinitions());
            Production.AttachNaturalResources(ResourceEcology, Industry);
            SpawnRegionProductionSources();
            SpawnP104NaturalSources();
            SpawnP104IndustrialZones();
            BindPopulatedWorldAffordances();
            resourceRespawnSurface = new P104ResourceRespawnSurface(FindAnyObjectByType<P102WorldPlacementSurface>());
            gameObject.AddComponent<ProductionDebugOverlay>().Initialize(Production, Resources, pointerRaycaster);
            gameObject.AddComponent<P104ResourceHoverLabel>().Initialize(selectionProbe, Production);

            Clock = new GameClock(new GameTimeState(0, Character.BiologicalTicksPerYear * 20L, 1, false));
            Dynasty = DynastyState.Restore(NewGame.Dynasty);
            PlayerRealm = new PlayerRealmState(MaleDemoCharacter.Id);
            GameRun = new GameRunState();
            Succession = new SuccessionService(Characters, Dynasty, PlayerRealm, GameRun);
            HealthFamily.BindSuccession(Succession);
            AiAgents = new Tier1AiAgentRegistry();
            HaulClaims = new HaulClaimRegistry();
            Work = new WorkManager(Characters);
            var haulJobs = new HaulWorldQuery(Inventories, WorldPiles, Buildings, HaulClaims, buildingGridSize,
                new WorldPosition(buildingGridOrigin.x, buildingGridOrigin.y, buildingGridOrigin.z));
            var aiWorld = new Tier1AiWorld(haulJobs, HaulClaims, ResourceTransfers, Production, Research, Buildings);
            AiScheduler = new Tier1AiScheduler(Characters, AiAgents, aiWorld, workManager: Work,
                capabilityProvider: HealthFamily, domesticService: Domestic);
            AiScheduler.SetAutonomousWorkMode(Tier1AiScheduler.AutonomousWorkMode.NeedsOnly);
            AiRuntime = gameObject.AddComponent<Tier1AiRuntimeDriver>();
            AiRuntime.Initialize(Clock, AiScheduler, () =>
                Weather.GetWeatherAtCalendarTick(Clock.State.CalendarTicks).Modifiers.MovementPerThousand);
            AiRuntime.Register(MaleDemoCharacter, demoPresenters[0]);
            AiRuntime.Register(FemaleDemoCharacter, demoPresenters[1], new Tier1Needs(0d, 0.25d));
            ResearchDebug = gameObject.AddComponent<ResearchDebugOverlay>();
            ResearchDebug.Initialize(Research, selectionProbe, AiAgents, Characters, pointerRaycaster);
            BuildingDebug = gameObject.AddComponent<BuildingDebugSelector>();
            BuildingDebug.Initialize(buildingCatalog, Research, buildingPlacementController, pointerRaycaster);
            TimeDebug = gameObject.AddComponent<TimeDebugOverlay>();
            TimeDebug.Initialize(input, AiRuntime, pointerRaycaster);
            WeatherDebug = gameObject.AddComponent<WeatherDebugOverlay>();
            WeatherDebug.Initialize(Weather, Clock, pointerRaycaster);
            WeatherPresentation = gameObject.AddComponent<WeatherPresentationController>();
            WeatherPresentation.Initialize(Weather, ReferenceCamp != null ? ReferenceCamp.RainParticleMaterial : null,
                InhabitableRegion, naturalAtmosphereProfile);
            WeatherPresentation.Refresh(Clock.State.CalendarTicks);
            DynastyDebug = gameObject.AddComponent<DynastyDebugOverlay>();
            DynastyDebug.Initialize(Succession, Characters, selectionProbe, Clock, pointerRaycaster);
            HealthFamilyDebug = gameObject.AddComponent<HealthFamilyDebugOverlay>();
            HealthFamilyDebug.Initialize(HealthFamily, Characters, Succession.Family, selectionProbe, Clock,
                pointerRaycaster);
            Tier2Runtime = new Tier2Runtime("Serenity Local Tier 2");
            Tier3Characters = new Tier3CharacterRegistry();
            OffCameraSimulation = new OffCameraSimulationService(
                OffCameraSimulationSettings.ForWorld(worldSeed));
            Tier1Adapter = new Tier1CharacterAdapter(characterCatalog, CharacterPresentations, CharacterSpawner,
                AiRuntime, AiAgents);
            var tier2 = new Tier2CharacterAdapter(Tier2Runtime);
            Tier3Adapter = new Tier3CharacterAdapter(Tier3Characters, OffCameraSimulation,
                () => new SimulationTimePoint(Clock.State.CalendarTicks, Clock.State.BiologicalTicks));
            Tiers = new TierManager(Characters, Tier1Adapter, tier2, Tier3Adapter);
            Tiers.RegisterExisting(MaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            Tiers.RegisterExisting(FemaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            for (var i = 2; i < playerCharacters.Length; i++)
                Tiers.Materialize(playerCharacters[i], CharacterSimulationTier.Tier3,
                    new CharacterRuntimeState(playerCharacters[i].Id, new WorldPosition(-6 + i * 1.5f, 0, 4),
                        default, false, "starter_settlement", Clock.State.CalendarTicks, Clock.State.BiologicalTicks));
            DirectWorkCommands = new DirectWorkCommandService(Characters, AiAgents, AiScheduler, Work);
            DirectWorkCommands.EnsureRoleGroups();
            ContextCommands = new ContextCommandService(Work, AiScheduler);
            if (StoneAgeSettlement != null)
            {
                WorkOrderIndicators = gameObject.AddComponent<P94WorkOrderIndicatorSystem>();
                WorkOrderIndicators.Initialize(Work, StoneAgeSettlement);
            }
            TierPolicy = new TierDistancePolicy(Tiers);
            Combat = new CombatService(Characters, HealthFamily, new Tier1CombatPositionProvider(AiRuntime),
                worldSeed, Tiers);
            AiRuntime.SetCombatActivityProvider(characterId => Combat.TryGetCombatant(characterId, out _));
            foreach (var character in playerCharacters) Combat.RegisterSide(character.Id, new CombatSideId("player"));
            CombatCommands = new CombatCommandService(Combat);
            HealthFamily.ChildBorn += OnChildBorn;
            ManualMoveCommands = new ManualMoveCommandService(Work, Tiers);
            MilitaryUnits = new MilitaryUnitService(Characters, HealthFamily);
            MilitaryRepresentations = new MilitaryUnitRepresentationService(MilitaryUnits, Tiers, Combat,
                Characters, HealthFamily);
            MilitaryCommands = new MilitaryUnitCommandService(MilitaryUnits, ManualMoveCommands);
            MilitaryCombat = new MilitaryUnitCombatService(MilitaryUnits, Combat);
            PlayerControl = new PlayerControlService(Characters, HealthFamily, Tiers, Dynasty, MilitaryUnits,
                AiScheduler, new MilitarySideId("player"));
            Battles = new BattleResolutionService(Characters, Combat, MilitaryUnits, MilitaryRepresentations,
                MilitaryCombat, PlayerControl);
            GlobalMap = new GlobalMapService(StarterGlobalMap.Create(Clock.State.CalendarTicks, worldSeed), MilitaryUnits,
                Battles);
            ConnectedWorld = new ConnectedWorldService(GlobalMap.Locations, Clock.State.CalendarTicks);
            StarterSettlementId = GlobalMap.Settlements.Single(x => x.LocationId == new WorldLocationId("starter_settlement")).Id;
            InhabitableRegion?.BindCanonicalMap(GlobalMap.Locations.Select(x => x.Id.Value));
            SynchronizeStarterSettlement();
            ModeTransitions = new GameModeTransitionService(GlobalMap, Tiers, MilitaryUnits,
                MilitaryRepresentations, Battles, PlayerControl);
            BuildingDamage = new BuildingDamageService(Buildings,
                buildingCatalog.Entries.Select(entry => buildingCatalog.ResolveDefinition(new BuildingDefinitionId(entry.DefinitionId))),
                Production, Inventories, WorldPiles, Weather, Combat, BuildingOccupancy);
            var strategicDefinitions = buildingCatalog.Entries.Select(entry =>
                buildingCatalog.ResolveDefinition(new BuildingDefinitionId(entry.DefinitionId))).ToArray();
            StarterEnemyAi = AiRuntimeBootstrap.CreateStarterEnemy(worldSeed,
                Clock.State.CalendarTicks, Clock.State.BiologicalTicks, Characters, HealthFamily, Tiers, GlobalMap, Buildings,
                BuildingPlacementService, Resources, ProductionRecipes, Technologies, strategicDefinitions, Weather);
            Appearances.EnsureDefaults(Characters.GetAll());
            HealthFamily.BindSuccession(StarterEnemyAi.Succession);
            BuildingDamage.RegisterEconomyScope(StarterEnemyAi.Production, StarterEnemyAi.Inventories,
                StarterEnemyAi.WorldPiles);
            StrategicAi = new AiStrategicScheduler(worldSeed, Characters, HealthFamily,
                MilitaryUnits, GlobalMap, Battles, Buildings, BuildingDamage);
            StrategicAi.AddFaction(StarterEnemyAi);
            MilitaryTraining = new MilitaryTrainingService(Characters, HealthFamily, Buildings, Inventories,
                MilitaryUnits, Appearances, worldSeed);
            MilitaryAttack = new MilitaryAttackCommandService(Characters, Buildings, MilitaryUnits,
                MilitaryCommands, MilitaryCombat, BuildingDamage, AiAgents, MilitaryTraining, worldSeed);
            SettlementCapture = new SettlementFlagCaptureService(worldSeed, GlobalMap, Buildings, BuildingDamage,
                Characters, MilitaryUnits);
            Diagnostics = new DiagnosticService(worldSeed, Research, Resources, Inventories, Buildings,
                Characters, Appearances, HealthFamily, Domestic, MilitaryUnits, MilitaryTraining, Tiers, Combat,
                () => Clock.State.CalendarTicks, () => Clock.State.BiologicalTicks);
            RegisterSettlementCaptureFlags();
            MilitaryAttack.BindSettlementCapture(SettlementCapture);
            var eventTargets = new CharacterEventTargetSource(Characters,
                () => Characters.GetAll().Select(x => x.Id).Except(StarterEnemyAi.Residents),
                () => StarterEnemyAi.Residents,
                () => Clock.State.BiologicalTicks);
            Events = new EventService(StoneAgeEventCatalog.Create(), eventTargets,
                new CanonicalEventEffectExecutor(Characters, HealthFamily), worldSeed,
                Clock.State.CalendarTicks);
            CampaignProgress = new CampaignProgressService(ActiveNewGameConfiguration, new CombatSideId("player"),
                StarterEnemyAi, GlobalMap, MilitaryUnits, StrategicAi, Battles, Clock.State.CalendarTicks, GameRun);
            gameObject.AddComponent<StrategicAiDebugOverlay>().Initialize(StrategicAi, pointerRaycaster,
                () => AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1d / (int)Clock.Speed)));
            BuildingDamage.BuildingChanged += OnBuildingChanged;
            Tiers.BindTransitionGuard(new CharacterTierTransitionGuardSet(Combat, PlayerControl));
            AiScheduler.BindExternalActivityOwner(new CharacterActivityOwnerSet(Combat, PlayerControl, MilitaryTraining));
            Combat.BindMovementSuppression(PlayerControl);
            Combat.BindCombatModifier(MilitaryUnits.ConsumeCombatSupplyForCharacter);
            AiRuntime.SimulationAdvanced += OnSimulationAdvanced;
            gameObject.AddComponent<Tier1AiDebugOverlay>().Initialize(selectionProbe, AiAgents, Inventories,
                Resources, HaulClaims, pointerRaycaster, Tiers, Clock, OffCameraSimulation);
            WorkDebug = gameObject.AddComponent<WorkDebugOverlay>();
            WorkDebug.Initialize(Work, selectionProbe, CharacterPresentations, WorldPiles, Inventories, Buildings,
                input, pointerRaycaster, ManualMoveCommands);
            ManualMoveInput = gameObject.AddComponent<ManualMoveInputController>();
            ManualMoveInput.Initialize(input, pointerRaycaster, selectionProbe, WorkDebug,
                buildingPlacementController, ManualMoveCommands, CombatCommands,
                () => Clock.State.CalendarTicks);
            CombatDebug = gameObject.AddComponent<CombatDebugOverlay>();
            CombatDebug.Initialize(Combat, Characters, HealthFamily, selectionProbe, pointerRaycaster,
                () => { StartDemoUnitBattle(BattleMode.Physical); }, Battles, () => Clock.State.CalendarTicks,
                () => AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1d / (int)Clock.Speed)));
            MilitaryDebug = gameObject.AddComponent<MilitaryUnitDebugOverlay>();
            MilitaryDebug.Initialize(MilitaryUnits, MilitaryRepresentations, MilitaryCommands, MilitaryCombat,
                selectionProbe, pointerRaycaster, () => Clock.State.CalendarTicks, Battles);
            BuildingDamageDebug = gameObject.AddComponent<BuildingDamageDebugOverlay>();
            BuildingDamageDebug.Initialize(BuildingDamage, Buildings, buildingCatalog, Inventories, Production,
                Combat, Clock, pointerRaycaster);
            ThirdPersonControl = gameObject.AddComponent<ThirdPersonControlController>();
            ThirdPersonControl.Initialize(input, selectionProbe, CharacterPresentations, AiRuntime,
                cameraController, buildingPlacementController, PlayerControl, HealthFamily, MilitaryUnits, Work,
                pointerRaycaster, Combat);
            GlobalMapBridge = gameObject.AddComponent<GlobalMapRuntimeBridge>();
            GlobalMapBridge.Initialize(this);

            var configuredSaveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            var saveDirectory = string.IsNullOrWhiteSpace(configuredSaveDirectory)
                ? System.IO.Path.Combine(Application.persistentDataPath, "Serenity", "P10", "Saves")
                : configuredSaveDirectory;
            var saveStore = new FileSaveStore(saveDirectory);
            Saves = new SaveCoordinator(ActiveNewGameConfiguration.SessionId, Clock, saveStore, Production, Research, Succession,
                HealthFamily, Weather, Combat, MilitaryUnits, PlayerControl, BuildingDamage, GlobalMap, StrategicAi,
                Events, CampaignProgress, Appearances, Work, ConnectedWorld, Domestic, Tiers,
                MilitaryTraining, SettlementCapture, MilitaryAttack, Diagnostics);
            PlayerSaves = new LocalGameplaySaveFacade(this, Saves, saveStore);
            var legacyOverlays = GetComponents<Behaviour>().Where(component =>
                component != null && component.GetType().Name.IndexOf("Debug", StringComparison.Ordinal) >= 0)
                .ToArray();
            GameplayUi = gameObject.AddComponent<GameplayUiController>();
            GameplayUi.Initialize(new GameplayUiContext(() => Clock, Characters, selectionProbe, Resources,
                    Inventories, StoredResources, Production, Research, AiAgents, AiScheduler, Work, DirectWorkCommands,
                    ContextCommands, Tiers, CharacterPresentations,
                    ManualMoveCommands, WorldPiles, Buildings, buildingCatalog, buildingPlacementController,
                    HealthFamily, Domestic, Weather, Dynasty, Succession.Family, GameRun, MilitaryUnits,
                    MilitaryRepresentations, MilitaryCommands, MilitaryTraining, MilitaryAttack, SettlementCapture,
                    Battles, Combat, Diagnostics, PlayerControl, ThirdPersonControl,
                    GlobalMap, PlayerSaves, AiRuntime.SetPaused, AiRuntime.SetSpeed,
                    GlobalMapBridge.RequestEnterGlobal, GlobalMapBridge.RequestEnterLocation,
                    GlobalMapBridge.RequestEnterBattle, GlobalMapBridge.SetDeveloperUiVisible, legacyOverlays,
                    Events, CampaignProgress, StartNewGame, ReturnToMainMenu,
                    () => Characters.GetAll().Where(x => !StarterEnemyAi.Residents.Contains(x.Id)).Select(x => x.Id).ToArray()),
                pointerRaycaster, input);
            ManualMoveInput.EnableContextMenus();

            var userSettings = UserSettingsStore.CreateDefault().Load().Settings;
            UserSettingsRuntime.ApplyGameplay(userSettings, input, cameraController, GameplayUi);

            if (SessionLaunchState.TryConsumeLoad(out var startupSlot))
            {
                try
                {
                    PlayerSaves.Load(startupSlot);
                }
                catch (Exception exception) when (exception is System.IO.IOException ||
                                                  exception is UnauthorizedAccessException ||
                                                  exception is System.IO.InvalidDataException ||
                                                  exception is NotSupportedException ||
                                                  exception is ArgumentException ||
                                                  exception is InvalidOperationException)
                {
                    Debug.LogException(exception);
                    SessionLaunchState.ReportLoadFailure(startupSlot);
                    SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                    return;
                }
            }
            PerformanceBenchmark = U29BenchmarkController.AttachIfRequested(this);
            SoakValidation = U30SoakController.AttachIfRequested(this);
            PlayerSmoke = U31PlayerSmokeController.AttachIfRequested(this);
            P3PlayerSmoke = P3PlayerSmokeController.AttachIfRequested(this);
            P5PlayerSmoke = P5PlayerSmokeController.AttachIfRequested(this);
            P6PlayerSmoke = P6PlayerSmokeController.AttachIfRequested(this);
            P7PlayerSmoke = P7PlayerSmokeController.AttachIfRequested(this);
            P81PlayerSmoke = P81PlayerSmokeController.AttachIfRequested(this);
            P82PlayerSmoke = P82PlayerSmokeController.AttachIfRequested(this);
            P83PlayerSmoke = P83PlayerSmokeController.AttachIfRequested(this);
            P91PlayerSmoke = P91PlayerSmokeController.AttachIfRequested(this);
            P92PlayerSmoke = P92PlayerSmokeController.AttachIfRequested(this);
            P93PlayerSmoke = P93PlayerSmokeController.AttachIfRequested(this);
            P94PlayerSmoke = P94PlayerSmokeController.AttachIfRequested(this);
            P95PlayerSmoke = P95PlayerSmokeController.AttachIfRequested(this);
        }

        private void OnDestroy()
        {
            if (AiRuntime != null) AiRuntime.SimulationAdvanced -= OnSimulationAdvanced;
            if (HealthFamily != null) HealthFamily.ChildBorn -= OnChildBorn;
            if (buildingPlacementController != null) buildingPlacementController.BuildingPlaced -= OnBuildingPlaced;
            if (BuildingDamage != null) BuildingDamage.BuildingChanged -= OnBuildingChanged;
            Tier2Runtime?.Dispose();
        }

        private void OnSimulationAdvanced(GameTimeAdvance advance)
        {
            ConnectedWorld?.AdvanceTo(Clock.State.CalendarTicks);
            if (ConnectedWorld != null)
                foreach (var presenter in demoPresenters.Where(x => x != null && x.gameObject.activeInHierarchy))
                    ConnectedWorld.ReportPhysicalPresence(presenter.transform.position.x, presenter.transform.position.z);
            DirectWorkCommands?.ReevaluateQueued();
            PerformanceBenchmark?.BeginSimulationAllocationSample();
            var targetCalendarTick = Clock.State.CalendarTicks;
            var startCalendarTick = checked(targetCalendarTick - advance.CalendarDelta.Ticks);
            Tier2Runtime.Step(advance, Weather.GetMovementModifierForInterval(startCalendarTick, targetCalendarTick));
            PerformanceBenchmark?.RecordTier2Allocation();
            Tier3Adapter.AdvanceAllToCurrent();
            Tier2Runtime.AdvanceResearch(Research, targetCalendarTick);
            Tier3Adapter.AdvanceResearchToCurrent(Research);
            PerformanceBenchmark?.RecordTier3AndResearchAllocation();
            tier2WorkerBuffer.Clear();
            Tier2Runtime.ConsumeProductionWorkers(tier2WorkerBuffer);
            RefreshTier3WorkerCache();
            MergeSortedDistinct(tier2WorkerBuffer, tier3WorkerCache, abstractWorkerBuffer);
            if (targetCalendarTick >= nextWeatherRosterRefreshTick)
            {
                outdoorWorkerBuffer.Clear();
                Production.CollectActiveWorkerIds(outdoorWorkerBuffer);
                outdoorWorkerBuffer.AddRange(abstractWorkerBuffer);
                // Exposure changes only at completed-day boundaries. A deterministic one-game-minute roster
                // cadence still catches new outdoor workers long before the next boundary without rescanning
                // every canonical resident on every rendered frame.
                Weather.AdvanceHealthExposureTo(targetCalendarTick, outdoorWorkerBuffer, false);
                nextWeatherRosterRefreshTick = checked(targetCalendarTick + WeatherRosterRefreshIntervalTicks);
            }
            PerformanceBenchmark?.RecordWorkersAndWeatherAllocation();
            StrategicAi.AdvanceTo(targetCalendarTick, Clock.State.BiologicalTicks);
            AdvanceMilitaryTrainingTo(targetCalendarTick);
            MilitaryAttack.AdvanceTo(targetCalendarTick);
            SettlementCapture.AdvanceTo(targetCalendarTick, Clock.State.BiologicalTicks,
                (settlementId, defender) =>
                {
                    var settlement = GlobalMap.Settlements.Single(x => x.Id == settlementId);
                    return MilitaryUnits.Units.Any(x => x.OwnerSideId.Value == defender.Value &&
                        x.Status != MilitaryUnitStatus.Disbanded &&
                        string.Equals(x.AbstractLocationId, settlement.LocationId.Value, StringComparison.Ordinal));
                });
            CampaignProgress.AdvanceTo(targetCalendarTick);
            Events.AdvanceTo(targetCalendarTick, false);
            HealthFamily.AdvanceTo(targetCalendarTick, Clock.State.BiologicalTicks, false);
            BuildingDamage.AdvanceFireTo(targetCalendarTick);
            Production.AdvanceTo(targetCalendarTick, abstractWorkerBuffer);
            ResourceEcology?.AdvanceTo(targetCalendarTick, resourceRespawnSurface);
            PerformanceBenchmark?.RecordCanonicalServicesAllocation();
            for (var i = 0; i < productionPresenters.Count; i++) productionPresenters[i].Refresh();
            WeatherPresentation.Refresh(targetCalendarTick);
            if (Time.unscaledTime >= nextTierPolicyRefreshTime)
            {
                var focus = cameraController.transform.position;
                TierPolicy.Evaluate(new WorldPosition(focus.x, focus.y, focus.z));
                nextTierPolicyRefreshTime = Time.unscaledTime + TierPolicyRefreshIntervalSeconds;
            }
            PerformanceBenchmark?.RecordPresentationAndTierPolicyAllocation();
        }

        private void RefreshTier3WorkerCache()
        {
            if (tier3WorkerCacheRevision == Tiers.Revision) return;
            tier3WorkerCache.Clear();
            var managedIds = Tiers.ManagedIds;
            for (var index = 0; index < managedIds.Count; index++)
                if (Tiers.GetTier(managedIds[index]) == CharacterSimulationTier.Tier3)
                    tier3WorkerCache.Add(managedIds[index]);
            tier3WorkerCacheRevision = Tiers.Revision;
        }

        private static void MergeSortedDistinct(IReadOnlyList<StableEntityId> first,
            IReadOnlyList<StableEntityId> second, List<StableEntityId> destination)
        {
            destination.Clear();
            var firstIndex = 0;
            var secondIndex = 0;
            while (firstIndex < first.Count || secondIndex < second.Count)
            {
                StableEntityId next;
                if (secondIndex >= second.Count || firstIndex < first.Count && first[firstIndex].CompareTo(second[secondIndex]) < 0)
                    next = first[firstIndex++];
                else if (firstIndex >= first.Count || second[secondIndex].CompareTo(first[firstIndex]) < 0)
                    next = second[secondIndex++];
                else
                {
                    next = first[firstIndex++];
                    secondIndex++;
                }
                if (destination.Count == 0 || destination[destination.Count - 1] != next) destination.Add(next);
            }
        }

        private void OnChildBorn(Character child)
        {
            Appearances.GetOrCreate(child);
            var state = new CharacterRuntimeState(child.Id, default, default, false, "local",
                lastSimulationCalendarTick: Clock.State.CalendarTicks,
                lastSimulationBiologicalTick: Clock.State.BiologicalTicks);
            Tiers.Materialize(child, CharacterSimulationTier.Tier3, state);
            Production.Population = Characters.Count;
            SynchronizeStarterSettlement();
        }

        public void SynchronizeStarterSettlement()
        {
            if (GlobalMap == null) return;
            var current = GlobalMap.Settlements.SingleOrDefault(x => x.Id == StarterSettlementId);
            GlobalMap.SynchronizeSettlement(StarterSettlementId,
                new WorldLocationId("starter_settlement"), current?.OwnerSideId ?? new CombatSideId("player"),
                Buildings.All.Where(x => current == null || x.OwnerSideId == current.OwnerSideId).Select(x => x.Id),
                Characters.GetAll().Where(x => StarterEnemyAi == null || !StarterEnemyAi.Residents.Contains(x.Id))
                    .Select(x => x.Id));
        }

        public void ApplyLocalLocationScope(WorldLocationId locationId)
        {
            // P10 is one persistent physical world. Entering a named location changes strategic focus only;
            // it must never hide or teleport canonical physical buildings.
            foreach (var presenter in BuildingPresentations.All)
                if (presenter != null) presenter.gameObject.SetActive(true);
        }

        public int AdvanceMilitaryTrainingTo(long calendarTick)
        {
            var completed = MilitaryTraining.AdvanceTo(calendarTick);
            if (completed > 0) RefreshTier1AppearanceAfterLoad();
            return completed;
        }

        public void RebindAfterLoad(GameClock restoredClock)
        {
            Clock = restoredClock ?? throw new ArgumentNullException(nameof(restoredClock));
            AiRuntime.RebindClock(Clock);
            Tier3Adapter.RebaseAfterLoad(new SimulationTimePoint(Clock.State.CalendarTicks,
                Clock.State.BiologicalTicks));
            RefreshTier1AppearanceAfterLoad();
            selectionProbe.RefreshSelectedPresentations();
            Production.Population = Characters.Count;
            RebindRegionResourcePresenters();
            DirectWorkCommands.EnsureRoleGroups();
            DirectWorkCommands.ReevaluateQueued();
            WeatherPresentation.Refresh(Clock.State.CalendarTicks);
            ApplyLocalLocationScope(GlobalMap.ActiveLocationId);
            GlobalMapBridge.ReconcileAfterLoad();
            GameplayUi?.RefreshAfterStateRebind();
        }

        public void StartNewGame(long worldSeed)
        {
            NewGameLaunchState.Request(worldSeed);
            SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
        }

        private void RefreshTier1AppearanceAfterLoad()
        {
            var activeIds = Characters.GetAll().Where(x =>
                    Tiers.TryGetTier(x.Id, out var tier) && tier == CharacterSimulationTier.Tier1 &&
                    CharacterPresentations.TryGet(x.Id, out var presenter) && presenter != null && presenter.IsBound)
                .Select(x => x.Id).ToArray();
            for (var i = 0; i < activeIds.Length; i++)
            {
                var character = Characters.Get(activeIds[i]);
                Tier1Adapter.RebuildPresentation(character);
            }
            demoPresenters.Clear();
            if (CharacterPresentations.TryGet(MaleDemoCharacter.Id, out var ruler)) demoPresenters.Add(ruler);
            if (CharacterPresentations.TryGet(FemaleDemoCharacter.Id, out var spouse)) demoPresenters.Add(spouse);
            if (demoPresenters.Count >= 2 && demoPresenters[0] != null && demoPresenters[1] != null)
                ReferenceCamp?.RebindCharacterTools(demoPresenters[0], demoPresenters[1]);
        }

        public void ReturnToMainMenu()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        public CombatEncounterState StartDemoCombat()
        {
            var active = Combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
            if (active != null) return active;
            PrepareDemoCombatants(out var player, out var enemy);
            var encounterId = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed,
                "demo_encounter", Combat.Encounters.Count);
            return Combat.CreateEncounter(encounterId, new CombatSideId("player"), new CombatSideId("enemy"),
                player, enemy, Clock.State.CalendarTicks,
                id => id == MaleDemoCharacter.Id || id == enemy[0]
                    ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear"));
        }

        public CombatEncounterState StartDemoUnitBattle(BattleMode mode)
        {
            var active = Combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
            if (active != null) return active;
            PrepareDemoCombatants(out var player, out var enemy);
            var playerUnit = MilitaryUnits.TryGetForMember(player[0], out var existingPlayer)
                ? existingPlayer : MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(
                    ActiveNewGameConfiguration.WorldSeed, "demo_player_unit")),
                    new MilitarySideId("player"), player, player[0]);
            var enemyUnit = MilitaryUnits.TryGetForMember(enemy[0], out var existingEnemy)
                ? existingEnemy : MilitaryUnits.Create(new MilitaryUnitId(DeterministicStableId.Create(
                    ActiveNewGameConfiguration.WorldSeed, "demo_enemy_unit")),
                    new MilitarySideId("enemy"), enemy, enemy[0]);
            var battleId = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed,
                "demo_battle", Combat.Encounters.Count);
            if (mode == BattleMode.Abstract)
            {
                MilitaryRepresentations.Transition(playerUnit.Id, MilitaryUnitRepresentationMode.Aggregate);
                MilitaryRepresentations.Transition(enemyUnit.Id, MilitaryUnitRepresentationMode.Aggregate);
                return Battles.StartAbstractBattle(playerUnit.Id, enemyUnit.Id, battleId,
                    Clock.State.CalendarTicks, DemoWeapon);
            }
            return Battles.StartPhysicalBattle(playerUnit.Id, enemyUnit.Id, battleId,
                Clock.State.CalendarTicks, DemoWeapon);
        }

        private void PrepareDemoCombatants(out StableEntityId[] player, out StableEntityId[] enemy)
        {
            player = new[] { MaleDemoCharacter.Id, FemaleDemoCharacter.Id, FirstDemoHeir.Id, SecondDemoHeir.Id };
            EnsureTier1(FirstDemoHeir, new WorldPosition(-5f, 0f, -2f));
            EnsureTier1(SecondDemoHeir, new WorldPosition(-5f, 0f, 2f));
            foreach (var id in player) Combat.RegisterSide(id, new CombatSideId("player"));

            enemy = new StableEntityId[4];
            for (var i = 0; i < enemy.Length; i++)
            {
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "demo_raider", i);
                if (!Characters.TryGet(id, out var character))
                {
                    character = CreateDemoCharacter("Raider" + (i + 1), i % 2 == 0 ? CharacterSex.Male : CharacterSex.Female,
                        id, null, null);
                    Characters.Add(character);
                }
                EnsureTier1(character, new WorldPosition(8f, 0f, -3f + i * 2f));
                Combat.RegisterSide(id, new CombatSideId("enemy"));
                enemy[i] = id;
            }
            Production.Population = Characters.Count;
        }

        private CombatWeaponId DemoWeapon(StableEntityId id) =>
            id == MaleDemoCharacter.Id || id == DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "demo_raider", 0)
                ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear");

        private void EnsureTier1(Character character, WorldPosition position)
        {
            if (Tiers.TryGetTier(character.Id, out var tier))
            {
                if (tier != CharacterSimulationTier.Tier1) Tiers.Transition(character.Id, CharacterSimulationTier.Tier1);
                return;
            }
            Tiers.Materialize(character, CharacterSimulationTier.Tier1,
                new CharacterRuntimeState(character.Id, position, default, false, "local",
                    lastSimulationCalendarTick: Clock.State.CalendarTicks,
                    lastSimulationBiologicalTick: Clock.State.BiologicalTicks));
        }

        private void SpawnDemoStorage()
        {
            // DEV_BOOTSTRAP_ONLY: a free completed basket makes the autonomous hauling slice immediately observable.
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            DemoStorageBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(5, 5),
                BuildingOrientation.North, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_building", 0));
            Storage.Attach(DemoStorageBuilding, definition);
            DemoStorageBuilding.MarkCompleted();
            DemoStoragePresenter = BuildingSpawner.Spawn(DemoStorageBuilding);
        }

        private void SpawnDemoShelter()
        {
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("primitive_shelter"));
            DemoShelterBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(-7, 4),
                BuildingOrientation.South, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_building", 2));
            DemoShelterBuilding.MarkCompleted();
            DemoShelterPresenter = BuildingSpawner.Spawn(DemoShelterBuilding);
        }

        private void SpawnDemoBarracks()
        {
            var definition = buildingCatalog.ResolveDefinition(MilitaryTrainingService.BarracksDefinitionId);
            DemoBarracksBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(-1, 6),
                BuildingOrientation.South, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_barracks"));
            DemoBarracksBuilding.MarkCompleted();
            DemoBarracksPresenter = BuildingSpawner.Spawn(DemoBarracksBuilding);
        }

        private void RegisterSettlementCaptureFlags()
        {
            foreach (var settlement in GlobalMap.Settlements)
            {
                var main = settlement.BuildingIds.Select(id => Buildings.TryGet(id, out var value) ? value : null)
                    .Where(x => x != null && !x.IsDestroyed)
                    .OrderByDescending(x => x.DefinitionId == new BuildingDefinitionId("primitive_shelter"))
                    .ThenBy(x => x.Id).FirstOrDefault();
                if (main != null) SettlementCapture.Register(settlement.Id, main.Id, Clock.State.CalendarTicks);
            }
        }

        private void RegisterStarterDomestic()
        {
            var residents = NewGame.PlayerCharacters.Select(x => x.Id).ToArray();
            Domestic.RegisterHome(new HomeState(DemoShelterBuilding.Id, MaleDemoCharacter.Id,
                new IndoorZoneState(-7, 4, -4, 6, true, true), residents));
            var firstBed = new BedState(
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "starter_bed", 0),
                DemoShelterBuilding.Id, new GridCoordinate(-6, 5),
                ToWorldPosition(ProjectToGameplayGround(new Vector3(-6f, 0f, 5f))));
            var secondBed = new BedState(
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "starter_bed", 1),
                DemoShelterBuilding.Id, new GridCoordinate(-5, 5),
                ToWorldPosition(ProjectToGameplayGround(new Vector3(-5f, 0f, 5f))));
            Domestic.RegisterBed(firstBed);
            Domestic.RegisterBed(secondBed);
            Domestic.AssignBed(firstBed.Id, MaleDemoCharacter.Id);
            Domestic.AssignBed(secondBed.Id, FemaleDemoCharacter.Id);
            Domestic.SetConsent(MaleDemoCharacter.Id, FemaleDemoCharacter.Id, false);
            Domestic.SetConsent(FemaleDemoCharacter.Id, MaleDemoCharacter.Id, false);
        }

        private static WorldPosition ToWorldPosition(Vector3 value) => new WorldPosition(value.x, value.y, value.z);

        private void SpawnDemoCropPlot()
        {
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("primitive_crop_plot"));
            DemoCropPlotBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(8, 5),
                BuildingOrientation.North, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_building", 1));
            DemoCropPlotBuilding.MarkCompleted();
            DemoCropPlotPresenter = BuildingSpawner.Spawn(DemoCropPlotBuilding);
            RegisterCropPlot(DemoCropPlotBuilding, DemoCropPlotPresenter);
        }

        private void OnBuildingPlaced(Building building, BuildingPresenter presenter)
        {
            if (building != null && building.DefinitionId == new BuildingDefinitionId("primitive_crop_plot"))
                RegisterCropPlot(building, presenter);
        }

        private void OnBuildingChanged(Building building)
        {
            if (building != null && BuildingPresentations.TryGet(building.Id, out var presenter))
                presenter.RefreshStructuralPresentation();
        }

        private void RegisterCropPlot(Building building, BuildingPresenter buildingPresenter)
        {
            if (Production.TryGetSite(building.Id, out _)) return;
            var definition = buildingCatalog.ResolveDefinition(building.DefinitionId);
            var world = BuildingSpawner.GridToWorld(building.Coordinate, definition, building.Orientation);
            var site = ProductionSite.CreateCropPlot(building.Id,
                new ProductionRecipeId("cultivate_berries"),
                new WorldPosition(world.x, world.y, world.z), Production.Settings.CropGrowthDuration);
            Production.AddSite(site);
            var presenter = buildingPresenter.gameObject.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, new Color(0.25f, 0.65f, 0.18f));
            productionPresenters.Add(presenter);
        }

        private void SpawnRegionProductionSources()
        {
            if (InhabitableRegion != null)
            {
                foreach (var anchor in InhabitableRegion.ResourceAnchors.OrderBy(x => x.StableIndex))
                    SpawnProductionSource(anchor.RecipeId, anchor.Amount,
                        new WorldPosition(anchor.VisualRoot.position.x, anchor.VisualRoot.position.y,
                            anchor.VisualRoot.position.z), anchor.ActiveColor, anchor.StableIndex,
                        anchor.VisualRoot.gameObject);
                return;
            }
            SpawnProductionSource("gather_berries", 60, new WorldPosition(-12f, 0f, 5f),
                new Color(0.65f, 0.15f, 0.35f), 0, null, PrimitiveType.Sphere);
            SpawnProductionSource("gather_fiber", 60, new WorldPosition(-8f, 0f, 7f),
                new Color(0.3f, 0.7f, 0.2f), 1, null, PrimitiveType.Capsule);
            SpawnProductionSource("chop_wood", 80, new WorldPosition(-4f, 0f, 9f),
                new Color(0.35f, 0.2f, 0.08f), 2, null, PrimitiveType.Cylinder);
            SpawnProductionSource("gather_stone", 80, new WorldPosition(1f, 0f, 9f),
                new Color(0.45f, 0.45f, 0.48f), 3, null, PrimitiveType.Sphere);
        }

        private IndustrialZoneDefinition[] CreateIndustrialDefinitions()
        {
            ResourceAmount Cost(string id, long units) =>
                new ResourceAmount(new ResourceId(id), new ResourceQuantity(units));
            ResourceAmount Output(string id, long units) =>
                new ResourceAmount(new ResourceId(id), new ResourceQuantity(units));
            var industrial = new TechnologyId("industrial_extraction");
            var restorative = new TechnologyId("restorative_mining");
            return new[]
            {
                new IndustrialZoneDefinition("forest_workshop", IndustrialFacilityKind.Workshop,
                    NaturalResourceBiome.Forest, new[] { Cost("wood_log", 8), Cost("plant_fiber", 4) },
                    new[] { Output("wood_log", 4) }, 500, 7, TimeSpan.FromHours(8), industrial),
                new IndustrialZoneDefinition("stone_quarry", IndustrialFacilityKind.Quarry,
                    NaturalResourceBiome.Rocky, new[] { Cost("wood_log", 10), Cost("stone", 6) },
                    new[] { Output("stone", 4) }, 500, 7, TimeSpan.FromHours(10), industrial),
                new IndustrialZoneDefinition("iron_mine", IndustrialFacilityKind.Mine,
                    NaturalResourceBiome.Mountain, new[] { Cost("wood_log", 12), Cost("stone", 10) },
                    new[] { Output("iron_ore", 3) }, 420, 7, TimeSpan.FromHours(12), industrial,
                    restorative, 2),
                new IndustrialZoneDefinition("gold_mine", IndustrialFacilityKind.Mine,
                    NaturalResourceBiome.Mountain, new[] { Cost("wood_log", 14), Cost("stone", 12) },
                    new[] { Output("gold_ore", 2) }, 300, 7, TimeSpan.FromHours(16), industrial,
                    restorative, 1),
                new IndustrialZoneDefinition("lake_fishing", IndustrialFacilityKind.FishingStation,
                    NaturalResourceBiome.Lake, new[] { Cost("wood_log", 10), Cost("plant_fiber", 6) },
                    new[] { Output("fish", 4) }, 500, 7, TimeSpan.FromHours(8), industrial),
                new IndustrialZoneDefinition("animal_hunting", IndustrialFacilityKind.HuntingLodge,
                    NaturalResourceBiome.AnimalRange, new[] { Cost("wood_log", 10), Cost("hide", 4) },
                    new[] { Output("meat", 4) }, 400, 7, TimeSpan.FromHours(12), industrial)
            };
        }

        private void SpawnP104NaturalSources()
        {
            SpawnProductionSource("hunt_animal", 36, new WorldPosition(-78f, 5f, 154f),
                new Color(0.45f, 0.22f, 0.12f), 24, null, PrimitiveType.Capsule);
            SpawnProductionSource("fish_water", 42, new WorldPosition(58f, 0.35f, 132f),
                new Color(0.12f, 0.62f, 0.82f), 25, null, PrimitiveType.Cylinder);
        }

        private void SpawnP104IndustrialZones()
        {
            var definitions = new[] { "forest_workshop", "stone_quarry", "iron_mine", "gold_mine",
                "lake_fishing", "animal_hunting" };
            var resources = new[] { "wood_log", "stone", "iron_ore", "gold_ore", "fish", "meat" };
            var points = new[] { new Vector3(-112f, 0f, 142f), new Vector3(124f, 0f, 146f),
                new Vector3(192f, 0f, -62f), new Vector3(154f, 0f, -118f),
                new Vector3(72f, 0f, 132f), new Vector3(-96f, 0f, 104f) };
            var colors = new[] { new Color(0.25f,0.42f,0.14f), new Color(0.45f,0.47f,0.5f),
                new Color(0.38f,0.25f,0.18f), new Color(0.85f,0.62f,0.12f),
                new Color(0.08f,0.48f,0.72f), new Color(0.55f,0.22f,0.12f) };
            for (var i = 0; i < definitions.Length; i++)
            {
                var position = resources[i] == "fish" ? points[i] : ProjectToGameplayGround(points[i]);
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "p104_industrial_zone", i);
                Industry.AddZone(id, definitions[i], new WorldPosition(position.x, position.y, position.z));
                var pile = WorldPiles.RegisterExistingInventory(new ResourceId(resources[i]),
                    new GridCoordinate(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z)), id);
                var marker = GameObject.CreatePrimitive(i == 4 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                marker.name = "P10.4 Industrial Zone " + definitions[i];
                marker.transform.position = position + Vector3.up * 0.65f;
                marker.transform.localScale = i == 4 ? new Vector3(2.8f, 0.3f, 2.8f) : new Vector3(2.4f, 1.2f, 2.4f);
                var renderer = marker.GetComponent<Renderer>();
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", colors[i]); block.SetColor("_Color", colors[i]); renderer.SetPropertyBlock(block);
                var presenter = marker.AddComponent<WorldResourcePilePresenter>();
                presenter.Bind(pile, Inventories.Get(pile.InventoryOwner));
                demoPilePresenters.Add(presenter);
            }
        }

        private void BindPopulatedWorldAffordances()
        {
            if (PopulatedBiomeWorld == null) return;
            var woodSites = Production.Sites.Where(x => x.RecipeId == new ProductionRecipeId("chop_wood")).ToArray();
            var rockSites = Production.Sites.Where(x => x.RecipeId == new ProductionRecipeId("gather_stone") ||
                x.RecipeId == new ProductionRecipeId("gather_iron") ||
                x.RecipeId == new ProductionRecipeId("gather_gold")).ToArray();
            BindAffordances(PopulatedBiomeWorld.CanopyTrees, woodSites, new Color(0.32f, 0.22f, 0.08f));
            BindAffordances(PopulatedBiomeWorld.MajorRocks, rockSites, new Color(0.5f, 0.5f, 0.52f));
            foreach (var formation in PopulatedBiomeWorld.ResourceFormations)
            {
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_source", formation.StableIndex);
                if (!Production.TryGetSite(id, out var site)) continue;
                BindAffordances(formation.DecorativeSatellites, new[] { site }, Color.white);
            }
        }

        private void BindAffordances(IEnumerable<GameObject> objects, IReadOnlyList<ProductionSite> sites, Color color)
        {
            if (sites == null || sites.Count == 0) return;
            foreach (var value in objects.Where(x => x != null))
            {
                var point = value.transform.position;
                var site = sites.OrderBy(x => (x.Position.X - point.x) * (x.Position.X - point.x) +
                    (x.Position.Z - point.z) * (x.Position.Z - point.z)).ThenBy(x => x.Id).First();
                if (value.GetComponentInChildren<Collider>(true) == null)
                    value.AddComponent<BoxCollider>();
                var presenter = value.GetComponent<ProductionSourcePresenter>() ?? value.AddComponent<ProductionSourcePresenter>();
                presenter.Bind(site, color, true);
                if (!productionPresenters.Contains(presenter)) productionPresenters.Add(presenter);
            }
        }

        private void SpawnProductionSource(string recipeId, long amount, WorldPosition position, Color color,
            int index, GameObject authoredView, PrimitiveType fallbackPrimitive = PrimitiveType.Sphere)
        {
            amount = 20 + Math.Abs(index * 17 + 11) % 31;
            var site = ProductionSite.Restore(new ProductionSiteState(
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_source", index),
                new ProductionRecipeId(recipeId), ProductionSiteKind.NaturalSource, position, amount, amount,
                CropPlotPhase.Unplanted, 0, 0));
            Production.AddSite(site);
            var view = authoredView != null ? authoredView : GameObject.CreatePrimitive(fallbackPrimitive);
            if (authoredView == null)
            {
                view.transform.position = new Vector3(position.X, position.Y + 0.5f, position.Z);
                view.transform.localScale = fallbackPrimitive == PrimitiveType.Cylinder
                    ? new Vector3(0.8f, 1.6f, 0.8f) : Vector3.one;
            }
            var presenter = view.GetComponent<ProductionSourcePresenter>() ?? view.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, color);
            productionPresenters.Add(presenter);
            var biome = BiomeForRecipe(recipeId);
            ResourceEcology.Register(new NaturalNodeRegistration(site, biome, RespawnCandidates(position)));
        }

        private static NaturalResourceBiome BiomeForRecipe(string recipeId)
        {
            if (recipeId == "chop_wood") return NaturalResourceBiome.Forest;
            if (recipeId == "gather_stone") return NaturalResourceBiome.Rocky;
            if (recipeId == "gather_iron" || recipeId == "gather_gold") return NaturalResourceBiome.Mountain;
            if (recipeId == "fish_water") return NaturalResourceBiome.Lake;
            if (recipeId == "hunt_animal") return NaturalResourceBiome.AnimalRange;
            if (recipeId == "gather_fiber") return NaturalResourceBiome.Wetland;
            return NaturalResourceBiome.Meadow;
        }

        private static WorldPosition[] RespawnCandidates(WorldPosition origin) => new[]
        {
            origin,
            new WorldPosition(origin.X + 4f, origin.Y, origin.Z),
            new WorldPosition(origin.X - 4f, origin.Y, origin.Z),
            new WorldPosition(origin.X, origin.Y, origin.Z + 4f),
            new WorldPosition(origin.X, origin.Y, origin.Z - 4f),
            new WorldPosition(origin.X + 3f, origin.Y, origin.Z + 3f),
            new WorldPosition(origin.X - 3f, origin.Y, origin.Z - 3f)
        };

        private void RebindRegionResourcePresenters()
        {
            if (InhabitableRegion == null) return;
            foreach (var anchor in InhabitableRegion.ResourceAnchors)
            {
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed,
                    "player_source", anchor.StableIndex);
                var presenter = anchor.VisualRoot.GetComponent<ProductionSourcePresenter>();
                if (Production.TryGetSite(id, out var site))
                {
                    anchor.VisualRoot.gameObject.SetActive(true);
                    if (presenter == null) presenter = anchor.VisualRoot.gameObject.AddComponent<ProductionSourcePresenter>();
                    presenter.Bind(site, anchor.ActiveColor);
                    if (!productionPresenters.Contains(presenter)) productionPresenters.Add(presenter);
                }
                else if (presenter != null) anchor.VisualRoot.gameObject.SetActive(false);
            }
        }

        private void BuildNavigationSurface()
        {
            NavigationSurface = gameObject.AddComponent<NavMeshSurface>();
            NavigationSurface.collectObjects = CollectObjects.Volume;
            NavigationSurface.center = new Vector3(0f, 3f, 0f);
            NavigationSurface.size = RealisticGameplayRegion == null
                ? new Vector3(100f, 8f, 100f) : new Vector3(512f, 80f, 512f);
            NavigationSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            NavigationSurface.BuildNavMesh();
        }

        private Vector3 ProjectToGameplayGround(Vector3 position) => RealisticGameplayRegion == null
            ? position : RealisticGameplayRegion.ProjectToGround(position, 0.04f);

        private void SpawnDemoPile(string id, long units, GridCoordinate coordinate, Color color, int index)
        {
            var pile = WorldPiles.Create(new ResourceId(id), new ResourceQuantity(units), coordinate,
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_pile", index));
            var authoredAnchor = InhabitableRegion?.GetPileAnchor(index);
            var view = authoredAnchor != null ? authoredAnchor.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (authoredAnchor == null)
            {
                view.transform.position = buildingGridOrigin + new Vector3(coordinate.X * buildingGridSize, 0.35f,
                    coordinate.Z * buildingGridSize);
                view.transform.localScale = new Vector3(0.75f, 0.7f, 0.75f);
            }
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
            }
            var presenter = view.GetComponent<WorldResourcePilePresenter>() ?? view.AddComponent<WorldResourcePilePresenter>();
            presenter.Bind(pile, Inventories.Get(pile.InventoryOwner));
            demoPilePresenters.Add(presenter);
        }

        private static Character CreateDemoCharacter(string name, CharacterSex sex, StableEntityId id,
            StableEntityId? dynastyId, ParentLink[] parents)
        {
            return Character.Restore(new CharacterState(id, new CharacterName(name), sex, 0,
                CharacterLifeState.Alive, null, parents, null, dynastyId, dynastyId, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
        }

        private static ParentLink Parent(StableEntityId id, ParentRole role) =>
            new ParentLink(id, role, ParentageKind.Biological, true);

    }

    internal static class NewGameLaunchState
    {
        private static NewGameConfiguration requestedConfiguration;
        public static void Request(long seed) => Request(new NewGameConfiguration(seed));
        public static void Request(NewGameConfiguration configuration) =>
            requestedConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        public static NewGameConfiguration Consume(long fallback)
        {
            var result = requestedConfiguration ?? new NewGameConfiguration(fallback);
            requestedConfiguration = null;
            return result;
        }
    }
}
