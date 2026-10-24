using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using Game.Simulation.Resources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("U09")]
    public sealed class ResourceGameplayPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocalScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGameplayCreatesCanonicalPilesAndCharacterInventories()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root.DemoPilePresenters.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var pile in root.DemoPilePresenters)
                Assert.That(root.Inventories.Get(new InventoryOwner(InventoryOwnerKind.WorldPile, pile.PileId))
                    .GetAmount(pile.ResourceId), Is.EqualTo(pile.Quantity));
            Assert.That(root.Inventories.All.Count(x => x.Owner.Kind == InventoryOwnerKind.Character), Is.Zero);
            Assert.That(root.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.CarryLoad)
                .Select(x => x.Owner.Id), Is.EquivalentTo(root.PlayerCharacterIds));
            Assert.That(root.DemoPresenters.Count, Is.EqualTo(2));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                CaptureRuntimeValidation(root.PointerRaycaster.WorldCamera);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShelterConfirmConsumesExactRealResources()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            root.AiRuntime.SetPaused(true);
            CompleteTechnology(root, "shelter_construction");
            var beforeBuildings = root.Buildings.Count;
            var wood = new ResourceId("wood_log");
            var fiber = new ResourceId("plant_fiber");
            var beforeWood = Total(root, wood);
            var beforeFiber = Total(root, fiber);
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(new Vector3(20f, 0f, 20f)), Is.True);
            var building = controller.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Planned));
            Assert.That(Total(root, wood), Is.EqualTo(beforeWood));
            Assert.That(Total(root, fiber), Is.EqualTo(beforeFiber));
            var definition = root.BuildingCatalog.ResolveDefinition(building.DefinitionId);
            var carry = new InventoryOwner(InventoryOwnerKind.CarryLoad, root.MaleDemoCharacter.Id);
            // Exercise the canonical pickup, transport buffer and construction delivery transactions.
            // Planning itself must not consume or teleport any material.
            foreach (var cost in definition.ConstructionCost)
            {
                var remaining = cost.Quantity.Units;
                foreach (var source in root.Inventories.All.Where(x => x.Owner.Kind == InventoryOwnerKind.WorldPile ||
                             x.Owner.Kind == InventoryOwnerKind.BuildingStorage).ToArray())
                {
                    while (remaining > 0 && source.GetAmount(cost.ResourceId).Units > 0)
                    {
                        var amount = new ResourceQuantity(System.Math.Min(remaining,
                            System.Math.Min(source.GetAmount(cost.ResourceId).Units, root.Inventories.Get(carry).Capacity)));
                        Assert.That(root.ResourceTransfers.TransferExact(source.Owner, carry, cost.ResourceId, amount),
                            Is.EqualTo(TransferFailureReason.None));
                        Assert.That(root.ResourceTransfers.TransferExact(carry, building.ConstructionBufferOwner, cost.ResourceId, amount),
                            Is.EqualTo(TransferFailureReason.None));
                        root.ConstructionSites.NotifyDelivery(building);
                        remaining -= amount.Units;
                    }
                }
                Assert.That(remaining, Is.Zero, cost.ResourceId.ToString());
            }
            building.AdvanceConstruction(System.TimeSpan.FromTicks(building.ConstructionRequiredTicks));
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(Total(root, wood), Is.EqualTo(beforeWood - definition.ConstructionCost.Single(x => x.ResourceId == wood).Quantity.Units));
            Assert.That(Total(root, fiber), Is.EqualTo(beforeFiber - definition.ConstructionCost.Single(x => x.ResourceId == fiber).Quantity.Units));
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnfundedConfirmLeavesRegistryOccupancyAndResourcesUnchanged()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "shelter_construction");
            var wood = new ResourceId("wood_log");
            foreach (var inventory in root.Inventories.All)
            {
                var amount = inventory.GetAmount(wood);
                if (amount.IsPositive) inventory.TryRemove(wood, amount);
            }
            var controller = root.BuildingPlacementController;
            var beforeBuildings = root.Buildings.Count;
            var beforeCells = root.BuildingOccupancy.OccupiedCellCount;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(new Vector3(24f, 0f, 24f)), Is.False);
            Assert.That(controller.ConfirmPlacement(), Is.Null);
            Assert.That(root.Buildings.Count, Is.EqualTo(beforeBuildings));
            Assert.That(root.BuildingOccupancy.OccupiedCellCount, Is.EqualTo(beforeCells));
            Assert.That(Total(root, wood), Is.Zero);
            controller.CancelPlacement();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StorageBasketKeepsInventoryAcrossViewRespawn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            CompleteTechnology(root, "stone_tools");
            CompleteTechnology(root, "woodworking");
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("storage_basket"));
            controller.TryMovePreviewToWorldPoint(new Vector3(16f, 0f, 16f));
            var building = controller.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            var storage = root.Inventories.Get(owner);
            Assert.That(controller.LastPlacedPresenter.StorageInventory, Is.SameAs(storage));
            root.BuildingSpawner.Despawn(controller.LastPlacedPresenter);
            yield return null;
            var respawned = root.BuildingSpawner.Spawn(building);
            Assert.That(respawned.StorageInventory, Is.SameAs(storage));
            Assert.That(respawned.BuildingId, Is.EqualTo(building.Id));
            yield return null;
        }

        private static long Total(LocalSceneCompositionRoot root, ResourceId id) =>
            root.Inventories.All.Sum(x => x.GetAmount(id).Units);

        private static void CompleteTechnology(LocalSceneCompositionRoot root, string id)
        {
            var technologyId = new Game.Domain.Research.TechnologyId(id);
            if (root.Research.IsCompleted(technologyId)) return;
            Assert.That(root.Research.Start(technologyId), Is.True);
            Assert.That(root.Research.TryAssignWorker(root.MaleDemoCharacter.Id) ||
                root.Research.IsAssigned(root.MaleDemoCharacter.Id), Is.True);
            root.Research.AdvanceWorkerTo(root.MaleDemoCharacter.Id,
                root.Research.LastProcessedTick + root.Technologies.Get(technologyId).ResearchWorkRequiredTicks, true);
            Assert.That(root.Research.IsCompleted(technologyId), Is.True);
        }

        private static void CaptureRuntimeValidation(Camera camera)
        {
            camera.transform.position = new Vector3(-3f, 10f, -14f);
            camera.transform.LookAt(new Vector3(-3f, 0.5f, 0f));
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/U09-runtime-resources.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }
        }
    }
}
