# WM15 prop replacement — validation commands

Editor: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
All Unity/Player processes are started with PowerShell `Start-Process -WindowStyle Hidden -PassThru`
and awaited with `WaitForExit()`. Full 100/300/500 performance gates were not run.

Selective admission (local archives only):
```powershell
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Artifacts/admit_wm15_replacements.py
```
Final manifest: 52 files excluding `.meta` (Fishing 8, Bones 6, NatureFX 38).
The evidence copy of this script is `admit_wm15_replacements.py` beside this file.

Asset preparation:
```text
-batchmode -quit -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.WM15PropAuthoring.Generate -logFile C:\serenity_game\Artifacts\WM15Replacement\author-f.log
```
Final author-f: exit 0, 63 entries. Earlier attempts found unused particle trail material slots,
URP/base-package GUID aliases, a missing vendor fire script, and pier scaling. Those were corrected.
The authoring command now fails explicitly if a prefab cannot be saved (Unity can otherwise return null
without terminating batchmode). Previews inspected: stone, logs, fish, rod, pier, tanning, drying frame,
soil, trough and construction frame. These are previews, not proof of every runtime path.

Focused Editor checks:
```text
-batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.WM15PropReplacementTests;Game.Tests.Editor.P109ConstructionStateTests -testResults C:\serenity_game\Artifacts\WM15Replacement\focused-b.xml -logFile C:\serenity_game\Artifacts\WM15Replacement\focused-b.log
```
First run focused-a: 14/17 passed, 3 failures (null fire prefab and rod height; null fire also failed
the catalog material check). Focused-b after fixes: exit 0, **17/17 passed**. Includes 10 existing
construction checks and 7 prop checks. This is not a full Editor test-suite run.

Windows build command:
```text
-batchmode -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WorldMapAssetReplacement/Serenity.exe -logFile C:\serenity_game\Artifacts\WM15Replacement\build-a.log
```

Disposable Player route command (separate save directory, no user save edits):
```text
-screen-fullscreen 0 -screen-width 1440 -screen-height 900 -wm15OctoberSmoke C:\serenity_game\Artifacts\WM15Replacement\player-a -wm15PropsOnly -logFile C:\serenity_game\Artifacts\WM15Replacement\player-a.log
```
Environment: `SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\WM15Replacement\player-a-saves`.
The route covers a 26-resource gallery, selection/canonical quantities, wood/stone work, hunting and pier fishing.
Build-a exited 0: 0 errors / 81 warnings. Player-a exited 1: 23/25 checks passed, no runtime errors.
Its first gallery target was occluded by a starter tent; the diagnostic now finds a visible gallery pile.
Hunting was correctly rejected as MissingTechnology because this shortened route skipped UnlockAll;
the diagnostic now unlocks it explicitly, without changing gameplay rules. Visual inspection also found
white meat: Synty's original atlas survived in serialized `_Albedo_Map` after an earlier pipeline conversion.
The authoring adapter now resolves that saved property and preserves the atlas in the owned URP material.

Author-g then author-h used the same authoring command with matching log filenames, both exit 0.
Author-h selects PT_Generic_Rock_01 instead of the plain NatureGradient rock. Meat and rock previews inspected.
After the atlas fix, repeated only the 7 WM15PropReplacementTests, using `focused-c.xml`/`focused-c.log`:
exit 0, 7/7 pass. The material check now verifies that meat retains its atlas. The 10 unchanged construction
tests were not repeated. Total distinct focused tests remains 17.

Build-b uses the same Windows build command/path with `build-b.log`; Player-b uses the same route with
`player-b`, `player-b-saves` and `player-b.log`.

Final build-b: exit 0, **Succeeded; errors=0; warnings=81**. Source manifest remained unchanged during build;
fingerprint `F631144FF55285AC9DB40F6039897D18AFD440A6E32344F72493BEBE600BF79C`.
Build GUID `7e022466ce20450aa73cbb339d147123`. See `build-provenance.json`, `source-before.txt`, `source-after.txt`.
The build includes the working-tree changes on base commit 778b8622; its manifest records them explicitly.

Final Player-b: exit 0, **29/29 pass**, empty errors.txt. Inspected the final resource gallery (meat now
uses its original atlas), pier/rod/fish and working-character captures. Runtime checks cover real pathing,
work completion, hunting damage before harvesting and near/dry fishing output. This route is not full
map/weather/building visual acceptance, save/load regression, or a performance benchmark.
