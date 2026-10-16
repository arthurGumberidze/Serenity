# P10.2 handoff — connected world and same-world strategic view

## Status

P10.2 is DONE on 2026-10-16. P10.3 is the only active successor. P8.6/U29/U30/U31 remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED.

## Delivered world contract

- `LocalGameplay` owns one 512×512 m physical Unity Terrain used by local RTS, TPS and strategic overview.
- The 2 m grid audit found 58,336 accessible dry samples, or 233,344 m², above the 163,840 m² minimum.
- All six established location IDs remain unchanged. Four append-only locations (`mirror_lake`, `west_marsh`, `iron_ridge`, `southern_coast`) extend the graph to ten connected districts and thirteen routes.
- The world composition includes sea boundaries, two inland lakes, a river corridor, mountain blockers, forests, settlement/district clearings, routes and deliberate empty travel distances.
- All 24 canonical resource roots keep their existing index/`StableEntityId` mapping and remain selectable/actionable through the existing production presenters.
- The existing runtime NavMesh spans the connected Terrain. The final affected navigation tests cover all ten locations.
- Construction remains available on ordinary accessible Terrain. `P102WorldPlacementSurface` adds explicit water/steep-surface rejection before the canonical research, funding, grid and occupancy checks.

## Authority and persistence

`ConnectedWorldService` is an engine-free Simulation service. It owns deterministic 16 m fog cells, initial reveal, physical-presence reveal and assigned-scout completion. It advances from the canonical `GameClock`; there is no per-cell, per-location or per-scout `Update`.

`GlobalMapRuntimeBridge` no longer opens the additive schematic map. It stores the local camera/action-map state and enables `P102StrategicWorldView`, an orthographic overlay over the same loaded Terrain and presenters. Returning to a location restores the local camera without changing spatial authority.

Snapshot v20 appends `worldGenerationId = p10-connected-world-v1`, fog cells and scouting assignments. The ordinary file facade defaults to `Serenity/P10/Saves`, leaving P9 files untouched. The XML codec retains explicit legacy v1–v19 reads for archive/diagnostic paths; P10 does not infer or overwrite legacy world state.

## Asset and dependency result

No external asset or package was added. The expanded Terrain reuses the already registered Unity URP Terrain Sample source; the inland-water/route materials, connected-world code and scene composition are Serenity-owned. Vendor source is unchanged. Registry entry: `SERENITY-P102-CONNECTED-WORLD`.

## Geometry and build audit

- Terrain footprint: 512×512 m.
- Accessibility sample: 2 m.
- Accessible dry land: 233,344 m².
- Locations: 10 (six preserved, four append-only).
- Routes: 13.
- Canonical resource anchors: 24.
- Strategic authority: same `LocalGameplay` scene/Terrain; no additive map scene.
- Unity: 6000.6.4f1.
- Target: Windows x64 Mono Development, D3D11 Player.
- Final build: `Succeeded; errors=0; warnings=26`.
- Build GUID: `bc8b6cf8f0284348bd5864b769e61d74`.
- Source fingerprint: `052C80E9A150198518D845E98482030C5E9CD800E1249335F16BCCD42D52C029`.

## Exact validation commands and results

Scene authoring/validation:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod Game.Infrastructure.Editor.P102ConnectedWorldBuilder.Build -quit -logFile 'C:\serenity_game\Artifacts\P102\builder.log'
```

Result: `P102_CONNECTED_WORLD_BUILT size=512x512; sample=2m; accessibleSamples=58336; accessibleAreaM2=233344; locations=10; resources=24; authority=LocalGameplayTerrain; strategicView=same-scene-camera`.

Focused EditMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'P102' -testResults 'C:\serenity_game\Artifacts\P102\final-editmode.xml' -logFile 'C:\serenity_game\Artifacts\P102\final-editmode.log'
```

Result: 4 passed, 0 failed, 0 skipped.

Focused PlayMode plus same-world map regression:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'P102;P95AtmosphereAndGlobalMapPlayModeTests' -testResults 'C:\serenity_game\Artifacts\P102\final-playmode.xml' -logFile 'C:\serenity_game\Artifacts\P102\final-playmode.log'
```

Result: 4 passed, 0 failed, 0 skipped.

Affected EditMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'P102;P92;P93;P94;P95;P96;GlobalMap;Save' -testResults 'C:\serenity_game\Artifacts\P102\affected-editmode.xml' -logFile 'C:\serenity_game\Artifacts\P102\affected-editmode.log'
```

Result: 63 passed, 0 failed, 6 intentionally ignored by their existing environment/category guards.

Final affected PlayMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'P102;P92;P93;P94;P95;P96;GlobalMap;Save' -testResults 'C:\serenity_game\Artifacts\P102\final-affected-playmode.xml' -logFile 'C:\serenity_game\Artifacts\P102\final-affected-playmode.log'
```

Result: 32 passed, 0 failed, 0 skipped. The earlier 31/32 diagnostic found the inherited P9.3 six-location assertion after the world had become ten-location; the test was corrected to verify all connected locations, its focused rerun passed 3/3, and this complete final sweep passed 32/32.

Final Windows Development build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P102/Serenity.exe' -quit -logFile 'C:\serenity_game\Artifacts\P102\build-provenance-final.log'
```

Result: process exit 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=26`; immutable pre/post source fingerprint passed and the fresh provenance file was written.

Final normal visible Player (isolated save/settings directories):

```powershell
$env:SERENITY_SAVE_DIRECTORY = 'C:\serenity_game\Artifacts\P102\player-final-evidence-1920x1080\Saves'
$env:SERENITY_SETTINGS_PATH = 'C:\serenity_game\Artifacts\P102\player-final-evidence-1920x1080\settings.json'
& 'C:\serenity_game\Builds\P102\Serenity.exe' -logFile 'C:\serenity_game\Artifacts\P102\player-final-evidence-1920x1080\Player.log' -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11 -p102Smoke -p102SmokeOutput 'C:\serenity_game\Artifacts\P102\player-final-evidence-1920x1080'
```

Result: process exit 0 and `passed=true`. Same-world strategic view, connected navigation, construction, initial fog, physical reveal, scouting reveal and v20 Save/Load all passed. Exact resolution is 1920×1080; accessible area is 233,344 m²; location count is 10; resource anchors are 24. The three final PNGs were visually inspected. The Player log has no exception, assertion, crash, shader error, missing-script or invalid screenshot-capture diagnostic.

## Honest performance observation

The Player recorded real unscaled frame samples rather than a synthetic estimate:

- p50: 35.664 ms;
- p95: 128.342 ms;
- p99: 313.966 ms;
- max: 784.861 ms.

P10.2 does not claim the future minimum-performance gate. P10.9 explicitly owns the feature-complete p95 ≤33.33 ms requirement and must profile/optimize this debt. The smoke also records `rain_snow_seasons=false` and `fps_displayed=false`, preserving the known P10.1 baseline gaps for P10.9 rather than concealing them.

## Versioned evidence

`docs/validation/P102` contains:

- focused and affected EditMode/PlayMode NUnit XML;
- final `build-provenance.json`;
- 1920×1080 Player telemetry JSON and complete `Player.log`;
- local connected-world, same-world strategic and after-Load construction PNGs.

Local intermediate logs remain under ignored `Artifacts/P102`; the clean final evidence is versioned under `docs/validation/P102`.

## Next task

P10.3 — selection, contextual actions and queues. Do not begin P10.4+ or resume the independent P8.6/U29/U30/U31 qualification tracks.
