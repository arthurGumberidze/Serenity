# U04A local PostgreSQL workflow

Unity 6000.6.4f1, Windows x64 Mono, .NET Standard 2.1. Active simulation stays in RAM; PostgreSQL is durable checkpoint/history storage.

## Package restore

Before opening Unity on a fresh checkout, run `./Tools/Restore-NuGet.ps1`. Requires a .NET SDK; validated with SDK 10.0.401/runtime 10.0.12. `dotnet-tools.json` pins NuGetForUnity.Cli 4.5.0; the script permits its .NET 9 executable to roll forward to installed .NET 10. UPM pins NuGetForUnity 4.5.0 by tag and lockfile commit. `Assets/packages.config` pins Npgsql 8.0.9 and all transitive dependencies. `Assets/NuGet.config` uses the official nuget.org feed. Restored `Assets/Packages` is ignored; no manually sourced DLLs are committed. The script upgrades generated analyzer PluginImporter metadata to version 2 for Unity 6000.6.

Npgsql uses netstandard2.1; dependencies use that framework where available, otherwise netstandard2.0. NuGetForUnity selects frameworks and configures explicit references. Only Infrastructure and Editor tests explicitly reference Npgsql.dll. No ORM is installed. Editor/backend/API level are unchanged; other platforms/backends are unverified.

## Existing development server

Start your PostgreSQL service using your normal administration workflow. Create separate `dynasty_game_dev` and `dynasty_game_test` databases. Example with your own role and interactive authentication:

```powershell
createdb -h 127.0.0.1 -U YOUR_LOCAL_ROLE -W dynasty_game_dev
createdb -h 127.0.0.1 -U YOUR_LOCAL_ROLE -W dynasty_game_test
```

The role needs CONNECT/CREATE on its databases; the migrator creates schema `serenity` and its tables. A future runtime role can have only SELECT/INSERT/UPDATE on save_sessions. Migration privileges are not required for ordinary saves.

Configure process environment before launching Unity:

| Variable | Value |
|---|---|
| DYNASTYGAME_PG_HOST | 127.0.0.1, localhost or ::1; remote hosts rejected |
| DYNASTYGAME_PG_PORT | Local server port, default 5432 |
| DYNASTYGAME_PG_DATABASE | dynasty_game_dev |
| DYNASTYGAME_PG_TEST_DATABASE | dynasty_game_test; distinct from dev and ending in _test |
| DYNASTYGAME_PG_USER | Your local role, outside Git |
| DYNASTYGAME_PG_PASSWORD | Password, outside Git; optional if local authentication permits |

No credentials/complete connection strings belong in source, assets, ProjectSettings, command arguments or logs. There is no automatic .env reader. `.env*`, `*.local.config` and `.local/` are ignored as defense in depth. Supply secrets through your secure launcher or interactive prompt, avoiding literal passwords in shell history. Configuration ToString is redacted; store/migrator errors expose only operation and SQLSTATE, never raw provider messages/inner exceptions. SSL is disabled for this local-only developer configuration.

Close the interactive project Editor, then apply migrations and verify:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Tests.PostgresDeveloperSetup.Migrate -logFile C:\serenity_game\Logs\U04A-setup.log
./Tools/Verify-U04A.ps1
```

The setup entry point is Editor-only and reads StreamingAssets/Serenity/Postgres. It does not create/delete databases. SQLSTATE 3D000: create the missing database. 28P01: correct authentication. Connection failure: check local service/port/config. Never log the connection string.

## Isolated integration verification

`./Tools/Verify-U04A.ps1 -ManagedTestCluster` uses installed PostgreSQL 18 binaries, a new ignored `.local/pg-u04a-<random>` directory, a free loopback port and SCRAM authentication. Random role/password are generated in memory; a temporary ignored initdb password file is immediately deleted. Only `dynasty_game_test` is created; user databases are untouched. Tests assert that no Serenity schema exists before migration 001. Finally stops only this cluster and restores inherited environment. Stopped data directories remain ignored for inspection; no DROP DATABASE or automatic recursive deletion exists.

The port is released before pg_ctl starts; a port race fails startup explicitly. Do not run parallel verification against one Unity checkout. The script restores packages, runs core tests excluding PostgresIntegration, real PostgreSQL tests, and Windows Development build. Without DB configuration it still runs core/build but reports incomplete validation and exits with failure. Direct Unity test runs skip only the DB fixture when TEST_DATABASE is absent; configured connection errors fail.

## Schema and evolution

- `serenity.schema_migrations`: version integer PK, name text, normalized-SQL SHA256 checksum text, applied_at timestamptz.
- `serenity.save_sessions`: slot_id uuid PK, session_id uuid, save_format_version integer, payload bytea, created_at/updated_at timestamptz. IDs must be nonempty; payload is 1..65536 bytes. Multiple slots may capture one session.
- `001_initial_persistence.sql` creates save_sessions. Schema/ledger bootstrap is part of the migrator transaction. Advisory transaction lock serializes migrators. Ordered scripts must match existing history exactly; changed/missing/future versions fail. All pending migrations and history inserts commit or roll back together. Append new scripts; never edit applied ones.
- XML snapshot remains canonical; session/version metadata must match it. Database schema version and SaveSnapshot.FormatVersion are independent. No snapshot format migration exists yet.
- One UPSERT atomically replaces a slot; concurrent writes use last committed writer wins. Created timestamp is retained. Wall-clock timestamps are storage metadata; game time remains snapshot ticks.
- Connections close after each call, pooling is off, connect timeout 5 seconds and command timeout 15 seconds. Use the synchronous coordinator only at committed simulation barriers. Never call SQL for movement, Update, AI/per-NPC ticks or continuous HP changes.
- Future Character/Dynasty/City/HistoricalEvent repositories get separate Simulation ports and migrations, retaining StableEntityId UUID mapping. U04A tests mapping for these four future roles without gameplay tables/models, following the current request's narrower scope. Successful Load replaces GameClock; future composition must rebind consumers. Storage holds no clock references.

Sources: [Npgsql 8 compatibility](https://www.npgsql.org/doc/release-notes/8.0.html), [Npgsql 8.0.9](https://www.nuget.org/packages/Npgsql/8.0.9), [NuGetForUnity 4.5.0](https://github.com/GlitchEnzo/NuGetForUnity/tree/v4.5.0).
