# NEXT_TASK.md

The active product task is **P8.3: рабочие группы и простые задания**. P8.1 and P8.2 are DONE at their implementation/evidence boundaries; do not redo them or start P8.4–P8.6 in the same task.

Make settlement work directly controllable on the accepted P8.2 map:

1. Implement the direct player flow **click an object → choose an action → assign priority** for supported resource, construction, hauling, crafting, farming/gathering, research, scouting, military and care targets.
2. Expose assignable groups for добытчики, строители, носильщики, ремесленники, земледельцы/собиратели, учёные, разведчики, воины and лекари/смотрители. Reuse the canonical U11 work-group/job authorities instead of creating UI-owned assignments.
3. Add the explicit priority **«Прямо сейчас»** and make scheduler/reservation behavior deterministic, conflict-safe and observable. No per-NPC `Update` loop is allowed.
4. Preserve `StableEntityId`, Tier1↔Tier2↔Tier3 behavior, canonical inventories/jobs/reservations and Save/Load across assignment, interruption, reprioritization and restore.
5. Add proportional EditMode, headless/GPU PlayMode, Windows Development build and visible Player evidence for direct commands, group assignment, priority, contention and Save/Load. Do not expand into the P8.4 appearance editor or P8.5 interface restyle.

P8.6 alone owns the deferred complete U29 matrix, U30 endurance/native-memory/history closure and final U31 gate. Their recorded thresholds and statuses must not be changed during P8.3.

Completion requires updated project/task/decision/asset documentation and a commit prefixed `P8.3:`.
