# NEXT_TASK.md

Active task: **P9.3 — Биомы и наполненный мир**.

Starting point: P9.2 moved the canonical six-location/24-resource gameplay region to the accepted physical Terrain composition and removed the superseded visible map roots after verification. Read `docs/P92_HANDOFF.md` before changing presentation or the scene.

Required scope:

1. Fill the entire accessible local region with distinct forest, undergrowth, grass, rock, soil, shoreline and resource-formation zones at believable density and scale. Remove the visible impression of a sparse single-colour clearing or repeated faceted trees.
2. Use only the approved official Unity URP Terrain Sample, UMA 3 and documented project-derived assets. Scatter deterministically; justify LOD and collider choices. Preserve the P9.2 physical Terrain, shore, routes, buildable areas, six locations, 24 resource identities, navigation and Save/Load.
3. Keep resources clickable and existing Domain/Simulation/StableEntityId authorities unchanged. Verify RTS/TPS readability, collision/NavMesh, weather/season presentation and resource interactions with proportional EditMode/PlayMode tests, a Windows Development build and visible normal-Player screenshots. Do not hide failures.
4. Update project state, task graph, decisions, this next-task pointer, asset registry if needed and `docs/P93_HANDOFF.md`; commit only P9.3 work locally without push. On honest completion, set the next task to **P9.4 — Поселение, ресурсы и взаимодействие**. If an approved asset is insufficient or access is blocked, report the exact need instead of substituting an unapproved dependency.
5. Do not run U29/U30/U31 or reinterpret the P8.6 checkpoint. Their statuses remain PARTIAL / IN_PROGRESS / BLOCKED; see `docs/P86_HANDOFF.md`.
