# WM15 recovered-checkpoint audit — 2026-11-01

The local `6965286b` recovery checkpoint was audited against `main` instead of being merged wholesale. The
bulk winter/Terrain Sample presentation experiment, duplicate snow-track implementation, authoring scripts,
recovery scenes, vendor/material reserialization and unrelated UMA/editor settings were rejected. No new Asset
Store dependency or visual candidate was admitted.

The small retained closure is:

- persistent Unity runner requests now carry an explicit `EditMode`/`PlayMode` test mode;
- the P108 diagnostic army excludes `Generalissimo`, which remains the unique ruler rank;
- the P108 full-world fixture and Player smoke wait for `LocalSceneCompositionRoot.IsInitialized` and validate
  the active world save profile instead of the obsolete fixed v25 assumption;
- the fixture checks the approved rank skill values and treats a colonel-general army commander as a commander,
  not incorrectly as a squad member;
- dead AlpineRock global assignments were removed from `TiledTerrainWorld`; their unaccepted shader/catalog
  experiment was not imported;
- the missing `TerrainSample` folder meta and Unity-regenerated UMA asset index were retained because Unity
  otherwise rewrote source during every build and failed immutable provenance.

## Final evidence

```powershell
.\Tools\UnityRunner.ps1 compile -OutputPath Artifacts\WM15Audit\compile-save-profile
.\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames 'Game.Tests.Editor.P108DiagnosticsTests' -OutputPath Artifacts\WM15Audit\editmode
.\Tools\UnityRunner.ps1 test -TestMode PlayMode -TestNames 'Game.Tests.PlayMode.WM12WorldMapBuildabilityPlayModeTests' -OutputPath Artifacts\WM15Audit\runner-playmode
.\Tools\UnityRunner.ps1 test -TestMode PlayMode -TestNames 'Game.Tests.PlayMode.P108DiagnosticsPlayModeTests.PauseCodesRouteRunsAllCanonicalEffectsRepeatsAndPersistsIrreversibleFlag' -OutputPath Artifacts\WM15Audit\playmode-pausecodes-final2 -TimeoutSeconds 1500
.\Tools\UnityRunner.ps1 build -BuildPath Builds\WM15AuditFinal4\Serenity.exe -OutputPath Artifacts\WM15Audit\build-final4 -TimeoutSeconds 1200
Builds\WM15AuditFinal4\Serenity.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -force-d3d11 -p108Smoke -p108SmokeOutput C:\serenity_game\Artifacts\WM15Audit\p108-player-final2 -p108SmokeWidth 1920 -p108SmokeHeight 1080 -logFile C:\serenity_game\Artifacts\WM15Audit\p108-player-final2.log
```

Results: compile completed with 0 errors; P108 EditMode passed 10/10; the explicit runner PlayMode probe passed
1/1; the two non-route P108 PlayMode cases passed in the readiness run and the final targeted route passed 1/1
in 624.44 seconds. The final immutable-provenance Windows Development build succeeded with 0 errors and 52
warnings. Its exact Player exited 0 and wrote `passed=true`: all technologies/resources/family/UI/Save-Load
checks passed, the diagnostic catalog contained nine non-ruler ranks, and achievements remained irreversibly
disabled after load. The earlier fixture failures were successively classified as pre-readiness access, the
obsolete `>=20` private-skill assertion (approved value is 18), and the incorrect assumption that an army
commander must remain a squad member.
