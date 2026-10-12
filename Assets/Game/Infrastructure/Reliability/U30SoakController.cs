using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Infrastructure.Performance;
using Game.Simulation;
using Game.Simulation.Time;
using Unity.Profiling;
using UnityEngine;

namespace Game.Infrastructure.Reliability
{
    [DefaultExecutionOrder(30000)]
    public sealed class U30SoakController : MonoBehaviour
    {
        private const double HeartbeatIntervalSeconds = 1d;
        private const int MaximumFrameSamples = 262144;
        private const long JournalRotationBytes = 32L * 1024L * 1024L;
        private static readonly StableEntityId CheckpointSlotId =
            StableEntityId.Parse("00000000000000000000000000003000");

        private LocalSceneCompositionRoot root;
        private U30SoakConfig config;
        private SaveCoordinator checkpointCoordinator;
        private readonly U30Coverage coverage = new U30Coverage();
        private readonly List<double> intervalFrames = new List<double>(512);
        private readonly List<double> allFrames = new List<double>(131072);
        private readonly List<long> managedSamples = new List<long>();
        private readonly List<long> workingSetSamples = new List<long>();
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder renderThreadRecorder;
        private ProfilerRecorder gpuRecorder;
        private ProfilerRecorder allocationRecorder;
        private Process process;
        private DateTime startedUtc;
        private double startedRealtime;
        private double nextHeartbeat;
        private double nextCheckpoint;
        private long heartbeatSequence;
        private long checkpointSequence;
        private int simulationSteps;
        private int criticalErrors;
        private int invariantFailures;
        private int recoverableErrors;
        private int collectionsAtStart;
        private int intervalCollections;
        private int previousCollectionCount;
        private long previousCpuTicks;
        private double previousCpuRealtime;
        private double intervalCpuPercent;
        private bool memoryRunaway;
        private bool resumeVerified;
        private bool invariantInjected;
        private bool exceptionInjected;
        private bool watchdogInjected;
        private bool captureAttempted;
        private bool finished;
        private bool memoryMetricsVerified;
        private int previousTargetFrameRate;
        private int previousVSyncCount;
        private int journalRotation = 1;
        private string rawFramesPath;
        private U30FrameSummary worstInterval = new U30FrameSummary();
        private string latestCheckpoint = string.Empty;
        private string heartbeatPath;
        private string performancePath;
        private string incidentPath;
        private string startedUtcText;
        private long observedWeatherDay = long.MinValue;

        public static U30SoakController AttachIfRequested(LocalSceneCompositionRoot compositionRoot)
        {
            var parsed = U30SoakConfig.Parse(Environment.GetCommandLineArgs());
            if (parsed == null) return null;
            var controller = compositionRoot.gameObject.AddComponent<U30SoakController>();
            controller.Initialize(compositionRoot, parsed);
            return controller;
        }

        public void Initialize(LocalSceneCompositionRoot compositionRoot, U30SoakConfig soakConfig)
        {
            root = compositionRoot ?? throw new ArgumentNullException(nameof(compositionRoot));
            config = soakConfig ?? throw new ArgumentNullException(nameof(soakConfig));
            config.Validate();
            Directory.CreateDirectory(config.output_directory);
            Directory.CreateDirectory(config.checkpoint_root);
            heartbeatPath = Path.Combine(config.output_directory, "heartbeats.jsonl");
            performancePath = Path.Combine(config.output_directory, "performance-0001.jsonl");
            incidentPath = Path.Combine(config.output_directory, "incidents.jsonl");
            rawFramesPath = Path.Combine(config.output_directory, "frames-0001.jsonl");
            File.WriteAllText(Path.Combine(config.output_directory, "performance-index.jsonl"),
                "{\"file\":\"performance-0001.jsonl\",\"frames\":\"frames-0001.jsonl\",\"rotation\":1}\n", new UTF8Encoding(false));

            var benchmarkConfig = new U29BenchmarkConfig
            {
                population = U30SoakConfig.DefaultPopulation,
                tier1Count = 25,
                tier2Count = 50,
                tier3Count = 25,
                repeatIndex = 1,
                repeatCount = 1,
                warmupSeconds = 1d,
                measurementSeconds = Math.Max(1d, config.segment_wall_clock_seconds),
                seed = config.seed,
                outputDirectory = config.output_directory,
                configId = "u30-soak-" + config.seed.ToString(CultureInfo.InvariantCulture),
                baseCommit = "u30",
                mandatoryGate = false,
                detailedDiagnostics = false,
                captureProfilerTrace = false,
                enableTierChurn = true,
                enableSaveObservation = true
            };
            U29BenchmarkScenario.Create(root, benchmarkConfig);
            checkpointCoordinator = root.Saves;
            root.AiRuntime.enabled = false;
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            // The accelerated canonical workload remains four fixed steps/second. An uncapped
            // headless render loop only burns CPU and fills instrumentation buffers.
            previousTargetFrameRate = Application.targetFrameRate;
            previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            if (!string.IsNullOrWhiteSpace(config.resume_from_checkpoint)) RestoreCheckpoint(config.resume_from_checkpoint);
            // Bootstrap finite representative workloads once. Later internal segments resume the
            // canonical outcomes and aggregate their earlier coverage instead of fabricating a
            // replacement battle with participants who may already be dead or unavailable.
            if (config.segment_index == 1 || string.IsNullOrWhiteSpace(config.resume_from_checkpoint))
                EnsureRepresentativeWorkloads();

            startedUtc = DateTime.UtcNow;
            startedUtcText = startedUtc.ToString("O", CultureInfo.InvariantCulture);
            startedRealtime = Time.realtimeSinceStartupAsDouble;
            nextHeartbeat = startedRealtime;
            nextCheckpoint = startedRealtime + config.checkpoint_interval_seconds;
            collectionsAtStart = previousCollectionCount = GC.CollectionCount(0);
            process = Process.GetCurrentProcess();
            previousCpuTicks = process.TotalProcessorTime.Ticks;
            previousCpuRealtime = startedRealtime;
            StartRecorders();
            latestCheckpoint = WriteCheckpoint();
            WriteHeartbeat(0d);
        }

        private void Update()
        {
            if (finished || root == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            var elapsed = now - startedRealtime;
            if (config.inject_watchdog_stall && !watchdogInjected && elapsed >= config.injection_after_seconds)
            {
                watchdogInjected = true;
                return; // The external watchdog owns termination and restart evidence.
            }
            if (watchdogInjected) return;

            try
            {
                ApplyFailureInjection(elapsed);
                var dueSteps = Math.Min(config.TargetSimulationSteps,
                    (int)Math.Floor(elapsed * config.steps_per_second));
                var budget = 16;
                while (simulationSteps < dueSteps && budget-- > 0) AdvanceOneStep();

                if (now >= nextHeartbeat)
                {
                    ValidateInvariants();
                    WriteHeartbeat(elapsed);
                    nextHeartbeat = now + HeartbeatIntervalSeconds;
                }
                if (now >= nextCheckpoint)
                {
                    latestCheckpoint = WriteCheckpoint();
                    nextCheckpoint = now + config.checkpoint_interval_seconds;
                }
                if (elapsed >= config.segment_wall_clock_seconds)
                {
                    while (simulationSteps < config.TargetSimulationSteps) AdvanceOneStep();
                    Complete(elapsed);
                }
            }
            catch (Exception exception)
            {
                criticalErrors++;
                recoverableErrors++;
                RecordIncident("recoverable_exception", true, exception.Message, exception);
                // The clock or earlier callbacks may already have mutated. Never advance or
                // publish a recovery checkpoint from this partially processed world.
                FinishAttempt(elapsed, false, "gameplay_exception");
            }
        }

        private void LateUpdate()
        {
            if (finished || watchdogInjected) return;
            var milliseconds = Time.unscaledDeltaTime * 1000d;
            if (milliseconds <= 0d || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds)) return;
            if (allFrames.Count >= MaximumFrameSamples)
            {
                criticalErrors++;
                recoverableErrors++;
                RecordIncident("frame_buffer_limit", true, "Bounded frame buffer exhausted; restart required.", null);
                FinishAttempt(Time.realtimeSinceStartupAsDouble - startedRealtime, false, "frame_buffer_limit");
                return;
            }
            intervalFrames.Add(milliseconds);
            allFrames.Add(milliseconds);
            var collections = GC.CollectionCount(0);
            intervalCollections += Math.Max(0, collections - previousCollectionCount);
            previousCollectionCount = collections;
        }

        private void AdvanceOneStep()
        {
            // Count the representative combat workload at the committed pre-step barrier. A
            // small abstract encounter can legitimately finish inside this single accelerated
            // step, so observing only afterwards would incorrectly report zero coverage.
            foreach (var battle in root.Combat.Encounters.Where(x => x.Status == CombatEncounterStatus.Active))
            {
                if (battle.Mode == BattleMode.Physical) coverage.physical_battle_steps++;
                else coverage.abstract_battle_steps++;
            }
            root.AiRuntime.AdvanceForDebug(TimeSpan.FromSeconds(U30SoakConfig.SimulationRealSecondsPerStep));
            simulationSteps++;
            coverage.production_steps++;
            coverage.research_steps++;
            coverage.dynasty_checks++;
            coverage.health_steps++;
            coverage.military_checks++;
            coverage.fire_steps++;
            coverage.global_travel_steps++;
            coverage.strategic_ai_steps++;
            coverage.event_steps++;
            var day = GameCalendar.CompletedDays(root.Clock.State.CalendarTicks);
            if (day != observedWeatherDay) { coverage.weather_days++; observedWeatherDay = day; }
            TryExerciseCapture();
        }

        private void EnsureRepresentativeWorkloads()
        {
            var now = root.Clock.State.CalendarTicks;
            if (!root.HealthFamily.Pregnancies.Any(x => x.Status == PregnancyStatus.Active))
            {
                try
                {
                    root.HealthFamily.StartPregnancy(root.FemaleDemoCharacter.Id, root.MaleDemoCharacter.Id,
                        now, root.Clock.State.BiologicalTicks);
                }
                catch (InvalidOperationException) { }
            }
            if (!root.HealthFamily.Conditions.Any(x => x.TargetId == root.FemaleDemoCharacter.Id))
            {
                try { root.HealthFamily.ApplyCondition(root.FemaleDemoCharacter.Id, new HealthConditionId("minor_cut"), now); }
                catch (InvalidOperationException) { }
            }
            if (!root.Combat.Encounters.Any(x => x.Status == CombatEncounterStatus.Active))
            {
                try { root.StartDemoUnitBattle(BattleMode.Abstract); }
                catch (InvalidOperationException exception)
                {
                    RecordIncident("workload_setup", false, exception.Message, exception);
                }
            }
            var fireBuilding = root.DemoCropPlotBuilding ?? root.DemoStorageBuilding;
            if (fireBuilding != null && fireBuilding.FireStatus != BuildingFireStatus.Burning && !fireBuilding.IsDestroyed)
                root.BuildingDamage.IgniteBuilding(fireBuilding.Id, 120, now);
        }

        private void TryExerciseCapture()
        {
            if (captureAttempted) return;
            var finishedBattle = root.Combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Finished);
            if (finishedBattle == null)
            {
                var active = root.Combat.Encounters.FirstOrDefault(x => x.Status == CombatEncounterStatus.Active);
                if (active != null)
                {
                    root.BuildingDamage.EvaluateCapture(root.DemoStorageBuilding.Id,
                        new CombatSideId("enemy"), active);
                    coverage.capture_attempts++;
                }
                return;
            }

            captureAttempted = true;
            coverage.capture_attempts++;
            var victor = finishedBattle.Outcome == CombatOutcome.SideAVictory ? finishedBattle.SideA :
                finishedBattle.Outcome == CombatOutcome.SideBVictory ? finishedBattle.SideB : default;
            if (!victor.IsValid) return;
            var target = root.Buildings.All.FirstOrDefault(x => !x.IsDestroyed && x.OwnerSideId != victor);
            if (target != null)
                root.BuildingDamage.CaptureBuilding(target.Id, victor, finishedBattle, root.Clock.State.CalendarTicks);
        }

        private void ApplyFailureInjection(double elapsed)
        {
            if (config.inject_invariant_failure && !invariantInjected && elapsed >= config.injection_after_seconds)
            {
                invariantInjected = true;
                invariantFailures++;
                criticalErrors++;
                RecordIncident("invariant_failure", true, "Injected U30 invariant failure.", null);
            }
            if (config.inject_recoverable_exception && !exceptionInjected && elapsed >= config.injection_after_seconds)
            {
                exceptionInjected = true;
                throw new InvalidOperationException("Injected U30 recoverable gameplay exception.");
            }
        }

        private void ValidateInvariants()
        {
            var failures = U30InvariantMonitor.Validate(root);
            foreach (var failure in failures)
            {
                invariantFailures++;
                criticalErrors++;
                RecordIncident("invariant_failure", true, failure, null);
            }
        }

        private void RestoreCheckpoint(string checkpointPath)
        {
            var fullPath = Path.GetFullPath(checkpointPath);
            var directory = Path.GetDirectoryName(fullPath);
            var metadataPath = Path.Combine(directory, "checkpoint.json");
            var tiersPath = Path.Combine(directory, "tiers.json");
            if (!File.Exists(fullPath) || !File.Exists(metadataPath) || !File.Exists(tiersPath))
                throw new InvalidDataException("U30 resume checkpoint bundle is incomplete.");
            var metadata = JsonUtility.FromJson<U30CheckpointMetadata>(File.ReadAllText(metadataPath, Encoding.UTF8));
            if (metadata == null || !metadata.independently_verified || metadata.seed != config.seed)
                throw new InvalidDataException("U30 resume checkpoint metadata is invalid.");
            if (!string.Equals(Sha256(fullPath), metadata.snapshot_sha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Sha256(tiersPath), metadata.tiers_sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("U30 resume checkpoint hash mismatch.");

            var codec = new SaveXmlCodec();
            var snapshot = codec.Deserialize(File.ReadAllBytes(fullPath));
            var tierCollection = JsonUtility.FromJson<U30TierRuntimeCollection>(
                File.ReadAllText(tiersPath, Encoding.UTF8));
            if (snapshot.DynastyWorld == null || tierCollection == null)
                throw new InvalidDataException("U30 checkpoint lacks canonical characters or tier state.");

            // Long segments may contain births. Seed the missing canonical identities without
            // creating a representation yet so CharacterRegistry's transactional restore can
            // validate the complete identity set. Representation materialization is deferred
            // until after the restored clock is rebound.
            var snapshotIds = new HashSet<StableEntityId>(snapshot.DynastyWorld.Characters.Select(x => x.Id));
            if (root.Characters.GetAll().Any(x => !snapshotIds.Contains(x.Id)))
                throw new InvalidDataException("U30 checkpoint is missing a bootstrap character identity.");
            foreach (var state in snapshot.DynastyWorld.Characters)
                if (!root.Characters.TryGet(state.Id, out _)) root.Characters.Add(Character.Restore(state));

            var store = new SnapshotStore(snapshot);
            checkpointCoordinator = new SaveCoordinator(snapshot.SessionId, root.Clock, store,
                root.Production, root.Research, root.Succession, root.HealthFamily, root.Weather, root.Combat,
                root.MilitaryUnits, root.PlayerControl, root.BuildingDamage, root.GlobalMap, root.StrategicAi,
                root.Events, root.CampaignProgress);
            checkpointCoordinator.Load(CheckpointSlotId);
            root.RebindAfterLoad(checkpointCoordinator.Clock);

            // Restore the canonical clock and character registry before materializing checkpointed
            // representations. Off-camera adapters reject states whose processed ticks are ahead of
            // the currently bound clock, and dynasty restore may replace canonical Character objects.
            var records = tierCollection.records.ToDictionary(x => StableEntityId.Parse(x.character_id));
            if (root.Characters.Count != snapshot.DynastyWorld.Characters.Count)
                throw new InvalidDataException("U30 checkpoint character identity set does not match restored state.");
            foreach (var state in snapshot.DynastyWorld.Characters)
            {
                if (!records.TryGetValue(state.Id, out var record))
                    throw new InvalidDataException("U30 checkpoint lacks tier state for " + state.Id + ".");
                if (!root.Tiers.TryGetTier(state.Id, out _))
                    root.Tiers.Materialize(root.Characters.Get(state.Id), (CharacterSimulationTier)record.tier,
                        record.ToState());
            }
            foreach (var record in records.Values.OrderBy(x => x.character_id, StringComparer.Ordinal))
                root.Tiers.RestoreRepresentationState(StableEntityId.Parse(record.character_id),
                    (CharacterSimulationTier)record.tier, record.ToState());
            root.AiRuntime.SetPaused(false);
            root.AiRuntime.SetSpeed(GameSpeed.Maximum);
            var restoredHash = ComputeLogicalStateHash();
            if (!string.Equals(restoredHash, metadata.canonical_state_hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("U30 checkpoint logical hash changed during resume.");
            checkpointSequence = metadata.sequence;
            latestCheckpoint = fullPath;
            resumeVerified = true;
        }

        private string WriteCheckpoint()
        {
            var sequence = ++checkpointSequence;
            var directory = Path.Combine(config.checkpoint_root,
                "checkpoint-" + sequence.ToString("D8", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directory);
            var snapshotPath = Path.Combine(directory, "checkpoint.xml");
            var tiersPath = Path.Combine(directory, "tiers.json");
            var metadataPath = Path.Combine(directory, "checkpoint.json");
            var snapshot = checkpointCoordinator.Capture();
            var codec = new SaveXmlCodec();
            WriteAtomic(snapshotPath, codec.Serialize(snapshot));
            codec.Deserialize(File.ReadAllBytes(snapshotPath));
            var tiers = CaptureTiers();
            WriteAtomic(tiersPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(tiers, true)));
            JsonUtility.FromJson<U30TierRuntimeCollection>(File.ReadAllText(tiersPath, Encoding.UTF8));
            var metadata = new U30CheckpointMetadata
            {
                seed = config.seed,
                segment_index = config.segment_index,
                sequence = sequence,
                created_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                snapshot_file = snapshotPath,
                snapshot_sha256 = Sha256(snapshotPath),
                tiers_file = tiersPath,
                tiers_sha256 = Sha256(tiersPath),
                canonical_state_hash = ComputeLogicalStateHash(snapshot),
                calendar_ticks = snapshot.CalendarTicks,
                simulation_steps = simulationSteps,
                independently_verified = true
            };
            WriteAtomic(metadataPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(metadata, true)));
            AppendDurable(Path.Combine(config.checkpoint_root, "checkpoint-index.jsonl"),
                JsonUtility.ToJson(metadata, false) + "\n");
            return snapshotPath;
        }

        private U30TierRuntimeCollection CaptureTiers()
        {
            var records = root.Tiers.ManagedIds.Select(id => U30TierRuntimeRecord.Capture(
                root.Tiers.GetTier(id), root.Tiers.GetRuntimeState(id))).ToArray();
            return new U30TierRuntimeCollection { records = records };
        }

        private string ComputeLogicalStateHash() => ComputeLogicalStateHash(checkpointCoordinator.Capture());

        private string ComputeLogicalStateHash(SaveSnapshot snapshot)
        {
            var codec = new SaveXmlCodec();
            var snapshotBytes = codec.Serialize(snapshot);
            var tiers = new StringBuilder();
            foreach (var id in root.Tiers.ManagedIds)
                tiers.Append(id).Append('|').Append((int)root.Tiers.GetTier(id)).Append('\n');
            using (var hash = SHA256.Create())
            {
                hash.TransformBlock(snapshotBytes, 0, snapshotBytes.Length, null, 0);
                var tierBytes = Encoding.UTF8.GetBytes(tiers.ToString());
                hash.TransformFinalBlock(tierBytes, 0, tierBytes.Length);
                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            }
        }

        /// <summary>
        /// Cross-process outcome fingerprint. Canonical New Game is deterministic, while this
        /// normalized form remains compatible with older checkpoints that contain generated IDs.
        /// It replaces 128-bit identity leaves, sorts structured children, and adds the complete
        /// representation-neutral tier payload without character IDs.
        /// </summary>
        private string ComputeRepeatableOutcomeHash()
        {
            var codec = new SaveXmlCodec();
            XDocument document;
            var xmlBytes = codec.Serialize(checkpointCoordinator.Capture());
            using (var stream = new MemoryStream(xmlBytes, false))
                document = XDocument.Load(stream, LoadOptions.None);
            // Compare the multiset of logical scalar facts. Hierarchical ownership may differ in
            // the legacy bootstrap solely because pre-U30A Guid identities affect deterministic
            // iteration order (for example, which equivalent gathering site receives a recipe).
            // Exact hierarchy/identity is still protected by canonical_state_hash on every resume.
            var leaves = document.Root.DescendantsAndSelf().Where(x => !x.HasElements)
                .Select(x => x.Name.LocalName + "=" + NormalizeIdentityLeaf(x.Value))
                .OrderBy(x => x, StringComparer.Ordinal);
            // Tier runtime includes frame-scheduled presentation interpolation and is therefore
            // deliberately excluded from the cross-process logical outcome hash. Its exact values
            // are still included in canonical_state_hash and validated on every checkpoint resume.
            var builder = new StringBuilder(string.Join("\n", leaves));
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())))
                    .Replace("-", string.Empty);
        }

        private static string NormalizeIdentityLeaf(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length == 32 && value.All(Uri.IsHexDigit)) value = "<stable-id>";
            return value;
        }

        private void WriteHeartbeat(double elapsed)
        {
            var summary = CaptureFrameSummary(intervalFrames);
            worstInterval.samples = allFrames.Count;
            worstInterval.p50_ms = Math.Max(worstInterval.p50_ms, summary.p50_ms);
            worstInterval.p95_ms = Math.Max(worstInterval.p95_ms, summary.p95_ms);
            worstInterval.p99_ms = Math.Max(worstInterval.p99_ms, summary.p99_ms);
            worstInterval.max_ms = Math.Max(worstInterval.max_ms, summary.max_ms);
            var heartbeat = new U30HeartbeatRecord
            {
                segment_index = config.segment_index,
                sequence = ++heartbeatSequence,
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                wall_clock_seconds = elapsed,
                simulation_steps = simulationSteps,
                calendar_ticks = root.Clock.State.CalendarTicks,
                game_date = "Y" + GameCalendar.Year(root.Clock.State.CalendarTicks) + " D" +
                    GameCalendar.DayOfYear(root.Clock.State.CalendarTicks) + " " +
                    GameCalendar.HourOfDay(root.Clock.State.CalendarTicks).ToString("D2") + ":" +
                    GameCalendar.MinuteOfHour(root.Clock.State.CalendarTicks).ToString("D2"),
                population = root.Characters.Count,
                resource_units = TotalResources(),
                active_battles = root.Combat.Encounters.Count(x => x.Status == CombatEncounterStatus.Active),
                ai_factions = root.StrategicAi.Factions.Count,
                current_hash = ComputeLogicalStateHash(),
                last_checkpoint = latestCheckpoint,
                performance = summary,
                coverage = coverage,
                performance_samples = allFrames.Count,
                checkpoint_resume_verified = resumeVerified,
                memory_runaway_detected = memoryRunaway,
                memory_metrics_verified = memoryMetricsVerified
            };
            AppendDurable(heartbeatPath, JsonUtility.ToJson(heartbeat, false) + "\n");
            AppendDurable(performancePath, JsonUtility.ToJson(summary, false) + "\n");
            AppendDurable(rawFramesPath, JsonUtility.ToJson(new FrameBatch
                { sequence = heartbeatSequence, frame_ms = intervalFrames.ToArray() }, false) + "\n");
            var progress = CreateResult(elapsed, worstInterval, false, "running");
            progress.performance_summary_kind = "worst_interval_percentiles";
            WriteAtomic(Path.Combine(config.output_directory, "segment-progress.json"),
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(progress, true)));
            intervalFrames.Clear();
            intervalCollections = 0;
            if (new FileInfo(rawFramesPath).Length >= JournalRotationBytes)
            {
                journalRotation++;
                rawFramesPath = Path.Combine(config.output_directory, "frames-" + journalRotation.ToString("D4") + ".jsonl");
                performancePath = Path.Combine(config.output_directory, "performance-" + journalRotation.ToString("D4") + ".jsonl");
                AppendDurable(Path.Combine(config.output_directory, "performance-index.jsonl"),
                    "{\"file\":\"" + Path.GetFileName(performancePath) + "\",\"frames\":\"" +
                    Path.GetFileName(rawFramesPath) + "\",\"rotation\":" + journalRotation + "}\n");
            }
        }

        private long TotalResources()
        {
            long total = 0;
            foreach (var inventory in root.Inventories.All)
                foreach (var amount in inventory.CaptureState().Amounts) total = checked(total + amount.Quantity.Units);
            foreach (var faction in root.StrategicAi.Factions)
                foreach (var inventory in faction.Inventories.All)
                    foreach (var amount in inventory.CaptureState().Amounts) total = checked(total + amount.Quantity.Units);
            return total;
        }

        private U30FrameSummary CaptureFrameSummary(IReadOnlyCollection<double> values)
        {
            var ordered = values.Count == 0 ? new[] { 0.001d } : values.OrderBy(x => x).ToArray();
            var now = Time.realtimeSinceStartupAsDouble;
            if (process == null) process = Process.GetCurrentProcess();
            process.Refresh();
            var cpuTicks = process.TotalProcessorTime.Ticks;
            var cpuSeconds = Math.Max(0.001d, now - previousCpuRealtime);
            intervalCpuPercent = (cpuTicks - previousCpuTicks) / TimeSpan.TicksPerSecond / cpuSeconds * 100d;
            previousCpuTicks = cpuTicks;
            previousCpuRealtime = now;
            var workingSet = ReadWorkingSet(process);
            memoryMetricsVerified |= workingSet > 0;
            var managed = GC.GetTotalMemory(false);
            workingSetSamples.Add(workingSet);
            managedSamples.Add(managed);
            if (workingSetSamples.Count >= 3)
            {
                var middle = workingSetSamples[workingSetSamples.Count / 2];
                memoryRunaway |= workingSetSamples[0] > 0 && middle > 0 && workingSet > 0 &&
                    U29BenchmarkStatistics.IsRunawayGrowth(workingSetSamples[0], middle, workingSet,
                    U29BenchmarkConfig.WorkingSetGrowthLimitBytes);
                middle = managedSamples[managedSamples.Count / 2];
                memoryRunaway |= U29BenchmarkStatistics.IsRunawayGrowth(managedSamples[0], middle, managed,
                    U29BenchmarkConfig.ManagedGrowthLimitBytes);
            }
            return new U30FrameSummary
            {
                samples = values.Count,
                p50_ms = Percentile(ordered, 50), p95_ms = Percentile(ordered, 95),
                p99_ms = Percentile(ordered, 99), max_ms = ordered[ordered.Length - 1],
                main_thread_average_ms = RecorderMilliseconds(mainThreadRecorder),
                render_thread_average_ms = RecorderMilliseconds(renderThreadRecorder),
                gpu_average_ms = RecorderMilliseconds(gpuRecorder),
                // This recorder is a last-frame sample, not an interval allocation average.
                allocated_bytes_per_frame = -1d,
                collections = intervalCollections,
                process_cpu_percent = intervalCpuPercent,
                working_set_bytes = workingSet,
                managed_heap_bytes = managed
            };
        }

        private static double Percentile(IReadOnlyList<double> ordered, double percentile)
        {
            if (ordered.Count == 1) return ordered[0];
            var rank = percentile / 100d * (ordered.Count - 1);
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            return lower == upper ? ordered[lower] : ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower);
        }

        private void Complete(double elapsed)
        {
            if (finished) return;
            ValidateInvariants();
            latestCheckpoint = WriteCheckpoint();
            WriteHeartbeat(elapsed);
            FinishAttempt(elapsed, true, "segment_complete");
        }

        private void FinishAttempt(double elapsed, bool completed, string reason)
        {
            if (finished) return;
            finished = true;
            // Tail frames remain durable even when a step fails between scheduled heartbeats.
            if (intervalFrames.Count > 0)
                AppendDurable(rawFramesPath, JsonUtility.ToJson(new FrameBatch
                    { sequence = heartbeatSequence + 1, frame_ms = intervalFrames.ToArray() }, false) + "\n");
            var performance = CaptureFrameSummary(allFrames);
            var result = CreateResult(elapsed, performance, completed, reason);
            result.final_state_hash = completed ? ComputeRepeatableOutcomeHash() : string.Empty;
            WriteAtomic(Path.Combine(config.output_directory, "segment-result.json"),
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(result, true)));
            DisposeRecorders();
            Application.Quit(result.passed ? 0 : 2);
        }

        private U30SegmentResult CreateResult(double elapsed, U30FrameSummary performance, bool completed, string reason)
        {
            // A segment is a recovery boundary, not an independent full-night scenario. Some
            // finite workloads (notably a battle) can complete before later segments start; the
            // external runner aggregates coverage across the whole requested wall-clock run.
            var passed = completed && criticalErrors == 0 && invariantFailures == 0 && !memoryRunaway && memoryMetricsVerified &&
                simulationSteps == config.TargetSimulationSteps;
            var result = new U30SegmentResult
            {
                passed = passed,
                seed = config.seed,
                segment_index = config.segment_index,
                started_utc = startedUtcText,
                completed_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                actual_wall_clock_seconds = elapsed,
                productive_simulation_seconds = simulationSteps / (double)config.steps_per_second,
                simulation_steps = simulationSteps,
                calendar_ticks = root.Clock.State.CalendarTicks,
                critical_errors = criticalErrors,
                invariant_failures = invariantFailures,
                watchdog_failures = 0,
                recoverable_errors = recoverableErrors,
                memory_runaway_detected = memoryRunaway,
                checkpoint_resume_verified = resumeVerified,
                performance_samples = allFrames.Count,
                final_state_hash = string.Empty,
                latest_checkpoint = latestCheckpoint,
                incident_journal = incidentPath,
                heartbeat_journal = heartbeatPath,
                performance_journal = performancePath,
                performance = performance,
                coverage = coverage,
                summary = passed ? "Segment completed without a critical reliability failure." :
                    "Segment retained reliability evidence that fails the U30 gate.",
                completed = completed,
                aborted = !completed && reason != "running",
                end_reason = reason,
                performance_summary_kind = "exact_segment_percentiles",
                memory_metrics_verified = memoryMetricsVerified,
                raw_frames_journal = rawFramesPath
            };
            return result;
        }

        private void RecordIncident(string kind, bool critical, string message, Exception exception)
        {
            var record = new U30IncidentRecord
            {
                seed = config.seed,
                scenario = "u30-representative-100",
                segment_index = config.segment_index,
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                kind = kind,
                critical = critical,
                calendar_ticks = root == null || root.Clock == null ? 0 : root.Clock.State.CalendarTicks,
                state_hash = SafeStateHash(),
                checkpoint = latestCheckpoint,
                message = message ?? string.Empty,
                exception = exception == null ? string.Empty : exception.ToString(),
                reproduction_command = "Tools\\Run-Soak-U30.ps1 -Seed " + config.seed +
                    " -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder <absolute-external-path>"
            };
            AppendDurable(incidentPath, JsonUtility.ToJson(record, false) + "\n");
        }

        private void StartRecorders()
        {
            mainThreadRecorder = StartRecorder(ProfilerCategory.Internal, "Main Thread");
            renderThreadRecorder = StartRecorder(ProfilerCategory.Internal, "Render Thread");
            gpuRecorder = StartRecorder(ProfilerCategory.Render, "GPU Frame Time");
            allocationRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private static ProfilerRecorder StartRecorder(ProfilerCategory category, string marker)
        {
            try { return ProfilerRecorder.StartNew(category, marker, 1); }
            catch { return default; }
        }

        private static double RecorderMilliseconds(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.LastValue > 0 ? recorder.LastValue / 1000000d : -1d;

        private static void WriteAtomic(string path, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void AppendDurable(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = Encoding.UTF8.GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private string SafeStateHash()
        {
            try { return root == null || checkpointCoordinator == null ? string.Empty : ComputeLogicalStateHash(); }
            catch (Exception) { return string.Empty; } // Invalid partial state must not prevent its exception journal.
        }

        [Serializable]
        private sealed class FrameBatch
        {
            public long sequence;
            public double[] frame_ms;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint size;
            public uint pageFaultCount;
            public UIntPtr peakWorkingSet;
            public UIntPtr workingSet;
            public UIntPtr quotaPeakPagedPool, quotaPagedPool, quotaPeakNonPagedPool, quotaNonPagedPool;
            public UIntPtr pageFileUsage, peakPageFileUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr handle, out ProcessMemoryCounters counters, uint size);

        private static long ReadWorkingSet(Process value)
        {
            var bytes = value.WorkingSet64;
            if (bytes > 0) return bytes;
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                var size = (uint)Marshal.SizeOf<ProcessMemoryCounters>();
                if (GetProcessMemoryInfo(value.Handle, out var counters, size))
                    return checked((long)counters.workingSet.ToUInt64());
            }
            return -1;
        }

        private void OnDestroy()
        {
            DisposeRecorders();
            if (config == null) return;
            Application.targetFrameRate = previousTargetFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
        }

        private void DisposeRecorders()
        {
            if (mainThreadRecorder.Valid) mainThreadRecorder.Dispose();
            if (renderThreadRecorder.Valid) renderThreadRecorder.Dispose();
            if (gpuRecorder.Valid) gpuRecorder.Dispose();
            if (allocationRecorder.Valid) allocationRecorder.Dispose();
        }

        private sealed class SnapshotStore : ISaveStore
        {
            private readonly SaveSnapshot snapshot;
            public SnapshotStore(SaveSnapshot value) => snapshot = value ?? throw new ArgumentNullException(nameof(value));
            public void Save(StableEntityId slotId, SaveSnapshot value) => throw new NotSupportedException();
            public SaveSnapshot Load(StableEntityId slotId) => snapshot;
        }
    }
}
