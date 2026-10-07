# U18 HANDOFF Family Events, Diseases and Injuries

## Summary

U18 is complete. It adds one engine-independent canonical health/family authority, nine data-driven Stone Age conditions, effective capability modifiers, exactly-once health death through U17, four relationship events, deterministic pregnancy/birth, real canonical children with deterministic IDs and dynasty membership, Tier1/2/3/off-camera equivalence, snapshot v5 and a click-blocked debug panel. U19 was not started.

## Implementation

- Domain: condition/event/pregnancy definitions and save state; deterministic `StableEntityId` hash factory; canonical character creation with a supplied ID; dynasty member insertion.
- Simulation: batched `HealthAndFamilyService`; U10 work eligibility, U15 production efficiency and U16 research efficiency integration.
- Infrastructure/presentation: scene composition, Tier3 child registration, snapshot v5 XML/coordinator support and health/family debug overlay.
- Content: four injuries, three diseases, one temporary condition and one permanent condition. Serious conditions can incapacitate, become permanent or die; `lasting_limp` demonstrates a persistent capability penalty.

## Determinism and persistence

Condition/event/pregnancy/child IDs and random outcomes use world seed, canonical stable IDs, purpose/stream and absolute ticks. Dictionary order and presentation tier are excluded. The final route hash is `7877019868490988825`; T3-only, T3-T2-T3 and T3-T1-T3 produced the same hash and child ID. The 20-person long-run hash is `15340608186238284958`.

Snapshot v5 serializes health/family checkpoints and exactly-once markers. U17's character/dynasty snapshot serializes born children, parent links, relationships and dynasty membership. V1/V2/V3/V4 still load without a health payload. PostgreSQL schema is unchanged because it stores the validated XML opaquely; its metadata whitelist now accepts every supported v1-v5 payload.

## Validation

- Compile: succeeded; no compiler errors. Existing obsolete `FindObjectsSortMode` warnings remain in older U14/U15/U16/Tier2 tests.
- U18 EditMode: 13 passed, 0 failed, 0 skipped.
- U18 PlayMode: 2 passed, 0 failed, 0 skipped.
- Full non-PostgreSQL EditMode: 263 passed, 0 failed, 0 skipped.
- Full PlayMode: 46 passed, 0 failed, 0 skipped.
- Required U06/U10/U14/U15/U16/U16A/U17 EditMode: 99 passed.
- Required U10/U14/U15/U16/U16A/U17 PlayMode: 17 passed.
- Persistence/legacy snapshots: 35 passed; dedicated U18 v1-v4 compatibility also passed.
- GPU U18 PlayMode: 2 passed on Direct3D 11 / Intel Arc Graphics.
- PostgreSQL: 25 integration tests discovered and skipped because `DYNASTYGAME_PG_TEST_DATABASE` was absent; no U18 SQL change exists.
- Windows x64 Mono Development: succeeded; 0 errors, 2 inherited build warnings; `Builds/Windows/Serenity.exe` generated.
- Long run: 20 initial people, five pregnancies/births, three biological years; no duplicate child IDs, broken parents or invalid condition state; 0.019 seconds in the focused test.

Primary timeout-aware verifier:

```powershell
& 'C:\serenity_game\Tools\Verify-U18.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -GpuValidation -BuildPlayer -StepTimeoutSeconds 900
```

When a disposable PostgreSQL test database is configured, add `-PostgresIntegration`.

## Commands executed

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`; project: `C:\serenity_game`. Test commands intentionally omit `-quit` so Unity Test Framework completes and writes XML.

```powershell
& $Unity -batchmode -nographics -projectPath $Project -quit -logFile "$Project\Logs\U18-compile.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testCategory U18 -testResults "$Project\Logs\U18-editmode.xml" -logFile "$Project\Logs\U18-editmode.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testCategory U18 -testResults "$Project\Logs\U18-playmode.xml" -logFile "$Project\Logs\U18-playmode.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults "$Project\Logs\U18-full-edit-final.xml" -logFile "$Project\Logs\U18-full-edit-final.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testResults "$Project\Logs\U18-full-play.xml" -logFile "$Project\Logs\U18-full-play.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testCategory 'U06;U10;U14;U15;U16;U16A;U17' -testResults "$Project\Logs\U18-required-edit-regressions.xml" -logFile "$Project\Logs\U18-required-edit-regressions.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform PlayMode -testCategory 'U10;U14;U15;U16;U16A;U17' -testResults "$Project\Logs\U18-required-play-regressions.xml" -logFile "$Project\Logs\U18-required-play-regressions.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.PersistenceTests -testResults "$Project\Logs\U18-persistence.xml" -logFile "$Project\Logs\U18-persistence.log"
& $Unity -batchmode -nographics -projectPath $Project -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults "$Project\Logs\U18-postgres.xml" -logFile "$Project\Logs\U18-postgres.log"
& $Unity -batchmode -projectPath $Project -runTests -testPlatform PlayMode -testCategory U18 -testResults "$Project\Logs\U18-playmode-gpu.xml" -logFile "$Project\Logs\U18-playmode-gpu.log"
& $Unity -batchmode -nographics -projectPath $Project -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -logFile "$Project\Logs\U18-build.log"
```

All commands exited 0. Test results are listed above; the PostgreSQL command exited 0 with all 25 tests skipped for the absent database variable.

## Known limitations

No natural-death curve, disease transmission, advanced treatment, genetics, autonomous conception, childbirth complications or child presentation exists. Birth naming uses a small deterministic project-owned list because no earlier naming service exists. Capability modifiers are deterministic integer per-thousand multipliers; broader balance remains future work.

## Next task

U19 — Weather and Seasons is the next unblocked roadmap item. It has not been started.
