# WM15 content follow-up — source checkpoint, 2026-10-24

**IN_PROGRESS. Uncompiled, untested, not visually accepted.** The user explicitly forbids tests and rebuilding
while adding bugs. No Unity Editor, Player, batchmode, compilation or build was launched. The existing
`Builds/WorldMapV2Feedback/Serenity.exe` does not contain this checkpoint or the preceding source-only repair.

## Implemented in source

- Residents use project-owned copies of the user-imported Polytope male/female/boy peasant prefabs. Current
  player/NPC monarchs keep UMA; the character editor remains UMA. Style is reconsidered after simulation changes
  (succession/age) and on load. Initial resident creation precedes Clock creation, so the initial biological epoch
  is explicitly supported. The shared animation budget remains; resident models are retained when their pose
  is frozen rather than replaced with the old generic crowd body. This adds renderer cost within Tier1 and
  requires later measurement; Tier2/3 architecture is unchanged.
- Axe, pickaxe and hammer have separate textured prefabs, normalized grip transforms and activity visibility.
  Tools bind to the current humanoid hand, including after UMA generation. Idle residents hide work tools.
  Demo-camp permanent equipment was removed to prevent a second implement intersecting the active work tool.
- Crops: berries, carrot, cabbage and plant fiber are available through agricultural research. The empty-field
  inspector selects the recipe, with ownership/operational-state/task checks in ProductionService. The selected
  recipe already belongs to the saved ProductionSiteState; state hashing now includes it. Existing saves gain
  missing carrot/cabbage stock targets without overwriting explicit targets. Carrot/cabbage are edible resources.
- CropPlotPresentation builds orderly soil rows, crop-specific models and a low fence with an entrance. Plant
  visibility/scale comes from canonical planting/growth/harvest state. No per-field Update loop or parallel
  growth/save authority. A maximum of 12×12 plant instances bounds each field. Existing building refresh owns
  updates; load reconnects the projection to restored sites. Toon Farm vegetation materials now have their atlas
  in URP BaseMap, with alpha clipping; the previously null BaseMap would have produced pale/untextured vegetation.
- Fox and wolf were removed from active wildlife definitions, concentration-zone lists, target ranges and art
  catalogs. Old saves explicitly retire only these species; arbitrary unknown species remain an error. Deer,
  bear, boar, hare and waterfowl remain. Rabbit art is still pending below.
- The mountain stream's first spline point was outside its lake. It now begins inside the lake at (855,790).
  The south-lake stream also receives an interior point (615,225). Shore meshes use signed-boundary clipping
  and common corner heights instead of whole occupied grid squares. Clipping scratch storage is reused; empty
  triangle meshes are not given physics colliders. Mesh resolution doubles, so load/GPU cost must be measured
  later. The repetitive checker-like wave modulation is replaced with broad two-direction ripples; only water
  appearance changes, not the regional color proposal.
- The two-pronged object in screenshot 3 was `SM_Env_Monolith_02`, assigned to `poi_rock_spire`. Its mesh mapping
  is removed, including from the authoring command. The stable logical terrain landmark remains for exploration
  and save identity; the large black object no longer has an art prefab to instantiate.

## Assets and outstanding work

Local admission: 38 asset files plus metadata, about 90 MiB. Project copies live under
`Assets/Game/Art/WM15Content`. Source vendor assets and the user's concurrent imports were not edited.
The copied Polytope NPC shader uses the **URP 17** version from the already present nested
`PT_Peasants_Free_URP_17.unitypackage`; its color-mask system is retained. Tool materials use existing URP Lit.
Exact source/destination/GUID provenance is in `WM15_CONTENT_ASSET_MANIFEST.json` beside this report.

- [Polytope peasants](https://assetstore.unity.com/packages/3d/characters/humanoids/humans/low-poly-medieval-peasants-free-lowpoly-medieval-fantasy-series-122225)
  were already imported and are connected in source. Their actual animation, silhouette, tool grip and materials
  still need a future rendered review.
- [White Rabbit](https://assetstore.unity.com/packages/3d/characters/animals/white-rabbit-138709) is the approved
  replacement. It was not found in Assets, local package cache or Downloads. Browser initialization failed with
  `failed to write kernel assets ... os error 3`, so acquisition through the authenticated Asset Store was
  unavailable. **The hare still uses the old temporary cat prefab.** No substitute was represented as the
  requested model. WM08 catalog regeneration now preserves an assigned hare entry instead of overwriting it;
  actual rabbit import, URP material admission, animation hookup and catalog assignment remain open.
- Toon Farm Pack contains domestic animals (including sheep/lambs, pigs/piglets and cows/bulls), modular fences
  and gates, as well as crops. The pack is useful. It does not supply this game's canonical livestock system.
  **A functioning animal pen is not implemented by this checkpoint.** Adding decorative animals as if they
  were a working herd would repeat the interaction/state problem. This remains a follow-up: choose species,
  canonical herd stock/ownership, feeding and outputs, then bind the existing animal/fence art. Crop fields
  and their selection/growth presentation are implemented separately above.
- [Animal Rug Pack Free](https://www.fab.com/listings/4e95de92-2edb-4565-8706-6ac9e937f692), by the same publisher,
  contains four decorative hides/rugs with textures. These are props, not living/animated animals. It is not
  needed for the current defects; it could furnish a hunting lodge later. No import or purchase made.
- [Stylized Water 3](https://assetstore.unity.com/packages/vfx/shaders/stylized-water-3-287769) is an optional
  visual upgrade: the official listing checked this turn showed URP support, Unity 6000.0.68 baseline,
  version 3.3.3 and US$50 before tax. The [publisher's description](https://discussions.unity.com/t/stylized-water-3-art-focused-water-shader/1542753)
  includes river flow, foam and ripples. Compatibility/performance in this exact project is not verified.
  It is unnecessary to repair disconnected geometry. **No purchase or new water dependency was introduced.**
  Prices may change; a paid purchase requires separate approval.

## Point 4: analysis only

`WM15_MAP_ART_DIRECTION_QUESTIONS.md` records the renderer/palette diagnosis, an eight-region proposal and
13 questions. No sakura, biome palette, vegetation distribution, global lighting or localized swamp fog was
changed. Existing route-h screenshots are historical references, not proof of this revision's appearance.

## Commands and evidence boundary

Executed source preparation:

```powershell
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15Content/admit_assets.py
& 'C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Artifacts/WM15Content/configure_assets.py
git diff --check -- Assets/Game/Domain Assets/Game/Infrastructure Assets/Game/Presentation Assets/Game/Resources Assets/Game/Art/Config docs
git diff --cached --check
```

The scripts copy/remap assets and edit source data, not execute the game. Subsequent material corrections and
source changes were applied directly; the admission script is a work artifact, not an idempotent final pipeline
(rerunning it alone would overwrite the copied material corrections). The first staged whitespace review found
trailing spaces in the copied vendor shader; those spaces were removed from the project copy. The final static
whitespace review returned 0. No vendor original was reformatted.
Unity/batchmode/test/build commands executed: **none**. No functional/performance/visual pass is claimed.

Before eventual acceptance, reconcile prior tests that assumed seven wildlife species, old demo hand props,
the original crop catalog and generic Tier1 crowd LOD. Then verify peasant/ruler succession and load, tool
attachments, crop ownership/plant/grow/harvest/haul/load, continuous lake outlets and water collision, missing
materials, removed monolith and actual runtime cost. Do not run these while the user's prohibition remains.

Inherited changes are broader than the former seven-material list: new Polytope imports and many vendor
material/meta edits were present at entry. `Artifacts/WM15Content/baseline-status.txt` records that baseline;
none of those inherited paths or `New Unity Project/` belong in this commit.
