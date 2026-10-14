# P9.1 handoff — official realistic-world benchmark

## Outcome

P9.1 is DONE. Unity URP Terrain Sample 1.0.3 (Asset Store 213197, Unity Technologies) is selectively imported and used by a separate, idempotent benchmark in normal `LocalGameplay`. The old P3/P5/P8.2 map is intentionally preserved for P9.2.

The benchmark contains a 96×96 m Unity Terrain, four official PBR Terrain layers, 64 trees, 24 rock instances, 240 foliage instances and an official water presentation. It has physical Terrain/tree/rock colliders and no per-object update loop. `P91RealisticWorldBenchmark` is a read-only Presentation marker; no canonical gameplay/save authority moved.

## Import and compatibility

- Source package: user-downloaded `.unitypackage`, version 1.0.3, original editor Unity 6000.3.
- Project editor: Unity 6000.6.4f1.
- Selected source: `Assets/TerrainDemoScene_URP` runtime Terrain layers/textures, Prefabs/Details, Foliage, Rocks, Skybox, Trees, Water and required Shader Graph dependencies.
- Excluded after import: Animation, Notes/Readme, Scenes, Scripts, Settings, demo Terrain brushes/data/materials/palettes/detail maps, TextMeshPro/VFX demo content, plus Prefabs/Bookmarks, Lighting, Shots, Terrain, UI and Volumes.
- Dependency proposal was skipped to preserve Input System 1.20.0, URP 17.6.0, Test Framework 1.8.0 and Visual Scripting 1.9.11. Built-in `com.unity.modules.terrain` 1.0.0 was enabled because project asmdefs now reference `UnityEngine.TerrainModule`.
- Retained source size: 769 files / approximately 2308.9 MiB. Git LFS coverage was extended for the package's `.tif`, `.tiff`, `.hdr` and `.st` sources; all 62 retained files larger than 10 MiB are stored as LFS pointers in the index.

## Verification

Final complete verifier command:

```powershell
& 'C:\serenity_game\Tools\Verify-P91.ps1' -ProjectPath 'C:\serenity_game' -StepTimeoutSeconds 3600 -PlayerSmokeTimeoutSeconds 300
```

Results before the camera-only follow-up:

- P9.1 EditMode: 2/2 passed.
- P9.1 PlayMode: 1/1 passed.
- P8.2 preservation EditMode: 3/3 passed.
- Windows x64 Mono Development build: `Succeeded; errors=0; warnings=40`.
- Visible D3D11 Player smoke: passed, 4 layers / 64 trees / 24 rocks / 240 foliage, both PNGs present.

Final camera-only cached rebuild command:

```powershell
$p = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -ArgumentList @('-batchmode','-nographics','-projectPath','C:\serenity_game','-quit','-executeMethod','U00Build.WindowsDevelopment','-serenityBuildPath','Builds/P91/Serenity.exe','-logFile','C:\serenity_game\Logs\P91-camera-rebuild.log') -WindowStyle Hidden -PassThru
$p.WaitForExit()
```

Result: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=40`; all shader passes were local-cache hits.

Final visible Player evidence:

- Result: `Logs/P91-player-smoke-camera-final-20261014-165947/p91-player-smoke.json` (`passed=true`).
- Screenshots: `p91-rts.png`, `p91-tps.png`, both 1280×720 and visually inspected.
- The final TPS view clearly shows the meadow, undergrowth, forest frame, water and rock formation under a non-white sky.

One initial focused Editor run failed only because old NUnit did not support `Has.Count` on `IReadOnlyList`; the assertion was corrected to direct `.Count` and the full verifier then passed. One uncached build completed successfully but the 30-minute wrapper limit expired during shader compilation; the repeated 60-minute run recorded the successful build. A later `-noUpm` experiment was rejected because it intentionally omitted cached package assemblies; the normal UPM rebuild restored the package graph and passed. These diagnostics are not acceptance evidence.

U29, U30 and U31 were not run. P8.6/U29/U30/U31 statuses remain PARTIAL / PARTIAL / IN_PROGRESS / BLOCKED.

## P9.2 constraints

P9.2 must migrate existing canonical gameplay anchors and rebuild NavMesh/construction/weather/Save-Load proofs before deleting the legacy visual map. Do not edit vendor assets and do not make Terrain GameObjects canonical state.
