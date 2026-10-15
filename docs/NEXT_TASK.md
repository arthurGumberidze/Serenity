# NEXT_TASK.md

Active task: **P9.6 — Интеграция и визуальная приёмка (PARTIAL)**.

Completed technical checkpoint (2026-10-15): the ordinary Player route Main Menu → New Game/edited UMA ruler → local RTS/TPS → six-location global map → return local → finite-resource gather/haul/build → rain/snow/seasons/day-night → Save/Load passes at exact 1280×720 and 1600×900. Focused P9.6 PlayMode passed 2/2, affected P8.4/P9.3/P9.4/P9.5 passed 12/12 EditMode and 10/10 PlayMode, and the Windows Development build succeeded with errors=0/warnings=41. See `docs/P96_HANDOFF.md` and `Logs/P96-player-smoke-20261015-final`.

Remaining scope before P9.6 may become DONE:

1. Obtain explicit approval for a skeleton-compatible realistic Stone Age UMA wardrobe and a coherent mass-NPC/LOD Character set, or obtain explicit acceptance that the modern UMA demo clothing and low-poly Hodaart fallbacks remain placeholders.
2. Replace or explicitly accept the visible centralized aggregate crowd diagnostic capsule without moving Character authority out of the existing tier/simulation boundary.
3. Correct and visibly recheck the transient UI Toolkit text clipping reproduced at 1600×900 after TPS/Load, or obtain explicit acceptance of the defect.
4. Re-run only proportional affected tests, the Windows Development build and the two visible Player sizes needed to prove those changes. Preserve `WeatherService`, `GameClock`, `GlobalMapService`, stable IDs, finite resources, work/building authority and snapshot formats.
5. Do not import an external asset/dependency without explicit approval. Do not run U29/U30/U31 or reinterpret P8.6; their statuses remain PARTIAL / IN_PROGRESS / BLOCKED.
