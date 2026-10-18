using System.Collections;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Interaction;
using Game.Presentation.Production;
using Game.Presentation.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P103")]
    public sealed class P103ContextInteractionPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryInteractiveWorldObjectHasStableHitProxyAndCollider()
        {
            foreach (var presenter in Object.FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude))
                AssertProxy(presenter.gameObject, presenter.CharacterId, ContextTargetKind.Character);
            foreach (var presenter in Object.FindObjectsByType<WorldResourcePilePresenter>(FindObjectsInactive.Exclude)
                         .Where(x => x.GetComponent<StableWorldHitProxy>()?.IsBound == true))
                AssertProxy(presenter.gameObject, presenter.PileId, ContextTargetKind.ResourcePile);
            foreach (var presenter in Object.FindObjectsByType<ProductionSourcePresenter>(FindObjectsInactive.Exclude)
                         .Where(x => x.IsBound))
                AssertProxy(presenter.gameObject, presenter.Site.Id, ContextTargetKind.ResourceSource);
            foreach (var presenter in Object.FindObjectsByType<BuildingPresenter>(FindObjectsInactive.Exclude)
                         .Where(x => x.IsBound))
                AssertProxy(presenter.gameObject, presenter.BuildingId, ContextTargetKind.Building);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BoxSelectionContextMenuQueueSaveAndTierRoundTripKeepStableIdentity()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            foreach (var order in root.Work.Jobs.Where(x => !x.IsTerminal).ToArray()) root.Work.Cancel(order.Id);
            var presenters = Object.FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude)
                .Where(x => x.IsBound).OrderBy(x => x.CharacterId).ToArray();
            Assert.That(presenters.Length, Is.GreaterThanOrEqualTo(2));

            var points = presenters.Take(2).Select(x => root.PointerRaycaster.WorldCamera.WorldToScreenPoint(
                x.transform.position + Vector3.up)).ToArray();
            var box = Rect.MinMaxRect(points.Min(x => x.x) - 12f, points.Min(x => x.y) - 12f,
                points.Max(x => x.x) + 12f, points.Max(x => x.y) + 12f);
            Assert.That(root.SelectionProbe.SelectWithinScreenRect(box), Is.GreaterThanOrEqualTo(2));
            Assert.That(root.SelectionProbe.SelectedCharacterIds.Count, Is.GreaterThanOrEqualTo(2));

            root.SelectionProbe.SelectCharacters(new[] { presenters[0] });
            var selectedId = presenters[0].CharacterId;
            var anchor = root.InhabitableRegion.PlayerTerritoryAnchor.position;
            var firstTarget = new ContextCommandTarget(ContextTargetKind.Ground,
                new WorldPosition(anchor.x + 1f, anchor.y, anchor.z + 1f));
            root.GameplayUi.OpenContextMenu(firstTarget, new Vector2(Screen.width - 2f, 2f));
            yield return null;
            Assert.That(root.GameplayUi.ContextActionStates.Count, Is.EqualTo(9));
            Assert.That(root.GameplayUi.ContextActionStates[ContextActionKind.Move].Enabled, Is.True);
            Assert.That(root.GameplayUi.ContextActionStates[ContextActionKind.Inspect].Reason,
                Is.EqualTo(ContextUnavailableReason.WrongTargetType));
            // A normal Player click can be observed by SelectionProbe in the same frame that UI Toolkit
            // closes the menu. The menu must execute against its captured stable roster and restore it.
            root.SelectionProbe.ClearSelection();
            var normal = root.GameplayUi.ExecuteContextAction(ContextActionKind.Move, false).Orders.Single();
            yield return null;
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(selectedId));

            var secondTarget = new ContextCommandTarget(ContextTargetKind.Ground,
                new WorldPosition(anchor.x + 2f, anchor.y, anchor.z + 2f));
            root.GameplayUi.OpenContextMenu(secondTarget, new Vector2(20f, 20f));
            var shifted = root.GameplayUi.ExecuteContextAction(ContextActionKind.Move, true).Orders.Single();
            Assert.That(root.Work.GetQueue(selectedId).Select(x => x.Id),
                Is.EqualTo(new[] { normal.Id, shifted.Id }));

            root.PlayerSaves.Save(3);
            root.Work.Cancel(normal.Id);
            root.Work.Cancel(shifted.Id);
            root.GameplayUi.ConfirmLoadSlot(3);
            Assert.That(root.Work.GetQueue(selectedId).Select(x => x.Id),
                Is.EqualTo(new[] { normal.Id, shifted.Id }));
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(selectedId));

            root.Tiers.Transition(selectedId, CharacterSimulationTier.Tier2);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(selectedId));
            var tierQueue = root.Work.GetQueue(selectedId).Select(x => x.Id).ToArray();
            Assert.That(tierQueue, Has.Member(normal.Id));
            Assert.That(tierQueue, Has.Member(shifted.Id));
            root.Tiers.Transition(selectedId, CharacterSimulationTier.Tier1);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(root.SelectionProbe.SelectedCharacterIds, Does.Contain(selectedId));
            Assert.That(root.SelectionProbe.SelectedCharacterPresenter, Is.Not.Null);
        }

        private static void AssertProxy(GameObject gameObject, StableEntityId expectedId, ContextTargetKind kind)
        {
            var proxy = gameObject.GetComponent<StableWorldHitProxy>();
            Assert.That(proxy, Is.Not.Null, gameObject.name);
            Assert.That(proxy.IsBound, Is.True, gameObject.name);
            Assert.That(proxy.EntityId, Is.EqualTo(expectedId), gameObject.name);
            Assert.That(proxy.TargetKind, Is.EqualTo(kind), gameObject.name);
            Assert.That(gameObject.GetComponentInChildren<Collider>(true), Is.Not.Null, gameObject.name);
        }
    }
}
