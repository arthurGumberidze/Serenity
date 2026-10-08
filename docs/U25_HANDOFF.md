# U25 Handoff — Global Map and Local/Global Mode

## Status

U25 is DONE after the mandatory validation gates. U26 has not started.

## Result

- Added a validated stable-ID world graph, settlement state, route travel and Local/Global mode state.
- Added rollback-safe Local→Global, Global→settlement and Global→existing-battle transitions over U13 tiers, U21 unit representation, U22 direct control and U23 battle mode.
- Added deterministic absolute-tick movement, exactly-once arrivals and hostile co-location handoff to the existing U23 abstract battle.
- Added a separate additive `GlobalMap` scene with schematic routes/locations, aggregate unit/battle markers, pan/zoom and development controls.
- Kept `LocalGameplay` and its composition root alive off-camera so the existing clock, research, production, weather, fire and abstract battle systems continue centrally.
- Added snapshot v12 global-map persistence with v1–v11 compatibility and unchanged opaque PostgreSQL storage.
- Added 12 EditMode and 3 PlayMode U25 tests plus `Tools/Verify-U25.ps1`.

## Validation evidence

- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Verify-U25.ps1 -FullRegression -GpuValidation -BuildPlayer`.
- U25: 12/12 EditMode, 3/3 headless PlayMode and 3/3 Direct3D GPU PlayMode.
- Required U13/U14/U21/U23 regressions: 49/49 EditMode and 9/9 PlayMode.
- Persistence: 35/35. Full suites: 358/358 non-PostgreSQL EditMode and 74/74 PlayMode.
- Windows x64 Mono Development build: succeeded; `Builds/Windows/Serenity.exe` exists; 0 errors and 2 inherited warnings.
- Travel catch-up/hourly-step/reversed-order hash: `05FDEC2FB26A11F9`.
- 100-unit/50-location stress: 0.768 ms, 0 measured current-thread managed bytes, hash `EBCF24AF1D0B3C71`.
- PostgreSQL integration was not run because `DYNASTYGAME_PG_TEST_DATABASE` is absent; the opaque XML version guard and persistence tests cover v12.
- Computer Use initialized but exposed no native-app launcher/backend. No manual clicks are claimed; the equivalent headless and GPU PlayMode paths passed.
- `git diff --check` passed; the staged check is run immediately before commit.

## Known limits

No U26 strategic AI, procedural geography, territory borders, diplomacy, supply/logistics, fog of war, per-location terrain streaming or final U27 UI was added. `LocalGameplay` is the one authored local environment; known building presenters are scoped by the selected settlement, while richer terrain/resource presentation remains future content work.

## Reproduction

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Verify-U25.ps1 -FullRegression -GpuValidation -BuildPlayer
```

The verifier writes exact logs and NUnit XML beneath `Logs/`. Add `-PostgresIntegration` only when `DYNASTYGAME_PG_TEST_DATABASE` points to an isolated test database.

## Boundaries for U26

Reuse `WorldLocationId`, `SettlementId`, `GlobalMapService`, U21 orders/locations and U23 battles. Do not introduce copied Characters, armies, settlements or combat state. U26 may decide strategic actions but must submit them through the canonical U25/U21/U23 APIs. See `docs/GLOBAL_MAP.md` for the authority model and explicit limits.
