# WM15 screenshot/video repair — delivered and verified

Feedback: 2026-10-30, three screenshots and recordings `09-29-13.mp4` / `09-33-25.mp4`.
Artifacts: `Artifacts/WM15RetestRepair`. Persistent Editor: Unity 6000.6.4f1, PID 2344.

## Current acceptance

| Route | Result |
|---|---|
| Authored building material review | `authoring-final`: 1/1 passed; review sheets 1 and 4 inspected |
| Work, construction, housing and domestic regression | `domain-final`: 56/56 passed; `role-regression`: 7/7 passed |
| Actual gameplay route | `gameplay-test-e`: 1/1 passed, 497.9 seconds |
| Final settlement-origin route | `gameplay-final`: 1/1 passed, 525.7 seconds |
| Empty-selection pier-card command plus full gameplay | `gameplay-final-assignment`: 1/1 passed, 429.7 seconds |
| Windows Development build | `build-final`: passed, 0 errors / 0 warnings; immutable provenance verified |
| Built Player snow frame timing and hut capture | Player exited 0; no runtime errors; both images inspected |

The gameplay route verifies immediate scaffolding while paused; automatic worker arrival and completion
of a real hut upgrade; usable beds for every housing assignment; normal NavMesh walking at x10 with visible
snow deformation; actual material hauling and automatic distant pier construction; fishing at the selected
built pier and fish production; then an exhausted resident physically returning home and occupying a bed.
No test teleports builders to their work or supplies construction buffers directly. The snow test positions
one resident at the route start, then uses a normal Move order for the full walking segment.

## Exact persistent-runner commands

Run from `C:/serenity_game` in PowerShell, with the existing Editor accessible:

```powershell
& .\Tools\UnityRunner.ps1 compile -OutputPath Artifacts/WM15RetestRepair/compile-s -TimeoutSeconds 600
& .\Tools\UnityRunner.ps1 test -TestNames Game.Tests.Editor.WM15AssetExpansionTests.AuthorAndValidateExpansion -OutputPath Artifacts/WM15RetestRepair/authoring-final -TimeoutSeconds 900
& .\Tools\UnityRunner.ps1 test -GroupNames '.*(WorkGroupAndJobTests|WM15LivingBuildingsTests|P106DomesticAndSaveTests|P109ConstructionStateTests|P109ConstructionClearanceTests).*' -OutputPath Artifacts/WM15RetestRepair/domain-final -TimeoutSeconds 600
& .\Tools\UnityRunner.ps1 test -TestNames Game.Tests.Editor.WM15RetestRepairRuntimeTests.AutomaticBuilderUpgradeAndDistantPierDelivery -OutputPath Artifacts/WM15RetestRepair/gameplay-final-assignment -TimeoutSeconds 1200
& .\Tools\UnityRunner.ps1 build -BuildPath Builds/WM15RetestRepair/Serenity.exe -OutputPath Artifacts/WM15RetestRepair/build-final -TimeoutSeconds 3600
```

Compile s passed with 0 errors / 59 warnings. Assets, Packages, ProjectSettings and HEAD are frozen during
rendered tests and build. The final build command is conditional on successful final UI gameplay acceptance.

## Diagnosis and repaired regressions

- The previous shutdown left stale EditorInstance metadata and an unlocked Temp/UnityLockfile. Verified
  there was no live Unity process, removed only that stale lock, and hardened runner process validation.
  Unity recovery scenes and all unrelated imported/mountain changes are preserved.
- The hut's generic primitive-shelter tint overrode its imported slots. RTS roof materials also retained
  inappropriate transparency. Owned material clones now cover original and replacement building meshes.
- Gameplay a passed upgrade arrival/work/completion and bed eligibility but failed its shoreline-placement
  fixture. Gameplay b failed on zero footprints: the resident crossed before the snow mesh was ready.
- Baseline close-camera frames included an 11.67-second frame; weather-disabled Editor p95 was 84.25 ms.
  After background water sampling, bounded snow generation and smaller weather budgets, run c produced
  57 footprints and 0.1469 m maximum depression. Its visual capture shows a continuous depressed trail.
  Weather-on Editor mean/p95/max were 67.01/81.36/188.87 ms. These are Editor measurements, not a claim
  of acceptable final Player frame rate; the standalone measurement remains required.
- Shore validation used mismatched terrain policies and discarded candidates whose preparation exceeded
  one frame. Gameplay d placed a pier but exposed a disconnected approach; e and final passed after a
  real route check. The origin now comes from an ordinary resident nearest the settlement, not a traveller.
- Selecting a building clears character selection. The pier card now automatically chooses a free player
  resident, remembers that exact pier, and exposes an explicit no-free-resident message when needed.
- Compile d/h/m failed on test/helper namespace or local-variable mistakes; subsequent compiles passed.

Winter collision/weather adjustments live in TiledTerrainWorld's initialization hook. The pre-existing
LateLandscapePresentation changes stay outside the task commit. The prior night's one-time shutdown was
already performed; this daytime follow-up does not request another shutdown.

## Final assignment regression follow-up

The first UI-inclusive run placed a different valid pier and delivered all construction materials, but
then repeatedly assigned the Build order to a distant resident while a carrier was already at the site.
The test correctly failed rather than counting deliveries as finished construction. Ungrouped ordinary
work now participates in nearest-available-worker assignment. Commands issued with an empty role roster
remain ungrouped, preserving save-state group-membership invariants. Player construction orders exclude
foreign residents. Added a regression covering nearest selection and strict work-state restoration.

`compile-s`: passed, 0 errors / 59 warnings.
`domain-final`: passed 56/56, including the new assignment regression.
`gameplay-final-assignment`, `role-regression` and the conditional Windows build are pending.
Optional new-game preparation also writes newly generated tiles to the existing disk cache before reuse,
so preloading does not bypass persistent caching for a new seed.


Final gameplay acceptance passed 1/1 in 429.7 seconds. All three delivery jobs and the Build job completed;
the builder was one of the actual carriers. Both pier command paths passed, real fish were produced, and
critical-energy sleep completed. Snow route recorded 57 footprints and 0.1469 m depression at x10.
Role regression passed 7/7. Windows build began at 09:22:05 UTC with source state frozen.


## Final Player result and limits

The first build compiled successfully but failed the immutable-source check because UMA automatically
resaved `Assets/UMAProjectData/Resources/AssetIndexerProject.asset`. Before/after manifests are preserved
under `Artifacts/WM15RetestRepair/build`. Rebuilding the stabilized state passed in `build-final` with
0 errors / 0 warnings. The UMA index is a pre-existing modified asset and is excluded from the task commit.

Player command (PowerShell; launched with `Start-Process -WindowStyle Hidden -PassThru`):

```powershell
C:\serenity_game\Builds\WM15RetestRepair\Serenity.exe -wm15SnowPerformance C:\serenity_game\Artifacts\WM15RetestRepair\player -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile C:\serenity_game\Artifacts\WM15RetestRepair\player.log
```

Player PID 5252 exited 0. `player/errors.txt` is empty. Actual Player screenshots `player/hut.png` and
`player/snow-close.png` were inspected: separate hut wall/roof/chimney colors, intact visible roof,
raised continuous alpine snow. Motion-driven trails were separately verified in the full rendered gameplay.

Intel Arc integrated graphics, 1920x1080, VSync off, no target-frame-rate cap; 180 measured frames per phase:

| Phase | Mean ms | p95 ms | Maximum ms |
|---|---:|---:|---:|
| Close snow, paused | 17.10 | 22.72 | 355.27 |
| Close snow, simulation running | 31.99 | 42.07 | 280.96 |
| Paused, diagnostic snow overlay disabled | 16.84 | 23.76 | 37.35 |

The steady paused overlay difference is only about 0.25 ms. This is not a promise of 60 FPS with simulation:
the measured running average is approximately 31 FPS, and isolated sub-second spikes remain. The old
multi-second cold stall did not recur in these runs. Broader population/simulation profiling remains the
previously deferred separate task; no unrelated quality reduction was applied.

Build provenance: Unity 6000.6.4f1; source HEAD at build `1fd2d54dcff2747157dc247d3b5a86c3a72527f4`,
plus the full recorded dirty/untracked source payload. SHA-256 fingerprint
`FC67893033217FD82CA4E54FAEDBD5CA1D59F41F153C60CF520688AEFA3A8A61`;
build GUID `98dbda9af174460faf1f677ffb5c06d4`. The task commit follows the immutable build, as required.
