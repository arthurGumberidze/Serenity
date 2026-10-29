# WM15WaterReview5 validation

Date: 2026-10-29  
Unity: 6000.6.4f1  
Scope: ordinary-lake opacity and continuous river-to-marsh presentation only.

## Final capture

```powershell
$editor='C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor'
$output='C:\serenity_game\Artifacts\WM15WaterReview5'
$unityArgs=@(
  '-batchmode','-projectPath','C:\serenity_game',
  '-executeMethod','Game.Infrastructure.Editor.WM15EnvironmentReviewCapture.Begin',
  '-wm15EnvironmentPreview',$output,
  '-logFile',(Join-Path $output 'capture-a.log'))
Start-Process -FilePath (Join-Path $editor 'Unity.exe') -WorkingDirectory $editor `
  -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
```

Result: Unity exit 0. `capture-a.log` contains `WM15_ENVIRONMENT_REVIEW_CAPTURE` and no `error CS`,
`Shader error`, unhandled exception, `NullReferenceException` or `ArgumentException`.

Artifacts:

- `Artifacts/WM15WaterReview5/lake.png`
- `Artifacts/WM15WaterReview5/marsh.png`
- `Artifacts/WM15WaterReview5/capture-a.log`

The lake and marsh were visually inspected at original resolution. The lake is materially denser than the
previous candidate. The marsh view shows a continuous change from blue river water to dark olive opaque water
without a discrete material band. Mountain terrain and the mountain-lake profile were not changed or captured.

## Focused EditMode validation

The first 16-test run passed 15/16. The only failure was a stale assertion that expected a 4x4 fishing-pier
footprint while the canonical tracked building catalog is 6x6. The placement route itself found the required
candidates. The assertion was aligned with the catalog; the isolated regression then passed 1/1.

Final command:

```text
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.WM15CurrentFeedbackTests;Game.Tests.Editor.WM15CurrentPresentationTests;Game.Tests.Editor.WM15ShorePlacementTests;Game.Tests.Editor.WM04WorldMapTerrainPaintTests" -testResults C:\serenity_game\Artifacts\WM15WaterReview5\editor-b.xml -logFile C:\serenity_game\Artifacts\WM15WaterReview5\editor-b.log
```

Result: Unity exit 0; **16 total / 16 passed / 0 failed**; NUnit duration `175.9548275 s`. No C# or shader
compilation errors and no unhandled exception.

## Windows Development build

```text
Unity.exe -batchmode -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WM15WaterFinal/Serenity.exe -quit -logFile C:\serenity_game\Artifacts\WM15WaterReview5\build-a.log
```

Result: Unity exit 0; `Build Finished, Result: Success`; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=85`.
Output: `Builds/WM15WaterFinal/Serenity.exe`.

## Player world-creation repair

The first delivered Player reported:

```text
InvalidOperationException: The approved Procedural Water Shader URP asset is not available.
```

The shader existed in the project but was referenced only through `Shader.Find`, so the Player build stripped
it. Its GUID `bbaacdad9e0d49f4594c986566db96c4` is now in Graphics Settings Always Included Shaders. The production
build entry point also fails before compilation if `FX/ProceduralWater` cannot be found or is not retained.

The first post-shader-fix Player launch removed that exception and exposed a second world-creation blocker at
the accepted steep rock-spire terrain: no valid arrival stance existed inside its authored protection radius.
The planner now keeps the POI in place and selects the nearest deterministic, road-connected, dry and
slope-valid stance within a maximum 64 m fallback radius.

Focused command:

```text
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.WM11WorldMapPoiExplorationTests.EveryPointHasDrySlopeBoundedApproachFromReachableWm09Route;Game.Tests.Editor.WM15AfternoonFeedbackTests.NewWorldPoiApproachesStayDryAcrossReportedWorldKeys" -testResults C:\serenity_game\Artifacts\WM15WaterReview5\editor-shaderfix-c.xml -quit -logFile C:\serenity_game\Artifacts\WM15WaterReview5\editor-shaderfix-c.log
```

Result: Unity exit 0; **4 total / 4 passed / 0 failed**; NUnit duration `481.8401853 s`. The cases cover the
canonical route and three previously reported world keys. No C# or shader compilation error was recorded.

Production rebuild command:

```text
Unity.exe -batchmode -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WM15WaterFinal/Serenity.exe -quit -logFile C:\serenity_game\Artifacts\WM15WaterReview5\build-shaderfix-c.log
```

Result: Unity exit 0; `Build Finished, Result: Success`;
`U00_BUILD_RESULT: Succeeded; errors=0; warnings=85`.

Actual Player evidence:

- `player-shaderfix-b/player.log` reached `WM15_BOOT population ready` and `WM15_BOOT gameplay ready` without
  the Procedural Water availability exception or the rock-spire approach exception.
- Its opt-in diagnostic then rejected the current multiple-fishing-landing-marker contract using an obsolete
  one-marker-per-zone assertion. The assertion was updated to require at least one marker for every hydrology
  lake; this does not alter normal gameplay initialization.
- The final repeated opt-in diagnostic launch from build `shaderfix-c` ended during the expensive strategic
  overview stage and emitted no new exception, but did not reach its pass marker. It is therefore not counted
  as a passed Player smoke. Runtime world-creation evidence comes from `player-shaderfix-b`, whose production
  initialization code is identical to the final build.
