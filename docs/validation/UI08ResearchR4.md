# UI08-R4 — approved circular research implementation

Date: 2026-11-02 (local). Unity 6000.6.4f1, persistent Editor runner.

## Scope and authorization

The user approved the R4 HTML for game implementation and explicitly confirmed professional-group researcher
assignment and a bounded effective contribution of 4, upgraded to 5/6. No new art dependencies, unrelated
simulation rewrite or old-save migration. The character inspector retains its compact layout, labels the
existing Care action «Лечиться», and no longer exposes personal priority/research/scout/military task controls.
Task/object priorities remain. The former research assignment shortcut in the selection ribbon is also gone.

## Runtime contracts

- 51 definitions, 33 retained legacy IDs, five circular direction hubs, local top-down single-parent branches.
  All graph prerequisites are drawn; no hidden OR or cross-direction prerequisite.
- Canonical ResearchService owns availability, costs, progress, science modifiers and epoch completion.
  Wood/stone are paid once at project start using existing funding inventories. Cancellation does not refund.
- Practical facts use current inventories, living residents, actual military professions and operational homes.
  ResearchBalanceSettings exposes the epoch/practical/contribution thresholds.
- Existing profession membership and shifts supply player researchers at the central reconciliation cadence.
  New workers start from the current tick; no retroactive progress. Existing skill and health rate providers remain.
- Contribution is scaled by min(capacity, assigned count) / assigned count; upgrades add one capacity each.
  Observation/calendar/systematization add 5/5/10 percent. Inactive workers do not earn work.
- Eleven future-only definitions cannot start or gate playable technologies/epoch completion. Restorative mining
  was corrected from the prototype audit: existing IsCompleted(RestorativeTechnology) consumers already work.
- The milestone requires 14 earned discoveries, three developed directions with three discoveries each, key
  discoveries in three directions, basic fire/tools/shelter and existing settlement facts. Military is optional.
  Later historical eras remain U38; no new era/world state is invented here.

## Commands and evidence

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath 'Artifacts/UI08R4/compile-qualified' -TimeoutSeconds 900

$researchCases = Select-String -Path 'Assets/Game/Tests/Editor/ResearchFrameworkTests.cs' `
  -Pattern '\[Test\] public void (\w+)\(' |
  ForEach-Object { $_.Matches[0].Groups[1].Value } |
  Where-Object { $_ -notin @('U15ProductionAndU16ResearchAdvanceTogetherForMultipleYears',
    'ThousandWorkerCentralizedResearchHasBoundedTimeAndAllocation') } |
  ForEach-Object { 'Game.Tests.Editor.ResearchFrameworkTests.' + $_ }
$researchCases += 'Game.Tests.Editor.ResearchFrameworkTests.ApprovedSpecializationsReachEpochWithoutResearchingTheWholeTree'
$researchCases += 'Game.Tests.Editor.UI08UserFeedbackTests'
$researchCases += 'Game.Tests.Editor.UI08ReleaseQualificationTests'
& .\Tools\UnityRunner.ps1 test -TestMode EditMode -TestNames $researchCases `
  -OutputPath 'Artifacts/UI08R4/edit-layout' -TimeoutSeconds 900

& .\Tools\UnityRunner.ps1 test -TestMode PlayMode -TestNames `
  'Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer',`
  'Game.Tests.PlayMode.GameplayUiPlayModeTests.SelectionInspectorAndDeveloperToggleRemainAdapters' `
  -OutputPath 'Artifacts/UI08R4/play-final' -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -TestMode PlayMode -TestNames `
  'Game.Tests.PlayMode.GameplayUiPlayModeTests.UserFeedbackWorkspacesRenderTreesRecruitmentAndBlockTheWorldPointer' `
  -OutputPath 'Artifacts/UI08R4/play-ready' -TimeoutSeconds 1200

& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R4/Serenity.exe' `
  -OutputPath 'Artifacts/UI08R4/build' -TimeoutSeconds 1800
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R4/Serenity.exe' `
  -OutputPath 'Artifacts/UI08R4/build-final' -TimeoutSeconds 1800
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R4/Serenity.exe' `
  -OutputPath 'Artifacts/UI08R4/build-layout' -TimeoutSeconds 1800
& .\Tools\UnityRunner.ps1 build -BuildPath 'Builds/UI08R4/Serenity.exe' `
  -OutputPath 'Artifacts/UI08R4/build-qualified' -TimeoutSeconds 1800
```

Final compile: **0 errors / 139 warnings**, request `20261101215137678-b65b7fed8fa04a909c76defdaed881fd`.
Final EditMode: **28/28**, request `20261101214656336-244b73b298914b7caa64e7ac1b5c1d62`.
Includes material exact-once behavior, prerequisites, cancellation, tier consistency, save/load progress,
bounded group contribution, no retroactive work for new or continuously assigned workers, four actual
specialization routes with the normal game's starting stone tools, post-milestone access and real catalog effects.
No 730-day or 1000-worker performance suite was launched. Earlier preliminary passes precede final edits and
are not final-source evidence. Two intermediate compile errors (missing imports/test capture module) were
corrected before the successful compile/test chain.

PlayMode: the inspector case passed in `play-final` (the same two-case command's research case hit its old
180-second initialization timeout). The corrected research/group route passed **1/1** in `play-ready`, request
`20261101212317743-eaa2b370fc4143c19e98ae8fd6c77d57`, after the real world/snow readiness gate, 440.525 s total.
The intermediate `play-layout` attempt exposed a test using a disposable presenter ID; it now uses the canonical
resident ID and a time-based readiness wait. Group admission/removal, UI-started fire research, actual graph
edges/bounds and the existing dynasty/military/economy pointer regressions passed.

The Editor ignored Screen.SetResolution: the actual captured Game View was **751x362**. Its image is
`Artifacts/UI08R4/research-editor.png`, not evidence of 1920x1080. It revealed a world resource tooltip over the
fullscreen graph. The subsequent presentation fix suppresses world hover/work labels under full-screen menus;
the research workspace also recalculates its height after resolution/scale changes. These final presentation
changes and the idle-project accounting fix are compiled and covered by the final EditMode/native chain;
the long world-initializing PlayMode case was not repeated solely for them.

The first package was rejected by immutable-source verification: UMA regenerated
`Assets/UMAProjectData/Resources/AssetIndexerProject.asset`. Before/after manifests differ only by that source
and Git status; the generated index SHA256 is `4C13F1757FC057C07956C55D7DA3A4826EFAD551C8C83804A1D763B8E5BB9FCF`.
Temporary PerformanceTestRunInfo/Settings files were automatically cleaned by the build callbacks.
The index is retained. The immediate stable rebuild passed: request
`20261101213624908-09d43b7be0fb4368908e014ac89c33ff`, 0 errors / 0 warnings, 34.71 s.
Executable: `Builds/UI08R4/Serenity.exe`. Build GUID: `a150757b9aa54f7f980dc36ef0c3a5f6`.
Source fingerprint: `8E9BD452BFA9632234339D5F052F1EC833194F257D121A903A28813968CAB0FE`.
The provenance records pre-commit HEAD `d3b84e51f92e4add1d3ce95f6fcf17a831a7e29b` plus the actual
dirty-source fingerprint; the implementation commit is made after qualification.

The first native smoke used a hidden window and failed the black-frame guard at the menu capture.
That failed test process was stopped; the same binary is being checked in a visible window, required for
real rendered screenshot qualification. No source change or weakened capture threshold was used.

```powershell
$researchSmokeOut = 'C:\serenity_game\Artifacts\UI08R4\player-qualified'
New-Item -ItemType Directory -Force -Path $researchSmokeOut | Out-Null
$env:SERENITY_SAVE_DIRECTORY = Join-Path $researchSmokeOut 'saves'
$env:SERENITY_SETTINGS_PATH = Join-Path $researchSmokeOut 'settings.json'
$researchSmokeArgs = @('-screen-fullscreen','0','-force-d3d11',
  '-logFile',(Join-Path $researchSmokeOut 'Player.log'),
  '-ui08Smoke','-ui08SmokeOutput',$researchSmokeOut)
$researchSmokeProcess = Start-Process -FilePath 'C:\serenity_game\Builds\UI08R4\Serenity.exe' `
  -ArgumentList $researchSmokeArgs -WindowStyle Normal -PassThru -Wait
exit $researchSmokeProcess.ExitCode
```

The first visible run rendered the complete 1920x1080 graph and passed menu, settings, HUD, management,
people/dynasty, military/TPS, save/load identity and pause checks. It then correctly rejected the old smoke
fixture's attempt to create a unit from civilians. The fixture now assigns military professions through the
existing player professional-group service before formation (enemy fixture residents likewise have military
professions); the production restriction is preserved. The screenshot also exposed stale viewport height
after a scale change and an unstyled search input. Responsive layout now resizes the research workspace,
the input uses the shared field theme, and native qualification additionally requires at least 600 physical
pixels of graph height at 1920x1080.

Intermediate corrected Windows build: **succeeded, 0 errors / 52 warnings**, request
`20261101214701546-dfa61e20e37240cfaa00b5a9b2f824ed`, 45.551 s, immutable-source check passed.
GUID `962d68edfb4f458fbf1ff3647410c3cd`, source fingerprint
`EE443CBA73EEF87B75BF1C8205A201A56E27596D8FF309946644B99EFC05336F`.
Warnings are recorded in the runner log; this is not a warning-free build claim.
The next native pass completed every gameplay predicate, including realm travel, victory and defeat,
but the added height guard compared UI Toolkit logical units with physical pixels. Its report remains a
failed intermediate attempt (`player-final`), not a green result. The guard now converts using panel-to-screen
scale and records the measured physical height. The viewport reserves another 30 logical pixels for the
legend/help footer, keeping that text visible at full HD. The 28-test EditMode pass precedes only this
presentation size/measurement correction, which is compiled and checked in the final native run.

Final Windows build: **succeeded, 0 errors / 52 warnings**, request
`20261101215158192-df05d2babfc94c2883a3209909190fa1`, 47.295 s. Immutable-source verification passed.
GUID `bb953e62b6eb43e98f112a9504ce6c03`, source fingerprint
`3BEB9C2FF29E8E240E2791E32E788695056AD497C4BB0CAD5F67E8BAB16429A9`.
After qualification, only an extra blank line at the end of StoneAgeResearchPlan.cs was removed for
`git diff --cached --check`; no executable code changed. Documentation was finalized after the run.
Final native result: **PASS, exit code 0**, all predicates true in
`Artifacts/UI08R4/player-qualified/ui08-player-smoke.json`. Includes normal menu/new game, settings/focus,
HUD/selection, management, people/dynasty/events, military/TPS, realm/local return, save/load stable IDs,
pause, victory/defeat, both resolutions, and complete graph bounds. Measured graph viewport: **612 physical
pixels high at 1920x1080**. Actual screenshot `Artifacts/UI08R4/player-qualified/ui08-research-1920x1080.png`
was visually inspected: all branches/epoch strip fit, right detail text wraps, help/footer remains visible,
search uses the theme, and no world resource label overlaps the menu.

This run sampled UI refresh mean/max **31.17 / 41.87 ms**, frame mean/p95 **13.04 / 18.85 ms**.
These are this test route's observations, not a mass-population benchmark or a universal FPS guarantee.
No exceptions were found in the final Player log. Existing ICU/text warnings remain.

After qualification, the owned Editor was stopped with:
`& .\Tools\UnityRunner.ps1 stop -QuitOwnedEditor -OutputPath 'Artifacts/UI08R4/shutdown-editor' -TimeoutSeconds 60`.
Request `20261101215651597-2f461a1fa7ef4723b5d4b25beb690be7` succeeded. The user explicitly requested
Windows shutdown after delivery; verify clean/pushed Git and stopped Unity/Ollama before scheduling it.
The Editor remained open after the runner response; an ordinary `CloseMainWindow()` request then closed
it and its import workers normally. Process verification found no Unity or Ollama processes afterward.
Ollama had no loaded models before its idle app/server processes were stopped. The Editor shutdown log
contains JobTempAlloc/native allocation warnings; no new source fix is claimed for those existing subsystems.

## Limits

The 3–6-hour balance target remains an estimate, not a timed full playthrough. The HTML is design history;
the runtime uses the existing project icon atlas. Old save files remain untouched and unmigrated; retest with
a new game. The Care rename invokes the existing command, without adding new healing rules.
