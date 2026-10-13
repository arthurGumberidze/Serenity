# P5 handoff — inhabitable Stone Age region

## Outcome

P5 is DONE at its implementation/evidence boundary. `LocalGameplay` is now a coherent traversable authored region: the existing shore camp sits beside a water boundary and connects through forest, stone outcrops and open construction ground to a distinct rival territory. The result remains the reversible P3 stylized baseline; final visual acceptance is not claimed.

The authored population is 40 approved tree wrappers, 22 approved rock wrappers, 96 project-owned grass clusters, six readable route segments, three buildable-area anchors, four authored supply views and 16 finite visible resource nodes. The resource set includes four berry thickets, four fibre stands, four trees and four stone nodes. Resource and supply views do not require Unity cube/sphere primitives.

## Canonical integration

- `P5InhabitableRegionPresentation` owns scene anchors, renderer groups and validation only.
- Every visible harvest node resolves to one `ProductionService` natural source using deterministic `player_source` identity. Legacy indices 0–3 remain berry/fibre/wood/stone; P5 extends through index 15.
- Save/Load restores canonical production/building/Character/appearance data and rebinds disposable resource presenters to the restored sites.
- Construction still crosses research, funding, occupancy and `BuildingPlacementController`; the Player proof researches and places a real shelter in the clearing.
- Water is non-walkable, shore/resource/obstacle geometry is collidable, and a complete baked NavMesh path connects the player and rival territory anchors after art fill.
- Weather remains canonical. Presentation projects seasonal ground/foliage/water colours and snow coverage without changing `WeatherService`.
- Local territory anchors bind to the stable six-location `GlobalMapService`; the global view remains its existing schematic projection.
- No package was purchased or imported. P5 uses approved P3 Serenity wrappers and project-owned generated meshes/materials recorded in `ASSET_REGISTRY.md`.

## Verification evidence

Executed with Unity `6000.6.4f1`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\serenity_game\Tools\Verify-P5.ps1 -ProjectPath C:\serenity_game -StepTimeoutSeconds 1200 -PlayerSmokeTimeoutSeconds 180 -PlayerSmokeOutput C:\serenity_game\Logs\P5-player-smoke-final
```

- Scene generator: `Game.Infrastructure.Editor.P5RegionSceneBuilder.Build`, clean exit.
- P5 EditMode: 4 passed, 0 failed/skipped (`Logs/P5-focused-editmode.xml`).
- P5 headless PlayMode: 3 passed, 0 failed/skipped (`Logs/P5-focused-playmode.xml`).
- P5 D3D11 PlayMode: 3 passed, 0 failed/skipped (`Logs/P5-gpu-playmode.xml`).
- Affected EditMode: 77 passed, 0 failed/skipped (`Logs/P5-affected-editmode.xml`).
- Affected PlayMode: 37 passed, 0 failed/skipped (`Logs/P5-affected-playmode.xml`).
- Windows x64 Mono Development: `Builds/P5/Serenity.exe`; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=9` (`Logs/P5-build.log`). Warnings are existing obsolete instrumentation/diagnostic API warnings.
- Visible Player: `passed=true` in `Logs/P5-player-smoke-final/p5-player-smoke.json`. It gathered from a finite canonical source, unlocked and built a shelter, validated a complete NavMesh route, completed the normal RTS manual move to 6.99 m from the rival anchor, rendered TPS and the global map, then preserved ruler/building/resource identity through Save/Load and projected winter.
- Visually inspected: `p5-rts-region.png`, `p5-tps-rival-territory.png`, `p5-global-region.png`, and `p5-after-load-winter.png` in that evidence directory.
- PowerShell parse and `git diff --check` are part of the final repository check recorded at commit time.

The restricted-sandbox Unity editor can crash while starting its build-report loopback listener; required Unity commands were run outside that restriction. One early smoke accelerated game time 10× and consumed the movement action's simulation timeout before NavMesh presentation completed. The final proof uses normal gameplay speed and genuine arrival; no teleport or relaxed path assertion is used.

## Open limitations and next task

- The global map is still the established schematic presentation rather than an authored terrain view. Its stable local/global location and route correspondence is verified; richer map art belongs to later product polish.
- Hodaart people and primitive shelter/building content remain documented grade-C placeholders. P5 does not overrule the open P3/P5 visual-acceptance boundary.
- P5 adds no new gather/build/combat animations or production sound library beyond the existing presentation foundation; deeper first-era content/polish remains P6/P7/P8 work.
- U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. No qualifying performance matrix or eight-hour run was started.

P6 is active next: make the first-hour loop understandable on this region without introducing tutorial-only canonical state.
