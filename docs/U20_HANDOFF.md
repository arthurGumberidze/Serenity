# U20 Handoff — Tactical RTS Combat

## Status

U20 is DONE and committed only after the checks below. U21 has not started. The next task is U21 — Army, Captains and Abstract Units, pending review of this handoff.

## Delivered

- Engine-independent encounter, side, combatant, weapon, attack-record, outcome and save models keyed only by `StableEntityId`.
- Central `CombatService` for eligibility, deterministic target acquisition, melee approach, cooldown attacks, U18 injury/death, morale, retreat and exactly-once completion.
- U17 succession reuse for fatal monarch injuries; repeated fatal applications are no-ops.
- Tier 1 physical ownership with demotion guard and suspension of ordinary centralized Tier 1 AI while combat owns an agent.
- Player right-click Attack through the existing selection/work-group/pointer boundary; terrain right-click Move and UI blocking remain intact.
- Hostile agents use the same deterministic tactical loop; LocalGameplay includes a 4v4 demo and combat diagnostics.
- Snapshot v7 combat persistence with strict v1-v6 compatibility and opaque PostgreSQL storage.
- Timeout-aware `Tools/Verify-U20.ps1`, 17 focused EditMode tests and 3 focused physical PlayMode tests.

See `docs/TACTICAL_COMBAT.md` for the rules and authority boundaries.

## Validation evidence (2026-10-08)

Final command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\serenity_game\Tools\Verify-U20.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -GpuValidation -BuildPlayer -StepTimeoutSeconds 900
```

Final result:

- compile: passed;
- U20 EditMode: 17 passed, 0 failed, 0 skipped;
- U20 headless PlayMode: 3 passed, 0 failed, 0 skipped;
- required U05/U06/U10/U11/U13/U14/U17/U18/U19 EditMode regressions: 123 passed;
- required U05/U10/U11/U13/U14/U17/U18/U19 PlayMode regressions: 25 passed;
- persistence: 35 passed;
- full non-PostgreSQL EditMode: 299 passed;
- full PlayMode: 51 passed;
- U20 GPU PlayMode: 3 passed on Direct3D 11.0 / Intel Arc Graphics;
- Windows x64 Mono Development: succeeded, 0 errors, 2 inherited warnings;
- PostgreSQL integration: not run because `DYNASTYGAME_PG_TEST_DATABASE` is absent; v7 remains opaque XML and required no schema migration.

Stress/determinism evidence:

- 50v50 / 100 combatants: 203.162 ms, 0 measured current-thread managed bytes, 250 attacks, hash `BC4B733A3E3410B1`;
- equal logical battles created with reversed registration/input order produced identical combat and health hashes;
- active battle v7 round-trip preserved target/cooldown/history and produced no same-tick duplicate attack or injury;
- fatality + finished outcome v7 round-trip preserved the death/outcome/history and rejected repeated fatal resolution;
- actual 4v4 NavMesh PlayMode battle reached a terminal outcome.

The first two sandboxed final-gate attempts crashed inside Unity's `BuildReportRestService`/Mono socket initialization before compilation. Re-running the same command outside that restricted sandbox exposed one test-only API typo, which was fixed; the final complete command above then passed. This was an editor-host issue, not a gameplay failure.

Computer Use was initialized twice (including one reset) for interactive manual validation, but its Windows helper exited before exposing any app window with `windows sandbox failed: helper_unknown_error: setup refresh had errors`. No manual clicks are claimed. The GPU PlayMode validation above is the visual/runtime evidence.

## Known limitations / deferred work

- U21 persistent armies/captains and U23 aggregate/off-camera battle are not implemented.
- U20 is two-side local Tier 1 melee with deterministic always-hit resolution.
- No projectiles, friendly fire, cover/elevation, complex flanking, deep armour/shields, stamina, durability, loot, prisoners, executions or building damage/fire/capture.
- Approach slots are fixed tactical spacing; there is no formation editor.
- Weapon profiles are logical and data-driven, but dedicated held-weapon visuals are deferred.
- The debug overlay is not the final U27 gameplay UI.

## Review starting points

- `Assets/Game/Domain/Combat/CombatModels.cs`
- `Assets/Game/Simulation/Combat/CombatService.cs`
- `Assets/Game/Presentation/Combat/Tier1CombatPositionProvider.cs`
- `Assets/Game/Presentation/Combat/CombatDebugOverlay.cs`
- `Assets/Game/Tests/Editor/TacticalCombatTests.cs`
- `Assets/Game/Tests/PlayMode/TacticalCombatPlayModeTests.cs`
- `Tools/Verify-U20.ps1`
