# NEXT_TASK.md

U28 is DONE. U29 remains PARTIAL by explicit user decision; its 1000-Character p95 debt remains open. U30 is IN_PROGRESS in its external-run phase.

## Next authorized task: U30 finalization — verify the external 8+ hour soak

### Status

IN_PROGRESS. Harness preparation is complete and committed separately from the final result. The user launched the official offline run at `C:\serenity_u30_soak_20261012_run3`; its first heartbeat is `2026-10-12T00:08:26.7481820Z`. Do not mark U30 DONE until that process finishes and a fresh Codex finalizer audits all artifacts.

### Required finalizer checks

Read `docs/U30_HANDOFF.md`, `docs/SOAK_TESTING.md`, and `C:\serenity_u30_soak_20261012_run3\soak-result.json`. Verify exit/result evidence, `requested_wall_clock_seconds >= 28800`, `actual_wall_clock_seconds >= requested_wall_clock_seconds`, zero critical/invariant/watchdog failures, no runaway-memory flag, verified checkpoint/resume, non-empty final logical hash, complete aggregate coverage, append-only journal/manifest hashes, and independently verified checkpoint bundles. Inspect all segment results and the first-failure bundle if present.

If every gate passes, update U30 to DONE and commit `U30: validate 8 hour soak`. If the run is incomplete, short, missing evidence, or reports any critical incident, leave U30 IN_PROGRESS and document the exact failure. Do not start U30A or U31. U29 remains PARTIAL regardless of U30 reliability evidence.
