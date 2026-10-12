// Runner protocol fixture only. This is not gameplay or official soak evidence.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal static class U30FakePlayer
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static string Arg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return "";
    }
    private static Dictionary<string, object> Obj(params object[] pairs)
    {
        var result = new Dictionary<string, object>();
        for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
        return result;
    }
    private static void Write(string path, object value)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, Json.Serialize(value), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
    }
    public static int Main(string[] args)
    {
        string output = Arg(args, "-u30OutputDirectory");
        string checkpointRoot = Arg(args, "-u30CheckpointRoot");
        int segment = int.Parse(Arg(args, "-u30SegmentIndex"));
        int seed = int.Parse(Arg(args, "-u30Seed"));
        double duration = double.Parse(Arg(args, "-u30SegmentSeconds"), System.Globalization.CultureInfo.InvariantCulture);
        string mode = Environment.GetEnvironmentVariable("SERENITY_U30_FIXTURE_MODE") ?? "healthy";
        string playerLog = Arg(args, "-logFile");
        if (playerLog.Length > 0)
            File.WriteAllText(playerLog, "PlayerConnection::Cleanup##utp:{\"type\":\"MemoryLeaks\",\"allocatedMemory\":1234}\n");
        string resume = Arg(args, "-u30ResumeFromCheckpoint");
        long sequence = segment;
        string checkpointDir = Path.Combine(checkpointRoot, "checkpoint-" + sequence.ToString("D8"));
        Directory.CreateDirectory(checkpointDir);
        string checkpoint = Path.Combine(checkpointDir, "checkpoint.xml");
        string tiers = Path.Combine(checkpointDir, "tiers.json");
        File.WriteAllText(checkpoint, "<runnerFixture segment='" + segment + "'/>");
        File.WriteAllText(tiers, "{\"records\":[]}");
        Write(Path.Combine(checkpointDir, "checkpoint.json"), Obj("schema_version", 2, "seed", seed,
            "sequence", sequence, "independently_verified", true, "snapshot_file", checkpoint,
            "snapshot_sha256", Hash(checkpoint), "tiers_file", tiers, "tiers_sha256", Hash(tiers)));
        if (mode == "corrupt" && segment == 1)
        {
            string corruptDir = Path.Combine(checkpointRoot, "checkpoint-00000099");
            Directory.CreateDirectory(corruptDir);
            File.WriteAllText(Path.Combine(corruptDir, "checkpoint.json"), "{broken");
        }
        var coverage = Obj("production_steps", 1, "research_steps", 1, "dynasty_checks", 1,
            "health_steps", 1, "weather_days", 1, "military_checks", 1, "physical_battle_steps", 0,
            "abstract_battle_steps", 1, "fire_steps", 1, "capture_attempts", 1,
            "global_travel_steps", 1, "strategic_ai_steps", 1, "event_steps", 1);
        DateTime started = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        Dictionary<string, object> result = null;
        bool fault = mode == "exception_loop" || ((mode == "exception" || mode == "partial") && segment == 1);
        bool stall = mode == "watchdog_loop" || (mode == "watchdog" && segment == 1);
        while (timer.Elapsed.TotalSeconds < duration)
        {
            double elapsed = timer.Elapsed.TotalSeconds;
            var performance = Obj("samples", Math.Max(1, (int)(elapsed * 60)), "p50_ms", 16,
                "p95_ms", 20, "p99_ms", 25, "max_ms", 30, "working_set_bytes", 10000000,
                "managed_heap_bytes", 1000000, "allocated_bytes_per_frame", -1);
            result = Obj("schema_version", 2, "canonical_world_seed", seed, "passed", false, "completed", false, "aborted", false, "end_reason", "running",
                "seed", seed, "segment_index", segment, "actual_wall_clock_seconds", elapsed,
                "productive_simulation_seconds", Math.Floor(elapsed * 4) / 4,
                "critical_errors", 0, "invariant_failures", 0, "recoverable_errors", 0,
                "memory_runaway_detected", false, "memory_metrics_verified", true,
                "checkpoint_resume_verified", resume.Length > 0, "final_state_hash", "",
                "performance_samples", performance["samples"], "performance", performance,
                "coverage", coverage, "latest_checkpoint", checkpoint,
                "performance_summary_kind", "worst_interval_percentiles", "summary", "runner fixture");
            Write(Path.Combine(output, "segment-progress.json"), result);
            File.AppendAllText(Path.Combine(output, "heartbeats.jsonl"), Json.Serialize(Obj("utc", DateTime.UtcNow.ToString("O"))) + "\n");
            if ((fault || stall) && elapsed >= 1)
            {
                if (stall) { Thread.Sleep(60000); return 9; }
                string line = Json.Serialize(Obj("schema_version", 1, "seed", seed, "segment_index", segment,
                    "utc", DateTime.UtcNow.ToString("O"), "kind", "recoverable_exception", "critical", true,
                    "checkpoint", checkpoint, "calendar_ticks", 123, "state_hash", "fixture-hash",
                    "message", "fixture gameplay failure", "exception", "fixture stack")) + "\n";
                string incidents = Path.Combine(output, "incidents.jsonl");
                if (mode == "partial")
                {
                    File.AppendAllText(incidents, line.Substring(0, line.Length / 2));
                    Thread.Sleep(700);
                    File.AppendAllText(incidents, line.Substring(line.Length / 2));
                }
                else File.AppendAllText(incidents, line);
                result["critical_errors"] = 1;
                result["recoverable_errors"] = 1;
                result["aborted"] = true;
                result["end_reason"] = "gameplay_exception";
                Write(Path.Combine(output, "segment-result.json"), result);
                return 2;
            }
            Thread.Sleep(200);
        }
        result["passed"] = true;
        result["completed"] = true;
        result["end_reason"] = "segment_complete";
        result["productive_simulation_seconds"] = duration;
        result["final_state_hash"] = "FIXTURE-OUTCOME";
        result["performance_summary_kind"] = "exact_segment_percentiles";
        Write(Path.Combine(output, "segment-result.json"), result);
        return mode == "bad_exit" ? 7 : 0;
    }
}
