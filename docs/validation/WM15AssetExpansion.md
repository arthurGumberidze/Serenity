# WM15 asset expansion — 2026-10-30

Scope and editable settings: `docs/world_map/WM15_ASSET_EXPANSION.md`; decision D-221.
Unity 6000.6.4f1, persistent primary Editor PID 26512. No batchmode test invocation was used.
All commands below ran in PowerShell from `C:\serenity_game`.

## Compilation and domain/content regression

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts/WM15AssetExpansion/compile-n -TimeoutSeconds 600
& .\Tools\UnityRunner.ps1 test -GroupNames '.*(WM15AssetExpansionTests|WorkGroupAndJobTests|BuildingFoundationTests|BuildingDestructionFireCaptureTests|P109ExtendedBuildingsTests|P109BuildingProductionTests|P107MilitaryTrainingCaptureTests|ProductionAndFarmingTests|WM15LivingBuildingsTests|StrategicAiTests).*' -OutputPath Artifacts/WM15AssetExpansion/regression-final -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -GroupNames '.*StrategicAiTests.*' -OutputPath Artifacts/WM15AssetExpansion/strategic-final -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -GroupNames '.*WM15AssetExpansionTests.*' -OutputPath Artifacts/WM15AssetExpansion/author-verified -TimeoutSeconds 900
```

`compile-n`: success, 0 errors / 39 warnings. `regression-final`: 130 cases, 127 passed;
the three failing strategic cases were corrected and the complete strategic group passed 26/26 in
`strategic-final`. Other 104 domain/content cases passed. After the final visual adjustments, content
authoring/catalog, uniform and water checks passed again, 4/4 in `author-verified`.

The strategic fixture had omitted modern military equipment resources, and the newly added checkpoint test
incorrectly constructed a second scheduler against an already bound map. The fixture now starts at the requested
calendar tick and the training scenario supplies actual equipment. The production fix starts the scheduler
at the same tick as factions, preserving immediate paused-world saves without an artificial AI update.

Earlier iterations under the same artifact root retain failure evidence. They caught missing preview geometry
for procedural zones, Unity fake-null destroyed components, obsolete production-fixture expectations,
an unavailable initial food-storage assumption and the real initial AI checkpoint defect. These were fixed;
no failing assertions were disabled. Runtime source files remain unchanged while Play Mode tests run.

## Rendered acceptance and Player

```powershell
& .\Tools\UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.WM15AssetExpansionRuntimeTests.RenderedUpgradeSaveUniformSnowAndLakeRoute' -OutputPath Artifacts/WM15AssetExpansion/runtime-test-d -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 build -BuildPath Builds/WM15AssetExpansion/Serenity.exe -OutputPath Artifacts/WM15AssetExpansion/build -TimeoutSeconds 3600
```

`runtime-test-d`: 1/1 passed, 399.249 s, all 20 route assertions passed and no unexpected Unity errors.
Visual review rejected these first screenshots: the snow clipped to white and Cinemachine overwrote the
requested test camera poses. Snow lighting is now bounded and the acceptance route disables the physical
camera's Cinemachine Brain before applying fixed poses. These presentation/capture corrections are validated
in the final built Player below, rather than treating the first screenshots as evidence for lake appearance.

Final compilation command:

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts/WM15AssetExpansion/compile-final -TimeoutSeconds 600
```

`compile-final`: 0 errors / 52 warnings. The first `build` produced a Player (Unity: 0 errors / 85 warnings)
but was rejected by immutable-source validation. No code changed during the immediate repeat:

```powershell
& .\Tools\UnityRunner.ps1 build -BuildPath Builds/WM15AssetExpansion/Serenity.exe -OutputPath Artifacts/WM15AssetExpansion/build-final -TimeoutSeconds 3600
& .\Artifacts\WM15AssetExpansion\Run-PlayerSmoke.ps1
```

`build-final`: succeeded, 34.369 s, 0 errors / 0 warnings. The Player and `build-provenance.json` are in
`Builds/WM15AssetExpansion`. The launcher sets `SERENITY_SAVE_DIRECTORY` to
`C:\serenity_game\Artifacts\WM15AssetExpansion\player-saves`, starts the executable hidden with
`-wm15AssetExpansion C:\serenity_game\Artifacts\WM15AssetExpansion\player -logFile C:\serenity_game\Artifacts\WM15AssetExpansion\player\player.log -screen-width 1280 -screen-height 720 -screen-fullscreen 0`,
waits for exit and writes `player/process-result.json`. It restores the parent environment afterward.

Final Player PID 7220 exited **0** at `2026-10-29T22:35:29.9249849Z`: **21/21 checks passed**;
`runtime-errors.txt` is empty. Reviewed `snow-before.png` / `snow-after.png`: the same fixed camera shows
a continuous depressed, shaded trail behind the moving resident without the previous white clipping.
Reviewed lake views at 20, 5 and 1 metres: shore and water remain visible, with no screen-covering dark-blue
plane. The deliberately unconstrained 60-metre camera also reveals a foreground terrain coverage boundary;
it is retained as evidence, not claimed as a clean general-terrain acceptance image.

After application shutdown Unity reported two outstanding Persistent native allocations. This diagnostic
occurs after the route has finished and is preserved in `player.log`; its allocation origin has not been
isolated in this task. No exception or error was recorded during the running scenario.

The default P109 save root contained no `Saves` directory when inspected, so no unrelated settings or
tile caches were deleted. The isolated test saves remain with the acceptance artifacts.

The route checks stable-identity model replacement, selected card continuity, storage capacity, unchanged
current HP, real save/load, rank uniforms, raised snow geometry, motion-driven depressed tracks and four
lake camera distances. Domain scheduling independently verifies builder arrival, sustained labour,
saved work progress, cancellation and exact material consumption. Rendered images are in
`Artifacts/WM15AssetExpansion/runtime` and the built Player equivalent is in `player`.

## Visual review and limits

Every authored building and all sixteen male/female uniform prefabs were rendered for review under
`Artifacts/WM15AssetExpansion/previews`. The final housing progression is a small timber-frame house for
four residents to the existing stone house for six. The communal house remains independently buildable.
Chief/hunting/butcher upgrades retain their original function-defining structure and add covered work space;
the watchtower progresses from timber to the existing reinforced structure.

This task does not claim the deferred 100/300/500-population acceptance gates. Snow is visual geometry over
existing navigation, its trails last for the current session, and upgrade materials are consumed on completion
without a separate delivery job. Existing unrelated imported/material/mountain work was preserved and is
present in the local build; it is not folded into this task's selective commit.
