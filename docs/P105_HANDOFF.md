# P10.5 handoff — Characters, clothing, LOD and animation

Status: **PARTIAL** on 2026-10-18. `NEXT_TASK.md` remains P10.5; do not start P10.6.

## Implemented benchmark

- `CharacterAppearanceService` deterministically supplies every normal-New-Game starter with canonical skin, hair/beard, hair/eye colour and nine UMA morph values while preserving the edited ruler exactly.
- Detailed UMA generation is capped at eight. Direct/selected/nearest residents remain detailed; a camera-distance policy with hysteresis projects farther residents through one instanced project-owned humanoid mesh using the same stable appearance, clothing and body scale.
- Walk/run animator playback is normalized to measured translation speed. Existing Tier1 work/combat state maps centrally to bounded carry, gather, chop, mine, build, fish, hunt and combat poses without a per-NPC `Update`.
- Civilian/work/military clothing IDs are data-driven and persist through existing snapshot v22 appearance state. Only civilian visual content is available; work/military deliberately use the admitted fallback and retain their IDs for approved content/P10.7.
- One scene-level environment controller enforces cross-faded mesh LOD stages and disables `BillboardRenderer` stages without rotating vegetation.

## Automated evidence

Unity executable used for all commands:

`C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`

Focused P10.5 EditMode:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P105CharacterProductionTests -testResults C:\serenity_game\Artifacts\P105\editmode-p105.xml -logFile C:\serenity_game\Artifacts\P105\editmode-p105.log -quit`

Result: **4/4 passed**.

Focused P10.5 PlayMode final:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P105CharacterProductionPlayModeTests -testResults C:\serenity_game\Artifacts\P105\playmode-final.xml -logFile C:\serenity_game\Artifacts\P105\playmode-final.log -quit`

Result: **2/2 passed**.

Affected P8.4/P9.3/P9.6/P10.3/P10.4 suites: **32/32 EditMode**, **11/11 PlayMode** (`affected-editmode.xml`, `affected-playmode.xml`). Character/Tier1/work regressions: **4/4 EditMode**, **35/35 PlayMode** (`affected-ai-editmode.xml`, `affected-ai-playmode.xml`). After correcting normal-New-Game canonical appearance creation, the P10.5/P8.4/U30A bootstrap set passed **16/16 EditMode** (`bootstrap-editmode.xml`).

Final build command was run through `Start-Process -Wait -PassThru` so the batch process was observed to completion:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.U00BuildCommand.BuildWindows64MonoDevelopment -u00BuildPath C:\serenity_game\Builds\P105\Serenity.exe -logFile C:\serenity_game\Artifacts\P105\build-success.log -quit`

Result: exit 0, `U00_BUILD_RESULT: Succeeded; errors=0; warnings=44`. Player `Builds/P105/Serenity.exe`; build GUID `b1e61c66484f4a3abc80e94cf889d0f3`; source fingerprint `DB463FDF96DA0A000E6121B5FBE1684743D230AF447219A1C5305ABFBBE977ED`; before/after source manifests match; `Builds/P105/build-provenance.json` exists.

## Ordinary Player route and the beige bands

Launch arguments:

`Builds\P105\Serenity.exe -logFile C:\serenity_game\Artifacts\P105\visible-player-rerun.log -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -force-d3d11`

The save/settings environment was isolated under `Artifacts/P105/visible-rerun-*`. Windows exposed a 1282×751 application-window capture (1280×768 logical content), so it is not claimed as a literal 1920×1080 screenshot. The rerun reached Main Menu → New Game → local world → same-world strategic view with no `NullReferenceException`, `MissingReferenceException`, `ArgumentException`, `InvalidOperationException`, assertion, crash, UMA fallback or wardrobe fallback. While the Player remained open, the log contained 17 known UMA eyelash-atlas warnings.

The broad beige/brown bands in the user screenshot were reproduced. They are inherited P10.2 objects named `P10.2 Physical Route XX`: default-view-aligned `LineRenderer` strips, width 2.1 m, only nine linearly sampled points, flat route material, overlapping at district anchors. The nearby cylinder is the P10.2 district landmark. They are not Terrain cracks, lighting, Character LOD or tree billboard defects. Their fan-shaped overlaps and flat material are separate P10.2/P10.9 visual debt; P10.5 did not change them.

## Why P10.5 remains PARTIAL

The approved inventory lacks:

1. A compatible original soft-stylized modular human source with broad face/hair/body/age coverage and real profession/military wardrobe.
2. Rig-compatible authored chop, mine, gather, carry, build, fish, hunt, combat and sleep clips.
3. An authored multi-mesh Character LOD chain, or a source from which project-owned LODs may legally be derived.
4. Optionally, matching fauna/fish if P10.4 primitive replacements are included in the same approval.

Human-visible acceptance still needs an uninterrupted route proving resident diversity, tree transitions, RTS/TPS locomotion, representative authored work/combat motion, profession/military wardrobe and Save/Load/Tier continuity. The present procedural benchmark must not be called final art. P10.3/P10.4 debt remains unchanged.
