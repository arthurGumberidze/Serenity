# P9.6 handoff — integrated visual acceptance

## Status

P9 and P9.6 are DONE on 2026-10-15. The ordinary Windows Player route passes both technical and subjective visual acceptance at exact 1280×720 and 1600×900. P8.6 remains PARTIAL, U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED; none of those qualification tracks was resumed.

## Closed gaps

- Modern UMA demo clothes were replaced by project-owned `F_StoneAgeHideTunic_Recipe` / `M_StoneAgeHideTunic_Recipe`, composed from the already admitted UMA 3.1f3 Human 3.0 chest slots, leather overlay, wrapped trousers and bare feet. The leather overlay is brown-tinted in the recipe.
- `Serenity_UMA_Human30.prefab` is a generated project-owned copy of the admitted UMA avatar with the unavailable sample `RandomCharacterWalker` removed. Vendor source is unchanged. Final Player logs contain no avatar missing-script or UMA fallback diagnostics.
- Detailed UMA presentation is bounded to eight Characters, matching the existing high-detail crowd budget. Remaining mass presentation keeps one centralized draw path but now uses a generated human-scale torso/head/limb/tunic LOD mesh and non-metallic hide material instead of a capsule.
- `GameplayUiController.RefreshAfterStateRebind` performs immediate and scheduled UI Toolkit repaint passes after Load. The previously reproduced 1600×900 text clipping is absent from the final after-Load images.
- The Player smoke verifies the actually active chest wardrobe recipe and missing-script-free UMA instances. `subjective_visual_acceptance` was set true only after inspecting both final evidence sets.

No external download/import was required after authorization: the solution uses the registered MIT `UMA-3.1F3` inventory plus Serenity-owned recipes, prefab and procedural LOD.

## Canonical boundaries preserved

`StableEntityId`, `AppearanceState`, Character/domain state, jobs, resources, buildings, `GameClock`, `WeatherService`, `GlobalMapService` and snapshot authority remain in their existing Domain/Simulation services. The wardrobe, clean prefab, LOD mesh and repaint scheduling are disposable Presentation. There is no per-mass-NPC `Update`, coroutine or GameObject expansion.

## Final verification

Unity: `6000.6.4f1`; Windows x64 Mono Development; D3D11 visible Player.

Combined affected EditMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-acceptance-editmode.log' -runTests -testPlatform EditMode -testCategory 'P96|P84|P93|P94|P95' -testResults 'C:\serenity_game\Logs\P96-acceptance-editmode.xml'
```

Result: 15 passed, 0 failed, 0 skipped.

Combined affected PlayMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-acceptance-playmode.log' -runTests -testPlatform PlayMode -testCategory 'P96|P84|P93|P94|P95' -testResults 'C:\serenity_game\Logs\P96-acceptance-playmode.xml'
```

Result: 12 passed, 0 failed, 0 skipped. The generic UMA generator Editor warning does not leave a missing component on a runtime scene object; the prior avatar-specific Player warning is absent.

Final build:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P96-done-build.log' -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P96/Serenity.exe
```

Result: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=44`.

Final visible Player commands used isolated save/settings directories and the following common arguments:

```text
Builds/P96/Serenity.exe -screen-fullscreen 0 -force-d3d11 -p96Smoke -p96SmokeSeed 96006 -p96SmokeWidth <width> -p96SmokeHeight <height> -p96SmokeOutput <output>
```

Evidence:

- `Logs/P96-done/1280x720`
- `Logs/P96-done/1600x900`

Both JSON reports record `passed=true`, `technical_passed=true`, `subjective_visual_acceptance=true`, `stone_age_wardrobe=true`, `clean_uma_prefab=true`, `character_asset_gap=false`, `aggregate_crowd_caveat=false`, the exact requested size, and all route/state flags true. Each directory contains seven non-empty PNGs: editor, RTS work, TPS settlement, global map, rain, winter-night and after Load. All fourteen final images were inspected. Both Player logs have zero avatar missing-script, UMA fallback, unhandled, null-reference or assertion matches.

## Next task

None is active. Do not resume P8.6/U29/U30/U31 or activate U32+ without an explicit user decision.
