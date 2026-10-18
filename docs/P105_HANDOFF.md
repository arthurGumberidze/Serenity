# P10.5 handoff — Characters, clothing, LOD and animation

Status: **DONE** on 2026-10-18. `NEXT_TASK.md` advances to P10.6; no P10.6 implementation was performed.

## Delivered

- Normal New Game deterministically creates civilian, worker and military visuals plus three age groups while preserving the edited ruler and canonical stable-ID `AppearanceState`.
- Civilian presentation remains UMA. Worker and military visuals use Quaternius CC0 Peasant/Ranger outfits with Universal Base Character heads/eyes rebound to the outfit skeleton. Each role visual has one Humanoid `Animator`.
- Authored Quaternius clips cover carry, gather, chop, mine, build, fish, hunt, combat and sleep. The presenter enables animation before applying intent, holds the action through its lifecycle and returns to locomotion/idle correctly.
- Character presentation uses detailed skinned, instanced mid, lower-detail instanced far and culled stages. Thresholds are 24/32 m with hysteresis, 64 m and 120 m. The project-owned LOD shader is retained in Player builds.
- The scene-level environment LOD controller continues to use mesh-only cross-fades and never rotates trees toward the camera.

## External content and licence

The user explicitly authorized independent use of free assets with a clear licence. P10.5 imported these Quaternius CC0 packs:

- Universal Base Characters: `https://quaternius.com/packs/universalbasecharacters`
- Modular Character Outfits Fantasy: `https://quaternius.com/packs/modularcharacteroutfitsfantasy.html`
- Universal Animation Library 1: `https://quaternius.com/packs/universalanimationlibrary.html`
- Universal Animation Library 2: `https://quaternius.com/packs/universalanimationlibrary2.html`

Retained licence/readme files, source URLs and hashes are under `Assets/Game/Art/ThirdParty/Quaternius/P105/Licenses` and `Assets/Game/Art/ThirdParty/Quaternius/P105/SOURCE.md`.

## Accepted automated evidence

Unity executable for every command:

`C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`

Accepted affected EditMode:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P105\accepted-affected-edit.log -runTests -testPlatform EditMode -testCategory "P105|P84|U30A|P93|P96|P103|P104" -testResults C:\serenity_game\Artifacts\P105\accepted-affected-edit.xml`

Result: **45/45 passed**.

Accepted affected PlayMode:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P105\accepted-affected-play.log -runTests -testPlatform PlayMode -testCategory "P105|P84|P93|P96|P103|P104" -testResults C:\serenity_game\Artifacts\P105\accepted-affected-play.xml`

Result: **15/15 passed**.

AI/Character EditMode:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P105\accepted-ai-edit.log -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.CharacterPresentationTests;Game.Tests.Editor.Tier1UtilityAiTests;Game.Tests.Editor.WorkGroupAndJobTests" -testResults C:\serenity_game\Artifacts\P105\accepted-ai-edit.xml`

Result: **4/4 passed**.

AI/Character PlayMode:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P105\accepted-ai-play.log -runTests -testPlatform PlayMode -testFilter "Game.Tests.PlayMode.CharacterPresentationPlayModeTests;Game.Tests.PlayMode.ThirdPersonControlPlayModeTests;Game.Tests.PlayMode.U29PerformanceGatePlayModeTests" -testResults C:\serenity_game\Artifacts\P105\accepted-ai-play.xml`

Result: **35/35 passed**.

Exact normal-New-Game/role/action/LOD/Save-Load route:

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P105\exact-route-navmesh-play.log -runTests -testPlatform PlayMode -testCategory P105 -testResults C:\serenity_game\Artifacts\P105\exact-route-navmesh-play.xml`

Result: **4/4 passed**.

## Build

`Unity.exe -batchmode -nographics -projectPath C:\serenity_game -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P105\Serenity.exe -logFile C:\serenity_game\Artifacts\P105\pass-build.log -quit`

Result: exit 0; `U00_BUILD_RESULT: Succeeded; errors=0; warnings=44`.

- Player: `Builds/P105/Serenity.exe`
- Build GUID: `afde1eb6f77d4d839852e4d29a3d89f6`
- Source commit: `c3e29e3a547ce7069b0990cb5c942e74083a438a`
- Source fingerprint SHA-256: `FC959E6619E1BE5489B840F8EF83A36ECE8BCFC93E8BC26B308528BC4798F291`
- Created UTC: `2026-10-18T13:58:37.4339660Z`
- Before/after manifests match; `Builds/P105/build-provenance.json` exists.

## Accepted Player smoke

The isolated Development Player was launched windowed with forced D3D11 and exact `-screen-width 1920 -screen-height 1080`. The built-in P10.5 smoke followed the normal Main Menu → New Game route, produced evidence, exercised Save/Load and exited itself with process code 0.

Machine-readable result: `Artifacts/P105/p105-pass/p105-player-smoke.json`, `passed=true`, 1920×1080, 16 Characters. Every gate is true: menu/new game, worker visual, military visual, composed heads, authored chop, authored combat, three age groups, mid LOD, far LOD and Save/Load/Tier continuity. The exact sampled counts are 16 Tier1, one mid-LOD and one far-LOD projection.

Screenshots:

- `Artifacts/P105/p105-pass/p105-role-actions.png`
- `Artifacts/P105/p105-pass/p105-lod-chain.png`
- `Artifacts/P105/p105-pass/p105-after-load.png`

The near action and after-load frames visibly show complete headed worker/military Characters. The exact-distance LOD frame is dark and the distant projections are small, so it is retained as context rather than overstated as the sole proof; JSON telemetry and the 4/4 exact-route PlayMode test prove both LOD stages. The accepted Player log is `Artifacts/P105/p105-pass.log` and contains no null/missing-reference, crash or wardrobe-fallback diagnostic. Existing UMA eyelash-atlas warnings and URP unused post-process shader stripping remain unrelated warnings. Earlier failed/partial smoke directories were diagnostic iterations and are superseded by `p105-pass`.

## Beige bands in the supplied screenshot

The broad beige/brown bands are inherited P10.2 `LineRenderer` objects named `P10.2 Physical Route XX`. They are flat routes, 2.1 m wide, with only nine linear samples, and several overlap at a district anchor; the nearby cylinder is that district landmark. They are not Terrain cracks, lighting corruption, Character LOD or tree billboards. Their fan-shaped intersections and flat material remain explicit P10.2/P10.9 presentation debt.

## Remaining independent debt

P10.3 and P10.4 remain PARTIAL exactly as recorded in their handoffs and D-141/D-143. P10.5 completion does not waive their manual/Player gates. P10.2's p95 performance and route-strip presentation debt remain assigned to P10.9. P8.6/U29/U30/U31 remain unchanged.
