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
