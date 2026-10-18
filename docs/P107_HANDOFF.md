# P10.7 handoff — Military training, direct attack and settlement capture

Status: **DONE** on 2026-10-18. `NEXT_TASK.md` advances to P10.8. No P10.8 code, family diagnostic or cheat effect was implemented.

## Delivered

- `MilitaryTrainingService` trains an existing living adult in an operational same-side `military_barracks`. Recruit→Soldier→Veteran→Elite is data-driven; every level increases duration, cost and combat skill. Funding is checked and consumed transactionally. Completion preserves the same `StableEntityId`/Character instance, applies P10.5 military clothing and adds that person to the canonical military roster.
- `MilitaryAttackCommandService` is the single P10.7 direct-attack policy. Attackers must all be trained active members of one unit/side. It exposes explicit no-attacker/civilian-attacker/mixed-side/friendly/civilian-target/non-military-building/missing-target/no-path/unavailable reasons. A valid order uses the existing shared-world movement job and resolves military combat, barracks damage or a nearby registered capture flag only at physical proximity; it never teleports and cannot claim a remote flag.
- RMB exposes `Напасть` with visible availability reasons. The military panel exposes rank, next course, increasing cost/duration, progress and settlement capture. Strategic `Напасть` transitions the unit to detailed physical representation and walks to the selected same-world location.
- `SettlementFlagCaptureService` owns a persisted five-game-hour flag clock. The current main building and current operational barracks must share the attacker owner; without an operational barracks only the main flag is required. A destroyed barracks falls through to the next operational one. Defender contest freezes progress, defender recapture resets it, pause advances nothing, and calendar speed scales it (five real minutes at x1).
- Completion transfers every surviving canonical building through the existing damage/ownership authority, synchronizes settlement/map owner and residents, and keeps inventories/stores on their stable building owners. A deterministic saved seed selects defender soldiers who join; the rest are removed from units and resolved dead off-camera. The saved outcome makes reload/repeated advance idempotent.
- Snapshot schema is v24. The strict XML payload includes training records/sequence/tick, capture flags/progress/status/seed/outcome and direct orders/sequence. PostgreSQL version validation now explicitly accepts v21–v23 as well as v24.

## Automated evidence

Unity executable for every command: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

- Compile: `Unity.exe -batchmode -nographics -quit -projectPath C:\serenity_game -logFile C:\serenity_game\artifacts\p107-compile-physical-flags2.log` — exit 0, no compiler errors.
- Focused EditMode: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P107MilitaryTrainingCaptureTests -testResults C:\serenity_game\artifacts\p107-editmode-physical-final.xml -logFile C:\serenity_game\artifacts\p107-editmode-physical-final.log` — **6/6 passed**, including near-only physical flag claims.
- Focused PlayMode: `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P107MilitaryTrainingCapturePlayModeTests -testResults C:\serenity_game\artifacts\p107-playmode-physical-final.xml -logFile C:\serenity_game\artifacts\p107-playmode-physical-final.log` — **2/2 passed**.
- Context-menu regression after the full sweep: `... -testFilter Game.Tests.PlayMode.P103ContextInteractionPlayModeTests ...` — **2/2 passed** (`p107-p103-regression.xml`).
- v24 U29 save observation after the full sweep: `... -testFilter Game.Tests.PlayMode.U29PerformanceGatePlayModeTests.SaveObservationRoundTripsEveryNormalGameplayPayload ...` — **1/1 passed** (`p107-u29-save-regression.xml`).

The complete suites were executed and retained without presenting inherited failures as green:

- Full EditMode: `... -runTests -testPlatform EditMode -testResults ...\p107-editmode-full-final.xml` — **557 passed, 1 failed, 25 skipped (583 total)**. The only failure is the same P8.2 authored-geography assertion recorded in `P106_HANDOFF.md`; P10.7 changes no authored scene geography.
- Full PlayMode: `... -runTests -testPlatform PlayMode -testResults ...\p107-playmode-full-final2.xml` — **145 passed, 24 failed (169 total)** before the two affected harness corrections above. P10.7 focused tests pass. Two failures were P10.7-adjacent and fixed/rerun green: the old eight-action expectation and the standalone U29 restore coordinator missing v24 services. The remaining 22 reproduce the project’s broad cross-test scene/static-state pollution, legacy movement timing, and the U31 GPU test intentionally rejecting `-nographics`; they are not hidden and do not change the established legacy task statuses.

## Build and visible Player evidence

Final build command:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P107-final3\Serenity.exe -logFile C:\serenity_game\artifacts\p107-final-build-final3.log -quit`

Accepted result: **Succeeded; errors=0; warnings=44**. Unity 6000.6.4f1, build GUID `ac4725fe644149fb84b6c99af5e2c8dd`, source commit `285d24bb6ca8f228d524415d367456b1c6163433`, source fingerprint SHA-256 `3988AF0178B70AC0DCAF0B1027C5D5452FA1CBB5B49855C852AE8948D1DBE47F`. `Builds/P107-final3/build-provenance.json` exists and the before/after source manifests match.

Visible Player command:

`Serenity.exe -force-d3d11 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -p107Smoke -p107SmokeOutput C:\serenity_game\artifacts\P107\player-final3 -p107SmokeWidth 1920 -p107SmokeHeight 1080 -logFile C:\serenity_game\artifacts\P107\p107-player-final3.log`

The ordinary Main Menu→New Game route passed every machine-readable gate in `Artifacts/P107/player-final3/p107-player-smoke.json`: same-person training, Recruit rank/skill/clothing/roster, civilian direct-target rejection, physical move job and changed world position, strategic attack UI, both-flags-required/progress, v24 Save→Load, exact 1920×1080 and clean exit. The capture-inclusive 120-frame sample recorded average 30.746 FPS and minimum 3.109 FPS; synchronous 1920×1080 PNG captures are included in that sample, so it is evidence only and not a P10.9 performance measurement.

Visually inspected evidence:

- `Artifacts/P107/player-final3/p107-training.png`
- `Artifacts/P107/player-final3/p107-attack-order.png`
- `Artifacts/P107/player-final3/p107-capture-progress.png`
- `Artifacts/P107/player-final3/p107-after-load.png`
- `Artifacts/P107/player-final3/p107-movement-frames/frame-000.png` through `frame-100.png` (six-frame sequence visibly shows physical traversal)
- `Artifacts/P107/p107-player-final3.log` confirms Direct3D 11.0 level 11.1 and contains `P107_PLAYER_SMOKE PASS`; it has no crash, null-reference or missing-reference diagnostic. Unity's shutdown audit reports 1,675,041 bytes of immediate labelled native allocations, retained honestly as inherited runtime debt rather than a P10.9 performance pass.

## Preserved debt

P10.3 and P10.4 remain PARTIAL exactly as recorded in D-141/D-143 and their handoffs. P10.2 performance/route-strip debt remains for P10.9. The inherited P8.2 geography failure and full PlayMode cross-test isolation failures remain broad-suite debt rather than P10.7 behavior failures. P8.6 remains PARTIAL, U29 PARTIAL, U30 IN_PROGRESS and U31 BLOCKED.
