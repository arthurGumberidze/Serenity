# P6 handoff — understandable first hour

## Outcome

P6 is DONE at its implementation/evidence boundary. A normal new game presents an optional Russian seven-step route: inspect the group → assign work → understand shelter/storage → produce food → begin research → resolve an event/danger → start scouting. The card is visible by default, can be hidden/restored, and reports the current blocker plus a useful next action. Final usability acceptance by a fresh human player remains open and is not replaced by automated tests.

## Canonical integration

- `FirstHourOnboardingProgress` stores only disposable in-session Presentation observations. It is absent from snapshots and user settings.
- Completion comes from canonical facts: Work jobs, operational Building/Inventory state, newly produced food, Research state, Event/Health/Battle evidence and player Military/GlobalMap travel/contact.
- Guide actions navigate existing panels. Gameplay mutations remain in Work, Production, Research, Event, Military, GlobalMap and Save services.
- The settlement panel shows ruler, dynamic player-only population (10 at start), shelter/storage readiness, approximate berry days and the starting group. Character cards show Tier1 hunger, energy, utility action and phase.
- Work, research, military and map actions disable early and display a concrete recovery instruction rather than surfacing an exception.
- Save/Load proof retains the ruler `StableEntityId`, P4 appearance profile, P5 finite resource-site identity/remaining amount and the active scouting travel order.

## Verification evidence

Unity `6000.6.4f1` was used. The maintained entry point is:

```powershell
& C:\serenity_game\Tools\Verify-P6.ps1 -ProjectPath C:\serenity_game -PlayerSmokeOutput C:\serenity_game\Logs\P6-player-smoke-final4
```

Results:

- Focused EditMode: 3 passed, 0 failed/skipped (`Logs/P6-focused-editmode.xml`).
- Focused headless PlayMode: 2 passed, 0 failed/skipped (`Logs/P6-focused-playmode.xml`).
- D3D11 PlayMode: 2 passed, 0 failed/skipped (`Logs/P6-gpu-playmode.xml`).
- Affected EditMode: 20 passed, 0 failed/skipped (`Logs/P6-affected-editmode.xml`).
- Affected PlayMode: 33 passed, 0 failed/skipped (`Logs/P6-final-affected-play.xml`, final rerun after the deterministic haul fixture update).
- Final P6 + Gameplay UI recheck after player-only population projection: 8 passed, 0 failed/skipped (`Logs/P6-final-play2.xml`).
- Windows x64 Mono Development: `Builds/P6/Serenity.exe`; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=9` (`Logs/P6-final-build2.log`). Warnings are inherited obsolete diagnostic/test APIs.
- Visible 1280×720 Player: `passed=true` in `Logs/P6-player-smoke-final4/p6-player-smoke.json`; every route flag and Save/Load identity flag is true.
- Visually inspected at original resolution: `p6-onboarding-start.png`, `p6-settlement-food.png`, `p6-event-result.png`, `p6-scouting-map.png`, `p6-after-load.png` in the same evidence directory. The final settlement/HUD frame shows 10 player residents, not the 16 world Characters that include the AI state.

The affected PlayMode runs found three stale assumptions from the P5 expansion: exactly four production sites, every formation member travelling more than two metres even when already near a slot, and a shared resource pile remaining untouched while the expanded autonomous population was active. The tests now assert the real invariants (at least the legacy sources; completed formation with meaningful group movement and every member arriving near the target; an exact two-unit manual-haul fixture reserved from background workers), and the final affected suite passed 33/33. One early route test also exposed a paused clock before the event stage; the test now explicitly uses the existing resume/pause commands.

On this host Unity twice lingered with build workers after logging `U00_BUILD_RESULT: Succeeded; errors=0`. The verifier now waits 30 seconds after that explicit success marker, then terminates only its own exact Editor PID and continues; this does not treat a partial build as success. The successful Player was executed and inspected directly. A Computer Use attachment identified the real `Serenity` window, but OS/app approval timed out before a second screenshot; this is not counted as acceptance evidence.

## Likely first-player confusion and open acceptance

- The authored starting camp already contains a shelter and storage basket. P6 explains this rather than forcing duplicate construction; a fresh player may still expect to place both because the high-level route says “build”.
- Only two residents have active Tier1 presenters at the start; the settlement list shows all 10 player residents and intentionally excludes the six rival residents, while selecting an off-camera resident directly is not yet a normal world action.
- Food “days” is an estimate from canonical consumption settings and current population, not a promise under births, supply loading or future balance changes.
- Scouting currently requires creating a military unit and switching it to aggregate mode before map travel. The guide explains the steps, but the distinction between a work group and military unit still needs human validation.
- The global map remains the established schematic P5 projection, and the P3/P5 art remains provisional.

Give the build to a player with no debug knowledge and record where they hesitate, misread consequences or cannot find an action. P6 automation proves commands and explanations are connected, not that the UX has been accepted.

## Next task and deferred gates

P7 is next: connect and balance the full Stone Age route, including the approved symmetric undefended-settlement occupation mechanic, succession, war, victory/defeat and continued play. U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. No P8 performance matrix or eight-hour run was started.
