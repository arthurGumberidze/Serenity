# Stone Age MVP Asset Gap Analysis

Checked on 2026-10-04 against the U05A acceptance set and the semi-realistic stylized PBR mid-poly target.

## Ready

- Pipeline separation: ignored external staging, selected third-party source imports, Serenity-owned runtime prefabs and a development-only gallery scene.
- Humanoid readiness: two licensed Humanoid Hodaart candidates with valid-avatar and idle/walk/run test gates; a separate single-mesh Tier 2 placeholder.
- Weapons/tools: Stone Axe, Stone Spear and Torch from the CC0 Stoneage Weapons pack.
- Hunt target: animated boar from the CC0 Stoneage Wild Hunt pack.
- Nature: one tree, one rock and one campfire from Stoneage Nature.
- Placement placeholders: project-owned primitive shelter and storage basket.

## Partial

- Characters: rig and locomotion coverage exists, but the selected Hodaart figures are cartoon placeholders, not the final Stone Age male/female visual baseline.
- Clothing and appearance: GanzSe has hair, facial hair and modular fantasy armor in the local tree, but is not approved as runtime Stone Age content. Primitive clothing, sandals and period jewelry remain unresolved.
- Animals: one boar with prototype animations is ready; species diversity, death/carry/butchering poses and authored LODs are absent.
- Nature: baseline tree/rock/campfire is ready, while bushes, grass, logs, stumps and seasonal variants remain staging-only.
- Buildings: shelter and storage placement contracts are represented, but final mid-poly hut geometry, doors, modular walls, palisades and workshops are absent.
- Crafting props: project-owned storage proxy is ready; drying rack, rope, baskets, nets, tanning frame and bone items need licensed art.
- Animation: human idle/walk/run is ready. Work, gather, attack, carry, death and tool-specific animation need retargeting/acquisition.

## Missing

- Production-quality Tier 1 Stone Age adult male and female models in the target semi-realistic stylized PBR style.
- Children and age variants (deferred beyond immediate U05A use).
- Primitive modular clothing, hair, beard, footwear, belts and bone/wood jewelry.
- Stone Age building kit suitable for later U08 modular construction.
- Bow draw/release and arrow attachment setup; scraper and bone needle assets.
- Animal species beyond boar and their full locomotion/combat/death coverage.
- Authored LOD chains for final characters, animals, buildings and vegetation.

## License blocked

- `CaveManCreation`: relevant characters, but no source/license evidence.
- `FreeSurvivalKit`: useful tent/campfire/tools, but its local license does not identify the author/source or acquisition record.
- `Low Poly Tools`: useful coverage but no license file/source evidence and substantial overlap with approved CC0 weapons.
- `model/model.glb`: identified as Stone Field Hut, source/license unknown.
- root `model.glb`: identified as Fibre and Hide Workshop with excellent crafting props, source/license unknown.
- `3D Retro Medieval Fantasy Kit`, `medievalsurvivalprops.blend` and most imported vendor packs: no complete shipping registry evidence yet.

## Style mismatch

- Toony Tiny RTS and several low-poly packs are too cartoon-like for the primary baseline; they remain Tier 2/placeholder references only.
- Medieval Fortification, Slavic World, Top Down Fantasy Village and Free Modular Medieval Assets are era-mismatched for Stone Age even when legally usable.
- Vexa and fantasy armor packs are more detailed but costume/theme-mismatched.

## Acquisition priority

1. Manually acquire the official Stoneage Characters FBX archive and preserve its license evidence.
2. Confirm or replace the local hut/workshop GLB provenance.
3. Acquire a coherent primitive clothing/hair/beard set on a documented Humanoid skeleton.
4. Add wildlife and human work/combat/carry/death animation coverage only after the first three gaps are resolved.
