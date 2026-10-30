# WM16 follow-up validation (2026-10-30)

Scope: in-game main menu; full resident snow before gameplay; loading progress; concurrent upgrades;
external AI cadence with two-day delay and action audit; livestock selection NullReferenceException.

## Diagnosis and changes

Preserved user Player.log / Player-prev.log in Artifacts/WM15Followup/user-logs before launching anything.
The stack points to ProductionSourcePresenter.Refresh using cached destroyed Renderers while selecting
or deselecting an animal resource. Renderer bindings now refresh after model replacement. The regression
replaces a model, selects it, verifies its highlight, and deselects it without an exception.

Snow formerly queued only a 13x13 patch square, then expanded with camera/actor movement. Initialization
now enumerates the full authored ellipse. Gameplay waits for all patches and map tiles. Geometry remains
resident across camera motion, while ordinary rendering culling is allowed. One-metre cached ground
samples feed the 20-cm deformation mesh; samples clamp to map bounds. Loading gets a bounded 10-ms
preparation budget. The progress bar represents weighted stages plus actual tile/patch completion.
The game clock remains at 10:00 during new-world preparation.

The original three starter upgrades completed even before the navigation change; the user's exact stuck
state was not available as a save. Inspection identified approach points that omitted carved footprints,
and arrival checks that demanded near-zero velocity despite crowd avoidance. Both conditions are repaired.
The final runtime test includes five simultaneous upgrades (three starter structures and two completed
hut fixtures), all executed by ordinary auto-assigned residents without forced arrival or progress.

Ruler calls: first at founding + 48 game hours, then 30 / 182.5 / 365 days. The shared ChatGPT URL returned
a fetch cache miss; committed integration ab116b5c and AI_RULER_STRATEGY.md supplied its contract.
Automatic war/crisis/trade events cannot bypass cadence; explicit manual/dialogue requests remain gated
by the initial delay. The audit writes accepted plan JSON, strategic execution and resident transitions,
including production recipe/site/task IDs. Faction + active plan sequence supports comparison, not proof
that the plan caused every action. Player residents have no external neighbor-ruler plan.

## Persistent runner evidence

Commands below run from C:/serenity_game. No batchmode test runner was used.

- `& .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts/WM15Followup/compile-d -TimeoutSeconds 600`
  — passed, 0 errors / 0 warnings, Editor 9996. Earlier compile-b passed with 7 existing obsolete-API
  warnings; compile-c with 104 existing warnings. One test fixture namespace typo was corrected before
  compile-b. The final compile-d followed a fresh Editor import/compilation.
- `& .\Tools\UnityRunner.ps1 test -GroupNames '.*(WM16FollowupTests|StrategicAiTests|P2StartupTests|WM15LivingBuildingsTests|WorkGroupAndJobTests).*' -OutputPath Artifacts/WM15Followup/domain-b -TimeoutSeconds 900`
  — 71/71 passed, including all three cadence options and the destroyed-renderer regression. An earlier
  70-case run used the previous loaded assemblies; it is not evidence for the added renderer test.
- `& .\Tools\UnityRunner.ps1 test -TestNames Game.Tests.Editor.WM15RetestRepairRuntimeTests.ConcurrentUpgradesAndCompleteSnow -OutputPath Artifacts/WM15Followup/gameplay-c -TimeoutSeconds 1800`
  — 1/1 passed in 414.11 seconds, including world initialization. Five upgrades completed; snow patch
  count remained unchanged after camera travel and a real x10 walk; 58 new footprints, maximum depression
  0.1457 m; returning to MainMenu succeeded. Screenshots and the actual action audit are copied into
  Artifacts/WM15Followup/gameplay-evidence. The snow image was visually inspected.
- gameplay-a passed the initial three-building route in 813.38 seconds. Gameplay-b was deliberately
  cancelled after code inspection found a map-edge interpolation risk. The Unity UI cancellation did not
  complete the runner callback; only the agent-owned Editor was restarted. Its final result is a failed,
  interrupted command, not a pass. The released stale UnityLockfile was removed only after confirming
  there was no project Editor and acquiring an exclusive file handle.
- `& .\Tools\UnityRunner.ps1 build -BuildPath Builds/WM16Followup/Serenity.exe -OutputPath Artifacts/WM15Followup/build -TimeoutSeconds 3600`
  — binary produced, but immutable-source gate failed: UMA auto-resaved its pre-existing dirty
  AssetIndexerProject.asset. Source manifest comparison found only that change; no gate was bypassed.
- `& .\Tools\UnityRunner.ps1 build -BuildPath Builds/WM16Followup/Serenity.exe -OutputPath Artifacts/WM15Followup/build-final -TimeoutSeconds 3600`
  — passed in 47.09 seconds, 0 errors / 0 warnings, GUID 6cb3f3b4d26c424da3f4e28b860efeeb.
  Provenance: Builds/WM16Followup/build-provenance.json, source HEAD ab116b5c0be30388e7628690e71c20ce9dcf24e5
  plus recorded dirty payload; fingerprint 655F5B97F565EC6E9664C74EE0F6DDADBD0944CBF5560876F0EC790B21A6758E.
- `& .\Tools\UnityRunner.ps1 stop -QuitOwnedEditor -OutputPath Artifacts/WM15Followup/runner-stop -TimeoutSeconds 120`
  — passed; owned Editor closed before standalone measurements.

The Editor snow route is functional evidence, not a Player FPS benchmark. General simulation-performance
work remains separate.

## Native Windows Player

Launched using PowerShell (process waited to completion):

```powershell
$player = Start-Process -FilePath C:\serenity_game\Builds\WM16Followup\Serenity.exe -WorkingDirectory C:\serenity_game -WindowStyle Hidden -PassThru -ArgumentList @('-wm15SnowPerformance','C:\serenity_game\Artifacts\WM15Followup\player','-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile','C:\serenity_game\Artifacts\WM15Followup\player.log')
$player.WaitForExit()
$player.Refresh()
$player.ExitCode
```

Exit 0; player/errors.txt is empty. Full world initialized, hut and close-snow screenshots captured;
snow-close.png visually inspected. Intel Arc, 1920x1080, VSync off: 180-frame close-snow samples:
paused mean 9.92 ms / p95 10.91 ms / max 278.20 ms; simulation running mean 23.82 ms
/ p95 28.19 ms / max 429.10 ms (about 42 FPS from mean). Diagnostic paused without snow overlay:
mean 8.87 ms / p95 12.87 ms / max 24.39 ms. These short samples do not establish sustained 60 FPS;
isolated frame spikes remain. Snow no longer creates/destroys patches in response to camera movement.
The native shutdown log reports two persistent allocations; no runtime exception was captured,
and native leak attribution was not investigated in this follow-up. Full raw log retained.

All delivery gates passed. Final source commit follows the build; provenance intentionally records
the pre-commit HEAD plus actual dirty payload, not a claim of building from the later clean commit.
