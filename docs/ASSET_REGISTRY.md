# Serenity Asset Registry

Checked on 2026-10-04. `Assets/Game/Art/ThirdParty` contains only explicitly selected source files. `Assets/Game/Art/Prefabs` contains Serenity-owned runtime wrappers. `serenity_games_assets` is an ignored download/staging area and is never a Unity asset root.

Status vocabulary: `APPROVED_RUNTIME`, `APPROVED_REFERENCE`, `PLACEHOLDER_ONLY`, `DEFERRED`, and `LICENSE_REVIEW_REQUIRED`. Visual grades are A (near-direct fit), B (fit after normalization), C (Tier 2/placeholder), D (style mismatch), and X (source/license blocked).

## Approved runtime packages

### HODAART-LPCC3

- Name: Low Poly Character Collection 3
- Type/category: Character and animation
- Source: Unity Asset Store
- URL: https://assetstore.unity.com/packages/3d/characters/humanoids/low-poly-character-collection-3-388758
- Author: Hodaart
- License: Standard Unity Asset Store EULA
- License URL: https://unity.com/legal/as-terms
- Date acquired: user-imported before U05A; checked 2026-10-04
- Commercial use: YES
- Attribution: NO
- Source/staging path: existing `Assets/Hodaart/HodaartLowPolyCharacterCollection3`
- Imported path: unchanged vendor paths for Characters 01 and 02, shared material/texture, and eight animation clips
- Runtime prefab path: `Assets/Game/Art/Prefabs/Characters/Serenity_Adult_A_Tier1.prefab`; `Serenity_Adult_B_Tier1.prefab`; separate project-owned `Serenity_Humanoid_Tier2.prefab`
- Selected role mapping: Character 01 / Adult A is the male Tier 1 candidate; Character 02 / Adult B is the female Tier 1 candidate
- Modifications: vendor files are not edited; Serenity wrappers add ground-aligned roots and capsule colliders
- Scale status: vendor importer remains untouched at scale 1; the Serenity wrapper applies one non-destructive uniform normalization to 1.8 m height because the selected shared vendor prefabs measure about 2.15 m
- Materials status: package-provided URP material; one shared 2K atlas
- Rig status: Humanoid/Mixamo skeleton; Avatar validity is an automated gate
- Animation status: idle, walk, run available; jump, greeting, happy walk and two dances also present; work/gather/attack/carry/death missing
- Collider status: wrapper capsule only
- LOD status: Tier 1 source has no authored LOD; single-mesh project-owned Tier 2 placeholder is available
- Performance: vendor documentation reports roughly 2.7K-3.1K vertices and 5.3K-6.1K triangles for selected Characters 01/02; one shared material
- Visual grade/status: C, `PLACEHOLDER_ONLY`; strongly cartoon/low-poly and not the final semi-realistic Stone Age baseline
- Notes: Existing user import was selected deliberately; vendor demo scripts/scenes/packages are not runtime dependencies and are not included in the U05A commit.

### WIZARDHAT-STONEAGE-NATURE

- Name: Stoneage Nature 2.1
- Type/category: Environment and campfire prop
- Source: itch.io official creator page
- URL: https://wizardhatstudio.itch.io/low-poly-nature
- Author: WizardHatStudio / Kaigar
- License: official page permits personal, educational and commercial use plus modification; raw redistribution and false authorship are prohibited
- License URL: https://wizardhatstudio.itch.io/low-poly-nature
- Date acquired: user-staged before U05A; checked 2026-10-04
- Commercial use: YES
- Attribution: NO stated requirement
- Source/staging path: `serenity_games_assets/FBX_LP_NaturePack_V2_1`
- Imported path: `Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageNature` (only Tree 01, Rock 01 and Campfire 01)
- Runtime prefab path: `Serenity_Tree.prefab`, `Serenity_Rock.prefab`, `Serenity_Campfire.prefab`
- Modifications: scoped importer normalization; external materials disabled; Serenity URP Lit material variants; wrapper pivots/colliders
- Scale status: importer-normalized to 6 m tree height, 1.4 m rock maximum dimension and 0.8 m campfire maximum dimension
- Materials status: Serenity-owned URP Lit variants; no vendor material mutation
- Rig/animation status: not applicable
- Collider status: simple wrapper box for tree/rock; campfire intentionally no collider
- LOD status: low-poly single mesh; additional LODs deferred
- Visual grade/status: C, `APPROVED_RUNTIME` for Tier 2/placeholder use
- Notes: 3 of 34 staged FBX models were selected; the rest remain in ignored staging.

### WIZARDHAT-STONEAGE-WEAPONS

- Name: Stoneage Weapons 1.1
- Type/category: Weapon and tool
- Source: itch.io official creator page plus archive license
- URL: https://wizardhatstudio.itch.io/stoneage-weapons
- Author: WizardHatStudio / Kaigar
- License: CC0 1.0 in the downloaded `License.txt`
- License URL: https://creativecommons.org/publicdomain/zero/1.0/
- Date acquired: user-staged before U05A; checked 2026-10-04
- Commercial use: YES
- Attribution: NO
- Source/staging path: `serenity_games_assets/FBX_Stoneage_Weapons_V1_1`
- Imported path: `Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWeapons` (Stone Axe, Stone Spear and Torch only)
- Runtime prefab path: `Serenity_StoneAxe.prefab`, `Serenity_StoneSpear.prefab`, `Serenity_Torch.prefab`
- Modifications: scoped importer normalization; external materials disabled; Serenity URP Lit variants; centered attachment wrapper pivots
- Scale status: importer-normalized to 0.7 m axe, 1.8 m spear and 0.8 m torch maximum dimension
- Materials status: Serenity-owned URP Lit variants
- Rig/animation status: static; bow draw and attachment animations are not included
- Collider status: none on hand-held props
- LOD status: source meshes are already lightweight; additional LODs deferred
- Visual grade/status: C, `APPROVED_RUNTIME` for placeholder/Tier 2 use
- Notes: 3 of 11 models were selected. The full staging package is not copied.

### WIZARDHAT-STONEAGE-WILD-HUNT

- Name: Stoneage Wild Hunt 1.1
- Type/category: Animal
- Source: itch.io official creator page plus archive license
- URL: https://wizardhatstudio.itch.io/stoneage-wild-hunt
- Author: WizardHatStudio / Kaigar
- License: CC0 1.0 in the downloaded `License.txt`
- License URL: https://creativecommons.org/publicdomain/zero/1.0/
- Date acquired: user-staged before U05A; checked 2026-10-04
- Commercial use: YES
- Attribution: NO
- Source/staging path: `serenity_games_assets/FBX_Stoneage_WildHunt_V1_1`
- Imported path: `Assets/Game/Art/ThirdParty/WizardHatStudio/StoneageWildHunt/Models/Boar_Animations_V4.fbx`
- Runtime prefab path: `Assets/Game/Art/Prefabs/Animals/Serenity_Boar.prefab`
- Modifications: Generic animation import, scale normalization, Serenity URP Lit material and wrapper box collider
- Scale status: importer-normalized to 1.8 m maximum dimension
- Materials status: Serenity-owned URP Lit variant
- Rig status: Generic animated animal rig; not Humanoid
- Animation status: idle 01/02, walk, run and attack advertised by official source; clip inventory is validated in Unity
- Collider status: simple wrapper box
- LOD status: no authored LOD detected; source is low-poly
- Visual grade/status: C, `APPROVED_RUNTIME` for MVP hunt target placeholder
- Notes: only the boar FBX and included license are imported.

## Approved project-owned placeholders

### SERENITY-PRIMITIVE-PLACEHOLDERS

- Name: Serenity primitive Stone Age placeholders
- Type/category: Humanoid Tier 2, shelter, storage
- Source/author/license: project-owned Unity primitive geometry; Serenity project
- Date created: 2026-10-04
- Commercial use: YES; attribution: NO
- Imported path: none
- Runtime prefab path: `Serenity_Humanoid_Tier2.prefab`, `Serenity_PrimitiveShelter.prefab`, `Serenity_StorageBasket.prefab`
- Modifications/status: generated deterministically by `U05AAssetFoundation`; meter scale, ground pivots, URP materials and simple colliders
- Rig/animation/LOD: Tier 2 humanoid is unrigged single-mesh placeholder; shelter/storage are static; no additional LOD
- Visual grade/status: C, `PLACEHOLDER_ONLY`
- Notes: these close the pipeline/test-scene acceptance slots without pretending to be final art.

## Staging packages considered but not imported

| Asset ID | Name and category | Source or URL | License and commercial status | Staging path | Runtime/import path | Readiness and visual grade | Normalization notes |
|---|---|---|---|---|---|---|---|
| STAGE-RETRO-MEDIEVAL | 3D Retro Medieval Fantasy Kit; buildings/materials | Unknown local download | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN; attribution UNKNOWN | `serenity_games_assets/3D Retro Medieval Fantasy Kit` | none | X; medieval and source-blocked | BLEND-only source plus textures; no runtime import |
| STAGE-CAVEMAN | CaveManCreation; three character variants | Unknown local Unity-style export | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN; attribution UNKNOWN | `serenity_games_assets/CaveManCreation` | none | X; visually relevant but license-blocked | FBX preferred over duplicate 3DS/GLTF/OBJ; rig/material/animation readiness not trusted until source is established |
| STAGE-FREE-MODULAR-MEDIEVAL | Free Modular Medieval Assets; building modules | https://mortaleh.itch.io/modular-medieval-town-house | CC0 1.0 local license; commercial YES; attribution NO | `serenity_games_assets/Free Modular Medieval Assets` | none | C/D; `APPROVED_REFERENCE`, medieval rather than Stone Age | FBX preferred; no need to import while project-owned shelter placeholder suffices |
| STAGE-FREE-SURVIVAL | Free Survival Kit; tent, campfire, mattress, axe, pickaxe | Unknown local purchase/download | local custom commercial-use license exists but author/source and proof of acquisition are absent: `LICENSE_REVIEW_REQUIRED` | `serenity_games_assets/FreeSurvivalKit` | none | X until provenance; likely C | FBX is preferred; BLEND ignored; no runtime use |
| STAGE-LOW-POLY-TOOLS | Low Poly Tools; 20 stone/wood tools and weapons | Unknown | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN | `serenity_games_assets/Low Poly Tools` | none | X; likely C | Duplicates approved Stoneage Weapons coverage; no import |
| STAGE-HUT-GLB | Stone Field Hut; shelter | unknown; GLB generator reports `3dassets.dev ingest` | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN | `serenity_games_assets/model/model.glb` | none | X; contents identified, provenance not identified | Scene contains stone and thatch meshes/materials; no animations; quarantined |
| STAGE-WORKSHOP-GLB | Fibre and Hide Workshop; crafting props | unknown; GLB generator reports `3dassets.dev ingest` | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN | `serenity_games_assets/model.glb` | none | X; strong Stone Age relevance but provenance blocked | Contains loom, hide frame, stump, punch, hamper, reeds, fleshing beam and cordage spool; quarantined |
| STAGE-MEDIEVAL-SURVIVAL-BLEND | medievalsurvivalprops; props | Unknown | `LICENSE_REVIEW_REQUIRED`; commercial UNKNOWN | `serenity_games_assets/medievalsurvivalprops.blend` | none | X/D | BLEND-only, source unknown, no import |

## Existing Unity vendor packages considered

These remain in their original user-imported locations. Except for the deliberate Hodaart subset above, they are not U05A runtime dependencies and are not added to the commit.

| Asset ID | Existing package | Category | Source/license status | Visual grade | Runtime decision and technical notes |
|---|---|---|---|---|---|
| UNITY-EMACE-SLAVIC | EmacEArt Slavic World Free | Medieval environment | Unity Asset Store origin inferred; exact local entitlement/version proof not captured: `LICENSE_REVIEW_REQUIRED` for shipping selection | B/C | deferred future-era source; large prefab/FBX set, no U05A wrapper |
| UNITY-EMBERS-NATURE | EmbersStorm Free Nature Pack | Nature | local README but no complete license record: `LICENSE_REVIEW_REQUIRED` | B/C | potentially closer environment candidate; deferred to avoid duplicate nature baseline |
| UNITY-EMBERS-ARMOR | EmbersStorm Magic Fantasy Armors | Armor | incomplete three-file import; `LICENSE_REVIEW_REQUIRED` | D/X | not usable |
| UNITY-HODAART-LPCC3 | Low Poly Character Collection 3 | Character | confirmed Standard Unity Asset Store EULA; see approved entry | C | Characters 01/02 selected for rig and animation readiness only |
| UNITY-MEDIEVAL-FORT | Medieval Fortification | Building | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | B/D | future-era, very large textures; not selected |
| UNITY-POLYONE-TERRAIN | Free Modular Terrain | Terrain | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | C | deferred; no terrain overhaul in U05A |
| UNITY-STYLIZED-NATURE | Stylized Nature Environment | Nature | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | B/C | candidate for later material unification; 16 PSDs and heavy source footprint |
| UNITY-SUNBOX-AVATARS | SunboxGames Avatars | Modular characters | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | B/C | feature-rich but 573 MB and contains scripts; not selected |
| UNITY-TOONY-RTS | Toony Tiny RTS | Characters/RTS | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | D | proportions conflict with approved style |
| UNITY-TRIFORGE-VILLAGE | Top Down Fantasy Village | Buildings/environment | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | C/D | future medieval placeholder only; 354 MB |
| UNITY-GANZSE-MODULAR | GanzSe Free Modular Character Pack | Modular characters/clothing/hair/beard | Unity Asset Store origin visible, but exact package record not finalized: `LICENSE_REVIEW_REQUIRED` for runtime selection | C | useful modularity and hair/beard inventory, but strongly fantasy/low-poly and very many parts; deferred |
| UNITY-VEFECTS-VEXA | Stylized Female Character Vexa | Female character | no local proof recorded: `LICENSE_REVIEW_REQUIRED` | B/D | detailed rig/animation candidate but modern/fantasy costume and 343 MB; deferred |

## Registry policy

- Unknown or incompatible licenses prohibit runtime use.
- Vendor source assets remain untouched. Serenity gameplay and presentation prefabs live under `Assets/Game/Art/Prefabs` and may reference only selected, registered source content.
- 1 Unity unit equals 1 meter. Importer scale is preferred; wrapper transforms handle only ground/attachment pivot alignment.
- Addressables are not used in U05A. Build inclusion remains scene/reference driven.
