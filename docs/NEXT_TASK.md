# NEXT_TASK.md

## Active task: U11 — Work groups and jobs

### Prerequisites
U10 is DONE, including its read-only development selection/resource inspection follow-up. U06 owns canonical characters, U07 owns disposable Tier 1 presentation, U09 owns resources/inventories/storage, and U10 owns centralized autonomous Utility AI, movement intent and haul claims.

### Goal
Implement the TASK_GRAPH U11 slice so the player can manage more than 100 NPCs through explicit work groups and jobs without bypassing U10 scheduling or duplicating canonical state.

### Scope boundary
U11 has not started. Read `docs/U10_HANDOFF.md` and the work-group/job sections of the FRS before implementation. Preserve the U10 separation between utility selection, task execution and presentation movement. Do not begin U12 DOTS, U13 tier management, U15 production/farming, U20 combat or U27 full gameplay UI.

### Relevant handoffs
Player-authored groups and job policy may constrain or prioritize U10 action availability, but must remain Simulation/Domain data keyed by `StableEntityId`. Do not create per-NPC Update loops, mutate presenter state as authority, use Character IDs as task IDs, or replace U09 atomic inventory transfers and U10 claim cleanup.
