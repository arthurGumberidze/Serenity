using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U08")]
    public sealed class BuildingPlacementPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneWiresBuildingFoundationWithoutRegressingCharacters()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Buildings, Is.Not.Null);
            Assert.That(root.BuildingPlacementController, Is.Not.Null);
            Assert.That(root.BuildingSpawner, Is.Not.Null);
            Assert.That(root.DemoPresenters.Count, Is.EqualTo(2));
            Assert.That(root.CameraController, Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewHasNoIdentityAndCancelCreatesNothing()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            controller.TryMovePreviewToWorldPoint(new Vector3(20.3f, 0f, 20.4f));
            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.HasPreview, Is.True);
            Assert.That(controller.IsPreviewValid, Is.True);
            Assert.That(Object.FindObjectsByType<BuildingPresenter>().Count(candidate => candidate.IsBound), Is.EqualTo(0));
            Assert.That(root.Buildings.Count, Is.EqualTo(0));
            controller.CancelPlacement();
            yield return null;
            Assert.That(controller.IsActive, Is.False);
            Assert.That(root.Buildings.Count, Is.EqualTo(0));
            Assert.That(root.InteractionMode.IsBuildingPlacement, Is.False);
        }

        [UnityTest]
        public IEnumerator RotateConfirmAndOverlapUseLogicalGrid()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            controller.TryMovePreviewToWorldPoint(new Vector3(15.2f, 0f, 15.2f));
            controller.RotatePreview();
            Assert.That(controller.Orientation, Is.EqualTo(BuildingOrientation.East));
            var building = controller.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(root.Buildings.Count, Is.EqualTo(1));
            Assert.That(controller.LastPlacedPresenter.BuildingId, Is.EqualTo(building.Id));

            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(new Vector3(15f, 0f, 15f)), Is.False);
            Assert.That(controller.ConfirmPlacement(), Is.Null);
            Assert.That(root.Buildings.Count, Is.EqualTo(1));
            controller.CancelPlacement();
            yield return null;
            Assert.That(root.InteractionMode.IsBuildingPlacement, Is.False);
        }
    }
}
