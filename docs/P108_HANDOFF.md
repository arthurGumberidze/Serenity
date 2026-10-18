# P10.8 handoff — Codes and diagnostic scenarios

Status: **DONE** on 2026-10-18. `NEXT_TASK.md` advances to P10.9. No P10.9 optimization or final product acceptance work was implemented.

## Delivered

- The ordinary paused-game navigation now exposes `Коды`. It cannot be opened while simulation is running and closes automatically on unpause. The panel documents and submits the exact `serenity`, `res_serenity` and `army_serenity` commands; unknown/empty input changes nothing and does not mark the party.
- `DiagnosticService` is an engine-free orchestration boundary. `serenity` completes the entire current technology catalog through `ResearchService`. `res_serenity` chooses one operational player-owned canonical common storage that accepts every catalog resource, expands its capacity atomically when needed and adds exactly 10,000 of each of the current nine resource definitions on every invocation. A failure restores both quantities and capacity, leaving no hidden overflow remainder.
- `army_serenity` creates one canonical adult Character for every rank in the data-driven military catalog on every invocation. Deterministic sequence-based `StableEntityId` values remain unique across repeats; each person receives inventory, appearance, combat skill, player ownership, Tier state, completed training and canonical unit roster membership. The diagnostic completion path reuses `MilitaryTrainingService`; no parallel army authority was added.
- The separate family diagnostic creates two canonical adults, mutual spouse/relationship state, two independent consents, an indoor bed in an operational closed/habitable home and the established P10.6/U18 pregnancy. Presentation uses the existing implicit `Время проходит…` modal and explicit `Пропустить` action.
- The first successful code or family diagnostic irreversibly sets `cheatsUsed=true` for the running party. Invalid codes and failed/rolled-back transactions do not set it. Snapshot v25 persists the flag and diagnostic identity sequence; restore uses OR/max semantics so loading an earlier clean v25 checkpoint cannot re-enable achievements or reuse generated IDs. Load/Save metadata and the in-game panel visibly say that codes were used and achievements are disabled. v24 remains readable without inventing cheat evidence, while malformed v25 diagnostics are rejected.
- Save restoration now reconciles the canonical Character registry and tier projections by stable identity. This permits repeated diagnostic-created Characters to survive Save/Load without making GameObjects or ScriptableObjects authoritative.
- No external asset/package, per-NPC `Update`, alternate resource/Character/military/pregnancy authority or P10.9 shortcut was introduced. `ASSET_REGISTRY.md` therefore required no change.

## Automated evidence

Unity executable for every command: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.

- Focused EditMode:
  `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter P108DiagnosticsTests -testResults C:\serenity_game\Artifacts\P108\focused-editmode-final.xml -logFile C:\serenity_game\Artifacts\P108\focused-editmode-final.log`
  — **9/9 passed**. This covers strict parsing, current-catalog unlock/repeat, exact inventory capacity rollback, v25 flag/sequence/corruption and v24 compatibility.
- Focused PlayMode after the final visual-only notification change:
  `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter P108DiagnosticsPlayModeTests -testResults C:\serenity_game\Artifacts\P108\post-visual-playmode.xml -logFile C:\serenity_game\Artifacts\P108\post-visual-playmode.log`
  — **3/3 passed**. This covers pause gating; unknown/failure no-mark behavior; all codes and repeats; unique soldiers of every rank; canonical family/pregnancy; exact resource additions; Save/Load; irreversible cheat evidence after loading a clean checkpoint; corrupt-slot rejection; and atomic missing-storage failure.
- Affected EditMode:
  `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "P104;P106;P107;P108;Persistence;ResearchFramework;PlayerControlState;ResourceInventory" -testResults C:\serenity_game\Artifacts\P108\affected-editmode.xml -logFile C:\serenity_game\Artifacts\P108\affected-editmode.log`
  — **90/90 passed**.
- Affected PlayMode excluding separately blocked U31:
  `Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter "P102;P103;P104;P106;P107;P108;P86;U29;U30" -testResults C:\serenity_game\Artifacts\P108\affected-playmode-final.xml -logFile C:\serenity_game\Artifacts\P108\affected-playmode-final.log`
  — **39/39 passed**.
- U31 was also run correctly with graphics rather than `-nographics`:
  `Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter "U31GpuPlayModeTests;U31IntegrationPlayModeTests" -testResults C:\serenity_game\Artifacts\P108\u31-graphics.xml -logFile C:\serenity_game\Artifacts\P108\u31-graphics.log`
  — **1/3 passed, 2 failed**. The existing GPU journey resolved `SideBVictory` where the test expects `SideAVictory`; the connected journey could not find its legacy UI row button. P10.8 neither modifies those tests nor claims U31 green. U31 remains BLOCKED.

## Build and visible Player evidence

Final build command:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P108Final\Serenity.exe -quit -logFile C:\serenity_game\Artifacts\P108\build-final7.log`

Accepted result: **Succeeded; errors=0; warnings=26**. Unity 6000.6.4f1, build GUID `67e07852d8eb42e78bf7c8b8e7ebaa2d`, source commit `bc9629f1b9e65916250fd41e9e54db542c2bd1b6`, source fingerprint SHA-256 `495D53495E0C7B0F4378ED004F984480F516172273CDE9ECFDC7781D4AEFEDC7`. `Builds/P108Final/build-provenance.json` exists and the before/after source fingerprint check completed before Unity exited.

Visible Player command:

`Serenity.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -force-d3d11 -p108Smoke -p108SmokeOutput C:\serenity_game\Artifacts\P108\player-final -p108SmokeWidth 1920 -p108SmokeHeight 1080 -logFile C:\serenity_game\Artifacts\P108\player-final.log`

The normal Main Menu → New Game → Pause → Codes → Save/Load route exited normally with every machine-readable flag true in `Artifacts/P108/player-final/p108-player-smoke.json`: unknown rejection/no mark, all current technologies, two exact resource grants, two unique all-rank armies, canonical family/pregnancy, fade/skip, irreversible achievements-off state, snapshot v25 round trip, visible Save warning, D3D11 and exact 1920×1080. The report saw nine resource definitions and four rank definitions. Its 120-frame screenshot-inclusive sample recorded average 42.197 FPS and minimum 7.332 FPS; this is route evidence only, not a P10.9 p95 performance pass.

Visually inspected evidence:

- `Artifacts/P108/player-final/p108-codes-panel.png`
- `Artifacts/P108/player-final/p108-family-fade.png`
- `Artifacts/P108/player-final/p108-save-warning.png`
- `Artifacts/P108/player-final/p108-after-load.png`
- `Artifacts/P108/player-final.log` confirms Direct3D 11.0 level 11.1 and `P108_PLAYER_SMOKE PASS`; no `Crash!!!`, `NullReferenceException` or `MissingReferenceException` was found.

The desktop computer-control surface exposed browsers but not the native Unity Player, so repeatable acceptance used the visible Player plus a build-contained controller that queries and invokes the actual named UI Toolkit `TextField`/`Button` controls. It does not mutate domain state directly and is active only under the explicit `-p108Smoke` argument.

## Preserved debt

P10.3 and P10.4 remain PARTIAL exactly as recorded in D-141/D-143 and their handoffs. P10.2's measured 1920×1080 p95 128.342 ms and broad overlapping route-strip presentation remain explicit P10.9 debt. The two reproduced U31 failures above remain U31 debt; P8.6/U29/U30/U31 statuses remain PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED. P10.9 must perform integration/performance work and cannot reinterpret this smoke sample as its performance gate.
