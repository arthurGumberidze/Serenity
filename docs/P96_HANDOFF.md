# P9.6 handoff — integrated route and visual acceptance

Status: **PARTIAL** on 2026-10-15. Technical integration passes; subjective visual acceptance does not.

## Outcome

The ordinary visible Player route is now repeatable from the production Main Menu: New Game opens the normal appearance editor, creates an edited UMA female ruler, enters local RTS and TPS, visits the additive global map with all six canonical locations, returns to local play, completes finite-resource gather/haul/build orders, previews rain/snow/seasons/day-night, saves, mutates state and loads the canonical state back. The result verifies stable Character, appearance, job, resource and global-map identity rather than relying on screenshot-only assertions.

P9.6 is not DONE. Both Player sizes record `technical_passed=true` and `subjective_visual_acceptance=false`. Visual inspection exposes Character art and UI defects that cannot be solved with the currently approved inventory without either a new asset decision or explicit acceptance of placeholders.

## Implementation

- `P96PlayerSmokeController` drives the complete normal-menu route and emits machine-readable evidence plus seven screenshots without disabling the production AI/tier presentation or hiding known crowd artifacts.
- `MainMenuController` recognizes `-p96Smoke` only as validation instrumentation and launches it after the normal P9.5 initialization boundary.
- `P96IntegrationAcceptancePlayModeTests` covers the combined Terrain/biome/settlement/resource/weather route and global-map/Save-Load identity round trip.
- `Verify-P96.ps1` runs focused/affected suites, builds Windows x64 Development and launches visible Player evidence at exact 1280×720 and 1600×900 with isolated settings/save directories.
- No snapshot format, stable ID, clock/weather, work/building, resource, global-map or tier authority changed. No external asset or package was imported.

## Technical acceptance

| Check | Result |
|---|---|
| Focused P9.6 PlayMode | 2 passed, 0 failed, 0 skipped |
| Affected P8.4/P9.3/P9.4/P9.5 EditMode | 12 passed, 0 failed, 0 skipped |
| Affected P8.4/P9.3/P9.4/P9.5 PlayMode | 10 passed, 0 failed, 0 skipped |
| Windows x64 Mono Development build | succeeded, errors=0, warnings=41 |
| Visible D3D11 Player 1280×720 | technical pass; actual JSON size 1280×720 |
| Visible D3D11 Player 1600×900 | technical pass; actual JSON size 1600×900 |

Both Player results pass Main Menu/New Game, generated UMA editor preview, local RTS/TPS, global map/all six locations/return, Terrain-biome-settlement, finite resources, gather/haul/build, weather/day-night, rain/snow/seasons and Save/Load stable IDs. Counts are 170 biome trees, 94 rocks, 930 ground-cover instances, 24 resource formations, six worksites and two detailed Character presentations. Hashes remain:

- global layout: `84BE8C806F7B2B61`
- biome layout: `D907AA41DB7EA053`
- settlement layout: `681E040B60C66ED2`

## Evidence and visual inspection

Root: `C:\serenity_game\Logs\P96-player-smoke-20261015-final`.

Each `1280x720` and `1600x900` directory contains:

- `p96-player-smoke.json`
- `p96-new-game-editor.png`
- `p96-rts-work.png`
- `p96-tps-settlement.png`
- `p96-global-map.png`
- `p96-rts-rain.png`
- `p96-rts-winter-night.png`
- `p96-rts-after-load.png`

All fourteen final PNGs were inspected at original detail. The Terrain is populated and spatially coherent; forest, stone, shore, settlement, worksites, water and atmosphere are readable. RTS work, rain and winter-night changes are visible. The global map fits the island and permanently labels all six locations with routes and river. Save/Load restores the expected local composition.

The following visual failures remain open and are deliberately present in the evidence:

1. The detailed UMA ruler wears modern demo clothing (bright tank top and modern trousers), not realistic Stone Age clothing.
2. Mass/fallback Hodaart Characters are visibly low-poly and do not match the realistic environment or key UMA Character.
3. The centralized aggregate crowd draw can appear as a bright diagnostic capsule in TPS. P9.6 does not suppress it for a cleaner screenshot.
4. At 1600×900, UI Toolkit labels can be transiently clipped/partially rendered after the TPS and Load transitions. Preventing redundant resolution resets did not remove the defect.

The winter-night screenshot uses a presentation preview while the HUD continues to show the canonical simulated weather line. That distinction is expected for this non-authoritative visual preview and is not reported as a simulation-state change.

## Exact commands and results

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-focused-playmode.log' -runTests -testPlatform PlayMode -testCategory P96 -testResults 'C:\serenity_game\Logs\P96-focused-playmode.xml'
# Passed 2/2.

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-affected-editmode.log' -runTests -testPlatform EditMode -testCategory 'P84|P93|P94|P95' -testResults 'C:\serenity_game\Logs\P96-affected-editmode.xml'
# Passed 12/12.

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-affected-playmode.log' -runTests -testPlatform PlayMode -testCategory 'P84|P93|P94|P95' -testResults 'C:\serenity_game\Logs\P96-affected-playmode.xml'
# Passed 10/10.

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-build.log' -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P96/Serenity.exe'
# U00_BUILD_RESULT: Succeeded; errors=0; warnings=41.

& 'C:\serenity_game\Builds\P96\Serenity.exe' -logFile 'C:\serenity_game\Logs\P96-player-smoke-20261015-final\1280x720\Player-final.log' -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p96Smoke -p96SmokeSeed 96006 -p96SmokeWidth 1280 -p96SmokeHeight 720 -p96SmokeOutput 'C:\serenity_game\Logs\P96-player-smoke-20261015-final\1280x720'
# technical_passed=true; subjective_visual_acceptance=false; actual 1280x720.

& 'C:\serenity_game\Builds\P96\Serenity.exe' -logFile 'C:\serenity_game\Logs\P96-player-smoke-20261015-final\1600x900\Player-final.log' -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -force-d3d11 -p96Smoke -p96SmokeSeed 96006 -p96SmokeWidth 1600 -p96SmokeHeight 900 -p96SmokeOutput 'C:\serenity_game\Logs\P96-player-smoke-20261015-final\1600x900'
# technical_passed=true; subjective_visual_acceptance=false; actual 1600x900.
```

`Tools/Verify-P96.ps1` packages the same sequence and validates results/screenshots. Unity occasionally retained a batch process after emitting a conclusive test/build marker because of an editor network timeout; only that exact Unity PID was stopped after the result was present. This does not change the recorded NUnit/build outcome.

## Required decision / remaining work

To resume P9.6, the user must approve/provide a skeleton-compatible realistic Stone Age UMA wardrobe and coherent mass-NPC/LOD Character pack, or explicitly accept the current Character placeholders. The aggregate crowd visualization must then be replaced or explicitly accepted, and the 1600×900 transition clipping must be corrected or explicitly accepted. Re-run proportional tests/build and both Player sizes only after that material change.

P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. None was run or reinterpreted. The pre-existing `pre-P9.2` stash was not applied, changed or dropped.
