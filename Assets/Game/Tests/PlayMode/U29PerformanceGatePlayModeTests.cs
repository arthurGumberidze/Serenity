using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain.Characters;
using Game.Infrastructure;
using Game.Infrastructure.Performance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("U29")]
    public sealed class U29PerformanceGatePlayModeTests
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
        public IEnumerator BenchmarkActivationRequiresExplicitFlag()
        {
            Assert.That(U29BenchmarkConfig.IsRequested(new[] { "Serenity.exe" }), Is.False);
            Assert.That(U29BenchmarkConfig.IsRequested(new[] { "Serenity.exe", "-u29Benchmark" }), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalLocalGameplayDoesNotAttachBenchmarkController()
        {
            Assert.That(Root().PerformanceBenchmark, Is.Null);
            Assert.That(Object.FindAnyObjectByType<U29BenchmarkController>(), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SmokeOneHundredBuildsExactTwentyFiveFiftyTwentyFiveRealTiers()
        {
            var root = Root();
            var scenario = U29BenchmarkScenario.Create(root, SmokeConfig());
            var counts = scenario.CaptureCounts();
            Assert.That((counts.canonicalCharacters, counts.tier1, counts.tier2, counts.tier3),
                Is.EqualTo((100, 25, 50, 25)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierOnePresenterCountMatchesTierOnePopulation()
        {
            var scenario = U29BenchmarkScenario.Create(Root(), SmokeConfig());
            var counts = scenario.CaptureCounts();
            Assert.That(counts.tier1Presenters, Is.EqualTo(counts.tier1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovingTierOnePresenterUsesContinuousAnimatorThenFreezesIdle()
        {
            var root = Root();
            var presenter = root.DemoPresenters[0];
            root.AiRuntime.enabled = false;

            presenter.SetSimulationPaused(false);
            presenter.SetMovementSpeed(1f);
            Assert.That(presenter.Animator.enabled, Is.True,
                "A moving presenter must stay in Unity's continuous Animator loop instead of sliding on a frozen pose.");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(presenter.Animator.enabled, Is.True);

            presenter.SetMovementSpeed(0f);
            Assert.That(presenter.Animator.enabled, Is.True,
                "The locomotion-to-idle transition must be allowed to finish before animation is frozen.");
            yield return new WaitForSecondsRealtime(0.25f);
            presenter.RefreshAnimation(Time.unscaledTime);
            Assert.That(presenter.Animator.enabled, Is.False);
            root.AiRuntime.enabled = true;
        }

        [UnityTest]
        public IEnumerator TierTwoEntityCountMatchesTierTwoPopulation()
        {
            var scenario = U29BenchmarkScenario.Create(Root(), SmokeConfig());
            var counts = scenario.CaptureCounts();
            Assert.That(counts.tier2Entities, Is.EqualTo(counts.tier2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierThreeRecordsHaveNeitherPresenterNorEntity()
        {
            var root = Root();
            U29BenchmarkScenario.Create(root, SmokeConfig());
            foreach (var id in root.Tiers.ManagedIds.Where(x => root.Tiers.GetTier(x) == CharacterSimulationTier.Tier3))
            {
                Assert.That(root.CharacterPresentations.TryGet(id, out _), Is.False);
                Assert.That(root.Tier2Runtime.Materializer.TryGetEntity(id, out _), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalU27UiExistsAndIsEnabled()
        {
            var ui = Root().GameplayUi;
            Assert.That(ui, Is.Not.Null);
            Assert.That(ui.enabled, Is.True);
            Assert.That(ui.RootVisual, Is.Not.Null);
            Assert.That(ui.RootVisual.Q("top-hud"), Is.Not.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiSubscriptionCountDoesNotGrowAcrossRefreshes()
        {
            var ui = Root().GameplayUi;
            var before = ui.SubscriptionCount;
            for (var i = 0; i < 20; i++) ui.RefreshNow();
            Assert.That(ui.SubscriptionCount, Is.EqualTo(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepresentativeSimulationSystemsAdvance()
        {
            var root = Root();
            U29BenchmarkScenario.Create(root, SmokeConfig());
            var ticks = root.Clock.State.CalendarTicks;
            var decisions = root.AiScheduler.Metrics.TotalDecisions;
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromHours(2));
            Assert.That(root.Clock.State.CalendarTicks, Is.GreaterThan(ticks));
            Assert.That(root.AiScheduler.Metrics.TotalDecisions, Is.GreaterThan(decisions));
            Assert.That(root.Events.LastProcessedTick, Is.EqualTo(root.Clock.State.CalendarTicks));
            Assert.That(root.HealthFamily.LastCalendarTick, Is.EqualTo(root.Clock.State.CalendarTicks));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TierChurnPreservesPopulationIdsAndTargetMix()
        {
            var root = Root();
            var scenario = U29BenchmarkScenario.Create(root, SmokeConfig());
            var ids = root.Characters.GetAll().Select(x => x.Id).ToArray();
            scenario.ExerciseTierChurn();
            var counts = scenario.CaptureCounts();
            Assert.That(root.Characters.GetAll().Select(x => x.Id), Is.EqualTo(ids));
            Assert.That((counts.tier1, counts.tier2, counts.tier3), Is.EqualTo((25, 50, 25)));
            Assert.That(scenario.TierChurnTransitions, Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalGlobalTransitionPreservesCanonicalPopulationIdentity()
        {
            var root = Root();
            U29BenchmarkScenario.Create(root, SmokeConfig());
            var before = root.Characters.GetAll().Select(x => x.Id).ToArray();
            var location = root.GlobalMap.ActiveLocationId;
            var enter = root.ModeTransitions.EnterGlobal(root.Clock.State.CalendarTicks);
            Assert.That(enter.Status, Is.EqualTo(Game.Simulation.World.GameModeTransitionStatus.Changed));
            var leave = root.ModeTransitions.EnterLocation(location, root.Clock.State.CalendarTicks);
            Assert.That(leave.Status, Is.EqualTo(Game.Simulation.World.GameModeTransitionStatus.Changed));
            Assert.That(root.Characters.GetAll().Select(x => x.Id), Is.EqualTo(before));
            Assert.That(root.Tiers.Count, Is.EqualTo(100));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedU28AdvanceAtSameTickDoesNotReroll()
        {
            var root = Root();
            U29BenchmarkScenario.Create(root, SmokeConfig());
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromDays(3));
            var hash = root.Events.ComputeStateHash();
            root.Events.AdvanceTo(root.Clock.State.CalendarTicks);
            Assert.That(root.Events.ComputeStateHash(), Is.EqualTo(hash));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedSmokeResultCanBeStrictlyValidated()
        {
            var config = SmokeConfig();
            var scenario = U29BenchmarkScenario.Create(Root(), config);
            var counts = scenario.CaptureCounts();
            var result = SmokeResult(config, counts);
            Assert.That(U29BenchmarkValidator.Validate(config, result, true), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidSetupFailsClosedInsteadOfProducingSuccess()
        {
            var config = SmokeConfig();
            config.tier1Count++;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyF10DeveloperSurfaceRemainsSeparated()
        {
            var root = Root();
            Assert.That(root.GameplayUi.DeveloperUiVisible, Is.False);
            Assert.That(root.GetComponents<Behaviour>().Where(x => x.GetType().Name.Contains("Debug")).All(x => !x.enabled),
                Is.True);
            root.GameplayUi.SetDeveloperUiVisible(true);
            Assert.That(root.GameplayUi.DeveloperUiVisible, Is.True);
            root.GameplayUi.SetDeveloperUiVisible(false);
            Assert.That(root.GameplayUi.DeveloperUiVisible, Is.False);
            yield return null;
        }

        private static LocalSceneCompositionRoot Root() => Object.FindAnyObjectByType<LocalSceneCompositionRoot>();

        private static U29BenchmarkConfig SmokeConfig() => new U29BenchmarkConfig
        {
            configId = "u29-playmode-smoke",
            baseCommit = "test",
            outputDirectory = Path.GetFullPath(Path.Combine("Temp", "U29PlayMode")),
            population = 100,
            tier1Count = 25,
            tier2Count = 50,
            tier3Count = 25,
            repeatIndex = 1,
            repeatCount = 1,
            warmupSeconds = 0.1,
            measurementSeconds = 0.1,
            mandatoryGate = false,
            enableTierChurn = true,
            enableSaveObservation = true
        };

        private static U29BenchmarkResult SmokeResult(U29BenchmarkConfig config, U29RuntimeCounts counts) =>
            new U29BenchmarkResult
            {
                schemaVersion = config.schemaVersion,
                harnessVersion = config.harnessVersion,
                population = config.population,
                requestedTier1 = config.tier1Count,
                requestedTier2 = config.tier2Count,
                requestedTier3 = config.tier3Count,
                repeatCount = 1,
                warmupSeconds = config.warmupSeconds,
                measurementSeconds = config.measurementSeconds,
                graphicsDeviceActive = true,
                uiEnabled = true,
                cameraActive = true,
                rawFramesSha256 = "raw",
                countsStart = counts,
                countsEnd = counts,
                frame = new U29FrameMetrics { sampleCount = 1, p50Ms = 1, p95Ms = 1, p99Ms = 1, maximumMs = 1 },
                memory = new U29MemoryMetrics(),
                save = new U29SaveObservation { exactIdentityTierRoundTrip = true, exactCanonicalStateRoundTrip = true },
                failureReasons = Array.Empty<string>()
            };
    }
}
