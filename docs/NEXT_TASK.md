# NEXT_TASK.md

U21 was completed and validated on 2026-10-08. Do not reopen U21 unless review finds a regression. Do not begin U22 until the user explicitly accepts the U21 handoff.

## Next unblocked task: U22 — Third-person control

### Status

Not started; awaiting U21 review.

### Dependencies

U02 local scene/control foundation, U07 Tier1 presentation, U20 tactical RTS combat and U21 military units/captains are DONE.

### Goal

After explicit authorization, implement the TASK_GRAPH U22 RTS↔third-person control slice without losing orders or canonical state.

### Scope boundary

Begin from `docs/U21_HANDOFF.md`, `docs/MILITARY_UNITS_AND_CAPTAINS.md`, current architecture decisions and relevant FRS sections. Captain remains an existing Character and unit/combat state remains authoritative outside presentation. Do not begin U23 or later tasks.
