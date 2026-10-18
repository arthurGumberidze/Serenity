# P10.9 integration/stabilization handoff

Status: **PARTIAL** on 2026-10-18. P10.10 has not started.

## Retained implementation

- The 13 connected-world route renderers are narrowed from 2.1 m to 0.75 m, trimmed away from district-centre overlap, sampled at no more than 3 m planar intervals, projected 0.045 m above Terrain and excluded from shadow casting/receiving. The P10.2 builder now authors the same result, and `P109IntegrationStabilizer.Apply` migrates the existing scene.
- Stable resource formations 0–3 are moved to four distinct Terrain-projected locations within 20 m of the starting settlement. Stable IDs and canonical resource/production state are unchanged.
- The U29 harness retains its mandatory 1600x900 default. An explicit `-p109Performance` profile requires exact 1920x1080 and records the feature-complete connected-world scenario without weakening thresholds, warmup, duration, focus, save or memory checks.
- No external asset or package was added. `ASSET_REGISTRY.md` therefore needs no entry.

## Acceptance matrix

| Gate | Result | Evidence / remaining blocker |
|---|---|---|
| P10.2 broad route-strip defect | Improved and regression-tested | 13 routes satisfy width, shadow, Terrain-conformance and sampling assertions; inspected `Artifacts/P109/player-p102/p102-local-connected-world.png`. The route style remains intentionally simple and is not claimed as a final visual redesign. |
| P10.2 p95 <=33.33 ms at 1920x1080 | **FAIL** | Two valid fully focused 60+300 s repeats measured p95 60.618 ms and 75.126 ms. A third complete run measured 48.428 ms but had 134 background samples and is diagnostic only. All runs preserved exact identity/canonical save state and showed no runaway managed/working-set growth. GPU averages (41.004/59.132 ms) and render markers dominate the failure. Raw CSV, summaries, screenshots and logs are under `Artifacts/P109/performance`. |
| P10.3 physical Shift-click / Shift-command | **UNPROVEN** | The computer-control surface enumerated browser surfaces only and exposed no native Unity Player window. Automated P10.3 coverage is retained but is not substituted for physical input. |
| P10.3 slow multi-frame drag | **UNPROVEN** | Same native-window blocker. |
| P10.3 needs/protected-carry resume | **UNPROVEN in normal Player** | Existing automated coverage remains; no physical normal-Player forcing route was available. |
| P10.3 Tier1->Tier2->Tier1 and remaining target/action matrix | **UNPROVEN in normal Player** | Existing stable-ID automated coverage remains; physical route still required. |
| P10.4 inherited U15 physical walk regression | **CLOSED** | `ProductionPlayModeTests.Tier1WalksWorksAndCommitsOneCanonicalProductionOutput` passed 1/1 in 9.578 s: the Tier1 agent selected production, walked on NavMesh, entered Working and committed canonical output. |
| P10.4 provenance-stamped build | **CLOSED** | `Builds/P109Final/Serenity.exe`, source commit `fa2322b6c8fa3f83621a47b4df785bea3d991607`, fingerprint `510DD146096B6798882412406CE9FD4001EC330CFBA6765CCF394A483C632134`, build GUID `1cc9c1ffc9c4472bbfe67a67948d7f34`, Unity 6000.6.4f1. Build succeeded with 0 errors / 44 warnings. |
| P10.4 complete normal visible resource/industry route | **UNPROVEN** | P102/P105/P107/P108 build-contained UI smokes all passed at 1920x1080 with D3D11, screenshots and clean shutdown, but they do not cover the full natural/industrial build, 1x/7x, exhaustion/blocked respawn/restoration route through physical user input. |
| Crash/null/missing-reference scan for accepted Player smokes | PASS | P102/P105/P107/P108 logs contain D3D11 initialization, normal shutdown and zero `Crash!!!`, null-reference, missing-reference, unhandled-exception or assertion matches. The interrupted benchmark attempt is excluded and retained separately. |

## Automated verification

- P10.9 focused EditMode: `Artifacts/P109/focused-editmode-final3.xml` — 3/3 passed.
- Isolated U15 physical-walk regression: `Artifacts/P109/u15-physical-walk.xml` — 1/1 passed.
- Affected EditMode (`P102;P104;P109;U29PerformanceGateTests`): `Artifacts/P109/affected-editmode.xml` — 35/35 passed.
- Affected PlayMode (`P102;P103;P104;ProductionPlayModeTests;U29PerformanceGatePlayModeTests`): `Artifacts/P109/affected-playmode.xml` — 32/32 passed.
- Full EditMode: `Artifacts/P109/full-editmode-final.xml` — 569 passed, 2 failed, 25 skipped. Failures are the recorded P8.2 on-land geography defect and the historical 500-participant wall-clock flake (36.759 s versus 20 s). Two earlier launch attempts are retained as licensing-client timeout/crash logs and produced no XML.
- Full PlayMode: `Artifacts/P109/full-playmode-final.xml` — 150 passed, 22 failed. The failures reproduce the already-recorded cross-test scene/static-state pollution plus the mandatory `-nographics` U31 GPU-runner rejection; the focused/affected P10.9 set is green. U31 remains BLOCKED.

## Visible final-build routes

All four commands used `Builds/P109Final/Serenity.exe -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11`, isolated `SERENITY_SAVE_DIRECTORY` / `SERENITY_SETTINGS_PATH`, and an output-specific `-logFile`.

- `-p102Smoke -p102SmokeOutput C:\serenity_game\Artifacts\P109\player-p102` — pass; connected local/strategic world, fog, construction and Save/Load.
- `-p105Smoke -p105SmokeOutput C:\serenity_game\Artifacts\P109\player-p105 -p105SmokeWidth 1920 -p105SmokeHeight 1080` — pass; roles, actions, age/LOD and Save/Load.
- `-p107Smoke -p107SmokeOutput C:\serenity_game\Artifacts\P109\player-p107 -p107SmokeWidth 1920 -p107SmokeHeight 1080` — pass; training, physical military movement, capture and Save/Load.
- `-p108Smoke -p108SmokeOutput C:\serenity_game\Artifacts\P109\player-p108 -p108SmokeWidth 1920 -p108SmokeHeight 1080` — pass; codes, family fade/skip, irreversible warning and v25 Save/Load.

These controllers exercise real UI/canonical services in the Player, but do not replace the explicitly required physical Shift/drag/resource-industry acceptance.

## Performance reproduction

For repeat `N`, launch the final build visibly and keep it foreground for the entire run:

```powershell
& 'C:\serenity_game\Builds\P109Final\Serenity.exe' `
  -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11 `
  -u29Benchmark -p109Performance -u29Width 1920 -u29Height 1080 `
  -u29Population 100 -u29Tier1 25 -u29Tier2 50 -u29Tier3 25 `
  -u29Repeat N -u29RepeatCount 3 -u29WarmupSeconds 60 -u29MeasurementSeconds 300 `
  -u29Seed 10918 -u29ConfigId p109-full-hd-100 -u29BaseCommit fa2322b6c8fa `
  -u29OutputDirectory "C:\serenity_game\Artifacts\P109\performance\repeat-N" `
  -logFile "C:\serenity_game\Artifacts\P109\performance\repeat-N\Player.log"
```

Valid focused repeat summaries are `repeat-1/summary.json` and `repeat-2/summary.json`. `repeat-3-valid/summary.json` is a complete diagnostic run but fails the focus-integrity check as well as p95. The first `repeat-3` directory is an excluded interrupted attempt: a pointer-up activated a work button, produced a handled reservation exception and exited before telemetry.

## Exact final commands

Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

```powershell
& $unity -batchmode -nographics -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.P109IntegrationStabilizer.Apply -logFile C:\serenity_game\Artifacts\P109\apply-stabilization.log -quit
& $unity -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testResults C:\serenity_game\Artifacts\P109\full-editmode-final.xml -logFile C:\serenity_game\Artifacts\P109\full-editmode-final.log
& $unity -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testResults C:\serenity_game\Artifacts\P109\full-playmode-final.xml -logFile C:\serenity_game\Artifacts\P109\full-playmode-final.log
& $unity -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P109Final\Serenity.exe -logFile C:\serenity_game\Artifacts\P109\build-final.log -quit
```

## Next action

Keep P10.9 active. On a host where native Player input can be controlled, execute the remaining P10.3 and full P10.4 physical route and capture evidence. Separately profile/optimize the connected-world render path until three fully focused 1920x1080 60+300 s repeats meet p95 <=33.33 ms. Do not start P10.10 before those gates pass or the user explicitly changes acceptance.
