# U26 Handoff — Strategic AI of One State

## Status

U26 is DONE after all mandatory gates. U27 has not started.

## Result

- Added a deterministic normal-runtime rival on the existing U25 enemy settlement: six canonical Tier3 Characters, dynasty/ruler, finite resources/sites and independent research/production.
- Added engine-independent strategic state, stable decisions/orders/history and a centralized multi-state scheduler.
- Reused U16 research, U09 funded placement, U15 target-stock production, U21 units/captains, U25 travel, U23 Abstract battle and U24 capture.
- Added deterministic AI building/unit identities, worker presence filtering, loss/disband/replacement, succession and no-settlement defeat.
- Generalized U18 death routing to multiple dynasty succession authorities and U24 destruction/capture to registered owner economy scopes.
- Made settlement building capture all-or-nothing and exposed the same player/AI capture entry point.
- Added snapshot v13 with v1–v12 compatibility, normal composition wiring and an inspect/advance-only debug overlay.
- Added 10 EditMode and 2 PlayMode U26 tests plus `Tools/Verify-U26.ps1`.

## Validation evidence

- Command: `& .\Tools\Verify-U26.ps1 -FullRegression -BuildPlayer`.
- U26: 10/10 EditMode and 2/2 headless PlayMode.
- Required U12/U13/U14/U15/U16/U17/U18/U19/U21/U23/U24/U25 regressions: 160/160 EditMode and 31/31 PlayMode.
- Persistence: 35/35. Full suites: 368/368 non-PostgreSQL EditMode and 76/76 PlayMode.
- Windows x64 Mono Development build: succeeded; `Builds/Windows/Serenity.exe` exists; 0 errors and 2 inherited warnings.
- The configured engine-independent baseline completed research → funded building → army/captain → multi-leg travel → U23 battle → U24/U25 capture without direct final-state mutation.
- PostgreSQL integration and GPU-only PlayMode were not run; neither is a mandatory U26 gate in the active prompt.

## Known limits

The rival is configured and autonomous, but discovery/fog, equipment supply, diplomacy, trade, doctrines, final victory flow and production UI are out of U26 scope. U30A owns the documented discovery/supply/new-game compliance patch. U27 owns final UI; do not replace this scheduler or its scoped canonical services there.

## Reproduction

From the repository root:

```powershell
& .\Tools\Verify-U26.ps1 -FullRegression -BuildPlayer
```

Logs and NUnit XML are written beneath `Logs/`. Add `-PostgresIntegration` only with an isolated `DYNASTYGAME_PG_TEST_DATABASE`; add `-GpuValidation` when an interactive graphics session is available.

## Boundaries for U27

Treat `AiStrategicScheduler` and `AiFactionRuntime` as Simulation read/command boundaries. UI may display state and submit existing commands but must not own goals, resources, research, armies, settlement ownership or outcomes. Start from `docs/STRATEGIC_AI.md` and the existing per-system design documents.
