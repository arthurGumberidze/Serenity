# NEXT_TASK.md

U14 was completed on 2026-10-06. The U11 RTS right-click Move follow-up was completed and validated on 2026-10-06 without starting U15. Do not reopen U11/U14 unless a regression is found. The next active implementation task remains U15.

## Active task: U15 — Производство и фермерство

### Prerequisites
U09 resources/inventories, U10 centralized worker AI and U14 deterministic off-camera simulation are DONE.

### Goal
Implement the TASK_GRAPH U15 vertical slice so one Stone Age settlement can sustainably transform canonical resources and produce food/materials for several game years without violating resource conservation or tier-independent simulation.

### Scope boundary
U15 has not started. Begin from `docs/U14_HANDOFF.md`, `docs/U10_HANDOFF.md`, `docs/U09_HANDOFF.md`, D-026 and the FRS production/farming sections. Reuse canonical inventories, work orders, explicit U03 time and U14 deterministic remote-step infrastructure. Do not implement research U16, dynasty U17, weather/seasons U19, global map U25 or production UI U27.

### Required invariants
- Recipes, buildings, inputs, outputs, durations and balance values are data-driven.
- Resource quantities change only through canonical Simulation services and remain conserved except for declared recipe inputs/outputs.
- Detailed and off-camera production consume the same logical recipe/time model and cannot be accelerated by camera/tier switching.
- Production/farming uses centralized batching; no per-worker or per-building `Update` loop.
- Multi-year deterministic tests cover pause, save-like continuation, tier changes and insufficient inputs.

### Relevant handoffs
Read `docs/U14_HANDOFF.md`, `docs/U10_HANDOFF.md`, `docs/U09_HANDOFF.md`, the U03 handoff and the FRS production, agriculture, logistics and resource requirements before implementation.
