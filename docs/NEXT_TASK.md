# NEXT_TASK.md

U30A is DONE. U29 remains PARTIAL because the mandatory sustained 1000-Character Player p95 gate is still above 33.33 ms. U30 remains IN_PROGRESS until a fresh external eight-hour soak is audited and passes. U31 is the next implementation/integration task, but its final DONE verdict is BLOCKED by both unresolved gates. U32 must not start.

## Next task: U31 — MVP Integration Gate

U31 may prepare and exercise the normal player path `New Game -> economy/research/construction -> discovery -> military travel/combat -> capture -> victory -> continued sandbox -> Save/Load`. It must reuse U30A's canonical campaign, supply and TPS/U20 paths and may fix integration defects found in that flow. It must not duplicate authorities or reinterpret partial performance/reliability evidence as a pass.

Final U31 completion requires every dependency in `docs/TASK_GRAPH.md` to be DONE, including:

- U29: fresh qualifying performance evidence with the 1000-Character worst-repeat p95 at or below 33.33 ms and all existing integrity gates.
- U30: a separately audited full external soak with requested and actual wall-clock duration at least 28,800 seconds, zero disqualifying incidents, verified memory/checkpoint resume and complete immutable evidence.
- U30A: already DONE with snapshot v15 and the focused evidence recorded in `docs/U30A_HANDOFF.md`.

Do not change U29 or U30 status from U31. A dedicated finalizer must audit their own evidence. If either remains PARTIAL/IN_PROGRESS/FAILED, U31 remains BLOCKED even when its gameplay integration tests pass.
