using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Infrastructure.Performance
{
    [Serializable]
    public sealed class U29BenchmarkConfig
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentHarnessVersion = 1;
        public const double MandatoryWarmupSeconds = 60d;
        public const double MandatoryMeasurementSeconds = 300d;
        public const int MandatoryRepeatCount = 3;
        public const double MaximumP95Milliseconds = 33.33d;
        public const int TargetFrameRate = 60;
        public const int Width = 1600;
        public const int Height = 900;
        public const long ManagedGrowthLimitBytes = 32L * 1024L * 1024L;
        public const long WorkingSetGrowthLimitBytes = 128L * 1024L * 1024L;
        public const double MajorCollectionsPerMinuteLimit = 12d;

        public int schemaVersion = CurrentSchemaVersion;
        public int harnessVersion = CurrentHarnessVersion;
        public string scenarioId = "combined_representative_settlement";
        public string configId;
        public string baseCommit;
        public string outputDirectory;
        public int width = Width;
        public int height = Height;
        public bool p109IntegrationProfile;
        public int population;
        public int tier1Count;
        public int tier2Count;
        public int tier3Count;
        public int repeatIndex;
        public int repeatCount = MandatoryRepeatCount;
        public double warmupSeconds = MandatoryWarmupSeconds;
        public double measurementSeconds = MandatoryMeasurementSeconds;
        public long seed = 20261006L;
        public bool mandatoryGate = true;
        public bool enableTierChurn = true;
        public bool enableSaveObservation = true;
        public bool detailedDiagnostics;
        public bool captureProfilerTrace;

        public static bool IsRequested(string[] args) =>
            args != null && args.Any(x => string.Equals(x, "-u29Benchmark", StringComparison.OrdinalIgnoreCase));

        public static U29BenchmarkConfig Parse(string[] args)
        {
            if (!IsRequested(args)) return null;
            var population = IntArg(args, "-u29Population");
            GetTierMix(population, out var tier1, out var tier2, out var tier3);
            var config = new U29BenchmarkConfig
            {
                p109IntegrationProfile = HasArg(args, "-p109Performance"),
                width = OptionalIntArg(args, "-u29Width", Width),
                height = OptionalIntArg(args, "-u29Height", Height),
                population = population,
                tier1Count = OptionalIntArg(args, "-u29Tier1", tier1),
                tier2Count = OptionalIntArg(args, "-u29Tier2", tier2),
                tier3Count = OptionalIntArg(args, "-u29Tier3", tier3),
                repeatIndex = IntArg(args, "-u29Repeat"),
                repeatCount = OptionalIntArg(args, "-u29RepeatCount", MandatoryRepeatCount),
                warmupSeconds = OptionalDoubleArg(args, "-u29WarmupSeconds", MandatoryWarmupSeconds),
                measurementSeconds = OptionalDoubleArg(args, "-u29MeasurementSeconds", MandatoryMeasurementSeconds),
                seed = OptionalLongArg(args, "-u29Seed", 20261006L),
                outputDirectory = StringArg(args, "-u29OutputDirectory"),
                configId = StringArg(args, "-u29ConfigId"),
                baseCommit = StringArg(args, "-u29BaseCommit"),
                mandatoryGate = !HasArg(args, "-u29Smoke"),
                detailedDiagnostics = HasArg(args, "-u29Diagnostics"),
                captureProfilerTrace = HasArg(args, "-u29Trace"),
                enableTierChurn = !HasArg(args, "-u29DisableTierChurn"),
                enableSaveObservation = !HasArg(args, "-u29DisableSaveObservation")
            };
            if (config.p109IntegrationProfile)
                config.scenarioId = "p10_feature_complete_connected_world";
            config.Validate();
            return config;
        }

        public void Validate()
        {
            if (schemaVersion != CurrentSchemaVersion || harnessVersion != CurrentHarnessVersion)
                throw new InvalidOperationException("Unsupported U29 benchmark config version.");
            GetTierMix(population, out var expected1, out var expected2, out var expected3);
            if (tier1Count != expected1 || tier2Count != expected2 || tier3Count != expected3 ||
                tier1Count + tier2Count + tier3Count != population)
                throw new InvalidOperationException("U29 requires the exact 25/50/25 tier mix.");
            if (repeatIndex < 1 || repeatIndex > repeatCount || repeatCount < 1)
                throw new InvalidOperationException("Invalid repeat index/count.");
            if (!FinitePositive(warmupSeconds) || !FinitePositive(measurementSeconds))
                throw new InvalidOperationException("Warm-up and measurement durations must be finite and positive.");
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Benchmark resolution must be positive.");
            if (p109IntegrationProfile && (width != 1920 || height != 1080))
                throw new InvalidOperationException("P10.9 integration performance requires exact 1920x1080.");
            if (!p109IntegrationProfile && mandatoryGate && (width != Width || height != Height))
                throw new InvalidOperationException("The U29 mandatory gate retains its fixed 1600x900 contract.");
            if (captureProfilerTrace && (mandatoryGate || !detailedDiagnostics))
                throw new InvalidOperationException("Profiler trace requires diagnostic smoke mode, never the acceptance gate.");
            if (mandatoryGate && (warmupSeconds < MandatoryWarmupSeconds ||
                                  measurementSeconds < MandatoryMeasurementSeconds ||
                                  repeatCount < MandatoryRepeatCount))
                throw new InvalidOperationException("Mandatory U29 duration/repeat minimum was not met.");
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Path.IsPathRooted(outputDirectory))
                throw new InvalidOperationException("U29 output directory must be an absolute path.");
            if (string.IsNullOrWhiteSpace(configId) || string.IsNullOrWhiteSpace(baseCommit))
                throw new InvalidOperationException("Config ID and base commit are required.");
        }

        public static void GetTierMix(int population, out int tier1, out int tier2, out int tier3)
        {
            if (population != 100 && population != 300 && population != 500 && population != 1000)
                throw new ArgumentOutOfRangeException(nameof(population), "Population must be 100, 300, 500 or 1000.");
            tier1 = population / 4;
            tier2 = population / 2;
            tier3 = population / 4;
        }

        private static bool FinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        private static bool HasArg(string[] args, string key) => args.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
        private static string StringArg(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            throw new ArgumentException("Missing U29 argument " + key + ".");
        }
        private static int IntArg(string[] args, string key) => int.Parse(StringArg(args, key), CultureInfo.InvariantCulture);
        private static int OptionalIntArg(string[] args, string key, int fallback) =>
            HasArg(args, key) ? IntArg(args, key) : fallback;
        private static long OptionalLongArg(string[] args, string key, long fallback) =>
            HasArg(args, key) ? long.Parse(StringArg(args, key), CultureInfo.InvariantCulture) : fallback;
        private static double OptionalDoubleArg(string[] args, string key, double fallback) =>
            HasArg(args, key) ? double.Parse(StringArg(args, key), CultureInfo.InvariantCulture) : fallback;
    }

    [Serializable]
    public sealed class U29HardwareProfile
    {
        public string operatingSystem;
        public string processor;
        public int logicalProcessorCount;
        public int systemMemoryMb;
        public string graphicsDevice;
        public string graphicsDeviceType;
        public int graphicsMemoryMb;
        public string unityVersion;
        public string buildGuid;
        public int width;
        public int height;
        public string qualityLevel;
        public int vSyncCount;
        public int targetFrameRate;
    }

    [Serializable]
    public sealed class U29FrameMetrics
    {
        public int sampleCount;
        public double averageMs;
        public double p50Ms;
        public double p90Ms;
        public double p95Ms;
        public double p99Ms;
        public double maximumMs;
        public double derivedAverageFps;
        public double mainThreadAverageMs;
        public double renderThreadAverageMs;
        public double gpuAverageMs;
        public bool mainThreadRecorderAvailable;
        public bool renderThreadRecorderAvailable;
        public bool gpuRecorderAvailable;
        public int focusedSampleCount;
        public int unfocusedSampleCount;
    }

    [Serializable]
    public sealed class U29MemoryMetrics
    {
        public long allocatedBytesInWindow;
        public double allocatedBytesPerFrame;
        public long managedHeapStartBytes;
        public long managedHeapMidBytes;
        public long managedHeapEndBytes;
        public long workingSetStartBytes;
        public long workingSetMidBytes;
        public long workingSetEndBytes;
        public string workingSetSource;
        public int gen0Collections;
        public int gen1Collections;
        public int gen2Collections;
        public double gen2CollectionsPerMinute;
        public bool runawayManagedGrowth;
        public bool runawayWorkingSetGrowth;
        public bool recurringMajorGcStorm;
        public bool monoRuntime;
        public bool incrementalGcEnabled;
        public bool collectionCountersAliasAllGenerations;
        public string collectionCounterInterpretation;
    }

    [Serializable]
    public sealed class U29RuntimeCounts
    {
        public int canonicalCharacters;
        public int distinctStableIds;
        public int tier1;
        public int tier2;
        public int tier3;
        public int tier1Presenters;
        public int tier2Entities;
        public int tier3Records;
        public int gameObjects;
        public int uiSubscriptions;
        public int pendingEvents;
        public int eventHistory;
        public int activeCombats;
        public int militaryUnits;
        public int activeTravels;
    }

    [Serializable]
    public sealed class U29OperationMetrics
    {
        public long calendarTicksAdvanced;
        public long tier1Decisions;
        public long tier1NeedUpdates;
        public long tier1ActionUpdates;
        public int tier2LastScheduledEntities;
        public int tier3Records;
        public int tierChurnTransitions;
        public ulong canonicalStateHash;
        public string canonicalStateHashHex;
    }

    [Serializable]
    public sealed class U29AllocationBreakdown
    {
        public long simulationSamples;
        public long tier1RuntimeSamples;
        public double tier1SchedulerBytesPerSample;
        public double simulationCallbackBytesPerSample;
        public double movementPresentationBytesPerSample;
        public double tier2BytesPerSample;
        public double tier3AndResearchBytesPerSample;
        public double workersAndWeatherBytesPerSample;
        public double canonicalServicesBytesPerSample;
        public double presentationAndTierPolicyBytesPerSample;
        public double totalSimulationBytesPerSample;
    }

    [Serializable]
    public sealed class U29ProfilerMarkerMetric
    {
        public string name;
        public bool available;
        public double averageMs;
        public double maximumMs;
    }

    [Serializable]
    public sealed class U29SaveObservation
    {
        public double captureMs;
        public double xmlSerializeMs;
        public double fileWriteMs;
        public double readDeserializeMs;
        public double restoreMs;
        public long payloadBytes;
        public int restoredCharacters;
        public int restoredTier1;
        public int restoredTier2;
        public int restoredTier3;
        public ulong identityTierHash;
        public string identityTierHashHex;
        public bool exactIdentityTierRoundTrip;
        public ulong canonicalHashBeforeRestore;
        public ulong canonicalHashAfterRestore;
        public string canonicalHashBeforeRestoreHex;
        public string canonicalHashAfterRestoreHex;
        public bool exactCanonicalStateRoundTrip;
        public int restoredPendingEvents;
        public int restoredEventHistory;
    }

    [Serializable]
    public sealed class U29BenchmarkResult
    {
        public int schemaVersion;
        public int harnessVersion;
        public string timestampUtc;
        public string scenarioId;
        public string configId;
        public string baseCommit;
        public string buildGuid;
        public long seed;
        public int population;
        public int requestedTier1;
        public int requestedTier2;
        public int requestedTier3;
        public int repeatIndex;
        public int repeatCount;
        public bool mandatoryGate;
        public bool graphicsDeviceActive;
        public bool batchMode;
        public bool uiEnabled;
        public bool cameraActive;
        public double warmupSeconds;
        public double measurementSeconds;
        public string rawFramesFile;
        public string rawFramesSha256;
        public string visualEvidenceFile;
        public string visualEvidenceSha256;
        public U29HardwareProfile hardware;
        public U29FrameMetrics frame;
        public U29MemoryMetrics memory;
        public U29RuntimeCounts countsStart;
        public U29RuntimeCounts countsEnd;
        public U29OperationMetrics operations;
        public U29AllocationBreakdown allocationBreakdown;
        public U29ProfilerMarkerMetric[] profilerMarkers;
        public U29SaveObservation save;
        public bool passed;
        public string[] failureReasons;
    }

    [Serializable]
    public sealed class U29AggregateResult
    {
        public int schemaVersion = U29BenchmarkConfig.CurrentSchemaVersion;
        public int harnessVersion = U29BenchmarkConfig.CurrentHarnessVersion;
        public string generatedUtc;
        public string configId;
        public string buildGuid;
        public U29BenchmarkResult[] repeats;
        public int worstRepeatIndex;
        public double worstP95Ms;
        public bool memoryTrendPassed;
        public bool passed;
        public string[] failureReasons;
    }

    public static class U29BenchmarkStatistics
    {
        public static double Percentile(IEnumerable<double> samples, double percentile)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (double.IsNaN(percentile) || percentile < 0d || percentile > 100d)
                throw new ArgumentOutOfRangeException(nameof(percentile));
            var ordered = samples.ToArray();
            if (ordered.Length == 0) throw new ArgumentException("At least one sample is required.", nameof(samples));
            if (ordered.Any(x => double.IsNaN(x) || double.IsInfinity(x) || x <= 0d))
                throw new ArgumentException("Frame samples must be finite and positive.", nameof(samples));
            Array.Sort(ordered);
            if (ordered.Length == 1) return ordered[0];
            var rank = percentile / 100d * (ordered.Length - 1);
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            if (lower == upper) return ordered[lower];
            return ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower);
        }

        public static U29BenchmarkResult WorstRepeat(IEnumerable<U29BenchmarkResult> repeats)
        {
            if (repeats == null) throw new ArgumentNullException(nameof(repeats));
            return repeats.Where(x => x != null).OrderByDescending(x => x.frame.p95Ms)
                .ThenByDescending(x => x.repeatIndex).FirstOrDefault()
                ?? throw new ArgumentException("At least one result is required.", nameof(repeats));
        }

        public static bool IsRunawayGrowth(long start, long middle, long end, long limit)
        {
            if (start < 0 || middle < 0 || end < 0 || limit < 0) throw new ArgumentOutOfRangeException();
            var minimumStep = Math.Max(1024L * 1024L, limit / 8L);
            return middle - start > minimumStep && end - middle > minimumStep && end - start > limit;
        }
    }

    public static class U29BenchmarkValidator
    {
        public static string[] Validate(U29BenchmarkConfig config, U29BenchmarkResult result, bool rawEvidenceExists)
        {
            if (config == null || result == null) return new[] { "missing_config_or_result" };
            var failures = new List<string>();
            try { config.Validate(); } catch (Exception exception) { failures.Add("invalid_config:" + exception.Message); }
            if (result.schemaVersion != U29BenchmarkConfig.CurrentSchemaVersion ||
                result.harnessVersion != U29BenchmarkConfig.CurrentHarnessVersion) failures.Add("unsupported_result_version");
            if (result.population != config.population || result.requestedTier1 != config.tier1Count ||
                result.requestedTier2 != config.tier2Count || result.requestedTier3 != config.tier3Count)
                failures.Add("config_population_or_tier_mismatch");
            if (result.countsEnd == null || result.countsEnd.canonicalCharacters != config.population ||
                result.countsEnd.distinctStableIds != config.population || result.countsEnd.tier1 != config.tier1Count ||
                result.countsEnd.tier2 != config.tier2Count || result.countsEnd.tier3 != config.tier3Count ||
                result.countsEnd.tier1Presenters != config.tier1Count ||
                result.countsEnd.tier2Entities != config.tier2Count ||
                result.countsEnd.tier3Records != config.tier3Count) failures.Add("actual_population_or_representation_mismatch");
            var requiredSeconds = config.captureProfilerTrace ? result.measurementSeconds : config.measurementSeconds;
            if (result.frame == null || result.frame.sampleCount < Math.Max(1, (int)(requiredSeconds * 10d)))
                failures.Add("insufficient_frame_samples");
            if (result.frame == null || !FinitePositive(result.frame.p50Ms) || !FinitePositive(result.frame.p95Ms) ||
                !FinitePositive(result.frame.p99Ms) || !FinitePositive(result.frame.maximumMs)) failures.Add("invalid_frame_statistics");
            if (config.mandatoryGate)
            {
                if (result.warmupSeconds < U29BenchmarkConfig.MandatoryWarmupSeconds) failures.Add("short_warmup");
                if (result.measurementSeconds < U29BenchmarkConfig.MandatoryMeasurementSeconds) failures.Add("short_measurement");
                if (result.repeatCount < U29BenchmarkConfig.MandatoryRepeatCount) failures.Add("insufficient_repeats");
                if (result.batchMode || !result.graphicsDeviceActive) failures.Add("headless_or_missing_graphics");
                if (!result.uiEnabled) failures.Add("ui_disabled");
                if (!result.cameraActive) failures.Add("camera_inactive");
                if (result.frame != null && result.frame.unfocusedSampleCount != 0)
                    failures.Add("unfocused_or_background_samples");
                if (string.IsNullOrWhiteSpace(result.visualEvidenceFile) ||
                    string.IsNullOrWhiteSpace(result.visualEvidenceSha256)) failures.Add("missing_visual_evidence");
                if (result.frame != null && result.frame.p95Ms > U29BenchmarkConfig.MaximumP95Milliseconds)
                    failures.Add("p95_threshold_breach");
            }
            if (!rawEvidenceExists || string.IsNullOrWhiteSpace(result.rawFramesSha256)) failures.Add("missing_raw_evidence");
            if (result.memory == null) failures.Add("missing_memory_metrics");
            else if (config.mandatoryGate)
            {
                if (result.memory.runawayManagedGrowth) failures.Add("runaway_managed_growth");
                if (result.memory.runawayWorkingSetGrowth) failures.Add("runaway_working_set_growth");
                if (result.memory.recurringMajorGcStorm) failures.Add("recurring_major_gc_storm");
            }
            if (config.enableSaveObservation && (result.save == null || !result.save.exactIdentityTierRoundTrip ||
                !result.save.exactCanonicalStateRoundTrip))
                failures.Add("save_observation_identity_mismatch");
            if (result.countsStart != null && result.countsEnd != null &&
                result.countsEnd.uiSubscriptions != result.countsStart.uiSubscriptions) failures.Add("ui_subscription_growth");
            return failures.Distinct().ToArray();
        }

        public static string[] ValidateAggregate(IReadOnlyList<U29BenchmarkResult> repeats)
        {
            var failures = new List<string>();
            if (repeats == null || repeats.Count < U29BenchmarkConfig.MandatoryRepeatCount)
                return new[] { "insufficient_repeats" };
            if (repeats.Select(x => x.repeatIndex).Distinct().Count() != repeats.Count)
                failures.Add("duplicate_repeat_index");
            var first = repeats[0];
            if (repeats.Any(x => x.configId != first.configId || x.buildGuid != first.buildGuid ||
                                 x.population != first.population || x.seed != first.seed ||
                                 x.hardware == null || first.hardware == null ||
                                 x.hardware.processor != first.hardware.processor ||
                                 x.hardware.graphicsDevice != first.hardware.graphicsDevice ||
                                 x.hardware.operatingSystem != first.hardware.operatingSystem))
                failures.Add("hardware_or_config_mismatch");
            if (repeats.Any(x => !x.passed)) failures.Add("failed_repeat");
            return failures.ToArray();
        }

        private static bool FinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
    }

    public static class U29BenchmarkJson
    {
        public static string Serialize<T>(T value) => JsonUtility.ToJson(value, true);
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is required.", nameof(json));
            var value = JsonUtility.FromJson<T>(json);
            return value == null ? throw new InvalidDataException("JSON did not contain the requested U29 model.") : value;
        }
    }
}
