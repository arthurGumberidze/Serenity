# NEXT_TASK.md

## Active task: U13 — Tier manager 1↔2↔3

### Prerequisites
U04 stable IDs/save boundary, U07 Tier 1 presentation and U12 Tier 2 DOTS bootstrap are DONE.

### Goal
Implement the TASK_GRAPH U13 vertical slice so one named character preserves identity, family and health while moving through the supported Tier 1, Tier 2 and Tier 3 representations.

### Scope boundary
U13 has not started. Read the tier-transition and off-camera sections of the FRS plus `docs/U12_HANDOFF.md` before implementation. Reuse U12's explicit transfer/materialization/extraction boundary; do not create a second Tier 2 identity authority. U13 owns automatic tier transitions and coordination. U14 still owns deterministic off-camera simulation, and U15/U20/U25/U27/U29 remain deferred.

### Required invariants
- A transition never generates a replacement `StableEntityId` for an existing character.
- Only one tier representation is active/authoritative for a mutable field at a time.
- Jobs complete before extraction or disposal at the transition barrier.
- Tier 1 GameObject presentation, Tier 2 ECS projection and Tier 3 aggregate remain distinct representations.
- Camera distance alone cannot reroll canonical results or bypass a committed state transfer.

### Relevant handoffs
Start from `docs/U12_HANDOFF.md`, D-023 and the U12 architecture section. U12 deliberately did not register its private ECS world in the default player loop and did not implement transition triggers; those composition responsibilities begin here.
