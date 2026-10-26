# WM15 afternoon — commands and evidence

Unity 6000.6.4f1 executable: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`.
PowerShell launches use `Start-Process -WindowStyle Hidden -PassThru -Wait` (local licensing requires elevated tool execution, not a Unity version change).

## Asset preparation

`-batchmode -nographics -projectPath C:/serenity_game -executeMethod Game.Infrastructure.Editor.WM15AfternoonAuthoring.Generate -quit -logFile C:/serenity_game/Artifacts/WM15Afternoon/author-i.log`

Final preparation i: exit 0. Earlier attempts exposed missing diagnostic namespace/reference, Unity missing-component fake-null handling, and the hidden fish armature. The copied Blender exporter reveals the armature before exporting its original swim action. Portable Blender 4.5.3 came from download.blender.org and stays in ignored Artifacts, not a game dependency.

## Focused Editor checks

`-batchmode -nographics -projectPath C:/serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15AfternoonFeedbackTests;Game.Tests.Editor.WM15PropReplacementTests -testResults C:/serenity_game/Artifacts/WM15Afternoon/tests-a.xml -logFile C:/serenity_game/Artifacts/WM15Afternoon/tests-a.log`

Initial a: 10/13. Corrected hidden fish armature export; barracks acceptance now respects its 2.8 m depth limit; pier geometry is checked by raycasting actual board surfaces rather than the tallest decorative point.

`-batchmode -nographics -projectPath C:/serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15AfternoonFeedbackTests;Game.Tests.Editor.WM15PropReplacementTests.RodGripAndPierFootprintMatchTheirInteractionAdapters -testResults C:/serenity_game/Artifacts/WM15Afternoon/tests-b.xml -logFile C:/serenity_game/Artifacts/WM15Afternoon/tests-b.log`

b: 7/7. 13 distinct checks passed across the two runs. No full test suite or 100/300/500 performance gates.

## Build and Player

`-batchmode -nographics -projectPath C:/serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapAfternoonFix/Serenity.exe -quit -logFile C:/serenity_game/Artifacts/WM15Afternoon/build-c.log`

Build a succeeded, 0 errors / 81 warnings; Player a found an initialization-order null clock when creating fish. Fixed by keeping fish animation paused until Clock exists. Build b was stopped to wire the shoreline exception into the canonical placement service as well as the visual preview; c contains both corrections.

Player command (separate save directory):

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:/serenity_game/Artifacts/WM15Afternoon/player-b-saves'
Start-Process -FilePath 'C:/serenity_game/Builds/WorldMapAfternoonFix/Serenity.exe' -WindowStyle Hidden -Wait -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-wm15OctoberSmoke','C:/serenity_game/Artifacts/WM15Afternoon/player-b','-wm15Afternoon','-logFile','C:/serenity_game/Artifacts/WM15Afternoon/player-b.log')
```

Final results below supersede this intermediate candidate. WM15 is not marked DONE.

## Additional launch / boar regression

User Player.log: No dry WM09 approach for poi_abandoned_camp; later stale world-pile inventory in inspector. Added adaptive approach grid (8 → 4 → 2 m), safe stale-pile UI, actual skinned geometry alignment and centralized live-boar hoof grounding.

`-batchmode -nographics -projectPath C:/serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15AfternoonFeedbackTests.NewWorldPoiApproachesStayDryAcrossReportedWorldKeys;Game.Tests.Editor.WM15AfternoonFeedbackTests.AnimatedBoarHoovesDoNotUseItsOversizedSkinBoundsAsGround;Game.Tests.Editor.WM11WorldMapPoiExplorationTests.EveryPointHasDrySlopeBoundedApproachFromReachableWm09Route -testResults C:/serenity_game/Artifacts/WM15Afternoon/tests-c.xml -logFile C:/serenity_game/Artifacts/WM15Afternoon/tests-c.log`

5/5 passed; world keys 20261026, 1220639287, 1380062136. Total distinct focused checks: 18. User's exact failing key is not in the log.

Build c passed (0 errors / 81 warnings). Player b loaded successfully but the diagnostic tried MarkCompleted without delivering materials. Fixed diagnostic to transfer actual stock into the construction buffer first. Build d stopped when user reported additional launch/boar issues; build e includes those changes.


## Shore placement regression and final candidate

Build e: 0 errors / 82 warnings. Player c: 41/44 checks, no runtime exceptions. Pier feet, rod, fishing completion, deer bone animation, 13 boar hoof checks and campfire light passed. Garden order worked but planting exceeded the diagnostic wait at normal speed. Mountain landing remained impossible and one preview call reached 362.1 ms; this candidate was not accepted.

Cached immutable terrain/mask cell results separately from dynamic blockers, time-sliced cold footprint preparation and retained the pending shore candidate. A dry 8×6 m building needs a 2.4 m working-face reach at curved shores. Water, protected POIs and physical occupancy remain forbidden. Shore-only slope allowance is 32° and foundation delta remains 3 m.

`-batchmode -nographics -projectPath C:/serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15AfternoonFeedbackTests.MountainLakeHasAValidDryFishingCampFootprint -testResults C:/serenity_game/Artifacts/WM15Afternoon/tests-f.xml -logFile C:/serenity_game/Artifacts/WM15Afternoon/tests-f.log`

Tests d/e exposed the narrow waterfront reach. Tests f: 1/1 passed after correction; 19 distinct focused checks now passed. Garden diagnostic now uses the normal game fast-forward control while waiting for planting. Petal material draws after transparent water; added a close visual capture.

Final build command uses the same U00Build.WindowsDevelopment invocation above with `-logFile C:/serenity_game/Artifacts/WM15Afternoon/build-f.log`. Final Player uses player-d paths. Build f exited 0, 0 errors / 82 warnings, source fingerprint recorded.
Player d exited 0: 47/47 checks, errors.txt empty. Worst shore-preview call 43.0 ms. This bounded interaction
check is not a population performance gate. Captures inspected: planted garden, mountain fishing camp,
floating petals, campfire (earlier equivalent Player-c capture), pier feet/rod (earlier equivalent Player-c capture).
Boar capture is partly tree-occluded; hoof/terrain positions are asserted for 13 live views.
Unity shutdown additionally reports two Persistent allocations; not diagnosed in this repair batch.

Final exact Player invocation:
```powershell
$env:SERENITY_SAVE_DIRECTORY='C:/serenity_game/Artifacts/WM15Afternoon/player-d-saves'
$run = Start-Process -FilePath 'C:/serenity_game/Builds/WorldMapAfternoonFix/Serenity.exe' -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-wm15OctoberSmoke','C:/serenity_game/Artifacts/WM15Afternoon/player-d','-wm15Afternoon','-logFile','C:/serenity_game/Artifacts/WM15Afternoon/player-d.log') -WindowStyle Hidden -PassThru -Wait
$run.ExitCode
```

