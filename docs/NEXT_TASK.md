# NEXT_TASK.md

U16A was completed and validated on 2026-10-07. Do not reopen U16/U16A unless review finds a regression. Do not begin U17 until the user explicitly accepts the U16A handoff.

## Next unblocked task: U17 — Династия и наследование

### Status

Not started; awaiting U16A review.

### Prerequisites

U03 time, U04/U04A persistence, and U06 character domain are DONE.

### Goal

After explicit authorization, implement the TASK_GRAPH U17 dynasty/succession slice: a monarch's death transfers control to an eligible heir and dynasty extinction produces Game Over.

### Scope boundary

Begin from `docs/U16A_HANDOFF.md`, `docs/U16_HANDOFF.md`, the U17 section of `docs/TASK_GRAPH.md`, current architecture decisions and the relevant FRS dynasty/succession sections. Preserve the U16 research/save contracts and U16A input/debug boundaries. Do not begin U18 or later tasks.
