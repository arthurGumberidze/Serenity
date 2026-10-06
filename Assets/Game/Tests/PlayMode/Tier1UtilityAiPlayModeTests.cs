using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Presentation.AI;
using Game.Presentation.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U10")]
    public sealed class Tier1UtilityAiPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGameplayBuildsNavMeshAndRegistersTier1Agents()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.NavigationSurface, Is.Not.Null);
            Assert.That(root.NavigationSurface.navMeshData, Is.Not.Null);
            Assert.That(root.AiRuntime, Is.Not.Null);
            Assert.That(root.AiRuntime.PerAgentAiUpdateCount, Is.Zero);
            Assert.That(root.AiAgents.Count, Is.EqualTo(2));
            Assert.That(root.AiScheduler.Count, Is.EqualTo(2));
            Assert.That(root.DemoStorageBuilding, Is.Not.Null);
            Assert.That(root.DemoPresenters.All(x => x.GetComponent<NavMeshAgent>() != null), Is.True);
            Assert.That(root.DemoPresenters.All(x => x.GetComponent<NavMeshAgent>().isOnNavMesh), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavMeshMovementDrivesAnimationAndHaulConservesResources()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var initialPosition = root.DemoPresenters[0].transform.position;
            var initialTotal = Total(root);
            var initialPileUnits = TotalForKind(root, InventoryOwnerKind.WorldPile);
            var initialCharacterUnits = TotalForKind(root, InventoryOwnerKind.Character);
            var maximumCharacterUnits = initialCharacterUnits;
            var resourceOverlay = Object.FindAnyObjectByType<ResourceDebugOverlay>();
            var storage = root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                root.DemoStorageBuilding.Id));
            var sawMoving = false;
            var moved = false;
            var timeout = Time.realtimeSinceStartup + 18f;
            while (Time.realtimeSinceStartup < timeout && storage.TotalUnits == 0)
            {
                var presenter = root.DemoPresenters[0];
                sawMoving |= presenter.Animator != null && presenter.Animator.GetBool("Moving");
                moved |= Vector3.Distance(initialPosition, presenter.transform.position) > 0.4f;
                maximumCharacterUnits = System.Math.Max(maximumCharacterUnits,
                    TotalForKind(root, InventoryOwnerKind.Character));
                yield return null;
            }
            Assert.That(moved, Is.True, "The Tier 1 presenter never traversed the NavMesh.");
            Assert.That(sawMoving, Is.True, "The U07 Moving parameter was never driven by navigation velocity.");
            Assert.That(storage.TotalUnits, Is.GreaterThan(0), "No canonical resource reached the Storage Basket.");
            Assert.That(Total(root), Is.EqualTo(initialTotal), "Hauling must conserve world + character + storage units.");
            Assert.That(TotalForKind(root, InventoryOwnerKind.WorldPile), Is.LessThan(initialPileUnits),
                "Pickup must reduce canonical world-pile stock.");
            Assert.That(maximumCharacterUnits, Is.GreaterThan(initialCharacterUnits),
                "Pickup must pass through a canonical character inventory.");
            Assert.That(TotalForKind(root, InventoryOwnerKind.Character), Is.LessThan(maximumCharacterUnits),
                "Dropoff must remove the carried quantity from the character inventory.");
            foreach (var definition in root.Resources.All)
            {
                var totals = resourceOverlay.GetCanonicalTotals(definition.Id);
                Assert.That(totals.Total, Is.EqualTo(root.Inventories.All.Sum(x => x.GetAmount(definition.Id).Units)));
                Assert.That(totals.Total, Is.EqualTo(totals.WorldPiles + totals.Characters + totals.Storage));
            }
            Assert.That(root.HaulClaims.Count, Is.LessThanOrEqualTo(root.AiAgents.Count));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(root.PointerRaycaster.WorldCamera);
        }

        [UnityTest]
        public IEnumerator PointerSelectionShowsCanonicalPileAndStorageValues()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            var overlay = Object.FindAnyObjectByType<Tier1AiDebugOverlay>();
            var pile = root.DemoPilePresenters.First();
            SelectFromAbove(root, pile);
            Assert.That(root.SelectionProbe.SelectedWorldResourcePilePresenter, Is.SameAs(pile));
            Assert.That(overlay.TryCaptureSelected(out var pileSnapshot), Is.True);
            Assert.That(pileSnapshot.Kind, Is.EqualTo(DevelopmentSelectionKind.WorldResourcePile));
            Assert.That(pileSnapshot.StableId, Is.EqualTo(pile.PileId));
            Assert.That(pileSnapshot.Resources.Single().ResourceId, Is.EqualTo(pile.ResourceId));
            Assert.That(pileSnapshot.Resources.Single().Quantity, Is.EqualTo(pile.Quantity.Units));

            SelectFromAbove(root, root.DemoStoragePresenter);
            Assert.That(root.SelectionProbe.SelectedBuildingPresenter, Is.SameAs(root.DemoStoragePresenter));
            Assert.That(overlay.TryCaptureSelected(out var storageSnapshot), Is.True);
            var canonicalStorage = root.DemoStoragePresenter.StorageInventory;
            Assert.That(storageSnapshot.Kind, Is.EqualTo(DevelopmentSelectionKind.BuildingStorage));
            Assert.That(storageSnapshot.StableId, Is.EqualTo(root.DemoStorageBuilding.Id));
            Assert.That(storageSnapshot.Capacity, Is.EqualTo(canonicalStorage.Capacity));
            Assert.That(storageSnapshot.Occupied, Is.EqualTo(canonicalStorage.TotalUnits));
            Assert.That(storageSnapshot.FreeCapacity, Is.EqualTo(canonicalStorage.Capacity - canonicalStorage.TotalUnits));
            foreach (var amount in storageSnapshot.Resources)
                Assert.That(amount.Quantity, Is.EqualTo(canonicalStorage.GetAmount(amount.ResourceId).Units));
            root.AiRuntime.SetPaused(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerSelectionShowsCharacterActionTargetInventoryAndClaim()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var timeout = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < timeout && root.HaulClaims.Count == 0) yield return null;
            Assert.That(root.HaulClaims.Count, Is.GreaterThan(0), "No haul claim became observable for debug selection.");
            root.AiRuntime.SetPaused(true);
            var claim = root.HaulClaims.CaptureActive().First();
            var presenter = root.DemoPresenters.Single(x => x.CharacterId == claim.Claimant);
            SelectFromAbove(root, presenter);
            Assert.That(root.SelectionProbe.SelectedCharacterId, Is.EqualTo(claim.Claimant));
            var overlay = Object.FindAnyObjectByType<Tier1AiDebugOverlay>();
            Assert.That(overlay.TryCaptureSelected(out var snapshot), Is.True);
            Assert.That(snapshot.Kind, Is.EqualTo(DevelopmentSelectionKind.Character));
            Assert.That(snapshot.StableId, Is.EqualTo(claim.Claimant));
            Assert.That(snapshot.CurrentAction, Is.EqualTo(root.AiAgents.TryGet(claim.Claimant, out var agent)
                ? agent.CurrentAction : (Game.Domain.AI.UtilityActionKind?)null));
            Assert.That(snapshot.Source, Is.EqualTo(claim.Source));
            Assert.That(snapshot.Destination, Is.EqualTo(claim.Destination));
            Assert.That(snapshot.Target.HasValue, Is.True);
            Assert.That(snapshot.ActiveClaims.Single().Id, Is.EqualTo(claim.Id));
            Assert.That(snapshot.ActiveClaims.Single().Claimant, Is.EqualTo(claim.Claimant));
            var canonicalInventory = root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, claim.Claimant));
            foreach (var amount in snapshot.Resources)
                Assert.That(amount.Quantity, Is.EqualTo(canonicalInventory.GetAmount(amount.ResourceId).Units));
            root.AiRuntime.SetPaused(false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActivePauseFreezesSimulationAndMovementWhileCameraRemainsIndependent()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var timeout = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < timeout &&
                   root.DemoPresenters.All(x => x.GetComponent<NavMeshAgent>().velocity.sqrMagnitude < 0.01f))
                yield return null;
            root.AiRuntime.SetPaused(true);
            var ticks = root.Clock.State.CalendarTicks;
            var positions = root.DemoPresenters.Select(x => x.transform.position).ToArray();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(root.Clock.State.CalendarTicks, Is.EqualTo(ticks));
            for (var i = 0; i < positions.Length; i++)
                Assert.That(Vector3.Distance(positions[i], root.DemoPresenters[i].transform.position), Is.LessThan(0.02f));
            Assert.That(root.CameraController.enabled, Is.True);
            root.AiRuntime.SetPaused(false);
        }

        private static long Total(LocalSceneCompositionRoot root) => root.Inventories.All.Sum(x => x.TotalUnits);

        private static long TotalForKind(LocalSceneCompositionRoot root, InventoryOwnerKind kind) =>
            root.Inventories.All.Where(x => x.Owner.Kind == kind).Sum(x => x.TotalUnits);

        private static void SelectFromAbove(LocalSceneCompositionRoot root, Component target)
        {
            var collider = target.GetComponentInChildren<Collider>(true);
            Assert.That(collider, Is.Not.Null, "Selectable presenter requires a collider in the existing raycast pipeline.");
            var center = collider.bounds.center;
            var screenPoint = PointerSelectionTestPoint.AimCameraAtUnblockedPoint(root, center);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));
            Assert.That(root.SelectionProbe.TrySelectAt(screenPoint), Is.True);
        }

        private static void Capture(Camera camera)
        {
            camera.transform.position = new Vector3(-2f, 14f, -18f);
            camera.transform.LookAt(new Vector3(-1f, 0.5f, 1f));
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/U10-utility-ai.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
