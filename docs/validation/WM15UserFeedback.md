# WM15 living-building runtime feedback validation — 2026-10-29

## Scope

This pass closes the fifteen user-reported runtime issues covering housing UI, paused motion, military/civilian
work separation, construction and reachability, livestock presentation, campfire navigation, UI input ownership,
lake-edge rendering, hunter roles, shore placement and per-member work shifts. Startup optimization and the
parallel mountain/import work were intentionally left untouched.

## EditMode

Broad affected regression command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P83WorkGroupTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests;Game.Tests.Editor.P109ConstructionClearanceTests;Game.Tests.Editor.P104ResourcePresentationTests;Game.Tests.Editor.P105BuildingInteractionTests;Game.Tests.Editor.P108ShorePlacementTests' -testResults 'C:\serenity_game\Artifacts\WM15UserFeedback\editor-regression-4.xml' -quit -logFile 'C:\serenity_game\Artifacts\WM15UserFeedback\editor-regression-4.log'
```

Result: 59 total, 58 passed, 1 failed. The sole failure was a new P83 test whose two eligible workers occupied
the same point, so deterministic nearest-worker selection correctly chose the other worker. The fixture was
corrected by placing the night-shift worker at a distinct position; no production rule was weakened.

Final corrected P83 command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P83WorkGroupTests' -testResults 'C:\serenity_game\Artifacts\WM15UserFeedback\editor-p83-final.xml' -quit -logFile 'C:\serenity_game\Artifacts\WM15UserFeedback\editor-p83-final.log'
```

Result: 7/7 passed, process exit 0. This rerun includes the previously failed assertion, so all affected
EditMode assertions are green in their final source state.

## Full-world PlayMode

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.P103ContextInteractionPlayModeTests.EveryInteractiveWorldObjectHasStableHitProxyAndCollider;Game.Tests.PlayMode.P106DomesticPlayModeTests.Assigned_bed_drives_sleep_and_ui_exposes_player_facing_domestic_route' -testResults 'C:\serenity_game\Artifacts\WM15UserFeedback\playmode-ready-3.xml' -quit -logFile 'C:\serenity_game\Artifacts\WM15UserFeedback\playmode-ready-3.log'
```

Result: 2/2 passed, 0 failed, duration 524.7366479 s. Both scenarios reached `WM15_BOOT gameplay ready` before
asserting. The timeout is 480 s per test because this dirty asset tree currently needs roughly three minutes to
create each full world; the timeout does not change runtime behavior.

An earlier generic 23-test PlayMode run is not acceptance evidence: those older fixtures load `LocalGameplay`
and inspect it one frame later, before the current asynchronous world bootstrap can finish. It failed 23/23 on
missing root/UI/characters and did not identify production exceptions. The two current full-world scenarios above
wait for `root.IsInitialized` and are the relevant acceptance gate.

## Windows Development build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'C:\serenity_game\Builds\WM15UserFeedback\Serenity.exe' -quit -logFile 'C:\serenity_game\Artifacts\WM15UserFeedback\build.log'
```

Result: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=82`, followed by `Exiting batchmode successfully now!`.
Output: `C:\serenity_game\Builds\WM15UserFeedback\Serenity.exe` (667,136 bytes).
