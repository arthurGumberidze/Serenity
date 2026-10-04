# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development.

## Current milestone / last completed task
U07 DONE — GameObject Presentation Tier 1. Existing U06 characters can now be materialized as visible, animated, selectable GameObjects without moving identity or canonical state into Presentation.

## Active task
U08 — Строительная сетка и blueprint-система is next and has NOT started.

## U07 presentation foundation
- `CharacterPresenter` has explicit `Bind`/`Unbind`, exposes the bound `Character` and its existing `StableEntityId`, supports movement-speed and presentation-pause animation inputs, and has no `Update` loop.
- `CharacterPresentationRegistry` is session-owned and rejects two simultaneous Tier 1 views for one ID. Unbind, despawn and destruction remove only the ephemeral mapping; `CharacterRegistry` and the aggregate survive.
- `CharacterPresentationSpawner` receives an existing Character, catalog and world transform. It never calls `Character.CreateNew` or `StableEntityId.NewId`.
- `CharacterPresentationCatalog` is a ScriptableObject mapping Male to `Serenity_Adult_A_Tier1` and Female to `Serenity_Adult_B_Tier1`. The mapping is deterministic and is the replacement seam for final art and later appearance descriptors.
- Both project-owned wrappers have a root presenter/capsule, nested Hodaart source visual, valid Humanoid Animator/Avatar and bone-parented `RightHandSocket`/`LeftHandSocket`. The shared project controller defaults to Idle and has `Speed`/`Moving` locomotion parameters with idle/walk/run states.
- `SelectionProbe` now exposes a selected `CharacterPresenter` and `StableEntityId` while retaining U05 primitive marker behavior. Selection owns no domain entity.
- `LocalSceneCompositionRoot` explicitly constructs demo domain/presentation registries and spawner, creates deterministic male/female sample data through U06, and materializes two Tier 1 views. No scene-wide dependency lookup is used by runtime composition.
- Active pause behavior is explicit presentation-level Animator freeze/resume; it does not use `Time.timeScale`, advance GameClock or stop the unscaled RTS camera.

## Preserved architecture
- `Game.Domain` and `Game.Simulation` remain pure engine-free assemblies. No Domain file or project-layer dependency edge changed in U07.
- Canonical character identity/state remains in `Game.Domain.Characters.Character`; GameObjects, transforms, prefab references, Animator state and Unity instance IDs are not saved.
- U04 save format and PostgreSQL schema remain unchanged. Presentation contains no Npgsql, SQL or persistence-provider reference.
- Tier 2 DOTS, Tier 3 aggregation, AI, navigation, needs, jobs, inventory, equipment gameplay, combat, family simulation, third-person control and full character UI remain deferred.

## Assets and visual status
- Hodaart Character 01/02 remain licensed U05A class-C placeholder candidates, mapped male/female respectively. Vendor files and materials are not modified; Serenity wrappers and catalog hold all U07 integration.
- Runtime GPU validation shows both characters at approximately 1.8 m, grounded, correctly materialized and in Idle rather than T-pose, with no pink shader. Capsule colliders match the existing normalized wrappers.
- The current art has no authored LOD and is not final Stone Age clothing. Future replacement changes the catalog/wrapper, not Domain, save/load, dynasty or simulation.

## Validation status
`Tools/Verify-U07.ps1 -ManagedTestCluster` passed on 2026-10-05:

- 130/130 core EditMode tests passed;
- 25/25 real PostgreSQL integration tests passed;
- 6/6 full PlayMode tests passed;
- 29/29 focused U06 tests passed;
- 4/4 focused U07 EditMode tests passed;
- 4/4 focused U07 PlayMode tests passed;
- U05A generation/validation and missing-script/reference gates passed;
- Windows x64 Mono Development build succeeded with 0 errors and 2 inherited warnings; `Serenity.exe` is 667136 bytes;
- final searched logs contain no C# compiler error, missing-reference/script, assertion or shader-error diagnostic.

Exact commands and limitations are recorded in `docs/U07_HANDOFF.md`.

## Working tree
The U07 commit is allow-listed to U07 code, project-owned character wrappers/controller/catalog, LocalGameplay, tests, verifier and documentation. Pre-existing TutorialInfo edits, unselected vendor packs, `_Recovery`, unrelated ProjectSettings, reference/update files, NuGet metadata noise, Boar controller/gallery regeneration noise and local credentials remain uncommitted.

## Next
Start only U08 from `docs/NEXT_TASK.md`. U08 has not been implemented.
