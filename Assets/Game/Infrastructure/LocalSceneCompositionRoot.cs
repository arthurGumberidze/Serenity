using System;
using System.Collections;
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
        private StableEnvironmentLodController environmentLod;
        private GameObject[] pondTrees = Array.Empty<GameObject>();

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
        public SettlementTerritoryRegistry BuildingTerritories { get; private set; }
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
        public ConstructionSiteService ConstructionSites { get; private set; }
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
        public NeighbourEconomyRuntime NeighbourEconomy { get; private set; }
        public Tier1AiRuntimeDriver AiRuntime { get; private set; }
        public WorkManager Work { get; private set; }
        public DirectWorkCommandService DirectWorkCommands { get; private set; }
        public ConstructionClearanceCoordinator ConstructionClearance { get; private set; }
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
        private IndustrialWorkCoordinator industrialWork;
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
        public SettlementFacilityService Facilities { get; private set; }
        public BuildingPathingService BuildingPathing { get; private set; }
        public BuildingLifecycleService BuildingLifecycle { get; private set; }
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
        public TiledWorldDefinition WorldDefinition { get; private set; }
        public TiledWorldHeightSampler WorldHeight { get; private set; }
        public TiledTerrainWorld TiledWorld { get; private set; }
        public P109TiledWorldPopulationPresenter WorldPopulation { get; private set; }
        public P102WorldPlacementSurface WorldPlacementSurface { get; private set; }
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
        private bool performanceTierPolicySuppressed;
        private P104ResourceRespawnSurface resourceRespawnSurface;

        /// <summary>
        /// Keeps an explicitly authored performance population in its requested tiers while its schedulers run.
        /// Normal gameplay never calls this diagnostic-only boundary and continues to evaluate camera distance.
        /// </summary>
        public void SetPerformanceTierPolicySuppressed(bool suppressed) =>
            performanceTierPolicySuppressed = suppressed;

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

        public bool IsInitialized { get; private set; }
        private WorldLoadingOverlay loadingOverlay;

        private void Awake()
        {
            loadingOverlay = gameObject.AddComponent<WorldLoadingOverlay>();
            StartCoroutine(InitializeGuarded());
        }

        private IEnumerator InitializeGuarded()
        {
            // Drive nested initialization iterators here so any stage failure replaces the loading screen.
            var stages = new Stack<IEnumerator>();
            stages.Push(InitializeScene());
            while (stages.Count > 0)
            {
                object next = null;
                Exception failure = null;
                var moved = false;
                try { moved = stages.Peek().MoveNext(); if (moved) next = stages.Peek().Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    Debug.LogException(failure);
                    loadingOverlay.Fail();
                    yield break;
                }
                if (!moved) { (stages.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stages.Push(nested);
                else yield return next;
            }
            IsInitialized = true;
            TiledWorld?.BeginBackgroundTileWarmup();
            Destroy(loadingOverlay);
            Start();
            Debug.Log("WM15_BOOT gameplay ready");
        }

        private IEnumerator InitializeScene()
        {
            yield return null;
            if (input == null || cameraController == null || pointerRaycaster == null || selectionProbe == null ||
                characterCatalog == null || buildingCatalog == null || buildingPlacementController == null)
                throw new InvalidOperationException("Local scene composition references must be assigned explicitly.");

            Debug.Log("WM15_BOOT local composition");
            var requestedConfiguration = NewGameLaunchState.Consume(developmentWorldSimulationSeed);
            var resolvedWorldSeed = U30SoakConfig.ResolveWorldSeed(Environment.GetCommandLineArgs(),
                requestedConfiguration.WorldSeed);
            ActiveNewGameConfiguration = resolvedWorldSeed == requestedConfiguration.WorldSeed
                ? requestedConfiguration
                : new NewGameConfiguration(resolvedWorldSeed, requestedConfiguration.ConfigurationId,
                    requestedConfiguration.RivalKnownAtStart, requestedConfiguration.RulerName,
                    requestedConfiguration.DynastyName, requestedConfiguration.RulerSex,
                    requestedConfiguration.RulerAppearanceProfileId, requestedConfiguration.RulerAppearance);
            WorldDefinition = TiledWorldDefinitionLoader.LoadActive();
            WorldHeight = new TiledWorldHeightSampler(WorldDefinition, ActiveNewGameConfiguration.WorldSeed);
            var activePopulationPlan = WorldDefinition.MapData == null ? null :
                new TiledWorldPopulationPlanner(WorldDefinition, WorldHeight,
                    ActiveNewGameConfiguration.WorldSeed).Create();
            if (activePopulationPlan?.StartTerritory != null)
                buildingGridOrigin = new Vector3(activePopulationPlan.StartTerritory.Center.X, 0f,
                    activePopulationPlan.StartTerritory.Center.Z);
            var appearanceCatalog = characterCatalog.CreateDefinitionCatalog();
            NewGame = NewGameBootstrap.Create(ActiveNewGameConfiguration, appearanceCatalog, activePopulationPlan);
            var worldSeed = ActiveNewGameConfiguration.WorldSeed;
            Characters = new CharacterRegistry();
            Appearances = new CharacterAppearanceService(Characters, appearanceCatalog, worldSeed);
            HealthFamily = new HealthAndFamilyService(Characters, HealthConditionCatalog.CreateStoneAge(),
                worldSeed);
            Weather = new WeatherService(worldSeed, ClimateProfile.Temperate, HealthFamily);
            CharacterPresentations = new CharacterPresentationRegistry();
            CharacterSpawner = new CharacterPresentationSpawner(characterCatalog, CharacterPresentations, Appearances,
                presentationPositionProjector: ProjectToGameplayGround,
                rulerSelector: character => (Dynasty?.CurrentMonarchId ?? NewGame.Dynasty.CurrentMonarchId) == character.Id ||
                    StarterEnemyAi?.Dynasty.CurrentMonarchId == character.Id ||
                    StrategicAi != null && StrategicAi.Factions.Any(faction => faction.Dynasty.CurrentMonarchId == character.Id),
                childSelector: character => character.AgeInCompletedYears(Clock != null
                    ? Clock.State.BiologicalTicks : Character.BiologicalTicksPerYear * 20L) < 16);

            InteractionMode = new LocalInteractionMode();
            Buildings = new BuildingRegistry();
            BuildingOccupancy = new BuildingOccupancyGrid();
            BuildingPlacementService = new BuildingPlacementService(Buildings, BuildingOccupancy,
                WorldGridBounds());
            BuildingPathing = new BuildingPathingService(Buildings, BuildingOccupancy,
                id => buildingCatalog.ResolveDefinition(id));
            if (buildingCatalog.ResourceCatalog == null) throw new InvalidOperationException("Building catalog has no resource catalog.");
            Resources = buildingCatalog.ResourceCatalog.CreateCatalog();
            InhabitableRegion = GetComponent<P5InhabitableRegionPresentation>();
            InhabitableRegion?.ValidateConfiguration();
            RealisticGameplayRegion = FindAnyObjectByType<P92RealisticGameplayRegion>();
            RealisticGameplayRegion?.ValidateConfiguration();
            // P10.2 authored a different map: its water cylinders, mountain capsules and
            // navigation exclusions must not survive alongside the new Terrain authority.
            var legacyWorld = FindAnyObjectByType<P102ConnectedWorldPresentation>();
            if (legacyWorld != null)
            {
                foreach (var water in legacyWorld.InlandWater) if (water != null) water.gameObject.SetActive(false);
                foreach (var mountain in legacyWorld.MountainLandmarks) if (mountain != null) mountain.gameObject.SetActive(false);
                foreach (var volume in legacyWorld.GetComponentsInChildren<NavMeshModifierVolume>(true))
                    volume.enabled = false;
            }
            CreateFishingLandingMarkers();
            TiledWorld = gameObject.AddComponent<TiledTerrainWorld>();
            yield return TiledWorld.InitializeRoutine(WorldDefinition, WorldHeight, RealisticGameplayRegion?.Terrain,
                loadingOverlay.SetStage);
            if (RealisticGameplayRegion?.WaterRenderer != null)
                RealisticGameplayRegion.WaterRenderer.enabled = false;
            WorldPlacementSurface = FindAnyObjectByType<P102WorldPlacementSurface>() ??
                gameObject.AddComponent<P102WorldPlacementSurface>();
            WorldPlacementSurface.Configure(WorldHeight);
            cameraController.Settings.BoundsMin = new Vector2(WorldDefinition.Origin.X + 10f,
                WorldDefinition.Origin.Z + 10f);
            cameraController.Settings.BoundsMax = new Vector2(WorldDefinition.Origin.X + WorldDefinition.WidthMeters - 10f,
                WorldDefinition.Origin.Z + WorldDefinition.DepthMeters - 10f);
            cameraController.Settings.MoveSpeed = 24f;
            cameraController.Settings.DragPanSpeed = 1.2f;
            cameraController.Settings.MaxZoom = Mathf.Max(cameraController.Settings.MaxZoom, 160f);
            cameraController.Settings.ZoomSpeed = 4f;
            cameraController.Settings.Validate();
            cameraController.ConfigureGround(position => WorldHeight.TrySampleWaterSurface(position.x, position.z, out var water)
                    ? Mathf.Max(water, WorldHeight.SampleHeight(position.x, position.z))
                    : WorldHeight.SampleHeight(position.x, position.z),
                new Vector3(WorldDefinition.PlayerStart.X, 0f, WorldDefinition.PlayerStart.Z));
            if (WorldDefinition.MapData != null)
                cameraController.FocusPosition(new Vector3(WorldDefinition.PlayerStart.X, 0f,
                    WorldDefinition.PlayerStart.Z), 90f);
            PopulatedBiomeWorld = FindAnyObjectByType<P93PopulatedBiomeWorld>();
            PopulatedBiomeWorld?.ValidateConfiguration();
            StoneAgeSettlement = FindAnyObjectByType<P94StoneAgeSettlement>();
            StoneAgeSettlement?.ValidateConfiguration();
            if (PopulatedBiomeWorld != null && WorldDefinition.MapData == null)
            {
                PopulatedBiomeWorld.CaptureAuthoredTreePositions();
                var nature = UnityEngine.Resources.Load<StarterNatureCatalog>("World/StarterNature");
                var pond = WorldDefinition.Zones.Single(x => x.Id == "starter_pond");
                var shoreProps = Array.Empty<GameObject>();
                if (nature != null)
                    pondTrees = StarterNatureProjection.CreatePondGrove(transform,
                        new Vector3(pond.Center.X, 0, pond.Center.Z), new Vector2(pond.RadiusX, pond.RadiusZ), nature, out shoreProps);
                StarterNatureProjection.Apply(StarterTreeViews().Concat(PopulatedBiomeWorld.ResourceFormations
                    .Where(x => x.RecipeId == "chop_wood").Select(x => x.ClickableRoot.gameObject)),
                    nature);
                environmentLod = gameObject.AddComponent<StableEnvironmentLodController>();
                environmentLod.Initialize(
                    StarterTreeViews(),
                    position => TiledWorld.ProjectToGround(position).y,
                    PopulatedBiomeWorld.MajorRocks.Concat(PopulatedBiomeWorld.GroundCover)
                        .Concat(PopulatedBiomeWorld.BaseRegion.Rocks).Concat(PopulatedBiomeWorld.BaseRegion.Foliage).Concat(shoreProps));
                TiledWorld.VisualTilesChanged += RefreshAuthoredTreeVisibility;
                RefreshAuthoredTreeVisibility();
            }
            else if (WorldDefinition.MapData != null)
                DisableLegacyLocalPresentation();
            Inventories = new ResourceInventoryRegistry();
            ResourceTransfers = new ResourceTransferService(Inventories);
            Storage = new StorageService(Inventories, Resources);
            WorldPiles = new WorldPileService(Resources, Inventories);
            ConstructionSites = new ConstructionSiteService(Resources, Inventories);
            StoredResources = new SettlementResourceView(Inventories);
            ProductionRecipes = ProductionCatalog.CreatePlayablePatch(Resources);
            Technologies = TechnologyCatalog.CreateStoneAge(ProductionRecipes,
                buildingCatalog.Entries.Select(entry => new BuildingDefinitionId(entry.DefinitionId)),
                startsWithStoneTools: true);
            Research = new ResearchService(Technologies, new CharacterResearchRateProvider(Characters, HealthFamily));
            Construction = new ConstructionFundingService(BuildingPlacementService, Inventories, Resources, Storage,
                Research);
            Construction.RequiresWorker = true;
            gameObject.AddComponent<ResourceDebugOverlay>().Initialize(Inventories, Resources, pointerRaycaster);
            BuildingPresentations = new BuildingPresentationRegistry();
            BuildingSpawner = new BuildingPresentationSpawner(buildingCatalog, BuildingPresentations,
                buildingGridSize, buildingGridOrigin, null, Inventories,
                position => TiledWorld.ProjectToGround(position).y);
            selectionProbe.Configure(input, pointerRaycaster, InteractionMode);
            buildingPlacementController.Configure(input, pointerRaycaster, buildingCatalog,
                buildingGridSize, buildingGridOrigin);
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
            yield return TiledWorld.BuildNavigationRoutine(loadingOverlay.SetStage);
            NavigationSurface = TiledWorld.PrimaryNavigationSurface;
            if (NavigationSurface == null)
                throw new InvalidOperationException("The starter tiled Terrain has no navigation surface.");
            loadingOverlay.SetStage("Заселяем мир…");
            yield return null;

            var playerCharacters = NewGame.PlayerCharacters.Select(Character.Restore).ToArray();
            foreach (var character in playerCharacters)
            {
                Characters.Add(character);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.CarryLoad, character.Id), Resources, 12));
            }
            loadingOverlay.SetStage("Заселяем семьи…");
            yield return null;
            Appearances.RestoreState(NewGame.Appearances);
            MaleDemoCharacter = Characters.Get(NewGame.MonarchId);
            FemaleDemoCharacter = Characters.Get(NewGame.SpouseId);
            FirstDemoHeir = Characters.Get(NewGame.FirstHeirId);
            SecondDemoHeir = Characters.Get(NewGame.SecondHeirId);
            Domestic = new DomesticService(Characters, Buildings, HealthFamily, new[]
            {
                new BuildingDefinitionId("stone_age_hut"),
                new BuildingDefinitionId("clan_house"),
                new BuildingDefinitionId("chief_hut")
            });
            Facilities = new SettlementFacilityService(Characters, Buildings,
                id => buildingCatalog.ResolveDefinition(id), HealthFamily, Inventories);
            Facilities.SetRulingDynasty(MaleDemoCharacter.DynastyId);
            RegisterStarterDomestic();
            SpawnDemoPile("berries", 60, new GridCoordinate(-8, -4), new Color(0.6f, 0.12f, 0.18f), 4);
            loadingOverlay.SetStage("Создаём жителей…");
            yield return null;
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter,
                StartingResidentPosition(0, new Vector3(-3f, 0f, 0f)), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter,
                StartingResidentPosition(1, new Vector3(3f, 0f, 0f)), Quaternion.identity));
            ReferenceCamp = GetComponent<StoneAgeReferenceCampPresentation>();
            ReferenceCamp?.Initialize(demoPresenters[0], demoPresenters[1], DemoShelterPresenter, DemoStoragePresenter);

            Production = new ProductionService(ProductionRecipes, Resources, Inventories, Buildings, ResourceTransfers,
                researchUnlocks: Research, capabilityProvider: HealthFamily, weatherModifierProvider: Weather);
            Production.BuildingWorkAllowed = (id, tick) =>
            {
                var policy = Facilities.GetPolicy(id);
                if (policy.WorkPaused) return false;
                var hour = (int)((tick % TimeSpan.TicksPerDay) / TimeSpan.TicksPerHour);
                return policy.Shift == WorkShiftKind.Day ? hour >= 6 && hour < 18 : hour < 6 || hour >= 18;
            };
            Production.BuildingWorkPriority = id => (int)Facilities.GetPolicy(id).Priority;
            Production.BuildingHasAssignedWorker = id => Facilities.WorkplaceAssignments.Values.Contains(id);
            Production.AssignedWorkplace = id => Facilities.WorkplaceAssignments.TryGetValue(id, out var workplace)
                ? workplace : (StableEntityId?)null;
            Production.Animals.Configure(AnimalHuntingConfig.Load());
            Production.UseGroundOutputs(WorldPiles, position => new GridCoordinate(
                Mathf.RoundToInt((position.X - buildingGridOrigin.x) / buildingGridSize),
                Mathf.RoundToInt((position.Z - buildingGridOrigin.z) / buildingGridSize)));
            Production.Population = Characters.Count;
            ResourceEcology = new ResourceEcologyService();
            Industry = new IndustrialExtractionService(Resources, Inventories, CreateIndustrialDefinitions());
            Production.AttachNaturalResources(ResourceEcology, Industry);
            if (WorldDefinition.MapData == null)
            {
                SpawnRegionProductionSources();
                SpawnP104NaturalSources();
                SpawnP104IndustrialZones();
            }
            var worksite = gameObject.AddComponent<WorksitePresentation>();
            worksite.Initialize(this);
            gameObject.AddComponent<WorldWorkApproaches>().Initialize(this);
            loadingOverlay.SetStage("Расселяем животных и ресурсы…");
            yield return null;
            WorldPopulation = gameObject.AddComponent<P109TiledWorldPopulationPresenter>();
            WorldPopulation.Initialize(TiledWorld, Production, ResourceEcology, Industry, WorldPiles,
                Inventories, PopulatedBiomeWorld, ActiveNewGameConfiguration.WorldSeed);
            loadingOverlay.SetStage("Подключаем жителей к работам…");
            yield return null;
            if (WorldDefinition.MapData == null)
            {
                RegisterStarterTrees();
                RegisterStarterRocks();
            }
            DisableSupersededRouteStrips();
            resourceRespawnSurface = new P104ResourceRespawnSurface(FindAnyObjectByType<P102WorldPlacementSurface>());
            gameObject.AddComponent<ProductionDebugOverlay>().Initialize(Production, Resources, pointerRaycaster);
            gameObject.AddComponent<P104ResourceHoverLabel>().Initialize(selectionProbe, Production);

            // Begin a new session in daylight; loading still restores the exact saved clock.
            Clock = new GameClock(new GameTimeState(TimeSpan.TicksPerHour * 10L,
                Character.BiologicalTicksPerYear * 20L, 1, false));
            Dynasty = DynastyState.Restore(NewGame.Dynasty);
            PlayerRealm = new PlayerRealmState(MaleDemoCharacter.Id);
            GameRun = new GameRunState();
            Succession = new SuccessionService(Characters, Dynasty, PlayerRealm, GameRun);
            HealthFamily.BindSuccession(Succession);
            AiAgents = new Tier1AiAgentRegistry();
            HaulClaims = new HaulClaimRegistry();
            Work = new WorkManager(Characters);
            var haulJobs = new HaulWorldQuery(Inventories, WorldPiles, Buildings, HaulClaims, buildingGridSize,
                new WorldPosition(buildingGridOrigin.x, buildingGridOrigin.y, buildingGridOrigin.z),
                point => new WorldPosition(point.X, WorldHeight.SampleHeight(point.X, point.Z) + 0.04f, point.Z));
            haulJobs.PositionOverride=owner=>
            {
                if(owner.Kind==InventoryOwnerKind.Character || owner.Kind==InventoryOwnerKind.CarryLoad)
                    return Tiers!=null && Tiers.TryGetTier(owner.Id,out _) ? Tiers.GetRuntimeState(owner.Id).Position : (WorldPosition?)null;
                if(!Buildings.TryGet(owner.Id,out var building))return null;
                var def=buildingCatalog.ResolveDefinition(building.DefinitionId);
                var center=BuildingSpawner.GridToWorld(building.Coordinate,def,building.Orientation);
                center-=Quaternion.Euler(0,building.Orientation.Degrees(),0)*Vector3.forward*(def.Footprint.Depth*buildingGridSize*.5f+1.5f);
                return new WorldPosition(center.x,WorldHeight.SampleHeight(center.x,center.z),center.z);
            };
            var aiWorld = new Tier1AiWorld(haulJobs, HaulClaims, ResourceTransfers, Production, Research, Buildings,
                owner =>
                {
                    if (owner.Kind == InventoryOwnerKind.ConstructionBuffer &&
                        Buildings.TryGet(owner.Id, out var construction))
                        ConstructionSites.NotifyDelivery(construction);
                });
            aiWorld.DrinkingWell = origin => Buildings.All.Where(x => x.IsOperational &&
                    x.DefinitionId == new BuildingDefinitionId("well"))
                .Select(x =>
                {
                    var definition = buildingCatalog.ResolveDefinition(x.DefinitionId);
                    var point = BuildingSpawner.GridToWorld(x.Coordinate, definition, x.Orientation);
                    return new WorldPosition(point.x, WorldHeight.SampleHeight(point.x, point.z), point.z);
                })
                .OrderBy(x => x.DistanceSquared(origin)).Cast<WorldPosition?>().FirstOrDefault();
            AiScheduler = new Tier1AiScheduler(Characters, AiAgents, aiWorld, workManager: Work,
                capabilityProvider: HealthFamily, domesticService: Domestic);
            AiScheduler.SetAutonomousWorkMode(Tier1AiScheduler.AutonomousWorkMode.NeedsOnly);
            AiRuntime = gameObject.AddComponent<Tier1AiRuntimeDriver>();
            AiRuntime.Initialize(Clock, AiScheduler, () =>
                Weather.GetWeatherAtCalendarTick(Clock.State.CalendarTicks).Modifiers.MovementPerThousand);
            Production.ReconcileHorseRiders(Characters.GetAll().Where(x => x.LifeState == CharacterLifeState.Alive)
                .Select(x => x.Id));
            AiRuntime.SetIndividualMovementModifierProvider(Production.GetResidentMovementPerThousand);
            AiRuntime.Register(MaleDemoCharacter, demoPresenters[0]);
            AiRuntime.Register(FemaleDemoCharacter, demoPresenters[1], new Tier1Needs(0d, 0.8d));
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
            WeatherPresentation.ConfigureMapArt(TiledWorld.ArtDirection, TiledWorld.ArtWorldToDesign);
            WeatherPresentation.Refresh(Clock.State.CalendarTicks);
            DynastyDebug = gameObject.AddComponent<DynastyDebugOverlay>();
            DynastyDebug.Initialize(Succession, Characters, selectionProbe, Clock, pointerRaycaster);
            HealthFamilyDebug = gameObject.AddComponent<HealthFamilyDebugOverlay>();
            HealthFamilyDebug.Initialize(HealthFamily, Characters, Succession.Family, selectionProbe, Clock,
                pointerRaycaster);
            Tier2Runtime = new Tier2Runtime("Serenity Local Tier 2");
            WorldPopulation.InitializeWildlifeTiers(Tier2Runtime, Clock.State.CalendarTicks);
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
                Tiers.Materialize(playerCharacters[i], CharacterSimulationTier.Tier1,
                    new CharacterRuntimeState(playerCharacters[i].Id, StartingResidentWorldPosition(i,
                            new WorldPosition(-6 + i * 1.5f, 0, 4)),
                        default, false, "starter_settlement", Clock.State.CalendarTicks, Clock.State.BiologicalTicks));
            DirectWorkCommands = new DirectWorkCommandService(Characters, AiAgents, AiScheduler, Work, CanCommandResident);
            DirectWorkCommands.EnsureRoleGroups();
            ConstructionClearance = new ConstructionClearanceCoordinator(Buildings, Production, Work, Construction,
                id => buildingCatalog.ResolveDefinition(id), FundingSources, ConstructionWorkTarget,
                buildingGridSize, new WorldPosition(buildingGridOrigin.x, buildingGridOrigin.y, buildingGridOrigin.z),
                WorldDefinition.MapData?.Buildability == null ? null : site =>
                    site != null && site.Kind == ProductionSiteKind.NaturalSource && !site.IsDepleted &&
                    WorldDefinition.MapData.Buildability.IsClearableRecipe(site.RecipeId.ToString()));
            buildingPlacementController.ConfigureClearance(ConstructionClearance);
            ContextCommands = new ContextCommandService(Work, AiScheduler, CanCommandResident);
            selectionProbe.ConfigureCommandPermission(CanCommandResident);
            industrialWork = new IndustrialWorkCoordinator(Industry, Inventories, Work,
                () => Characters.GetAll().Where(value => CanCommandResident(value.Id) &&
                    HealthFamily.GetCapabilities(value.Id).CanPerformOrdinaryWork).Select(value => value.Id),
                id => AiAgents.TryGet(id, out _) && AiScheduler.CanInterruptForDirectWork(id),
                id => AiAgents.TryGet(id, out var agent) ? agent.Position : default,
                id => Buildings.TryGet(id, out var storage) && storage.IsOperational &&
                    storage.OwnerSideId == new CombatSideId("player"));
            if (StoneAgeSettlement != null && WorldDefinition.MapData == null)
            {
                WorkOrderIndicators = gameObject.AddComponent<P94WorkOrderIndicatorSystem>();
                WorkOrderIndicators.Initialize(Work, StoneAgeSettlement);
            }
            var starterIds = new HashSet<StableEntityId>(playerCharacters.Select(value => value.Id));
            TierPolicy = new TierDistancePolicy(Tiers,
                new TierDistancePolicySettings(220f, 300f, 600f, 800f, 32, 8, 2),
                id => starterIds.Contains(id) || Work.HasOpenJob(id) ||
                    selectionProbe.SelectedCharacterIds.Contains(id));
            Combat = new CombatService(Characters, HealthFamily, new Tier1CombatPositionProvider(AiRuntime),
                worldSeed, Tiers);
            AiRuntime.SetCombatActivityProvider(characterId => Combat.TryGetCombatant(characterId, out _));
            AiRuntime.SetMovementRouteValidator(GateAllowsMovement);
            foreach (var character in playerCharacters) Combat.RegisterSide(character.Id, new CombatSideId("player"));
            CombatCommands = new CombatCommandService(Combat, CanCommandResident);
            HealthFamily.ChildBorn += OnChildBorn;
            ManualMoveCommands = new ManualMoveCommandService(Work, Tiers,
                ManualMoveCommandService.DefaultFormationSpacing, PrepareNavigationForManualMove, CanCommandResident);
            MilitaryUnits = new MilitaryUnitService(Characters, HealthFamily);
            MilitaryRepresentations = new MilitaryUnitRepresentationService(MilitaryUnits, Tiers, Combat,
                Characters, HealthFamily);
            MilitaryCommands = new MilitaryUnitCommandService(MilitaryUnits, ManualMoveCommands);
            MilitaryCombat = new MilitaryUnitCombatService(MilitaryUnits, Combat);
            PlayerControl = new PlayerControlService(Characters, HealthFamily, Tiers, Dynasty, MilitaryUnits,
                AiScheduler, new MilitarySideId("player"), CanCommandResident);
            Battles = new BattleResolutionService(Characters, Combat, MilitaryUnits, MilitaryRepresentations,
                MilitaryCombat, PlayerControl);
            GlobalMap = new GlobalMapService(StarterGlobalMap.CreateTiled(WorldDefinition,
                Clock.State.CalendarTicks, worldSeed), MilitaryUnits,
                Battles);
            GlobalMap.BeforeDeparture = id =>
            {
                if (MilitaryUnits.TryGet(id, out var unit) && unit.OwnerSideId == new MilitarySideId("player"))
                    MilitaryUnits.LoadInitialSupply(id, Inventories, FundingSources());
            };
            ConnectedWorld = new ConnectedWorldService(GlobalMap.Locations,
                ConnectedWorldLayout.For(WorldDefinition), Clock.State.CalendarTicks);
            StarterSettlementId = GlobalMap.Settlements.Single(x => x.LocationId == new WorldLocationId("starter_settlement")).Id;
            BuildingTerritories = new SettlementTerritoryRegistry();
            var starterTerritoryRadius = Mathf.Max(1, Mathf.RoundToInt(128f / buildingGridSize));
            BuildingTerritories.Replace(StarterSettlementId, new[]
            {
                new GridTerritoryRegion(-starterTerritoryRadius, -starterTerritoryRadius,
                    starterTerritoryRadius, starterTerritoryRadius)
            });
            BuildingPlacementService.ConfigureTerritories(BuildingTerritories);
            Construction.ConfigureLogistics(ConstructionSites, Work, WorldPiles, StarterSettlementId, HaulClaims);
            InhabitableRegion?.BindCanonicalMap(GlobalMap.Locations.Select(x => x.Id.Value));
            SynchronizeStarterSettlement();
            ModeTransitions = new GameModeTransitionService(GlobalMap, Tiers, MilitaryUnits,
                MilitaryRepresentations, Battles, PlayerControl);
            BuildingDamage = new BuildingDamageService(Buildings,
                buildingCatalog.Entries.Select(entry => buildingCatalog.ResolveDefinition(new BuildingDefinitionId(entry.DefinitionId))),
                Production, Inventories, WorldPiles, Weather, Combat, BuildingOccupancy);
            BuildingLifecycle = new BuildingLifecycleService(Buildings,
                id => buildingCatalog.ResolveDefinition(id), BuildingDamage, Inventories, WorldPiles,
                BuildingOccupancy, Production);
            BuildingLifecycle.BuildingChanged += OnBuildingChanged;
            BuildingLifecycle.ConfigureWork(Work, building =>
            {
                var cells = building.PlacementCells;
                var x = cells.Count > 0 ? cells.Average(c => c.X) : building.Coordinate.X;
                var z = (cells.Count > 0 ? cells.Min(c => c.Z) : building.Coordinate.Z) - 1.5f;
                var wx = buildingGridOrigin.x + (float)x * buildingGridSize;
                var wz = buildingGridOrigin.z + z * buildingGridSize;
                return new WorldPosition(wx, WorldHeight.SampleHeight(wx, wz), wz);
            });
            aiWorld.BuildingLifecycle = BuildingLifecycle;
            BuildingLifecycle.BuildingDemolished += OnBuildingDemolished;
            var strategicDefinitions = buildingCatalog.Entries.Select(entry =>
                buildingCatalog.ResolveDefinition(new BuildingDefinitionId(entry.DefinitionId))).ToArray();
            StarterEnemyAi = AiRuntimeBootstrap.CreateStarterEnemy(worldSeed,
                Clock.State.CalendarTicks, Clock.State.BiologicalTicks, Characters, HealthFamily, Tiers, GlobalMap, Buildings,
                WorldDefinition.MapData == null ? BuildingPlacementService : CreateEnemyPlacement(),
                Resources, ProductionRecipes, Technologies, strategicDefinitions, Weather,
                position => { var p = TiledWorld.ProjectToGround(new Vector3(position.X, position.Y, position.Z));
                    return new WorldPosition(p.x, p.y, p.z); });
            if (WorldDefinition.MapData != null)
            { InitializeEnemyCamp(); NeighbourEconomy=new NeighbourEconomyRuntime(this,buildingGridSize,buildingGridOrigin); }
            Appearances.EnsureDefaults(Characters.GetAll());
            HealthFamily.BindSuccession(StarterEnemyAi.Succession);
            BuildingDamage.RegisterEconomyScope(StarterEnemyAi.Production, StarterEnemyAi.Inventories,
                StarterEnemyAi.WorldPiles);
            StrategicAi = new AiStrategicScheduler(worldSeed, Characters, HealthFamily,
                MilitaryUnits, GlobalMap, Battles, Buildings, BuildingDamage);
            StrategicAi.AddFaction(StarterEnemyAi);
            StarterEnemyAi.Training = new MilitaryTrainingService(Characters, HealthFamily, Buildings,
                StarterEnemyAi.Inventories, MilitaryUnits, Appearances, worldSeed,
                newUnitRepresentation: MilitaryUnitRepresentationMode.Aggregate, newUnitLocation: "enemy_settlement");
            MilitaryTraining = new MilitaryTrainingService(Characters, HealthFamily, Buildings, Inventories,
                MilitaryUnits, Appearances, worldSeed);
            MilitaryTraining.IsRuler = id => PlayerRealm.CurrentRulerId == id;
            Func<MilitaryTrainingRecordState, bool> canTrain = record =>
                Buildings.TryGet(record.BarracksId, out var trainingBuilding) && trainingBuilding.IsOperational &&
                trainingBuilding.OwnerSideId == record.SideId &&
                Characters.TryGet(record.CharacterId, out var trainee) && trainee.LifeState == CharacterLifeState.Alive &&
                !HealthFamily.GetCapabilities(record.CharacterId).IsIncapacitated;
            MilitaryTraining.IsAtBarracks = canTrain;
            StarterEnemyAi.Training.IsAtBarracks = canTrain;
            MilitaryAttack = new MilitaryAttackCommandService(Characters, Buildings, MilitaryUnits,
                MilitaryCommands, MilitaryCombat, BuildingDamage, AiAgents, MilitaryTraining, worldSeed, CanCommandResident);
            SettlementCapture = new SettlementFlagCaptureService(worldSeed, GlobalMap, Buildings, BuildingDamage,
                Characters, MilitaryUnits);
            SettlementCapture.BindCapturedSoldiers((ids, barracksId, winner, tick) =>
            {
                MilitaryTraining.RemoveMilitaryHistory(ids);
                StarterEnemyAi.Training.RemoveMilitaryHistory(ids);
                var destination = winner.Value == "player" ? MilitaryTraining : StarterEnemyAi.Training;
                destination.RegisterCapturedPrivates(ids, barracksId, winner, tick);
            });
            Diagnostics = new DiagnosticService(worldSeed, Research, Resources, Inventories, Buildings,
                Characters, Appearances, HealthFamily, Domestic, MilitaryUnits, MilitaryTraining, Tiers, Combat,
                () => Clock.State.CalendarTicks, () => Clock.State.BiologicalTicks,
                connectedWorld: ConnectedWorld);
            RegisterSettlementCaptureFlags();
            MilitaryAttack.BindSettlementCapture(SettlementCapture);
            var eventTargets = new CharacterEventTargetSource(Characters,
                () => Characters.GetAll().Select(x => x.Id).Except(StarterEnemyAi.Residents),
                () => StarterEnemyAi.Residents,
                () => Clock.State.BiologicalTicks);
            IEventTargetSource combinedEventTargets = WorldPopulation?.PoiPlan == null
                ? eventTargets
                : new CompositeEventTargetSource(eventTargets,
                    new WorldMapPoiEventTargetSource(WorldPopulation.PoiPlan.Points));
            Events = new EventService(StoneAgeEventCatalog.Create(WorldPopulation?.PoiPlan?.Points), combinedEventTargets,
                new CanonicalEventEffectExecutor(Characters, HealthFamily), worldSeed,
                Clock.State.CalendarTicks);
            WorldPopulation?.InitializePoiExploration(ConnectedWorld, Events);
            CampaignProgress = new CampaignProgressService(ActiveNewGameConfiguration, new CombatSideId("player"),
                StarterEnemyAi, GlobalMap, MilitaryUnits, StrategicAi, Battles, Clock.State.CalendarTicks, GameRun);
            gameObject.AddComponent<StrategicAiDebugOverlay>().Initialize(StrategicAi, pointerRaycaster,
                () => AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1d / (int)Clock.Speed)));
            BuildingDamage.BuildingChanged += OnBuildingChanged;
            Tiers.BindTransitionGuard(new CharacterTierTransitionGuardSet(Combat, PlayerControl));
            AiScheduler.BindExternalActivityOwner(new CharacterActivityOwnerSet(Combat, PlayerControl, MilitaryTraining, StarterEnemyAi.Training));
            NeighbourEconomy?.BindActivityOwner(new CharacterActivityOwnerSet(Combat, PlayerControl, MilitaryTraining, StarterEnemyAi.Training));
            Combat.BindMovementSuppression(PlayerControl);
            Combat.BindCombatModifier((characterId, tick) => Math.Min(2000,
                MilitaryUnits.ConsumeCombatSupplyForCharacter(characterId, tick) *
                MilitaryTraining.GetDamagePerThousand(characterId) / 1000));
            AiRuntime.SimulationAdvanced += OnSimulationAdvanced;
            gameObject.AddComponent<LivestockPresentation>().Initialize(this);
            gameObject.AddComponent<HuntingPresentation>().Initialize(this, characterCatalog.HuntingArrow);
            gameObject.AddComponent<BarracksTrainingPresentation>().Initialize(this);
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

            var saveProfile = StartupSaveProfile.ForWorld(WorldDefinition);
            var configuredSaveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            var saveDirectory = string.IsNullOrWhiteSpace(configuredSaveDirectory)
                ? saveProfile.ResolveDirectory(Application.persistentDataPath)
                : configuredSaveDirectory;
            var saveStore = new FileSaveStore(saveDirectory);
            Saves = new SaveCoordinator(ActiveNewGameConfiguration.SessionId, Clock, saveStore, Production, Research, Succession,
                HealthFamily, Weather, Combat, MilitaryUnits, PlayerControl, BuildingDamage, GlobalMap, StrategicAi,
                Events, CampaignProgress, Appearances, Work, ConnectedWorld, Domestic, Tiers,
                MilitaryTraining, SettlementCapture, MilitaryAttack, Diagnostics,
                saveProfile.SaveGenerationId, WorldPopulation?.WildlifePopulation, Facilities);
            PlayerSaves = new LocalGameplaySaveFacade(this, Saves, saveStore,
                saveProfile.SaveGenerationId);
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
                    () => Characters.GetAll().Where(x => CanCommandResident(x.Id)).Select(x => x.Id).ToArray(),
                    position => cameraController.FocusPosition(new Vector3(position.X, position.Y, position.Z), 28f),
                     BuildingLifecycle, BuildingPathing, Facilities, ConstructionClearance, StrategicAi),
                pointerRaycaster, input);
            ManualMoveInput.EnableContextMenus();
            gameObject.AddComponent<SnowTrackPresentation>().Initialize(WorldHeight, CharacterPresentations,
                TiledWorld.ArtWorldToDesign);

            var userSettings = UserSettingsStore.CreateDefault().Load().Settings;
            UserSettingsRuntime.ApplyGameplay(userSettings, input, cameraController, GameplayUi);

            TiledWorld.ConfigureStreaming(CameraStreamingFocus, StreamingPhysicalPositions,
                StreamingNavigationRoutes);
            BuildingPlacementService.ConfigureEnvironment(cells =>
                WorldPlacementSurface.AllowsFootprint(cells, buildingGridSize, buildingGridOrigin));
            BuildingPlacementService.ConfigureRequestEnvironment(context => WorldPlacementSurface.EvaluateFootprint(
                context.Cells,buildingGridSize,buildingGridOrigin,
                context.Request.Definition.Id.ToString()=="fishing_pier" ? 20f :
                    IsShoreFacility(context.Request.Definition.Id) ? 4.5f : (float?)null,
                context.Request.Definition.Id.ToString()=="gate",
                IsShoreFacility(context.Request.Definition.Id),
                context.Request.Definition.Id.ToString()=="fishing_pier").CanPlace);
            BuildingPlacementService.ConfigureRuleEvaluator(EvaluateSpecialPlacementRule);
            BuildingPathing.GateChanged+=(id,open)=> { if(BuildingPresentations.TryGet(id,out var gate))gate.SetGateOpen(open); };

            loadingOverlay.SetStage("Подготавливаем береговые площадки…");
            while(!shoreIndexTask.IsCompleted) yield return null;
            completeShoreIndex();
            completeShoreIndex=null;

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
                    yield break;
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

        private void Start()
        {
            if (!IsInitialized || WorldDefinition?.MapData == null || cameraController == null) return;
            cameraController.FocusPosition(new Vector3(WorldDefinition.PlayerStart.X, 0f,
                WorldDefinition.PlayerStart.Z), 120f);
        }

        private void OnDestroy()
        {
            if (TiledWorld != null) TiledWorld.VisualTilesChanged -= RefreshAuthoredTreeVisibility;
            if (AiRuntime != null) AiRuntime.SimulationAdvanced -= OnSimulationAdvanced;
            if (HealthFamily != null) HealthFamily.ChildBorn -= OnChildBorn;
            if (buildingPlacementController != null) buildingPlacementController.BuildingPlaced -= OnBuildingPlaced;
            if (BuildingDamage != null) BuildingDamage.BuildingChanged -= OnBuildingChanged;
            if (BuildingLifecycle != null)
            {
                BuildingLifecycle.BuildingChanged -= OnBuildingChanged;
                BuildingLifecycle.BuildingDemolished -= OnBuildingDemolished;
            }
            WorldPopulation?.ShutdownWildlife();
            Tier2Runtime?.Dispose();
        }

        private void RefreshAuthoredTreeVisibility()
        {
            if (environmentLod == null || TiledWorld == null) return;
            var visible = new HashSet<WorldTileId>(TiledWorld.VisualTiles);
            environmentLod.RefreshVisibility(position => !TiledWorld.StrategicOverviewVisible &&
                !WorldHeight.IsWater(position.x, position.z) &&
                WorldDefinition.TryGetTile(position.x, position.z, out var tile) && visible.Contains(tile));
        }

        private void AdvanceSubmersion(GameTimeAdvance advance)
        {
            var minute = TimeSpan.TicksPerMinute;
            var damage = (int)Math.Min(100, (Clock.State.CalendarTicks / minute -
                (Clock.State.CalendarTicks - advance.CalendarDelta.Ticks) / minute) * 4);
            if (damage <= 0) return;
            foreach (var movement in AiRuntime.Movements)
            {
                var point = movement.CurrentPosition;
                if (!WorldHeight.TrySampleWaterSurface(point.X, point.Z, out var water)) continue;
                var character = Characters.Get(movement.CharacterId);
                if (character.LifeState != CharacterLifeState.Alive) continue;
                var headHeight = 1.7f;
                var animator = movement.Presenter.Animator;
                if (animator != null && animator.isHuman)
                {
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) headHeight = Math.Max(.6f, head.position.y - point.Y + .15f);
                }
                character.SetHealth(Game.Domain.Health.SubmersionRules.Damage(character.Health,
                    water - point.Y, headHeight, damage));
                if (character.Health.Head.Value == 0 || character.Health.Torso.Value == 0)
                    Succession.ProcessDeath(character.Id, Clock.State.BiologicalTicks, Clock.State.CalendarTicks);
            }
        }

        private void OnSimulationAdvanced(GameTimeAdvance advance)
        {
            var targetCalendarTick = Clock.State.CalendarTicks;
            AdvanceSubmersion(advance);
            ConnectedWorld?.AdvanceTo(Clock.State.CalendarTicks);
            if (ConnectedWorld != null)
                foreach (var presenter in demoPresenters.Where(x => x != null && x.gameObject.activeInHierarchy))
                    ConnectedWorld.ReportPhysicalPresence(presenter.transform.position.x, presenter.transform.position.z);
            WorldPopulation?.AdvancePoiExploration(targetCalendarTick);
            ConstructionClearance?.Advance();
            industrialWork?.Advance(targetCalendarTick);
            DirectWorkCommands?.ReevaluateQueued();
            PerformanceBenchmark?.BeginSimulationAllocationSample();
            var startCalendarTick = checked(targetCalendarTick - advance.CalendarDelta.Ticks);
            Tier2Runtime.Step(advance, Weather.GetMovementModifierForInterval(startCalendarTick, targetCalendarTick));
            WorldPopulation?.AdvanceWildlifeTo(targetCalendarTick);
            PerformanceBenchmark?.RecordTier2Allocation();
            Tier3Adapter.AdvanceAllToCurrent();
            Tier2Runtime.AdvanceResearch(Research, targetCalendarTick);
            Tier3Adapter.AdvanceResearchToCurrent(Research);
            PerformanceBenchmark?.RecordTier3AndResearchAllocation();
            tier2WorkerBuffer.Clear();
            Tier2Runtime.ConsumeProductionWorkers(tier2WorkerBuffer);
            RefreshTier3WorkerCache();
            MergeSortedDistinct(tier2WorkerBuffer, tier3WorkerCache, abstractWorkerBuffer);
            var currentWeather = Weather.GetWeatherAtCalendarTick(targetCalendarTick);
            Production.AdvanceFireFuel(targetCalendarTick, currentWeather.TemperatureCelsius);
            if (targetCalendarTick >= nextWeatherRosterRefreshTick)
            {
                outdoorWorkerBuffer.Clear();
                Production.CollectActiveWorkerIds(outdoorWorkerBuffer);
                outdoorWorkerBuffer.AddRange(abstractWorkerBuffer);
                RemoveWarmResidents(outdoorWorkerBuffer);
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
            Facilities.AdvanceClinics(targetCalendarTick);
            foreach (var character in Characters.GetAll())
                if (CharacterSpawner.RequiresStyleRefresh(character) &&
                    CharacterPresentations.TryGet(character.Id, out _)) Tier1Adapter.RebuildPresentation(character);
            BuildingDamage.AdvanceFireTo(targetCalendarTick);
            BuildingLifecycle.AdvanceWearTo(targetCalendarTick);
            Production.Population = CountAbstractPlayerPopulation();
            Production.MaintainFisheryModes();
            Production.AdvanceHuntingPopulation(targetCalendarTick);
            Production.MaintainContinuousDeliveries(Work);
            Production.MaintainLivingBuildingLogistics(Work);
            Production.AdvanceTo(targetCalendarTick, abstractWorkerBuffer);
            Production.ReconcileHorseRiders(Characters.GetAll().Where(x => x.LifeState == CharacterLifeState.Alive)
                .Select(x => x.Id));
            ResourceEcology?.AdvanceTo(targetCalendarTick, resourceRespawnSurface);
            PerformanceBenchmark?.RecordCanonicalServicesAllocation();
            for (var i = 0; i < productionPresenters.Count; i++) productionPresenters[i].Refresh();
            foreach (var buildingView in BuildingPresentations.All) buildingView.RefreshStructuralPresentation();
            RefreshEnemyBuildings();
            WorldPopulation?.RefreshViews();
            WeatherPresentation.Refresh(targetCalendarTick);
            if (!performanceTierPolicySuppressed && Time.unscaledTime >= nextTierPolicyRefreshTime)
            {
                var focus = cameraController.transform.position;
                TierPolicy.Evaluate(new WorldPosition(focus.x, focus.y, focus.z));
                nextTierPolicyRefreshTime = Time.unscaledTime + TierPolicyRefreshIntervalSeconds;
            }
            PerformanceBenchmark?.RecordPresentationAndTierPolicyAllocation();
        }

        private void RemoveWarmResidents(List<StableEntityId> candidates)
        {
            candidates.RemoveAll(id => Facilities.HomeAssignments.ContainsKey(id));
            foreach (var fireId in Production.HeatedBuildings.OrderBy(x => x))
            {
                if (!Buildings.TryGet(fireId, out var fire)) continue;
                var definition = buildingCatalog.ResolveDefinition(fire.DefinitionId);
                var point = BuildingSpawner.GridToWorld(fire.Coordinate, definition, fire.Orientation);
                var center = new WorldPosition(point.x, point.y, point.z);
                var warmed = candidates.Where(id => Tiers.TryGetTier(id, out _) &&
                        Tiers.GetRuntimeState(id).Position.DistanceSquared(center) <= 64f)
                    .OrderBy(id => Tiers.GetRuntimeState(id).Position.DistanceSquared(center))
                    .ThenBy(id => id).Take(5).ToArray();
                foreach (var id in warmed) candidates.Remove(id);
            }
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

        private int CountAbstractPlayerPopulation()
        {
            if (Tiers == null) return Characters.Count;
            var count = 0;
            foreach (var id in Tiers.ManagedIds)
                if (Tiers.GetTier(id) != CharacterSimulationTier.Tier1 &&
                    Characters.TryGet(id, out var character) && character.LifeState == CharacterLifeState.Alive)
                    count++;
            return count;
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

        private System.Threading.Tasks.Task<ShorePlacementIndex[]> shoreIndexTask;
        private Action completeShoreIndex;
        private readonly HashSet<(GridCoordinate,BuildingOrientation)> preparedShoreAnchors=new();
        private void CreateFishingLandingMarkers()
        {
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("fishing_camp"));
            var pierDefinition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("fishing_pier"));
            var campPads = new List<(GridCoordinate anchor, BuildingOrientation facing, Vector3 center)>();
            var pierPads = new List<(GridCoordinate anchor, BuildingOrientation facing, Vector3 center)>();
            var world=WorldDefinition;var seed=ActiveNewGameConfiguration.WorldSeed;
            var size=buildingGridSize;var origin=buildingGridOrigin;var footprint=definition.Footprint;
            shoreIndexTask=System.Threading.Tasks.Task.Run(() => new[]
            {
                ShorePlacementIndex.Create(world,seed,footprint,size,origin.x,origin.z),
                ShorePlacementIndex.Create(world,seed,pierDefinition.Footprint,size,origin.x,origin.z,true)
            });
            completeShoreIndex=()=>
            {
                var indices=shoreIndexTask.GetAwaiter().GetResult(); var index=indices[0]; var pierIndex=indices[1];
                WorldPlacementSurface.ImportShoreCells(index.Cells,size,origin);
                foreach(var candidate in index.Candidates)preparedShoreAnchors.Add((candidate.Anchor,candidate.Facing));
                campPads.AddRange(index.Candidates.Select(x=>(x.Anchor,x.Facing,new Vector3(x.X,x.Y,x.Z))));
                pierPads.AddRange(pierIndex.Candidates.Select(x=>(x.Anchor,x.Facing,new Vector3(x.X,x.Y,x.Z))));
                var lakeIds=new HashSet<string>(world.MapData.Hydrology.Lakes.Select(x=>x.Id));
                foreach(var candidate in index.Candidates.Where(x=>lakeIds.Contains(x.WaterBodyId)))
                {
                    var marker=WorldPropCatalog.Create("logs",transform);
                    marker.name="Fishing landing place "+candidate.WaterBodyId;
                    marker.transform.position=new Vector3(candidate.X,candidate.Y+.03f,candidate.Z);
                    marker.transform.localScale*=.22f;
                    marker.transform.rotation=Quaternion.Euler(0,candidate.Facing.Degrees(),0);
                }
                Debug.Log($"WM15 shoreline candidates prepared: camps={campPads.Count}, adaptive piers={pierPads.Count}");
            };
            (Vector3 point,BuildingOrientation orientation)? cached=null;
            Vector3 lastCursor=Vector3.positiveInfinity;
            int cursorIndex=0;
            string lastShoreDefinition=string.Empty;
            List<(GridCoordinate anchor, BuildingOrientation facing, Vector3 center)> near=new();
            var knownCount=-1;
            var reportedRejections = new HashSet<string>();
            buildingPlacementController.ShoreSnap=point=>
            {
                var activeDefinitionId=buildingPlacementController.ActiveDefinitionId?.ToString() ?? string.Empty;
                var adaptivePier=activeDefinitionId=="fishing_pier";
                var activeDefinition=adaptivePier?pierDefinition:definition;
                var pads=adaptivePier?pierPads:campPads;
                if(lastShoreDefinition!=activeDefinitionId)
                { lastShoreDefinition=activeDefinitionId;cached=null;lastCursor=Vector3.positiveInfinity; }
                if((point-lastCursor).sqrMagnitude>64 || (!cached.HasValue && cursorIndex>=near.Count && knownCount!=pads.Count))
                {
                    lastCursor=point;knownCount=pads.Count;cursorIndex=0;cached=null;
                    near=pads.Where(x=>new Vector2(x.center.x-point.x,x.center.z-point.z).sqrMagnitude<60*60)
                        .OrderBy(x=>new Vector2(x.center.x-point.x,x.center.z-point.z).sqrMagnitude).ToList();
                }
                if(cached.HasValue)return cached;
                var timer=System.Diagnostics.Stopwatch.StartNew();
                while(cursorIndex<near.Count && timer.Elapsed.TotalMilliseconds<2)
                {
                    var candidate=near[cursorIndex];
                    var cells=activeDefinition.Footprint.EnumerateCells(candidate.anchor,candidate.facing).ToArray();
                    if(!WorldPlacementSurface.PrepareFootprint(cells,buildingGridSize,buildingGridOrigin,true,2,adaptivePier))return null;
                    cursorIndex++;
                    var verdict=BuildingPlacementService.Evaluate(activeDefinition,candidate.anchor,candidate.facing);
                    if(!verdict.IsValid)
                    {
                        if(System.Environment.GetCommandLineArgs().Contains("-wm15Evening"))
                        {
                            var surface=WorldPlacementSurface.EvaluateFootprint(cells,buildingGridSize,buildingGridOrigin,
                                adaptivePier?20f:4.5f,false,true,adaptivePier);
                            var reason=verdict.FailureReason+" "+surface.BlockedReason;
                            if(reportedRejections.Add(reason))Debug.Log("WM15_SHORE_CANDIDATE "+reason+" at "+candidate.center);
                        }
                        continue;
                    }
                    cached=(candidate.center,candidate.facing);break;
                }
                return cached;
            };
            buildingPlacementController.BuildingPlaced+=(_,__)=>{cached=null;lastCursor=Vector3.positiveInfinity;};
        }

        private static bool IsShoreFacility(BuildingDefinitionId id) =>
            id.ToString()=="fishing_camp" || id.ToString()=="fishing_pier";

        private bool BuildingOccupancyCellsBlocked(GridCoordinate anchor, BuildingDefinition definition, BuildingOrientation facing)
            => !BuildingPlacementService.Evaluate(definition, anchor, facing).IsValid;

        private PlacementFailureReason EvaluateSpecialPlacementRule(PlacementRuleContext context)
        {
            var constraints = context.Request.Definition.Placement.Constraints;
            if ((constraints & BuildingPlacementConstraint.Shoreline) != 0)
            {
                var centerX=buildingGridOrigin.x+context.Request.Anchor.X*buildingGridSize;
                var centerZ=buildingGridOrigin.z+context.Request.Anchor.Z*buildingGridSize;
                var body=WorldHeight.NearestWaterBodyId(centerX,centerZ);
                if (context.Request.Definition.Id.ToString()=="fishing_camp" &&
                    WorldDefinition.MapData.Hydrology.Lakes.Any(x=>x.Id==body) &&
                    !preparedShoreAnchors.Contains((context.Request.Anchor,context.Request.Orientation)))
                    return PlacementFailureReason.ShorelineRequired;
                var adaptivePier=context.Request.Definition.Id.ToString()=="fishing_pier";
                if (!(IsShoreFacility(context.Request.Definition.Id) && preparedShoreAnchors.Contains((context.Request.Anchor,context.Request.Orientation))) &&
                    !ShoreBuildingPlacement.IsValid(WorldHeight,context.Request.Anchor,
                    context.Request.Definition.Footprint,context.Request.Orientation,
                    buildingGridSize,buildingGridOrigin.x,buildingGridOrigin.z,adaptivePier))
                    return PlacementFailureReason.ShorelineRequired;
            }
            if ((constraints & BuildingPlacementConstraint.Groundwater) != 0)
            {
                var groundwater = context.Cells.All(cell =>
                {
                    var x = buildingGridOrigin.x + (cell.X + .5f) * buildingGridSize;
                    var z = buildingGridOrigin.z + (cell.Z + .5f) * buildingGridSize;
                    return !WorldHeight.IsWater(x, z) &&
                           WorldHeight.SampleHeight(x, z) <= TiledWorldHeightSampler.WaterLevelMeters + 14f;
                });
                if (!groundwater) return PlacementFailureReason.GroundwaterRequired;
            }
            return PlacementFailureReason.None;
        }

        private void OnChildBorn(Character child)
        {
            // A child's allegiance follows their parents, never the currently viewed estate.
            if (child.Parents.Any(parent => CanCommandResident(parent.ParentId)))
                Combat.RegisterSide(child.Id, new CombatSideId("player"));
            Appearances.GetOrCreate(child);
            var state = new CharacterRuntimeState(child.Id, default, default, false, "local",
                lastSimulationCalendarTick: Clock.State.CalendarTicks,
                lastSimulationBiologicalTick: Clock.State.BiologicalTicks);
            Tiers.Materialize(child, CharacterSimulationTier.Tier3, state);
            Production.Population = Characters.Count;
            SynchronizeStarterSettlement();
        }

        public bool CanCommandResident(StableEntityId id)
        {
            if (!id.IsValid || Characters == null || !Characters.TryGet(id, out _)) return false;
            var memberships = GlobalMap?.Settlements.Where(value => value.ResidentCharacterIds.Contains(id)).ToArray();
            if (memberships != null && memberships.Length > 0)
                return memberships.All(value => value.OwnerSideId == new CombatSideId("player"));
            return Combat != null && Combat.TryGetSide(id, out var side) && side == new CombatSideId("player");
        }

        private bool GateAllowsMovement(StableEntityId characterId, WorldPosition from, WorldPosition to)
        {
            foreach (var gate in Buildings.All.Where(x => x.IsOperational &&
                         x.DefinitionId == new BuildingDefinitionId("gate")).OrderBy(x => x.Id))
            {
                var definition = buildingCatalog.ResolveDefinition(gate.DefinitionId);
                var center = BuildingSpawner.GridToWorld(gate.Coordinate, definition, gate.Orientation);
                if (DistanceToSegmentSquared(center.x, center.z, from.X, from.Z, to.X, to.Z) > 9f) continue;
                CombatSideId? side = Combat.TryGetSide(characterId, out var combatSide) ? combatSide : null;
                if (BuildingPathing.EvaluatePassage(gate.Id, characterId, side) != GatePassageResult.Allowed)
                    return false;
            }
            return true;
        }

        private static float DistanceToSegmentSquared(float px, float pz, float ax, float az, float bx, float bz)
        {
            var dx = bx - ax; var dz = bz - az;
            var length = dx * dx + dz * dz;
            var t = length <= 0.0001f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / length);
            var x = ax + dx * t; var z = az + dz * t;
            var ex = px - x; var ez = pz - z;
            return ex * ex + ez * ez;
        }

        public void SynchronizeStarterSettlement()
        {
            if (GlobalMap == null) return;
            var current = GlobalMap.Settlements.SingleOrDefault(x => x.Id == StarterSettlementId);
            GlobalMap.SynchronizeSettlement(StarterSettlementId,
                new WorldLocationId("starter_settlement"), current?.OwnerSideId ?? new CombatSideId("player"),
                Buildings.All.Where(x => current == null || x.OwnerSideId == current.OwnerSideId).Select(x => x.Id),
                Characters.GetAll().Where(x => CanCommandResident(x.Id))
                    .Select(x => x.Id));
        }

        public void ApplyLocalLocationScope(WorldLocationId locationId)
        {
            // P10 is one persistent physical world. Entering a named location changes strategic focus only;
            // it must never hide or teleport canonical physical buildings.
            foreach (var presenter in BuildingPresentations.All)
                if (presenter != null) presenter.gameObject.SetActive(true);
            var location = GlobalMap.GetLocation(locationId);
            var current = cameraController.transform.position;
            var x = Mathf.Clamp(location.MapX, cameraController.Settings.BoundsMin.x,
                cameraController.Settings.BoundsMax.x);
            var z = Mathf.Clamp(location.MapY, cameraController.Settings.BoundsMin.y,
                cameraController.Settings.BoundsMax.y);
            cameraController.transform.position = new Vector3(x, current.y, z);
            TiledWorld?.RefreshNow(new WorldPosition(x, WorldHeight.SampleHeight(x, z), z),
                StreamingPhysicalPositions(), StreamingNavigationRoutes());
        }

        public int AdvanceMilitaryTrainingTo(long calendarTick)
        {
            var candidates = MilitaryTraining.Active.Select(record => record.CharacterId).ToArray();
            var completed = MilitaryTraining.AdvanceTo(calendarTick);
            if (completed > 0)
            {
                var pending = MilitaryTraining.Active.Select(record => record.CharacterId).ToHashSet();
                foreach (var id in candidates)
                    if (!pending.Contains(id) && CharacterPresentations.TryGet(id, out _))
                        Tier1Adapter.RebuildPresentation(Characters.Get(id));
                selectionProbe.RefreshSelectedPresentations();
            }
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
            if (WorldDefinition.MapData == null) RebindRegionResourcePresenters();
            foreach (var building in Buildings.All.Where(x=>x.OwnerSideId.Value=="player"))
                ConstructionSites.RebindRestored(building);
            ConstructionClearance?.RebindAfterLoad(Buildings.All.Where(x=>x.OwnerSideId.Value=="player"));
            NeighbourEconomy?.RebindAfterLoad();
            BuildingPathing.RebindAfterLoad();
            foreach (var gate in Buildings.All.Where(x => x.IsOperational &&
                         x.DefinitionId == new BuildingDefinitionId("gate")))
                BuildingPathing.SetGateAccessMode(gate.Id, Facilities.GetPolicy(gate.Id).GateAccess);
            if (WorldDefinition.MapData == null) RegisterStarterTrees();
            foreach (var view in productionPresenters)
                if (view != null && view.IsBound && Production.TryGetSite(view.Site.Id, out var canonical))
                    view.Bind(canonical, Color.white, true);
            foreach (var building in Buildings.All)
                if (building.OwnerSideId.Value=="player" && (building.DefinitionId == new BuildingDefinitionId("primitive_crop_plot") ||
                     (building.DefinitionId == new BuildingDefinitionId("primitive_garden") || building.DefinitionId.ToString() == "animal_pasture")) &&
                    BuildingPresentations.TryGet(building.Id, out var cropView)) RegisterCropPlot(building, cropView);
            WorldPopulation?.RebindAfterLoad();
            DirectWorkCommands.EnsureRoleGroups();
            DirectWorkCommands.ReevaluateQueued();
            WeatherPresentation.Refresh(Clock.State.CalendarTicks);
            ApplyLocalLocationScope(GlobalMap.ActiveLocationId);
            GlobalMapBridge.ReconcileAfterLoad();
            GameplayUi?.RefreshAfterStateRebind();
            TiledWorld?.RefreshNow(CameraStreamingFocus(), StreamingPhysicalPositions(), StreamingNavigationRoutes());
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

        private BuildingPlacementService enemyPlacement;
        private BuildingPresentationSpawner enemyBuildingSpawner;

        private void Update()
        {
            // Camera inspection must materialize nearby residents even while simulation time is paused.
            if (!IsInitialized || TierPolicy == null || Clock == null || !Clock.State.IsPaused || performanceTierPolicySuppressed ||
                Time.unscaledTime < nextTierPolicyRefreshTime) return;
            var focus = cameraController.transform.position;
            TierPolicy.Evaluate(new WorldPosition(focus.x, focus.y, focus.z));
            nextTierPolicyRefreshTime = Time.unscaledTime + TierPolicyRefreshIntervalSeconds;
        }

        private BuildingGridBounds WorldGridBounds() => new BuildingGridBounds(
            Mathf.CeilToInt((WorldDefinition.Origin.X - buildingGridOrigin.x) / buildingGridSize),
            Mathf.CeilToInt((WorldDefinition.Origin.Z - buildingGridOrigin.z) / buildingGridSize),
            Mathf.FloorToInt((WorldDefinition.Origin.X + WorldDefinition.WidthMeters - buildingGridOrigin.x) / buildingGridSize) - 1,
            Mathf.FloorToInt((WorldDefinition.Origin.Z + WorldDefinition.DepthMeters - buildingGridOrigin.z) / buildingGridSize) - 1);

        private BuildingPlacementService CreateEnemyPlacement()
        {
            enemyPlacement = new BuildingPlacementService(Buildings, BuildingOccupancy, WorldGridBounds());
            enemyPlacement.ConfigureTerritories(BuildingTerritories);
            enemyPlacement.ConfigureEnvironment(cells => cells.All(cell =>
                WorldHeight.IsBuildable(buildingGridOrigin.x + cell.X * buildingGridSize,
                    buildingGridOrigin.z + cell.Z * buildingGridSize, 18f)));
            return enemyPlacement;
        }

        private void InitializeEnemyCamp()
        {
            var settlement = GlobalMap.GetSettlementAt(new WorldLocationId("enemy_settlement"));
            var location = GlobalMap.GetLocation(settlement.LocationId);
            var anchor = new GridCoordinate(Mathf.RoundToInt((location.MapX - buildingGridOrigin.x) / buildingGridSize),
                Mathf.RoundToInt((location.MapY - buildingGridOrigin.z) / buildingGridSize));
            BuildingTerritories.Replace(settlement.Id, new[] { new GridTerritoryRegion(
                anchor.X - 60, anchor.Z - 60, anchor.X + 60, anchor.Z + 60) });
            StarterEnemyAi.Construction.ConfigureSettlement(settlement.Id);
            StarterEnemyAi.ConstructionOrigin = new GridCoordinate(anchor.X + 12, anchor.Z - 14);
            enemyBuildingSpawner = new BuildingPresentationSpawner(buildingCatalog, BuildingPresentations,
                buildingGridSize, buildingGridOrigin, null, StarterEnemyAi.Inventories,
                position => TiledWorld.ProjectToGround(position).y);
            var ids = new List<StableEntityId>();
            var definitions = new[] { "stone_age_hut", "stone_age_hut", "storage_basket", "campfire" };
            var offsets = new[] { new GridCoordinate(-8, 6), new GridCoordinate(3, 6),
                new GridCoordinate(7, -5), new GridCoordinate(-2, -6) };
            var storage = new StorageService(StarterEnemyAi.Inventories, Resources);
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId(definitions[i]));
                var coordinate = new GridCoordinate(anchor.X + offsets[i].X, anchor.Z + offsets[i].Z);
                coordinate=FindStarterBuildingAnchor(enemyPlacement,definition,coordinate,BuildingOrientation.North,settlement.Id);
                var request = new BuildingPlacementRequest(definition, coordinate, BuildingOrientation.North, settlement.Id);
                var building = enemyPlacement.Confirm(request, settlement.OwnerSideId,
                    DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "enemy_start_building", i));
                building.MarkCompleted();
                storage.Attach(building, definition);
                ids.Add(building.Id);
            }
            GlobalMap.SynchronizeSettlement(settlement.Id, settlement.LocationId, settlement.OwnerSideId,
                ids, StarterEnemyAi.Residents);
            RefreshEnemyBuildings();
        }

        private void RefreshEnemyBuildings()
        {
            if (enemyBuildingSpawner == null || StarterEnemyAi == null) return;
            foreach (var building in Buildings.All.Where(value => value.OwnerSideId == StarterEnemyAi.Strategic.SideId))
                if (!BuildingPresentations.TryGet(building.Id, out _)) enemyBuildingSpawner.Spawn(building);
        }

        private GridCoordinate FindStarterBuildingAnchor(BuildingPlacementService placement,BuildingDefinition definition,
            GridCoordinate preferred,BuildingOrientation facing,SettlementId? settlement=null)
        {
            for(var radius=0;radius<=60;radius++)
            for(var dx=-radius;dx<=radius;dx++)
            for(var dz=-radius;dz<=radius;dz++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dz))!=radius)continue;
                var anchor=new GridCoordinate(preferred.X+dx,preferred.Z+dz);
                var request=new BuildingPlacementRequest(definition,anchor,facing,settlement);
                if(!placement.Evaluate(request).IsValid)continue;
                if(!definition.Footprint.EnumerateCells(anchor,facing).All(cell=>
                    WorldHeight.IsBuildable(buildingGridOrigin.x+cell.X*buildingGridSize,
                        buildingGridOrigin.z+cell.Z*buildingGridSize,18f)))continue;
                return anchor;
            }
            throw new InvalidOperationException("No dry free starter footprint for "+definition.Id);
        }

        private void SpawnDemoStorage()
        {
            // DEV_BOOTSTRAP_ONLY: a free completed basket makes the autonomous hauling slice immediately observable.
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            DemoStorageBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(5, 5),
                BuildingOrientation.North, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_building", 0));
            var inventory = Storage.Attach(DemoStorageBuilding, definition);
            if (inventory != null && Resources.Contains(new ResourceId("cooked_berries")))
                inventory.TryAdd(new ResourceId("cooked_berries"), new ResourceQuantity(24));
            if (inventory != null)
                foreach (var starter in new[] { "stone_spear", "wooden_shield", "combat_bow", "arrows", "leather_clothing" })
                {
                    var id = new ResourceId(starter);
                    if (Resources.Contains(id)) inventory.TryAdd(id, new ResourceQuantity(starter == "arrows" ? 24 : 4));
                }
            DemoStorageBuilding.MarkCompleted();
            DemoStoragePresenter = BuildingSpawner.Spawn(DemoStorageBuilding);
        }

        private void SpawnDemoShelter()
        {
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut"));
            DemoShelterBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(-7, 4),
                BuildingOrientation.South, new CombatSideId("player"),
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_start_building", 2));
            DemoShelterBuilding.MarkCompleted();
            DemoShelterPresenter = BuildingSpawner.Spawn(DemoShelterBuilding);
        }

        private void SpawnDemoBarracks()
        {
            var definition = buildingCatalog.ResolveDefinition(MilitaryTrainingService.BarracksDefinitionId);
            DemoBarracksBuilding = BuildingPlacementService.Confirm(definition,
                FindStarterBuildingAnchor(BuildingPlacementService,definition,new GridCoordinate(-1,6),BuildingOrientation.South),
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
                    .OrderByDescending(x => x.DefinitionId == new BuildingDefinitionId("chief_hut"))
                    .ThenByDescending(x => x.DefinitionId == new BuildingDefinitionId("stone_age_hut"))
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
                ToWorldPosition(ProjectToGameplayGround(buildingGridOrigin +
                    new Vector3(-6f * buildingGridSize, 0f, 5f * buildingGridSize))));
            var secondBed = new BedState(
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "starter_bed", 1),
                DemoShelterBuilding.Id, new GridCoordinate(-5, 5),
                ToWorldPosition(ProjectToGameplayGround(buildingGridOrigin +
                    new Vector3(-5f * buildingGridSize, 0f, 5f * buildingGridSize))));
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
            if(building?.DefinitionId.ToString()=="gate")
            {
                foreach(var wallView in BuildingPresentations.All.Where(x=>x.IsBound && x.Building.DefinitionId.ToString()=="palisade").ToArray())
                {
                    var wallId=wallView.BuildingId; BuildingSpawner.Despawn(wallView);
                    if(Buildings.TryGet(wallId,out var wall))BuildingSpawner.Spawn(wall);
                }
            }

            if (building != null && !building.IsOperational && Work != null)
                ConstructionClearance?.Register(building,
                    buildingPlacementController.LastPlacedClearanceSourceIds, WorkPriority.Normal);
            if (building != null && (building.DefinitionId == new BuildingDefinitionId("primitive_crop_plot") ||
                                     (building.DefinitionId == new BuildingDefinitionId("primitive_garden") || building.DefinitionId.ToString() == "animal_pasture")))
                RegisterCropPlot(building, presenter);
            if (building != null)
            {
                BuildingPathing.Reconcile(building);
                RegisterBuildingWorkplace(building, presenter);
            }
        }

        private WorldPosition ConstructionWorkTarget(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (BuildingPresentations.TryGet(building.Id, out var presenter) && presenter != null)
            {
                var bounds = new Bounds(presenter.transform.position, Vector3.one);
                foreach (var collider in presenter.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
                var target = ProjectToGameplayGround(new Vector3(bounds.center.x, 0f, bounds.min.z - 1.2f));
                return new WorldPosition(target.x, target.y, target.z);
            }
            var cells = building.PlacementCells;
            var x = cells.Count > 0 ? cells.Average(c => c.X) : building.Coordinate.X;
            var z = (cells.Count > 0 ? cells.Min(c => c.Z) : building.Coordinate.Z) - 1.5f;
            var wx = buildingGridOrigin.x + (float)x * buildingGridSize;
            var wz = buildingGridOrigin.z + (float)z * buildingGridSize;
            return new WorldPosition(wx, WorldHeight.SampleHeight(wx, wz), wz);
        }

        private void OnBuildingChanged(Building building)
        {
            BuildingPathing?.Reconcile(building);
            if (building != null && (building.IsDestroyed || building.OwnerSideId != new CombatSideId("player")))
                Facilities?.RemoveBuildingAssignments(building.Id);
            else if (building != null && building.IsOperational)
                Facilities?.ReconcileHousing();
            if (building != null && BuildingPresentations.TryGet(building.Id, out var presenter))
                {
                if (building.DefinitionId.ToString()=="animal_pasture" && building.IsOperational)
                    RegisterCropPlot(building,presenter);
                presenter.RefreshStructuralPresentation();
            }
        }

        private void OnBuildingDemolished(StableEntityId buildingId)
        {
            Facilities?.RemoveBuildingAssignments(buildingId);
            if (BuildingPresentations.TryGet(buildingId, out var presenter)) BuildingSpawner.Despawn(presenter);
        }

        private void RegisterCropPlot(Building building, BuildingPresenter buildingPresenter)
        {
            var definition = buildingCatalog.ResolveDefinition(building.DefinitionId);
            var world = buildingPresenter.transform.position;
            if (!Production.TryGetSite(building.Id, out var site))
            {
                site = ProductionSite.CreateCropPlot(building.Id, new ProductionRecipeId("cultivate_berries"),
                    new WorldPosition(world.x, world.y, world.z), Production.Settings.CropGrowthDuration);
                Production.AddSite(site);
            }
            var visual = buildingPresenter.GetComponent<CropPlotPresentation>() ??
                buildingPresenter.gameObject.AddComponent<CropPlotPresentation>();
            var width = definition.Footprint.Width * buildingGridSize;
            var depth = definition.Footprint.Depth * buildingGridSize;
            if (building.PlacementCells.Count > 0)
            {
                width = (building.PlacementCells.Max(cell => cell.X) - building.PlacementCells.Min(cell => cell.X) + 1) * buildingGridSize;
                depth = (building.PlacementCells.Max(cell => cell.Z) - building.PlacementCells.Min(cell => cell.Z) + 1) * buildingGridSize;
                if (building.Orientation.Degrees() % 180 != 0) (width, depth) = (depth, width);
            }
            visual.Configure(site, width, depth, Production.Animals, building.DefinitionId.ToString()=="animal_pasture");
            var presenter = buildingPresenter.GetComponent<ProductionSourcePresenter>() ??
                buildingPresenter.gameObject.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, new Color(0.25f, 0.65f, 0.18f), preserveColor: true);
            if (!productionPresenters.Contains(presenter)) productionPresenters.Add(presenter);
        }

        private void RegisterBuildingWorkplace(Building building, BuildingPresenter buildingPresenter)
        {
            var definition = buildingCatalog.ResolveDefinition(building.DefinitionId);
            if (definition.Workplace == null || definition.Production == null) return;
            var world = BuildingSpawner.GridToWorld(building.Coordinate, definition, building.Orientation);
            if (definition.Id.ToString() == "fishing_camp")
            {
                world -= Quaternion.Euler(0,building.Orientation.Degrees(),0) * Vector3.forward *
                    (definition.Footprint.Depth * buildingGridSize * .5f + 1.5f);
                world.y = WorldHeight.SampleHeight(world.x,world.z);
            }
            Production.RegisterBuildingWorkplace(building.Id, definition,
                new WorldPosition(world.x, world.y, world.z));
        }

        private void SpawnRegionProductionSources()
        {
            if (InhabitableRegion != null)
            {
                foreach (var anchor in InhabitableRegion.ResourceAnchors.OrderBy(x => x.StableIndex))
                {
                    var projected = ProjectToGameplayGround(anchor.VisualRoot.position, 0.04f);
                    anchor.VisualRoot.position = projected;
                    SpawnProductionSource(anchor.RecipeId, anchor.Amount,
                        new WorldPosition(projected.x, projected.y, projected.z), anchor.ActiveColor, anchor.StableIndex,
                        anchor.VisualRoot.gameObject);
                }
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
            // All lakes, including the local pond, use the same canonical school planner.
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
                var marker = Game.Presentation.Environment.WorldPropCatalog.Create("pile_" + resources[i], transform);
                marker.name = "P10.4 Industrial Zone " + definitions[i];
                marker.transform.position = position;
                var hit=marker.AddComponent<BoxCollider>();hit.isTrigger=true;
                hit.center=Vector3.up*.3f;hit.size=new Vector3(1.2f,.8f,1.4f);
                var presenter = marker.AddComponent<WorldResourcePilePresenter>();
                presenter.Bind(pile, Inventories.Get(pile.InventoryOwner));
                demoPilePresenters.Add(presenter);
            }
        }

        private void RegisterStarterTrees()
        {
            if (PopulatedBiomeWorld == null) return;
            var trees = StarterTreeViews().ToArray();
            var authoredSources = new HashSet<GameObject>(PopulatedBiomeWorld.ResourceFormations
                .Where(x => x.ClickableRoot != null).Select(x => x.ClickableRoot.gameObject));
            for (var index = 0; index < trees.Length; index++)
            {
                var tree = trees[index];
                if (!WorldDefinition.TryGetTile(tree.transform.position.x, tree.transform.position.z, out _) ||
                    WorldHeight.IsWater(tree.transform.position.x, tree.transform.position.z)) continue;
                // BaseRegion.Trees also contains canonical resource roots. Keep their existing
                // player_source identity instead of leaving an invisible duplicate source behind.
                if (authoredSources.Contains(tree)) continue;
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed,
                    "p109_starter_tree", index);
                if (!Production.TryGetSite(id, out var site))
                {
                    var point = tree.transform.position;
                    var amount = 20 + index % 31;
                    site = ProductionSite.Restore(new ProductionSiteState(id, new ProductionRecipeId("chop_wood"),
                        ProductionSiteKind.NaturalSource, new WorldPosition(point.x, point.y, point.z),
                        amount, amount, CropPlotPhase.Unplanted, 0, 0));
                    Production.AddSite(site);
                }
                if (!ResourceEcology.States.Any(value => value.SiteId == id))
                    ResourceEcology.Register(new NaturalNodeRegistration(site, NaturalResourceBiome.Forest,
                        new[] { site.Position }));
                var presenter = tree.GetComponent<ProductionSourcePresenter>() ?? tree.AddComponent<ProductionSourcePresenter>();
                presenter.Bind(site, new Color(0.28f, 0.4f, 0.18f), true);
                if (!productionPresenters.Contains(presenter)) productionPresenters.Add(presenter);
                if (tree.GetComponentInChildren<Collider>(true) == null)
                {
                    var collider = tree.AddComponent<CapsuleCollider>();
                    var scale = Mathf.Max(0.01f, tree.transform.lossyScale.y);
                    collider.height = 5f / scale;
                    collider.radius = 1.1f / Mathf.Max(0.01f, tree.transform.lossyScale.x);
                    collider.center = Vector3.up * collider.height * 0.5f;
                }
            }
            RefreshAuthoredTreeVisibility();
        }

        private Vector3 StartingResidentPosition(int index, Vector3 legacyPosition)
        {
            var spawns = NewGame?.StartTerritory?.ResidentSpawns;
            if (spawns != null && index >= 0 && index < spawns.Count)
            {
                var position = spawns[index];
                return new Vector3(position.X, position.Y, position.Z);
            }
            return ProjectToGameplayGround(legacyPosition);
        }

        private WorldPosition StartingResidentWorldPosition(int index, WorldPosition legacyPosition)
        {
            var spawns = NewGame?.StartTerritory?.ResidentSpawns;
            return spawns != null && index >= 0 && index < spawns.Count ? spawns[index] : legacyPosition;
        }

        private void DisableLegacyLocalPresentation()
        {
            void Hide(GameObject value)
            {
                if (value != null && value.scene.IsValid()) value.SetActive(false);
            }

            if (RealisticGameplayRegion != null)
                foreach (var value in RealisticGameplayRegion.Trees.Concat(RealisticGameplayRegion.Rocks)
                             .Concat(RealisticGameplayRegion.Foliage)) Hide(value);
            if (PopulatedBiomeWorld != null)
            {
                foreach (var value in PopulatedBiomeWorld.CanopyTrees.Concat(PopulatedBiomeWorld.MajorRocks)
                             .Concat(PopulatedBiomeWorld.GroundCover)) Hide(value);
                foreach (var formation in PopulatedBiomeWorld.ResourceFormations)
                {
                    if (formation.ClickableRoot != null) Hide(formation.ClickableRoot.gameObject);
                    foreach (var value in formation.DecorativeSatellites) Hide(value);
                }
            }
            if (StoneAgeSettlement != null)
            {
                foreach (var worksite in StoneAgeSettlement.Worksites) Hide(worksite.VisualRoot);
                foreach (var supply in StoneAgeSettlement.SupplyPresentations)
                    if (supply != null) Hide(supply.gameObject);
            }
        }

        private IEnumerable<GameObject> StarterTreeViews() =>
            PopulatedBiomeWorld.CanopyTrees.Concat(PopulatedBiomeWorld.BaseRegion.Trees)
                .Concat(PopulatedBiomeWorld.ResourceFormations.Where(x => x.RecipeId == "chop_wood")
                    .SelectMany(x => x.DecorativeSatellites))
                .Concat(pondTrees)
                .Where(x => x != null).Distinct();

        private void RegisterStarterRocks()
        {
            if (PopulatedBiomeWorld == null) return;
            var rocks = PopulatedBiomeWorld.MajorRocks.Concat(PopulatedBiomeWorld.BaseRegion.Rocks)
                .Concat(PopulatedBiomeWorld.ResourceFormations.Where(x => x.RecipeId != "chop_wood")
                    .SelectMany(x => x.DecorativeSatellites)).Where(x => x != null).Distinct().ToArray();
            for (var index = 0; index < rocks.Length; index++)
            {
                var rock = rocks[index];
                if (!WorldDefinition.TryGetTile(rock.transform.position.x, rock.transform.position.z, out _) ||
                    WorldHeight.IsWater(rock.transform.position.x, rock.transform.position.z)) continue;
                if (rock.GetComponent<ProductionSourcePresenter>() != null) continue;
                var formation = PopulatedBiomeWorld.ResourceFormations.FirstOrDefault(x => x.DecorativeSatellites.Contains(rock));
                var recipe = formation?.RecipeId ?? "gather_stone";
                var id = DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "p109_starter_outcrop", index);
                if (!Production.TryGetSite(id, out var site))
                {
                    var bounds = new Bounds(rock.transform.position, Vector3.one);
                    foreach (var renderer in rock.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                    var point = ProjectToGameplayGround(new Vector3(bounds.center.x, 0f, bounds.min.z - 1.2f));
                    site = ProductionSite.Restore(new ProductionSiteState(id, new ProductionRecipeId(recipe),
                        ProductionSiteKind.NaturalSource, new WorldPosition(point.x, point.y, point.z),
                        32, 32, CropPlotPhase.Unplanted, 0, 0));
                    Production.AddSite(site);
                }
                var presenter = rock.AddComponent<ProductionSourcePresenter>();
                presenter.Bind(site, Color.white, true);
                productionPresenters.Add(presenter);
                ResourceEcology.Register(new NaturalNodeRegistration(site, BiomeForRecipe(recipe),
                    RespawnCandidates(site.Position), renewable: false));
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

        private static void DisableSupersededRouteStrips()
        {
            foreach (var renderer in FindObjectsByType<LineRenderer>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (renderer != null && renderer.name.StartsWith("P10.2 Physical Route ",
                        StringComparison.Ordinal)) renderer.gameObject.SetActive(false);
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
            if (!WorldDefinition.TryGetTile(position.X, position.Z, out _) || WorldHeight.IsWater(position.X, position.Z))
            {
                if (authoredView != null) authoredView.SetActive(false);
                return;
            }
            amount = 20 + Math.Abs(index * 17 + 11) % 31;
            var site = ProductionSite.Restore(new ProductionSiteState(
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_source", index),
                new ProductionRecipeId(recipeId), ProductionSiteKind.NaturalSource, position, amount, amount,
                CropPlotPhase.Unplanted, 0, 0));
            Production.AddSite(site);
            if (authoredView == null && (recipeId == "gather_stone" || recipeId == "gather_iron" || recipeId == "gather_gold" || recipeId == "gather_berries"))
            {
                authoredView = Game.Presentation.Environment.WorldPropCatalog.Create(recipeId == "gather_stone" ? "rock" :
                    recipeId == "gather_berries" ? "berries" : "ore", transform);
                authoredView.transform.position = new Vector3(position.X,position.Y,position.Z);
                var hit=authoredView.AddComponent<BoxCollider>(); hit.isTrigger=true;
                hit.center=Vector3.up*.65f;hit.size=new Vector3(1.8f,1.5f,1.8f);
            }
            var view = authoredView != null ? authoredView : GameObject.CreatePrimitive(fallbackPrimitive);
            if (authoredView == null)
            {
                view.transform.position = new Vector3(position.X, position.Y + 0.5f, position.Z);
                view.transform.localScale = fallbackPrimitive == PrimitiveType.Cylinder
                    ? new Vector3(0.8f, 1.6f, 0.8f) : Vector3.one;
            }
            var presenter = view.GetComponent<ProductionSourcePresenter>() ?? view.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, color, authoredView != null);
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
            TiledWorld.BuildNavigation();
            NavigationSurface = TiledWorld.PrimaryNavigationSurface;
            if (NavigationSurface == null)
                throw new InvalidOperationException("The starter tiled Terrain has no navigation surface.");
        }

        private Vector3 ProjectToGameplayGround(Vector3 position) => ProjectToGameplayGround(position, 0.04f);

        private Vector3 ProjectToGameplayGround(Vector3 position, float offset) => TiledWorld == null
            ? position : TiledWorld.ProjectToGround(position, offset);

        private WorldPosition CameraStreamingFocus()
        {
            // During scene unload the camera may be destroyed before strategic-view OnDisable.
            if (cameraController == null) return WorldDefinition.PlayerStart;
            var focus = cameraController.transform.position;
            return new WorldPosition(focus.x, focus.y, focus.z);
        }

        private IEnumerable<WorldPosition> StreamingPhysicalPositions()
        {
            if (AiRuntime == null) yield break;
            foreach (var movement in AiRuntime.Movements)
                if (movement != null && movement.HasBoundPresenter) yield return movement.CurrentPosition;
        }

        private IEnumerable<Tuple<WorldPosition, WorldPosition>> StreamingNavigationRoutes()
        {
            if (Work == null || Tiers == null) yield break;
            foreach (var job in Work.Jobs.Where(value => !value.IsTerminal && value.AssigneeId.HasValue))
            {
                var target = job.Target.Position;
                if (!WorldDefinition.TryGetTile(target.X, target.Z, out _)) continue;
                WorldPosition origin;
                try { origin = Tiers.GetRuntimeState(job.AssigneeId.Value).Position; }
                catch (InvalidOperationException) { continue; }
                catch (KeyNotFoundException) { continue; }
                yield return Tuple.Create(origin, target);
            }
        }

        private void PrepareNavigationForManualMove(IReadOnlyList<StableEntityId> characterIds,
            WorldPosition target)
        {
            if (TiledWorld == null || characterIds == null || characterIds.Count == 0) return;
            var requested = new List<Tuple<WorldPosition, WorldPosition>>(characterIds.Count);
            foreach (var id in characterIds)
            {
                try { requested.Add(Tuple.Create(Tiers.GetRuntimeState(id).Position, target)); }
                catch (InvalidOperationException) { }
                catch (KeyNotFoundException) { }
            }
            TiledWorld.RefreshNow(CameraStreamingFocus(), StreamingPhysicalPositions(),
                StreamingNavigationRoutes().Concat(requested));
        }

        private void SpawnDemoPile(string id, long units, GridCoordinate coordinate, Color color, int index)
        {
            var pile = WorldPiles.Create(new ResourceId(id), new ResourceQuantity(units), coordinate,
                DeterministicStableId.Create(ActiveNewGameConfiguration.WorldSeed, "player_pile", index));
            var authoredAnchor = InhabitableRegion?.GetPileAnchor(index);
            if (authoredAnchor != null) authoredAnchor.gameObject.SetActive(false);
            var view = Game.Presentation.Environment.WorldPropCatalog.Create("pile_" + id, transform);
            view.transform.position = ProjectToGameplayGround(buildingGridOrigin +
                new Vector3(coordinate.X * buildingGridSize, 0f, coordinate.Z * buildingGridSize), 0f);
            var hit = view.AddComponent<BoxCollider>(); hit.isTrigger = true;
            hit.center = Vector3.up * .3f; hit.size = new Vector3(1.2f,.8f,1.4f);
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
