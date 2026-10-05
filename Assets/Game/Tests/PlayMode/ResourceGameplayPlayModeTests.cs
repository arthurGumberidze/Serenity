using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Presentation.Buildings;
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
            Assert.That(root.DemoPilePresenters.Count, Is.EqualTo(4));
            Assert.That(root.Inventories.All.Count(x => x.Owner.Kind == InventoryOwnerKind.WorldPile), Is.EqualTo(4));
            Assert.That(root.Inventories.All.Count(x => x.Owner.Kind == InventoryOwnerKind.Character), Is.EqualTo(2));
            Assert.That(root.DemoPilePresenters.Sum(x => x.Quantity.Units), Is.EqualTo(65));
            Assert.That(root.DemoPresenters.Count, Is.EqualTo(2));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                CaptureRuntimeValidation(root.PointerRaycaster.WorldCamera);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShelterConfirmConsumesExactRealResources()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var wood = new ResourceId("wood_log");
            var fiber = new ResourceId("plant_fiber");
            var beforeWood = Total(root, wood);
            var beforeFiber = Total(root, fiber);
            var controller = root.BuildingPlacementController;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(new Vector3(20f, 0f, 20f)), Is.True);
            var building = controller.ConfirmPlacement();
            Assert.That(building, Is.Not.Null);
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(Total(root, wood), Is.EqualTo(beforeWood - 8));
            Assert.That(Total(root, fiber), Is.EqualTo(beforeFiber - 3));
            Assert.That(root.Buildings.Count, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnfundedConfirmLeavesRegistryOccupancyAndResourcesUnchanged()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var wood = new ResourceId("wood_log");
            foreach (var inventory in root.Inventories.All)
            {
                var amount = inventory.GetAmount(wood);
                if (amount.IsPositive) inventory.TryRemove(wood, amount);
            }
            var controller = root.BuildingPlacementController;
            var beforeCells = root.BuildingOccupancy.OccupiedCellCount;
            controller.StartPlacement(new BuildingDefinitionId("primitive_shelter"));
            Assert.That(controller.TryMovePreviewToWorldPoint(new Vector3(24f, 0f, 24f)), Is.False);
            Assert.That(controller.ConfirmPlacement(), Is.Null);
            Assert.That(root.Buildings.Count, Is.Zero);
            Assert.That(root.BuildingOccupancy.OccupiedCellCount, Is.EqualTo(beforeCells));
            Assert.That(Total(root, wood), Is.Zero);
            controller.CancelPlacement();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StorageBasketKeepsInventoryAcrossViewRespawn()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
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
