# NEXT_TASK.md

U11 GUI click-through maintenance was completed on 2026-10-06 without reopening U11 or starting U14. The active implementation task remains U14.

## Active task: U14 — Детерминированный off-camera simulation

### Prerequisites
U03 deterministic time, U04 stable IDs/save boundary and U13 tier lifecycle manager are DONE.

### Goal
Implement the TASK_GRAPH U14 vertical slice so equal initial state, explicit time deltas and equal seed produce the same remote-simulation result, independent of camera zoom or tier materialization history.

### Scope boundary
U14 has not started. Begin from `docs/U13_HANDOFF.md`, D-024 and the U13 architecture section. Reuse `Tier3CharacterRecord`, `CharacterRuntimeState` and `TierManager`; do not create a second persistent-character identity, tier coordinator or random regeneration path. Production/farming remains U15, armies/abstract battle remain U21/U23, global-map mode remains U25 and formal performance gates remain U29.

### Required invariants
- Off-camera advancement consumes explicit U03 simulation time and a controlled deterministic RNG stream/seed; it never reads wall time, frame delta or camera state as simulation input.
- Zooming/materializing changes representation only and cannot grant extra progress or reroll an already committed result.
- Persistent named characters retain their canonical `Character` identity, family, health and inventory ownership through remote ticks.
- Tier 3 advancement is batched and adds no GameObject, ECS Entity or per-character `Update` requirement.
- Returning to Tier 1 or Tier 2 materializes the latest committed remote state through the existing U13 transition barrier.

### Relevant handoffs
Read `docs/U13_HANDOFF.md`, `docs/U12_HANDOFF.md`, the FRS off-camera/LOD requirements and the U03 deterministic-time handoff before implementation.
