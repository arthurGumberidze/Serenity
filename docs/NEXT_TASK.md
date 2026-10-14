# NEXT_TASK.md

The active product task is **P8.6: интеграция и квалификация**. P8.1–P8.5 are DONE at their implementation/evidence boundaries; do not redo their feature work unless a qualification failure identifies a concrete defect.

Qualify the feature-complete first-era build without weakening any existing gate:

1. Run and diagnose the complete U29 performance matrix at 100/300/500/1000 Characters. The 1000-Character Player p95 gate remains ≤33.33 ms; the recorded 38.216 ms result is not a pass.
2. Close the U30 workload/telemetry, native-memory, bounded-history and continuous-process gaps, then run the approved fresh segmented eight-hour endurance route. Historical run4 is evidence, not DONE.
3. Re-run the final U31 integration/release path only after U29 and U30 honestly pass, including PostgreSQL integration when configured, full regressions, Windows Development build and visible Player evidence.
4. Keep canonical simulation, save compatibility, `StableEntityId`, tier boundaries and the P8.5 presentation-only UI contract intact while fixing any qualification defects.
5. Record exact commands, machine/configuration, raw results and any remaining limitation. Do not relabel partial/blocked gates as complete.

P3/P5 visual taste, P6 first-hour usability and P7 long-route campaign acceptance remain explicit human acceptance items even when automation passes.

Completion requires updated project/task/decision documentation and a commit prefixed `P8.6:`.
