using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class P109PlayablePatchTests
    {
        [UnityTest]
        public IEnumerator CameraAndOrdersSurviveMovingFocusAway()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var focus = root.CameraController.transform.position;
            Assert.That(focus.y, Is.EqualTo(root.WorldHeight.SampleHeight(focus.x, focus.z)).Within(0.01f));
            Assert.That(root.PointerRaycaster.WorldCamera.transform.position.y, Is.GreaterThan(focus.y + 5f));
            var resident = root.AiRuntime.Movements.First(value => value.CharacterId == root.MaleDemoCharacter.Id).Presenter;
            root.SelectionProbe.SelectCharacters(new[] { resident });
            var target = new WorldPosition(resident.transform.position.x + 12f, focus.y, resident.transform.position.z + 8f);
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.Ground, target), Vector2.zero);
            var result = root.GameplayUi.ExecuteContextAction(ContextActionKind.Move, false);
            Assert.That(result.Orders.Count, Is.EqualTo(1));
            root.CameraController.transform.position += Vector3.right * 350f;
            for (var i = 0; i < 12; i++) root.TierPolicy.Evaluate(new WorldPosition(350f, focus.y, 3f));
            Assert.That(root.Tiers.GetTier(root.MaleDemoCharacter.Id), Is.EqualTo(CharacterSimulationTier.Tier1));
            var deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline && Vector3.Distance(resident.transform.position,
                       new Vector3(target.X, target.Y, target.Z)) > 2f) yield return null;
            Assert.That(Vector3.Distance(resident.transform.position, new Vector3(target.X, target.Y, target.Z)), Is.LessThan(2f));
            root.CameraController.FocusHome();
            Assert.That(resident.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator EveryStarterTreeHasItsOwnCanonicalClickableSource()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var trees = root.PopulatedBiomeWorld.CanopyTrees.Concat(root.PopulatedBiomeWorld.BaseRegion.Trees).Distinct().ToArray();
            var ids = trees.Select(value => value.GetComponent<ProductionSourcePresenter>()).ToArray();
            Assert.That(ids.All(value => value != null && value.IsBound), Is.True);
            Assert.That(ids.Select(value => value.Site.Id).Distinct().Count(), Is.EqualTo(trees.Length));
            Assert.That(trees.All(value => value.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(root.Production.IsRecipeAvailable(new ProductionRecipeId("chop_wood")), Is.True);
            foreach (var formation in root.PopulatedBiomeWorld.ResourceFormations.Where(x => x.RecipeId == "chop_wood"))
                Assert.That(formation.ClickableRoot.GetComponent<ProductionSourcePresenter>().Site.Id,
                    Is.EqualTo(Game.Domain.Campaign.DeterministicStableId.Create(root.ActiveNewGameConfiguration.WorldSeed,
                        "player_source", formation.StableIndex)),
                    "An authored source must not be rebound to a second invisible production identity.");
            foreach (var tree in trees)
            {
                Assert.That(tree.GetComponentsInChildren<Renderer>(true).SelectMany(value => value.sharedMaterials)
                    .Where(value => value != null).Any(value => value.shader.name.Contains("SpeedTree")), Is.False);
                Assert.That(tree.GetComponentsInChildren<Renderer>(true).Any(value => value.isPartOfStaticBatch),
                    Is.False, "Runtime-grounded trees must not use scene-space static batches.");
            }
        }

        [UnityTest]
        public IEnumerator PointerSelectsResidentInsteadOfTreeAndClearGroundRemainsMoveTarget()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.CameraController.FocusHome();
            yield return null;
            Physics.SyncTransforms();
            var resident = root.AiRuntime.Movements.First(x => x.CharacterId == root.MaleDemoCharacter.Id).Presenter;
            var screen = root.PointerRaycaster.WorldCamera.WorldToScreenPoint(resident.transform.position + Vector3.up);
            Assert.That(root.PointerRaycaster.TryGetSelectableHit(screen, out var selected), Is.True);
            Assert.That(selected.collider.GetComponentInParent<Game.Presentation.Characters.CharacterPresenter>(),
                Is.SameAs(resident), "Tree foliage selection must not steal the resident click.");
            var ground = new Vector3(0f, root.WorldHeight.SampleHeight(0f, -12f), -12f);
            screen = root.PointerRaycaster.WorldCamera.WorldToScreenPoint(ground);
            Assert.That(root.PointerRaycaster.TryGetContextHit(screen, out var contextHit), Is.True);
            Assert.That(contextHit.collider, Is.InstanceOf<TerrainCollider>(),
                "The visible empty clearing must offer movement, not an invisible crown's chop order.");
        }

        [UnityTest]
        public IEnumerator MineCommandReachesRockAndCommitsWholeSource()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var source = root.PopulatedBiomeWorld.ResourceFormations.First(x => x.RecipeId == "gather_stone")
                .ClickableRoot.GetComponent<ProductionSourcePresenter>();
            var amount = source.RemainingAmount;
            var stone = new ResourceId("stone");
            var before = root.Production.GetProducedTotal(stone);
            var resident = root.AiRuntime.Movements.First(x => x.CharacterId == root.MaleDemoCharacter.Id).Presenter;
            root.SelectionProbe.SelectCharacters(new[] { resident });
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,
                source.Site.Position, source.Site.Id, stone, ContextResourceClass.Mineable), Vector2.zero);
            var command = root.GameplayUi.ExecuteContextAction(ContextActionKind.Mine, false);
            Assert.That(command.Orders.Count, Is.EqualTo(1));
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!source.Site.IsDepleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(source.Site.IsDepleted, Is.True, "Mining must reach a connected approach and complete.");
            Assert.That(root.Production.GetProducedTotal(stone) - before, Is.EqualTo(amount));
            Assert.That(source.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ChopCommandFellsWholeTreeCommitsExactLogsAndSurvivesIsolatedSave()
        {
            Assert.That(System.Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY"),
                Does.Contain("Artifacts"), "This file-writing test requires an isolated test save directory.");
            yield return SceneManager.LoadSceneAsync("LocalGameplay");
            yield return null;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var source = root.PopulatedBiomeWorld.ResourceFormations.First(x => x.RecipeId == "chop_wood")
                .ClickableRoot.GetComponent<ProductionSourcePresenter>();
            var id = source.Site.Id;
            var amount = source.RemainingAmount;
            var wood = new ResourceId("wood_log");
            var before = root.Production.GetProducedTotal(wood);
            var resident = root.AiRuntime.Movements.First(x => x.CharacterId == root.MaleDemoCharacter.Id).Presenter;
            root.SelectionProbe.SelectCharacters(new[] { resident });
            root.GameplayUi.OpenContextMenu(new ContextCommandTarget(ContextTargetKind.ResourceSource,
                source.Site.Position, id, wood, ContextResourceClass.Choppable), Vector2.zero);
            var command = root.GameplayUi.ExecuteContextAction(ContextActionKind.Chop, false);
            Assert.That(command.Orders.Count, Is.EqualTo(1));
            root.Clock.SetSpeed(Game.Simulation.Time.GameSpeed.Maximum);
            var deadline = Time.realtimeSinceStartup + 60f;
            var sawProgress = false;
            while (!source.Site.IsDepleted && Time.realtimeSinceStartup < deadline)
            {
                sawProgress |= root.Production.ActiveTasks.Any(x => x.SiteId == id && x.AccumulatedWorkTicks > 0);
                yield return null;
            }
            var movement = root.AiRuntime.Movements.First(x => x.CharacterId == resident.CharacterId);
            var diagnosis = $"Position={resident.transform.position}; source={source.transform.position}; " +
                $"movement={movement.State}; agentPath={movement.Agent.pathStatus}; " +
                $"distance={movement.Agent.remainingDistance}; velocity={movement.Agent.velocity}; " +
                $"orders={string.Join(",", root.Work.Jobs.Select(x => x.Status))}";
            Assert.That(sawProgress, Is.True, "The physical command never progressed through work. " + diagnosis);
            Assert.That(source.Site.IsDepleted, Is.True, "One chop command must fell the selected tree.");
            Assert.That(root.Production.GetProducedTotal(wood) - before, Is.EqualTo(amount));
            source.Refresh();
            Assert.That(Object.FindObjectsByType<NaturalHarvestEffect>(FindObjectsSortMode.None).Length, Is.GreaterThan(0));
            root.Clock.Pause();
            root.PlayerSaves.Save(3);
            root.PlayerSaves.Load(3);
            yield return null;
            Assert.That(root.Production.TryGetSite(id, out var restored), Is.True);
            Assert.That(restored.RemainingAmount, Is.Zero);
            Assert.That(root.Production.GetProducedTotal(wood) - before, Is.EqualTo(amount));
        }
    }
}
