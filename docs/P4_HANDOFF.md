# P4 handoff — ruler creation and canonical appearance

## Outcome

P4 is DONE at its implementation/evidence boundary. New Game now provides a Russian ruler editor with ruler and dynasty names, two approved integrated appearance profiles, a rendered model preview, rotate/zoom, deterministic randomize, reset and confirm. The confirmed female or male ruler enters the normal bootstrap without changing existing inheritance law or the ownership of family/dynasty/succession rules.

The local approved catalog contains only `Serenity_Adult_A_Tier1` and `Serenity_Adult_B_Tier1` as whole compatible wrappers. They do not provide verified independently swappable faces, bodies, skin, hair, beards or clothing. The editor therefore displays each component's persisted stable ID but does not fabricate modular options. No package was bought or imported.

## Canonical and persistence contracts

- `AppearanceState` records Character ID plus stable profile/body/face/skin/hair/beard/hair-colour/clothing IDs.
- `CharacterAppearanceService` owns deterministic defaults and capture/restore independently from Unity presentation.
- `CharacterPresentationCatalog` resolves the same two profiles for menu preview and Tier1. Unknown saved content retains its canonical IDs while presentation logs once and uses a sex-compatible fallback.
- New Game assigns one appearance to all ten player Characters; later registered Characters receive deterministic defaults from the same catalog.
- Snapshot v16 appends `AppearanceWorldSaveState`. Versions 1–15 remain readable; v15 receives deterministic defaults.
- Save/Load rebuilds active disposable Tier1 views from stable Character IDs. P3 tools are rebound without duplicating camp fire, audio or particles.

## Verification evidence

Executed with Unity `6000.6.4f1`:

1. `-executeMethod Game.Infrastructure.Editor.P4AppearanceFoundation.Build` — catalog and MainMenu regenerated; exit 0.
2. `-runTests -testPlatform EditMode -testCategory P4` — 4 passed, 0 failed/skipped (`Logs/P4-focused-editmode.xml`).
3. `-runTests -testPlatform PlayMode -testCategory P4 -nographics` — 2 passed, 0 failed/skipped (`Logs/P4-focused-playmode.xml`).
4. Same P4 PlayMode category with `-force-d3d11` and graphics enabled — 2 passed, 0 failed/skipped (`Logs/P4-gpu-playmode.xml`).
5. Affected EditMode filter (persistence, dynasty/succession, control and P4) — 59 passed, 0 failed/skipped (`Logs/P4-affected-editmode.xml`).
6. Affected PlayMode filter (P2 menu, P3 camp, Character presentation, tiers, gameplay UI and P4) — 19 passed, 0 failed/skipped (`Logs/P4-affected-playmode.xml`).
7. `-buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P4/Serenity.exe` — succeeded, errors=0, warnings=8 (`Logs/P4-build.log`). Warnings are existing obsolete instrumentation/diagnostic API warnings.
8. Visible Player: `Builds/P4/Serenity.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p4Smoke -p4SmokeSeed 44009 -p4SmokeOutput Logs/P4-player-smoke-final` — PASS. JSON proves editor open/live preview, confirmed identity, projected profile and post-Save/Load preservation. `p4-ruler-editor.png` and `p4-world-after-load.png` were visually inspected.

The first restricted Unity launch crashed inside Unity's build-report loopback listener. The identical command outside the restricted sandbox completed normally; this was environment execution evidence, not a project failure.

## Next task

P5 is active: turn the reference camp into one coherent inhabitable simulation-backed region. Keep P4 appearance and v16 compatibility intact. Do not start P6/P7 or the deferred P8 U29/U30/U31 qualification.
