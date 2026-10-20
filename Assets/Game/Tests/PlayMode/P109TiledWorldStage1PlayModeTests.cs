using System.Collections;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P109Stage1")]
    public sealed class P109TiledWorldStage1PlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadLocal()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalSceneUsesBoundedKilometreTilesAndPreparesFarMoveCorridor()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.WorldDefinition.Columns, Is.EqualTo(12));
            Assert.That(root.WorldDefinition.Rows, Is.EqualTo(12));
            Assert.That(root.WorldDefinition.TileSizeMeters, Is.EqualTo(1000));
            Assert.That(root.TiledWorld.LoadedVisualTileCount, Is.EqualTo(9));
            Assert.That(root.TiledWorld.LoadedCollisionTileCount, Is.EqualTo(9));
            Assert.That(root.RealisticGameplayRegion.Terrain.enabled, Is.False);
            Assert.That(root.RealisticGameplayRegion.Terrain.GetComponent<TerrainCollider>().enabled, Is.False);
            foreach (var id in root.TiledWorld.LoadedTiles)
            {
                Assert.That(root.TiledWorld.TryGetTerrain(id, out var terrain), Is.True);
                Assert.That(terrain.terrainData.size, Is.EqualTo(new Vector3(1000f,
                    Game.Domain.World.TiledWorldHeightSampler.TerrainHeightMeters, 1000f)));
                Assert.That(terrain.terrainData.heightmapResolution, Is.EqualTo(129));
            }
            Assert.That(root.CameraController.Settings.BoundsMin.x,
                Is.EqualTo(root.WorldDefinition.Origin.X + 10f));
            Assert.That(root.CameraController.Settings.BoundsMax.y,
                Is.EqualTo(root.WorldDefinition.Origin.Z + root.WorldDefinition.DepthMeters - 10f));

            var far = root.WorldDefinition.Zones.Single(value => value.Id == "northwest_settlement").Center;
            var result = root.ManualMoveCommands.Issue(new[] { root.MaleDemoCharacter.Id }, far,
                WorkPriority.Immediate);
            Assert.That(result.CreatedCount, Is.EqualTo(1));
            Assert.That(root.WorldDefinition.TryGetTile(far.X, far.Z, out var destinationTile), Is.True);
            Assert.That(root.TiledWorld.LoadedTiles, Does.Contain(destinationTile));
            Assert.That(root.TiledWorld.LoadedVisualTileCount, Is.EqualTo(9));
            Assert.That(root.TiledWorld.LoadedCollisionTileCount, Is.GreaterThan(9));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StrategicViewFramesFullWorldAndRestoresLocalStreaming()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var camera = root.PointerRaycaster.WorldCamera;
            root.GlobalMapBridge.RequestEnterGlobal();
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.True);
            Assert.That(root.TiledWorld.StrategicOverviewVisible, Is.True);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.GreaterThanOrEqualTo(6240f));
            Assert.That(camera.farClipPlane, Is.GreaterThan(camera.transform.position.y));
            Assert.That(root.TiledWorld.LoadedVisualTileCount, Is.LessThanOrEqualTo(9));

            root.GlobalMapBridge.RequestEnterLocation(root.GlobalMap.ActiveLocationId);
            yield return null;
            Assert.That(root.GlobalMapBridge.IsGlobalSceneLoaded, Is.False);
            Assert.That(root.TiledWorld.StrategicOverviewVisible, Is.False);
            Assert.That(root.TiledWorld.LoadedVisualTileCount, Is.LessThanOrEqualTo(9));
        }
    }
}
