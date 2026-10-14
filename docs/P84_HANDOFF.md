# P8.4 handoff — полноценный редактор внешности

## Result

P8.4 is DONE at its implementation and evidence boundary. The Russian New Game route exposes both admitted UMA 3.0 human profiles and every verified compatible control used by the implementation: three skin tones, four hair choices, male beard choices, five hair colours, five eye colours, three age treatments and nine 0–100% morph sliders for head, jaw, chin, nose, eyes, brows, lips, height and build. Randomize is deterministic; reset restores neutral 50% morphs. Preview rotation and zoom remain available.

The clothing control is deliberately disabled and labelled as one verified compatible set. The admitted package does not contain a verified modular Stone Age wardrobe, so P8.4 does not claim one. The current UMA recipes are replaceable visual content, not canonical state.

`AppearanceState` owns stable profile/part IDs and ordered 0–1000 integer morph values. Snapshot v19 appends eye colour, age and parameters; v1–v18 remain readable. v18 appearance records receive safe `eye_brown`, `age_adult` and empty-parameter defaults. Preview, confirmed New Game, Tier1 presentation, Tier rematerialization and Save/Load use the same state. UMA does not own `StableEntityId`, identity, family, dynasty, health or simulation data.

Detailed UMA generation is reserved for editor-confirmed/key-character states carrying the complete morph set. Ordinary generated NPCs keep compact canonical defaults and the bounded wrapper fallback; P8.4 adds no per-NPC `Update` loop. UMA generation failure leaves canonical state untouched and restores the compatible fallback. Generated UMA colliders are disabled and rigidbodies are non-colliding.

## Verification

- focused P8.4 EditMode: 4/4 passed;
- focused P8.4 headless PlayMode: 2/2 passed;
- final P8.4 D3D11 PlayMode after preview fixes: 2/2 passed;
- full EditMode excluding unavailable `PostgresIntegration`: 489/489 passed;
- full D3D11 PlayMode excluding the separately blocked U31 category: 128/128 passed;
- Windows x64 Mono Development build: `Build Finished, Result: Success`, `U00_BUILD_RESULT: Succeeded; errors=0; warnings=21`;
- visible 1280×720 D3D11 Player: PASS for editor controls, generated UMA preview, canonical world equality, Tier equality and Save/Load equality;
- final preview generation: one generation in 629 ms;
- `p84-editor.png` and `p84-world.png` under `Logs/P84-player-smoke-final3` were visually inspected. The final editor image shows the complete head-to-feet UMA Character; the earlier black and head-cropped captures are superseded.

PostgreSQL integration was not configured and was excluded rather than reported as passed. U31 was excluded from the broad PlayMode regression because it remains explicitly BLOCKED by U29/U30; P8.4 did not weaken or relabel that gate.

## Exact commands

The reusable verification entry point is:

```powershell
& .\Tools\Verify-P84.ps1 -ProjectPath 'C:\serenity_game' -PlayerSmokeOutput 'C:\serenity_game\Logs\P84-player-smoke-final3'
```

The successful component operations were equivalent to:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.P4AppearanceFoundation.Build -quit
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory P84 -testResults C:\serenity_game\Logs\P84-focused-editmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P84 -testResults C:\serenity_game\Logs\P84-focused-playmode.xml
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P84 -force-d3d11 -testResults C:\serenity_game\Logs\P84-final-gpu.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory !PostgresIntegration -testResults C:\serenity_game\Logs\P84-all-editmode.xml
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory !U31 -force-d3d11 -testResults C:\serenity_game\Logs\P84-all-playmode.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P84/Serenity.exe
Builds\P84\Serenity.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p84Smoke -p84SmokeSeed 84004 -p84SmokeOutput C:\serenity_game\Logs\P84-player-smoke-final3
```

The final successful build log is `Logs/P84-build-final-visible.log`. As on earlier P8 builds, the batch Editor remained alive after the success marker in an external post-build curl/save shutdown path; only the verified build PID was stopped. The Player assemblies had already been rebuilt and the subsequent visible smoke ran from that output.

## Known content boundary

The admitted UMA sample prefab logs its inherited missing-script warning and can warn that the eyelashes overlay has no usable atlas area. Body generation succeeds and the safe fallback remains available. These warnings are not hidden, but repairing vendor sample content or authoring a real Stone Age wardrobe is not fabricated inside P8.4.

## Scope boundary and next task

P8.4 does not restyle the complete game UI and does not perform release qualification. P8.5 is the only next unblocked task. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED; no U29 matrix, replacement eight-hour U30 run or U31 closure was started.
