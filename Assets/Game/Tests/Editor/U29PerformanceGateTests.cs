using System;
using System.Collections.Generic;
using System.Linq;
using Game.Infrastructure.Performance;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U29")]
    public sealed class U29PerformanceGateTests
    {
        [Test]
        public void ConfigRejectsInvalidPopulationAndTierMix()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => U29BenchmarkConfig.GetTierMix(99, out _, out _, out _));
            var config = Config();
            config.tier1Count++;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Test]
        public void NativeProfilerCaptureCannotQualifyAsAcceptanceGate()
        {
            var config = Config();
            config.captureProfilerTrace = true;
            config.detailedDiagnostics = true;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.mandatoryGate = false;
            Assert.DoesNotThrow(() => config.Validate());
            config.detailedDiagnostics = false;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [TestCase(100, 25, 50, 25)]
        [TestCase(300, 75, 150, 75)]
        [TestCase(500, 125, 250, 125)]
        [TestCase(1000, 250, 500, 250)]
        public void PopulationMatrixUsesExactQuarterHalfQuarter(int population, int tier1, int tier2, int tier3)
        {
            U29BenchmarkConfig.GetTierMix(population, out var actual1, out var actual2, out var actual3);
            Assert.That((actual1, actual2, actual3), Is.EqualTo((tier1, tier2, tier3)));
        }

        [Test]
        public void GeneratedCharactersUseUniqueDeterministicStableIds()
        {
            var ids = Enumerable.Range(0, 1000).Select(x => U29BenchmarkScenario.DeterministicCharacterId(77, x)).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(1000));
            Assert.That(ids, Is.EqualTo(Enumerable.Range(0, 1000)
                .Select(x => U29BenchmarkScenario.DeterministicCharacterId(77, x))));
        }

        [Test]
        public void PercentilesUseSortedLinearInterpolationAtBoundaries()
        {
            Assert.Throws<ArgumentException>(() => U29BenchmarkStatistics.Percentile(Array.Empty<double>(), 50));
            Assert.That(U29BenchmarkStatistics.Percentile(new[] { 7d }, 95), Is.EqualTo(7d));
            Assert.That(U29BenchmarkStatistics.Percentile(new[] { 4d, 1d, 3d, 2d }, 0), Is.EqualTo(1d));
            Assert.That(U29BenchmarkStatistics.Percentile(new[] { 4d, 1d, 3d, 2d }, 50), Is.EqualTo(2.5d));
            Assert.That(U29BenchmarkStatistics.Percentile(new[] { 4d, 1d, 3d, 2d }, 100), Is.EqualTo(4d));
        }

        [Test]
        public void WorstRepeatSelectsActualLargestP95()
        {
            var values = new[] { Result(1, 15), Result(2, 28), Result(3, 19) };
            Assert.That(U29BenchmarkStatistics.WorstRepeat(values).repeatIndex, Is.EqualTo(2));
        }

        [Test]
        public void ValidatorRejectsShortWarmup()
        {
            var config = Config();
            var result = ValidResult(config);
            result.warmupSeconds = 59.99;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true), Does.Contain("short_warmup"));
        }

        [Test]
        public void ValidatorRejectsShortMeasurementWindow()
        {
            var config = Config();
            var result = ValidResult(config);
            result.measurementSeconds = 299.99;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true), Does.Contain("short_measurement"));
        }

        [Test]
        public void ValidatorRejectsFewerThanThreeRepeats()
        {
            var config = Config();
            var result = ValidResult(config);
            result.repeatCount = 2;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true), Does.Contain("insufficient_repeats"));
        }

        [Test]
        public void ValidatorRejectsHeadlessOrUiDisabledMandatoryResult()
        {
            var config = Config();
            var result = ValidResult(config);
            result.batchMode = true;
            result.graphicsDeviceActive = false;
            result.uiEnabled = false;
            var failures = U29BenchmarkValidator.Validate(config, result, true);
            Assert.That(failures, Does.Contain("headless_or_missing_graphics"));
            Assert.That(failures, Does.Contain("ui_disabled"));
        }

        [Test]
        public void ValidatorRejectsUnfocusedMandatorySamples()
        {
            var config = Config();
            var result = ValidResult(config);
            result.frame.focusedSampleCount--;
            result.frame.unfocusedSampleCount = 1;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true),
                Does.Contain("unfocused_or_background_samples"));
        }

        [Test]
        public void ValidatorRejectsP95AboveHardGate()
        {
            var config = Config();
            var result = ValidResult(config);
            result.frame.p95Ms = 33.331;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true), Does.Contain("p95_threshold_breach"));
        }

        [Test]
        public void ValidatorRejectsWrongTierCount()
        {
            var config = Config();
            var result = ValidResult(config);
            result.countsEnd.tier2--;
            Assert.That(U29BenchmarkValidator.Validate(config, result, true),
                Does.Contain("actual_population_or_representation_mismatch"));
        }

        [Test]
        public void ResultJsonRoundTripPreservesEvidenceExactly()
        {
            var expected = ValidResult(Config());
            expected.rawFramesSha256 = new string('a', 64);
            expected.operations.canonicalStateHash = 0xFEDCBA9876543210UL;
            var actual = U29BenchmarkJson.Deserialize<U29BenchmarkResult>(U29BenchmarkJson.Serialize(expected));
            Assert.That(actual.rawFramesSha256, Is.EqualTo(expected.rawFramesSha256));
            Assert.That(actual.frame.p95Ms, Is.EqualTo(expected.frame.p95Ms));
            Assert.That(actual.operations.canonicalStateHash, Is.EqualTo(expected.operations.canonicalStateHash));
        }

        [Test]
        public void MemoryTrendDetectsSyntheticRunawayGrowth()
        {
            var mb = 1024L * 1024L;
            Assert.That(U29BenchmarkStatistics.IsRunawayGrowth(100 * mb, 130 * mb, 170 * mb, 32 * mb), Is.True);
        }

        [Test]
        public void MemoryTrendAcceptsBoundedNoisyPlateau()
        {
            var mb = 1024L * 1024L;
            Assert.That(U29BenchmarkStatistics.IsRunawayGrowth(100 * mb, 125 * mb, 110 * mb, 32 * mb), Is.False);
        }

        [Test]
        public void BenchmarkRequiresExplicitCommandLineFlag()
        {
            Assert.That(U29BenchmarkConfig.IsRequested(new[] { "Serenity.exe" }), Is.False);
            Assert.That(U29BenchmarkConfig.IsRequested(new[] { "Serenity.exe", "-u29Benchmark" }), Is.True);
        }

        [Test]
        public void ScenarioIdentityGenerationIsEnumerationOrderIndependent()
        {
            var ascending = Enumerable.Range(0, 1000).Select(x => U29BenchmarkScenario.DeterministicCharacterId(991, x))
                .OrderBy(x => x).ToArray();
            var descending = Enumerable.Range(0, 1000).Reverse().Select(x => U29BenchmarkScenario.DeterministicCharacterId(991, x))
                .OrderBy(x => x).ToArray();
            Assert.That(descending, Is.EqualTo(ascending));
        }

        [Test]
        public void IdentityTierObservationContainsOneThousandUniqueAssignments()
        {
            var values = new List<KeyValuePair<Game.Domain.StableEntityId, Game.Domain.Characters.CharacterSimulationTier>>();
            for (var i = 0; i < 1000; i++) values.Add(new KeyValuePair<Game.Domain.StableEntityId,
                Game.Domain.Characters.CharacterSimulationTier>(U29BenchmarkScenario.DeterministicCharacterId(5, i),
                i < 250 ? Game.Domain.Characters.CharacterSimulationTier.Tier1 : i < 750
                    ? Game.Domain.Characters.CharacterSimulationTier.Tier2 : Game.Domain.Characters.CharacterSimulationTier.Tier3));
            var text = U29BenchmarkScenario.SerializeIdentityTiers(values);
            Assert.That(text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length, Is.EqualTo(1000));
            Assert.That(values.Select(x => x.Key).Distinct().Count(), Is.EqualTo(1000));
        }

        [Test]
        public void AggregateValidatorRejectsDuplicateAndMismatchedRepeats()
        {
            var config = Config();
            var repeats = new[] { ValidResult(config), ValidResult(config), ValidResult(config) };
            repeats[0].repeatIndex = 1;
            repeats[1].repeatIndex = 1;
            repeats[2].repeatIndex = 3;
            Assert.That(U29BenchmarkValidator.ValidateAggregate(repeats), Does.Contain("duplicate_repeat_index"));
            repeats[1].repeatIndex = 2;
            repeats[2].hardware.graphicsDevice = "different";
            Assert.That(U29BenchmarkValidator.ValidateAggregate(repeats), Does.Contain("hardware_or_config_mismatch"));
        }

        private static U29BenchmarkConfig Config()
        {
            U29BenchmarkConfig.GetTierMix(1000, out var t1, out var t2, out var t3);
            return new U29BenchmarkConfig
            {
                configId = "u29-test",
                baseCommit = "b90fafd",
                outputDirectory = PathForTest(),
                population = 1000,
                tier1Count = t1,
                tier2Count = t2,
                tier3Count = t3,
                repeatIndex = 1
            };
        }

        private static string PathForTest() => System.IO.Path.GetFullPath(System.IO.Path.Combine("Temp", "U29Tests"));
        private static U29BenchmarkResult Result(int repeat, double p95) => new U29BenchmarkResult
        {
            repeatIndex = repeat,
            frame = new U29FrameMetrics { p95Ms = p95 }
        };

        private static U29BenchmarkResult ValidResult(U29BenchmarkConfig config)
        {
            var counts = new U29RuntimeCounts
            {
                canonicalCharacters = config.population,
                distinctStableIds = config.population,
                tier1 = config.tier1Count,
                tier2 = config.tier2Count,
                tier3 = config.tier3Count,
                tier1Presenters = config.tier1Count,
                tier2Entities = config.tier2Count,
                tier3Records = config.tier3Count,
                uiSubscriptions = 4
            };
            return new U29BenchmarkResult
            {
                schemaVersion = config.schemaVersion,
                harnessVersion = config.harnessVersion,
                configId = config.configId,
                buildGuid = "build",
                population = config.population,
                requestedTier1 = config.tier1Count,
                requestedTier2 = config.tier2Count,
                requestedTier3 = config.tier3Count,
                repeatIndex = 1,
                repeatCount = 3,
                graphicsDeviceActive = true,
                uiEnabled = true,
                cameraActive = true,
                warmupSeconds = 60,
                measurementSeconds = 300,
                rawFramesSha256 = "hash",
                visualEvidenceFile = "visual-evidence.png",
                visualEvidenceSha256 = "visual-hash",
                frame = new U29FrameMetrics { sampleCount = 18000, focusedSampleCount = 18000,
                    p50Ms = 16, p95Ms = 20, p99Ms = 25, maximumMs = 30 },
                memory = new U29MemoryMetrics(),
                countsStart = counts,
                countsEnd = counts,
                operations = new U29OperationMetrics(),
                hardware = new U29HardwareProfile { processor = "cpu", graphicsDevice = "gpu", operatingSystem = "os" },
                save = new U29SaveObservation { exactIdentityTierRoundTrip = true, exactCanonicalStateRoundTrip = true },
                passed = true,
                failureReasons = Array.Empty<string>()
            };
        }
    }
}
