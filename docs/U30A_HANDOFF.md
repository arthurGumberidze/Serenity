# U30A HANDOFF — pre-MVP FRS compliance closure

## Status and sequencing boundary

U30A is DONE on 2026-10-12 under the user's explicit sequencing exception while U30 external work remains pending. This does not waive dependencies: U29 remains PARTIAL, U30 remains IN_PROGRESS until an actual eight-hour result is independently audited, and U31 final completion remains BLOCKED. U32 has not started. The stale old run2 Player process was stopped at the user's direction; run3, run4 and preflight evidence were not modified.

## Implemented player path

- Normal New Game constructs exactly ten deterministic player Characters: monarch, spouse, two valid heirs and six additional residents. It also composes the existing independent six-Character AI state. Seed/config-derived stable IDs cover the session, Characters, dynasties, settlements, starting buildings and starting resource piles.
- The U27 Save panel exposes New Game with confirmation and retains the normal three-slot Save/Load facade. Repeated New Game scene reloads leave one composition root and one gameplay UI without duplicated subscriptions.
- `CampaignProgressService` persists exactly-once rival discovery and victory audit facts. Rival detail is hidden until player contact with the rival settlement. Discovery survives Save/Load.
- U26 remains AI strategy authority. It funds U09 placement through its owner-scoped U15 inventory/production state, creates an army and uses the existing U25/U23/U24 capture path.
- U21 supply uses berries from canonical inventories. Resupply is atomic and conserved; travel and combat consume at stable checkpoints; shortage modifies U25 travel and U20 combat/morale. Representation changes and Save/Load cannot refill supply.
- TPS left-click is a press action routed through `CombatService.TryManualAttack`. U20 owns range, cooldown, weapon, damage, injury, morale and outcome. Pause and cooldown reject attacks, and one press produces at most one attack.
- Player victory is recorded only after a finished battle causes canonical rival settlement capture and the shared no-settlement defeat rule succeeds. A victory banner is shown, while time controls, continued play and Save/Load remain available.

## Persistence and authority changes

Snapshot version is 15. `CampaignProgressSaveState` and `MilitarySupplyState` are strict XML sections captured by the shared `SaveCoordinator`. V1-v14 snapshots load with empty campaign and uninitialized supply defaults. PostgreSQL requires no schema change because it stores the XML opaquely. U30 resume now supplies the campaign service to the v15 coordinator.

No parallel Character, resource, building, AI, battle, health or global-map authority was added. The new campaign service stores only progression facts; all world mutations remain in the earlier owning systems.

## Validation evidence

Final aggregate command:

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools\Verify-U30A.ps1 -ProjectPath C:\serenity_game -StepTimeoutSeconds 1200`

Results:

- compile: PASSED, Unity batchmode exit 0;
- focused U30A EditMode: 8 passed, 0 failed, 0 skipped;
- focused U30A headless PlayMode: 5 passed, 0 failed, 0 skipped;
- critical U04/U09/U17/U20-U28 EditMode regressions: 133 passed;
- critical U04/U09/U17/U20-U28 PlayMode regressions: 44 passed;
- persistence/legacy: 35 passed;
- GPU U30A PlayMode: 5 passed;
- Windows x64 Mono Development build: PASSED at `C:\serenity_game\Builds\U30A\Serenity.exe` (667,136 bytes);
- PostgreSQL: `SKIPPED_NOT_CONFIGURED`; `DYNASTYGAME_PG_TEST_DATABASE` was absent;
- final fresh short soak: PASSED at `C:\serenity_u30a_short_soak_20261012_103419\soak-result.json`, 30 requested/39 actual seconds, 0 critical/invariant/watchdog/recoverable incidents, 2 Player restarts, 3 completed segments, verified checkpoint resume and memory metrics, 1,642 frame samples, worst p95 16.936161 ms, final logical hash `C5FB112880CD4E99E34147356F5FE3107F03697AA986D41A1DD763DC6248E199`.

Because U30 resume composition changed, the additional command `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools\Verify-U30.ps1 -ProjectPath C:\serenity_game -StepTimeoutSeconds 1200 -SkipU29Regression` passed all seven PowerShell runner protocol fixtures, U30 EditMode 16/16, U30 PlayMode 7/7 and the Windows build.

An earlier short-soak attempt at `C:\serenity_u30a_short_soak_20261012_100013\soak-result.json` failed. It exposed both a four-second startup watchdog that was shorter than real Player startup and the missing campaign binding in the snapshot-v15 U30 resume coordinator. The verifier now uses a ten-second startup watchdog and resume binds the canonical campaign service. The passing fresh run supersedes this diagnostic failure; the failed artifact was not rewritten.

One subsequent verifier launch inside the restricted process/network sandbox crashed in Unity's internal `BuildReportRestService` before project compilation while opening its `HttpListener`. No project test had run. Repeating the identical verifier outside that restriction produced the complete passing evidence above.

Final verifier logs are under `C:\serenity_game\Logs\U30A-*.log` and test XML under `C:\serenity_game\Logs\U30A-*.xml`; these generated artifacts remain ignored. The isolated build is also ignored and is not committed.

## Known gates and next action

U30A has no open implementation defect in its scoped acceptance evidence. It does not prove the U29 performance gate or the U30 full-duration reliability gate. Next, U31 may prepare its end-to-end normal player integration path, but cannot be marked DONE until U29 and U30 are separately completed with qualifying evidence.
