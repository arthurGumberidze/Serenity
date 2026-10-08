# NEXT_TASK.md

U19 was completed and validated on 2026-10-08. Do not reopen U19 unless review finds a regression. Do not begin U20 until the user explicitly accepts the U19 handoff.

## Next unblocked task: U20 — Tactical RTS Combat

### Status

Not started; awaiting U19 review.

### Dependencies

U05 gameplay scene, U05A assets, U06 characters and U07 Tier 1 presentation are DONE.

### Goal

After explicit authorization, implement the TASK_GRAPH U20 tactical RTS combat slice with deterministic battle losses.

### Scope boundary

Begin from `docs/U19_HANDOFF.md`, `docs/WEATHER_AND_SEASONS.md`, the U20 section of `docs/TASK_GRAPH.md`, current architecture decisions and relevant FRS sections. Reuse U18 health/capability and U19 movement/weather hooks where combat explicitly needs them. Do not begin U21 or later tasks.
