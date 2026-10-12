# U30 HANDOFF

## Task

ID: U30

Title: Automated tests and 8+ hour soak

Status: IN_PROGRESS — run4 completed the eight-hour runner gate, but independent audit found an unreported persistent AI travel loop and acceptance/telemetry gaps (see the final section)

## Implemented

- Command-line-only `U30SoakController` on the normal composition root; normal gameplay startup is unchanged without `-u30Soak`.
- Reused U29's deterministic 100-Character population with exact 25/50/25 tiers and accelerated centralized simulation covering production, research, dynasty/health/birth, weather, military/combat, fire/capture, global travel, strategic AI and events.
- Read-only committed-barrier invariant monitoring for StableEntityId, tier representation, resource ownership/quantity, birth/pregnancy, dynasty/succession, military/combat, building/fire, event exactly-once and AI targets/orders.
- Atomic snapshot-v14 plus exact tier-runtime checkpoints, independent reread/SHA-256 verification, append-only index, exact resume hash, birth-aware restore and transactional TierManager representation restore.
- Fail-soft PowerShell orchestration with real/productive duration, segment restart manifest, heartbeat/performance journals, first-failure reproduction bundle, bounded backoff, verified-checkpoint fallback, watchdog and aggregate fail-closed result.
- Process-scoped Windows sleep guard; display power-off remains allowed and persistent power settings are not changed.
- Separate exact checkpoint hash and cross-process logical outcome hash, documented in D-090.

## Short-run evidence

- Same seed/config repeat A: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-det-h-20261012025410`; PASS, 17 actual seconds, 12 productive seconds, two segments, resume verified, zero critical failures.
- Repeat B: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-det-i-20261012025434`; same PASS metrics.
- Both ended with exact logical hash `38ACBA28253F7E052DAD8696F7F4E0F0CA5BCDFC680339C31413E88BC79FFDE0`.
- Invariant injection: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-invariant-20261012025503`; expected exit 1/FAIL after 18 actual and 12 productive seconds, one invariant/critical incident, two completed segments, one restart, verified resume, one incident line and two manifest lines.
- Watchdog injection: `C:\Users\Lenovo\AppData\Local\Temp\serenity-u30-watchdog-20261012025529`; expected exit 1/FAIL after 28 actual and 12 productive seconds, one watchdog/critical incident, two completed segments after the stalled attempt, two restarts, verified resume, 55,633 retained performance samples, one incident line and three manifest lines.

## Verification

- `& .\Tools\Verify-U30.ps1 -SkipU29Regression`: PowerShell AST validation, Unity compile, U30 EditMode 5/5, U30 PlayMode 2/2 and Windows x64 Mono Development build passed. The final build log reports `Build Finished, Result: Success`, 0 errors and 2 warnings.
- `& .\Tools\Verify-U30.ps1`: U30 5/5 EditMode and 2/2 PlayMode; U29 focused 28/28 EditMode and 16/16 PlayMode; required dependency regressions 158/158 EditMode and 38/38 PlayMode; persistence 35/35; PostgreSQL 25 discovered/25 skipped without configured DB; Windows x64 Mono Development build succeeded with 0 errors/2 warnings. Logs are under `C:\serenity_game\Logs` with timestamps 2026-10-12 02:56–03:01 local.
- Short real Player runs above prove integrated coverage, checkpoint/restart, deterministic logical outcome and fail-soft evidence behavior.

## Official run command

The user launched:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_soak_20261012_run3"
```

First heartbeat: `2026-10-12T00:08:26.7481820Z`. During preparation handoff the run had reached heartbeat sequence 424, 422.477 segment seconds, game date Y1 D12 17:30, 105 Characters, two published checkpoints and zero published aggregate result because the run was still active. This observation is progress only, not acceptance evidence.

## External result audit (2026-10-12)

The command above completed with `passed=false`. Orchestration started at `2026-10-12T00:08:23.6147134Z` and ended at `2026-10-12T01:21:07.6783493Z` (03:08:23–04:21:07 Moscow). Actual/requested duration is 4,364/28,800 seconds. Result fields report six critical errors, five watchdog failures, five recoverable errors, four Player restarts, zero completed segments, zero aggregate productive seconds/performance/coverage, no final hash and `checkpoint_resume_verified=false`. A separately captured PowerShell exit code is unavailable; the saved verdict is decisive FAIL and the runner source selects exit 1 for this result.

At `2026-10-12T00:28:50.4561322Z`, about 20 minutes 27 seconds after orchestration started, the first Player raised `Cannot add a condition to a dead character.` at calendar tick `29376000000000` (Y1 D35). The stack is `U30SoakController.AdvanceOneStep -> Tier1AiRuntimeDriver.Advance -> LocalSceneCompositionRoot.OnSimulationAdvanced -> WeatherService.AdvanceHealthExposureTo -> HealthAndFamilyService.ApplyCondition`. The outdoor roster includes Tier3 managed IDs without a life-state filter; WeatherService checks existing conditions but calls ApplyCondition without excluding dead residents. The health authority correctly rejects this mutation.

U30 catches the exception around the entire step/heartbeat/checkpoint block. The simulation-step counter increments only after the canonical advance returns; however, the clock has already advanced when Weather throws. Every subsequent frame retries, raises the same exception and bypasses heartbeat/checkpoint publication. During the first exception storm the clock moves from completed day 34 to 330.597 without successfully completing the remaining ordered service callbacks. This is partially processed state, not productive soak coverage.

| Attempt | Last heartbeat UTC | First gameplay exception UTC | Retained exception records |
| --- | --- | --- | --- |
| 1 | 00:28:50.1852452 | 00:28:50.4561322 | 42,711 |
| 2 | 00:39:19.3586303 | 00:39:20.2037559 | 35,768 |
| 3 | 00:49:50.2311052 | 00:49:51.0362806 | 34,843 |
| 4 | 01:00:24.7313394 | 01:00:25.5536899 | 34,272 |
| 5 | 01:11:07.1558194 | 01:11:07.9773062 | 35,083 |

Each restored attempt starts at tick `28800000000000` and reaches the same failure after approximately 24 seconds. Its last healthy heartbeat has 92 completed steps and the same state hash `3241BEE87B3F5840BF3EF4FDFBD441CD556E6EED7B6435F85C851448DDD88DFB`. The Player remains alive writing exceptions; after 600 seconds without heartbeat the orchestrator kills/restarts it. After attempt 5, `recovery_exhausted` ends the run. All 182,677 Player incident records have the same exception message; their files total 398,359,682 bytes.

### Evidence integrity and limitations

- SHA-256 recomputation matches the aggregate incident journal (`6a3c487c1dfa800b443e7688b386084719358f9316fc640c033e0248f0f7f65e`) and manifest (`f048a2f4e73dedcec02acdc48c22009ff12e5f10ca74575d62cdd6783061d0f7`). The manifest contains all five watchdog attempts.
- All nine checkpoint XML and tier-file hashes match their metadata; the index contains nine entries. Checkpoint 5 is the last periodic checkpoint before failure: 4,800 completed steps, tick `28800000000000`, created at `00:28:26.5087927Z`. Checkpoints 6–9 are restart-start checkpoints at that same game tick. Starting heartbeats show restore progressed through the controller's exact-hash check, but no completed segment exported its resume result. Bundle integrity does not prove an eight-hour continuation.
- No attempt produced `segment-result.json`. Runner aggregation reads Player incidents/counters, memory flag, coverage, performance and resume verification only from a completed segment result. Therefore aggregate zeros do not mean the simulation never ran, and six aggregate critical errors substantially understate the retained gameplay failures. The healthy first attempt contains 1,225 heartbeat/performance records and 6,561,176 frame samples through 4,894 completed steps (last date Y1 D34 23:40, population 105). Each later attempt contains 25 records.
- `first-failure/failure.json` describes the watchdog at `00:38:50.8718769Z`, ten minutes after the actual gameplay failure, with an empty checkpoint reference. Its reproduction command starts from scratch and still has an output-path placeholder; it does not preserve the first gameplay stack/tick/hash or point to checkpoint 5.
- The first healthy attempt's managed-heap observations rise from 8,196,096 to 76,021,760 bytes, with a maximum of 80,719,872. The controller retains all frame timings in an unbounded per-segment list. This does not establish a leak; the eight-hour memory gate is unproven. Every working-set reading is zero and allocation readings are unqualified zero values, so they cannot establish memory safety or allocation-free execution. Headless frame timings also cannot close U29's visible Player gate.
- The five-attempt early stop occurred while verified checkpoints remained available. The original U30 prompt permits early termination only for explicit unsafe/evidence/cancellation/no-safe-recovery conditions; the recorded no-completed-segment condition alone does not demonstrate one. This policy needs reconciliation before another official run.

### Review validation

Read-only PowerShell checks used `Get-Content ... | ConvertFrom-Json`, streaming `System.IO.File.OpenText` parsing/counting of all five Player incident journals, `Get-FileHash -Algorithm SHA256` for the aggregate journals and all 18 snapshot/tier payloads, checkpoint-index inspection and `rg -n` plus line-numbered reads of the weather, health, composition, controller and runner sources. `git status --porcelain` was empty before documentation updates. `git diff --check` passed; the reviewed diff changes only PROJECT_STATE, TASK_GRAPH, NEXT_TASK, DECISIONS and this handoff. No gameplay/harness implementation, Player rerun or Unity tests/build occurred in this audit; earlier preparation results remain historical evidence only.

## Remediation and preflight (2026-10-12)

Dead residents now advance their deterministic weather-exposure cursor without receiving a new condition; unknown IDs still fail atomically and direct health mutation on a dead Character remains rejected. A gameplay exception ends the Player attempt immediately after durable incident/result writes, so no checkpoint is published from the partially processed world. The orchestrator imports incidents live and exactly once, preserves the true first Player failure plus a complete checkpoint reproduction command, consumes atomic progress evidence when a Player is killed, preserves aborted coverage/performance and continues repeated restart loops with bounded backoff through the real deadline. Malformed newest checkpoint metadata no longer prevents fallback. A 60 FPS headless cap plus exact rotating raw-frame journals bounds segment memory; process working set is refreshed with a native Windows fallback, and a PASS requires verified memory telemetry. Unqualified last-frame allocation values are reported unavailable rather than zero.

`Tools/Test-U30Runner.ps1` passed seven protocol fixtures: healthy, gameplay exception, partial JSONL, watchdog, repeated gameplay exception, repeated watchdog and corrupt checkpoint. The repeated cases continued beyond five failed attempts to their requested deadline. The verifier now runs these automatically.

`C:\serenity_u30_preflight_20261012_run4` used seed 3030 and resumed the failed run's checkpoint 5. It crossed the old failure boundary through game day 35 and returned PASS: 60 productive/72 actual seconds, three completed/resumed segments, 3,318 performance samples, five weather days, abstract-battle coverage, zero-byte incident journal, zero critical/invariant/watchdog/recoverable errors, verified working set and no runaway signal. All 33 snapshot/tier hashes and aggregate journal/manifest hashes match. Every Player exit code is 0. Final logical hash is `46B480B158091A574F604F3CA6A79A3CD8B64CEA02DE9A14837EAA2804E9BB88`.

Final command `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Verify-U30.ps1" -ProjectPath "C:\serenity_game" -StepTimeoutSeconds 1200` passed runner protocols; U30 EditMode 8/8 and PlayMode 2/2; U29 EditMode 28/28 and PlayMode 16/16; required EditMode 160/160 and PlayMode 38/38; persistence 35/35; Windows x64 Mono Development build with 0 errors/4 pre-existing obsolete-API warnings. PostgreSQL discovery found 25 and skipped all because no test database is configured. `git diff --check` passed. A Unity licensing refresh line appears in the successful build log but did not affect the build result.

## Required next action

Keep run3 and all preflight trees immutable. Launch the fresh official run into a new folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_soak_20261012_run4"
```

After it exits, audit every gate and only then mark U30 DONE. U30 remains IN_PROGRESS and U29 remains PARTIAL. The user explicitly authorized U30A implementation while the external run is pending; this does not relax either gate or permit a false U31 completion.

## Independent run4 audit (2026-10-12, after completion)

### Verdict

The external run is complete and its original result reports **runner PASS** under the implemented predicates. Independent review does **not** approve U30 DONE: persisted AI decisions demonstrate a sustained invalid travel loop, prohibited by the original U30 requirement but not detected by the monitor. This is a gameplay/monitoring finding, not a crash or artifact-integrity failure. U30 remains IN_PROGRESS, U29 PARTIAL and U31 BLOCKED. Earlier statements that run4 does not exist are historical and superseded by this audit. No gameplay/harness code or external artifact was modified.

### Duration and independently verified integrity

- Result: `C:\serenity_u30_soak_20261012_run4\soak-result.json`, SHA-256 `C750450D1B292175EF00D1EA4257949B1591E0D9E1E2CBACDDA350938F95425C`.
- UTC interval `2026-10-12T12:04:35.5402795Z` to `2026-10-12T20:06:50.7813372Z` (15:04:35–23:06:50 Moscow). Timestamp difference: 28,935.241 seconds; runner Stopwatch: 28,934 whole seconds. Both exceed 28,800; the approximately 1.24-second difference does not affect acceptance duration.
- All 16 segments pass, complete without abortion, report `segment_complete`, and have Player exit code 0. Fifteen restarts are planned 30-minute boundaries, not crashes/watchdogs. The external PowerShell exit code is not independently retained.
- Each segment completes 7,200 steps; total 115,200. Productive duration is defined as steps/4, totaling 28,800 seconds, not CPU time. Continuous calendar progression reaches `691200000000000` ticks: 800 calendar days, Y3 D71 00:00. Weather coverage 816 includes segment-boundary recounting, not 816 distinct days (801 observed day numbers).
- All 27,977 heartbeat lines and corresponding performance/frame batches parse, agree and have contiguous sequences. Simulation ticks are monotonic. All 1,475,102 raw frame samples reconcile with segment/aggregate counts; independent percentile calculations match every segment result.
- Journal hashes match: incidents `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` (empty) and manifest `4A84461BE3958DB3639CD172F1A558ADD0A482C31097E6491AF6E1E9D87F9CAB`.
- All 113 checkpoint index entries match metadata. All **226 XML/tier payload SHA-256 checks** and **113 independently reconstructed canonical hashes** pass. All **15 resume chains** match previous final checkpoints and first-heartbeat restored hashes.
- Each segment outcome hash is independently reproduced from normalized XML leaves. Final hash: `86A49005B9EAF2BD303A52029EAF29BC2EB00887A782BCB8BF65B7657C5C0A7D`.
- Canonical hashing covers XML plus ID/tier pairs, NOT full tier-runtime values. Fourteen restart tier payloads match exactly; first restart changes eleven energy values by approximately 1.1e-16 (floating-point JSON round-trip scale). This is not material gameplay corruption, but comments claiming full runtime hash validation are too strong. The normalized final hash discards identity/ownership hierarchy and cannot replace exact comparisons or prove a repeated eight-hour deterministic run.

### Blocking finding: persistent AI arrival loop

The AI unit `9c5158cfdab064bfa5da56b7aa1801da` traverses two route legs to `starter_settlement`. The player demo army remains at the distinct `local` location, so no hostile-unit battle starts at the settlement. AI decision 8 at tick `1512000000000` (Y1 D2 18:00, about 63 productive seconds into the run) first records `Travel rejected: NoDirectRoute`. Decision 3201 at the final tick records the same rejection. Across bounded histories in all checkpoints there are **3,067 distinct directly observed rejected decision sequences**; gaps in retained histories are not counted as observations. Final pending travel order is still sequence 6, issued at tick `1080000000000`.

Cause traced in code: `AiStrategicScheduler.NextHop` returns the origin when origin equals target; `Travel` issues a self-route; `GlobalMapService.IssueTravelOrder` rejects it. The scheduler lacks arrival/no-defender progression in this branch. Recover clears current target detail while the stale travel order persists. `U30InvariantMonitor` validates target references and unique order IDs but not repeated failures or stale-order progress. Thus zero recorded invariant failures does not satisfy the original "no AI invalid target loops" requirement.

Remediation belongs to U26/U25 progression, with a focused arrival-at-undefended-settlement regression and U30 liveness observation. This review does not implement a fix or choose a new capture rule.

### Workload and reproducibility limits

- Reported seed is 3030, while saved health/weather/combat/AI/events/campaign world seeds are all **20261006**. The CLI seed controls harness metadata and benchmark Character identities; the normal scene bootstraps its own default before the soak attaches. The present runner does not configure one authoritative world seed.
- Initial checkpoint: **104 Characters, tiers 23/50/31**, after demo raiders/representation changes. Checkpoint 2 (about five minutes): **105 Characters, tiers 0/53/52**. Every later published checkpoint has zero Tier1; final tiers 0/55/50. The active tier policy means this is not sustained 25/50/25 presentation load or U29 evidence.
- One abstract battle ends in the first step (five in-game minutes); zero physical battle steps. Building history records one actual storage capture. The 16 capture attempts include per-segment re-entry, not 16 captures. No AI settlement capture or military victory occurs.
- Real player production totals 240 berries/32 plant fiber, plateaued by checkpoint 6 (about 25 minutes, calendar day 41.67) and unchanged thereafter. Food consumed ends at 300; shortage at 5,299. Player research adds controlled_fire and then has no active project. AI funds a shelter and continues producing berries, reaching 324. Subsystem step counters are invocation coverage, not counts of successful actions or sustained activity.
- One birth raises registry count 104 to 105. Aren dies at calendar day 360 and one succession persists; final registry includes 104 living and one dead Character. Health/weather/events continue without the old weather/dead-character exception. Events retain 128 history entries plus 556 archived records; two player offers remain unselected. These are useful long-duration exercises.
- Both demo combat units keep uninitialized supply and no checkpoints. AI supply has two travel checkpoints, six berries consumed and twelve remaining. Sustained combat supply, shortage and post-victory play are not exercised here.
- No immutable commit/build GUID/binary-hash manifest accompanies this run. Logs identify Unity 6000.6.4f1, Mono under Builds/Windows and Intel Arc/D3D11; current DLL timestamps alone are not immutable provenance.

### Performance and memory

Full raw distribution: p50 **16.667100 ms**, p95 **16.801396 ms**, p99 **74.915698 ms**, mean **19.557105 ms**, maximum **30,368.522644 ms**. There are 31,399 frames over 33.33 ms (2.129% of frames, 16.418% of recorded frame time), 6,509 over 100 ms and 770 over one second. Aggregate p95 hides concentrated bad intervals.

| Segment | p95 ms | p99 ms | max ms | Samples |
|---:|---:|---:|---:|---:|
| 1 | 16.971 | 43.435 | 2021.546 | 103358 |
| 5 | 16.926 | 111.091 | 2372.471 | 86372 |
| 6 | 134.410 | 1340.899 | 30368.523 | 31833 |
| 7 | 34.541 | 433.897 | 4888.950 | 55474 |
| 8 | 17.116 | 206.039 | 6064.948 | 78244 |
| 16 | 16.705 | 83.528 | 1555.787 | 100368 |

The bad interval is concentrated in segments 5–8, about 17:04–19:06 Moscow. Segment 6's 30-second maximum is its first recorded frame, including startup/restore boundary time, not evidence of a 30-second steady-state stall. Excluding batches from its first 30 seconds still leaves p95 about **131.193 ms** and maximum **9,840.203 ms**. Longest heartbeat gap is about 9.02 seconds, below the 600-second watchdog. No subsystem trace or machine-load history can establish the cause; GPU, other processes and temperature remain unproven explanations.

All working-set readings are positive: **393.87–465.01 MiB**. Managed live memory ranges **5.23–36.38 MiB**. The implemented runaway predicate never fires (32 MiB managed/128 MiB working-set thresholds, three-point growth), but resets every 30 minutes. It cannot exclude smaller leaks in one continuously running eight-hour process. Five-minute managed minima grow from roughly 6.0 to 9.3 MiB. XML grows **180,798 → 1,708,970 bytes**; final health subtree is about 1.36 MB with **2,683 conditions: 2,682 recovered, one fatal**. Health advancement keeps sorting/scanning retained conditions. This is measurable retained-state growth/scaling risk, not proof of an unmanaged leak.

Interval journals record **129,780 GC counter increments**, about 270/min (5,420 in segment 1 versus 10,022 in segment 16). Final segment `collections=0` is the reset last interval, not a segment total. Per-heartbeat snapshot capture/serialization/hashing adds instrumentation work; cost attribution needs profiling. Mono generation-zero counts do not alone prove major GC pauses.

CPU reporting truncates sub-second usage because `(cpuTicks - previousCpuTicks) / TimeSpan.TicksPerSecond` performs integer division. Profiler fields named average use `LastValue`, not interval averages. GPU/render and allocation values are unavailable (`-1`), not zero cost.

All Player logs contain a shutdown `MemoryLeaks` record of approximately 1.115–1.118 MB, mostly Manager, without growth across segments. Ownership/baseline investigation is needed before declaring no leaks; this is not evidence of runaway growth. ICU text fallback warnings recur. Segment 5 also logs three shutdown `abort_threads: Failed aborting id ... mono_thread_manage will ignore it` messages, while exiting 0 with a complete result. These are retained runtime shutdown diagnostics, not managed gameplay exceptions. No managed exception/assert/crash was found. Final runner heartbeat is the last live snapshot (27,000 productive seconds/checkpoint 106); segment 16/checkpoint 113 and the completed result supersede it.

### Audit commands and next action

Local ignored analysis files: `Logs/audit_u30_run4.py`, `Logs/audit_u30_state.py`, `Logs/U30-run4-independent-audit.json`, `Logs/U30-run4-state-audit.txt` under `C:\serenity_game`. The scripts parse full JSONL/XML, reconstruct SHA-256/canonical/outcome hashes, compare restart state, and recalculate interpolated percentiles from all frames.

```powershell
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Logs/audit_u30_run4.py > Logs/U30-run4-independent-audit.json
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Logs/audit_u30_state.py > Logs/U30-run4-state-audit.txt
rg -n 'Exception|error|assert|crash|failed|MemoryLeaks' C:\serenity_u30_soak_20261012_run4/segments -g '*.log'
git diff --check
git diff --cached --check
```

Integrity/statistics checks returned zero mismatches. This review changes documentation only; no Unity batchmode/test/build or replacement soak was run. Prior tests/build remain historical. Preserve run4, repair AI arrival/liveness detection, align world seed/provenance and workload coverage, and correct telemetry. Verify focused regressions and representative short runs before new official evidence. Do not repeat the unchanged eight-hour run. U29 still requires its own matrix; U31 cannot be finalized.
