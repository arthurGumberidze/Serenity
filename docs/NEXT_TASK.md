# NEXT_TASK.md

Active task: **P9.4 — Поселение, ресурсы и взаимодействие**.

Starting point: P9.3 filled the physical P9.2 Terrain with deterministic distinct biomes and readable formations around all 24 canonical resource roots. Read `docs/P93_HANDOFF.md` before changing the scene or interaction presentation.

Required scope:

1. Replace the visible prototype shelter, storage, camp and work-site language with a coherent Stone Age settlement; make gathering, hauling, building, jobs and priorities visually understandable in normal RTS/TPS play.
2. Preserve all existing Domain/Simulation services, work orders, `StableEntityId` values, finite resources, construction costs, NavMesh, six locations and Save/Load. Presentation must not become a second gameplay authority.
3. Use only approved official Unity URP Terrain Sample, UMA 3 and documented project-owned derivatives. If these sources cannot honestly supply a required building, prop or Character surface, report the exact asset need and request approval rather than importing or substituting an unapproved package.
4. Verify resource selection/actions, gathering/hauling/building, priorities, settlement readability, collision/navigation, seasons and Save/Load with proportional EditMode/PlayMode tests, a Windows Development build and visible normal-Player RTS/TPS evidence. Do not hide failures.
5. Update project state, task graph, decisions, this next-task pointer, asset registry if needed and `docs/P94_HANDOFF.md`; commit only P9.4 work locally without push. On honest completion, set the next task to **P9.5 — Атмосфера и глобальная карта**.
6. Do not run U29/U30/U31 or reinterpret the P8.6 checkpoint. Their statuses remain PARTIAL / IN_PROGRESS / BLOCKED; see `docs/P86_HANDOFF.md`.
