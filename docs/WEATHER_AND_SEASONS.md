# Weather and Seasons

## Authority and calendar

`WeatherService` is the canonical weather query. It accepts the existing U03 absolute calendar ticks and never reads wall time, Unity random state, camera, tier or frame count. `SeasonCalendar` maps a 365-day year as follows:

| Season | Inclusive days |
|---|---:|
| Spring | 1-91 |
| Summer | 92-182 |
| Autumn | 183-273 |
| Winter | 274-365 |

The starts are configurable and validated; Spring must begin on day 1. Season is derived, not saved as mutable state.

## Temperate climate and generation

The `temperate` profile contains all four seasonal records and all eight weather effects: Clear, Cloudy, Rain, HeavyRain, Snow, Windy, ColdSnap and HeatWave. Seasonal configuration supplies base deci-Celsius temperature, symmetric daily variation, weather weights, crop rate and wild-food rate. Weather effects supply temperature anomaly, precipitation/wind ranges and crop/work/movement rates.

Each sample uses U14 `DeterministicKeyedRandom` with world seed, a stable hash of `temperate`, absolute day and a dedicated stream for type, temperature, precipitation, wind or health exposure. There is no shared RNG cursor. Temperature is integer deci-Celsius. Rain becomes snow at or below 0.0 C; configured snow becomes rain above 0.0 C. Severe cold begins at -10.0 C and severe heat at 35.0 C. No snow accumulation, soil moisture, disasters, biome simulation or daily history exists.

## Modifier rules

All rates are integer per-thousand values. Crop progress is `base fixed-step × seasonal crop rate × weather crop rate`. Outdoor work is `raw time × character capability × weather work rate`, with a seasonal wild-food rate for berry/fiber gathering. Aggregate completion converts required effective work back to raw elapsed time so slowdown cannot be cancelled by the daily worker budget.

Temperate seasonal crop/wild-food rates are Spring 1000/1000, Summer 1100/1150, Autumn 800/850 and Winter 0/300. Rain crop/work/movement rates are 1100/920/950; HeavyRain 950/850/900; Snow 0/750/750; Windy 1000/850/900; ColdSnap 700/800/900; HeatWave 800/850/950. Clear and Cloudy are 1000 baselines.

Winter or snow stops new crop growth. It does not kill crops, reset plots, invalidate a Ready harvest or remove stored food. Natural sources remain finite; U19 changes gathering rate, not ecology or regeneration. Recipes classify current gathering/crop work as Outdoor; the metadata supports future Sheltered work without a second production system.

## Health, tiers and presentation

Severe outdoor cold/heat uses a deterministic per-character/day risk and can apply U18 `cold_stress` or `heat_stress`. Active matching conditions are not stacked. Per-character processed-day markers make repeated calls and load/catch-up idempotent. Current outdoor eligibility is active outdoor production workers plus Tier2/Tier3 aggregate workers; reliable shelter/clothing state does not yet exist.

Tier 1 receives movement rate through the existing NavMesh driver and production through the existing task authority. Tier 2 applies movement in one Burst/ECS batch and crosses its existing production barrier. Tier 3 supplies stable IDs to the same daily production and exposure services. A 365-day catch-up equals daily stepping, and tested T3-only, T3-T2-T3 and T3-T1-T3 routes converge.

Presentation refreshes fog, ambient light and the directional light at most once per game day. The debug panel displays date, season, type, temperature, precipitation, wind, crop/work/movement/wild-food rates, health exposure and a deterministic next-day preview. It registers with the existing world-pointer blocker. There is no saved or canonical forced-weather override.

## Persistence and limits

Snapshot v6 stores climate profile ID, weather world seed and sorted `(character ID, last processed exposure day)` markers. Daily states and seasons are regenerated, so the same seed/day/profile loads identically. V1-V5 load without a weather payload. PostgreSQL stores the same strict opaque XML; no relational migration was added.

Out of scope: physical climate, accumulation, mud, floods, storms/disasters, fires, ecology/regrowth, fertility/irrigation, shelters/heating/clothing, complex thermoregulation, forecasts and player-facing production UI.
