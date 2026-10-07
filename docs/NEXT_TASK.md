# NEXT_TASK.md

U17 was completed and validated on 2026-10-07. Do not reopen U17 unless review finds a regression. Do not begin U18 until the user explicitly accepts the U17 handoff.

## Next unblocked task: U18 — Семейные события, болезни и травмы

### Status

Not started; awaiting U17 review.

### Prerequisites

U06 character domain and U17 dynasty/succession are DONE.

### Goal

After explicit authorization, implement the TASK_GRAPH U18 family-event, disease and injury slice without changing U17 succession identity or exactly-once guarantees.

### Scope boundary

Begin from `docs/U17_HANDOFF.md`, `docs/DYNASTY_AND_SUCCESSION.md`, the U18 section of `docs/TASK_GRAPH.md`, current architecture decisions and the relevant FRS health/family sections. Reuse `SuccessionService.ProcessDeath` as the death boundary. Do not begin U19 or later tasks.
