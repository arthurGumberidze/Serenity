# P10.9 Stage 1 handoff — tiled-world geometry and runtime contracts

Date: 2026-10-19

Unity: 6000.6.4f1

Status: **Stage 1 complete; P10.9 remains PARTIAL**

## Delivered

- Normal new games now use generation `p109-tiled-world-v2`: 12 × 12 separate Unity Terrains, each 1000 × 1000 m, for 144 km² total.
- `TiledWorldHeightSampler` is the deterministic, engine-free authority for heights, river/lake depressions, water and buildability. Adjacent Terrain heightmaps use exactly the same world-coordinate border samples.
- `TiledWorldStreamingPlanner` separates the bounded camera visual ring from collision/navigation retention. Physical actors retain their tiles, and a manual move prepares every tile in its cross-map corridor before creating the WorkOrder.
- The legacy 512 m Terrain/collider and old water renderer are disabled at runtime. Camera bounds/speed, building bounds, ground projection, resource anchors and dynamic Terrain queries use the tiled world.
- The strategic view frames all 12 km, renders low-detail land plus river/lakes from the same sampler, and applies persisted 48 × 48 world-space fog. The global graph has three settlements and connected world-scale routes.
- The player starts in the selected lower near-central settlement. Stage 1 defines settlement/river/lake geometry and contracts; Stage 2 owns final population and visual cleanup.
- New saves use `Serenity/P109/TiledV2/Saves`; preserved old saves remain in `Serenity/P10/Saves`. Generation mismatch is rejected before rebind and cannot be overwritten by the new gameplay facade.

No Asset Store dependency or Unity/package migration was added. No performance benchmark was run.

## Automated evidence

All commands used Unity 6000.6.4f1 outside the sandbox so licensing and local editor services could run.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109TiledWorldDefinitionTests;Game.Tests.Editor.P109TiledWorldRuntimeTests' -testResults 'C:\serenity_game\Artifacts\P109\stage1-editmode-final.xml' -logFile 'C:\serenity_game\Artifacts\P109\stage1-editmode-final.log'
```

Result: **13 passed / 0 failed**.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testCategory P109 -testResults 'C:\serenity_game\Artifacts\P109\stage1-affected-editmode.xml' -logFile 'C:\serenity_game\Artifacts\P109\stage1-affected-editmode.log'
```

Result: **16 passed / 0 failed**.

An adjacent EditMode regression run covering `P102ConnectedWorldTests`, `P103ContextCommandTests`, `WorkGroupAndJobTests`, `P2StartupTests` and `GameplayUiTests` is stored as `stage1-adjacent-editmode.xml`: **11 passed / 0 failed**.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.P109VideoRegressionPlayModeTests;Game.Tests.PlayMode.P109FunctionalIntegrationPlayModeTests;Game.Tests.PlayMode.P109TiledWorldStage1PlayModeTests' -testResults 'C:\serenity_game\Artifacts\P109\stage1-affected-playmode.xml' -logFile 'C:\serenity_game\Artifacts\P109\stage1-affected-playmode.log'
```

Result: **6 passed / 0 failed**. The final far-clip ownership regression rerun at `stage1-playmode-clip-fix.xml` passed **2/2**.

## Windows build and normal Player

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Artifacts\P109\stage1-build-accepted.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P109Stage1/Serenity.exe'
```

Result: **Succeeded; errors=0; warnings=44**, clean batchmode exit. Build GUID `64a799409aa34fef83f459aaf8282c18`; source commit `4a6df4188254561dddf28dc9b02aee2cc100e68b`; source fingerprint `FF2CCD2E4805709AA5C7ED8310A18CE1957B357FA16E5E98A3772788C48B3AA9`. Provenance is stored beside the executable.

```powershell
Start-Process 'C:\serenity_game\Builds\P109Stage1\Serenity.exe' -ArgumentList @(
  '-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-force-d3d11',
  '-logFile','C:\serenity_game\Artifacts\P109\stage1-player-accepted.log',
  '-p109Stage1Smoke','-p109Stage1SmokeOutput','C:\serenity_game\Artifacts\P109\stage1-player-accepted')
```

The normal Player route passed every flag and exited cleanly. It is recorded in `stage1-player-accepted/p109-stage1-player-smoke.json`; its local and strategic PNGs were visually inspected. The smoke checks normal menu/new-game launch, tile count/size, exact seam equality, bounded visuals, far-route retention, strategic framing including a world-reaching far clip plane, v2 save generation, exact 1920 × 1080 and D3D11.

## Stage 2 boundary

Stage 2 populates and visually distinguishes the approved agricultural, hunting, berry, settlement, river/lake, forest and ore zones; relocates or replaces legacy 512 m authored projections; and performs representative local/far/strategic visual acceptance. It must retain Stage 1's deterministic geometry, bounded visual set, physical-route retention and save isolation. Performance profiling remains deferred, so neither P10.9 nor P10.10 is DONE.
