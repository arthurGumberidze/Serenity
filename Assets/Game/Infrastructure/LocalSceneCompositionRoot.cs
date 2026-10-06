using System;
using System.Collections.Generic;
using System.Linq;
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
        public Tier2Runtime Tier2Runtime { get; private set; }
        public Tier3CharacterRegistry Tier3Characters { get; private set; }
        public TierManager Tiers { get; private set; }
        public TierDistancePolicy TierPolicy { get; private set; }
        public OffCameraSimulationService OffCameraSimulation { get; private set; }
        public Tier3CharacterAdapter Tier3Adapter { get; private set; }
        public IReadOnlyList<WorldResourcePilePresenter> DemoPilePresenters => demoPilePresenters;
        private readonly List<WorldResourcePilePresenter> demoPilePresenters = new List<WorldResourcePilePresenter>();

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
            Construction = new ConstructionFundingService(BuildingPlacementService, Inventories, Resources, Storage);
            gameObject.AddComponent<ResourceDebugOverlay>().Initialize(Inventories, Resources, pointerRaycaster);
            BuildingPresentations = new BuildingPresentationRegistry();
            BuildingSpawner = new BuildingPresentationSpawner(buildingCatalog, BuildingPresentations,
                buildingGridSize, buildingGridOrigin, null, Inventories);
            selectionProbe.Configure(input, pointerRaycaster, InteractionMode);
            buildingPlacementController.Initialize(BuildingPlacementService, BuildingSpawner, InteractionMode,
                Construction, FundingSources);

            // DEV_BOOTSTRAP_ONLY: finite canonical piles for the U09 LocalGameplay slice.
            SpawnDemoPile("wood_log", 30, new GridCoordinate(-8, -2), new Color(0.45f, 0.25f, 0.1f));
            SpawnDemoPile("plant_fiber", 20, new GridCoordinate(-6, -2), new Color(0.45f, 0.7f, 0.2f));
            SpawnDemoPile("stone", 10, new GridCoordinate(-4, -2), new Color(0.45f, 0.45f, 0.45f));
            SpawnDemoPile("hide", 5, new GridCoordinate(-2, -2), new Color(0.7f, 0.45f, 0.25f));

            SpawnDemoStorage();
            BuildNavigationSurface();

            MaleDemoCharacter = CreateDemoCharacter("Aren", CharacterSex.Male);
            FemaleDemoCharacter = CreateDemoCharacter("Mira", CharacterSex.Female);
            Characters.Add(MaleDemoCharacter);
            Characters.Add(FemaleDemoCharacter);
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, MaleDemoCharacter.Id), Resources, 8));
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, FemaleDemoCharacter.Id), Resources, 8));
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter, new Vector3(-3f, 0f, 0f), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter, new Vector3(3f, 0f, 0f), Quaternion.identity));

            Clock = new GameClock(new GameTimeState(0, 0, 1, false));
            AiAgents = new Tier1AiAgentRegistry();
            HaulClaims = new HaulClaimRegistry();
            Work = new WorkManager(Characters);
            var haulJobs = new HaulWorldQuery(Inventories, WorldPiles, Buildings, HaulClaims, buildingGridSize,
                new WorldPosition(buildingGridOrigin.x, buildingGridOrigin.y, buildingGridOrigin.z));
            var aiWorld = new Tier1AiWorld(haulJobs, HaulClaims, ResourceTransfers);
            AiScheduler = new Tier1AiScheduler(Characters, AiAgents, aiWorld, workManager: Work);
            AiRuntime = gameObject.AddComponent<Tier1AiRuntimeDriver>();
            AiRuntime.Initialize(Clock, AiScheduler);
            AiRuntime.Register(MaleDemoCharacter, demoPresenters[0]);
            AiRuntime.Register(FemaleDemoCharacter, demoPresenters[1], new Tier1Needs(0d, 0.25d));
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
            AiRuntime.SimulationAdvanced += OnSimulationAdvanced;
            gameObject.AddComponent<Tier1AiDebugOverlay>().Initialize(selectionProbe, AiAgents, Inventories,
                Resources, HaulClaims, pointerRaycaster, Tiers, Clock, OffCameraSimulation);
            WorkDebug = gameObject.AddComponent<WorkDebugOverlay>();
            WorkDebug.Initialize(Work, selectionProbe, CharacterPresentations, WorldPiles, Inventories, Buildings,
                input, pointerRaycaster);
        }

        private void OnDestroy()
        {
            if (AiRuntime != null) AiRuntime.SimulationAdvanced -= OnSimulationAdvanced;
            Tier2Runtime?.Dispose();
        }

        private void OnSimulationAdvanced(GameTimeAdvance advance)
        {
            Tier2Runtime.Step(advance);
            Tier3Adapter.AdvanceAllToCurrent();
            var focus = cameraController.transform.position;
            TierPolicy.Evaluate(new WorldPosition(focus.x, focus.y, focus.z));
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

        private static Character CreateDemoCharacter(string name, CharacterSex sex)
        {
            return Character.CreateNew(new CharacterCreationData(new CharacterName(name), sex, 0,
                null, null, null, null, null,
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
        }
    }
}
