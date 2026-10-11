# Performance gates

## U29 contract

The mandatory gate is a visible Windows x64 Mono Development Player at 1600×900, UI/camera/rendering enabled, exact 25/50/25 simulation tiers, 60 seconds warm-up, 300 seconds measurement and three repeats at each population 100, 300, 500 and 1000. Every repeat must preserve exact population/StableEntityId/tier/save hashes, foreground evidence and bounded memory. Worst-repeat p95 must be <=33.33 ms.

`Tools/Run-U29-Offline.ps1` is the complete local entry point. It performs no Codex/API call. `Tools/Run-U29-Performance.ps1` owns the matrix; `Tools/Write-U29-PerformanceReport.ps1` writes human and machine reports. `Tools/Run-U29-Diagnostic.ps1` and native traces are investigation tools and cannot complete the gate.

## Current result

U29 is PARTIAL, not DONE. The latest complete matrix is `Logs/U29/offline-20261011-194641`; it passed 8/12 repeats. Worst p95 was 16.671 ms at 100, 16.670 ms at 300, 27.199 ms at 500 and 40.626 ms at 1000. The clean post-optimization sustained 1000 control improved p95 to 38.216 ms and allocations to 45,432.75 bytes/frame, but still failed the 33.33 ms limit. No threshold, population, tier mix or gameplay cadence was relaxed.

See `docs/U29_HANDOFF.md` and the generated reports for exact evidence and remaining bottlenecks.
