using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Infrastructure;
using Game.Presentation.Characters;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Game.Simulation.Campaign;

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
            Assert.That(presenter.Animator.GetInteger("ActionIntent"), Is.EqualTo((int)CharacterAnimationIntent.Chop));
            Assert.That(presenter.Animator.runtimeAnimatorController.animationClips
                .Any(x => x.name.Contains("TreeChopping_Loop")), Is.True);

            presenter.SetAnimationIntent(CharacterAnimationIntent.Combat);
            Assert.That(presenter.Animator.GetInteger("ActionIntent"), Is.EqualTo((int)CharacterAnimationIntent.Combat));
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

        [UnityTest]
        public IEnumerator RoleWardrobeAndComposedHeadsSurviveSaveLoadAndTierRoundTrip()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var appearances = root.Appearances.CaptureState().Characters;
            var worker = appearances.First(x => x.ClothingId == StoneAgeAppearanceProfiles.WorkerClothing);
            var military = appearances.First(x => x.ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier1);
            root.Tiers.Transition(military.CharacterId, CharacterSimulationTier.Tier1);

            root.PlayerSaves.Save(3);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier1);
            root.PlayerSaves.Load(3);
            yield return null;

            Assert.That(root.Appearances.Get(worker.CharacterId).ClothingId,
                Is.EqualTo(StoneAgeAppearanceProfiles.WorkerClothing));
            Assert.That(root.Appearances.Get(military.CharacterId).ClothingId,
                Is.EqualTo(StoneAgeAppearanceProfiles.MilitaryClothing));
            Assert.That(root.CharacterPresentations.TryGet(worker.CharacterId, out var restoredWorker), Is.True);
            Assert.That(root.CharacterPresentations.TryGet(military.CharacterId, out var restoredMilitary), Is.True);
            Assert.That(HasComposedHead(restoredWorker), Is.True);
            Assert.That(HasComposedHead(restoredMilitary), Is.True);
        }

        [UnityTest]
        public IEnumerator NormalNewGameRoutePreservesRolePresentersAndExercisesBothPlayerLods()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var profile = StoneAgeAppearanceProfiles.CreateCatalog().Get(StoneAgeAppearanceProfiles.UmaFemale);
            menu.StartNewGame(new NewGameConfiguration(10518, rulerName: "Мира", dynastyName: "Дом Ясного Огня",
                rulerSex: CharacterSex.Female, rulerAppearanceProfileId: profile.Id,
                rulerAppearance: profile.CreateDetailedState(DeterministicStableId.Create(10518,
                    "player_character", 0)).With(ageId: new AppearancePartId("age_aged"))));
            while (SceneManager.GetActiveScene().name != "LocalGameplay") yield return null;
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            foreach (var appearance in root.Appearances.CaptureState().Characters)
                root.Tiers.Transition(appearance.CharacterId, CharacterSimulationTier.Tier1);
            root.AiRuntime.SetPaused(true);
            root.CameraController.enabled = false;
            root.enabled = false;

            var camera = Camera.main;
            var movements = root.AiRuntime.Movements.Where(x => x.Presenter != null).ToArray();
            Assert.That(movements.Length, Is.GreaterThanOrEqualTo(4));
            for (var index = 0; index < movements.Length; index++)
            {
                var distance = index < 2 ? 18f : index == 2 ? 46f : 82f;
                movements[index].SetCrowdDetail(index < 2);
                movements[index].Presenter.transform.position = camera.transform.position +
                    camera.transform.forward.normalized * distance;
            }
            typeof(Game.Presentation.AI.Tier1AiRuntimeDriver)
                .GetMethod("DrawLightweightPresenters", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(root.AiRuntime, null);
            Assert.That(root.AiRuntime.LastMidLodDrawCount, Is.GreaterThan(0),
                string.Join(",", movements.Select(x => Vector3.Distance(camera.transform.position,
                    x.Presenter.transform.position).ToString("F1"))));
            Assert.That(root.AiRuntime.LastFarLodDrawCount, Is.GreaterThan(0));

            var states = root.Appearances.CaptureState().Characters;
            var worker = states.First(x => x.ClothingId == StoneAgeAppearanceProfiles.WorkerClothing);
            var military = states.First(x => x.ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier1);
            root.Tiers.Transition(military.CharacterId, CharacterSimulationTier.Tier1);
            var focus = root.StoneAgeSettlement.BiomeWorld.BaseRegion.ProjectToGround(new Vector3(6f, 0f, 3f));
            MoveToNavMesh(root, worker.CharacterId, focus + Vector3.left);
            MoveToNavMesh(root, military.CharacterId, focus + Vector3.right);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier1);
            root.Tiers.Transition(military.CharacterId, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(military.CharacterId, CharacterSimulationTier.Tier1);
            root.PlayerSaves.Save(3);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier2);
            root.Tiers.Transition(worker.CharacterId, CharacterSimulationTier.Tier1);
            root.PlayerSaves.Load(3);
            yield return null;
            Assert.That(root.CharacterPresentations.TryGet(worker.CharacterId, out var restoredWorker), Is.True,
                "Worker Tier1 presenter was not restored.");
            Assert.That(root.CharacterPresentations.TryGet(military.CharacterId, out var restoredMilitary), Is.True,
                "Military Tier1 presenter was not restored.");
            Assert.That(HasComposedHead(restoredWorker), Is.True);
            Assert.That(HasComposedHead(restoredMilitary), Is.True);
        }

        private static bool HasComposedHead(CharacterPresenter presenter) =>
            presenter != null && presenter.transform.Find("RoleVisual/SourceVisual/P105_Head") != null &&
            presenter.transform.Find("RoleVisual/SourceVisual/P105_Eyes") != null;

        private static void MoveToNavMesh(LocalSceneCompositionRoot root, StableEntityId id, Vector3 position)
        {
            Assert.That(root.CharacterPresentations.TryGet(id, out var presenter), Is.True);
            var agent = presenter.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(position);
            else presenter.transform.position = position;
        }

        private static void RefreshCrowdLodNow(LocalSceneCompositionRoot root)
        {
            typeof(Game.Presentation.AI.Tier1AiRuntimeDriver)
                .GetMethod("RefreshCrowdDetailLevels", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(root.AiRuntime, null);
        }
    }
}
