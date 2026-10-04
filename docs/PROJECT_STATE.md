# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development. U04A adds only the pinned PostgreSQL provider/tooling described below and does not change the Editor or scripting backend.

## Current milestone / last completed task
U05A DONE — Asset Pipeline + Free Asset Acquisition. Selected licensed Stone Age source files now feed Serenity-owned runtime wrappers and a development-only gallery; `Assets/Scenes/LocalGameplay.unity` remains the sole enabled build scene and preserves the completed U05 RTS camera/input foundation.

## Active task
U06 — Character Domain Tier 1 is next and has NOT started. U05A stopped at asset readiness; Character Domain, NPC simulation, building gameplay, combat, third-person control and all other gameplay systems remain deferred.

## U05A asset foundation
- External download staging is the ignored `serenity_games_assets` tree. Only seven selected WizardHat FBXs were copied into `Assets/Game/Art/ThirdParty`, each with explicit source/license records; unknown-provenance files remain quarantined.
- Gameplay-facing content lives in Serenity-owned prefabs under `Assets/Game/Art/Prefabs`; wrappers reference vendor/source content without putting Serenity state or code into vendor packages.
- Selected Hodaart Characters 01/02 are the male/female Tier 1 placeholder candidates with valid Human Avatars and idle/walk/run coverage. A project-owned 832-triangle single-mesh Tier 2 placeholder is separate.
- Stone Age readiness includes axe, spear, torch, animated boar, tree, rock and campfire plus primitive project-owned shelter/storage placeholders. Production Stone Age clothing, final characters/buildings, crafting props, broader wildlife and missing work/combat/carry/death animation remain documented gaps.
- `StoneAgeAssetGallery` is development-only and excluded from Build Settings. Scoped editor tooling normalizes only registered files and never acts as a global AssetPostprocessor.

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
Windows x64 Mono Development succeeded after U05A: errors=0, warnings=2. Player output is `Builds/Windows/Serenity.exe` (667136 bytes). Exact commands and diagnostics are in `docs/U05A_HANDOFF.md`; the verifier reuses the inherited U04A build gate.

## Test status
97/97 EditMode tests passed (0 failed/skipped, 1.3318513 seconds), including six U05A prefab/importer/registry/build-scene validation tests and all inherited architecture/time/persistence/camera/input tests. 25/25 real PostgreSQL integration tests passed (0 failed/skipped, 15.727664 seconds). 2/2 PlayMode tests passed (0 failed/skipped, 0.2174304 seconds), revalidating LocalGameplay composition, Cinemachine, marker count, camera raycast and selection. Exact commands and evidence are in `docs/U05A_HANDOFF.md` and `docs/validation/`.

## Architecture facts
- External staging, selected third-party source and Serenity runtime prefabs are separate ownership zones. Vendor files are never gameplay state owners, and package folder layout is not a gameplay API.
- U05A importer automation is explicit and allow-listed. It normalizes registered FBXs to the one-unit/one-metre policy, creates project-owned URP material variants/wrappers and leaves unrelated vendor imports untouched.
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
The U05A character and primitive-building selections are readiness placeholders, not the final semi-realistic Stone Age visual baseline. No selected content has authored LOD chains; human work/gather/attack/carry/death animation, primitive clothing, final buildings/crafting props and broader wildlife remain missing or license-blocked. U05 retains its fixed-pitch placeholder camera and rectangular bounds. U04A remains synchronous/local-development-only with its documented limitations.

## Working tree
The U05A commit is allow-listed to the exact Hodaart dependencies used by two wrappers, seven licensed staging-derived FBXs, their evidence, Serenity content/tooling/tests, validation evidence and documentation. Pre-existing TutorialInfo edits, unselected vendor packs, `_Recovery`, unrelated ProjectSettings and update/reference documents remain uncommitted. The whole `serenity_games_assets` tree remains ignored and outside Git.
