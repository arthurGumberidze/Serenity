# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0. No version/package changes in U04.

## Current milestone / last completed task
U04 DONE — Stable IDs + Save/Load Core. StableEntityId, immutable versioned snapshots, SaveCoordinator, ISaveStore, strict XML serialization and atomic local file storage are implemented. U03 time round-trips without identity regeneration or partial restore.

## Active task
U04A — PostgreSQL Persistence Layer is next and has NOT started. No SQL, Npgsql, database schema, migrations, camera/input or Character Domain was added.

## Build status
Windows x64 / Mono / Development succeeded: exit 0; errors=0; warnings=2. Player: Builds/Windows/Serenity.exe (667136 bytes). Compilation and assembly boundary checks pass. Diagnostics and exact commands: docs/U04_HANDOFF.md; full logs remain in Logs/U04-tests.log and Logs/U04-build.log.

## Test status
70/70 EditMode tests passed, 0 failed, 0 skipped, 2.3469298 seconds. Includes all prior 35 tests and 35 persistence tests. Evidence: docs/validation/U04-tests.xml. Test suite covers exact 64-bit ticks, stable IDs, settings continuation, all speeds, pause, invalid/corrupt/unknown saves, file overwrite and failed atomic replacement.

## Known limitations / blockers
No U04 blocker. Synchronous owner-thread checkpoint API requires a committed simulation barrier and a single writer per save directory. Successful Load replaces the clock; future composition must rebind consumers. Version 1 has no migrations, checksum, UI/autosave, game-content sections or PostgreSQL. Its 64 KiB size limit must evolve with future schema versions. Atomic replacement does not promise universal power-loss durability. Biological balance discrepancy described in D-011/U03_HANDOFF remains deferred.

## Architecture facts
- Domain and Simulation remain pure C#; assembly dependency graph unchanged.
- Domain identity is a readonly non-empty GUID value object; canonical encoding is lowercase N format. GameObject/ECS handles are disposable projections, never persistent references.
- Runtime state remains in RAM. GameTimeState is unchanged; GameClock gains only a read-only biological multiplier accessor.
- SaveSnapshot version 1 contains session ID, calendar/biological ticks, selected speed, pause and biological multiplier. Slot ID is separate from session ID.
- Simulation coordinates capture/validated replacement; Infrastructure implements XML and file storage through ISaveStore.
- Definitions remain ScriptableObject/configuration; mutable canonical save-state is data-only.
- D-012 and ARCHITECTURE document U04 contracts and failure behavior.

## Working tree
Pre-existing TutorialInfo edits, QualitySettings/URPProjectSettings changes, vendor assets and update/reference documents remain outside the U04 commit. Validation ran against this current working tree, not a clean checkout. Only explicitly staged U04 code, tests and documentation are committed.
