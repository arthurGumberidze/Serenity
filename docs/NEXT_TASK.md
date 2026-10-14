# NEXT_TASK.md

Active task: **P9.2 — перенос игрового региона на новый реалистичный мир**.

Starting point: P9.1 selectively imported and proved Unity URP Terrain Sample 1.0.3 in a separate `LocalGameplay` benchmark. Read `docs/P91_HANDOFF.md` before changing the scene.

Required scope:

1. Move the canonical P5/P8.2 gameplay anchors (territories, all six resource families, routes, buildable areas, obstacles and water boundary) onto the accepted Terrain composition without changing their stable IDs or Domain/Simulation authorities.
2. Rebuild and verify NavMesh, construction placement, selection, RTS/TPS camera access, weather/season projection, local/global resource links and Save/Load rebinding.
3. Only after focused + affected regressions, Windows build and visible RTS/TPS Player evidence pass, remove the superseded P3/P5/P8.2 visual map objects. Keep the P9.1 generator/source assets reversible.
4. Do not run U29/U30/U31 or reinterpret the P8.6 checkpoint. Their statuses remain PARTIAL / IN_PROGRESS / BLOCKED; see `docs/P86_HANDOFF.md`.
