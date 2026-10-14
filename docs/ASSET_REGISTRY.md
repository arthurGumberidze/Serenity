# Serenity Asset Registry

Checked on 2026-10-14. `Assets/Game/Art/ThirdParty` contains only explicitly selected source files. `Assets/Game/Art/Prefabs` contains Serenity-owned runtime wrappers. `serenity_games_assets` is an ignored download/staging area and is never a Unity asset root.

Status vocabulary: `APPROVED_RUNTIME`, `APPROVED_REFERENCE`, `PLACEHOLDER_ONLY`, `DEFERRED`, and `LICENSE_REVIEW_REQUIRED`. Visual grades are A (near-direct fit), B (fit after normalization), C (Tier 2/placeholder), D (style mismatch), and X (source/license blocked).

## Approved runtime packages

### UMA-3.1F3

- Name: UMA 3 (Unity Multipurpose Avatar), release 3.1f3
- Type/category: modular runtime character generation, Human Male 3.0 content and URP/SRP materials
- Source: official UMA Steering Group GitHub repository and release package
- URL: https://github.com/umasteeringgroup/UMA/releases/tag/v3.1f3
- Author: UMA Steering Group
- License: MIT for the repository/package; retained `LICENSE.md` is resolved through the UPM package
- Date acquired: 2026-10-13/14
- Commercial use: YES
- Attribution: license notice retained
- Package dependency: `com.umasteeringgroup.uma` pinned to tag `v3.1f3` / commit `c92bf29f6f1c17c8bf05d6465288e76b2dfedd08`
- Imported path: `Assets/Game/Art/ThirdParty/UMA3`; selected source content plus the non-sample SRP subset from official `UMA31_f3.unitypackage`
- Runtime use: `UMADynamicCharacterAvatar-NoPhysics_NoLOD.prefab` in the P8.1 benchmark; Serenity `AppearanceState` remains the canonical save-state and UMA remains a replaceable Presentation provider
- Import scope: races, slots, overlays, textures, wearables, animation, DNA, expressions, poses, getting-started prefab and required URP/SRP materials; demo scenes, tests, vehicles, sample GUI, HDRP and UMA2 compatibility content are excluded
- Size/performance: about 692 MiB selected runtime source; the P8.1 generated Human Male 3.0 smoke has one renderer, 16,277 vertices and 29,430 triangles before later LOD/budget work
- Visual grade/status: B prototype source, `APPROVED_RUNTIME`; modular and materially closer to the requested stylized-natural/PBR direction, but final group art and LOD acceptance belong to later P8 stages
- Notes: the user explicitly accepted a large asset footprint. The complete 1.03 GiB release archive and checkout remain ignored under `Library`; only selected runtime dependencies are committed.

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

### SERENITY-P3-REFERENCE-CAMP

- Name: P3 Stone Age shore-camp reference set
- Type/category: project-owned scene composition, procedural meshes, URP materials, particle mask and audio synthesis
- Source/author/license: Serenity project-owned code and generated Unity assets; no external source files
- Date created/checked: 2026-10-13
- Commercial use: YES; attribution: NO
- Asset paths: `Assets/Game/Art/Meshes/P3`, `Assets/Game/Art/Materials/P3`, `Assets/Scenes/LocalGameplay.unity`
- Generator: `Assets/Game/Infrastructure/Editor/P3ReferenceSceneBuilder.cs`; repeatable and idempotent
- Runtime dependencies: approved HODAART-LPCC3 wrappers, WIZARDHAT-STONEAGE-NATURE tree/rock/campfire wrappers, WIZARDHAT-STONEAGE-WEAPONS axe/spear wrappers, and SERENITY-PRIMITIVE-PLACEHOLDERS shelter/storage
- Contents: curved shore and water meshes, grass clusters, ground/shore/water/sky/fire/building/particle materials, soft particle mask, scene layout, campfire/rain/shore presentation and procedural loop audio
- Scale/pivots: 1 Unity unit = 1 metre; generated meshes use world-metre dimensions; imported content retains approved wrapper normalization
- Collider/LOD status: existing approved wrapper colliders are retained; project-owned decorative meshes have no gameplay colliders; no new authored LODs
- Visual grade/status: B/C reference baseline, `APPROVED_RUNTIME`; coherent and readable but not final production art
- Notes: P3 imported no new vendor package and used none of the license-review-required staging/existing packages. Hodaart people and primitive shelter/storage remain explicit grade-C placeholders pending a separately approved compatible art source.

### SERENITY-P5-INHABITABLE-REGION

- Name: P5 authored inhabitable-region extension
- Type/category: project-owned route, resource, supply and territory presentation assets plus scene composition
- Source/author/license: Serenity project-owned code and generated Unity assets; no external source files
- Date created/checked: 2026-10-13
- Commercial use: YES; attribution: NO
- Asset paths: `Assets/Game/Art/Meshes/P3/P5_*`, `Assets/Game/Art/Materials/P3/P5_*`, `Assets/Scenes/LocalGameplay.unity`
- Generator: `Assets/Game/Infrastructure/Editor/P5RegionSceneBuilder.cs`, delegating to the idempotent P3/P5 scene recipe
- Runtime dependencies: the approved P3 runtime set and its registered Serenity wrappers; Unity AI Navigation already present in the project
- Contents: authored route strip, supply-cache and territory-totem meshes; route, berry, fibre and rival-territory materials; expanded forest/stone/grass layout; canonical visible berry/fibre/tree/stone nodes; buildable clearings and rival territory
- Scale/pivots: 1 Unity unit = 1 metre; generated meshes are authored in metre-scale local coordinates
- Collider/LOD status: resource and supply views have simple colliders; tree/rock wrapper colliders remain; water is marked non-walkable and shore is collidable; lightweight generated meshes have no additional LOD
- Visual grade/status: B/C provisional authored baseline, `APPROVED_RUNTIME`; coherent and gameplay-readable, but final visual acceptance remains open
- Notes: P5 bought or imported no package, edits no vendor source, and does not authorize any license-review-required staging asset.

### SERENITY-P81-VISUAL-BENCHMARK

- Name: P8.1 stylized-natural visual benchmark
- Type/category: project-owned procedural shore/meadow/forest/stone composition, PBR materials, lighting and fallback character modules
- Source/author/license: Serenity project-owned code and generated Unity assets; no external source files
- Date created/checked: 2026-10-13/14
- Commercial use: YES; attribution: NO
- Asset paths: `Assets/Game/Art/Meshes/P81`, `Assets/Game/Art/Materials/P81`, `Assets/Game/Art/Textures/P81`, `Assets/Scenes/LocalGameplay.unity`
- Generator: `Assets/Game/Infrastructure/Editor/P81VisualPrototypeBuilder.cs`; repeatable and idempotent
- Runtime dependencies: URP 17.6, approved UMA-3.1F3, and existing Serenity camera/composition boundaries
- Contents: compact meadow/shore/water study; five brown-trunk trees with irregular crowns; shoreline stones; calm palette; generated albedo/normal assets; local key light/reflection probe; one modular UMA character and project-owned fail-safe visual
- Canonical state: stable Character ID and data-only `AppearanceState`; neither UMA nor fallback GameObjects own save-state
- Visual grade/status: B prototype, `APPROVED_RUNTIME` for the P8.1 benchmark only; it deliberately does not replace the full map, army/group presentation or production UI
- Notes: machine acceptance covers URP Lit/PBR material presence, brown trunks, stable appearance, successful UMA generation and a visible Player screenshot. Final taste approval and propagation to the whole game remain later P8 stages.

### SERENITY-P82-NATURAL-REGION

- Name: P8.2 complete natural inhabitable region
- Type/category: project-owned finite land/shore/water, tree-trunk and ore-cluster meshes; PBR ore materials; authored scene composition
- Source/author/license: Serenity project-owned code and generated Unity assets; no external source files
- Date created/checked: 2026-10-14
- Commercial use: YES; attribution: NO
- Asset paths: `Assets/Game/Art/Meshes/P82`, `Assets/Game/Art/Materials/P82`, `Assets/Scenes/LocalGameplay.unity`
- Generator: `Assets/Game/Infrastructure/Editor/P82NaturalRegionBuilder.cs`; repeatable and idempotent over the P3/P5 recipe
- Runtime dependencies: approved P3/P5 wrappers and the project-owned P8.1 URP/PBR meadow, shore, water and brown-bark materials
- Contents: irregular 16-point inhabitable land mesh, shoreline ring, calm boundary water, tapered brown trunks, iron/gold ore clusters, PBR iron/gold surfaces and complete region layout
- Canonical state: geometry and markers are replaceable Presentation; resources bind to `ProductionService` with deterministic `StableEntityId`; map/territory/building/save authorities are unchanged
- Scale/collider/LOD: 1 Unity unit = 1 metre; land and ore use generated MeshColliders, water is non-walkable, existing wrapper colliders are retained; no new authored LOD
- Visual grade/status: B authored product pass, `APPROVED_RUNTIME`; final character/building/effect/UI and whole-product visual acceptance remain P8.3–P8.5
- Notes: no new vendor package was imported, no vendor source was edited, and no license-review-required staging asset is referenced. The deferred official Terrain Sample is not claimed as used.

## Newly approved runtime environment

### UNITY-URP-TERRAIN-SAMPLE

- Name: Unity URP Terrain | Terrain Sample Project, package 1.0.3
- Type/category: realistic URP terrain/environment source
- Source: Unity Asset Store, official Unity Technologies publisher, asset ID 213197
- URL: https://assetstore.unity.com/packages/3d/environments/landscapes/unity-urp-terrain-terrain-sample-project-213197
- License: Standard Unity Asset Store EULA; listing also identifies the Unity Companion License terms for supplied Unity content
- Date acquired/imported: user-downloaded and explicitly approved for import on 2026-10-14
- Commercial use: YES under the applicable Unity Asset Store/Companion terms; raw redistribution is not authorized
- Compatibility verified: source package targets Unity 6000.3; imported and built under approved Unity 6000.6.4f1 with URP 17.6.0
- Runtime/import path: `Assets/TerrainDemoScene_URP`; 769 selected files, approximately 2308.9 MiB source footprint
- Import scope: Terrain layers/textures, trees, rocks, details/foliage, water, skybox resources, required materials/models/Shader Graphs and third-party notices
- Excluded scope: demo scenes, sample scripts, UI, bookmarks, shots, lighting rigs, volume profiles, animation, authored demo Terrain data/brushes/palettes and package-proposed dependency downgrades
- Runtime use: P9.3 canonical 128 m gameplay Terrain with seven official layers, the P9.2 base population plus 170 trees, 94 rocks, 930 ground-cover instances and 120 resource-formation satellites; P9.1/P9.2 assets remain reversible source history
- Package impact: existing package versions preserved; built-in `com.unity.modules.terrain` and `com.unity.modules.terrainphysics` 1.0.0 enabled
- Visual grade/status: A/B source quality, `APPROVED_RUNTIME`; P9.3 populated-biome composition accepted through RTS/TPS Player evidence
- Notes: vendor assets are not edited. Project-owned P9.2 TerrainData/material/scene recipes compose the selected source; canonical gameplay state remains outside vendor and presentation assets.

### SERENITY-P92-REALISTIC-GAMEPLAY-REGION

- Name: Serenity P9.2 Realistic Gameplay Region
- Type/category: project-owned Terrain composition and runtime presentation binding
- Source/license: Serenity-owned authoring code/data composed from `UNITY-URP-TERRAIN-SAMPLE`
- Runtime path: `Assets/Game/Art/Terrain/P92`, `Assets/Game/Art/Materials/P92`, `Assets/Game/Presentation/Environment/P92RealisticGameplayRegion.cs`
- Runtime use: canonical six-location/24-resource local region projection, terrain-height projection for disposable building/Character presentation, seasons/weather and physical Terrain collision
- Stable-content rule: resource indices 0–23 and all established `StableEntityId`/`WorldLocationId` values are preserved; TerrainData, anchors and renderers own no mutable gameplay/save truth
- Visual grade/status: B terrain-migration baseline, `APPROVED_RUNTIME`; full biome density/zoning is P9.3, while settlement/building/Character finish is P9.4

### SERENITY-P93-POPULATED-BIOME-WORLD

- Name: Serenity P9.3 Populated Biome World
- Type/category: project-owned deterministic biome authoring and presentation binding
- Source/license: Serenity-owned composition code/data using only registered `UNITY-URP-TERRAIN-SAMPLE` assets
- Runtime path: `Assets/Game/Presentation/Environment/P93PopulatedBiomeWorld.cs`, `Assets/Game/Infrastructure/Editor/P93PopulatedBiomeWorldBuilder.cs`, `Assets/Scenes/LocalGameplay.unity`
- Runtime use: seven official Terrain layers; forest, understory, meadow, rocky upland, soil-clearing and shoreline zones; 170 trees, 94 rocks, 930 ground-cover instances and five satellites around each of 24 canonical resources
- Determinism/provenance: fixed scatter seed 93031 and serialized layout hash; every spawned vendor prefab and Terrain layer resolves under `Assets/TerrainDemoScene_URP`
- Scale/collider/LOD: official LODGroups retained for trees/rocks; physical colliders retained or supplied for major obstacles; traversable ground cover and formation satellites have no colliders or shadow cost
- Canonical-state rule: the six existing location IDs and 24 resource roots/indices/`StableEntityId` values are reused; biome objects, zone metadata and satellite decorations own no mutable save-state
- Visual grade/status: B populated-world pass, `APPROVED_RUNTIME`; settlement/building/resource-work/Character art replacement remains P9.4

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
