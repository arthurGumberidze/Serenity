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
        public IEnumerator BenchmarkLoadsInNormalGameplayWithoutOwningSimulationState()
        {
            var benchmark = Object.FindAnyObjectByType<P91RealisticWorldBenchmark>();
            Assert.That(benchmark, Is.Not.Null);
            Assert.DoesNotThrow(benchmark.ValidateConfiguration);
            Assert.That(benchmark.enabled, Is.True);
            Assert.That(benchmark.GetComponents<MonoBehaviour>(), Has.Length.EqualTo(1));
            Assert.That(benchmark.Terrain.gameObject.activeInHierarchy, Is.True);
            yield return null;
        }
    }
}
