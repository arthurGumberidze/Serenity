# P8.1 handoff — visual prototype

Date: 2026-10-14

Status: DONE at the P8.1 implementation/evidence boundary

Next task: P8.2 — apply the benchmark language to the complete authored P5 region

## Outcome

`LocalGameplay` contains a compact, idempotently generated art benchmark under `P8.1 Visual Prototype`. It deliberately proves the direction before P8.2–P8.5 multiply it across the game:

- a readable meadow/shore transition and calm transparent water;
- stylized-natural URP Lit ground, sand, bark, canopy and stone materials with generated albedo/normal textures;
- visibly brown tree trunks, natural silhouettes and a restrained green/brown/grey palette;
- coherent daylight, fog and reflection-probe atmosphere;
- one detailed, customizable UMA human projected from stable canonical appearance data.

This is not a second map or a second character authority. `P81VisualPrototypePresentation` owns disposable scene views only. Its Character is keyed by stable ID `81000000000000000000000000000001`; canonical profile `uma_human_male_30` and all component IDs live in `AppearanceState`. UMA may be replaced or unavailable without changing persistent identity or simulation state.

## Approved content and licenses

- UMA 3.1f3 is pinned by UPM to upstream commit `c92bf29f6f1c17c8bf05d6465288e76b2dfedd08` and is MIT licensed.
- Selected official release content under `Assets/Game/Art/ThirdParty/UMA3` includes runtime races, slots, overlays, textures, wardrobe, animation/DNA/pose and non-demo SRP support. Demo scenes, sample GUI, tests, vehicles, UMA2 and HDRP examples are excluded.
- The retained content is approximately 692 MiB. This large size is explicitly accepted by the user. No individual tracked file exceeds common 100 MiB hosting limits.
- The official Unity URP Terrain Sample (Asset Store 213197) remains approved and approximately 2.8 GB is acceptable, but acquisition is deferred because the Asset Store/Package Manager requires the user's authenticated Unity session. P8.1 does not substitute an unapproved third-party terrain package.
- Full provenance and scope are in `docs/ASSET_REGISTRY.md` and `Assets/Game/Art/ThirdParty/UMA3/SOURCE.md`.

## UMA integration proof

The generated UMA project library owns 619 explicit official references. Its content audit reports 146 slots and 11 races; the inspected source slot contains 3,183 vertices. A real built-in male UMA recipe completed with one renderer, 16,277 vertices and 29,430 triangles. Runtime generation uses UMA when available and keeps a project-owned visual fallback for presentation resilience; the P8.1 acceptance path requires and reports provider `uma3`.

## Verification

Primary verifier:

```powershell
& .\Tools\Verify-P81.ps1 -ProjectPath 'C:\serenity_game' -StepTimeoutSeconds 1800 -PlayerSmokeTimeoutSeconds 300 -PlayerSmokeOutput 'C:\serenity_game\Logs\P81-player-smoke-final'
```

The focused and affected stages completed with:

- P8.1 EditMode: 4/4 passed;
- P8.1 headless PlayMode: 1/1 passed;
- P8.1 D3D11 PlayMode: 1/1 passed;
- affected P3/P4/P5 EditMode: 11/11 passed;
- affected P3/P4/P5 PlayMode: 8/8 passed.

The first Windows build attempt was interrupted by the command host's long-running process boundary after more than 15 minutes of first-time UMA shader compilation; it did not report a Unity build failure and produced no Player. A sandbox retry then exposed Unity's local Build Report/licensing IPC restriction. The final build ran outside that restriction from the warmed shader cache with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\serenity_game' -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P81/Serenity.exe' -logFile 'C:\serenity_game\Logs\P81-build-final5.log'
```

Result: `Succeeded; errors=0; warnings=14`; batchmode exited 0 and the immutable source fingerprint gate passed. Build GUID is `24c23cded5cb4f209ec22ec4b6e702c4`; source fingerprint is `7970C507FA06A921CCD63E238B23127CD34B4D759A354B3394302D43CE28ED9E`. Warnings are inherited obsolete object-query diagnostics plus official UMA DQ-skin/hair-shader and mesh-collider bake advisories; there are no compiler/build errors.

Final visible Player command:

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Logs\P81-player-smoke-final3\saves'
$env:SERENITY_SETTINGS_PATH='C:\serenity_game\Logs\P81-player-smoke-final3\settings\user-settings.json'
& 'C:\serenity_game\Builds\P81\Serenity.exe' -logFile 'C:\serenity_game\Logs\P81-player-smoke-final3\Player.log' -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -p81Smoke -p81SmokeOutput 'C:\serenity_game\Logs\P81-player-smoke-final3'
```

Result: exit 0 and `passed=true`; benchmark, natural Lit materials, brown trunks, stable appearance and visible Character all passed; `character_provider=uma3`, never fallback. The final image was visually inspected after correcting double-tinted albedo, local-light overexposure and UI occlusion. It visibly includes the official modular hair, beard, chest and leg recipes selected before UMA generation. Compact XML/JSON/PNG evidence is retained under `docs/validation/P81`.

## Boundaries and next work

- P8.1 does not rebuild the full P5 map, population groups, buildings/resources/effects or final UI; those remain P8.2–P8.5.
- Visual acceptance remains a user decision even when automated material, identity and visibility checks pass.
- P8.6 alone owns the complete U29 matrix, continuous/endurance/native-memory/history gates and final U31 completion. This task does not alter those thresholds or statuses.
