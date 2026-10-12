# U30 SOAK TESTING

## Purpose and status

`Tools/Run-Soak-U30.ps1` is the offline Windows reliability runner. It invokes no Codex/OpenAI API. Harness readiness and short runs do not complete U30: only a separately audited external result with at least 28,800 requested and actual wall-clock seconds may pass the official gate.

The runner keeps Windows awake for its own lifetime through `ES_SYSTEM_REQUIRED`; the display may turn off. Keep the launching PowerShell process alive and keep the machine connected to AC power.

## Official eight-hour run

Use a new absolute output directory outside the Git worktree:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_soak_20261012_run4"
```

Official mode rejects `WallClockHours` below 8. The runner exits 0 only for a completed passing aggregate result and exits non-zero after preserving evidence for any failed gate.

## Short validation

`TestMode` is intentionally non-official and exists for focused development:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -OutputFolder "C:\Temp\serenity-u30-short" -TestMode -TestWallClockSeconds 30 -TestSegmentSeconds 10 -TestWatchdogSeconds 5
```

Use a new/empty folder each time. Short mode still requires productive duration, aggregate subsystem coverage, checkpoint/resume, performance evidence and a non-empty logical hash.

## Resume from a verified checkpoint

Resume writes a new evidence tree and never overwrites the original run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_resumed" -ResumeFromCheckpoint "C:\serenity_u30_soak_20261012_run3\checkpoints\checkpoint-00000002\checkpoint.xml"
```

The seed must match checkpoint metadata. The runner rechecks snapshot and tier SHA-256 values; an internal restart chooses the newest verified bundle and falls back through older bundles if validation fails.

## Evidence layout

- `soak-result.json`: atomic aggregate verdict and mandatory/additive diagnostics.
- `incidents.jsonl`: append-only aggregate incident journal.
- `segment-manifest.jsonl`: PID, start/end reason, checkpoint, exit code and performance summary per attempt.
- `first-failure/`: immutable first failure record and minimal reproduction command.
- `segments/segment-NNNN/`: Player log, heartbeat JSONL, losslessly aggregated performance JSONL, incident JSONL and segment result.
- `segments/segment-NNNN/segment-progress.json`: atomic latest evidence used if a Player is killed before its final result.
- `segments/segment-NNNN/frames-NNNN.jsonl`: exact bounded/rotated frame batches referenced by the performance index.
- `checkpoints/checkpoint-NNNNNNNN/`: snapshot-v14 `checkpoint.xml`, exact `tiers.json` and hash-bearing `checkpoint.json`.
- `checkpoints/checkpoint-index.jsonl`: append-only checkpoint publication history.

Heartbeat records include real segment time, game tick/date, population/resources, active battles/AI, memory, frame p50/p95/p99/max, available main/render/GPU timings, allocation/collection/CPU/working-set metrics, current exact hash and last checkpoint.

## Verdict rules

`soak-result.json` contains the required duration, timestamps, error counters, memory flag, resume flag, final logical hash and summary, plus productive duration, aggregate coverage, samples/restarts/segments and journal/manifest paths with hashes. A PASS requires:

- actual real duration and productive simulation duration meet the request;
- no critical, invariant or watchdog failures and no runaway-memory signal;
- verified nonzero process-memory telemetry; unavailable working-set/allocation probes never become zero-cost evidence;
- at least one successful restart/resume verification;
- all required systems have aggregate coverage;
- at least one completed segment and performance sample;
- a non-empty repeatable logical outcome hash and durable evidence paths/hashes.

An incident remains a final FAIL even after successful recovery. Watchdog/crash recovery continues from a verified checkpoint with bounded backoff through the requested real deadline whenever the output and a safe verified/fresh-start recovery path remain available. The aggregate includes progress/performance/coverage from aborted attempts and imports Player incidents while the process is still alive.

`Tools/Test-U30Runner.ps1` exercises the orchestration protocol with a local fake Player: healthy completion, gameplay exception, partial JSONL write, watchdog, repeated exception/watchdog loops beyond five attempts and corrupt-newest-checkpoint fallback. It is automatically included by `Tools/Verify-U30.ps1`.

## Finalizer

After the external process exits, use a fresh Codex chat with the U30 prompt. The finalizer must inspect the result, journals, manifest, progress/segment results, raw performance index and checkpoint hashes. It must leave U30 IN_PROGRESS on a missing, incomplete, short or failed result. On a complete pass it updates repository state and commits `U30: validate 8 hour soak`. The user explicitly authorized U30A implementation while the rerun is pending; that sequencing exception does not satisfy U30 or permit U31 to claim a passed dependency.
