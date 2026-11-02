# UI08-R5a — unavailable calculation shown as zero

User report: Calculate Automatically shows zero in every row (berries 40, carcass 4).

Cause reproduced in code: StockPlanner returns CanApply=false and an empty Targets dictionary when no
prepared-food recipes are unlocked. UpdateStockNumbers rendered missing dictionary keys as zero regardless
of CanApply. Its explanatory warning appeared only after the long resource table.

The fix distinguishes unavailable results (dash) from valid zero demand, shows the cooking prerequisite
above the table and beside the calculate action, keeps Apply disabled and preserves current targets.
Valid zero results include a tooltip explaining the near-term horizon and existing-stock deductions.
No research is unlocked automatically, no recipe/ration rules or save formats are changed.

Added a service regression for empty plan/failed apply/preserved 40 berries and 4 carcasses, plus a required
native UI gate on the normal fresh game before the existing unlocked-recipe and large-stock fixture.

## Qualification

Commands run on the host through the existing persistent Unity runner:

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts/UI08R5a/compile' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames Game.Tests.Editor.StockPlanningTests,Game.Tests.Editor.UI08UserFeedbackTests,Game.Tests.Editor.UI08ReleaseQualificationTests -OutputPath 'Artifacts/UI08R5a/tests' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R5/Serenity.exe' -OutputPath 'Artifacts/UI08R5a/build' -TimeoutSeconds 1800
```

Compile `20261102123049626-3314a106840e402fb7c805a90f31f720`: PASS, 0 errors/warnings.
EditMode `20261102123053371-cb622675990e47fdbd1f73bdd587b63c`: **20/20 passed**.
Intermediate stock-only build passed; superseded by the combined qualification below.

## Additional user reports and requested workplace behaviour

The Tier1 scheduler started its own clock at zero while the canonical new-game clock starts at 10:00.
Research workers were assigned at canonical time, so calls with the scheduler's earlier checkpoint were
correctly rejected as stale. Runtime initialization/load/extra schedulers now rebase to canonical time;
empty schedulers also advance their clock. Rebasing does not replay needs or award skipped research.
Resident header, wellbeing and occupation now share the same activity text, including travel/indoor work;
the obsolete thirst gauge is removed and research percentages retain fractional progress.

Research House: initially unlocked, 80 logs + 50 stone, 240 construction work minutes, existing house art.
Basic chopping and stone gathering are initially open so the first house can be constructed. Assignment
through profession groups and ResearchService requires an operational house. Research projects show a
clear prerequisite before construction. Shared IndoorWorkplaceService validates same-side operational
buildings, actual movement arrival and distance; only then does rendering hide the worker and work accrue.
Shift end/cancel/load releases transient admission; destroyed/captured buildings do not allow work. Player
barracks training now uses the same movement/arrival authority instead of immediate renderer suppression.

Focused regression command:

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts/UI08R5a/compile-indoor-final2' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames Game.Tests.Editor.StockPlanningTests,Game.Tests.Editor.ResearchFrameworkTests,Game.Tests.Tier1UtilityAiTests,Game.Tests.Editor.P107MilitaryTrainingCaptureTests,Game.Tests.Editor.UI08UserFeedbackTests,Game.Tests.Editor.UI08ReleaseQualificationTests,Game.Tests.Editor.P109BuildingDefinitionTests -OutputPath 'Artifacts/UI08R5a/tests-indoor-final' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R5/Serenity.exe' -OutputPath 'Artifacts/UI08R5a/build-indoor' -TimeoutSeconds 1800
```

Compile `20261102130428125-223000daae2f43278313e3113591257a`: PASS, 0 errors / 7 existing test warnings.
Tests `20261102130447453-ed1b3fd8fed8458fac9091b8ffa7ad86`: **75/75 PASS** (research 25,
stock 11, Tier1 AI 16, training/capture 9, building definitions 5, UI feedback 7, release guards 2).
Tests cover start at 10:00/22:00 and load without backdated progress; blocked/missing house; unlocked
construction/material acquisition; travel/wrong-position no credit; arrival credit; shift-end release;
and barracks course delay across save/restore.
Build `20261102130522613-3fe9f883c6074390ac71952edef7416f`: PASS, 0 errors / 85 warnings, 87.99 seconds.
Source parent `76fbaf1325d7fdd14389d2530f98b2f608e81ef1`; fingerprint
`038BD48D33B3490EECBA75C0022C222811C3B2CED87663263ABB5D2416158B37`, GUID `04ffc9b547d84a4fb99121e86b8e9ac5`.
Native Player qualification PASS in `Artifacts/UI08R5a/player-indoor` with isolated save/settings paths.

## Final native result — PASS

Host-launched Windows Player exited 0; all required gates in `Artifacts/UI08R5a/player-indoor/ui08-player-smoke.json`
are true. New gates: research_physical_workplace, barracks_physical_arrival, inspector_consistent_no_thirst,
stock_locked_recipe_feedback. Existing menu, stock/manual/monthly menu/save-load, military/TPS, realm,
victory/defeat, 1920x1080 and 1280x720 at 125% HUD bounds passed too. No exceptions in Player.log.
Visually inspected research workspace, unavailable stock preview, researcher and trainee inspectors.

Exact launch (isolated user data):

```powershell
$indoorRun = Join-Path (Get-Location) 'Artifacts\UI08R5a\player-indoor'
$env:SERENITY_SAVE_DIRECTORY = Join-Path $indoorRun 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $indoorRun 'settings.json'
$indoorPlayer = Start-Process -FilePath (Join-Path (Get-Location) 'Builds\UI08R5\Serenity.exe') -ArgumentList @('-screen-fullscreen','0','-force-d3d11','-logFile',(Join-Path $indoorRun 'Player.log'),'-ui08Smoke','-ui08SmokeOutput',$indoorRun) -WindowStyle Normal -PassThru
$indoorPlayer.WaitForExit()
```

Research was observed visibly travelling with zero accumulated work, then hidden at the workplace with
positive work. Training likewise stayed visible during movement and became hidden only after arrival.
The native fixture constructs completed workplaces through the placement/presentation APIs; construction
cost/content availability and blocked assignment are separately verified by EditMode tests. Night clock
and load arithmetic are covered by focused regressions; the live physical route runs during day shift.
No user saves were modified. This is functional qualification, not a mass-population performance claim.
