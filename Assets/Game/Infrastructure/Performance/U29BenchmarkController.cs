using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Game.Infrastructure.Performance
{
    /// <summary>
    /// Explicit command-line-only Player harness. It composes the normal LocalGameplay runtime, samples rendered
    /// frames, and exits with a non-zero code when evidence is incomplete or a hard gate fails.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class U29BenchmarkController : MonoBehaviour
    {
        private static readonly string[] MarkerNames =
        {
            "PlayerLoop", "Update.ScriptRunBehaviourUpdate", "UIR.RenderChain.Render",
            "RenderLoop.Draw", "Gfx.WaitForPresentOnGfxThread", "Serenity.Tier1.Advance",
            "Serenity.Tier1.Scheduler", "Serenity.Tier1.SimulationCallbacks",
            "Serenity.Tier1.MovementPresentation", "Serenity.Tier1.AnimationPresentation"
        };

        private readonly List<FrameSample> samples = new List<FrameSample>(32768);
        private readonly List<MarkerRecorder> markerRecorders = new List<MarkerRecorder>();
        private LocalSceneCompositionRoot root;
        private U29BenchmarkConfig config;
        private U29BenchmarkScenario scenario;
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder renderThreadRecorder;
        private ProfilerRecorder gpuRecorder;
        private ProfilerRecorder gcAllocatedRecorder;
        private double warmupStarted;
        private double measurementStarted;
        private bool measuring;
        private bool completing;
        private bool capturedMidpoint;
        private long managedStart;
        private long managedMid;
        private long managedEnd;
        private long workingSetStart;
        private long workingSetMid;
        private long workingSetEnd;
        private long cachedManagedHeap;
        private long cachedWorkingSet;
        private double nextMemorySample;
        private int gen0Start;
        private int gen1Start;
        private int gen2Start;
        private int gen0End;
        private int gen1End;
        private int gen2End;
        private long calendarTickStart;
        private long tier1DecisionsStart;
        private long tier1NeedsStart;
        private long tier1ActionsStart;
        private U29RuntimeCounts countsStart;
        private Vector3 cameraAnchor;
        private bool allocationProbeActive;
        private long allocationProbeCursor;
        private long allocationProbeSamples;
        private long tier2Allocated;
        private long tier3AndResearchAllocated;
        private long workersAndWeatherAllocated;
        private long canonicalServicesAllocated;
        private long presentationAndTierPolicyAllocated;
        private U29FrameDiagnostics diagnostics;
        private double traceStarted = -1d;
        private bool traceActive;

        public U29BenchmarkConfig Config => config;
        public U29BenchmarkScenario Scenario => scenario;
        public bool IsMeasuring => measuring;

        public void BeginSimulationAllocationSample()
        {
            diagnostics?.BeginSimulation();
            allocationProbeActive = measuring && !completing;
            if (allocationProbeActive) allocationProbeCursor = GC.GetAllocatedBytesForCurrentThread();
        }

        public void RecordTier2Allocation() { diagnostics?.EndSection(0); RecordAllocation(ref tier2Allocated); }
        public void RecordTier3AndResearchAllocation() { diagnostics?.EndSection(1); RecordAllocation(ref tier3AndResearchAllocated); }
        public void RecordWorkersAndWeatherAllocation() { diagnostics?.EndSection(2); RecordAllocation(ref workersAndWeatherAllocated); }
        public void RecordCanonicalServicesAllocation() { diagnostics?.EndSection(3); RecordAllocation(ref canonicalServicesAllocated); }
        public void RecordPresentationAndTierPolicyAllocation()
        {
            diagnostics?.EndSection(4);
            RecordAllocation(ref presentationAndTierPolicyAllocated);
            if (allocationProbeActive) allocationProbeSamples++;
            allocationProbeActive = false;
        }

        private void RecordAllocation(ref long destination)
        {
            if (!allocationProbeActive) return;
            var current = GC.GetAllocatedBytesForCurrentThread();
            destination += Math.Max(0L, current - allocationProbeCursor);
            allocationProbeCursor = current;
        }

        public static U29BenchmarkController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var parsed = U29BenchmarkConfig.Parse(Environment.GetCommandLineArgs());
            if (parsed == null) return null;
            var controller = compositionRoot.gameObject.AddComponent<U29BenchmarkController>();
            controller.Initialize(compositionRoot, parsed);
            return controller;
        }

        public void Initialize(LocalSceneCompositionRoot compositionRoot, U29BenchmarkConfig benchmarkConfig)
        {
            if (root != null) throw new InvalidOperationException("U29 benchmark is already initialized.");
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            config = benchmarkConfig ?? throw new ArgumentNullException(nameof(benchmarkConfig));
            try
            {
                config.Validate();
                if (File.Exists(Path.Combine(config.outputDirectory, "summary.json")))
                    throw new IOException("U29 output directory contains a stale summary.");
                Directory.CreateDirectory(config.outputDirectory);
                Application.runInBackground = true;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = U29BenchmarkConfig.TargetFrameRate;
                Screen.SetResolution(config.width, config.height, false);
                scenario = U29BenchmarkScenario.Create(root, config);
                cameraAnchor = root.CameraController.transform.position;
                if (config.enableTierChurn) scenario.ExerciseTierChurn();
                if (config.detailedDiagnostics)
                {
                    // A sustained diagnostic outlasts the usual five-minute matrix repeat.
                    // Reserve before warm-up so List growth cannot create a measured allocation spike.
                    samples.Capacity = Math.Max(samples.Capacity,
                        checked((int)Math.Ceiling(config.measurementSeconds * 120d) + 256));
                    diagnostics = gameObject.AddComponent<U29FrameDiagnostics>();
                    diagnostics.Initialize(this);
                }
                root.AiRuntime.AllocationDiagnosticsEnabled = true;
                CaptureVisualEvidence(Path.Combine(config.outputDirectory, "visual-evidence.png"));
                StartRecorders();
                NormalizeManagedHeap();
                warmupStarted = Time.realtimeSinceStartupAsDouble;
                Debug.Log("U29_BENCHMARK_READY: population=" + config.population + "; repeat=" + config.repeatIndex +
                          "; mandatory=" + config.mandatoryGate);
            }
            catch (Exception exception)
            {
                FailBeforeMeasurement(exception);
            }
        }

        private void Update()
        {
            if (root == null || config == null || completing) return;
            try
            {
                var now = Time.realtimeSinceStartupAsDouble;
                if (!measuring)
                {
                    if (now - warmupStarted < config.warmupSeconds) return;
                    BeginMeasurement(now);
                    return;
                }

                CaptureFrame();
                var elapsed = now - measurementStarted;
                if (config.captureProfilerTrace && AdvanceProfilerTrace(now, elapsed))
                {
                    Complete(elapsed);
                    return;
                }
                if (!capturedMidpoint && elapsed >= config.measurementSeconds * 0.5d)
                {
                    managedMid = Profiler.GetMonoUsedSizeLong();
                    workingSetMid = GetWorkingSet();
                    capturedMidpoint = true;
                }
                if (elapsed >= config.measurementSeconds) Complete(elapsed);
            }
            catch (Exception exception)
            {
                // Complete marks the controller as completing before artifact construction. Re-open the
                // fail-closed path so a save/result exception cannot leave a visible Player hung forever.
                completing = false;
                FailBeforeMeasurement(exception);
            }
        }

        private void LateUpdate()
        {
            if (root != null && config != null && !completing && root.CameraController != null)
            {
                var cameraTransform = root.CameraController.transform;
                if ((cameraTransform.position - cameraAnchor).sqrMagnitude > 0.00000001f)
                    cameraTransform.position = cameraAnchor;
            }
        }

        private void BeginMeasurement(double now)
        {
            NormalizeManagedHeap();
            samples.Clear();
            foreach (var marker in markerRecorders) marker.Reset();
            managedStart = Profiler.GetMonoUsedSizeLong();
            workingSetStart = GetWorkingSet();
            cachedManagedHeap = managedStart;
            cachedWorkingSet = workingSetStart;
            nextMemorySample = now + 1d;
            gen0Start = GC.CollectionCount(0);
            gen1Start = GC.CollectionCount(1);
            gen2Start = GC.CollectionCount(2);
            calendarTickStart = root.Clock.State.CalendarTicks;
            tier1DecisionsStart = root.AiScheduler.Metrics.TotalDecisions;
            tier1NeedsStart = root.AiScheduler.Metrics.TotalNeedUpdates;
            tier1ActionsStart = root.AiScheduler.Metrics.TotalActionUpdates;
            countsStart = scenario.CaptureCounts();
            root.AiRuntime.ResetAllocationDiagnostics();
            measurementStarted = now;
            measuring = true;
            Debug.Log("U29_MEASUREMENT_STARTED");
        }

        private void CaptureFrame()
        {
            var frameMs = Time.unscaledDeltaTime * 1000d;
            if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs <= 0d)
                throw new InvalidOperationException("Unity supplied an invalid frame duration.");
            var now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextMemorySample)
            {
                cachedManagedHeap = Profiler.GetMonoUsedSizeLong();
                cachedWorkingSet = GetWorkingSet();
                nextMemorySample = now + 1d;
            }
            var sample = new FrameSample
            {
                index = samples.Count,
                frameMs = frameMs,
                mainThreadMs = RecorderMilliseconds(mainThreadRecorder),
                renderThreadMs = RecorderMilliseconds(renderThreadRecorder),
                gpuMs = RecorderMilliseconds(gpuRecorder),
                allocatedBytes = RecorderValue(gcAllocatedRecorder),
                managedHeapBytes = cachedManagedHeap,
                workingSetBytes = cachedWorkingSet,
                focused = Application.isFocused
            };
            samples.Add(sample);
            foreach (var marker in markerRecorders) marker.Capture();
        }

        private void Complete(double measuredSeconds)
        {
            StopProfilerTrace();
            completing = true;
            measuring = false;
            if (!capturedMidpoint)
            {
                managedMid = Profiler.GetMonoUsedSizeLong();
                workingSetMid = GetWorkingSet();
            }
            gen0End = GC.CollectionCount(0);
            gen1End = GC.CollectionCount(1);
            gen2End = GC.CollectionCount(2);
            NormalizeManagedHeap();
            managedEnd = Profiler.GetMonoUsedSizeLong();
            workingSetEnd = GetWorkingSet();

            var rawPath = Path.Combine(config.outputDirectory, "raw-frames.csv");
            WriteRawFrames(rawPath);
            diagnostics?.WriteArtifacts();
            var rawHash = Sha256(rawPath);
            var visualPath = Path.Combine(config.outputDirectory, "visual-evidence.png");
            var result = BuildResult(measuredSeconds, rawPath, rawHash, visualPath,
                File.Exists(visualPath) ? Sha256(visualPath) : string.Empty);
            result.failureReasons = U29BenchmarkValidator.Validate(config, result, File.Exists(rawPath));
            result.passed = result.failureReasons.Length == 0;
            File.WriteAllText(Path.Combine(config.outputDirectory, "summary.json"),
                U29BenchmarkJson.Serialize(result), new UTF8Encoding(false));
            Debug.Log("U29_BENCHMARK_RESULT: " + (result.passed ? "PASS" : "FAIL") + "; p95=" +
                      result.frame.p95Ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms; output=" +
                      config.outputDirectory);
            DisposeRecorders();
            Application.Quit(result.passed ? 0 : 29);
        }

        private U29BenchmarkResult BuildResult(double measuredSeconds, string rawPath, string rawHash,
            string visualPath, string visualHash)
        {
            var frames = samples.Select(x => x.frameMs).ToArray();
            if (frames.Length == 0) throw new InvalidOperationException("No measured U29 frames were captured.");
            var allocated = samples.Sum(x => Math.Max(0L, x.allocatedBytes));
            var gen0 = gen0End - gen0Start;
            var gen1 = gen1End - gen1Start;
            var gen2 = gen2End - gen2Start;
            var p99 = U29BenchmarkStatistics.Percentile(frames, 99d);
            var countsEnd = scenario.CaptureCounts();
            var stateHash = scenario.ComputeCanonicalStateHash();
            var memory = new U29MemoryMetrics
            {
                allocatedBytesInWindow = allocated,
                allocatedBytesPerFrame = frames.Length == 0 ? 0d : (double)allocated / frames.Length,
                managedHeapStartBytes = managedStart,
                managedHeapMidBytes = managedMid,
                managedHeapEndBytes = managedEnd,
                workingSetStartBytes = workingSetStart,
                workingSetMidBytes = workingSetMid,
                workingSetEndBytes = workingSetEnd,
                workingSetSource = "Process.WorkingSet64 with Profiler.TotalReservedMemory fallback",
                gen0Collections = gen0,
                gen1Collections = gen1,
                gen2Collections = gen2,
                gen2CollectionsPerMinute = measuredSeconds <= 0d ? double.PositiveInfinity : gen2 * 60d / measuredSeconds,
                monoRuntime = Type.GetType("Mono.Runtime") != null,
                incrementalGcEnabled = UnityEngine.Scripting.GarbageCollector.isIncremental,
                collectionCountersAliasAllGenerations = gen0 == gen1 && gen1 == gen2,
                runawayManagedGrowth = U29BenchmarkStatistics.IsRunawayGrowth(managedStart, managedMid, managedEnd,
                    U29BenchmarkConfig.ManagedGrowthLimitBytes),
                runawayWorkingSetGrowth = U29BenchmarkStatistics.IsRunawayGrowth(workingSetStart, workingSetMid,
                    workingSetEnd, U29BenchmarkConfig.WorkingSetGrowthLimitBytes)
            };
            memory.collectionCounterInterpretation = memory.monoRuntime && memory.collectionCountersAliasAllGenerations
                ? "Unity Mono exposes the same non-generational collection count through generations 0/1/2; tail latency is the major-pause proxy."
                : "Generation 2 count is used as the major-collection proxy.";
            memory.recurringMajorGcStorm = config.mandatoryGate &&
                memory.gen2CollectionsPerMinute > U29BenchmarkConfig.MajorCollectionsPerMinuteLimit &&
                (!memory.monoRuntime || !memory.collectionCountersAliasAllGenerations ||
                 p99 > U29BenchmarkConfig.MaximumP95Milliseconds);
            var save = config.enableSaveObservation ? scenario.ObserveSave(config.outputDirectory) : null;
            return new U29BenchmarkResult
            {
                schemaVersion = config.schemaVersion,
                harnessVersion = config.harnessVersion,
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                scenarioId = config.scenarioId,
                configId = config.configId,
                baseCommit = config.baseCommit,
                buildGuid = Application.buildGUID,
                seed = config.seed,
                population = config.population,
                requestedTier1 = config.tier1Count,
                requestedTier2 = config.tier2Count,
                requestedTier3 = config.tier3Count,
                repeatIndex = config.repeatIndex,
                repeatCount = config.repeatCount,
                mandatoryGate = config.mandatoryGate,
                graphicsDeviceActive = !Application.isBatchMode &&
                    !string.Equals(SystemInfo.graphicsDeviceType.ToString(), "Null", StringComparison.OrdinalIgnoreCase),
                batchMode = Application.isBatchMode,
                uiEnabled = root.GameplayUi != null && root.GameplayUi.enabled && root.GameplayUi.RootVisual != null,
                cameraActive = Camera.main != null && Camera.main.isActiveAndEnabled,
                warmupSeconds = measurementStarted - warmupStarted,
                measurementSeconds = measuredSeconds,
                rawFramesFile = Path.GetFileName(rawPath),
                rawFramesSha256 = rawHash,
                visualEvidenceFile = Path.GetFileName(visualPath),
                visualEvidenceSha256 = visualHash,
                hardware = CaptureHardware(),
                frame = new U29FrameMetrics
                {
                    sampleCount = frames.Length,
                    averageMs = frames.Average(),
                    p50Ms = U29BenchmarkStatistics.Percentile(frames, 50d),
                    p90Ms = U29BenchmarkStatistics.Percentile(frames, 90d),
                    p95Ms = U29BenchmarkStatistics.Percentile(frames, 95d),
                    p99Ms = p99,
                    maximumMs = frames.Max(),
                    derivedAverageFps = 1000d / frames.Average(),
                    mainThreadAverageMs = AverageAvailable(samples.Select(x => x.mainThreadMs)),
                    renderThreadAverageMs = AverageAvailable(samples.Select(x => x.renderThreadMs)),
                    gpuAverageMs = AverageAvailable(samples.Select(x => x.gpuMs)),
                    mainThreadRecorderAvailable = mainThreadRecorder.Valid,
                    renderThreadRecorderAvailable = renderThreadRecorder.Valid,
                    gpuRecorderAvailable = gpuRecorder.Valid,
                    focusedSampleCount = samples.Count(x => x.focused),
                    unfocusedSampleCount = samples.Count(x => !x.focused)
                },
                memory = memory,
                countsStart = countsStart,
                countsEnd = countsEnd,
                operations = new U29OperationMetrics
                {
                    calendarTicksAdvanced = root.Clock.State.CalendarTicks - calendarTickStart,
                    tier1Decisions = root.AiScheduler.Metrics.TotalDecisions - tier1DecisionsStart,
                    tier1NeedUpdates = root.AiScheduler.Metrics.TotalNeedUpdates - tier1NeedsStart,
                    tier1ActionUpdates = root.AiScheduler.Metrics.TotalActionUpdates - tier1ActionsStart,
                    tier2LastScheduledEntities = root.Tier2Runtime.LastScheduledEntityCount,
                    tier3Records = root.Tier3Characters.Count,
                    tierChurnTransitions = scenario.TierChurnTransitions,
                    canonicalStateHash = stateHash,
                    canonicalStateHashHex = stateHash.ToString("X16", CultureInfo.InvariantCulture)
                },
                allocationBreakdown = BuildAllocationBreakdown(),
                profilerMarkers = markerRecorders.Select(x => x.ToMetric()).ToArray(),
                save = save,
                failureReasons = Array.Empty<string>()
            };
        }

        private U29AllocationBreakdown BuildAllocationBreakdown()
        {
            var divisor = Math.Max(1L, allocationProbeSamples);
            var tier1Divisor = Math.Max(1L, root.AiRuntime.AllocationDiagnosticSamples);
            return new U29AllocationBreakdown
            {
                simulationSamples = allocationProbeSamples,
                tier1RuntimeSamples = root.AiRuntime.AllocationDiagnosticSamples,
                tier1SchedulerBytesPerSample = (double)root.AiRuntime.SchedulerAllocatedBytes / tier1Divisor,
                simulationCallbackBytesPerSample = (double)root.AiRuntime.SimulationCallbackAllocatedBytes / tier1Divisor,
                movementPresentationBytesPerSample = (double)root.AiRuntime.MovementPresentationAllocatedBytes / tier1Divisor,
                tier2BytesPerSample = (double)tier2Allocated / divisor,
                tier3AndResearchBytesPerSample = (double)tier3AndResearchAllocated / divisor,
                workersAndWeatherBytesPerSample = (double)workersAndWeatherAllocated / divisor,
                canonicalServicesBytesPerSample = (double)canonicalServicesAllocated / divisor,
                presentationAndTierPolicyBytesPerSample = (double)presentationAndTierPolicyAllocated / divisor,
                totalSimulationBytesPerSample = (double)(tier2Allocated + tier3AndResearchAllocated +
                    workersAndWeatherAllocated + canonicalServicesAllocated + presentationAndTierPolicyAllocated) / divisor
            };
        }

        private U29HardwareProfile CaptureHardware() => new U29HardwareProfile
        {
            operatingSystem = SystemInfo.operatingSystem,
            processor = SystemInfo.processorType,
            logicalProcessorCount = SystemInfo.processorCount,
            systemMemoryMb = SystemInfo.systemMemorySize,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
            graphicsMemoryMb = SystemInfo.graphicsMemorySize,
            unityVersion = Application.unityVersion,
            buildGuid = Application.buildGUID,
            width = Screen.width,
            height = Screen.height,
            qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()],
            vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate
        };

        private static void CaptureVisualEvidence(string path)
        {
            var type = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var method = type?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (method == null) throw new InvalidOperationException("Unity ScreenCapture API is unavailable.");
            method.Invoke(null, new object[] { path, 1 });
        }

        private void StartRecorders()
        {
            mainThreadRecorder = TryStart(ProfilerCategory.Internal, "Main Thread");
            renderThreadRecorder = TryStart(ProfilerCategory.Internal, "Render Thread");
            gpuRecorder = TryStart(ProfilerCategory.Render, "GPU Frame Time");
            gcAllocatedRecorder = TryStart(ProfilerCategory.Memory, "GC Allocated In Frame");
            foreach (var markerName in MarkerNames)
                markerRecorders.Add(new MarkerRecorder(markerName, TryStart(ProfilerCategory.Scripts, markerName)));
        }

        private static ProfilerRecorder TryStart(ProfilerCategory category, string marker)
        {
            try { return ProfilerRecorder.StartNew(category, marker, 1); }
            catch { return default; }
        }

        private static long RecorderValue(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : 0L;
        private static double RecorderMilliseconds(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.LastValue > 0L ? recorder.LastValue / 1000000d : 0d;
        private static double AverageAvailable(IEnumerable<double> values)
        {
            var available = values.Where(x => x > 0d).ToArray();
            return available.Length == 0 ? 0d : available.Average();
        }

        private void WriteRawFrames(string path)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("frame,frame_ms,main_thread_ms,render_thread_ms,gpu_ms,gc_allocated_bytes,managed_heap_bytes,working_set_bytes,focused");
                foreach (var sample in samples)
                    writer.WriteLine(string.Join(",", sample.index.ToString(CultureInfo.InvariantCulture),
                        sample.frameMs.ToString("R", CultureInfo.InvariantCulture),
                        sample.mainThreadMs.ToString("R", CultureInfo.InvariantCulture),
                        sample.renderThreadMs.ToString("R", CultureInfo.InvariantCulture),
                        sample.gpuMs.ToString("R", CultureInfo.InvariantCulture),
                        sample.allocatedBytes.ToString(CultureInfo.InvariantCulture),
                        sample.managedHeapBytes.ToString(CultureInfo.InvariantCulture),
                        sample.workingSetBytes.ToString(CultureInfo.InvariantCulture),
                        sample.focused ? "1" : "0"));
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void NormalizeManagedHeap()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static long GetWorkingSet()
        {
            using (var process = Process.GetCurrentProcess())
            {
                if (process.WorkingSet64 > 0L) return process.WorkingSet64;
            }
            return Profiler.GetTotalReservedMemoryLong();
        }

        private void FailBeforeMeasurement(Exception exception)
        {
            if (completing) return;
            completing = true;
            measuring = false;
            try
            {
                if (config != null && !string.IsNullOrWhiteSpace(config.outputDirectory))
                {
                    Directory.CreateDirectory(config.outputDirectory);
                    var failure = new U29BenchmarkResult
                    {
                        schemaVersion = U29BenchmarkConfig.CurrentSchemaVersion,
                        harnessVersion = U29BenchmarkConfig.CurrentHarnessVersion,
                        timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        configId = config.configId,
                        population = config.population,
                        repeatIndex = config.repeatIndex,
                        passed = false,
                        failureReasons = new[] { "harness_exception:" + exception.GetType().Name + ":" + exception.Message }
                    };
                    File.WriteAllText(Path.Combine(config.outputDirectory, "failure.json"),
                        U29BenchmarkJson.Serialize(failure), new UTF8Encoding(false));
                }
            }
            catch { }
            Debug.LogException(exception);
            DisposeRecorders();
            Application.Quit(29);
        }

        private bool AdvanceProfilerTrace(double now, double elapsed)
        {
            if (traceStarted < 0d && elapsed >= 10d && samples.Count >= 60)
            {
                var slow = 0;
                for (var i = samples.Count - 60; i < samples.Count; i++)
                    if (samples[i].frameMs > U29BenchmarkConfig.MaximumP95Milliseconds) slow++;
                if (slow >= 12)
                {
                    var path = Path.Combine(config.outputDirectory, "native-trace.raw");
                    Profiler.maxUsedMemory = 256 * 1024 * 1024;
                    Profiler.logFile = path;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    traceActive = true;
                    traceStarted = now;
                    File.WriteAllText(Path.Combine(config.outputDirectory, "native-trace-trigger.txt"),
                        "Diagnostic only; profiler overhead invalidates acceptance timing.\n" +
                        "trigger_frame=" + Time.frameCount + "\nslow_frames_in_last_60=" + slow +
                        "\nmeasurement_elapsed_seconds=" + elapsed.ToString("R", CultureInfo.InvariantCulture));
                    Debug.Log("U29_NATIVE_TRACE_STARTED: " + path);
                }
            }
            if (traceStarted >= 0d && now - traceStarted >= 8d)
            {
                StopProfilerTrace();
                Debug.Log("U29_NATIVE_TRACE_COMPLETE");
                return true;
            }
            if (elapsed >= config.measurementSeconds)
                throw new InvalidOperationException("No complete slow-frame profiler trace before diagnostic deadline.");
            return false;
        }

        private void StopProfilerTrace()
        {
            if (!traceActive) return;
            Profiler.enabled = false;
            // Clearing logFile flushes the binary capture so the Editor can load it after exit.
            Profiler.logFile = string.Empty;
            Profiler.enableBinaryLog = false;
            traceActive = false;
        }

        private void OnDestroy() => DisposeRecorders();

        private void DisposeRecorders()
        {
            StopProfilerTrace();
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
            if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
            if (gpuRecorder.Valid) gpuRecorder.Dispose();
            if (gcAllocatedRecorder.Valid) gcAllocatedRecorder.Dispose();
            foreach (var marker in markerRecorders) marker.Dispose();
            markerRecorders.Clear();
        }

        private struct FrameSample
        {
            public int index;
            public double frameMs;
            public double mainThreadMs;
            public double renderThreadMs;
            public double gpuMs;
            public long allocatedBytes;
            public long managedHeapBytes;
            public long workingSetBytes;
            public bool focused;
        }

        private sealed class MarkerRecorder : IDisposable
        {
            private readonly string name;
            private ProfilerRecorder recorder;
            private long samples;
            private double totalMs;
            private double maximumMs;

            public MarkerRecorder(string markerName, ProfilerRecorder profilerRecorder)
            {
                name = markerName;
                recorder = profilerRecorder;
            }

            public void Capture()
            {
                if (!recorder.Valid || recorder.LastValue <= 0L) return;
                var value = recorder.LastValue / 1000000d;
                totalMs += value;
                maximumMs = Math.Max(maximumMs, value);
                samples++;
            }

            public void Reset() { samples = 0; totalMs = 0d; maximumMs = 0d; }
            public U29ProfilerMarkerMetric ToMetric() => new U29ProfilerMarkerMetric
            {
                name = name,
                available = recorder.Valid && samples > 0,
                averageMs = samples == 0 ? 0d : totalMs / samples,
                maximumMs = maximumMs
            };
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }
    }
}
