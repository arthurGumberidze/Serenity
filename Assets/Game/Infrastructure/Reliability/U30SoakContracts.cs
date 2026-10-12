using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;

namespace Game.Infrastructure.Reliability
{
    [Serializable]
    public sealed class U30SoakConfig
    {
        public const int CurrentSchemaVersion = 2;
        public const int DefaultPopulation = 100;
        public const int DefaultStepsPerSecond = 4;
        public const double SimulationRealSecondsPerStep = 1d;

        public int schema_version = CurrentSchemaVersion;
        public int seed;
        public int segment_index;
        public double segment_wall_clock_seconds;
        public int checkpoint_interval_seconds;
        public string output_directory;
        public string checkpoint_root;
        public string resume_from_checkpoint;
        public int steps_per_second = DefaultStepsPerSecond;
        public bool inject_invariant_failure;
        public bool inject_recoverable_exception;
        public bool inject_watchdog_stall;
        public double injection_after_seconds = 2d;

        public int TargetSimulationSteps => checked((int)Math.Round(
            segment_wall_clock_seconds * steps_per_second, MidpointRounding.AwayFromZero));

        public static bool IsRequested(string[] args) => HasArg(args, "-u30Soak");

        public static long ResolveWorldSeed(string[] args, long fallback) =>
            IsRequested(args) ? IntArg(args, "-u30Seed") : fallback;

        public static U30SoakConfig Parse(string[] args)
        {
            if (!IsRequested(args)) return null;
            var config = new U30SoakConfig
            {
                seed = IntArg(args, "-u30Seed"),
                segment_index = IntArg(args, "-u30SegmentIndex"),
                segment_wall_clock_seconds = DoubleArg(args, "-u30SegmentSeconds"),
                checkpoint_interval_seconds = IntArg(args, "-u30CheckpointIntervalSeconds"),
                output_directory = StringArg(args, "-u30OutputDirectory"),
                checkpoint_root = StringArg(args, "-u30CheckpointRoot"),
                resume_from_checkpoint = OptionalStringArg(args, "-u30ResumeFromCheckpoint"),
                steps_per_second = OptionalIntArg(args, "-u30StepsPerSecond", DefaultStepsPerSecond),
                inject_invariant_failure = HasArg(args, "-u30InjectInvariantFailure"),
                inject_recoverable_exception = HasArg(args, "-u30InjectRecoverableException"),
                inject_watchdog_stall = HasArg(args, "-u30InjectWatchdogStall"),
                injection_after_seconds = OptionalDoubleArg(args, "-u30InjectionAfterSeconds", 2d)
            };
            config.Validate();
            return config;
        }

        public void Validate()
        {
            if (schema_version != CurrentSchemaVersion)
                throw new InvalidOperationException("Unsupported U30 soak config version.");
            if (segment_index < 1) throw new InvalidOperationException("U30 segment index must be positive.");
            if (!FinitePositive(segment_wall_clock_seconds))
                throw new InvalidOperationException("U30 segment duration must be finite and positive.");
            if (segment_wall_clock_seconds > 1800d)
                throw new InvalidOperationException("U30 segments are bounded to thirty minutes.");
            if (checkpoint_interval_seconds < 1)
                throw new InvalidOperationException("U30 checkpoint interval must be positive.");
            if (steps_per_second < 1 || steps_per_second > 60)
                throw new InvalidOperationException("U30 steps per second must be between 1 and 60.");
            if (!FinitePositive(injection_after_seconds))
                throw new InvalidOperationException("U30 injection time must be finite and positive.");
            RequireAbsolute(output_directory, "U30 segment output directory");
            RequireAbsolute(checkpoint_root, "U30 checkpoint root");
            if (!string.IsNullOrWhiteSpace(resume_from_checkpoint))
                RequireAbsolute(resume_from_checkpoint, "U30 resume checkpoint");
        }

        private static void RequireAbsolute(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
                throw new InvalidOperationException(label + " must be absolute.");
        }

        private static bool FinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

        private static bool HasArg(IEnumerable<string> args, string key) =>
            args != null && args.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));

        private static string StringArg(string[] args, string key)
        {
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            throw new ArgumentException("Missing U30 argument " + key + ".");
        }

        private static string OptionalStringArg(string[] args, string key) =>
            HasArg(args, key) ? StringArg(args, key) : string.Empty;

        private static int IntArg(string[] args, string key) =>
            int.Parse(StringArg(args, key), CultureInfo.InvariantCulture);

        private static int OptionalIntArg(string[] args, string key, int fallback) =>
            HasArg(args, key) ? IntArg(args, key) : fallback;

        private static double DoubleArg(string[] args, string key) =>
            double.Parse(StringArg(args, key), CultureInfo.InvariantCulture);

        private static double OptionalDoubleArg(string[] args, string key, double fallback) =>
            HasArg(args, key) ? DoubleArg(args, key) : fallback;
    }

    [Serializable]
    public sealed class U30FrameSummary
    {
        public int samples;
        public double p50_ms;
        public double p95_ms;
        public double p99_ms;
        public double max_ms;
        public double main_thread_average_ms = -1d;
        public double render_thread_average_ms = -1d;
        public double gpu_average_ms = -1d;
        public double allocated_bytes_per_frame = -1d;
        public int collections;
        public double process_cpu_percent;
        public long working_set_bytes;
        public long managed_heap_bytes;
    }

    [Serializable]
    public sealed class U30Coverage
    {
        public long production_steps;
        public long research_steps;
        public long dynasty_checks;
        public long health_steps;
        public long weather_days;
        public long military_checks;
        public long physical_battle_steps;
        public long abstract_battle_steps;
        public long fire_steps;
        public long capture_attempts;
        public long global_travel_steps;
        public long strategic_ai_steps;
        public long event_steps;
        public long production_output_units;
        public long research_active_steps;
        public long burning_steps;
        public long active_travel_steps;
        public long successful_captures;
        public long tier1_steps;
        public long tier2_steps;
        public long tier3_steps;

        public bool HasObservedWorkloads => production_output_units > 0 && research_active_steps > 0 &&
            burning_steps > 0 && active_travel_steps > 0 && successful_captures > 0 &&
            physical_battle_steps > 0 && abstract_battle_steps > 0 &&
            tier1_steps > 0 && tier2_steps > 0 && tier3_steps > 0;

        public bool CoversRequiredSystems => production_steps > 0 && research_steps > 0 && dynasty_checks > 0 &&
            health_steps > 0 && weather_days > 0 && military_checks > 0 &&
            physical_battle_steps + abstract_battle_steps > 0 && fire_steps > 0 && capture_attempts > 0 &&
            global_travel_steps > 0 && strategic_ai_steps > 0 && event_steps > 0;
    }

    [Serializable]
    public sealed class U30HeartbeatRecord
    {
        public int schema_version = U30SoakConfig.CurrentSchemaVersion;
        public int segment_index;
        public long sequence;
        public string utc;
        public double wall_clock_seconds;
        public int simulation_steps;
        public long calendar_ticks;
        public string game_date;
        public int population;
        public long resource_units;
        public int active_battles;
        public int ai_factions;
        public string current_hash;
        public string current_hash_scope = "last_verified_checkpoint_not_live_world";
        public int tier1_count;
        public int tier2_count;
        public int tier3_count;
        public int active_health_conditions;
        public int retained_health_conditions;
        public string last_checkpoint;
        public U30FrameSummary performance;
        public U30Coverage coverage;
        public int performance_samples;
        public bool checkpoint_resume_verified;
        public bool memory_runaway_detected;
        public bool memory_metrics_verified;
    }

    [Serializable]
    public sealed class U30IncidentRecord
    {
        public int schema_version = U30SoakConfig.CurrentSchemaVersion;
        public int segment_index;
        public string utc;
        public string kind;
        public bool critical;
        public long calendar_ticks;
        public string state_hash;
        public string checkpoint;
        public string message;
        public string exception;
        public string reproduction_command;
        public int seed;
        public string scenario;
    }

    [Serializable]
    public sealed class U30TierRuntimeRecord
    {
        public string character_id;
        public int tier;
        public float position_x;
        public float position_y;
        public float position_z;
        public float units_per_day_x;
        public float units_per_day_y;
        public float units_per_day_z;
        public bool is_moving;
        public string location_key;
        public long processed_calendar_ticks;
        public long processed_biological_ticks;
        public uint step_count;
        public double hunger;
        public double energy;
        public long last_simulation_calendar_tick;
        public long last_simulation_biological_tick;
        public ulong off_camera_step_count;
        public ulong deterministic_accumulator;
        public long abstract_activity_progress;
        // Unity's JSON number round-trip is not bit-exact for doubles. This is the
        // authoritative numeric payload; the numeric fields above remain readable telemetry.
        public string numeric_bits;

        public static U30TierRuntimeRecord Capture(CharacterSimulationTier currentTier,
            CharacterRuntimeState state) => new U30TierRuntimeRecord
        {
            character_id = state.CharacterId.ToString(), tier = (int)currentTier,
            position_x = state.Position.X, position_y = state.Position.Y, position_z = state.Position.Z,
            units_per_day_x = state.UnitsPerGameDay.X, units_per_day_y = state.UnitsPerGameDay.Y,
            units_per_day_z = state.UnitsPerGameDay.Z, is_moving = state.IsMoving,
            location_key = state.LocationKey, processed_calendar_ticks = state.ProcessedCalendarTicks,
            processed_biological_ticks = state.ProcessedBiologicalTicks, step_count = state.StepCount,
            hunger = state.Hunger, energy = state.Energy,
            last_simulation_calendar_tick = state.LastSimulationCalendarTick,
            last_simulation_biological_tick = state.LastSimulationBiologicalTick,
            off_camera_step_count = state.OffCameraStepCount,
            deterministic_accumulator = state.DeterministicAccumulator,
            abstract_activity_progress = state.AbstractActivityProgress,
            numeric_bits = EncodeNumbers(state)
        };

        private static string EncodeNumbers(CharacterRuntimeState state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(state.Position.X); writer.Write(state.Position.Y); writer.Write(state.Position.Z);
                writer.Write(state.UnitsPerGameDay.X); writer.Write(state.UnitsPerGameDay.Y); writer.Write(state.UnitsPerGameDay.Z);
                writer.Write(state.Hunger); writer.Write(state.Energy);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public CharacterRuntimeState ToState()
        {
            var bytes = Convert.FromBase64String(numeric_bits ?? string.Empty);
            if (bytes.Length != 40) throw new InvalidDataException("Missing or invalid exact U30 numeric payload.");
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
                return new CharacterRuntimeState(StableEntityId.Parse(character_id),
                    new WorldPosition(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    new WorldPosition(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), is_moving, location_key,
                    processed_calendar_ticks, processed_biological_ticks, step_count, reader.ReadDouble(), reader.ReadDouble(),
                    last_simulation_calendar_tick, last_simulation_biological_tick, off_camera_step_count,
                    deterministic_accumulator, abstract_activity_progress);
        }
    }

    [Serializable]
    public sealed class U30TierRuntimeCollection
    {
        public U30TierRuntimeRecord[] records = Array.Empty<U30TierRuntimeRecord>();
    }

    [Serializable]
    public sealed class U30CheckpointMetadata
    {
        public string canonical_hash_kind = U30Evidence.CanonicalHashKind;
        public int schema_version = U30SoakConfig.CurrentSchemaVersion;
        public int seed;
        public int segment_index;
        public long sequence;
        public string created_utc;
        public string snapshot_file;
        public string snapshot_sha256;
        public string tiers_file;
        public string tiers_sha256;
        public string canonical_state_hash;
        public long calendar_ticks;
        public int simulation_steps;
        public bool independently_verified;
    }

    [Serializable]
    public sealed class U30SegmentResult
    {
        public string build_guid;
        public long canonical_world_seed;
        public string outcome_hash_kind = U30Evidence.OutcomeHashKind;
        public string memory_scope = "bounded_process_segment_not_continuous_8h";
        public string frame_scope = "post_bootstrap_first_delta_excluded";
        public int schema_version = U30SoakConfig.CurrentSchemaVersion;
        public bool passed;
        public int seed;
        public int segment_index;
        public string started_utc;
        public string completed_utc;
        public double actual_wall_clock_seconds;
        public double productive_simulation_seconds;
        public int simulation_steps;
        public long calendar_ticks;
        public int critical_errors;
        public int invariant_failures;
        public int watchdog_failures;
        public int recoverable_errors;
        public bool memory_runaway_detected;
        public bool checkpoint_resume_verified;
        public int performance_samples;
        public string final_state_hash;
        public string latest_checkpoint;
        public string incident_journal;
        public string heartbeat_journal;
        public string performance_journal;
        public U30FrameSummary performance;
        public U30Coverage coverage;
        public string summary;
        public bool completed;
        public bool aborted;
        public string end_reason;
        public string performance_summary_kind;
        public bool memory_metrics_verified;
        public string raw_frames_journal;
    }

    [Serializable]
    public sealed class U30AggregateResult
    {
        public bool passed;
        public int seed;
        public int requested_wall_clock_seconds;
        public int actual_wall_clock_seconds;
        public string started_utc;
        public string completed_utc;
        public int critical_errors;
        public int invariant_failures;
        public int watchdog_failures;
        public bool memory_runaway_detected;
        public bool memory_metrics_verified;
        public bool checkpoint_resume_verified;
        public string final_state_hash;
        public string summary;
        public int recoverable_errors;
        public int player_restarts;
        public int segments_completed;
        public double productive_simulation_seconds;
        public long performance_samples;
        public U30Coverage coverage;
        public string first_failure_utc;
        public string last_failure_utc;
        public string incident_journal_path;
        public string incident_journal_sha256;
        public string segment_manifest_path;
        public string segment_manifest_sha256;
    }

    public static class U30SoakResultValidator
    {
        public static string[] Validate(U30AggregateResult result, bool officialGate)
        {
            if (result == null) return new[] { "missing_result" };
            var failures = new List<string>();
            if (result.requested_wall_clock_seconds <= 0) failures.Add("invalid_requested_duration");
            if (officialGate && result.requested_wall_clock_seconds < 28800) failures.Add("official_duration_below_8h");
            if (result.actual_wall_clock_seconds < result.requested_wall_clock_seconds) failures.Add("short_actual_duration");
            if (result.productive_simulation_seconds < result.requested_wall_clock_seconds)
                failures.Add("short_productive_duration");
            if (string.IsNullOrWhiteSpace(result.started_utc) || string.IsNullOrWhiteSpace(result.completed_utc) ||
                !DateTime.TryParse(result.started_utc, null, DateTimeStyles.RoundtripKind, out _) ||
                !DateTime.TryParse(result.completed_utc, null, DateTimeStyles.RoundtripKind, out _))
                failures.Add("invalid_timestamps");
            if (result.critical_errors != 0) failures.Add("critical_errors");
            if (result.invariant_failures != 0) failures.Add("invariant_failures");
            if (result.watchdog_failures != 0) failures.Add("watchdog_failures");
            if (result.memory_runaway_detected) failures.Add("memory_runaway");
            if (!result.memory_metrics_verified) failures.Add("memory_metrics_unverified");
            if (!result.checkpoint_resume_verified) failures.Add("checkpoint_resume_unverified");
            if (string.IsNullOrWhiteSpace(result.final_state_hash)) failures.Add("missing_final_state_hash");
            if (result.segments_completed < 1 || result.performance_samples < 1)
                failures.Add("missing_segment_or_performance_evidence");
            if (result.coverage == null || !result.coverage.CoversRequiredSystems)
                failures.Add("incomplete_system_coverage");
            if (officialGate && (result.coverage == null || !result.coverage.HasObservedWorkloads))
                failures.Add("incomplete_observed_workloads");
            if (string.IsNullOrWhiteSpace(result.incident_journal_path) ||
                string.IsNullOrWhiteSpace(result.incident_journal_sha256) ||
                string.IsNullOrWhiteSpace(result.segment_manifest_path) ||
                string.IsNullOrWhiteSpace(result.segment_manifest_sha256))
                failures.Add("missing_evidence_paths_or_hashes");
            if (result.passed && failures.Count != 0) failures.Add("passed_flag_contradicts_evidence");
            if (!result.passed && failures.Count == 0) failures.Add("failed_flag_without_failure");
            return failures.ToArray();
        }
    }
}
