# WM15 — October 25 gameplay feedback repair

Status: IN_PROGRESS. This checkpoint addresses the six supplied recordings and five screenshots. It also includes the preceding cooperative-loading repair, which was not yet committed. No full 100/300/500 performance suites are authorized or executed.

## Implemented changes

1. Retired the pictured ancient ruin/stone-circle prefab mappings while retaining their canonical POI/event identities. Disabled camera-facing road lines and solid ribbon renderers; roads blend the soil terrain layer with soft shoulders. Existing navigation geometry remains.
2. Water surface opacity/depth-color transitions no longer expose the coarse lake bed as large painted patches. Rounded lake polygons and resampled river/tributary curves are synchronized with the canonical coordinate index.
3. Wildlife prefab demonstration-scene offsets are removed and models are grounded before creating their selection collider. Both mouse buttons recognize wildlife and prioritize characters/animals over decorative tree proxies. The inspector refresh hash and responsive layout both include selected animal identity.
4. UMA's admitted eyelash overlay referenced a missing texture. Rebound it to the existing UMA_EYELASH_4 texture. A generated model is still required; no cartoon fallback is substituted for the ruler.
5. Context menus filter commands by target kind and building state. Fish offers fishing, prey offers bow hunting/skinning, operational buildings omit construction. Military members can explicitly hunt despite their work-group role. Direct hunting/fishing orders do not require an automatic-work camp; research requirements still apply and automatic work retains its camp requirement.
6. Authored dry terrain below the legacy global water level is buildable: water is determined by authored hydrology. Placement controller and building spawner share the runtime settlement grid origin. Slopes, protected POIs, occupancy, clearance and funding checks remain active.
7. Production jobs resolve scene work stances without changing persistent resource positions. Miners stop outside the rock; woodcutters approach the trunk. Hand/tool contact poses use the source surface. Tool meshes are copied into owned readable assets for head/handle orientation detection.
8. Barracks training has an arrival gate: the course clock waits while the resident walks to the entrance, the presentation hides indoors and restores after completion. Only graduates rebuild their appearance. Trained residents retain their imported Polytope face/rig with a matching cloth tunic and owned atlas instead of tinting the whole person.
9. Army diagnostics register player combat ownership and create physical Tier1 residents near the current player group, rather than aggregate residents at obsolete world coordinates. The population/UI refresh includes registry count.
10. Terrain's lit albedo gains regional snow, flowering-grove ground, marsh and meadow variation. Normal lighting, weather and night remain active. The terrain shader derives from the installed URP shader under the included Unity Companion License notice. See WM15_OCTOBER_ASSET_REVIEW.md for the requested asset comparison.
11. One wooden fishing pier per canonical waterbody is prepared during population loading. Fish work stances use its navigable deck. Persistent head/torso HP decreases only when water rises above the character's head; neck-depth remains safe. Fatal submersion invokes existing death/succession handling.

Loading: immutable hydrology segment lengths and bounding boxes are cached; pure height/paint arrays are
computed off the main thread one tile at a time. Unity object creation and navigation remain on the main
thread. A stage overlay and guarded initialization expose failure instead of leaving a silent frozen screen.

## Evidence and commands

Workspace: `C:\serenity_game`. Editor: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
All Unity launches use `Start-Process -WindowStyle Hidden -PassThru`, wait for the process exit and record its code. Elevated execution is needed for the installed licensing client. The initial sandbox attempt crashed while accessing licensing. A stale, exclusively-openable UnityLockfile was removed after confirming it was not held.

Asset repair:
```
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod WM15OctoberAssetRepair.Repair -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\asset-repair-d.log
```
Exit 0. Repairs the admitted eyelash overlay, creates readable owned tool meshes and resident training clothing. Earlier asset-repair-c only repaired the eyelash reference.

Focused tests:
```
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.WM15OctoberFeedbackTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests.CourseClockStartsOnlyAfterArrivalAndSurvivesSaveRestore" -testResults C:\serenity_game\Artifacts\WM15OctoberFeedback\focused-tests-b.xml -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\focused-tests-b.log
```
4/4 passed: fish/prey command matrix; neck/head submersion HP; rounded lake water versus dry start; arrival-gated training with save-state restore. The first run was 3/4: stale coordinate river index, corrected before the second run. These tests do not validate animation or visuals.

Build candidate:
```
-batchmode -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapOctoberFix/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\build-a.log
```
Build-a: Succeeded, 0 errors / 69 warnings, but REJECTED by Player-a. Placement used a stale origin;
responsive layout hid the animal inspector; runtime terrain material lacked retained instancing variants.
The first screenshot capture also ran before the first rendered menu frame and logged a ReadPixels error.
These failures are not dismissed as successful compilation.

Asset repair f used the same asset-repair command with `asset-repair-f.log`, exit 0. It additionally copied
the actual Polytope clothing texture/mesh and authored an instanced Resources/World/RegionalTerrain material.
Repair e failed on an unavailable ScreenCapture module in the diagnostic harness; reverted that API.
Focused regression c used the same test command with `focused-tests-c.xml` / `.log`, adding
`Game.Tests.Editor.WM03WorldMapHydrologyTests.EveryLakeAndCourseIsCarvedBelowItsDeterministicSurface` and
`Game.Tests.Editor.WM03WorldMapHydrologyTests.AuthoredFishingLandingsRemainDryAboveTheirOwnLakeSurface`.
Result: 6/6 Passed, exit 0. No performance tests.

Build-b and build-c used the same build command with `build-b.log` and `build-c.log` respectively.
Both exited 0, Succeeded, 0 errors / 69 warnings. Build-c includes graduate-only appearance refresh and the
extended Player route. Player-c arguments:
```
-screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:\serenity_game\Artifacts\WM15OctoberFeedback\player-c -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\player-c.log
```
Player-c editor screenshot visibly contains the UMA ruler. The route stopped at a fixture error: MarkCompleted was called without delivery/installing material stages. Fixed the fixture to deliver the catalog costs before marking labor complete. This does not bypass production placement or material checks in normal gameplay.

Focused regression d added DirectHuntingAndFishingReserveSourcesWithoutAnAutomaticCamp to the preceding selection: 7/7 Passed, exit 0, focused-tests-d.xml/log. A subsequent isolated canopy picking regression used:
```
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15OctoberFeedbackTests.BothMouseButtonsPickAnimalThroughDecorativeCanopy -testResults C:\serenity_game\Artifacts\WM15OctoberFeedback\pointer-test.xml -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\pointer-test.log
```
Result: 1/1 Passed. Thus eight distinct focused regressions passed; no full suite or performance gates.

Build-d and build-e used the same build command with their respective log names: Succeeded, 0 errors / 69 warnings, exit 0. Player-e completed the functional route, exit 1: an actual RMB canopy interception and a nullable first-rank assertion failed. The latter was a fixture comparison bug (null previous rank); the graduate actually reappeared in new clothing. Both were corrected. Player-e passed physical bow impacts/skinning, fishing on the pier, wood/stone production, four physical army additions, dry-land building placement/material delivery, editor/world UMA and training travel/hiding/new clothing. No runtime errors. Evidence: player-e/checks.txt, errors.txt and screenshots. This candidate was not treated as final acceptance.

The opt-in `-wm15OctoberSmoke <output-directory>` route creates a disposable new game, checks editor/world ruler generation, mouse picking, menu contents, dry-land placement, physical army additions, barracks travel/hiding/completion and lake piers. It captures images and exits with a failed code on recorded failures. It never writes a save or performs population/performance workloads. Assets are not edited while building.

Original user log and extracted video contact sheets are retained in `Artifacts/WM15OctoberFeedback/`. Earlier boot repair build: `Artifacts/WM15BootRepair/build-b.log`, exit 0, 0 errors / 56 warnings, `Builds/WorldMapBootFix/Serenity.exe`. That build does not contain this October feedback batch.

## Remaining acceptance

Final build and functional route:
```
-batchmode -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapOctoberFix/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\build-f.log
```
Succeeded, exit 0, 0 errors / 69 warnings. Build GUID 13f961b016424e8d8072ebd5cf2901d5,
source fingerprint 7F95E70C73076636563E8F217943F4D4A8E21278EDCC8C9C9FB125EC76D882B6.
Provenance records pre-commit HEAD 028926b6415d167694fff99f481168cf189cde8e plus the complete dirty source
manifest; source files were unchanged during build. The delivery commit follows the validated build.
```
C:\serenity_game\Builds\WorldMapOctoberFix\Serenity.exe -screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:\serenity_game\Artifacts\WM15OctoberFeedback\player-f -logFile C:\serenity_game\Artifacts\WM15OctoberFeedback\player-f.log
```
Player-f exit 0, 38/38 checks PASS, errors.txt empty. It verifies editor/world UMA, both animal mouse
buttons and inspector/menu, dry-land barracks placement/material delivery, four physical army recruits,
training travel/hiding/advancement/new cloth, every lake pier, fish navigation/menu, actual wood/stone
production, damaging bow impacts followed by skinning, and fishing while physically standing on the pier.
This disposable fixture grants materials/research and completes already funded barracks labor; it is not
a normal-economy balance test. No saves are written. No population/performance workload is run.

Reviewed rendered captures: editor/world ruler visible; graduate tunic; rounded blue-green lake water;
white mountain ground and pink grove ground; worker outside rock and tool directed at the surface;
woodcutting beside trunk; actual fishing stance on wooden deck. Work captures use the normal gameplay
render (camera controller can restore its overview despite the diagnostic side-camera request).
This establishes the reproduced interactions, not exhaustive animation or all-seed visual acceptance.
Fine shoreline steps, loading duration (minutes, now with responsive stages) and art polish remain open.
White Rabbit admission, broader husbandry and full performance gates remain separate unresolved work
from prior checkpoints. Preserve inherited vendor/import changes and unrelated material edits; no paid
asset acquisition. Keep WM15 IN_PROGRESS pending user acceptance.
