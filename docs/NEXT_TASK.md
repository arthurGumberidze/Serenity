# NEXT_TASK.md

U28 is DONE. U29 remains PARTIAL by explicit user decision; its 1000-Character p95 debt remains open. U30 is IN_PROGRESS after a failed official external run.

## Active task: U30 — resolve the failed official soak before finalization

### Status

IN_PROGRESS. The official run at `C:\serenity_u30_soak_20261012_run3` is complete and audited FAIL: 4,364 seconds instead of 28,800, five watchdog failures, four restarts, zero completed segments and an empty final hash. The first gameplay failure is `Cannot add a condition to a dead character.` in WeatherService at Y1 D35. The user's analysis request has been handled through an artifact/source audit; remediation has not been implemented in this review.

### Required follow-up within U30

Read the audit in `docs/U30_HANDOFF.md`. Repair weather exposure/life-state integration with a dead-resident/day-boundary regression; prevent failed steps from repeatedly advancing a partially processed world while starving heartbeat; preserve aborted-segment incidents/performance/coverage and capture the actual first failure immediately. Reconcile the five-attempt early-stop policy with the U30 prompt's explicit immediate-stop conditions. Validate the same-seed failure boundary from checkpoint 5 in a new evidence directory, then verify focused regressions/build and perform a fresh official eight-hour run. Preserve the failed evidence tree. Do not start U30A or U31.

### Required finalizer checks

Read `docs/U30_HANDOFF.md`, `docs/SOAK_TESTING.md`, and `C:\serenity_u30_soak_20261012_run3\soak-result.json`. Verify exit/result evidence, `requested_wall_clock_seconds >= 28800`, `actual_wall_clock_seconds >= requested_wall_clock_seconds`, zero critical/invariant/watchdog failures, no runaway-memory flag, verified checkpoint/resume, non-empty final logical hash, complete aggregate coverage, append-only journal/manifest hashes, and independently verified checkpoint bundles. Inspect all segment results and the first-failure bundle if present.

If every gate passes, update U30 to DONE and commit `U30: validate 8 hour soak`. If the run is incomplete, short, missing evidence, or reports any critical incident, leave U30 IN_PROGRESS and document the exact failure. Do not start U30A or U31. U29 remains PARTIAL regardless of U30 reliability evidence.
