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
            CompleteTechnology(root, "shelter_construction");
            var controller = root.BuildingPlacementController;
            var beforeBuildings = root.Buildings.Count;
            var beforePresenters = Object.FindObjectsByType<BuildingPresenter>().Count(candidate => candidate.IsBound);
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(TryFindClearPlacement(root, controller, out _), Is.True,
                "No clear construction preview was found near the camp.");
            Assert.That(controller.IsActive, Is.True);
            Assert.That(controller.HasPreview, Is.True);
            Assert.That(controller.IsPreviewValid, Is.True,
                $"Placement={controller.LastPlacementFailure}; construction={controller.LastConstructionFailure}");
            Assert.That(Object.FindObjectsByType<BuildingPresenter>().Count(candidate => candidate.IsBound), Is.EqualTo(beforePresenters));
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings));
            controller.CancelPlacement();
            yield return null;
            Assert.That(controller.IsActive, Is.False);
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings));
            Assert.That(root.InteractionMode.IsBuildingPlacement, Is.False);
        }

        [UnityTest]
        public IEnumerator RotateConfirmAndOverlapUseLogicalGrid()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "shelter_construction");
            var controller = root.BuildingPlacementController;
            var beforeBuildings = root.Buildings.Count;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            controller.RotatePreview();
            Assert.That(controller.Orientation, Is.EqualTo(BuildingOrientation.East));
            Assert.That(TryFindClearPlacement(root, controller, out _), Is.True,
                "No clear rotated construction preview was found near the camp.");
            var building = controller.ConfirmPlacement();
            Assert.That(building, Is.Not.Null,
                $"Placement={controller.LastPlacementFailure}; construction={controller.LastConstructionFailure}");
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Planned));
            Assert.That(root.Work.Jobs.Any(x => x.Type == Game.Domain.Work.WorkJobType.Build &&
                x.Target.EntityId == building.Id), Is.True);
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings + 1));
            Assert.That(controller.LastPlacedPresenter.BuildingId, Is.EqualTo(building.Id));

            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(controller.LastPlacedPresenter.transform.position), Is.False);
            Assert.That(controller.ConfirmPlacement(), Is.Null);
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings + 1));
            controller.CancelPlacement();
            yield return null;
            Assert.That(root.InteractionMode.IsBuildingPlacement, Is.False);
        }

        [UnityTest]
        public IEnumerator ResidentApproachesAndBuildsInsteadOfInstantCompletion()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "shelter_construction");
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            var found = false;
            // A positive construction test must use an actually clear footprint. The
            // original hard-coded point is no longer a valid placement on this map.
            for (var z = 12; z <= 48 && !found; z += 3)
            for (var x = 12; x <= 48 && !found; x += 3)
            {
                var point = new Vector3(x, root.WorldHeight.SampleHeight(x, z), z);
                if (!Game.Presentation.AI.ReachableNavMeshPoint.TryFind(root.DemoPresenters[0].transform.position,
                        point, out var reachable) || Vector3.Distance(point, reachable) > 1f) continue;
                found = controller.TryMovePreviewToWorldPoint(point);
            }
            Assert.That(found, Is.True, "No clear, reachable construction footprint found near the camp.");
            var building = controller.ConfirmPlacement();
            Assert.That(building.IsOperational, Is.False);
            Assert.That(controller.LastPlacedPresenter.transform.Find("Construction frame").gameObject.activeSelf, Is.True);
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
            var sawBuild = false;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!building.IsOperational && Time.realtimeSinceStartup < deadline)
            {
                sawBuild |= root.AiRuntime.Movements.Any(x => x.Presenter.AnimationIntent ==
                    Game.Presentation.Characters.CharacterAnimationIntent.Build);
                yield return null;
            }
            Assert.That(sawBuild, Is.True, string.Join(",", root.Work.Jobs.Select(x => x.Type + ":" + x.Status)));
            Assert.That(building.IsOperational, Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(controller.LastPlacedPresenter.transform.Find("Construction frame").gameObject.activeSelf, Is.False);
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new Game.Domain.Research.TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(technologyId), Is.True);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }

        private static bool TryFindClearPlacement(LocalSceneCompositionRoot root,
            Game.Presentation.Buildings.BuildingPlacementController controller, out Vector3 point)
        {
            for (var z = 12; z <= 48; z += 3)
            for (var x = 12; x <= 48; x += 3)
            {
                point = new Vector3(x, root.WorldHeight.SampleHeight(x, z), z);
                if (controller.TryMovePreviewToWorldPoint(point)) return true;
            }
            point = default;
            return false;
        }
    }
}
