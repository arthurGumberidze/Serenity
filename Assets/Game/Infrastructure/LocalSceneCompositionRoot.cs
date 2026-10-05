using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Characters;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
using Game.Presentation.CameraControl;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Resources;
using Game.Presentation.Resources;
using UnityEngine;

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
            gameObject.AddComponent<ResourceDebugOverlay>().Initialize(Inventories, Resources);
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

            MaleDemoCharacter = CreateDemoCharacter("Aren", CharacterSex.Male);
            FemaleDemoCharacter = CreateDemoCharacter("Mira", CharacterSex.Female);
            Characters.Add(MaleDemoCharacter);
            Characters.Add(FemaleDemoCharacter);
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, MaleDemoCharacter.Id), Resources, 8));
            Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, FemaleDemoCharacter.Id), Resources, 8));
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter, new Vector3(-3f, 0f, 0f), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter, new Vector3(3f, 0f, 0f), Quaternion.identity));
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
