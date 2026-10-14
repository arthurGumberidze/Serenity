using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Weather;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P94")]
    public sealed class P94StoneAgeSettlementPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredSettlementSpawnsDistinctBuildingsAndStoneAgeWear()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.StoneAgeSettlement, Is.Not.Null);
            Assert.DoesNotThrow(root.StoneAgeSettlement.ValidateConfiguration);
            Assert.That(root.DemoShelterPresenter.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(18));
            Assert.That(root.DemoStoragePresenter.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(14));
            Assert.That(root.StoneAgeSettlement.CropPlotPrefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(12));
            Assert.That(root.DemoShelterPresenter.GetComponentsInChildren<MeshFilter>(true)
                .All(x => x.sharedMesh.name.StartsWith("P94")), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GatherHaulAndBuildOrdersReceiveReadablePriorityIndicators()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var worker = root.MaleDemoCharacter.Id;
            CancelOpen(root);
            var source = root.InhabitableRegion.ResourceAnchors
                .Select(x => x.VisualRoot.GetComponent<ProductionSourcePresenter>())
                .Where(x => x != null).OrderBy(x => x.Site.Id).First();

            Assign(root, worker, WorkGroupRole.Gatherers);
            var gather = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                WorkPriority.High);
            root.WorkOrderIndicators.RefreshNow();
            Assert.That(root.WorkOrderIndicators.TryGetIndicator(gather.Id, out var gatherIndicator), Is.True);
            Assert.That(gatherIndicator.name, Does.Contain("High"));
            root.Work.Cancel(gather.Id);

            var pile = root.DemoPilePresenters.First(x => x.Quantity.Units > 0);
            Assign(root, worker, WorkGroupRole.Haulers);
            var position = pile.transform.position;
            var haul = root.DirectWorkCommands.IssueHaul(
                new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId),
                new InventoryOwner(InventoryOwnerKind.BuildingStorage, root.DemoStorageBuilding.Id),
                pile.ResourceId, new ResourceQuantity(1), new WorldPosition(position.x, position.y, position.z),
                WorkPriority.Urgent);
            root.WorkOrderIndicators.RefreshNow();
            Assert.That(root.WorkOrderIndicators.TryGetIndicator(haul.Id, out var haulIndicator), Is.True);
            Assert.That(haulIndicator.name, Does.Contain("Urgent"));
            root.Work.Cancel(haul.Id);

            CompleteTechnology(root, "shelter_construction");
            Assert.That(root.BuildingPlacementController.TryStartPlacement(
                new BuildingDefinitionId("primitive_shelter")), Is.True);
            Assert.That(root.BuildingPlacementController.TryMovePreviewToWorldPoint(new Vector3(8f, 0f, -6f)), Is.True);
            var building = root.BuildingPlacementController.ConfirmPlacement();
            Assert.That(root.BuildingPresentations.TryGet(building.Id, out var presenter), Is.True);
            Assign(root, worker, WorkGroupRole.Builders);
            var target = presenter.transform.position;
            var build = root.DirectWorkCommands.Issue(WorkJobType.Build,
                WorkTarget.ForEntity(WorkTargetKind.Building, building.Id,
                    new WorldPosition(target.x, target.y, target.z)), WorkPriority.Immediate);
            root.WorkOrderIndicators.RefreshNow();
            Assert.That(root.WorkOrderIndicators.TryGetIndicator(build.Id, out var buildIndicator), Is.True);
            Assert.That(buildIndicator.name, Does.Contain("Immediate"));
            Assert.That(root.WorkOrderIndicators.VisibleIndicatorCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettlementNavigationWeatherAndSaveLoadKeepCanonicalIdentity()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var worker = root.MaleDemoCharacter.Id;
            CancelOpen(root);
            var source = root.InhabitableRegion.ResourceAnchors
                .Select(x => x.VisualRoot.GetComponent<ProductionSourcePresenter>())
                .Where(x => x != null).OrderBy(x => x.Site.Id).First();
            var siteId = source.Site.Id;
            Assign(root, worker, WorkGroupRole.Gatherers);
            var order = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, siteId, source.Site.Position),
                WorkPriority.High);
            root.PlayerSaves.Save(2);
            root.Work.SetPriority(order.Id, WorkPriority.Low);
            root.PlayerSaves.Load(2);
            Assert.That(root.Work.Get(order.Id).Priority, Is.EqualTo(WorkPriority.High));
            Assert.That(root.Production.TryGetSite(siteId, out var restored), Is.True);
            Assert.That(source.Site, Is.SameAs(restored));

            var destination = root.StoneAgeSettlement.Worksites
                .First(x => x.Kind == P94StoneAgeSettlement.WorksiteKind.Construction).VisualRoot.transform.position;
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(root.InhabitableRegion.PlayerTerritoryAnchor.position,
                destination, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

            var before = root.Weather.CaptureState();
            var winter = Enumerable.Range(0, 1460).Select(x => root.Weather.GetWeatherForDay(x))
                .First(x => x.Season == Season.Winter);
            root.WeatherPresentation.ShowPreview(winter);
            Assert.That(root.InhabitableRegion.DisplayedSeason, Is.EqualTo(Season.Winter));
            Assert.That(root.InhabitableRegion.SnowCoverVisible, Is.True);
            Assert.That(root.Weather.CaptureState().WorldSeed, Is.EqualTo(before.WorldSeed));
            yield return null;
        }

        private static void Assign(LocalSceneCompositionRoot root, StableEntityId worker, WorkGroupRole role)
        {
            var group = root.DirectWorkCommands.EnsureRoleGroups().Single(x => x.Role == role);
            if (root.Work.Groups.TryGetForMember(worker, out var prior) && prior.Id != group.Id)
                root.Work.RemoveMember(prior.Id, worker);
            if (!group.Contains(worker)) root.Work.AddMember(group.Id, worker);
        }

        private static void CancelOpen(LocalSceneCompositionRoot root)
        {
            foreach (var order in root.Work.Jobs.Where(x => !x.IsTerminal).ToArray()) root.Work.Cancel(order.Id);
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
