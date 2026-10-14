# P8.2 handoff — новая игровая карта

## Result

P8.2 is DONE at its implementation and evidence boundary. `LocalGameplay` now uses a repeatable project-owned P8.2 authoring pass over the P5/P7 world: an irregular finite land mesh, shoreline ring and calm water mask; preserved settlement clearings, six local routes and rival territory; 40 tree, 22 rock and 96 grass projections; and 24 finite canonical resource sites.

The original sixteen berry/fibre/wood/stone `player_source` indices remain 0–15. Four iron and four gold sources extend the deterministic sequence at 16–23. `iron_ore`, `gold_ore`, `gather_iron` and `gather_gold` are data-driven catalog/recipe entries unlocked with Stone Tools. Catalog factories remain compatible with tests or tools that intentionally construct the earlier five-resource catalog.

Every local natural source has a collider, selection/highlight state, Russian inspector summary, economy action and direct `ProductionSite`/`StableEntityId` link. The separate global-map mode projects one selectable marker for each of the six resource families, with canonical site IDs, remaining total and a normal action back to the settlement. These are disposable Presentation views; production, map, construction, navigation and Save/Load authorities are unchanged.

## Map and presentation boundary

- Generator: `Assets/Game/Infrastructure/Editor/P82NaturalRegionBuilder.cs`.
- Generated project assets: `Assets/Game/Art/Meshes/P82` and `Assets/Game/Art/Materials/P82`.
- Accepted P8.1 PBR meadow/shore/water/bark materials are reused; iron/gold materials and land/shore/water/trunk/ore meshes are project-owned.
- `P5InhabitableRegionPresentation` stores a 16-point boundary, authored placement list and trunk renderer list, then rejects any configured placement or resource outside the land polygon.
- The buildable ground is no longer a scaled cube: it uses `P82_InhabitableLand` with `MeshCollider`. Water remains non-walkable. Upward triangle winding preserves one connected runtime NavMesh between the player and rival anchors.
- Existing P3 object names remain as compatibility handles, but their ground/shore/water meshes are P8.2 assets.
- No vendor source was changed and no new external package was imported. The authenticated Unity Terrain Sample remains deferred under D-116.

## Verification

Final results:

- P8.2 EditMode: 3/3 passed.
- P8.2 headless PlayMode: 3/3 passed.
- P8.2 D3D11 PlayMode: 3/3 passed.
- affected P3/P5/production EditMode: 20/20 passed.
- affected P3/P5/P7/production PlayMode: 11/11 passed.
- Windows x64 Mono Development build: succeeded, errors=0, warnings=16.
- visible 1280×720 D3D11 Player: passed; 24 local sites, 6 global resource markers, finite-boundary/NavMesh/stable-link/clickability flags all true.
- Three final PNGs were visually inspected: local overview, selected gold source with normal inspector/action, and global map with selected iron summary/action.

The complete scripted route is:

```powershell
& .\Tools\Verify-P82.ps1 -ProjectPath 'C:\serenity_game' -PlayerSmokeOutput 'C:\serenity_game\docs\validation\P82\player-smoke'
```

The verifier executed these Unity operations:

```text
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.P82NaturalRegionBuilder.Build
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory P82
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P82
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory P82 -force-d3d11
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P3ReferenceCampTests;Game.Tests.Editor.P5InhabitableRegionTests;Game.Tests.Editor.ProductionAndFarmingTests
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P3ReferenceCampPlayModeTests;Game.Tests.PlayMode.P5InhabitableRegionPlayModeTests;Game.Tests.PlayMode.P7StoneAgePlayModeTests;Game.Tests.PlayMode.ProductionPlayModeTests
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P82/Serenity.exe
Builds\P82\Serenity.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p82Smoke -p82SmokeOutput C:\serenity_game\docs\validation\P82\player-smoke
```

Unity's first scripted build completed successfully but then lingered on two external curl timeouts before the verifier could launch the Player. Only the stalled wrapper was interrupted after `U00_BUILD_RESULT: Succeeded`; the final direct rebuild completed normally with the same success result, and the visible smoke then passed. Full Unity logs remain in ignored `Logs/`; compact NUnit XML and Player evidence are retained in `docs/validation/P82`.

## Scope boundary and next task

P8.2 does not implement the P8.3 group-character rollout, P8.4 final building/effect art, P8.5 final UI acceptance or P8.6 qualification. It does not run the complete U29 matrix, a new eight-hour U30 soak or the U31 release gate. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. The next unblocked product task is P8.3.
