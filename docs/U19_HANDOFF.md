# U19 HANDOFF Weather and Seasons

## Summary

U19 is complete. It adds canonical four-season derivation, deterministic daily temperate weather, fixed-point temperature/rain/snow/wind, data-driven gameplay modifiers, weather-aware farming/outdoor production/movement, exactly-once U18 cold/heat exposure, Tier1/2/3 catch-up equivalence, snapshot v6 and a click-blocked diagnostic/environment projection. U20 was not started.

## Determinism and persistence

Daily state is keyed by world seed, climate-profile ID, absolute day and named U14 streams; no RNG cursor, camera or tier is involved. Snapshot v6 stores only the profile/seed and per-character processed exposure days. Daily history is derived. V1-V5 remain readable. PostgreSQL needs no schema migration; the optional suite was unavailable because `DYNASTYGAME_PG_TEST_DATABASE` was absent.

Final deterministic observations:

- 10,000 daily queries: 0 measured managed bytes, hash `000000000000003D`.
- 20 people over five years, repeated twice in the final focused run: hash `4CF6296D268E48AA`, final food 4000, shortage 1, 6.554 seconds total.
- 365-day one-shot catch-up equals day-by-day; tested tier routes and reversed worker collection order converge.

## Validation

- Compile: passed.
- U19 EditMode: 19 passed, 0 failed, 0 skipped.
- U19 headless PlayMode: 2 passed, 0 failed, 0 skipped.
- Full non-PostgreSQL EditMode: 282 passed, 0 failed, 0 skipped.
- Full PlayMode: 48 passed, 0 failed, 0 skipped.
- Required U03/U14/U15/U16A/U18 EditMode: 44 passed.
- Required U03/U14/U15/U16A/U18 PlayMode: 10 passed.
- Persistence: 35 passed; dedicated U19 compatibility loads v1-v5.
- Graphics-enabled U19 PlayMode: 2 passed on Direct3D 11 / Intel Arc Graphics.
- Windows x64 Mono Development: succeeded with 0 errors and 2 inherited warnings from obsolete `FindObjectsByType` overloads in older research tests. U19 runtime sources add no compiler warning.

Primary timeout-aware command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\serenity_game\Tools\Verify-U19.ps1 -FullRegression -GpuValidation -BuildPlayer
```

Add `-PostgresIntegration` only with a disposable configured test database.

## Commands and artifacts

The verifier runs Unity 6000.6.4f1 at `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe` with project `C:\serenity_game`. It emits `Logs/U19-compile.log`, focused/regression/full XML and logs, `Logs/U19-playmode-gpu.xml`, `Logs/U19-build.log`, and `Builds/Windows/Serenity.exe`. Test commands intentionally omit `-quit`; compile/build commands use `-quit`.

## Known limitations

The temperate profile is the only current climate. Weather is an independent keyed daily draw rather than a continuity model. There is no physical accumulation, moisture, disasters, ecology, regrowth, shelter/clothing/heating or thermoregulation. Health exposure covers active outdoor production and aggregate workers because no canonical indoor/outdoor location state exists. Lighting/fog is deliberately lightweight and no new rain/snow assets were added. The next-day preview is debug-only.

## Next task

U20 — Tactical RTS Combat is next and remains not started pending review.
