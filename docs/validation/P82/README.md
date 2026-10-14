# P8.2 validation evidence

This folder retains compact machine-readable and visual evidence for the P8.2 map rebuild. Full Unity logs remain under ignored `Logs/`; the visible Player log is retained beside its screenshots.

- `P82-focused-editmode.xml`: 3/3 passed.
- `P82-focused-playmode.xml`: 3/3 headless passed.
- `P82-gpu-playmode.xml`: 3/3 Direct3D 11 passed.
- `P82-affected-editmode.xml`: affected P3/P5/production, 20/20 passed.
- `P82-affected-playmode.xml`: affected P3/P5/P7/production, 11/11 passed.
- `player-smoke/p82-player-smoke.json`: visible Player assertions; 24 local sites and 6 global resource families.
- `player-smoke/p82-local-map.png`: final local-map overview.
- `player-smoke/p82-selected-resource.png`: selected gold source with normal Russian inspector/action.
- `player-smoke/p82-global-map.png`: global-map resource projection with selected iron summary/action.
- `player-smoke/Player.log`: visible Player execution log ending in `P82_PLAYER_SMOKE PASSED`.

Exact commands, build result and scope limits are in `docs/P82_HANDOFF.md`.
