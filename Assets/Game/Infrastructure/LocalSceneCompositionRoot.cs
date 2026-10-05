using System;
using System.Collections.Generic;
using Game.Domain.Characters;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
using Game.Presentation.CameraControl;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Buildings;
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
            BuildingPresentations = new BuildingPresentationRegistry();
            BuildingSpawner = new BuildingPresentationSpawner(buildingCatalog, BuildingPresentations,
                buildingGridSize, buildingGridOrigin);
            selectionProbe.Configure(input, pointerRaycaster, InteractionMode);
            buildingPlacementController.Initialize(BuildingPlacementService, BuildingSpawner, InteractionMode);

            MaleDemoCharacter = CreateDemoCharacter("Aren", CharacterSex.Male);
            FemaleDemoCharacter = CreateDemoCharacter("Mira", CharacterSex.Female);
            Characters.Add(MaleDemoCharacter);
            Characters.Add(FemaleDemoCharacter);
            demoPresenters.Add(CharacterSpawner.Spawn(MaleDemoCharacter, new Vector3(-3f, 0f, 0f), Quaternion.identity));
            demoPresenters.Add(CharacterSpawner.Spawn(FemaleDemoCharacter, new Vector3(3f, 0f, 0f), Quaternion.identity));
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
