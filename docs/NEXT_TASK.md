# NEXT_TASK.md

## Active task: U01 - Архитектура assemblies и слоёв

### Prerequisite
U00 DONE. Unity 6000.6.4f1 is the user-approved pinned Editor (D-008); use this version, not the original 6000.3 target. See docs/U00_HANDOFF.md for build/test evidence.

### Goal
Define assembly boundaries and layer ownership following the FRS technical profile and docs/TASK_GRAPH.md.

### Required output
- Domain, Simulation, ECS, Presentation, Infrastructure and Tests assembly boundaries with no circular references.
- Replace the placeholder docs/ARCHITECTURE.md with actual dependencies, service ownership, data flow, scene strategy, save-state ownership, ECS bridges and testing strategy.
- All assemblies compile; dependency graph documented.
- Run project checks and complete the end-of-chat protocol.

### Do not do
Do not redo U00 or implement gameplay systems from U03 onward. U02 package/configuration work remains a separate task. This file selects the next chat; U01 was not started in the U00 chat.
