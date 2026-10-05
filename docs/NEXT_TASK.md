# NEXT_TASK.md

## Active task: U12 — DOTS bootstrap Tier 2

### Prerequisites
U02 assembly/package architecture and U06 canonical characters are DONE. U11 work groups/jobs are also complete but are not a prerequisite edge for this bootstrap.

### Goal
Implement the TASK_GRAPH U12 vertical slice so thousands of test entities can be updated through Entities/DOTS without one GameObject or MonoBehaviour per entity.

### Scope boundary
U12 has not started. Read the DOTS/Tier 2 sections of the FRS and preserve the existing no-engine Domain state and stable-identity rules. Establish only the Tier 2 data/update bootstrap and its performance/validation evidence. Do not begin U13 tier transitions, U14 off-camera simulation, U15 production, U20 combat, U27 production UI or broad persistence-schema expansion.

### Relevant handoffs
Tier 2 entities are projections of canonical identities, not replacement character IDs. Do not copy U11 groups/jobs into presenter-owned or unmanaged duplicate authorities. Reuse centralized scheduling/command boundaries where applicable, avoid structural changes in hot loops, and prove the target scale without per-entity GameObjects or per-entity Updates. See `docs/U11_HANDOFF.md`, `docs/ARCHITECTURE.md` and D-022.
