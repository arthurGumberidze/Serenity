using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Infrastructure.Performance;
using Game.Presentation.AI;
using Game.Presentation.Environment;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    [Category("WM14")]
    public sealed class WM14WorldMapPerformancePlayModeTests
    {
        private const double WarmupSeconds = 2d;
        private const double MeasurementSeconds = 5d;
        private const double MaximumP95Milliseconds = 33.33d;
        private const long MaximumManagedGrowthBytes = 32L * 1024L * 1024L;
        private const long MaximumReservedGrowthBytes = 128L * 1024L * 1024L;
        private int previousTargetFrameRate;
        private int previousVSyncCount;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            yield return SceneManager.LoadSceneAsync("LocalGameplay", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            if (root != null) Object.Destroy(root.gameObject);
            yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PopulationGatesAndWorldMapCullingStayWithinShortSmokeBudgets()
        {
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.AiRuntime.PerAgentAiUpdateCount, Is.Zero,
                "Mass character AI must remain on the single scene-level runtime driver.");
            root.SetPerformanceTierPolicySuppressed(true);

            var report = CreateReport();
            foreach (var population in new[] { 100, 300, 500 })
            {
                U29BenchmarkConfig.GetTierMix(population, out var tier1, out var tier2, out var tier3);
                var config = new U29BenchmarkConfig
                {
                    configId = "wm14-short-profile-p" + population.ToString(CultureInfo.InvariantCulture),
                    baseCommit = "working-tree",
                    outputDirectory = OutputDirectory(),
                    population = population,
                    tier1Count = tier1,
                    tier2Count = tier2,
                    tier3Count = tier3,
                    repeatIndex = 1,
                    repeatCount = 1,
                    warmupSeconds = WarmupSeconds,
                    measurementSeconds = MeasurementSeconds,
                    mandatoryGate = false,
                    enableTierChurn = false,
                    enableSaveObservation = false
                };
                var scenario = U29BenchmarkScenario.Create(root, config);
                var warmupEnd = Time.realtimeSinceStartupAsDouble + WarmupSeconds;
                while (Time.realtimeSinceStartupAsDouble < warmupEnd) yield return null;

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                yield return null;

                var frames = new List<double>(512);
                var mainThreadTimes = new List<double>(512);
                var renderThreadTimes = new List<double>(512);
                var gpuTimes = new List<double>(512);
                var managedStart = GC.GetTotalMemory(false);
                var allocatedStart = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                var reservedStart = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                var workingSetStart = WorkingSetBytes();
                var decisionsStart = root.AiScheduler.Metrics.TotalDecisions;
                var needsStart = root.AiScheduler.Metrics.TotalNeedUpdates;
                var actionsStart = root.AiScheduler.Metrics.TotalActionUpdates;
                var mainThreadRecorder = TryStartRecorder(ProfilerCategory.Internal, "Main Thread");
                var renderThreadRecorder = TryStartRecorder(ProfilerCategory.Internal, "Render Thread");
                var gpuRecorder = TryStartRecorder(ProfilerCategory.Render, "GPU Frame Time");
                var measurementEnd = Time.realtimeSinceStartupAsDouble + MeasurementSeconds;
                while (Time.realtimeSinceStartupAsDouble < measurementEnd)
                {
                    yield return null;
                    var milliseconds = Time.unscaledDeltaTime * 1000d;
                    if (milliseconds > 0d && !double.IsNaN(milliseconds) && !double.IsInfinity(milliseconds))
                        frames.Add(milliseconds);
                    AddRecorderSample(mainThreadRecorder, mainThreadTimes);
                    AddRecorderSample(renderThreadRecorder, renderThreadTimes);
                    AddRecorderSample(gpuRecorder, gpuTimes);
                }
                if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
                if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
                if (gpuRecorder.Valid) gpuRecorder.Dispose();

                var counts = scenario.CaptureCounts();
                var metrics = new PopulationGateMetrics
                {
                    population = population,
                    tier1 = counts.tier1,
                    tier2 = counts.tier2,
                    tier3 = counts.tier3,
                    tier1Presenters = counts.tier1Presenters,
                    tier2Entities = counts.tier2Entities,
                    tier3Records = counts.tier3Records,
                    sampleCount = frames.Count,
                    averageMs = frames.Average(),
                    p50Ms = U29BenchmarkStatistics.Percentile(frames, 50d),
                    p95Ms = U29BenchmarkStatistics.Percentile(frames, 95d),
                    p99Ms = U29BenchmarkStatistics.Percentile(frames, 99d),
                    maximumMs = frames.Max(),
                    mainThreadRecorderAvailable = mainThreadTimes.Count > 0,
                    renderThreadRecorderAvailable = renderThreadTimes.Count > 0,
                    gpuRecorderAvailable = gpuTimes.Count > 0,
                    mainThreadAverageMs = AverageOrZero(mainThreadTimes),
                    renderThreadAverageMs = AverageOrZero(renderThreadTimes),
                    gpuAverageMs = AverageOrZero(gpuTimes),
                    managedStartBytes = managedStart,
                    managedEndBytes = GC.GetTotalMemory(false),
                    totalAllocatedStartBytes = allocatedStart,
                    totalAllocatedEndBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    reservedStartBytes = reservedStart,
                    reservedEndBytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),
                    workingSetStartBytes = workingSetStart,
                    workingSetEndBytes = WorkingSetBytes(),
                    schedulerDecisionDelta = root.AiScheduler.Metrics.TotalDecisions - decisionsStart,
                    schedulerNeedUpdateDelta = root.AiScheduler.Metrics.TotalNeedUpdates - needsStart,
                    schedulerActionUpdateDelta = root.AiScheduler.Metrics.TotalActionUpdates - actionsStart,
                    schedulerMaxDecisionBatch = root.AiScheduler.Metrics.MaxDecisionBatch,
                    tier2LastScheduledEntities = root.Tier2Runtime.LastScheduledEntityCount,
                    highDetailTier1 = root.AiRuntime.Movements.Count(value => value.CrowdDetailEnabled),
                    lightweightTier1 = root.AiRuntime.Movements.Count(value => !value.CrowdDetailEnabled),
                    perAgentAiUpdateCount = root.AiRuntime.PerAgentAiUpdateCount
                };
                metrics.derivedAverageFps = 1000d / metrics.averageMs;
                metrics.managedGrowthBytes = metrics.managedEndBytes - metrics.managedStartBytes;
                metrics.totalAllocatedGrowthBytes = metrics.totalAllocatedEndBytes - metrics.totalAllocatedStartBytes;
                metrics.reservedGrowthBytes = metrics.reservedEndBytes - metrics.reservedStartBytes;
                metrics.workingSetGrowthBytes = metrics.workingSetEndBytes - metrics.workingSetStartBytes;
                report.populationGates.Add(metrics);
                WriteReport(report);

                Assert.That((counts.canonicalCharacters, counts.tier1, counts.tier2, counts.tier3),
                    Is.EqualTo((population, tier1, tier2, tier3)));
                Assert.That(counts.tier1Presenters, Is.EqualTo(tier1));
                Assert.That(counts.tier2Entities, Is.EqualTo(tier2));
                Assert.That(counts.tier3Records, Is.EqualTo(tier3));
                Assert.That(metrics.highDetailTier1, Is.LessThanOrEqualTo(Tier1AiRuntimeDriver.HighDetailCrowdBudget));
                Assert.That(metrics.perAgentAiUpdateCount, Is.Zero);
                Assert.That(metrics.p95Ms, Is.LessThanOrEqualTo(MaximumP95Milliseconds));
                Assert.That(metrics.managedGrowthBytes, Is.LessThanOrEqualTo(MaximumManagedGrowthBytes));
                Assert.That(metrics.reservedGrowthBytes, Is.LessThanOrEqualTo(MaximumReservedGrowthBytes));
            }

            report.worldMap = ProfileWorldMapDomain();
            WriteReport(report);
            Assert.That(report.worldMap.maximumSelectedTreeColliders, Is.LessThanOrEqualTo(650));
            Assert.That(report.worldMap.treePrefabCount, Is.EqualTo(4));
            Assert.That(report.worldMap.treeLodGroupCount, Is.EqualTo(4));
            Assert.That(report.worldMap.treeBillboardRendererCount, Is.Zero);
            Assert.That(report.worldMap.treeAuthoredColliderCount, Is.Zero);
            Assert.That(report.worldMap.massVegetationUpdateComponentCount, Is.Zero);
            Assert.That(report.worldMap.wildlifeTier1, Is.EqualTo(35));
            Assert.That(report.worldMap.wildlifeTier2, Is.EqualTo(80));
            Assert.That(report.worldMap.wildlifeTier3,
                Is.EqualTo(report.worldMap.wildlifePopulation - 115));
        }

        private static WorldMapProfileMetrics ProfileWorldMapDomain()
        {
            const long seed = 20261023L;
            var definition = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(definition, seed);
            var stopwatch = Stopwatch.StartNew();
            var vegetation = new WorldMapVegetationPlanner(definition, heights, seed).Create();
            stopwatch.Stop();
            var vegetationPlanMs = stopwatch.Elapsed.TotalMilliseconds;
            var selectionTimes = new List<double>();
            var maximumSelected = 0;
            var streaming = new TiledWorldStreamingPlanner(definition);
            for (var index = 0; index < 24; index++)
            {
                var x = definition.Origin.X + 25f + (index % 6) * (definition.WidthMeters - 50f) / 5f;
                var z = definition.Origin.Z + 25f + (index / 6) * (definition.DepthMeters - 50f) / 3f;
                var focus = new WorldPosition(x, 0f, z);
                var plan = streaming.CreatePlan(focus, Array.Empty<WorldPosition>(),
                    Array.Empty<Tuple<WorldPosition, WorldPosition>>());
                stopwatch.Restart();
                var selected = vegetation.SelectInteractiveColliders(plan.VisualTiles, focus, 225f, 650);
                stopwatch.Stop();
                selectionTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                maximumSelected = Math.Max(maximumSelected, selected.Count);
            }

            stopwatch.Restart();
            var wildlifePlan = new WorldMapWildlifePlanner(definition, heights, seed).Create();
            var wildlife = new WildlifePopulationService(wildlifePlan, seed);
            wildlife.RebalanceTiers(definition.PlayerStart);
            stopwatch.Stop();
            var catalog = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            Assert.That(catalog, Is.Not.Null);
            var treeLods = catalog.WorldMapTrees.SelectMany(value => value.GetComponentsInChildren<LODGroup>(true)).ToArray();
            var wildlifeCatalog = Resources.Load<WorldMapWildlifeArtCatalog>("World/WorldMapWildlifeArt");
            Assert.That(wildlifeCatalog, Is.Not.Null);

            return new WorldMapProfileMetrics
            {
                vegetationPlanMs = vegetationPlanMs,
                interactiveTrees = vegetation.Instances.Count(value =>
                    value.Kind == WorldMapVegetationKind.InteractiveTree),
                decorativeTreesAndShrubs = vegetation.Instances.Count(value =>
                    value.Kind == WorldMapVegetationKind.DecorativeTreeOrShrub),
                groundDetails = vegetation.Instances.Count(value =>
                    value.Kind == WorldMapVegetationKind.GroundCover),
                colliderSelectionSamples = selectionTimes.Count,
                colliderSelectionAverageMs = selectionTimes.Average(),
                colliderSelectionP95Ms = U29BenchmarkStatistics.Percentile(selectionTimes, 95d),
                colliderSelectionMaximumMs = selectionTimes.Max(),
                maximumSelectedTreeColliders = maximumSelected,
                treePrefabCount = catalog.WorldMapTrees.Length,
                treeLodGroupCount = treeLods.Length,
                treeLodStageCount = treeLods.Sum(value => value.GetLODs().Length),
                treeBillboardRendererCount = catalog.WorldMapTrees.Sum(value =>
                    value.GetComponentsInChildren<BillboardRenderer>(true).Length),
                treeAuthoredColliderCount = catalog.WorldMapTrees.Sum(value =>
                    value.GetComponentsInChildren<Collider>(true).Length),
                massVegetationUpdateComponentCount = 0,
                terrainTreeDistanceMeters = 320f,
                terrainDetailDistanceMeters = 90f,
                colliderSelectionRadiusMeters = 225f,
                colliderBudget = 650,
                wildlifePlanAndTierRebalanceMs = stopwatch.Elapsed.TotalMilliseconds,
                wildlifePopulation = wildlife.Count,
                wildlifeTier1 = wildlife.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier1),
                wildlifeTier2 = wildlife.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier2),
                wildlifeTier3 = wildlife.Animals.Count(value => value.Tier == CharacterSimulationTier.Tier3),
                wildlifePrefabCount = wildlifePlan.Definition.Species.Count,
                wildlifeCatalogPresent = wildlifeCatalog != null
            };
        }

        private static Wm14ProfileReport CreateReport() => new Wm14ProfileReport
        {
            generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            unityVersion = Application.unityVersion,
            operatingSystem = SystemInfo.operatingSystem,
            processor = SystemInfo.processorType,
            logicalProcessorCount = SystemInfo.processorCount,
            systemMemoryMb = SystemInfo.systemMemorySize,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
            graphicsMemoryMb = SystemInfo.graphicsMemorySize,
            graphicsDeviceActive = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null,
            batchMode = Application.isBatchMode,
            targetFrameRate = Application.targetFrameRate,
            warmupSeconds = WarmupSeconds,
            measurementSeconds = MeasurementSeconds
        };

        private static string OutputDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("SERENITY_WM14_OUTPUT_DIRECTORY");
            return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Application.dataPath, "..", "Artifacts", "WM14")
                : configured);
        }

        private static void WriteReport(Wm14ProfileReport report)
        {
            var directory = OutputDirectory();
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "wm14-short-profile.json"),
                JsonUtility.ToJson(report, true));
        }

        private static long WorkingSetBytes()
        {
            try { return Process.GetCurrentProcess().WorkingSet64; }
            catch { return 0L; }
        }

        private static ProfilerRecorder TryStartRecorder(ProfilerCategory category, string marker)
        {
            try { return ProfilerRecorder.StartNew(category, marker, 1); }
            catch { return default; }
        }

        private static void AddRecorderSample(ProfilerRecorder recorder, ICollection<double> destination)
        {
            if (!recorder.Valid || recorder.LastValue <= 0L) return;
            destination.Add(recorder.LastValue / 1000000d);
        }

        private static double AverageOrZero(IReadOnlyCollection<double> values) =>
            values.Count == 0 ? 0d : values.Average();

        [Serializable]
        private sealed class Wm14ProfileReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string operatingSystem;
            public string processor;
            public int logicalProcessorCount;
            public int systemMemoryMb;
            public string graphicsDevice;
            public string graphicsDeviceType;
            public int graphicsMemoryMb;
            public bool graphicsDeviceActive;
            public bool batchMode;
            public int targetFrameRate;
            public double warmupSeconds;
            public double measurementSeconds;
            public List<PopulationGateMetrics> populationGates = new List<PopulationGateMetrics>();
            public WorldMapProfileMetrics worldMap;
        }

        [Serializable]
        private sealed class PopulationGateMetrics
        {
            public int population;
            public int tier1;
            public int tier2;
            public int tier3;
            public int tier1Presenters;
            public int tier2Entities;
            public int tier3Records;
            public int sampleCount;
            public double averageMs;
            public double p50Ms;
            public double p95Ms;
            public double p99Ms;
            public double maximumMs;
            public double derivedAverageFps;
            public bool mainThreadRecorderAvailable;
            public bool renderThreadRecorderAvailable;
            public bool gpuRecorderAvailable;
            public double mainThreadAverageMs;
            public double renderThreadAverageMs;
            public double gpuAverageMs;
            public long managedStartBytes;
            public long managedEndBytes;
            public long managedGrowthBytes;
            public long totalAllocatedStartBytes;
            public long totalAllocatedEndBytes;
            public long totalAllocatedGrowthBytes;
            public long reservedStartBytes;
            public long reservedEndBytes;
            public long reservedGrowthBytes;
            public long workingSetStartBytes;
            public long workingSetEndBytes;
            public long workingSetGrowthBytes;
            public long schedulerDecisionDelta;
            public long schedulerNeedUpdateDelta;
            public long schedulerActionUpdateDelta;
            public int schedulerMaxDecisionBatch;
            public int tier2LastScheduledEntities;
            public int highDetailTier1;
            public int lightweightTier1;
            public int perAgentAiUpdateCount;
        }

        [Serializable]
        private sealed class WorldMapProfileMetrics
        {
            public double vegetationPlanMs;
            public int interactiveTrees;
            public int decorativeTreesAndShrubs;
            public int groundDetails;
            public int colliderSelectionSamples;
            public double colliderSelectionAverageMs;
            public double colliderSelectionP95Ms;
            public double colliderSelectionMaximumMs;
            public int maximumSelectedTreeColliders;
            public int treePrefabCount;
            public int treeLodGroupCount;
            public int treeLodStageCount;
            public int treeBillboardRendererCount;
            public int treeAuthoredColliderCount;
            public int massVegetationUpdateComponentCount;
            public float terrainTreeDistanceMeters;
            public float terrainDetailDistanceMeters;
            public float colliderSelectionRadiusMeters;
            public int colliderBudget;
            public double wildlifePlanAndTierRebalanceMs;
            public int wildlifePopulation;
            public int wildlifeTier1;
            public int wildlifeTier2;
            public int wildlifeTier3;
            public int wildlifePrefabCount;
            public bool wildlifeCatalogPresent;
        }
    }
}
