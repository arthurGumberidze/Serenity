# U05A — Asset Pipeline + Free Asset Acquisition

## Scope and preconditions
Work continued from `95d339f U05: implement local RTS scene and input`. Before any U05A change, that commit was confirmed at HEAD, U05 was DONE, `NEXT_TASK` selected U05A, U05A was still TODO, and the full inherited U05 baseline passed: 91 EditMode, 25 PostgreSQL integration, 2 PlayMode, and Windows Mono Development build. Existing vendor imports, `_Recovery`, TutorialInfo, unrelated ProjectSettings, reference/update documents and staging files were treated as user-owned dirty-tree content, not as an implicit commit set.

Only the U05A asset foundation is implemented. No Character Domain, genetics, aging, dynasty, AI, needs, inventory, crafting, building gameplay, combat, NPC pathfinding, third-person control, tier switching, world generation or global map work was started.

## 1–5. Inventory, licenses and visual classification
The actual top-level staging inventory was: `3D Retro Medieval Fantasy Kit`, `CaveManCreation`, `FBX_LP_NaturePack_V2_1`, `FBX_Stoneage_Weapons_V1_1`, `FBX_Stoneage_WildHunt_V1_1`, `Free Modular Medieval Assets`, `FreeSurvivalKit`, `Low Poly Tools`, `model/`, root `medievalsurvivalprops.blend`, and root `model.glb`.

Existing Unity vendor packages considered were EmacEArt Slavic World Free, EmbersStorm Free Nature Pack, the incomplete EmbersStorm Magic Fantasy Armors import, Hodaart Low Poly Character Collection 3, Medieval Fortification, PolyOne Free Modular Terrain, Stylized Nature Environment, SunboxGames Avatars, Toony Tiny RTS, TriForge Top Down Fantasy Village, GanzSe Free Modular Character Pack, and Vefects Vexa. They were not moved or rewritten.

`docs/ASSET_REGISTRY.md` is the authoritative package-by-package record. Main decisions are:

- Hodaart Low Poly Character Collection 3: Standard Unity Asset Store EULA confirmed from the official listing; grade C / placeholder-only. Only Characters 01 and 02 plus their exact material, atlas, controller and animation dependencies are selected.
- WizardHat Stoneage Weapons and Stoneage Wild Hunt: local CC0 license texts plus official creator pages; grades B/C. Selected for runtime.
- WizardHat Stoneage Nature: official creator page explicitly permits commercial use and modification; grade B/C. Selected files only.
- Free Modular Medieval Assets: local CC0 confirmed, but grade C/D and era-mismatched; reference only.
- `CaveManCreation`, `FreeSurvivalKit`, `Low Poly Tools`, both local GLBs, `3D Retro Medieval Fantasy Kit`, `medievalsurvivalprops.blend`, and all Unity imports without completed proof remain `LICENSE_REVIEW_REQUIRED` for shipping use.
- The two GLBs were inspected, not guessed: `model/model.glb` is a Stone Field Hut; root `model.glb` is a Fibre and Hide Workshop containing a loom, hide frame, hamper, cordage spool and related props. Their `3dassets.dev ingest` generator metadata is not license evidence, so both remain quarantined.

The A/B/C/D/X visual classifications are recorded in the registry. The primary target remains semi-realistic stylized PBR mid-poly. Cartoon, fantasy and medieval packs remain placeholders/references rather than the Stone Age baseline.

## 6–10. Approved runtime set and selected source files
The approved MVP foundation is deliberately small:

- male Tier 1 candidate: Hodaart Character 01, wrapped as `Serenity_Adult_A_Tier1`;
- female Tier 1 candidate: Hodaart Character 02, wrapped as `Serenity_Adult_B_Tier1`;
- one project-owned single-mesh Tier 2 humanoid placeholder;
- Stone Axe, Stone Spear and Torch from Stoneage Weapons;
- animated boar from Stoneage Wild Hunt;
- Tree 01, Rock 01 and Campfire 01 from Stoneage Nature;
- project-owned primitive shelter and storage basket placement placeholders.

The Hodaart set provides a valid Humanoid skeleton and one shared atlas, but no Stone Age clothing baseline. GanzSe contains hair/facial-hair and modular fantasy parts but is not approved for runtime. Primitive clothing, hair/beard variants, sandals, belts and period jewelry remain gaps.

Exactly seven FBX files were copied from staging into Unity, plus the two relevant local license files and three source records:

- Nature: `Tree_01.fbx`, `Rock_01.fbx`, `Campfire_01.fbx`;
- Weapons: `StoneAxe.fbx`, `StoneSpear.fbx`, `Torch.fbx`;
- Wild Hunt: `Boar_Animations_V4.fbx`.

They live under `Assets/Game/Art/ThirdParty/WizardHatStudio/<package>`. The whole `serenity_games_assets` tree was not copied and is ignored at the repository root. No BLEND, GLB, UE5 content, vendor scripts, demo scenes, ProjectSettings or custom gameplay systems were imported.

## 11–15. Serenity prefabs and normalization
Serenity-owned runtime wrappers were created under `Assets/Game/Art/Prefabs`:

- Characters: `Serenity_Adult_A_Tier1`, `Serenity_Adult_B_Tier1`, `Serenity_Humanoid_Tier2`;
- Animals: `Serenity_Boar`;
- Buildings: `Serenity_PrimitiveShelter`;
- Environment: `Serenity_Tree`, `Serenity_Rock`;
- Props: `Serenity_Campfire`, `Serenity_StorageBasket`;
- Tools: `Serenity_Torch`;
- Weapons: `Serenity_StoneAxe`, `Serenity_StoneSpear`.

`U05AAssetFoundation` is an explicit editor command, not a broad `AssetPostprocessor`. It normalizes only the seven registered staging-derived FBXs. One Unity unit equals one metre. Target measurements are: characters 1.8 m height, Tier 2 humanoid 1.8 m, boar 1.8 m maximum dimension, tree 6 m height, rock 1.4 m maximum dimension, campfire/torch 0.8 m, axe 0.7 m and spear 1.8 m. FBX sizes are normalized at the importer. Hodaart vendor importers stay untouched; non-destructive wrapper scale brings both characters from about 2.15 m to 1.8 m.

Wrapper roots provide predictable identity orientation. Character, animal, tree, rock and campfire visuals are centered horizontally and aligned to ground; tools/weapons are centered for future attachments; shelter/storage use placement-friendly project-owned roots. No source mesh is destructively rewritten.

Selected staging-derived renderers use Serenity-owned URP Lit materials. Vendor Hodaart characters retain the package-provided URP material and shared 2K atlas. Validation found no pink shaders, missing materials, missing meshes or exploded rigs.

## 16–24. Rig, animations, categories and technical policy
Both Hodaart candidates have valid Human Avatars and SkinnedMeshRenderers. Their package controller exposes Idle, Walking, Running, Jump, Greeting, Happy Walk and two dances. Work, gather, attack, carry and death remain missing/need retargeting. The boar imports as Generic and exposes Attack, Idle1, Idle2, Run and Walk through a Serenity-owned controller.

Animal inventory is one boar; additional wildlife and death/carry/butchering coverage are missing. Weapon/tool inventory is axe, spear and torch; club, knife, bow/arrows, pick, hammer, scraper and bone needle remain gaps or license-blocked staging references. Nature inventory is one tree, rock and campfire; bushes, grass, logs and variants remain unselected. Building/prop inventory is a primitive shelter and storage proxy; the quarantined hut/workshop GLBs are not runtime content.

Colliders are deliberately simple readiness colliders: capsules for humanoids and boxes for boar/tree/rock. Handheld items and visual campfire have no automatic physics collider. Final interaction/hitbox policy belongs to later gameplay tasks.

No selected third-party asset has an authored LOD chain. Hodaart Characters 01/02 measure 6,112 and 5,252 triangles; the project Tier 2 placeholder is 832; boar is 2,983; tree 360; rock 80; campfire 1,984; torch 1,140; axe 1,186; spear 2,932. The selected source binary payload totals 3,691,525 bytes (3.521 MiB), dominated by the 2.1 MiB boar FBX and 0.52 MiB Hodaart atlas. No 8K texture was selected.

Addressables were not changed. The gallery scene uses direct editor references and is not a gameplay delivery/catalog overhaul. Later tasks can label final content after ownership and loading boundaries are known.

## 25–30. Gallery, validation, gaps and acquisition
`Assets/Scenes/Development/StoneAgeAssetGallery.unity` is a development-only validation scene and is excluded from Build Settings. It contains both human candidates, the Tier 2 placeholder, boar, three handheld items, shelter, storage, tree, rock and campfire under neutral lighting. `LocalGameplay` remains the sole enabled build scene and was not modified.

Manual GPU-rendered inspection covered overview, character/weapon and environment/building views. Results: human height and proportions are mutually consistent; all ground-contact objects are aligned; weapon, boar, tree, rock and shelter scales are plausible for readiness use; no pink material, missing mesh, exploded rig or obvious broken pivot was observed. The Hodaart figures and primitive shelter/storage are visibly placeholder-grade and are not presented as final art. The desktop UI automation helper was unavailable, so the inspection used Unity's actual GPU render path and the automated PlayMode run reopened/validated `LocalGameplay` behavior.

Six U05A EditMode tests cover required prefab/scene paths, loads, renderers/materials/missing scripts, human Avatar plus idle/walk/run coverage, boar controller/clips, scoped importer rules and sizes, Build Settings isolation, registry coverage and staging exclusion. The inherited 2 PlayMode tests revalidate `LocalGameplay` composition, Cinemachine, raycast/selection and marker count.

`docs/STONE_AGE_ASSET_GAPS.md` records READY, PARTIAL, MISSING, LICENSE_BLOCKED and STYLE_MISMATCH results. `docs/MANUAL_ASSET_DOWNLOADS.md` requests the official Stoneage Characters FBX (not UE5), provenance for both GLBs, a coherent primitive clothing/appearance set and additional wildlife. No CAPTCHA, account, marketplace claim or click-through was bypassed.

## 31–32. Source control and deferred work
The U05A commit is intentionally allow-listed. It includes only the exact Hodaart dependencies selected by the two wrappers; seven staging-derived FBXs; two local CC0 license files; source notes; Serenity materials/prefabs/controller; the gallery scene; editor tooling/tests/verifier; validation evidence; and U05A documentation. It excludes all other pre-existing vendor folders, `_Recovery`, TutorialInfo edits, unrelated ProjectSettings, addendum/update/reference files and the full staging tree.

Deferred work includes final semi-realistic Stone Age male/female art, modular primitive clothing and appearance, final shelter/building kit, crafting props, broader wildlife, work/combat/carry/death animation, authored LODs, final texture budgets, Addressables organization and every gameplay system outside asset readiness.

## Validation commands and results
Primary final command from `C:\serenity_game`:

```powershell
./Tools/Verify-U05A.ps1 -ManagedTestCluster
```

The verifier explicitly ran scoped asset generation, inherited U05 validation and the U05A asset tests. Underlying Unity commands use:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U05AAssetFoundation.Build
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults C:\serenity_game\Logs\U04A-core-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults C:\serenity_game\Logs\U04A-postgres-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testResults C:\serenity_game\Logs\U05-playmode-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U05AAssetFoundation.CaptureValidationScene
```

Final results: EditMode 97 passed, 0 failed, 0 skipped in 1.3318513 seconds; PostgreSQL integration 25 passed, 0 failed, 0 skipped in 15.727664 seconds; PlayMode 2 passed, 0 failed, 0 skipped in 0.2174304 seconds. Windows x64 Mono Development succeeded with errors=0 and warnings=2; player executable remains 667,136 bytes. No C# compiler error, missing-reference exception, missing-script diagnostic or shader error matched the final generation, capture, test or build logs. Build Settings still contain only `LocalGameplay`.
