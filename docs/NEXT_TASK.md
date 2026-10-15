# NEXT_TASK.md

Active task: **P9.6 — Интеграция и визуальная приёмка**.

Starting point: P9.5 unified local daylight/shadows/fog/water/rain/snow/seasons/day-night and rebuilt the additive six-location global map as disposable Presentation driven by the same project-owned `P95NaturalAtmosphereProfile`. Read `docs/P95_HANDOFF.md` before changing local atmosphere, global-map framing or evidence automation.

Required scope:

1. Exercise the ordinary player route Main Menu → New Game → local RTS → local TPS → global map → return to local → resources/work/construction → weather/seasons/day-night → Save/Load.
2. Produce comparable normal-Player evidence at 1280×720 and at least one higher resolution. Inspect rather than merely assert screenshots, including local RTS/TPS and all six labelled global locations.
3. Fix only integration/readability defects found by that route. Preserve `WeatherService`, `GameClock`, `GlobalMapService`, stable IDs, finite resources, work/building authority, snapshot formats and the replaceable P9.2–P9.5 presentation boundary.
4. Keep the P9.4 caveat explicit: static wrapper-root clothing was rejected; the existing lightweight aggregate crowd draw may still appear in RTS evidence. Do not describe either as solved unless P9.6 actually changes and verifies it.
5. Run proportional focused/affected tests, a Windows Development build and visible normal-Player smoke. No new external asset/dependency without explicit approval.
6. Update project state, task graph, decisions, this pointer and `docs/P96_HANDOFF.md`; commit only P9.6 work locally without push.
7. Do not run U29/U30/U31 or reinterpret P8.6. Their statuses remain PARTIAL / IN_PROGRESS / BLOCKED.
