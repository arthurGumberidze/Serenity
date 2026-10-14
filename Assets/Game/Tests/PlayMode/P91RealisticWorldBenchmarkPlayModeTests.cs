using System.Collections;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("P91")]
    public sealed class P91RealisticWorldBenchmarkPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OfficialTerrainLoadsAsTheNormalGameplayProjectionWithoutOwningSimulationState()
        {
            var region = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(region, Is.Not.Null);
            Assert.DoesNotThrow(region.ValidateConfiguration);
            Assert.That(region.enabled, Is.True);
            Assert.That(region.GetComponents<MonoBehaviour>(), Has.Length.EqualTo(1));
            Assert.That(region.Terrain.gameObject.activeInHierarchy, Is.True);
            Assert.That(Object.FindAnyObjectByType<P91RealisticWorldBenchmark>(), Is.Null);
            yield return null;
        }
    }
}
