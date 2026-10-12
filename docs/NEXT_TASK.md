# NEXT_TASK.md

U31's existing integration tests pass but its final gate is BLOCKED. The completed U30 run4 audit identifies an AI arrival-loop regression and acceptance blind spots. The next implementation task is targeted U30/U26 remediation described below; this audit has not implemented it.

## Exact remaining gates

1. Finalize U29 separately from fresh qualifying performance evidence. The complete 100/300/500/1000 matrix must pass every integrity gate and the worst sustained 1000-Character Player p95 must be at or below 33.33 ms. The current retained sustained control is 38.216 ms, so U29 remains PARTIAL.
2. Preserve completed `C:\serenity_u30_soak_20261012_run4` as immutable runner-PASS evidence. Duration, checksums and resume chains pass, but AI repeatedly issues same-location travel after arrival and the monitor misses it. Repair that U26/U25 progression boundary and add a U30 liveness regression/invariant; align requested seed with canonical bootstrap; report actual workload/tier coverage; correct CPU/GC/timing telemetry and qualify memory/provenance. First validate focused regressions and short representative runs. Only then produce fresh official evidence in a new directory and audit all duration, behavior, memory, checkpoint and integrity requirements. U30 remains IN_PROGRESS; do not repeat the unchanged eight-hour run.
3. Only after U29 and U30 are DONE, rerun `Tools\Verify-U31.ps1`, audit `docs\U31_HANDOFF.md`, and change U31 from BLOCKED to DONE in a dedicated finalization commit.

Do not reinterpret short controls, preflights or the U31 Player smoke as U29/U30 acceptance evidence. Do not modify existing U29/U30 evidence while finalizing U31.
