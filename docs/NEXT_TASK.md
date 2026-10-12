# NEXT_TASK.md

U31's integration implementation is validated but its final gate is BLOCKED. Do not add more U31 gameplay scope unless a regression is found.

## Exact remaining gates

1. Finalize U29 separately from fresh qualifying performance evidence. The complete 100/300/500/1000 matrix must pass every integrity gate and the worst sustained 1000-Character Player p95 must be at or below 33.33 ms. The current retained sustained control is 38.216 ms, so U29 remains PARTIAL.
2. Run and independently audit the fresh U30 external soak at `C:\serenity_u30_soak_20261012_run4`. Requested and actual wall-clock duration must both be at least 28,800 seconds, productive duration and aggregate coverage must be complete, incidents/invariants/watchdogs must be zero, memory and checkpoint resume must be verified, journal/manifest/checkpoint hashes must match and the final logical hash must be non-empty. U30 remains IN_PROGRESS until that audit passes.
3. Only after U29 and U30 are DONE, rerun `Tools\Verify-U31.ps1`, audit `docs\U31_HANDOFF.md`, and change U31 from BLOCKED to DONE in a dedicated finalization commit.

Do not reinterpret short controls, preflights or the U31 Player smoke as U29/U30 acceptance evidence. Do not modify existing U29/U30 evidence while finalizing U31.
