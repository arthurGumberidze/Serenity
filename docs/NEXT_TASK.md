# NEXT_TASK.md

U28 is DONE. U29 remains PARTIAL by explicit user decision; its 1000-Character p95 debt remains open. U30 remediation is complete but U30 remains IN_PROGRESS until the fresh external eight-hour run passes.

## Next authorized implementation task: U30A — explicit sequencing exception while U30 reruns externally

### Status

U30 remains IN_PROGRESS. Its failed run3 is immutable evidence; the weather/runner remediation and checkpoint-5 preflight are complete. The user will launch the fresh official run at `C:\serenity_u30_soak_20261012_run4` overnight and explicitly authorized work on U30A, then U31, while that external process is pending. This sequencing permission does not turn U30 or U29 into DONE and does not waive any U31 gate.

### U30 external command

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\serenity_game\Tools\Run-Soak-U30.ps1" -Seed 3030 -WallClockHours 8 -CheckpointIntervalMinutes 5 -OutputFolder "C:\serenity_u30_soak_20261012_run4"`

Do not reuse any previous output directory. Preserve run3 and all preflight trees. A fresh finalizer must audit run4 before U30 can become DONE.

### U30A scope

Follow `serenity_automation/serenity_night_automation_v3/prompts/U30A_PROMPT.txt`. Record this user-authorized dependency sequencing exception in the U30A handoff. Implement and validate U30A without weakening U29/U30 gates. U31 may be prepared after U30A, but its final MVP gate remains blocked by every unmet dependency/result and must report them honestly.

### Required finalizer checks

Read `docs/U30_HANDOFF.md`, `docs/SOAK_TESTING.md`, and `C:\serenity_u30_soak_20261012_run4\soak-result.json`. Verify exit/result evidence, `requested_wall_clock_seconds >= 28800`, `actual_wall_clock_seconds >= requested_wall_clock_seconds`, productive duration, zero critical/invariant/watchdog failures, no runaway-memory flag, verified memory metrics and checkpoint/resume, non-empty final logical hash, complete aggregate coverage, append-only journal/manifest hashes, raw performance evidence and independently verified checkpoint bundles. Inspect all segment and progress results and the first-failure bundle if present.

If every gate passes, update U30 to DONE and commit `U30: validate 8 hour soak`. If the run is incomplete, short, missing evidence, or reports any critical incident, leave U30 IN_PROGRESS and document the exact failure. U29 remains PARTIAL regardless of U30 reliability evidence.
