using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Infrastructure;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [TestFixture, Category("P105")]
    public sealed class P105CharacterProductionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RtsAndTpsLocomotionPlaybackTracksTranslationSpeedAndActionPoseChanges()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var presenter = root.DemoPresenters[0];
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   Object.FindObjectsByType<UmaAppearanceProjection>(FindObjectsInactive.Include)
                       .Where(x => x.Appearance.CharacterId == presenter.CharacterId).Any(x => !x.UsesUma))
                yield return null;

            presenter.SetMovementSpeed(3.4f);
            Assert.That(presenter.Animator.speed, Is.EqualTo(1f).Within(0.08f));
            presenter.SetMovementSpeed(5.61f);
            Assert.That(presenter.Animator.speed, Is.EqualTo(1.65f).Within(0.1f));
            presenter.SetSimulationPaused(true);
            Assert.That(presenter.Animator.speed, Is.Zero);
            presenter.SetSimulationPaused(false);
            Assert.That(presenter.Animator.speed, Is.EqualTo(1.65f).Within(0.1f));

            presenter.SetMovementSpeed(0f);
            presenter.SetAnimationIntent(CharacterAnimationIntent.Chop);
            presenter.RefreshActionPose(0f);
            var first = presenter.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm)?.localRotation;
            presenter.RefreshActionPose(0.3f);
            var second = presenter.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm)?.localRotation;
            Assert.That(first.HasValue && second.HasValue, Is.True);
            Assert.That(Quaternion.Angle(first.Value, second.Value), Is.GreaterThan(1f));

            presenter.SetAnimationIntent(CharacterAnimationIntent.Combat);
            presenter.RefreshActionPose(0.7f);
            Assert.That(presenter.AnimationIntent, Is.EqualTo(CharacterAnimationIntent.Combat));
        }

        [UnityTest]
        public IEnumerator CameraDistanceSwitchesStableCharacterLodAndTreesNeverRotateTowardCamera()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var controller = Object.FindAnyObjectByType<StableEnvironmentLodController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.TreeCount, Is.GreaterThanOrEqualTo(170));
            Assert.That(controller.LodGroupCount, Is.GreaterThanOrEqualTo(170));
            Assert.That(Object.FindObjectsByType<BillboardRenderer>(FindObjectsInactive.Include)
                .Any(x => x.enabled), Is.False);

            var trees = root.PopulatedBiomeWorld.CanopyTrees.Take(12).ToArray();
            var rotations = trees.Select(x => x.transform.rotation).ToArray();
            var camera = Camera.main;
            var oldPosition = camera.transform.position;
            var oldRotation = camera.transform.rotation;
            camera.transform.position += Vector3.up * 180f + Vector3.forward * 180f;
            camera.transform.rotation = Quaternion.Euler(68f, 137f, 0f);
            RefreshCrowdLodNow(root);
            Assert.That(root.DemoPresenters.Any(x => x.UsesLightweightVisual), Is.True);
            yield return new WaitForSecondsRealtime(0.4f);
            for (var index = 0; index < trees.Length; index++)
                Assert.That(Quaternion.Angle(rotations[index], trees[index].transform.rotation), Is.LessThan(0.01f));

            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            RefreshCrowdLodNow(root);
            Assert.That(root.DemoPresenters.Any(x => x.DetailedVisualActive), Is.True);
        }

        private static void RefreshCrowdLodNow(LocalSceneCompositionRoot root)
        {
            typeof(Game.Presentation.AI.Tier1AiRuntimeDriver)
                .GetMethod("RefreshCrowdDetailLevels", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(root.AiRuntime, null);
        }
    }
}
