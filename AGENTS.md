# AGENTS.md — Unity project

## Mission
Build the game in `docs/reference/FRS_Unity.docx` following `docs/TASK_GRAPH.md`. Work only on the active task in `docs/NEXT_TASK.md`.

## Read first in every new Codex chat
1. `AGENTS.md`
2. `docs/PROJECT_STATE.md`
3. `docs/NEXT_TASK.md`
4. `docs/TASK_GRAPH.md`
5. `docs/DECISIONS.md`
6. `docs/ARCHITECTURE.md` if present
7. Relevant FRS section

## Unity engineering rules
- Target Unity 6000.6.4f1 (user-approved override on 2026-10-03; see D-008). Do not change Editor version without a migration task.
- Main language C#.
- Hybrid simulation: Tier1 GameObject/presentation + persistent domain state; Tier2 Entities/DOTS; Tier3 aggregate C# data.
- Do not put core rules only in MonoBehaviours. Prefer pure/testable Domain services.
- Do not create an Update loop per mass NPC. Use schedulers/ECS/jobs/ticked systems.
- ScriptableObject stores definitions/config, not canonical mutable save-state.
- Persistent references use StableEntityId, never Unity instance ID.
- Balance/content must be data-driven.
- No new Asset Store dependencies without explicit approval.
- Add/Edit asmdef references carefully; no circular dependencies.
- Build/tests are mandatory before DONE.

## End-of-chat protocol
1. Update `docs/PROJECT_STATE.md`.
2. Update task status in `docs/TASK_GRAPH.md`.
3. Append architectural/design choices to `docs/DECISIONS.md`.
4. Set `docs/NEXT_TASK.md` to the next unblocked task.
5. Record exact Unity/batchmode/test commands and results.
6. Commit with task ID prefix, e.g. `U10: implement utility AI scheduler`.

