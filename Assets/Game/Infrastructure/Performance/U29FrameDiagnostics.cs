using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Game.Infrastructure.Performance
{
    /// <summary>Opt-in diagnostic observer. All storage is reserved before measurement; CSV is written after it.</summary>
    [DefaultExecutionOrder(-30000)]
    public sealed class U29FrameDiagnostics : MonoBehaviour
    {
        private static readonly string[] Sections =
            { "tier2", "tier3_research", "workers_weather", "canonical_services", "presentation_tiers" };
        private U29BenchmarkController owner;
        private readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        private readonly List<string> markerNames = new List<string>();
        private int[] previousRecorderCounts;
        private readonly long[] sectionTicks = new long[5];
        private readonly long[] sectionBytes = new long[5];
        private long[,] rows;
        private int count;
        private long tickCursor;
        private long allocationCursor;
        private int simulationFrame = -1;
        private int previousCollections;
        private int lastObservedFrame = -1;
        private bool initialized;
        private bool started;
        private bool disposed;
        private bool capacityExceeded;
        private bool allocationProbeSupported;
        private long allocationProbeDelta;
        private double nextProcessSample;
        private double previousProcessSample;
        private long previousCpuTicks;
        private long processCpuPermille;
        private Process process;
        // Base columns are integers (nanoseconds, bytes, counts); no strings/boxing during collection.
        private const int BaseColumns = 18;

        public void Initialize(U29BenchmarkController controller)
        {
            owner = controller;
            allocationProbeSupported = ProbeThreadAllocationCounter(out allocationProbeDelta);
            process = Process.GetCurrentProcess();
            previousCpuTicks = process.TotalProcessorTime.Ticks;
            previousProcessSample = Time.realtimeSinceStartupAsDouble;
            nextProcessSample = previousProcessSample + 1d;
        }

        public static bool ProbeThreadAllocationCounter(out long delta)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[65536];
            probe[0] = 1;
            delta = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(probe);
            return delta >= 65536;
        }

        public void BeginSimulation()
        {
            simulationFrame = Time.frameCount;
            tickCursor = Stopwatch.GetTimestamp();
            allocationCursor = allocationProbeSupported ? GC.GetAllocatedBytesForCurrentThread() : 0;
        }

        public void EndSection(int section)
        {
            var now = Stopwatch.GetTimestamp();
            sectionTicks[section] = now - tickCursor;
            tickCursor = now;
            var allocated = allocationProbeSupported ? GC.GetAllocatedBytesForCurrentThread() : 0;
            sectionBytes[section] = allocationProbeSupported ? allocated - allocationCursor : -1;
            allocationCursor = allocated;
        }

        private void Update()
        {
            if (owner == null || disposed) return;
            // Wait one startup frame for native markers to register, still entirely inside warm-up.
            if (!initialized)
            {
                DiscoverRecorders();
                rows = new long[checked((int)Math.Ceiling(owner.Config.measurementSeconds * 120d) + 256),
                    BaseColumns + recorders.Count];
                initialized = true;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextProcessSample)
            {
                var cpuTicks = process.TotalProcessorTime.Ticks;
                // 1000 == one fully occupied logical CPU, not percent of all 22 cores.
                processCpuPermille = (long)((cpuTicks - previousCpuTicks) / (now - previousProcessSample) / 10000d);
                previousCpuTicks = cpuTicks;
                previousProcessSample = now;
                nextProcessSample = now + 1d;
            }
            var collections = GC.CollectionCount(0);
            if (!owner.IsMeasuring) { previousCollections = collections; return; }
            // First observed measured frame includes setup/normalization; exclude it from diagnostic rows.
            if (!started)
            {
                started = true;
                previousCollections = collections;
                lastObservedFrame = Time.frameCount;
                for (var i = 0; i < recorders.Count; i++) previousRecorderCounts[i] = recorders[i].Count;
                return;
            }
            if (count >= rows.GetLength(0)) { capacityExceeded = true; return; }
            rows[count, 0] = Time.frameCount - 1;
            rows[count, 1] = (long)(now * 1000000000d);
            rows[count, 2] = (long)(Time.unscaledDeltaTime * 1000000000d);
            rows[count, 3] = Application.isFocused ? 1 : 0;
            rows[count, 4] = collections - previousCollections;
            rows[count, 5] = simulationFrame;
            rows[count, 6] = processCpuPermille;
            rows[count, 7] = Time.frameCount - lastObservedFrame;
            for (var i = 0; i < 5; i++)
            {
                var aligned = simulationFrame == Time.frameCount - 1;
                rows[count, 8 + i] = aligned ? (long)(sectionTicks[i] * (1000000000d / Stopwatch.Frequency)) : -1;
                rows[count, 13 + i] = aligned ? sectionBytes[i] : -1;
            }
            for (var i = 0; i < recorders.Count; i++)
            {
                var recorder = recorders[i];
                var recorderCount = recorder.Valid ? recorder.Count : 0;
                rows[count, BaseColumns + i] = recorderCount > previousRecorderCounts[i] ? recorder.LastValue : -1;
                previousRecorderCounts[i] = recorderCount;
                // Do not reset/restart inside Update: that truncates outer PlayerLoop/Update scopes.
                // Non-wrapping preallocated history identifies genuinely new samples without stale repeats.
            }
            previousCollections = collections;
            lastObservedFrame = Time.frameCount;
            count++;
        }

        private void DiscoverRecorders()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var inventory = new StringBuilder("name,category,unit,recorded\n");
            foreach (var handle in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(handle);
                var n = d.Name;
                var wanted = n.StartsWith("Serenity.", StringComparison.Ordinal) ||
                    n == "PlayerLoop" || n == "Main Thread" || n == "Render Thread" ||
                    n == "GPU Frame Time" || n == "GC Allocated In Frame" ||
                    n == "Update.ScriptRunBehaviourUpdate" || n == "GC.Collect" ||
                    n.StartsWith("GarbageCollector.", StringComparison.Ordinal) ||
                    n.StartsWith("GC.Incremental", StringComparison.Ordinal) ||
                    n == "PreLateUpdate.DirectorUpdateAnimationBegin" ||
                    n == "PreLateUpdate.DirectorUpdateAnimationEnd" ||
                    n == "Animators.Update" || n == "Animator.ProcessAnimations" || n == "Animator.ProcessGraph" ||
                    n == "Animators.ProcessAnimationsJob" || n == "Animators.ProcessGraphJob" ||
                    n == "Animators.WriteJob" || n == "Animators.PrepareFirstPass" || n == "Animators.PrepareSecondPass" ||
                    n == "PreUpdate.AIUpdate" || n == "FixedUpdate.PhysicsFixedUpdate" ||
                    n == "WaitForTargetFPS" || n == "Gfx.WaitForPresentOnGfxThread" ||
                    n == "Gfx.WaitForGfxCommandsFromMainThread" || n == "Semaphore.WaitForSignal" ||
                    n == "PostLateUpdate.FinishFrameRendering" || n == "Camera.Render";
                var recorded = false;
                if (wanted && !markerNames.Contains(n) && recorders.Count < 64)
                {
                    var capacity = checked((int)Math.Ceiling(
                        (owner.Config.warmupSeconds + owner.Config.measurementSeconds + 60d) * 120d) + 256);
                    var recorder = new ProfilerRecorder(handle, capacity,
                        ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.StartImmediately);
                    if (recorder.Valid) { recorders.Add(recorder); markerNames.Add(n); recorded = true; }
                    else recorder.Dispose();
                }
                inventory.Append(Csv(n)).Append(',').Append(Csv(d.Category.ToString())).Append(',')
                    .Append(Csv(d.UnitType.ToString())).Append(',').Append(recorded ? "1" : "0").AppendLine();
            }
            File.WriteAllText(Path.Combine(owner.Config.outputDirectory, "diagnostic-marker-inventory.csv"),
                inventory.ToString(), new UTF8Encoding(false));
            previousRecorderCounts = new int[recorders.Count];
        }

        public void WriteArtifacts()
        {
            if (capacityExceeded) throw new InvalidOperationException("U29 diagnostic frame capacity exceeded.");
            if (!initialized || count == 0) throw new InvalidOperationException("U29 detailed diagnostics captured no frames.");
            var headers = new List<string> { "unity_frame", "realtime_ns", "frame_ns", "focused",
                "gc_collections_since_sample", "simulation_frame", "process_cpu_permille_one_core", "sample_frame_gap" };
            headers.AddRange(Sections.Select(x => x + "_ns"));
            headers.AddRange(Sections.Select(x => x + "_allocated_bytes"));
            headers.AddRange(markerNames.Select(x => "profiler:" + x));
            using (var writer = new StreamWriter(Path.Combine(owner.Config.outputDirectory, "diagnostic-frames.csv"),
                false, new UTF8Encoding(false)))
            {
                writer.WriteLine(string.Join(",", headers.Select(Csv)));
                for (var row = 0; row < count; row++)
                {
                    for (var col = 0; col < rows.GetLength(1); col++)
                    {
                        if (col != 0) writer.Write(',');
                        writer.Write(rows[row, col].ToString(CultureInfo.InvariantCulture));
                    }
                    writer.WriteLine();
                }
            }
            File.WriteAllText(Path.Combine(owner.Config.outputDirectory, "diagnostic-metadata.txt"),
                "format=1\nallocation_probe_supported=" + allocationProbeSupported +
                "\nallocation_probe_delta=" + allocationProbeDelta + "\nframes=" + count +
                "\n-1 means unavailable or not emitted in that frame; do not interpret as zero." +
                "\nTimings use previous completed Unity frame; simulation_frame must equal unity_frame." +
                "\nGC collections count covers sampler intervals, not measured GC pause duration." +
                "\nProfiler values use inventory units; nested and multithreaded times must not be summed." +
                "\nGPU timing can be delayed by the graphics pipeline. CPU counter is sampled every second." +
                "\nNo temperature or CPU frequency is captured; throttling cannot be proven by this report.\n");
            Dispose();
        }

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        private void OnDestroy() => Dispose();
        private void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var recorder in recorders) recorder.Dispose();
            process?.Dispose();
        }
    }
}
