using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Resources;
using Game.Infrastructure;
using Game.Presentation.AI;
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
                yield return null;
            }
            Assert.That(moved, Is.True, "The Tier 1 presenter never traversed the NavMesh.");
            Assert.That(sawMoving, Is.True, "The U07 Moving parameter was never driven by navigation velocity.");
            Assert.That(storage.TotalUnits, Is.GreaterThan(0), "No canonical resource reached the Storage Basket.");
            Assert.That(Total(root), Is.EqualTo(initialTotal), "Hauling must conserve world + character + storage units.");
            Assert.That(root.HaulClaims.Count, Is.LessThanOrEqualTo(root.AiAgents.Count));
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(root.PointerRaycaster.WorldCamera);
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
