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
using Game.ECS.Tier2;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Infrastructure
{
    public sealed class LocalSceneCompositionRoot : MonoBehaviour
    {
        [SerializeField] private LocalGameplayInputSource input;
        [SerializeField] private RtsCameraController cameraController;
        [SerializeField] private WorldPointerRaycaster pointerRaycaster;
        [SerializeField] private SelectionProbe selectionProbe;
        [SerializeField] private CharacterPresentationCatalog characterCatalog;
        [SerializeField] private BuildingPresentationCatalog buildingCatalog;
        [SerializeField] private BuildingPlacementController buildingPlacementController;
        [SerializeField, Min(0.1f)] private float buildingGridSize = 1f;
        [SerializeField] private Vector3 buildingGridOrigin;
        [SerializeField] private long developmentWorldSimulationSeed = 20261006L;

        private readonly List<CharacterPresenter> demoPresenters = new List<CharacterPresenter>();

        public LocalGameplayInputSource Input => input;
        public RtsCameraController CameraController => cameraController;
        public WorldPointerRaycaster PointerRaycaster => pointerRaycaster;
        public SelectionProbe SelectionProbe => selectionProbe;
        public CharacterRegistry Characters { get; private set; }
        public CharacterPresentationRegistry CharacterPresentations { get; private set; }
        public CharacterPresentationSpawner CharacterSpawner { get; private set; }
        public Character MaleDemoCharacter { get; private set; }
        public Character FemaleDemoCharacter { get; private set; }
        public Character FirstDemoHeir { get; private set; }
        public Character SecondDemoHeir { get; private set; }
        public IReadOnlyList<CharacterPresenter> DemoPresenters => demoPresenters;
        public BuildingRegistry Buildings { get; private set; }
        public BuildingOccupancyGrid BuildingOccupancy { get; private set; }
        public BuildingPlacementService BuildingPlacementService { get; private set; }
        public BuildingPresentationRegistry BuildingPresentations { get; private set; }
        public BuildingPresentationSpawner BuildingSpawner { get; private set; }
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
        public NavMeshSurface NavigationSurface { get; private set; }
        public GameClock Clock { get; private set; }
        public Tier1AiAgentRegistry AiAgents { get; private set; }
        public HaulClaimRegistry HaulClaims { get; private set; }
        public Tier1AiScheduler AiScheduler { get; private set; }
        public Tier1AiRuntimeDriver AiRuntime { get; private set; }
        public WorkManager Work { get; private set; }
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
        public BattleResolutionService Battles { get; private set; }
        public BuildingDamageService BuildingDamage { get; private set; }
        public BuildingDamageDebugOverlay BuildingDamageDebug { get; private set; }
        public MilitaryUnitDebugOverlay MilitaryDebug { get; private set; }
        public PlayerControlService PlayerControl { get; private set; }
        public ThirdPersonControlController ThirdPersonControl { get; private set; }
        public Building DemoCropPlotBuilding { get; private set; }
        public BuildingPresenter DemoCropPlotPresenter { get; private set; }
        public IReadOnlyList<WorldResourcePilePresenter> DemoPilePresenters => demoPilePresenters;
        private readonly List<WorldResourcePilePresenter> demoPilePresenters = new List<WorldResourcePilePresenter>();
        private readonly List<ProductionSourcePresenter> productionPresenters = new List<ProductionSourcePresenter>();

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

            Characters = new CharacterRegistry();
            HealthFamily = new HealthAndFamilyService(Characters, HealthConditionCatalog.CreateStoneAge(),
                developmentWorldSimulationSeed);
            Weather = new WeatherService(developmentWorldSimulationSeed, ClimateProfile.Temperate, HealthFamily);
            CharacterPresentations = new CharacterPresentationRegistry();
            CharacterSpawner = new CharacterPresentationSpawner(characterCatalog, CharacterPresentations);

            InteractionMode = new LocalInteractionMode();
            Buildings = new BuildingRegistry();
            BuildingOccupancy = new BuildingOccupancyGrid();
            BuildingPlacementService = new BuildingPlacementService(Buildings, BuildingOccupancy,
                new BuildingGridBounds(-45, -45, 45, 45));
            if (buildingCatalog.ResourceCatalog == null) throw new InvalidOperationException("Building catalog has no resource catalog.");
            Resources = buildingCatalog.ResourceCatalog.CreateCatalog();
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
                buildingGridSize, buildingGridOrigin, null, Inventories);
            selectionProbe.Configure(input, pointerRaycaster, InteractionMode);
            buildingPlacementController.Initialize(BuildingPlacementService, BuildingSpawner, InteractionMode,
                Construction, FundingSources);
            buildingPlacementController.BuildingPlaced += OnBuildingPlaced;

            // DEV_BOOTSTRAP_ONLY: finite canonical piles for the U09 LocalGameplay slice.
            SpawnDemoPile("wood_log", 30, new GridCoordinate(-8, -2), new Color(0.45f, 0.25f, 0.1f));
            SpawnDemoPile("plant_fiber", 20, new GridCoordinate(-6, -2), new Color(0.45f, 0.7f, 0.2f));
            SpawnDemoPile("stone", 10, new GridCoordinate(-4, -2), new Color(0.45f, 0.45f, 0.45f));
            SpawnDemoPile("hide", 5, new GridCoordinate(-2, -2), new Color(0.7f, 0.45f, 0.25f));

            SpawnDemoStorage();
            BuildNavigationSurface();

            var dynastyId = DemoId(100);
            MaleDemoCharacter = CreateDemoCharacter("Aren", CharacterSex.Male, DemoId(1), dynastyId, null);
            FemaleDemoCharacter = CreateDemoCharacter("Mira", CharacterSex.Female, DemoId(2), null, null);
            FirstDemoHeir = CreateDemoCharacter("Cale", CharacterSex.Male, DemoId(3), dynastyId,
                new[] { Parent(MaleDemoCharacter.Id, ParentRole.Father), Parent(FemaleDemoCharacter.Id, ParentRole.Mother) });
            SecondDemoHeir = CreateDemoCharacter("Doran", CharacterSex.Male, DemoId(4), dynastyId,
                new[] { Parent(MaleDemoCharacter.Id, ParentRole.Father), Parent(FemaleDemoCharacter.Id, ParentRole.Mother) });
            Characters.Add(MaleDemoCharacter);
            Characters.Add(FemaleDemoCharacter);
            Characters.Add(FirstDemoHeir);
            Characters.Add(SecondDemoHeir);
            Characters.LinkSpouses(MaleDemoCharacter.Id, FemaleDemoCharacter.Id);
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, MaleDemoCharacter.Id), Resources, 8));
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, FemaleDemoCharacter.Id), Resources, 8));
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter, new Vector3(-3f, 0f, 0f), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter, new Vector3(3f, 0f, 0f), Quaternion.identity));

            Production = new ProductionService(ProductionRecipes, Resources, Inventories, Buildings, ResourceTransfers,
                researchUnlocks: Research, capabilityProvider: HealthFamily, weatherModifierProvider: Weather);
            Production.Population = Characters.Count;
            SpawnProductionSource("gather_berries", 60, new WorldPosition(-12f, 0f, 5f),
                new Color(0.65f, 0.15f, 0.35f), PrimitiveType.Sphere);
            SpawnProductionSource("gather_fiber", 60, new WorldPosition(-8f, 0f, 7f),
                new Color(0.3f, 0.7f, 0.2f), PrimitiveType.Capsule);
            SpawnProductionSource("chop_wood", 80, new WorldPosition(-4f, 0f, 9f),
                new Color(0.35f, 0.2f, 0.08f), PrimitiveType.Cylinder);
            SpawnProductionSource("gather_stone", 80, new WorldPosition(1f, 0f, 9f),
                new Color(0.45f, 0.45f, 0.48f), PrimitiveType.Sphere);
            gameObject.AddComponent<ProductionDebugOverlay>().Initialize(Production, Resources, pointerRaycaster);

            Clock = new GameClock(new GameTimeState(0, Character.BiologicalTicksPerYear * 20L, 1, false));
            Dynasty = DynastyState.Create(dynastyId, "House Serenity", MaleDemoCharacter.Id,
                MaleDemoCharacter.Id, new[] { MaleDemoCharacter.Id, FirstDemoHeir.Id, SecondDemoHeir.Id });
            PlayerRealm = new PlayerRealmState(MaleDemoCharacter.Id);
            GameRun = new GameRunState();
            Succession = new SuccessionService(Characters, Dynasty, PlayerRealm, GameRun);
            HealthFamily.BindSuccession(Succession);
            AiAgents = new Tier1AiAgentRegistry();
            HaulClaims = new HaulClaimRegistry();
            Work = new WorkManager(Characters);
            var haulJobs = new HaulWorldQuery(Inventories, WorldPiles, Buildings, HaulClaims, buildingGridSize,
                new WorldPosition(buildingGridOrigin.x, buildingGridOrigin.y, buildingGridOrigin.z));
            var aiWorld = new Tier1AiWorld(haulJobs, HaulClaims, ResourceTransfers, Production, Research);
            AiScheduler = new Tier1AiScheduler(Characters, AiAgents, aiWorld, workManager: Work,
                capabilityProvider: HealthFamily);
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
            WeatherPresentation.Initialize(Weather);
            WeatherPresentation.Refresh(Clock.State.CalendarTicks);
            DynastyDebug = gameObject.AddComponent<DynastyDebugOverlay>();
            DynastyDebug.Initialize(Succession, Characters, selectionProbe, Clock, pointerRaycaster);
            HealthFamilyDebug = gameObject.AddComponent<HealthFamilyDebugOverlay>();
            HealthFamilyDebug.Initialize(HealthFamily, Characters, Succession.Family, selectionProbe, Clock,
                pointerRaycaster);
            Tier2Runtime = new Tier2Runtime("Serenity Local Tier 2");
            Tier3Characters = new Tier3CharacterRegistry();
            OffCameraSimulation = new OffCameraSimulationService(
                OffCameraSimulationSettings.ForWorld(developmentWorldSimulationSeed));
            var tier1 = new Tier1CharacterAdapter(characterCatalog, CharacterPresentations, CharacterSpawner,
                AiRuntime, AiAgents);
            var tier2 = new Tier2CharacterAdapter(Tier2Runtime);
            Tier3Adapter = new Tier3CharacterAdapter(Tier3Characters, OffCameraSimulation,
                () => new SimulationTimePoint(Clock.State.CalendarTicks, Clock.State.BiologicalTicks));
            Tiers = new TierManager(Characters, tier1, tier2, Tier3Adapter);
            Tiers.RegisterExisting(MaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            Tiers.RegisterExisting(FemaleDemoCharacter.Id, CharacterSimulationTier.Tier1);
            TierPolicy = new TierDistancePolicy(Tiers);
            Combat = new CombatService(Characters, HealthFamily, new Tier1CombatPositionProvider(AiRuntime),
                developmentWorldSimulationSeed, Tiers);
            Combat.RegisterSide(MaleDemoCharacter.Id, new CombatSideId("player"));
            Combat.RegisterSide(FemaleDemoCharacter.Id, new CombatSideId("player"));
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
            BuildingDamage = new BuildingDamageService(Buildings,
                buildingCatalog.Entries.Select(entry => buildingCatalog.ResolveDefinition(new BuildingDefinitionId(entry.DefinitionId))),
                Production, Inventories, WorldPiles, Weather, Combat, BuildingOccupancy);
            BuildingDamage.BuildingChanged += OnBuildingChanged;
            Tiers.BindTransitionGuard(new CharacterTierTransitionGuardSet(Combat, PlayerControl));
            AiScheduler.BindExternalActivityOwner(new CharacterActivityOwnerSet(Combat, PlayerControl));
            Combat.BindMovementSuppression(PlayerControl);
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
                pointerRaycaster);
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
            var targetCalendarTick = Clock.State.CalendarTicks;
            var startCalendarTick = checked(targetCalendarTick - advance.CalendarDelta.Ticks);
            Tier2Runtime.Step(advance, Weather.GetMovementModifierForInterval(startCalendarTick, targetCalendarTick));
            Tier3Adapter.AdvanceAllToCurrent();
            Tier2Runtime.AdvanceResearch(Research, targetCalendarTick);
            Tier3Adapter.AdvanceResearchToCurrent(Research);
            var abstractWorkers = Tier2Runtime.ConsumeProductionWorkers().Concat(Tiers.ManagedIds.Where(id =>
                Tiers.GetTier(id) == CharacterSimulationTier.Tier3)).Distinct().OrderBy(id => id).ToArray();
            var outdoorWorkers = Production.ActiveTasks.Select(task => task.WorkerId).Concat(abstractWorkers);
            Weather.AdvanceHealthExposureTo(targetCalendarTick, outdoorWorkers);
            Battles.AdvanceTo(targetCalendarTick, Clock.State.BiologicalTicks);
            HealthFamily.AdvanceTo(targetCalendarTick, Clock.State.BiologicalTicks);
            BuildingDamage.AdvanceFireTo(targetCalendarTick);
            Production.AdvanceTo(targetCalendarTick, abstractWorkers);
            for (var i = 0; i < productionPresenters.Count; i++) productionPresenters[i].Refresh();
            WeatherPresentation.Refresh(targetCalendarTick);
            var focus = cameraController.transform.position;
            TierPolicy.Evaluate(new WorldPosition(focus.x, focus.y, focus.z));
        }

        private void OnChildBorn(Character child)
        {
            var state = new CharacterRuntimeState(child.Id, default, default, false, "local",
                lastSimulationCalendarTick: Clock.State.CalendarTicks,
                lastSimulationBiologicalTick: Clock.State.BiologicalTicks);
            Tiers.Materialize(child, CharacterSimulationTier.Tier3, state);
            Production.Population = Characters.Count;
        }

        public CombatEncounterState StartDemoCombat()
        {
            var active = Combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
            if (active != null) return active;
            PrepareDemoCombatants(out var player, out var enemy);
            var encounterId = DemoId(200 + Combat.Encounters.Count);
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
                ? existingPlayer : MilitaryUnits.Create(new MilitaryUnitId(DemoId(300)),
                    new MilitarySideId("player"), player, player[0]);
            var enemyUnit = MilitaryUnits.TryGetForMember(enemy[0], out var existingEnemy)
                ? existingEnemy : MilitaryUnits.Create(new MilitaryUnitId(DemoId(301)),
                    new MilitarySideId("enemy"), enemy, enemy[0]);
            var battleId = DemoId(400 + Combat.Encounters.Count);
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
                var id = DemoId(20 + i);
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
            id == MaleDemoCharacter.Id || id == DemoId(20)
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
            DemoStorageBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(5, 5), BuildingOrientation.North);
            Storage.Attach(DemoStorageBuilding, definition);
            DemoStorageBuilding.MarkCompleted();
            DemoStoragePresenter = BuildingSpawner.Spawn(DemoStorageBuilding);
        }

        private void SpawnDemoCropPlot()
        {
            var definition = buildingCatalog.ResolveDefinition(new BuildingDefinitionId("primitive_crop_plot"));
            DemoCropPlotBuilding = BuildingPlacementService.Confirm(definition, new GridCoordinate(8, 5),
                BuildingOrientation.North);
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

        private void SpawnProductionSource(string recipeId, long amount, WorldPosition position, Color color,
            PrimitiveType primitive)
        {
            var site = ProductionSite.CreateNatural(new ProductionRecipeId(recipeId), position, amount);
            Production.AddSite(site);
            var view = GameObject.CreatePrimitive(primitive);
            view.transform.position = new Vector3(position.X, position.Y + 0.5f, position.Z);
            view.transform.localScale = primitive == PrimitiveType.Cylinder
                ? new Vector3(0.8f, 1.6f, 0.8f) : Vector3.one;
            var presenter = view.AddComponent<ProductionSourcePresenter>();
            presenter.Bind(site, color);
            productionPresenters.Add(presenter);
        }

        private void BuildNavigationSurface()
        {
            NavigationSurface = gameObject.AddComponent<NavMeshSurface>();
            NavigationSurface.collectObjects = CollectObjects.Volume;
            NavigationSurface.center = new Vector3(0f, 1f, 0f);
            NavigationSurface.size = new Vector3(100f, 8f, 100f);
            NavigationSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            NavigationSurface.BuildNavMesh();
        }

        private void SpawnDemoPile(string id, long units, GridCoordinate coordinate, Color color)
        {
            var pile = WorldPiles.Create(new ResourceId(id), new ResourceQuantity(units), coordinate);
            var view = GameObject.CreatePrimitive(PrimitiveType.Cube);
            view.transform.position = buildingGridOrigin + new Vector3(coordinate.X * buildingGridSize, 0.35f,
                coordinate.Z * buildingGridSize);
            view.transform.localScale = new Vector3(0.75f, 0.7f, 0.75f);
            var renderer = view.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            var presenter = view.AddComponent<WorldResourcePilePresenter>();
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

        private static StableEntityId DemoId(int value) =>
            StableEntityId.Parse(value.ToString("x32"));
    }
}
