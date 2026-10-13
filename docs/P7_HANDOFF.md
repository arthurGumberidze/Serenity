# P7_HANDOFF.md

## Outcome

P7 completes the connected Stone Age campaign at its implementation/evidence boundary. The shipped path now connects the existing settlement economy, construction, research, weather, health/family, dynasty/succession, scouting, supplied military, RTS/TPS battle, capture, victory/defeat and continued-play authorities without adding a second gameplay state.

The new rule is symmetric. A hostile combat-capable aggregate unit at an undefended settlement begins a canonical 24-game-hour occupation. A combat-capable aggregate defender requires a real U23 battle. Defender/contact processing wins same-tick ordering over completion. Interruption fully resets progress; claimant loss/movement/engagement, competing hostile sides and ownership changes also interrupt. Completion changes settlement and capturable-building ownership exactly once, then applies the same no-controlled-settlement defeat rule to player and AI.

## Persistence and presentation

Snapshot v17 adds active occupation state, a bounded occupation event audit and optional campaign `SourceOccupationId`. The claimant's stable unit/side/location are validated on restore. Legacy v1-v16 remain readable, and PostgreSQL remains an opaque XML payload store.

The Russian gameplay UI shows occupation progress and remaining time even from local mode, explains the defender/reset rule, publishes start/interruption/completion notifications, and distinguishes last-settlement defeat from dynasty extinction. Dynasty presentation now shows child ages/minor-adult state, active pregnancies and adulthood notification. Victory remains a persistent banner while simulation can continue; loading a pre-defeat save returns to a running game.

AI retains its own economy/dynasty/units and now records an explicit `Occupy` intent while `GlobalMapService` remains the only occupation clock/authority. Undefended arrival never fabricates a battle.

## Verification

Final verifier command:

```powershell
& .\Tools\Verify-P7.ps1 -ProjectPath 'C:\serenity_game' -StepTimeoutSeconds 1200 -PlayerSmokeTimeoutSeconds 240 -PlayerSmokeOutput 'C:\serenity_game\Logs\P7-player-smoke-final'
```

Results:

- P7 focused EditMode: 5 passed, 0 failed/skipped.
- P7 focused headless PlayMode: 2 passed, 0 failed/skipped.
- P7 D3D11 PlayMode: 2 passed, 0 failed/skipped.
- affected strategic/persistence/localization/dynasty/health EditMode: 90 passed, 0 failed/skipped.
- affected Gameplay UI/U30A/dynasty/health PlayMode: 15 passed, 0 failed/skipped.
- `U00Build.WindowsDevelopment -serenityBuildPath Builds/P7/Serenity.exe`: succeeded, errors=0, warnings=9 (existing obsolete instrumentation/diagnostic APIs).
- visible 1280×720 Windows Player smoke: passed; all eight scenario flags are true, Player.log contains no exception/error diagnostic, and all six PNGs were visually inspected.

The visible route uses the normal Main Menu/New Game composition and canonical services. It covers a defended settlement producing an actual battle; undefended occupation progress; Save/Load midway; defender interruption; occupation victory exactly once plus continued time; last-settlement defeat; and loading the pre-defeat save. `p7-player-smoke.json` reports `passed=true`; the inspected images are `p7-defended-battle.png`, `p7-occupation-progress.png`, `p7-occupation-interrupted.png`, `p7-victory-continues.png`, `p7-defeat.png` and `p7-recovered-after-load.png`. Evidence directory: `Logs/P7-player-smoke-final`.

An earlier compile/regression pass also ran:

```powershell
& .\Tools\Verify-U23.ps1 -ProjectPath 'C:\serenity_game' -StepTimeoutSeconds 600
```

It passed U23 EditMode 12/12, headless PlayMode 3/3, required editor regressions 88/88, required PlayMode regressions 25/25 and persistence 35/35 before the final P7-specific additions. The first sandboxed Unity attempt crashed in Unity's local report server; rerunning outside the sandbox was required and is not treated as code evidence.

## Known limitations / P8 boundary

- P3/P5 art and P6 first-hour usability remain provisional until the user's manual acceptance. P7's automated campaign route does not decide long-session balance or taste.
- The Stone Age occupation duration/reset thresholds are explicit data but have one implemented policy. Retained/partial progress and resident militia are intentionally not implemented.
- Multi-side contesting blocks occupation and uses existing pairwise battle behavior; richer alliances/sieges are future scope.
- Snapshot v17 is forward-only for current saves; no downgrade writer is provided.
- U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. No P8 matrix or new eight-hour run was launched during P7.

## Next task

P8: resolve and run the deferred performance/endurance/integration qualification, then conduct the separate visual/usability/balance acceptance. See `docs/NEXT_TASK.md`.
