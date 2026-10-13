# P3 handoff — reference camp and art production

## Outcome

P3 implementation is complete. `LocalGameplay` now contains a generated/idempotent shore-camp reference using only approved/local content. Characters and buildings are the normal canonical objects; P3 adds disposable visual/audio treatment and validation, not parallel gameplay state. The next active task is P4. User visual acceptance remains open, and the explicit placeholder gaps are recorded in `P3_ART_DIRECTION.md`.

## Main implementation

- `P3ReferenceSceneBuilder` creates/reuses the P3 meshes and URP materials and authors `LocalGameplay.unity` with 18 trees, 10 rocks, 52 grass clusters, shore, water, campfire, lighting and sky.
- `StoneAgeReferenceCampPresentation` attaches approved tools, building surface materials, masked fire/smoke, fire light and procedural spatial ambience to canonical presenters.
- `WeatherPresentationController` adds clear/rain presentation and procedural rain audio without mutating canonical weather. Serialized URP materials avoid Player shader-stripping failures found during smoke validation.
- The normal composition creates deterministic canonical shelter/storage objects and exposes camera state without adding a Cinemachine dependency to Infrastructure.
- `P3PlayerSmokeController` starts only under `-p3Smoke`, uses the normal Main Menu/New Game path, validates canonical links/StableEntityId and writes three screenshots plus JSON.
- `Tools/Verify-P3.ps1` regenerates the scene, runs focused/GPU/affected tests, builds Windows and runs the visible Player smoke with isolated save/settings paths.

## Final verification

Command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\serenity_game\Tools\Verify-P3.ps1 -ProjectPath C:\serenity_game -StepTimeoutSeconds 1200 -PlayerSmokeTimeoutSeconds 180 -PlayerSmokeOutput C:\serenity_game\Logs\P3-player-smoke-final
```

Results:

- Scene generator: Unity exit 0.
- P3 EditMode: 3 passed, 0 failed, 0 skipped (`Logs/P3-focused-editmode.xml`).
- P3 PlayMode: 3 passed, 0 failed, 0 skipped (`Logs/P3-focused-playmode.xml`).
- P3 GPU/D3D11 PlayMode: 3 passed, 0 failed, 0 skipped (`Logs/P3-gpu-playmode.xml`).
- Affected EditMode: 20 passed, 0 failed, 0 skipped (`Logs/P3-affected-editmode.xml`).
- Affected PlayMode: 25 passed, 0 failed, 0 skipped (`Logs/P3-affected-playmode.xml`).
- Windows x64 Mono Development: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=2`; output `Builds/P3/Serenity.exe`.
- Visible Player smoke: exit 0 and `passed=true`; 18 trees, 10 rocks, 52 grass clusters, two tools, two spatial audio sources, active campfire, 952 live rain particles, RTS/TPS rendered, canonical Character/building links true and stable identity true.
- Visually inspected evidence: `Logs/P3-player-smoke-final/p3-rts-clear.png`, `p3-tps-clear.png`, `p3-tps-rain.png`; machine result `p3-player-smoke.json`.
- PowerShell parser and `git diff --check` are part of the final repository check recorded at commit time.

## Diagnostics retained

- The first Player attempt exposed stripped runtime-only particle shaders in campfire FX; the final implementation references serialized URP assets.
- The next attempt exposed the same issue in rain FX; it now receives the serialized P3 rain material.
- One affected TPS test expected an English substring while the shipped locale is Russian; it now checks the stable localization key result.
- Unity internal services may emit two 300-second `Curl error 28` timeouts during post-build cleanup. The successful U00 result and Player smoke are retained; the verifier clears every step log first and waits for clean process exit, so it cannot accept a stale marker.

## Open limitations

- Final art taste is not accepted by the user yet. Hodaart Characters and primitive shelter/storage remain explicit grade-C placeholders.
- The ten-person canonical starter roster can be visible in the close camp composition through existing tier policy; P4/P5 should improve appearance variety and staging without replacing identities.
- P3 does not implement P4 appearance state, the P5 full region, P6 onboarding or any P8 performance/endurance closure.
- U29 remains PARTIAL, U30 remains IN_PROGRESS and U31 remains BLOCKED. No new eight-hour soak or U29 matrix was run.
