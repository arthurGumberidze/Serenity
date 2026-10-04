# U04A — PostgreSQL Persistence Layer

## Scope and prerequisites
Work continued from `71e8c9f U04: implement stable IDs and save/load core`. Before implementation, U04 was confirmed DONE, NEXT_TASK selected U04A, HEAD contained 71e8c9f, and `Tools/Verify-U04.ps1` passed the existing 70 EditMode tests plus Windows Development build. Read AGENTS, PROJECT_STATE, NEXT_TASK, TASK_GRAPH, DECISIONS, ARCHITECTURE, U04_HANDOFF, both FRS documents' persistence/save material, Appendix A and `docs/updates/2026-10-03_postgres_assets.md`.

Only U04A is implemented. U05, U05A assets, Character Domain, gameplay/history repositories, realtime SQL and production/cloud database work were not started.

## Provider and dependency
Local binaries report PostgreSQL 18.0 x64. The implementation uses Npgsql 8.0.9 without EF, Dapper or another ORM. This Npgsql line supplies a .NET Standard 2.1 target compatible with the project's Unity 6000.6.4f1 API profile and passed Editor compilation plus Windows x64 Mono Development build.

NuGetForUnity 4.5.0 is pinned as a tagged UPM Git package and lockfile commit. Its CLI is pinned in `dotnet-tools.json`; `Tools/Restore-NuGet.ps1` restores Npgsql and its complete pinned transitive graph from nuget.org into ignored `Assets/Packages`. The restore script also upgrades missing generated analyzer PluginImporter version metadata required by Unity 6000.6. `autoReferenced=false` and asmdef precompiled references expose Npgsql only to Game.Infrastructure and Game.Tests. Architecture tests verify Domain, Simulation, ECS and Presentation have no provider reference. No random DLL is committed.

## Configuration and security
`PostgresConfiguration` reads `DYNASTYGAME_PG_HOST`, `PORT`, `DATABASE`/`TEST_DATABASE`, `USER` and `PASSWORD` from the process environment. U04A accepts loopback hosts only. The integration database must be distinct from development and end with `_test`. Recommended names are `dynasty_game_dev` and `dynasty_game_test`; schema is `serenity`.

Secrets are not stored in source, ScriptableObjects, JSON/XML config, ProjectSettings, docs, tests or verification artifacts. `.env*`, `*.local.config`, `.local/` and restored packages are ignored. The connection string is private and ToString is redacted. Public provider exceptions deliberately discard raw exception text/inner exceptions and expose only safe operation and SQLSTATE. SQL parameters are used for every runtime/migration-history value. Repository migration SQL itself is trusted versioned code.

`docs/U04A_POSTGRES_SETUP.md` documents ordinary local setup, migration command, errors and the isolated test workflow. Neither setup nor verification drops a database. The managed verifier creates only its own fresh ignored cluster and `dynasty_game_test`, then stops only that cluster. It does not delete the stopped data directory automatically.

## Migrations and schema
`PostgresMigrations` explicitly applies an ordered contiguous set. It starts one transaction, acquires advisory transaction lock 739184042, creates schema/ledger if missing, reads existing history, then checks every version, filename and normalized-SQL SHA256. A missing, changed, noncontiguous or future entry fails. Each new script and its ledger insert are in the same transaction; PostgreSQL DDL and history roll back together on error. Concurrent migrators serialize, and applying migration 001 repeatedly produces one ledger entry.

Migration list:

1. `001_initial_persistence.sql` creates `serenity.save_sessions`.

Final U04A schema:

- `serenity.schema_migrations(version integer PK, name text, checksum text, applied_at timestamptz)`.
- `serenity.save_sessions(slot_id uuid PK, session_id uuid, save_format_version integer, payload bytea, created_at timestamptz, updated_at timestamptz)` with nonempty UUID checks and payload length 1..65536.

Database migration version and `SaveSnapshot.FormatVersion` are separate. The former controls relational schema scripts; the latter controls strict U04 XML compatibility. Migration scripts do not contain credentials.

## PostgresSaveStore behavior
The unchanged `ISaveStore` is sufficient. `PostgresSaveStore` validates/serializes the detached `SaveSnapshot` before opening a connection. Save runs one atomic parameterized `INSERT ... ON CONFLICT DO UPDATE`, so a slot is never partially updated. It preserves `created_at` and refreshes `updated_at`; both are storage metadata, never GameCalendar time. Concurrent writes use PostgreSQL last-committed-writer behavior and leave one complete snapshot.

Load selects by parameterized UUID, reports a missing slot as FileNotFoundException, rejects unsupported relational save version, strictly decodes the existing XML and checks that relational session UUID matches payload identity. Invalid XML/ID/ticks, metadata mismatch and unsupported format fail before `SaveCoordinator` can replace state. StableEntityId uses exact N-format Guid-to-PostgreSQL-UUID-to-Guid conversion without regeneration. Tests cover this mapping for the future Character, Dynasty, City and HistoricalEvent owners without implementing their models/tables.

Connections are opened and disposed per operation with pooling disabled, 5-second connection timeout and 15-second command timeout. The store contains no GameClock/runtime reference. Callers remain responsible for a committed tick barrier. A successful `SaveCoordinator.Load` creates a replacement GameClock, so future composition/UI/DOTS consumers must rebind. Failure preserves the old clock/session.

Allowed uses are manual save, autosave/checkpoint and future batched major-history writes. Per-frame, per-NPC, movement, AI tick, HP-change and other realtime SQL are forbidden. Future normalized `ICharacterRepository`, `IDynastyRepository`, `ICityRepository` and `IHistoricalEventRepository`-style ports/tables are deferred and must reuse StableEntityId.

## Tests and verification
Unit/fast coverage adds configuration validation/redaction, local/test database separation, UUID exactness, migration ordering/checksum behavior, early save validation, redacted errors, shipped migration discovery, and assembly/provider boundaries. These run without PostgreSQL. The database fixture is categorized `PostgresIntegration`; absent test configuration is explicitly ignored in an ordinary direct run, while configured connection errors fail.

Real PostgreSQL tests cover connection/database, clean migration, history, idempotent and concurrent apply, altered history, transactional DDL rollback, new save/load, all five speeds, pause/running, exact calendar and biological ticks, multiplier, StableEntityId and four future identity roles, overwrite/timestamps, missing save, invalid XML/ID/ticks, unsupported payload/metadata version, session metadata mismatch, connection failure/redaction/runtime safety, concurrent UPSERT, explicit transaction rollback, new store/connection persistence, and GameClock replacement only after successful load.

Primary command from `C:\serenity_game`:

```powershell
./Tools/Verify-U04A.ps1 -ManagedTestCluster
```

The script restores dependencies, initializes a new SCRAM-authenticated PostgreSQL 18 cluster under ignored `.local`, creates a separate clean `dynasty_game_test`, runs:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-core-tests.log -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults C:\serenity_game\Logs\U04A-core-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-postgres-tests.log -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults C:\serenity_game\Logs\U04A-postgres-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04A-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

Final results: core 86 passed, 0 failed, 0 skipped in 1.9070593 seconds; PostgreSQL integration 25 passed, 0 failed, 0 skipped in 22.1686577 seconds; Windows x64 Mono Development succeeded with errors=0 and warnings=2. Evidence is committed as `docs/validation/U04A-core-tests.xml`, `U04A-postgres-tests.xml` and `U04A-diagnostics.txt`. Baseline evidence is `U04A-baseline-tests.xml` (70/70 before U04A). The real DB fixture proves clean migration 001, repeated/concurrent idempotence, failure rollback and loading after store/connection recreation. Windows output is `Builds/Windows/Serenity.exe` (667136 bytes; reused executable timestamp is not build evidence, while BuildReport success and rebuilt player data are).

Known nonfatal environment diagnostics include Unity licensing token notices, two unsupported `Hidden/ChartRasterizerHardware` GPU messages, and package/import messages seen in earlier tasks. Final BuildReport contains two warnings (the individual texts are not expanded in the log). An earlier interrupted build observed a pre-existing third-party Hodaart obsolete-API warning, but the final build log did not repeat it. No U04A C# compiler error or provider warning remains.

## Files and deferred work
Created: PostgreSQL Infrastructure source folder, migration SQL, three PostgreSQL test/source files, NuGet configs/tool manifest, restore/verify scripts, setup/handoff docs and validation evidence. Modified: Infrastructure/Test asmdefs and Infrastructure AssemblyInfo, package manifest/lock, gitignore, ARCHITECTURE, DECISIONS, PROJECT_STATE, TASK_GRAPH and NEXT_TASK.

Unrelated pre-existing TutorialInfo edits, ProjectSettings changes, imported asset packs, update/reference documents and asset-planning files remain uncommitted. They were present during validation but are outside the U04A commit.

Deferred: async/background save orchestration, connection pooling/production security, runtime composition/menu/autosave, snapshot format migration/checksum expansion, normalized world/history repositories, other platform/backend validation, U04 XML size evolution, and GameClock consumer rebinding. U05 is next and is not started.
