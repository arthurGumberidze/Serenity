# U30 HANDOFF

## Task

ID: U30

Title: Automated tests and 8+ hour soak

Status: IN_PROGRESS — failed official run remediated and preflighted; a fresh eight-hour run remains required

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
