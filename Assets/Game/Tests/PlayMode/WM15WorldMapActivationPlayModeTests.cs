using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM15")]
    public sealed class WM15WorldMapActivationPlayModeTests
    {
        private string saveDirectory;
        private string previousSaveDirectory;

        [UnitySetUp]
        public IEnumerator LoadActiveWorld()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "serenity-wm15-active-" + Guid.NewGuid().ToString("N"));
            previousSaveDirectory = Environment.GetEnvironmentVariable("SERENITY_SAVE_DIRECTORY");
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", saveDirectory);
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            yield return null;
            Environment.SetEnvironmentVariable("SERENITY_SAVE_DIRECTORY", previousSaveDirectory);
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        [Timeout(480000)]
        public IEnumerator LocalGameplayBootsTheVisibleV2WorldAtItsAuthoredStart()
        {
            var root = UnityEngine.Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.WorldDefinition.GenerationId,
                Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            Assert.That(root.WorldDefinition.MapData, Is.Not.Null);
            Assert.That(root.NewGame.StartTerritory, Is.Not.Null);
            Assert.That(root.NewGame.StartTerritory.Center.X, Is.EqualTo(root.WorldDefinition.PlayerStart.X));
            Assert.That(root.NewGame.StartTerritory.Center.Z, Is.EqualTo(root.WorldDefinition.PlayerStart.Z));
            Assert.That(root.NewGame.StartTerritory.Center.Y, Is.GreaterThan(0f));
            Assert.That(root.TiledWorld.RoadNetworkPlan, Is.Not.Null);
            Assert.That(root.WorldPopulation.PoiPlan, Is.Not.Null);
            Assert.That(root.WorldPopulation.Plan.StartTerritory, Is.Not.Null);
            Assert.That(root.TiledWorld.VisualTiles, Is.Not.Empty);
            Assert.That(root.TiledWorld.ActiveVegetationViewCount, Is.GreaterThan(0));
            Assert.That(root.CameraController.CurrentZoom, Is.EqualTo(120f).Within(0.01f));

            var expected = root.NewGame.StartTerritory.ResidentSpawns;
            for (var index = 0; index < root.PlayerCharacterIds.Count; index++)
            {
                var actual = root.Tiers.GetRuntimeState(root.PlayerCharacterIds[index]).Position;
                Assert.That(DistanceSquared(actual, expected[index]), Is.LessThan(9f),
                    "Resident " + index + " must begin within three metres of the authored V2 spawn.");
            }

            Assert.That(root.PopulatedBiomeWorld.CanopyTrees.Where(value => value != null)
                .All(value => !value.activeInHierarchy), Is.True,
                "The retired compact-world canopy must not overlap the active V2 projection.");
            yield return null;
        }

        private static float DistanceSquared(Game.Domain.AI.WorldPosition left,
            Game.Domain.AI.WorldPosition right)
        {
            var x = left.X - right.X;
            var y = left.Y - right.Y;
            var z = left.Z - right.Z;
            return x * x + y * y + z * z;
        }
    }
}
