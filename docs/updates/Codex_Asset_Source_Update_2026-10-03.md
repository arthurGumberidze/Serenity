# Codex Asset Source Update — 2026-10-03

## Purpose
This addendum documents the current third-party asset layout and the rules Codex must follow when U05A (Asset Pipeline + Free Asset Acquisition) is reached.

## Current repository layout
Project root:
`C:\SERENITY_GAME`

Unity project assets:
`C:\SERENITY_GAME\Assets`

External/manual download staging:
`C:\SERENITY_GAME\serenity_games_assets`

The `serenity_games_assets` directory is **source staging**, not a Unity runtime content folder.

## Important rules

1. **Do not bulk-copy the entire `serenity_games_assets` directory into `Assets/`.**
2. **Do not delete or rename original downloaded source folders unless explicitly asked.**
3. Prefer **FBX + textures** for Unity imports when both FBX and GLB/OBJ/3DS are provided.
4. Use GLB/GLTF/OBJ only when the FBX version is missing, broken, or clearly inferior.
5. Preserve the original `License`, `LICENSE`, `Readme`, or source metadata files next to the staged download.
6. If a source folder has no visible license file, do not mark it as ship-ready. Record it as `LICENSE_REVIEW_REQUIRED`.
7. Every imported third-party package/model must be recorded in `docs/ASSET_REGISTRY.md`.
8. Imported runtime assets should go under a normalized structure such as:
   - `Assets/Art/ThirdParty/Characters/...`
   - `Assets/Art/ThirdParty/Buildings/...`
   - `Assets/Art/ThirdParty/Nature/...`
   - `Assets/Art/ThirdParty/Props/...`
   - `Assets/Art/ThirdParty/Weapons/...`
   - `Assets/Art/ThirdParty/Animals/...`
9. Create runtime prefabs separately from untouched source models when practical:
   - source mesh/material import
   - normalized material
   - collider
   - rig/avatar
   - animator
   - LOD
   - final prefab
10. Do not let third-party prefabs own gameplay/domain state. Gameplay components belong to Serenity runtime prefabs/components, not vendor demo scripts.
11. Vendor demo scenes and vendor demo/editor scripts should not be included in the MVP build unless needed.
12. Strip or disable unnecessary sample content only after confirming no required dependency is broken.
13. Do not bypass login, CAPTCHA, paywalls, license acceptance, or access controls.
14. If an asset cannot be acquired/imported automatically, create/update `docs/MANUAL_ASSET_DOWNLOADS.md` and continue with a placeholder.

## Git / source control
`serenity_games_assets/` contains manually downloaded third-party source packages and should normally be treated as local staging, not as canonical project source.

Codex should:
- inspect the current `.gitignore`;
- if `serenity_games_assets/` is not ignored, propose/add an ignore rule unless the project owner explicitly wants these raw third-party downloads versioned;
- never publish raw third-party asset packs to a public repository;
- keep license/source metadata in `docs/ASSET_REGISTRY.md` even if raw sources are ignored.

## Current manually downloaded source staging inventory
Detected in `C:\SERENITY_GAME\serenity_games_assets`:

- `3D Retro Medieval Fantasy Kit`
- `CaveManCreation`
- `FBX_LP_NaturePack_V2_1`
- `FBX_Stoneage_Weapons_V1_1`
- `FBX_Stoneage_WildHunt_V1_1`
- `Free Modular Medieval Assets`
  - `FBX`
  - `GLB`
  - `Textures`
- `FreeSurvivalKit`
  - `BLEND`
  - `FBX`
  - `TEXTURE`
- `Low Poly Tools`
- `model`

The last folder named only `model` is ambiguous. During U05A, Codex must inspect it and rename only the *imported Serenity-side destination*, not the original source folder, until the source package identity is known.

## Existing Asset Store / Unity-imported packages already inside `Assets`
Examples currently present:
- `EmaceArt/Slavic World Free`
- `EmbersStorm - Free Nature Pack`
- `Hodaart/HodaartLowPolyCharacterCollection3`
- `Medieval Fortification`
- `PolyOne/Free Modular Terrain`
- `Stylized Nature Environment`
- `SunboxGames/Avatars`
- `ToonyTinyPeople/TT_RTS`
- `TriForge Assets/Top Down - Fantasy Village`
- `URP GanzSe Free Modular Character Pack`
- `Vefects/Stylized Female Character - Vexa`

These are already Unity-imported vendor folders. Do **not** reorganize them blindly. First record them in the registry, identify which are actually used, then build Serenity-owned normalized prefabs/materials that reference them.

## Stone Age MVP priority
For the current MVP, prioritize:
1. Stone Age male/female character base.
2. Primitive/leather/fur clothing.
3. Stone/wood/bone weapons and tools.
4. Primitive hut/shelter.
5. Fire/campfire.
6. Storage / baskets / crates / racks.
7. Crafting props.
8. Stone Age animals / hunt targets.
9. Trees, bushes, rocks, logs, grass.
10. Basic fences/palisades.

Medieval packages remain useful as future-era content and as temporary modular geometry references, but they should not dominate the Stone Age MVP visual language.

## Visual style
Target style: **B — semi-realistic / stylized PBR / mid-poly**.

Avoid:
- extreme cartoon proportions,
- ultra-low-poly flat-shaded look as the final visual target,
- photoreal assets that clash with the rest of the environment.

Allowed:
- lower-detail Tier 2 variants for large NPC counts,
- higher-detail Tier 1 models for monarch, heirs, ministers, generals/captains,
- material rework and LOD generation to unify disparate source packs.

## U05A acceptance additions
U05A is not complete until:
- `docs/ASSET_REGISTRY.md` lists both the important Unity-imported vendor packs and selected manual-source packs;
- a Stone Age test scene exists using selected normalized assets;
- at least one humanoid, one primitive shelter, one tree, one rock, one campfire, one tool, one weapon, one storage prop and one animal are represented;
- imported assets have traceable source/license status;
- at least one Tier 1 character prefab and one lightweight Tier 2 character representation are validated;
- demo/vendor scripts are isolated from gameplay logic;
- the build still succeeds after asset integration.
