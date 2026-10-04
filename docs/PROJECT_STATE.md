# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development. U04A adds only the pinned PostgreSQL provider/tooling described below and does not change the Editor or scripting backend.

## Current milestone / last completed task
U05 DONE — Local Scene / RTS Camera / Input. `Assets/Scenes/LocalGameplay.unity` is the enabled build scene and uses project-owned primitives only. Presentation owns centralized Input System intent, bounded RTS navigation and pointer interaction; Infrastructure owns the explicit scene composition root.

## Active task
U05A — Asset Pipeline + Free Asset Acquisition is next and has NOT started. Character Domain, NPC simulation, buildings, combat, third-person control and other gameplay systems remain deferred.

## U05 runtime
- Camera input actions: WASD/arrows move, screen-edge scroll, middle-mouse drag pan, wheel zoom and Q/E yaw rotation. Bindings live in `LocalGameplay.inputactions` for future rebinding.
- Navigation uses `Time.unscaledDeltaTime`, so it remains a presentation concern and is not coupled to GameClock or active simulation pause.
- Movement, zoom, edge width/speed, rotation, fixed pitch and rectangular XZ bounds are serialized settings. Camera target motion has no vertical drift.
- Cinemachine 6.6 supplies the virtual/output camera path; the input abstraction and controller do not depend on Cinemachine input callbacks.
- `WorldPointerRaycaster` provides world hit/point APIs and blocks world interaction when an EventSystem reports pointer-over-UI. `SelectionProbe` proves pointer-to-target selection with three primitive markers.
- No vendor pack is a dependency of the scene. U05A remains responsible for asset inventory/import/normalization.

## Provider and schema
- PostgreSQL detected/validated: 18.0 x64 on a private loopback test cluster.
- Npgsql 8.0.9, netstandard2.1, no ORM. NuGetForUnity 4.5.0 restores a fully pinned dependency graph from nuget.org into ignored `Assets/Packages`.
- Configuration uses local-only `DYNASTYGAME_PG_*` process environment variables. No credentials or complete connection strings are committed/logged.
- Recommended databases: `dynasty_game_dev` and separate `dynasty_game_test`; schema `serenity`.
- Migration 001 creates `save_sessions`; the migrator transactionally bootstraps `schema_migrations`, verifies version/name/SHA256 history, serializes concurrent migrators with an advisory transaction lock and rolls back failure.
- StableEntityId round-trips through PostgreSQL UUID without regeneration. Canonical save data remains the strict U04 XML payload in BYTEA. Database schema version and save-format version are distinct.
- Save is one fully parameterized atomic UPSERT. Load validates relational session/version metadata and the complete XML snapshot before `SaveCoordinator` can replace runtime state.

## Build status
Windows x64 Mono Development succeeded after U05: errors=0, warnings=2. Player output is `Builds/Windows/Serenity.exe` (667136 bytes). Exact command and diagnostics are in `docs/U05_HANDOFF.md`; full log is `Logs/U04A-build.log` because the U05 verifier reuses the inherited U04A build gate.

## Test status
91/91 EditMode tests passed (0 failed/skipped, 1.2878448 seconds), including inherited architecture/time/persistence tests and U05 camera/input/scene dependency tests. 25/25 real PostgreSQL integration tests passed (0 failed/skipped, 16.4536168 seconds). 2/2 PlayMode tests passed (0 failed/skipped, 0.1850961 seconds), validating scene composition, Cinemachine, marker count, camera raycast and selection. Exact commands and evidence are in `docs/U05_HANDOFF.md` and `docs/validation/`.

## Architecture facts
- Game.Presentation now directly references the pinned Unity Input System and Cinemachine package assemblies; project-layer dependency directions remain unchanged.
- `LocalSceneCompositionRoot` uses explicit serialized references. Runtime code does not use scene-wide find/service-locator calls for composition.
- Camera and pointer state are disposable presentation state and never enter saves, StableEntityId mappings or PostgreSQL.
- Domain, Simulation, ECS and Presentation do not reference Npgsql; assembly dependency directions are unchanged. Infrastructure is the outer provider layer.
- PostgreSQL calls are limited to explicit setup/migration and save/checkpoint boundaries. No per-frame, per-NPC, movement, AI or continuous-state SQL exists.
- Timestamps are storage metadata. Calendar and biological ticks remain the only game time.
- Provider errors are controlled/redacted. Missing and corrupt save semantics remain explicit. Failed load never replaces active state.
- Successful load still creates a replacement GameClock. Future runtime composition/UI/DOTS consumers must rebind; the provider holds no GameClock reference.
- Future normalized Character/Dynasty/City/HistoricalEvent storage remains deferred behind separate ports/migrations.

## Known limitations
U05 has a fixed-pitch placeholder camera and rectangular bounds; it does not implement terrain-aware collision, production selection UI, orders, rebinding UI, touch/gamepad navigation, third-person control or global-map switching. U04A remains synchronous/local-development-only with the previously documented limitations. Managed verification keeps stopped ignored PostgreSQL cluster directories for inspection rather than deleting them automatically.

## Working tree
The U05 commit includes only the local-scene/input/camera/interaction implementation, tests, verifier, validation evidence and required documentation. Pre-existing TutorialInfo edits, imported asset packs, unrelated ProjectSettings and update/reference documents remain uncommitted and outside the commit. Validation runs against the current working tree, so existing third-party diagnostics can appear in logs.
