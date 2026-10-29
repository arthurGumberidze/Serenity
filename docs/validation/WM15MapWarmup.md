# WM15 resident map cache and warmup validation

Date: 2026-10-29

Unity: 6000.6.4f1

Build: `Builds/WM15MapWarmup/Serenity.exe`

## Scope

- Immutable seed/version-scoped cache for tile height/paint arrays and strategic overview pixels.
- Bounded post-bootstrap preparation with two worker tasks and one main-thread resident tile per frame.
- All 16 compact-map terrain and population roots retained and reused after warmup.
- Incremental pooled tree interaction colliders.
- One-time bottom-centre `Подготовка карты: N%` UI for incomplete caches.
- Dynamic animals do not turn construction footprints into permanent terrain blockers.

## EditMode

Command:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.P109TiledWorldRuntimeTests;Game.Tests.Editor.WorldTilePreparationCacheTests" -testResults C:\serenity_game\Artifacts\WM15MapWarmup\editor-j.xml -logFile C:\serenity_game\Artifacts\WM15MapWarmup\editor-j.log
```

Result: Unity exit 0; 6/6 passed, 0 failed. The cache regression covers tile/overview round-trip, completion
marker and seed isolation.

## PlayMode full residency

Command:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P109TiledWorldStage2PlayModeTests.FullMapWarmupKeepsTerrainAndPopulationViewsResidentAcrossCameraTravel -testResults C:\serenity_game\Artifacts\WM15MapWarmup\playmode-f.xml -logFile C:\serenity_game\Artifacts\WM15MapWarmup\playmode-f.log
```

Result: Unity exit 0; 1/1 passed. The test waits for all 16 tiles, traverses every tile centre and verifies that
terrain count and the identities of all pooled population roots are unchanged.

## Windows Development build

Command:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WM15MapWarmup/Serenity.exe -quit -logFile C:\serenity_game\Artifacts\WM15MapWarmup\build-e.log
```

Result: Unity exit 0; `Build Finished, Result: Success`; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=82`.

## Actual Player smoke

Command:

```text
C:\serenity_game\Builds\WM15MapWarmup\Serenity.exe -batchmode -p109FeedbackSmoke -logFile C:\serenity_game\Artifacts\WM15MapWarmup\player-final-4\player.log
```

Result: Player exit 0. Evidence line:

```text
P109_FEEDBACK_SMOKE_PASSED: LocalGameplay, UI, starter residents, valid lake landings=24, map cache startup=hit, cached tiles=16, worst warm camera refresh ms=5,4
```

The same route before incremental proxy pooling measured 1205.7 ms. The final warm route measures 5.4 ms and
logs `WM15_MAP_WARMUP_COMPLETE tiles=16 cacheHits=16`. No accepted terrain/water presentation was changed.
