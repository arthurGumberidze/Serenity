# WM15 morning feedback — commands and evidence

Working directory: `C:\serenity_game`. Unity: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
Commands below are PowerShell; execution used hidden `Start-Process -PassThru` and `WaitForExit()`.
Full performance gates (100/300/500 residents) were not run.

## Authoring and compilation

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -quit -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.P109BaseBuildingFoundation.AuthorMorningFeedback -logFile C:\serenity_game\Artifacts\WM15MorningFeedback\author-d.log
```

Exit 0. Only the fishing camp, palisade, gate and their presentation definitions were regenerated.
Owned URP materials and a clipped section of the imported Medieval Fortification wall were added.
`PreviewMorningFences` was also executed with the same arguments and its own log to inspect imported geometry.

## Targeted tests

Shared arguments: `-batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode`.
Each invocation additionally supplied `-testFilter FILTER -testResults PATH.xml -logFile PATH.log`.
Paths are under `C:\serenity_game\Artifacts\WM15MorningFeedback\`.

| Result file prefix | Filter | Result |
|---|---|---|
| focused-c | `Game.Tests.Editor.WM15MorningFeedbackTests;Game.Tests.Editor.StrategicAiTests` | 29/29 |
| construction-d | `Game.Tests.Editor.P109ConstructionStateTests` | 9/9 |
| construction-f | `Game.Tests.Editor.P109ConstructionStateTests.RetryDeliveryRequestsOnlyMaterialsMissingFromTheExistingBuffer` | 1/1 |
| focused-d | `Game.Tests.Editor.WM15MorningFeedbackTests;Game.Tests.Editor.StrategicAiTests.FactionWorkplaceOwnerAndQueueSurviveSaveRoundTrip;Game.Tests.Editor.StrategicAiTests.SaveV13RoundTripResumesWithoutRepeatingDecisionAndRejectsUnknownTarget` | 7/8; the new source-rebinding test allowed too little work time |
| source-e | `Game.Tests.Editor.WM15MorningFeedbackTests.RebindingSharedSourcesReplacesAutomaticSelectionCache` | 1/1 after correcting the test duration |

41 distinct test cases passed across this batch. Repeated cases are not counted twice. Earlier failed
compilation/fixture attempts remain in the artifact directory and are not acceptance evidence.

## Build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapMorningFix/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15MorningFeedback\build-e.log
```

Build-e follows build-d with the missing `fish_camp` Russian caption and an additional opt-in saved-game
verification branch. Production mechanics are unchanged between d and e. Both report 0 errors / 77 warnings.
The final source manifest and provenance are copied after Unity's post-build fingerprint check finishes.

## Player functional route

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Artifacts\WM15MorningFeedback\player-d-saves'
& 'C:\serenity_game\Builds\WorldMapMorningFix\Serenity.exe' -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15MorningSmoke C:\serenity_game\Artifacts\WM15MorningFeedback\player-d -logFile C:\serenity_game\Artifacts\WM15MorningFeedback\player-d.log
```

Player-d: exit 0, 31/31 checks, empty errors file. The fixture pays and completes the test player's
wall/camp construction to isolate gate/work/haul behavior. It does not grant technology/resources to the
neighbour or complete its construction. Actual arrows trigger fleeing; real production fills the camp;
automatic hauling transfers fish into a real warehouse. Neighbour production reaches 44 berries and
construction deliveries install all 8 logs and 3 fibre in a new shelter. Construction reaches its final
stage; completion is checked in the saved-game continuation below.

## Saved-game continuation

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Artifacts\WM15MorningFeedback\player-d-saves'
& 'C:\serenity_game\Builds\WorldMapMorningFix\Serenity.exe' -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15MorningSmoke C:\serenity_game\Artifacts\WM15MorningFeedback\player-e -wm15MorningResume 5e9f83b2a78f4ba3be1f6c12a0ba4174 -logFile C:\serenity_game\Artifacts\WM15MorningFeedback\player-e.log
```

This loads only the disposable test slot, moves the camera away and lets the normal neighbour scheduler
finish the already funded shelter. No player saves are read or overwritten. Player-e: exit 0, 6/6,
empty errors file. The saved shelter ID `b7370064b22aacf85e8a014c2129bed5` reached Completed with
30000000000/30000000000 construction work ticks. The actual finished model is in `neighbour-completed.png`.

## Limits

This is functional acceptance for the specified seed and targeted state transitions, not a long-term
campaign/performance certification. Every biome/shore orientation and old-save combination was not
exhaustively traversed. Initial full-map loading still takes minutes. AQUAS full URP integration,
professional replacements for listed temporary props, and wider art acceptance remain separate work.
