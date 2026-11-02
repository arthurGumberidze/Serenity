# UI08-R5 — grouped resources and monthly stock planning

User approved `docs/prototypes/stock-review-v1/Serenity-Stock-Review.html` on 2026-11-02 and requested
implementation, retaining manual targets and planning 3 portions per living resident/day for 30 days.

## Runtime changes

- Six permanent HUD group buttons (41 resource IDs, no water), wrapping at narrow widths, exact scrollable
  hover/focus/click breakdown. Notification bell occupies its own flex cell. Popovers persist across refresh
  without rebuilding their scroll content. Character inspector and notifications start below the taller HUD.
- Settlement stock workspace: category tabs, exact stock totals, manual target editing, automatic/manual
  toggle, resource priority and reserve override; warehouse horizon 1–30 days, default 3, default final-product
  buffer 25%. Calculate previews first; apply recalculates current facts and writes only automatic rows.
- Pure Simulation planner uses the actual unlocked production recipes and yields. One dish per day, fish
  then generic cooked meat when available; 30 editable day choices, locked choices replaced deterministically.
  Monthly gross demand stays separate from near-term net ingredients/fuel. Shared inputs are accumulated
  before rounding batches; available field-dressed carcasses substitute for ordinary carcasses.
- Canonical adapter reads living player residents, professional groups, inventories, remaining construction
  materials, active health cases and animals per pen. Recipe consumers get derived goals; unconnected items
  receive no invented quota. Equipment is a one-time role need, not multiplied by month length; inventories
  include carried physical equipment. Cosmetic appearance clothes are not counted as resource equipment.
- Saved production policy includes per-resource mode/priority/reserve, horizon, menu, first day and optional
  hourly updates. Editing a manual target removes that resource from automatic writes. Worker selection uses
  resource priority as a secondary ordering after building/task policy; no personal resident priorities.
- Planning norms live in `StockPlanningConfig.json`. Hunter bow/arrow and medicine values are stock-planning
  norms, not a newly implemented equipment durability/issue or treatment-course simulation. Existing army
  training equipment costs remain intact; this change does not invent swords, edible milk or recipes.
- 30 days is the planning window. Actual needs-driven Tier1 eating and existing simulation food consumption
  remain unchanged; the requested 3/day is the planning allowance. Shelf/capacity warnings are surfaced;
  the calculation does not guarantee freshness or turn a month of demand into a mandatory month of stock.

## Commands and evidence

All Unity commands use `Tools/UnityRunner.ps1` on the host, outside Windows app-container sandbox.

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts/UI08R5/compile-release' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames Game.Tests.Editor.StockPlanningTests,Game.Tests.Editor.ProductionAndFarmingTests,Game.Tests.Editor.P109BuildingProductionTests,Game.Tests.Editor.UI08UserFeedbackTests,Game.Tests.Editor.UI08ReleaseQualificationTests -OutputPath 'Artifacts/UI08R5/regression-final-polish' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R5/Serenity.exe' -OutputPath 'Artifacts/UI08R5/build-release' -TimeoutSeconds 1800
```

Final compile request `20261102112621879-8318cd5193a4462c95f284d1f434be09`: PASS, 0 errors / 139 warnings.
Final focused regression request `20261102112809602-b006f8d4f37840018923822cd2a49af8`:
**42 passed / 0 failed**, including 10 stock planner cases, production/farming, living-building production,
continuous cooking stopping after a whole batch, save round trip and UI qualification guards.
Explicit compilation preceded these tests: the runner test command alone does not refresh modified scripts.
Earlier 38-test runs used the previous compiled test set and are not the final qualification evidence.

The first combined regression had 37/38 passing: the existing normal-Player guard correctly rejected using
the cheat input API for a high-stock fixture. The fixture now seeds canonical test storage and a validated
research snapshot after the normal menu route, without enabling cheats. This is explicitly controlled test
data, not evidence that an ordinary starting settlement has 10,000 of every resource or finished cooking.

First Windows build `20261102111425979-fe47ca40d8594fa6a01e7801d5776742`: succeeded, 0 errors / 86 warnings,
235.49 seconds. The first native route passed menu/HUD/management/research bounds (612 physical pixels),
but stopped in the new stress fixture because no starting storage used unrestricted filters. The fixture now
explicitly configures its isolated test storage before seeding. This failed attempt is retained in `player/`.
Visual inspection also found the notification number inherited an unnecessary chip background; it now uses
a plain bounded label inside the bell.

Pre-polish Windows build `20261102113447660-45a18986e6fe41a0990b2c42cffd064d`: succeeded, 0 errors / 52 warnings,
100.92 seconds. `Builds/UI08R5/build-provenance.json` records build GUID
`348de0e054864cc4b68c959ac42498ea`, source fingerprint
`F4FCDD08B17F6C372534B73BA915C9075B5B45EA8BC4CD8B708D64C693B65427` and parent commit
`b6dd0f3e183b2ccaba7d9bf11b87b9ce9636eae7` (task changes were uncommitted at build time).
UnityRunner PowerShell AST syntax validation also passes.

Native qualification command, run in the normal host session with a visible window:

```powershell
$stockRun = Join-Path (Get-Location) 'Artifacts\UI08R5\player-release'
New-Item -ItemType Directory -Path $stockRun -Force | Out-Null
$env:SERENITY_SAVE_DIRECTORY = Join-Path $stockRun 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $stockRun 'settings.json'
$stockPlayer = Start-Process -FilePath (Join-Path (Get-Location) 'Builds\UI08R5\Serenity.exe') -ArgumentList @('-screen-fullscreen','0','-force-d3d11','-logFile',(Join-Path $stockRun 'Player.log'),'-ui08Smoke','-ui08SmokeOutput',$stockRun) -WindowStyle Normal -PassThru
$stockPlayer.WaitForExit()
```

The `player-qualified` route passed with exit 0: all stock/layout/save gates true, 30 menu selectors,
manual wood goal 777 retained, hourly update policy/menu persisted, all six HUD groups visible and the bell
within its own cell at 1920x1080 and 1280x720/125%. The existing research graph still fits (612 physical px).
No managed exceptions were logged. The large-stock fixture adds 10,000 units of each retained resource.
This is not a mass-population performance benchmark; refresh average/max were 46.70/69.11 ms, frame average/
p95 24.52/44.75 ms on this run. Normal menu, dynasty, military/TPS, realm, save/load and outcome routes passed.

Visual review then exposed cramped stock toggles/truncated priority text and translucent popover background.
The final polish widens fields, gives toggles dark/gold states and makes the popover opaque. Added native
captures scroll to the planner and month menu. Post-polish compile requests
`20261102114225605-674bb892c2e046ccaf512de0d4bbe63e` (0 errors/52 warnings) and
`20261102114346344-6618f82dd2c84eebbdb35212acda97c6` (asset refresh, 0 errors/warnings) passed.
Post-polish regression `20261102114351137-9a7100cf0df846dca0360aeea8e26703`: 42/42 passed.
Final build `20261102114439291-9cfcae25fdaf4d018431a6490da399b6`: succeeded, 0 errors / 85 warnings,
178.90 seconds. Current build GUID `3c8697535ad54db7a4c08b2a3aa37f35`, immutable source fingerprint
`00B32F72497E062B9D08C7490A029BE45A1496E091B8E409F8E65C9ED63864BE`, same parent commit as above.
The `player-final` route passed, exit 0, with every stock/layout/save and existing gameplay gate true.
Visual inspection confirmed readable priority fields, visible dark/gold automatic toggles and an opaque
resource popover. Its planner capture exposed the longest dish name clipping in the original 220px day
card, so day cards were widened to 280px. Release qualification of that final layout correction follows.

Release compile `20261102115333648-32af657c2ead4e9da07ea2237f70553c` and build
`20261102115408877-1334b6ecc00e49f59ddf7eb01ab33430` both succeeded with 0 errors / 0 warnings.
Build duration 62.77 seconds; final GUID `3a658bbd3c2843119f9b51a572be4d6d`, source fingerprint
`7D699604E79185EA379A36BBB36AB8BF4034C6B40DA80D1CEC2F3AC221149D04`.
Only the day-card width changed after the 42-test run; the native route verifies that presentation change.

Release native route (`player-release/ui08-player-smoke.json`) **PASS**, process exit **0**, all gates true,
no managed exceptions. Full-HD planner capture visibly shows the complete «Приготовленные ягоды» name;
stock rows and compact HUD were visually inspected. Six groups and the bell pass geometry checks under
10,000-per-resource stress at both resolutions. Manual target 777, 30-day menu and automatic update policy
survive save/load. Research still fully fits in its 612px viewport. The normal menu/gameplay/outcome route
also passes. Measured refresh average/max 50.32/82.39 ms and frame average/p95 24.74/43.91 ms are observations
of this ten-resident fixture, not a mass-population FPS guarantee.

Final captures under `Artifacts/UI08R5/player-release/`:
- `ui08-stocks-1920x1080.png`: manual goal and automatic controls.
- `ui08-stock-planner-1920x1080.png`: options and full dish names.
- `ui08-resource-groups-1920x1080.png`: opaque exact-count breakdown.
- `ui08-resource-groups-1280x720-125.png`: compact HUD/stock controls.
- `ui08-research-1920x1080.png`: retained research layout regression.

UI08-R5 is complete. User retest is the next action; no new feature task is started.

## Editor recovery

The first runner launch created Unity PID 34908 inside Windows app-container sandbox. Licensing IPC failed;
the crash stack is Mono `wakeup_pipes_init` / `System.Net.HttpListener` / `UnityEditor.BuildReportRestService`.
Evidence preserved locally in `Artifacts/UI08R5/licensing-crash/Editor-sandbox.log`. No report was uploaded.
Only the orphan licensing process from that launch was stopped. Host launch restored the existing license.
The runner now rejects app-container launches and removes an accessible stale project lock only when no
Unity Editor exists. No version migration, license bypass or Windows security changes were performed.

An intermediate C# error assigned a Domain property through an internal setter from Simulation. It was fixed
with constructor initialization. Safe Mode retained the old compile result until the Editor restarted;
the subsequent persistent-runner compile completed with zero errors. This is recorded as a repaired
development failure, not a successful first attempt.

## Boundaries

No old save migration/merging, new Asset Store dependency, UI01–UI08 parallel stage, or repeated shutdown.
Earlier full-build asset warnings remain in artifacts; the final incremental compile/build reported zero.
Do not infer mass-population performance from this functional qualification.
