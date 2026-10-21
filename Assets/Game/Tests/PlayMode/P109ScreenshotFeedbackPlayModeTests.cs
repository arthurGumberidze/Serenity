using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Interaction;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class P109ScreenshotFeedbackPlayModeTests
    {
        private LocalSceneCompositionRoot root;

        [UnitySetUp]
        public IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.Clock.Pause();
        }

        [UnityTest]
        public IEnumerator LegacyShapesAreInactiveAndFishStaySeparatedInPauseAndMotion()
        {
            var legacy = Object.FindAnyObjectByType<P102ConnectedWorldPresentation>();
            Assert.That(legacy.InlandWater.All(x => !x.gameObject.activeInHierarchy), Is.True);
            Assert.That(legacy.MountainLandmarks.All(x => !x.gameObject.activeInHierarchy), Is.True);
            var fishViews = Object.FindObjectsByType<ProductionSourcePresenter>()
                .Where(x => x.IsBound && x.Site.RecipeId.ToString() == "fish_water").ToArray();
            Assert.That(fishViews.Length, Is.EqualTo(44));
            foreach (var school in fishViews)
            {
                var fish = school.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("Swimming fish ")).ToArray();
                Assert.That(fish.Length, Is.EqualTo(10));
                for (var i = 0; i < fish.Length; i++)
                for (var j = i + 1; j < fish.Length; j++)
                    Assert.That(Vector3.Distance(fish[i].position, fish[j].position), Is.GreaterThan(0.5f), school.name);
                Assert.That(fish.All(x => x.Find("Tail") != null), Is.True);
            }
            var sample = fishViews[0].transform.Find("Swimming fish 0");
            var pausedPosition = sample.position;
            yield return null;
            Assert.That(sample.position, Is.EqualTo(pausedPosition));
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Normal);
            root.Clock.Resume();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Vector3.Distance(sample.position, pausedPosition), Is.GreaterThan(0.02f));
        }

        [UnityTest]
        public IEnumerator FoundationChecksAllCellsAndRejectsNewBlockerAtConfirmation()
        {
            var definition = new BuildingDefinition(new BuildingDefinitionId("test_footprint"), "Test", BuildingCategory.Shelter,
                new BuildingFootprint(2, 2));
            // Find a naturally empty, dry site rather than assuming an old map coordinate is clear.
            GridCoordinate? clear = null;
            for (var z = 15; z < 65 && !clear.HasValue; z += 3)
            for (var x = 15; x < 65 && !clear.HasValue; x += 3)
                if (root.BuildingPlacementService.Evaluate(definition, new GridCoordinate(x, z), BuildingOrientation.North).IsValid)
                    clear = new GridCoordinate(x, z);
            Assert.That(clear.HasValue, Is.True);
            var cell = clear.Value;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Test blocking rock at far footprint cell";
            rock.transform.position = root.TiledWorld.ProjectToGround(new Vector3(cell.X + 1, 0, cell.Z + 1)) + Vector3.up;
            rock.transform.localScale = new Vector3(0.6f, 2, 0.6f);
            Physics.SyncTransforms();
            Assert.That(root.BuildingPlacementService.Evaluate(definition, cell, BuildingOrientation.North).IsValid, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => root.BuildingPlacementService.Confirm(definition, cell, BuildingOrientation.North));
            Object.Destroy(rock);
            yield return null;
            Assert.That(root.BuildingPlacementService.Evaluate(definition, cell, BuildingOrientation.North).IsValid, Is.True);
            Assert.That(root.WorldPlacementSurface.AllowsFootprint(new[] { new GridCoordinate(88, 72) }, 1, Vector3.zero), Is.False);
        }

        [UnityTest]
        public IEnumerator LightweightMovementFollowsHeightAcrossASlope()
        {
            root.AiRuntime.enabled = false;
            var movement = root.AiRuntime.Movements.First();
            movement.Stop();
            var start = movement.Presenter.transform.position;
            var path = new NavMeshPath();
            Vector3? destination = null;
            for (var z = 20; z < 90 && !destination.HasValue; z += 8)
            for (var x = -25; x < 70 && !destination.HasValue; x += 8)
            {
                var target = root.TiledWorld.ProjectToGround(new Vector3(x, 0, z));
                if (!NavMesh.SamplePosition(target, out var hit, 2, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - start.y) < 0.4f) continue;
                if (NavMesh.CalculatePath(start, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    destination = hit.position;
            }
            Assert.That(destination.HasValue, Is.True);
            movement.SetCrowdDetail(false);
            movement.UpdatePresentation(false, 1);
            var end = destination.Value;
            movement.MoveTo(new Game.Domain.AI.WorldPosition(end.x, end.y, end.z), 0.1f);
            var steps = 0;
            while (movement.State == Game.Simulation.AI.MovementState.Moving && steps++ < 4000)
            {
                movement.AdvanceLightweight(0.05f, false);
                var point = movement.Presenter.transform.position;
                Assert.That(NavMesh.SamplePosition(point, out var ground, 2, NavMesh.AllAreas), Is.True);
                Assert.That(Mathf.Abs(point.y - ground.position.y), Is.LessThan(0.12f));
            }
            Assert.That(movement.State, Is.EqualTo(Game.Simulation.AI.MovementState.Arrived));
            Assert.That(Mathf.Abs(movement.Presenter.transform.position.y - start.y), Is.GreaterThan(0.25f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplacementDropsCargoWithoutLossAndCarryVisualFollowsHands()
        {
            yield return new WaitForSecondsRealtime(3);
            root.AiRuntime.enabled = false;
            var movement = root.AiRuntime.Movements.First(x => x.Presenter.Animator != null && x.Presenter.Animator.isHuman);
            movement.Stop();
            var presenter = movement.Presenter;
            var load = root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.CarryLoad, presenter.CharacterId));
            var wood = new ResourceId("wood_log");
            Assert.That(load.TryAdd(wood, new ResourceQuantity(5)), Is.True);
            presenter.Animator.enabled = true;
            presenter.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            presenter.Animator.Play("Carry", 0, 0.4f);
            presenter.Animator.Update(0);
            yield return new WaitForSecondsRealtime(0.35f);
            var cargo = presenter.transform.Find("Physical transport load");
            Assert.That(cargo, Is.Not.Null);
            var midpoint = (presenter.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position +
                            presenter.Animator.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
            Assert.That(Vector3.Distance(cargo.position, midpoint), Is.LessThan(0.15f));
            var before = root.WorldPiles.All.Where(x => x.ResourceId == wood)
                .Sum(x => root.Inventories.Get(x.InventoryOwner).GetAmount(wood).Units);
            var result = root.ContextCommands.Issue(new[] { presenter.CharacterId }, ContextActionKind.Move,
                new ContextCommandTarget(ContextTargetKind.Ground, movement.CurrentPosition), false,
                _ => new ContextActionFacts(alive: true));
            Assert.That(result.Orders.Count, Is.EqualTo(1));
            Assert.That(load.TotalUnits, Is.Zero);
            var after = root.WorldPiles.All.Where(x => x.ResourceId == wood)
                .Sum(x => root.Inventories.Get(x.InventoryOwner).GetAmount(wood).Units);
            Assert.That(after - before, Is.EqualTo(5));
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(cargo.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HumanAnimationStaysAtFeetAndFacesNavigationForward()
        {
            // Sample the real scene avatars/controllers, including late-generated UMA bodies.
            yield return new WaitForSecondsRealtime(3);
            root.AiRuntime.enabled = false;
            var poseErrors = new System.Collections.Generic.List<string>();
            foreach (var movement in root.AiRuntime.Movements.ToArray())
            {
                movement.Stop();
                var presenter = movement.Presenter;
                var animator = presenter.Animator;
                if (animator == null || !animator.isHuman) continue;
                Assert.That(animator.applyRootMotion, Is.False);
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1;
                var terrainY = root.WorldHeight.SampleHeight(presenter.transform.position.x, presenter.transform.position.z);
                if (Mathf.Abs(presenter.transform.position.y - terrainY) > 0.4f)
                    poseErrors.Add(animator.name + " root off terrain");
                foreach (var state in new[] { "Idle", "Carry", "Chop", "Mine", "Build", "Fish", "Sleep" })
                {
                    animator.Play(state, 0, 0.4f);
                    animator.Update(0);
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    var right = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    var feet = (left + right) * 0.5f;
                    var shoulders = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position -
                                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                    var forward = Vector3.Cross(shoulders, Vector3.up).normalized;
                    Debug.Log($"P109_POSE {presenter.name} {animator.name} {state} feetLocal={presenter.transform.InverseTransformPoint(feet)} forward={Vector3.Dot(forward, presenter.transform.forward)} root={animator.transform.localPosition}");
                    if ((state == "Idle" || state == "Carry") && Vector3.Dot(forward, presenter.transform.forward) <= 0.5f)
                        poseErrors.Add(animator.name + " " + state + " faces backwards");
                    if (Mathf.Min(left.y, right.y) - presenter.transform.position.y < -0.15f)
                        poseErrors.Add(animator.name + " " + state + " feet off ground");
                    if (animator.GetBoneTransform(HumanBodyBones.Head).position.y < terrainY + 0.1f)
                        poseErrors.Add(animator.name + " " + state + " head below terrain");
                }
                var feetVolume = presenter.transform.Find("Feet selection volume").GetComponent<Collider>();
                Physics.SyncTransforms();
                var footPoint = presenter.transform.TransformPoint(new Vector3(0, 0.1f, 0.5f));
                Assert.That(feetVolume.Raycast(new Ray(footPoint + Vector3.up * 2, Vector3.down), out _, 3), Is.True);
                var proxy = presenter.GetComponent<StableWorldHitProxy>();
                proxy.SetHovered(true);
                Assert.That(presenter.transform.Find("Interaction State Ring").gameObject.activeSelf, Is.True);
            }
            Assert.That(poseErrors, Is.Empty, string.Join("; ", poseErrors));
        }
    }
}
