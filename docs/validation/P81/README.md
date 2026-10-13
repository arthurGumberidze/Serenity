# P8.1 validation evidence

This folder retains the compact machine-readable evidence for the P8.1 visual prototype. Full Unity and Player logs remain under ignored `Logs/`.

- `P81-focused-editmode.xml`: 4/4 passed.
- `P81-focused-playmode.xml`: 1/1 headless passed.
- `P81-gpu-playmode.xml`: 1/1 Direct3D 11 passed.
- `P81-affected-editmode.xml`: affected P3/P4/P5, 11/11 passed.
- `P81-affected-playmode.xml`: affected P3/P4/P5, 8/8 passed.
- `p81-player-smoke.json`: visible Windows Player assertions, including the required `uma3` provider.
- `p81-visual-prototype.png`: final 1280×720 Player frame used for visual inspection.

Exact commands, build result and acceptance boundaries are in `docs/P81_HANDOFF.md`.
