using System.Collections;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P102")]
    public sealed class P102ConnectedWorldPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StrategicModeUsesSameTerrainCameraAndFogThenReturnsWithoutTeleport()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var world = UnityEngine.Object.FindAnyObjectByType<P102ConnectedWorldPresentation>();
            var terrainId = world.Terrain.GetEntityId();
            var playerPosition = root.InhabitableRegion.PlayerTerritoryAnchor.position;
            root.GlobalMapBridge.RequestEnterGlobal();
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<P102ConnectedWorldPresentation>().Terrain.GetEntityId(),
                Is.EqualTo(terrainId));
            Assert.That(root.PointerRaycaster.WorldCamera.orthographic, Is.True);
            Assert.That(root.ConnectedWorld.IsLocationRevealed(new WorldLocationId("enemy_settlement")), Is.False);
            root.GlobalMapBridge.RequestEnterLocation(ConnectedWorldContract.StarterSettlement);
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.InhabitableRegion.PlayerTerritoryAnchor.position, Is.EqualTo(playerPosition));
        }

        [UnityTest]
        public IEnumerator PlacementTerrainRejectsWaterAndAcceptsDrySettlementLand()
        {
            var surface = UnityEngine.Object.FindAnyObjectByType<P102WorldPlacementSurface>();
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(surface.IsSuitable(root.InhabitableRegion.PlayerTerritoryAnchor.position), Is.True);
            Assert.That(surface.IsSuitable(new Vector3(12f, 0f, 122f)), Is.False);
            Assert.That(root.PopulatedBiomeWorld.ResourceFormations.Select(x => x.StableIndex),
                Is.EqualTo(Enumerable.Range(0, 24)));
            yield return null;
        }
    }
}
