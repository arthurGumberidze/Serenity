# U27 Handoff — Gameplay UI

## Status

U27 is DONE after all mandatory milestone gates. U28 has not started.

## Result

- Added one UI Toolkit player interface over the existing canonical services, with a scaled HUD, navigation, contextual panels, selection inspector, notification feed, modal load confirmation and runtime theme.
- Exposed time, settlement economy, research, work groups/jobs, building placement, dynasty/health, military/combat, TPS, global travel/location/battle and Save/Load without legacy debug menus.
- Kept all legacy debug surfaces hidden by default behind one F10 toggle; Stable IDs and force/debug commands are not player-facing.
- Added local slot metadata/corrupt-slot handling around the existing `SaveCoordinator`/`FileSaveStore` and explicit post-load clock/UI/global/Tier3 rebinding without duplicate subscriptions.
- Integrated all player UI regions with the existing pointer blocker and suppressed U27 gameplay hotkeys while a text field owns focus.
- Added 3 EditMode and 6 PlayMode U27 tests plus `Tools/Verify-U27.ps1`.

## Validation evidence

Final command:

```powershell
& .\Tools\Verify-U27.ps1 -ProjectPath C:\serenity_game -FullRegression -GpuValidation -BuildPlayer
```

- U27: 3/3 EditMode, 6/6 headless PlayMode and 6/6 Direct3D GPU PlayMode.
- Dependency regressions: 170/170 EditMode and 33/33 PlayMode.
- Persistence: 35/35.
- Full suites: 371/371 non-PostgreSQL EditMode and 82/82 PlayMode.
- Windows x64 Mono Development build: succeeded, errors=0, two inherited BuildReport warnings; `Builds/Windows/Serenity.exe` exists.
- `git diff --check`: passed with line-ending notices only.

## Player flow coverage

The U27 PlayMode suite boots a single UI with debug hidden, validates HUD/selection/locked reasons/blocking, routes time/research/work/build/military/TPS commands through player controls, performs Local→Global→travel→arrival→Local, and performs local slot save→time mutation→load with the same UI and exactly one subscription set.

## Known limits

Final art, localization, controller/gamepad support and a richer global-map presentation are not part of U27. Notifications intentionally adapt current canonical histories rather than implementing U28. The existing save schema still defines which gameplay authorities are durable; U27 adds access, metadata and safe rebinding, not a parallel snapshot format. U30A remains responsible for the documented pre-U31 new-game/discovery/supply/TPS-combat compliance patch.

## Reproduction

Run the final command above from the repository root. Logs and NUnit XML are written under `Logs/`.

## Boundary for U28

Start from `docs/GAMEPLAY_UI.md` and keep U27 notifications as a consumer. U28 may add canonical event definitions/chains/cooldowns, but must not move gameplay authority into UI or replace the existing notification adapter until its own migration is explicit and tested.
