# NEXT_TASK.md

U27 was completed and validated on 2026-10-09.

## Next unblocked task: U28 — Event System

### Status

TODO. Awaiting explicit user authorization; U28 has not started.

### Dependencies

U04/U04A persistence, U06 Characters, U17 succession, U18 health/family and U27 player-facing UI are DONE.

### Goal

After explicit authorization, implement deterministic single events and weighted/cooldown event chains with stable-seed replay and persistence.

### Scope boundary

Begin from `docs/U27_HANDOFF.md`, `docs/GAMEPLAY_UI.md`, current architecture decisions and the relevant FRS event sections. Keep the U27 notification feed as a presentation consumer; do not make UI the event authority. Do not begin U29 or later tasks.
