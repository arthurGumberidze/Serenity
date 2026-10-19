using System.Collections;
using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
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
