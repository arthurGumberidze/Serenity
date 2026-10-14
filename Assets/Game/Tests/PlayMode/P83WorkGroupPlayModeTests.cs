using System.Collections;
using System.Linq;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Production;
using Game.Presentation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P83")]
    public sealed class P83WorkGroupPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectedResourceCreatesCanonicalPrioritizedGroupOrder()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var source = Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                .Where(x => x.IsBound && x.Site.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource)
                .OrderBy(x => x.Site.Id).First();
            AssignGatherer(root);
            root.SelectionProbe.SelectProductionSource(source);
            var order = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                WorkPriority.Immediate);

            Assert.That(root.Work.Groups.All.Count(x => x.Role != WorkGroupRole.General), Is.EqualTo(9));
            Assert.That(order.Target.EntityId, Is.EqualTo(source.Site.Id));
            Assert.That(order.Priority, Is.EqualTo(WorkPriority.Immediate));
            Assert.That(order.AssigneeId, Is.EqualTo(root.MaleDemoCharacter.Id));
            Assert.That(root.SelectionProbe.SelectedProductionSourcePresenter, Is.SameAs(source));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActiveAssignmentsAndReservationsSurviveSaveLoadAndRebind()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var source = Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsSortMode.None)
                .Where(x => x.IsBound && x.Site.Kind == Game.Domain.Production.ProductionSiteKind.NaturalSource)
                .OrderBy(x => x.Site.Id).First();
            AssignGatherer(root);
            var order = root.DirectWorkCommands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, source.Site.Id, source.Site.Position),
                WorkPriority.High);
            root.Work.SetClaimState(order.Id, WorkClaimState.Reserved);
            root.PlayerSaves.Save(2);
            root.Work.Cancel(order.Id);
            root.PlayerSaves.Load(2);

            var restored = root.Work.Get(order.Id);
            Assert.That(restored.Status, Is.EqualTo(WorkJobStatus.Assigned));
            Assert.That(restored.ClaimState, Is.EqualTo(WorkClaimState.Reserved));
            Assert.That(restored.AssigneeId, Is.EqualTo(root.MaleDemoCharacter.Id));
            Assert.Throws<System.InvalidOperationException>(() => root.DirectWorkCommands.Issue(WorkJobType.Gather,
                restored.Target, WorkPriority.Normal));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalResourceMarkerIssuesSameCanonicalGatherOrder()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            AssignGatherer(root);
            root.GlobalMapBridge.RequestEnterGlobal();
            var timeout = Time.realtimeSinceStartup + 15f;
            while (!root.GlobalMapBridge.IsGlobalSceneLoaded && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            var controller = Object.FindAnyObjectByType<GlobalMapPresentationController>();
            var marker = controller.GetComponentsInChildren<GlobalMapPresentationController.GlobalResourceMarker>(true)
                .OrderBy(x => x.ResourceId).First();
            controller.SelectResourceMarker(marker);
            var order = controller.IssueSelectedResourceWork();

            Assert.That(order.Type, Is.EqualTo(WorkJobType.Gather));
            Assert.That(marker.SiteIds, Does.Contain(order.Target.EntityId));
            Assert.That(root.Work.Get(order.Id), Is.SameAs(order));
        }

        private static void AssignGatherer(LocalSceneCompositionRoot root)
        {
            var group = root.Work.Groups.All.Single(x => x.Role == WorkGroupRole.Gatherers);
            if (root.Work.Groups.TryGetForMember(root.MaleDemoCharacter.Id, out var previous))
                root.Work.RemoveMember(previous.Id, root.MaleDemoCharacter.Id);
            root.Work.AddMember(group.Id, root.MaleDemoCharacter.Id);
        }
    }
}
