using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Infrastructure;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.Domain.Work;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using Game.Presentation.AI;
using Game.Presentation.World;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P109")]
    public sealed class P109VideoRegressionPlayModeTests
    {
        private LocalSceneCompositionRoot root;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
            root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
        }

        [UnityTest]
        public IEnumerator StrategicCameraOwnsPoseAndRestoresBrainAndViewport()
        {
            var camera = root.PointerRaycaster.WorldCamera;
            var brain = camera.GetComponent<CinemachineBrain>();
            var viewport = camera.rect;
            var orthographic = camera.orthographic;
            var view = root.GetComponent<P102StrategicWorldView>();
            view.Enter();
            yield return null;
            yield return null;
            Assert.That(brain.enabled, Is.False);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.rect, Is.EqualTo(P102StrategicWorldView.MapViewport));
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.down), Is.GreaterThan(0.999f));
            view.Exit();
            Assert.That(brain.enabled, Is.True);
            Assert.That(camera.rect, Is.EqualTo(viewport));
            Assert.That(camera.orthographic, Is.EqualTo(orthographic));
        }

        [UnityTest]
        public IEnumerator AllTreeLodsExcludeShaderBillboardsAndDuplicateMeshStages()
        {
            var trees = root.PopulatedBiomeWorld.CanopyTrees.Concat(root.PopulatedBiomeWorld.BaseRegion.Trees);
            foreach (var tree in trees)
            foreach (var group in tree.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                Assert.That(lods, Is.Not.Empty, tree.name);
                var renderers = lods.SelectMany(x => x.renderers).ToArray();
                Assert.That(renderers.Any(StableEnvironmentLodController.IsBillboard), Is.False, tree.name);
                Assert.That(renderers.Distinct().Count(), Is.EqualTo(renderers.Length), tree.name);
                Assert.That(lods.Last().screenRelativeTransitionHeight,
                    Is.EqualTo(StableEnvironmentLodController.MinimumLastLodHeight).Within(0.0001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredTreesStayGroundedOnTiledTerrain()
        {
            var trees = root.PopulatedBiomeWorld.CanopyTrees.Concat(root.PopulatedBiomeWorld.BaseRegion.Trees);
            foreach (var tree in trees)
                Assert.That(tree.transform.position.y,
                    Is.EqualTo(root.WorldHeight.SampleHeight(tree.transform.position.x, tree.transform.position.z))
                        .Within(0.1f), tree.name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredTreesHideWhenTheirTerrainTileIsNotDrawn()
        {
            var tree = root.PopulatedBiomeWorld.CanopyTrees.First();
            var origin = tree.transform.position;
            root.TiledWorld.RefreshNow(new WorldPosition(root.WorldDefinition.Origin.X + 10f, 0f,
                root.WorldDefinition.Origin.Z + 10f), System.Array.Empty<WorldPosition>(),
                System.Array.Empty<System.Tuple<WorldPosition, WorldPosition>>());
            Assert.That(tree.activeSelf, Is.False);
            root.TiledWorld.RefreshNow(new WorldPosition(origin.x, origin.y, origin.z),
                System.Array.Empty<WorldPosition>(),
                System.Array.Empty<System.Tuple<WorldPosition, WorldPosition>>());
            Assert.That(tree.activeSelf, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroupContextMoveUsesDistinctFormationDestinations()
        {
            var roster = root.Characters.GetAll()
                .Where(value => !root.StarterEnemyAi.Residents.Contains(value.Id))
                .Select(value => value.Id).ToArray();
            Assert.That(roster.Length, Is.EqualTo(10));
            var y = root.WorldHeight.SampleHeight(0f, 12f);
            var target = new ContextCommandTarget(ContextTargetKind.Ground, new WorldPosition(0f, y, 12f));
            var result = root.ContextCommands.Issue(roster, ContextActionKind.Move, target,
                false, _ => new ContextActionFacts(alive: true));
            Assert.That(result.Orders.Count, Is.EqualTo(roster.Length));
            Assert.That(result.Orders.Select(order => (order.Target.Position.X, order.Target.Position.Z)).Distinct().Count(),
                Is.EqualTo(roster.Length));
            yield return null;
        }

        [Test]
        public void TacticalCameraZoomNeverCrossesCharacterCullDistance()
        {
            Assert.That(Tier1AiRuntimeDriver.CharacterLodCullDistance,
                Is.GreaterThan(root.CameraController.Settings.MaxZoom * 2f));
        }

        [UnityTest]
        public IEnumerator StarterSettlementDoesNotSwapCharacterVisualsWhenCameraMoves()
        {
            foreach (var character in root.NewGame.PlayerCharacters)
                root.Tiers.Transition(character.Id, CharacterSimulationTier.Tier1);
            var movements = root.AiRuntime.Movements.Where(value => value.Presenter != null).ToArray();
            Assert.That(movements.Length, Is.EqualTo(10));
            Assert.That(movements.Length, Is.LessThanOrEqualTo(Tier1AiRuntimeDriver.FullyDetailedCrowdThreshold));
            var refresh = typeof(Tier1AiRuntimeDriver).GetMethod("RefreshCrowdDetailLevels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            var camera = root.PointerRaycaster.WorldCamera;
            var originalPosition = camera.transform.position;
            try
            {
                var center = movements.Aggregate(Vector3.zero,
                    (sum, value) => sum + value.Presenter.transform.position) / movements.Length;
                refresh.Invoke(root.AiRuntime, null);
                Assert.That(movements.All(value => value.CrowdDetailEnabled &&
                    !value.Presenter.UsesLightweightVisual), Is.True);

                camera.transform.position = center + Vector3.up * 160f;
                refresh.Invoke(root.AiRuntime, null);
                Assert.That(movements.All(value => value.CrowdDetailEnabled &&
                    !value.Presenter.UsesLightweightVisual), Is.True);
            }
            finally
            {
                camera.transform.position = originalPosition;
                refresh.Invoke(root.AiRuntime, null);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevealedWorldLocationCanBeEnteredAndCentersThePhysicalCamera()
        {
            var targetId = new WorldLocationId("north_forest");
            var target = root.GlobalMap.GetLocation(targetId);
            root.GlobalMapBridge.RequestEnterGlobal();
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            Assert.That(root.TiledWorld.StrategicGroundTextureReady, Is.True);

            root.GlobalMapBridge.RequestEnterLocation(targetId);
            yield return null;

            Assert.That(root.GlobalMap.Mode, Is.EqualTo(GameWorldMode.Local));
            Assert.That(root.GlobalMap.ActiveLocationId, Is.EqualTo(targetId));
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.CameraController.transform.position.x, Is.EqualTo(target.MapX).Within(0.1f));
            Assert.That(root.CameraController.transform.position.z, Is.EqualTo(target.MapY).Within(0.1f));
            Assert.That(root.WorldDefinition.TryGetTile(target.MapX, target.MapY, out var targetTile), Is.True);
            Assert.That(root.TiledWorld.VisualTiles, Does.Contain(targetTile));
        }

        [UnityTest]
        public IEnumerator CharacterTurningDoesNotRotateCameraTargetAndMissProducesFeedback()
        {
            var presenter = root.DemoPresenters[0];
            root.SelectionProbe.SelectCharacters(new[] { presenter });
            Assert.That(root.ThirdPersonControl.TryEnterSelected().IsEligible, Is.True);
            var target = root.ThirdPersonControl.ThirdPersonCamera.Follow;
            var rotation = target.rotation;
            presenter.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * presenter.transform.rotation;
            Assert.That(Quaternion.Angle(rotation, target.rotation), Is.LessThan(0.01f));
            Assert.That(target.IsChildOf(presenter.transform), Is.False);
            root.ThirdPersonControl.RequestAttack();
            Assert.That(root.ThirdPersonControl.LastAttackMessage, Is.Not.Empty);
            Assert.That(presenter.AnimationIntent, Is.EqualTo(CharacterAnimationIntent.Combat));
            yield return null;
            root.ThirdPersonControl.Exit();
        }
    }
}
