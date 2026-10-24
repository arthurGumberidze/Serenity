# WM15 feedback repair — 2026-10-24

The user's screen recording and four screenshots reject the earlier activation smoke as visual acceptance.
This repair covers camera stalls, wildlife selection/separation, missing POI localization, invalid PhysX meshes,
unbound decorative building presenters, water, material loss, mountain extraction and the enemy starter camp.

## Root causes and changes

- V2 now prepares and retains all 16 terrain/navigation tiles during loading. Camera movement does not create
  terrain or population tiles. The complete 39,000-instance vegetation plan uses immutable spatial mesh batches,
  LOD and frustum culling. The previous 192-object vegetation stream is removed.
- Local water grid spacing used integer `250 / 256`, producing zero-area meshes. Floating-point spacing restores
  the river, streams and four lakes and removes the corresponding PhysX cooking errors. A regression checks
  every triangle in an actual local lake mesh at the Player's heightmap resolution.
- Scenery instantiation removes vendor mesh colliders and unbound BuildingPresenter components before activation.
  POI elements use individually sampled terrain heights. Bridge fitting uses local bounds and its two bank endpoints.
- POI event title/description/acknowledge keys are translated. Wildlife has pointer hit volumes, selection feedback
  and an inspector. Canonical spawn spacing and movement reject overlapping animal positions; a single central
  presentation driver checks solid obstacles. Camera-derived wildlife focus is clamped to the map bounds.
- Legacy shader texture fields are migrated into URP BaseMap for the affected scenery materials. Stone uses a
  separate textured V2 prefab/material, preserving the user's dirty original material. Quarry uses a local model.
- The mountain stone resource region includes a canonical `stone_quarry` industrial zone and output inventory.
  It uses existing extraction technology/cost/storage/worker limits. This is the existing shift-command mechanic;
  it is not a new fully automated industrial worker/logistics system.
- Enemy settlement occupies the authored southern settlement region, with six independently positioned residents,
  two shelters, a basket and a campfire. It retains separate inventories, research and production. Construction uses
  its own territory and terrain validation; new canonical enemy buildings receive world views.

## Validation record

All commands use Unity `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`, project
`C:\serenity_game`, and the owned-process timeout wrapper in `Tools/Validation/WM15.Runner.psm1`.
Test/Player saves are isolated using `SERENITY_SAVE_DIRECTORY` under `Artifacts/WM15Feedback`.

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.WM15WorldMapActivationTests;Game.Tests.Editor.WM06WorldMapResourcesTests;Game.Tests.Editor.WM09WorldMapRoadNavigationTests' -testResults C:\serenity_game\Artifacts\WM15Feedback\edit-c.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\edit-c.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.WM03WorldMapHydrologyTests;Game.Tests.Editor.WM15WorldMapActivationTests;Game.Tests.Editor.StrategicAiTests' -testResults C:\serenity_game\Artifacts\WM15Feedback\edit-d.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\edit-d.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod WM15FeedbackAuthoring.BuildPlayer -serenityBuildPath Builds/WorldMapV2Feedback/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Feedback\build-d.log
```

- edit-c: 16/16 passed after repairing the dry-bank/expansion corridor height ordering.
- edit-d: 29/29 passed, including the local-water regression and strategic research/construction progression.
- Earlier native route-a deliberately remains failed evidence: 132 errors, including collapsed water and an
  out-of-bounds wildlife focus. It exposed bugs that startup screenshots and overview-water tests had missed.
- Build-c stopped on the material audit because a vendor used `_Main_Texture`; the explicit migration now includes it.
- build-d and build-e: Windows Development builds succeeded, 0 errors / 54 warnings. Build-e additionally makes
  the diagnostic Player run in the background. route-b was stopped because the hidden window paused the test;
  it is not a completed route or acceptance result.
- route-c: all 35 points retained their original terrain objects; water is visibly present, the scenery and instanced
  flora material audit is empty, wildlife picking and POI checks passed. The route intentionally failed its enemy
  growth assertion (4 buildings remained 4). This exposed research starvation after the new starter camp allowed
  immediate military travel. An imported mirrored bear root also produced collider warnings.
- The enemy economy now starts research independently of military decisions; civilian construction does not cancel
  or wait for a remote expedition. edit-e and edit-f: 17/17 strategic tests passed, including established-camp research
  and crop construction during travel. Wildlife hit geometry now uses a positive-scale root around imported models.
- play-a: 4/4 passed (WM03 water, WM05 vegetation/catalog and WM08 canonical Tier1/2/3 wildlife transitions).

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.StrategicAiTests -testResults C:\serenity_game\Artifacts\WM15Feedback\edit-f.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\edit-f.log
Unity.exe -batchmode -force-d3d11 -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WM03WorldMapHydrologyPlayModeTests;Game.Tests.PlayMode.WM05WorldMapVegetationPlayModeTests;Game.Tests.PlayMode.WM08WorldMapWildlifePlayModeTests' -testResults C:\serenity_game\Artifacts\WM15Feedback\play-a.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\play-a.log
```

## Remaining acceptance boundaries

### Settlement-first strategy requested during this repair

The enemy no longer selects the nearest foreign settlement automatically. Default policy requires seven days
of development, shelter/storage/agriculture, actual barracks training, three retained civilians, sufficient
home/expedition food, hostile relations and a 1.25 estimated strength advantage. Neutral neighbours develop and
train without attacking. A persisted PrepareAttack order gives at least one game day of warning before departure.
Improved relations or lost strength cancel preparation; reload preserves the original countdown. The map shows
relations and preparation, and offers gifts (10 actual berries transferred, +20 relations) or declared hostility.
AI training saves reuse the existing rank/course model. This is not a complete treaties/coalitions implementation.

- edit-i: 19/22 passed; three warning tests had an insufficient-strength fixture. Corrected the fixture to give
  the explicitly strong attacking army superior combat skill, keeping the 1.25 threshold unchanged.
- edit-j: 29/29 passed, including 30-day autonomous barracks training and canonical training save round-trip.
- edit-k: 30/30 passed, additionally checking conservation of food in diplomatic gifts.
- edit-l: 30/30 passed after adding a real trained-unit departure check and home/expedition food reserves.
  Enemy graduates now use aggregate representation at their own home; player graduate defaults are unchanged.
- build-i: succeeded, 0 errors / 54 warnings, 103.08 seconds for the owned Unity process.
- play-b: both WM05 vegetation checks passed, including shader support. The P105 locomotion case hit its
  existing 180-second per-test limit while loading the full map; it is NOT a passing animation test. Equivalent
  walk/run speed and combat-intent assertions are now included in the native route after returning to the camp.
- build-j: succeeded (0 errors / 54 warnings), but inspection showed the new shader's instancing variants had
  been stripped. It was not used as acceptance evidence. A Resources material with instancing enabled now
  retains those variants. build-k succeeded (0 errors / 54 warnings, 103.10 seconds); its log retains both
  ShadowCaster instancing variants and eight ForwardLit variants on D3D11/D3D12.
- route-f: enemy verification passed in the real Player: 4 -> 6 buildings, 9 completed training courses by
  day 30, six residents, relation 0, no unprovoked expedition. The overall route failed two white generated
  UMA materials, not scenery/water/POI or enemy development. This remains failed evidence until the revised
  character atlas projection and visible-material audit pass a fresh route.
- The corrected route-f isolation measured 96.92 ms median with vegetation, 13.90 ms without vegetation,
  9.96 ms without vegetation/terrain and 4.91 ms without the camera. Disabling light probes alone did not help.
  The per-marker recorder used a saturated one-sample buffer; those repeated marker values are invalid and
  must not be used for attribution. Wall-frame medians and the visual ablation are the useful evidence.
  The vendor vegetation shader performs extensive fragment noise/wind. A dedicated instanced shader retains
  leaf/bark textures, lighting, shadows, fog and vertex wind with cheaper distant LOD/shadow choices.
- route-g: water/scenery, visible-material audit, native animator assertions and autonomous enemy training passed.
  Continuous pan: median 33.596 ms, P95 38.5395 ms, max 55.2263 ms. Corrected comparison recorder shows vegetation
  mode median 32.2965 ms, GPU median 31.2044 ms; the scene is still GPU limited and is not a 60-FPS/gate pass.
  Overall route remained failed because the wildlife probe aimed 0.4m above every species instead of at its
  actual presentation bounds. The revised probe uses the bounds centre. Small animals now have a minimum
  0.6m picking volume; animals and residents share hit priority, ordered by distance, so a distant resident cannot
  steal an animal click. A focused regression exercises pointer selection for all seven species.
- play-c did not run: a missing Campaign namespace import in the new regression prevented compilation. Fixed
  before play-d; do not treat play-c as test evidence.
- play-d: passed (one test exercising actual pointer selection of all seven species).
- build-l: succeeded, 0 errors / 54 warnings, 92.56 seconds. This is the current candidate.
- route-h: **PASSED**, exit 0, 35 points, 0 recorded errors, 213.14 seconds including initial map loading.
  Both errors.txt and white-material-audit.txt are empty. Native animator speed/action assertions passed.
  Contents: four lakes, twelve water meshes, 39,000 vegetation instances, 119 animals across seven species;
  six enemy residents, four -> six buildings, nine completed training courses by day 30, relation 0 and no raid.
  Pan median 32.271 ms, P95 37.861 ms, max 54.4596 ms. This is functional acceptance evidence and a substantial
  rendering improvement, not a 60-FPS pass or a replacement for the rendered 100/300/500 gates.
  Existing imported-asset/UMA warnings and two persistent-allocation warnings at Player shutdown remain in the
  raw logs; zero recorded errors does not mean a warning-free engine shutdown. Some UMA looks use the valid
  fallback while an admitted generated atlas is unavailable. No white construction buffers are made visible.
- route-d and route-e: 35-point functional routes passed with no recorded errors; 4 lakes, 12 water meshes,
  39,000 vegetation instances, 119 wildlife across seven species, 6 enemy residents, 4 -> 5 enemy buildings.
  These preceding builds still used the earlier aggressive policy. Measured pan median was about 94–96 ms;
  this is not a smooth-performance pass. The initial disabled-vegetation comparison was invalid because the
  periodic terrain refresh re-enabled the renderer. The corrected diagnostic isolates that refresh explicitly.

Exact strategic regression command (Unity 6000.6.4f1):

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.StrategicAiTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests' -testResults C:\serenity_game\Artifacts\WM15Feedback\edit-k.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\edit-k.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.StrategicAiTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests' -testResults C:\serenity_game\Artifacts\WM15Feedback\edit-l.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\edit-l.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod WM15FeedbackAuthoring.BuildPlayer -serenityBuildPath Builds/WorldMapV2Feedback/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Feedback\build-i.log
Builds\WorldMapV2Feedback\Serenity.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -wm15FeedbackRoute C:\serenity_game\Artifacts\WM15Feedback\route-f -logFile C:\serenity_game\Artifacts\WM15Feedback\player-f.log
Unity.exe -batchmode -force-d3d11 -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WM05WorldMapVegetationPlayModeTests;Game.Tests.PlayMode.P105CharacterProductionPlayModeTests.RtsAndTpsLocomotionPlaybackTracksTranslationSpeedAndActionPoseChanges' -testResults C:\serenity_game\Artifacts\WM15Feedback\play-b.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\play-b.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod WM15FeedbackAuthoring.BuildPlayer -serenityBuildPath Builds/WorldMapV2Feedback/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Feedback\build-k.log
Builds\WorldMapV2Feedback\Serenity.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -wm15FeedbackRoute C:\serenity_game\Artifacts\WM15Feedback\route-g -logFile C:\serenity_game\Artifacts\WM15Feedback\player-g.log
Unity.exe -batchmode -force-d3d11 -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.WM08WorldMapWildlifePlayModeTests.EverySpeciesCanBePickedThroughItsActualPresentationBounds -testResults C:\serenity_game\Artifacts\WM15Feedback\play-d.xml -logFile C:\serenity_game\Artifacts\WM15Feedback\play-d.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod WM15FeedbackAuthoring.BuildPlayer -serenityBuildPath Builds/WorldMapV2Feedback/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Feedback\build-l.log
Builds\WorldMapV2Feedback\Serenity.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -wm15FeedbackRoute C:\serenity_game\Artifacts\WM15Feedback\route-h -logFile C:\serenity_game\Artifacts\WM15Feedback\player-h.log
```

Full rendered 100/300/500 resident gates remain assigned to the user's offline performance runner. This route
does not replace them. Hare, fox and wolf still use temporary cat/dog family models; bear is now a dedicated model
from the already present local package. No new Asset Store dependency was downloaded. User visual acceptance
and the full performance gates are still required before WM15 can be marked DONE. PostgreSQL remains PARTIAL.
Seven inherited dirty materials and `New Unity Project/` are outside this repair.
