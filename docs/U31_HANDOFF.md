# U31 HANDOFF — MVP Integration Gate

## Status

**BLOCKED** on 2026-10-12. U31's own integration implementation, tests, Windows build and normal Player smoke pass. It cannot be marked DONE because U29 is PARTIAL and U30 is IN_PROGRESS. No later stage was started.

## Verified player journey

Fixed seed `310031` is used by the built-Player smoke. Public U27 commands open Save/New Game, confirm New Game, rebuild the normal `LocalGameplay` composition once, verify one root/UI and four subscriptions, then Save and Load through the player facade. The connected PlayMode scenario covers UI work groups, production target and canonical production completion, Shelter Construction research, player building placement, separately scoped U26 AI construction, UI military-unit creation/provisioning/aggregation, exactly-once U25 travel consumption, U23/U20 battle resolution, U26 capture and common defeat rule, U30A discovery/victory, continued time, victory banner and post-victory Save/Load. It does not assign a victory marker or mutate campaign state directly.

The separate no-`-nographics` D3D11 PlayMode scenario verifies a clickable HUD, U22 TPS entry, actual Input System left mouse action and aim/raycast, pause rejection, one U20 hit, cooldown rejection, attacker/unit/battle identity continuity, U23 Physical-to-Abstract transition, calculated terminal battle, capture/victory banner, time control and Save/Load UI.

The normal Player smoke result is `C:\serenity_u31_player_smoke_20261012_113130\u31-player-smoke.json`: `passed=true`, seed 310031, 10 player Characters, 6 AI Characters, one composition root, one gameplay UI, four subscriptions, rival hidden before discovery and successful New Game/Save/Load. Its hidden-window screenshot exists but rendered black, so it is retained as an artifact and is not claimed as manual visual proof. GPU PlayMode and machine-readable UI assertions provide the visual/input acceptance evidence.

## Authority flow

`U27 UI -> existing U09/U15/U16 services -> canonical Character/resource/building state`

`U27 military commands -> U21 supply -> U25 travel/contact -> U23 battle representation -> U20 combat/outcome -> U26 capture/StateDefeatRule -> U30A CampaignProgressService`

`U27 Save/Load -> LocalGameplaySaveFacade -> SaveCoordinator -> snapshot v15 -> the same canonical services`

`U31PlayerSmokeController` is attached only for `-u31Smoke`, commands public UI operations, records evidence and owns no gameplay state.

## Integration findings and changes

- Added the opt-in normal Player smoke adapter and composition-root hook; normal startup is unchanged without its CLI flag.
- Added a fail-closed U31 verifier with isolated logs/build/smoke output, NUnit result validation, fatal-log scanning, PostgreSQL opt-in and per-process timeouts.
- Added U31 categories to existing deterministic bootstrap, AI isolation/victory, conservation, travel, capture and combat regressions rather than duplicating their authorities.
- Added a connected headless journey and a real Input System GPU/TPS journey. Time acceleration calls the existing runtime/clock; production acceleration calls `ProductionService.AdvanceTask`, not direct inventory/output mutation.
- No gameplay-authority defect remained after U30A. Development-time test-orchestration failures exposed incorrect assumptions about production ordering, occupied placement, logical battle locations, detailed/aggregate representation and per-attacker versus global attack counts; the tests were corrected without weakening production rules.

## Tests added or tagged

- `U31IntegrationPlayModeTests`: 2 connected/public-UI tests.
- `U31GpuPlayModeTests`: 1 real Input System/TPS/GPU test.
- U31 EditMode category: 11 tests covering same/different seed bootstrap, 10-player dynasty, 6-character isolated AI, v15 campaign/supply round-trip, v14 defaults, corrupt campaign rejection, resource conservation, exactly-once travel, capture and deterministic combat/victory state.
- Existing `U30ACompliancePlayModeTests`: 5 U31-tagged coverage tests included by the critical suite.

## Final commands and results

PowerShell parser:

`$errors=$null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path 'Tools/Verify-U31.ps1'),[ref]$null,[ref]$errors)` — PASSED.

Full verifier:

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\serenity_game\Tools\Verify-U31.ps1 -ProjectPath C:\serenity_game` — PASSED.

The verifier ran these Unity gates with Unity 6000.6.4f1:

- compile: PASSED; final clean log `C:\serenity_game\Logs\U31-compile.log`, exit 0;
- U31 EditMode: 11 passed, 0 failed, 0 skipped;
- U31 connected headless PlayMode: 2 passed, 0 failed, 0 skipped;
- U31 GPU PlayMode without `-nographics`: 1 passed, 0 failed, 0 skipped;
- critical U04/U09/U17/U20/U21/U22/U23/U24/U25/U26/U27/U28/U30A EditMode: 137 passed, 0 failed, 0 skipped;
- critical PlayMode: 49 passed, 0 failed, 0 skipped;
- persistence/legacy: 35 passed, 0 failed, 0 skipped;
- PostgreSQL: `SKIPPED_NOT_CONFIGURED` because `DYNASTYGAME_PG_TEST_DATABASE` is absent;
- Windows x64 Mono Development build: PASSED at `C:\serenity_game\Builds\U31\Serenity.exe`;
- bounded normal Player smoke: PASSED at `C:\serenity_u31_player_smoke_20261012_113130`.

The final connected test was additionally rerun after documentation preparation:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.U31IntegrationPlayModeTests -testResults C:\serenity_game\Logs\U31-focused-playmode.xml -logFile C:\serenity_game\Logs\U31-focused-playmode.log` — 2 passed, 0 failed, 0 skipped.

## Superseded and failed attempts

- The first restricted-sandbox compile crashed inside Unity's internal `BuildReportRestService`/`HttpListener` before project compilation (`Logs/U31-initial-compile-2.log`). Identical approved outside-sandbox compilation passed.
- Early focused test iterations failed on production wait ordering, an occupied building point, incompatible abstract locations, detailed/aggregate battle requirements, a batchmode-versus-graphics assertion, physical-battle timeout and global-versus-per-attacker hit counting. These were test orchestration defects; later focused and full runs supersede them.
- While the first full verifier was still running in a detached process, a second observer-launched verifier correctly failed on `UnityLockfile`. No process was terminated. The original verifier continued through critical suites, build and Player smoke and passed. A final standalone compile then rewrote `U31-compile.log` with exit 0.
- An attempted stricter timing experiment tried to intercept the independent AI army at its home settlement through route timing. The AI had already legitimately advanced to the player settlement; the unstable experiment was removed and the previously passing canonical U30A battle/capture setup retained. Its failed focused XML was superseded by the final 2/2 rerun.

## External gates

U29 remains **PARTIAL**. `docs/U29_HANDOFF.md` records a complete matrix with only 8/12 passing repeats and a sustained 1000-Character control p95 of 38.216 ms versus the required 33.33 ms. Short controls do not close it.

U30 remains **IN_PROGRESS**. Failed run3 is immutable. The remediated checkpoint-5 preflight at `C:\serenity_u30_preflight_20261012_run4` passed only 60 productive/72 actual seconds. No `C:\serenity_u30_soak_20261012_run4` eight-hour result exists yet, so requested/actual >=28,800 seconds, full coverage, memory and resume gates cannot be accepted.

Therefore U31 remains BLOCKED even though its own gate passes.

## Known issues and next permitted action

Verified remaining issues are only the failed U29 sustained 1000-Character p95 gate, the missing independently audited U30 eight-hour pass, and the non-informative black hidden-window smoke screenshot. Do not use the U31 smoke as replacement evidence.

Next: close U29 with a fresh qualifying full matrix; run and audit the fresh U30 eight-hour output; then rerun `Tools\Verify-U31.ps1` and finalize U31 in a dedicated commit if both dependencies are DONE. No later stage was started.
