# P8.3 validation evidence

- `P83-focused-editmode.xml`: 6/6 passed.
- `P83-focused-playmode.xml`: 3/3 passed.
- `P83-gpu-playmode.xml`: 3/3 passed with D3D11.
- `P83-affected-editmode.xml`: 12/12 passed.
- `P83-affected-playmode.xml`: 12/12 passed.
- `player-smoke/p83-player-smoke.json`: all eight assertions passed.
- `player-smoke/p83-local-work.png`: local selected iron source, nine role groups, action and three priorities.
- `player-smoke/p83-global-work.png`: global selected berry marker, canonical action and three priorities.
- `player-smoke/Player.log`: visible Player log containing `P83_PLAYER_SMOKE PASSED` and no runtime exception.

Full commands, build result and the supplementary legacy-test limitation are recorded in `docs/P83_HANDOFF.md`.
