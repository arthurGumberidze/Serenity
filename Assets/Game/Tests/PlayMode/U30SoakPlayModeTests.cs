using System;
using System.Collections;
using Game.Infrastructure;
using Game.Infrastructure.Performance;
using Game.Infrastructure.Reliability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U30")]
    public sealed class U30SoakPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalGameplayDoesNotAttachSoakController()
        {
            Assert.That(Root().SoakValidation, Is.Null);
            Assert.That(Object.FindAnyObjectByType<U30SoakController>(), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepresentativePopulationAdvancesWithAllMonitoredInvariantsValid()
        {
            var root = Root();
            U29BenchmarkScenario.Create(root, SmokeConfig());
            root.AiRuntime.enabled = false;
            for (var step = 0; step < 12; step++)
                root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(1));
            Assert.That(root.Characters.Count, Is.EqualTo(100));
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(0));
            Assert.That(U30InvariantMonitor.Validate(root), Is.Empty);
            yield return null;
        }

        private static LocalSceneCompositionRoot Root()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            return root;
        }

        private static U29BenchmarkConfig SmokeConfig() => new U29BenchmarkConfig
        {
            population = 100, tier1Count = 25, tier2Count = 50, tier3Count = 25,
            repeatIndex = 1, repeatCount = 1, warmupSeconds = 1, measurementSeconds = 1,
            seed = 3030, outputDirectory = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()),
            configId = "u30-playmode", baseCommit = "u30", mandatoryGate = false
        };
    }
}
