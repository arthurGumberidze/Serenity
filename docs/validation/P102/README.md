# P10.2 validation evidence

Final P10.2 evidence captured on 2026-10-16 with Unity 6000.6.4f1.

- `P102-focused-editmode.xml`: 4/4 passed.
- `P102-focused-playmode.xml`: 4/4 passed.
- `P102-affected-editmode.xml`: 63 passed, 0 failed, 6 existing ignored cases.
- `P102-affected-playmode.xml`: 32/32 passed.
- `build-provenance.json`: final Windows x64 Development build GUID and immutable source fingerprint.
- `player-1920x1080/p102-player-smoke.json`: exact normal-Player route flags, geometry counts and actual frame-time percentiles.
- `player-1920x1080/Player.log`: complete clean Player log.
- `player-1920x1080/*.png`: visually inspected local, same-world strategic and after-Load construction frames.

The Player telemetry is intentionally honest: `passed=true` refers to the P10.2 connected-world route, while the recorded p95 exceeds 33.33 ms. P10.9 owns that feature-complete performance gate. Exact commands and interpretation are in `docs/P102_HANDOFF.md`.
