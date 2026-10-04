# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development. U04A adds only the pinned PostgreSQL provider/tooling described below and does not change the Editor or scripting backend.

## Current milestone / last completed task
U04A DONE — PostgreSQL Persistence Layer. The U04 `ISaveStore`, `SaveCoordinator`, `SaveSnapshot`, `StableEntityId`, XML codec and FileSaveStore remain the architectural base. Infrastructure now provides local PostgreSQL configuration, checksummed transactional migrations and `PostgresSaveStore`. Runtime simulation remains in RAM.

## Active task
U05 — Local Scene / RTS Camera / Input is next and has NOT started. U05A asset work, Character Domain and other gameplay systems were not implemented by U04A.

## Provider and schema
- PostgreSQL detected/validated: 18.0 x64 on a private loopback test cluster.
- Npgsql 8.0.9, netstandard2.1, no ORM. NuGetForUnity 4.5.0 restores a fully pinned dependency graph from nuget.org into ignored `Assets/Packages`.
- Configuration uses local-only `DYNASTYGAME_PG_*` process environment variables. No credentials or complete connection strings are committed/logged.
- Recommended databases: `dynasty_game_dev` and separate `dynasty_game_test`; schema `serenity`.
- Migration 001 creates `save_sessions`; the migrator transactionally bootstraps `schema_migrations`, verifies version/name/SHA256 history, serializes concurrent migrators with an advisory transaction lock and rolls back failure.
- StableEntityId round-trips through PostgreSQL UUID without regeneration. Canonical save data remains the strict U04 XML payload in BYTEA. Database schema version and save-format version are distinct.
- Save is one fully parameterized atomic UPSERT. Load validates relational session/version metadata and the complete XML snapshot before `SaveCoordinator` can replace runtime state.

## Build status
Windows x64 Mono Development succeeded: errors=0, warnings=2. Player output remains `Builds/Windows/Serenity.exe` (667136 bytes). Exact command and diagnostics are in `docs/U04A_HANDOFF.md`; full log is `Logs/U04A-build.log`.

## Test status
86/86 core EditMode tests passed (0 failed/skipped, 1.9070593 seconds) without PostgreSQL; 25/25 real PostgreSQL integration tests passed (0 failed/skipped, 22.1686577 seconds). The managed verification creates a fresh ignored SCRAM-authenticated PostgreSQL 18 cluster and separate test database, then tests migration creation/idempotence/concurrency/rollback, save/load/overwrite/missing/corruption/version errors, UUID and all clock fields, connection recreation/failure, concurrent UPSERT and runtime failure safety. Exact commands and evidence are in `docs/U04A_HANDOFF.md` and `docs/validation/`.

## Architecture facts
- Domain, Simulation, ECS and Presentation do not reference Npgsql; assembly dependency directions are unchanged. Infrastructure is the outer provider layer.
- PostgreSQL calls are limited to explicit setup/migration and save/checkpoint boundaries. No per-frame, per-NPC, movement, AI or continuous-state SQL exists.
- Timestamps are storage metadata. Calendar and biological ticks remain the only game time.
- Provider errors are controlled/redacted. Missing and corrupt save semantics remain explicit. Failed load never replaces active state.
- Successful load still creates a replacement GameClock. Future runtime composition/UI/DOTS consumers must rebind; the provider holds no GameClock reference.
- Future normalized Character/Dynasty/City/HistoricalEvent storage remains deferred behind separate ports/migrations.

## Known limitations
U04A is synchronous, local-development-only and validates Windows Mono; other platforms/backends are unverified. It creates schemas/tables but never creates/drops a user's database. No connection pool, async save queue, history repositories, snapshot-format migrations, checksums beyond the strict XML/schema constraints, UI/autosave, cloud/remote production database or realtime SQL simulation are included. The U04 XML version-1 64 KiB limit remains. Managed validation keeps stopped ignored cluster directories for inspection rather than deleting them automatically.

## Working tree
The U04A commit includes only provider/tool/package configuration, migrations, tests, validation evidence and required project documentation. Pre-existing TutorialInfo, imported asset packs, user ProjectSettings and update/reference documents remain uncommitted and outside the U04A commit. Validation runs against the current working tree, so existing third-party code warnings can appear in logs.
