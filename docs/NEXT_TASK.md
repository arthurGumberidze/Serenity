# NEXT_TASK.md

Active task: **P9.5 — Атмосфера и глобальная карта**.

Starting point: P9.4 replaced the visible primitive camp with a deterministic Stone Age settlement presentation: detailed shelter/storage/crop prefabs, six readable work sites, four canonical camp-supply presentations and one centralized work-priority indicator system. Read `docs/P94_HANDOFF.md` before changing lighting, weather presentation or the global map.

Required scope:

1. Bring daylight, shadows, air/fog, water, weather/seasons and day/night into one coherent realistic-natural visual language that remains readable in normal RTS and TPS play.
2. Bring the six-location global map into the same visual language without copying another game's setting and without replacing `GlobalMapService`, canonical location/resource identity or campaign state.
3. Preserve the P9.2–P9.4 physical Terrain, NavMesh, finite resources, construction, work orders/priorities, Save/Load and all Domain/Simulation authorities. Atmosphere and map art remain replaceable Presentation.
4. Use only approved official Unity URP Terrain Sample, UMA 3 and documented project-owned derivatives. Request approval before any new external asset or dependency.
5. Verify local/global transitions, six locations, RTS/TPS readability, water/collision/navigation, weather/seasons/day-night and Save/Load with proportional EditMode/PlayMode tests, a Windows Development build and visible normal-Player screenshots. Do not hide failures.
6. Update project state, task graph, decisions, this pointer, asset registry if needed and `docs/P95_HANDOFF.md`; commit only P9.5 work locally without push. On honest completion, set the next task to **P9.6 — Интеграция и визуальная приёмка**.
7. Do not run U29/U30/U31 or reinterpret the P8.6 checkpoint. Their statuses remain PARTIAL / IN_PROGRESS / BLOCKED; see `docs/P86_HANDOFF.md`.
