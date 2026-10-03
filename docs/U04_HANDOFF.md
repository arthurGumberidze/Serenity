# U04 — Stable IDs + Save/Load Core

## Prerequisites and scope
Verified 6055749 (U03: implement deterministic game time) is HEAD/ancestor, U03 is DONE, and NEXT_TASK selected U04. Read AGENTS, PROJECT_STATE, NEXT_TASK, TASK_GRAPH, DECISIONS, ARCHITECTURE and U03_HANDOFF. Existing U03 evidence was 35/35 passed; final U04 verification reran all those tests successfully. Relevant FRS: FR-SIM-003/006/007, FR-LOD-001..004, FR-CHAR, FR-SAVE-001..004, sections 42.2/42.5 and v0.3 Appendix A. Full future character/history persistence remains future scope.

## Implemented API and data
- Domain/StableEntityId.cs: readonly GUID value object; NewId for new identity, Parse/TryParse for restoration, equality/operators/hash code and deterministic lowercase N encoding. Non-empty GUID has wide uniqueness without coupling to Unity or a central counter. Default/empty/malformed IDs are rejected at persistence boundaries. Load never calls NewId.
- Domain/SaveSnapshot.cs: immutable version-1 snapshot with SessionId, CalendarTicks, BiologicalTicks, SpeedMultiplier, IsPaused and BiologicalMultiplier. It copies U03 state and returns detached time instances. Separate slot ID supports multiple checkpoints of one session. No timestamp is needed.
- Simulation/ISaveStore.cs: minimal synchronous provider-neutral Save(slotId, snapshot)/Load(slotId) port. File/provider errors remain explicit exceptions. No speculative repositories.
- Simulation/SaveCoordinator.cs: validates capture using U03 GameClock; on load constructs the entire replacement clock/settings before assigning Clock and SessionId. Failed load leaves both and the old clock object unchanged. Successful load requires consumers to rebind to the new Clock. Storage never touches runtime objects.
- Infrastructure/SaveXmlCodec.cs: standard-library XML, exact integer ticks and numeric speed, no new packages. Strict required/duplicate/unknown-field validation; non-empty ID, time, settings and speed validation. DTD disabled, 64 KiB version-1 limit. Unsupported versions: NotSupportedException; malformed/invalid saves: InvalidDataException. No silent migrations or rerolls.
- Infrastructure/FileSaveStore.cs: GUID slot filenames; encode/validate before write; same-directory unique temp file; Flush(true); File.Replace or File.Move. No delete-before-write or unsafe fallback. Failed replacement keeps prior bytes; best-effort temp cleanup. Missing save: FileNotFoundException (or DirectoryNotFoundException for missing root). Other I/O failures propagate.
- GameClock.cs: only adds a read-only BiologicalMultiplier accessor; GameTimeState is unchanged. Persisting custom settings preserves future time evolution as well as existing state.

## Validation
From C:\serenity_game:

```powershell
./Tools/Verify-U04.ps1
```

Exact Unity invocations:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04-tests.log -runTests -testPlatform EditMode -testResults C:\serenity_game\Logs\U04-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U04-build.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
```

Results: 70 passed, 0 failed, 0 skipped, 2.3469298 seconds; tests exit 0. Windows x64 Mono Development: Succeeded, errors=0, warnings=2, exit 0; build duration reported 39.932 seconds. Serenity.exe is 667136 bytes (Unity's reused player executable timestamp is not build evidence; the successful report and rebuilt data/assemblies are). Full logs remain in Logs; committed evidence is validation/U04-tests.xml and validation/U04-diagnostics.txt. git diff --check for staged U04 changes is clean.

35 new test cases cover new/invalid/equal/restored IDs, detached snapshots, exact >2^53 and near-long-max ticks, five speeds, pause, save/change/load, fresh session identity restoration, custom settings continuation, overwrite, missing saves, bad fields/types/bounds/duplicates, unknown versions, malformed XML/DTD, invalid runtime save safety, blocked atomic replacement and oversized input. File failure test holds the old slot open exclusively and confirms unchanged bytes and temp cleanup after failure.

## Diagnostics and environment
Initial sandbox Unity launch failed during startup in BuildReportRestService socket initialization (exit -536870911). An overlapping retry exited 1. After the first process ended, an approved outside-sandbox run completed tests and build. Final logs contain nonfatal licensing validation/access-token messages and a primary listening socket diagnostic, unresolved package asmref messages (Cinemachine HDRP and Entities Properties internals), package partial-class import messages and two Hidden/ChartRasterizerHardware GPU-support messages. Build summary reports two warnings; their individual BuildReport texts are not expanded in the log. U00-U03 documented two debug-shader stripping warnings, but that count alone is not proof these are identical. No C# compile errors or U04-code warnings were found. No package/vendor repairs were made.

## Limits and handoff
One simulation owner thread, no concurrent ticks/jobs, one writer per directory. Caller supplies the synchronization barrier. No async queue, crash-recovery journal, backups, checksums, migration framework, menu, autosave or scene wiring. Atomic replacement protects the existing slot against tested I/O failures, not every power-loss/filesystem scenario. No new Character/World/Dynasty records or tier transitions. Future schema extensions must explicitly evolve version/validation and remove or increase the version-1 size cap as appropriate.

U04A is next, NOT implemented: PostgreSQL driver, configuration/secrets, schema migrations, transactions and separate-database integration tests. No Npgsql/SQL/connection strings were added. D-012 and ARCHITECTURE record the implemented boundary. NEXT_TASK selects U04A and TASK_GRAPH contains its TODO row.

Created files: the seven C# files listed above including Tests/Editor/PersistenceTests.cs, each with .meta; Tools/Verify-U04.ps1; this handoff; validation/U04-tests.xml; validation/U04-diagnostics.txt. Modified: GameClock.cs, PROJECT_STATE.md, TASK_GRAPH.md, NEXT_TASK.md, DECISIONS.md, ARCHITECTURE.md. Existing vendor assets, TutorialInfo edits, user ProjectSettings and update/reference documents are excluded from the commit and retained locally. Verification used the current working tree.
