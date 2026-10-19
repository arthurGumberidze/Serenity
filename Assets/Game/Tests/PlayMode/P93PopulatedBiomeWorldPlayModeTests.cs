using System.Collections;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P93")]
    public sealed class P93PopulatedBiomeWorldPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DenseBiomeCollidersPreserveStarterRoutesOnActiveTiledNavMesh()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.PopulatedBiomeWorld, Is.Not.Null);
            Assert.DoesNotThrow(root.PopulatedBiomeWorld.ValidateConfiguration);
            var start = root.DemoPresenters[0].transform.position;
            Assert.That(NavMesh.SamplePosition(start, out var sampledStart, 4f, NavMesh.AllAreas), Is.True);
            foreach (var formation in root.PopulatedBiomeWorld.ResourceFormations.Take(4))
            {
                var anchor = formation.ClickableRoot.position;
                Assert.That(NavMesh.SamplePosition(anchor, out var destination,
                    4f, NavMesh.AllAreas), Is.True, formation.RecipeId);
                Assert.That(Vector3.Distance(destination.position, anchor), Is.LessThanOrEqualTo(4f),
                    formation.RecipeId);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(sampledStart.position, destination.position, NavMesh.AllAreas, path), Is.True,
                    formation.RecipeId);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                    $"{formation.RecipeId}; anchor={anchor}; destination={destination.position}; " +
                    $"corners={string.Join(" -> ", path.corners.Select(x => x.ToString("F2")))}");
            }
            Assert.That(root.PopulatedBiomeWorld.CanopyTrees.All(x =>
                x.GetComponentInChildren<LODGroup>(true) != null &&
                x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(root.PopulatedBiomeWorld.GroundCover.All(x =>
                x.GetComponentInChildren<Collider>(true) == null), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllResourceFormationsRemainClickableAndUseCanonicalWorkAndSaveState()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var groups = root.DirectWorkCommands.EnsureRoleGroups();
            var gatherers = groups.Single(x => x.Role == WorkGroupRole.Gatherers);
            var worker = root.MaleDemoCharacter.Id;
            if (root.Work.Groups.TryGetForMember(worker, out var prior) && prior.Id != gatherers.Id)
                root.Work.RemoveMember(prior.Id, worker);
            if (!gatherers.Contains(worker)) root.Work.AddMember(gatherers.Id, worker);

            foreach (var formation in root.PopulatedBiomeWorld.ResourceFormations)
            {
                var presenter = formation.ClickableRoot.GetComponent<ProductionSourcePresenter>();
                Assert.That(presenter, Is.Not.Null, formation.StableIndex.ToString());
                root.SelectionProbe.SelectProductionSource(presenter);
                Assert.That(root.SelectionProbe.SelectedProductionSourcePresenter, Is.SameAs(presenter));
            }

            var selected = root.InhabitableRegion.ResourceAnchors.Single(x => x.StableIndex == 20)
                .VisualRoot.GetComponent<ProductionSourcePresenter>();
            var siteId = selected.Site.Id;
            var order = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, siteId, selected.Site.Position),
                WorkPriority.High);
            Assert.That(order.Target.EntityId, Is.EqualTo(siteId));
            root.Work.Cancel(order.Id);
            root.PlayerSaves.Save(2);
            root.PlayerSaves.Load(2);
            Assert.That(root.Production.TryGetSite(siteId, out var restored), Is.True);
            Assert.That(selected.Site, Is.SameAs(restored));
            Assert.That(root.PopulatedBiomeWorld.ResourceFormations.Count, Is.EqualTo(24));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SoilClearingsStillBuildAndSeasonProjectionDoesNotMutateWeatherAuthority()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "shelter_construction");
            Assert.That(root.BuildingPlacementController.TryStartPlacement(
                new BuildingDefinitionId("primitive_shelter")), Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)), Is.True);
            var building = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            var presenter = root.BuildingPlacementController.LastPlacedPresenter;
            var terrainY = root.TiledWorld.ProjectToGround(presenter.transform.position).y;
            Assert.That(Mathf.Abs(presenter.transform.position.y - terrainY), Is.LessThan(0.15f));

            var before = root.Weather.CaptureState();
            var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Season == Season.Winter);
            root.WeatherPresentation.ShowPreview(winter);
            Assert.That(root.InhabitableRegion.DisplayedSeason, Is.EqualTo(Season.Winter));
            Assert.That(root.InhabitableRegion.SnowCoverVisible, Is.True);
            Assert.That(root.Weather.CaptureState().WorldSeed, Is.EqualTo(before.WorldSeed));
            yield return null;
        }

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            root.Research.TryAssignWorker(root.MaleDemoCharacter.Id);
            Assert.That(root.Research.Start(technologyId), Is.True);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
        }
    }
}
