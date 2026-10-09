# NEXT_TASK.md

U28 was completed and validated on 2026-10-09.

## Next unblocked task: U29 — Performance gates: 100→300→500→1000

### Status

TODO. Awaiting explicit user authorization; U29 has not started.

### Dependencies

U10 Utility AI, U12 DOTS, U13 tiers, U14 off-camera simulation, U20 combat, U26 strategic AI, U27 gameplay UI and U28 events are DONE.

### Goal

After explicit authorization, validate and optimize real 100→300→500→1000 Character configurations so Player p95 remains at or below 33.33 ms without runaway allocations.

### Scope boundary

Begin from `docs/U28_HANDOFF.md`, `docs/EVENT_SYSTEM.md`, current architecture decisions and the performance requirements in the FRS. Preserve deterministic canonical behavior and do not start U30 or later tasks.
