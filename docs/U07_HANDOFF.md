# U07 — GameObject Presentation Tier 1

## Scope and preconditions
Work continued from `34de8ba5add2736a19fd8b81c67e1fdfd90c21f0 U06: implement character domain model`. The commit existed at HEAD, U06 was DONE, `NEXT_TASK` selected U07 and U07 had not started. U06 Domain remained free of UnityEngine dependencies. The U05A male/female wrappers loaded with valid humanoid Avatars, renderers, materials, colliders and no missing scripts/references.

Only Tier 1 GameObject presentation was implemented. U07 adds no AI, navigation, needs, jobs, work scheduling, inventory, equipment gameplay, combat, reproduction, genetics, dynasty, mortality simulation, Tier 2/3 implementation, third-person control, full character UI or PostgreSQL character repository.

## Presenter and identity
`CharacterPresenter` is a presentation MonoBehaviour on the Serenity wrapper root. `Bind(Character, CharacterPresentationRegistry)` accepts an already existing U06 aggregate, registers its current `StableEntityId`, caches animation parameters and starts with zero movement speed. `Unbind` removes the view mapping and clears only presentation references. The presenter never calls `StableEntityId.NewId`, never creates a Character and contains no serialized canonical character fields.

`OnDestroy` calls `Unbind`, so direct GameObject destruction, explicit spawner despawn and scene unload all release the presentation mapping. Tests prove the same aggregate remains in `CharacterRegistry` and a replacement presenter binds the identical ID. Unity instance IDs are never treated as persistent identity.

## Registry and spawner
`CharacterPresentationRegistry` is a regular session-owned C# object. It maps stable character IDs to active presenters, rejects a simultaneous duplicate with a controlled exception and unregisters only the matching presenter. It is not static, not a singleton, not persistent and not the U06 `CharacterRegistry`.

`CharacterPresentationSpawner` receives a catalog, presentation registry and optional parent. `Spawn` resolves the prefab from the supplied existing Character, instantiates at the supplied world position/rotation and binds it. Catalog/configuration or prefab failures are explicit exceptions; no silent empty or random fallback is created. `Despawn` unbinds before destroying the GameObject.

## Catalog and placeholder mapping
`CharacterPresentationCatalog` is a Presentation ScriptableObject stored at `Assets/Game/Art/Config/CharacterPresentationCatalog.asset`. It deterministically maps:

- `CharacterSex.Male` → `Serenity_Adult_A_Tier1` → approved Hodaart Character 01;
- `CharacterSex.Female` → `Serenity_Adult_B_Tier1` → approved Hodaart Character 02.

Exactly one non-null presenter prefab is required per supported sex. There is no spawn-time randomness because U06 has no persistent appearance descriptor yet. Future faces, bodies, hair, clothing, age/profession/ethnicity variants and equipment can extend this presentation descriptor/catalog without changing Character, StableEntityId, save/load, dynasty or simulation. The current models remain U05A class-C temporary placeholders under the existing Asset Store license record.

## Prefabs, Animator, collider and sockets
Each project-owned wrapper has this effective structure:

```text
Serenity_Adult_*_Tier1 (CharacterPresenter, CapsuleCollider)
└─ SourceVisual (Hodaart nested prefab, Humanoid Animator/Avatar)
   └─ humanoid bones
      ├─ RightHandSocket
      └─ LeftHandSocket
```

The project-owned `CharacterTier1.controller` has Idle as its default state plus Walking and Running. Parameters are `Speed` (float) and `Moving` (bool); U07 drives only presentation state and adds no movement/navigation logic. Root motion is disabled. `AlwaysAnimate` prevents nearby Tier 1 characters from remaining in bind pose before their first visible frame. Runtime validation and a humanoid-bone assertion prove Idle lowers the hands rather than leaving a T-pose.

Existing wrapper normalization remains approximately 1.8 m with feet on Y=0. The root capsule remains 1.8 m high and is suitable for raycast selection/future collision; no Rigidbody or MeshCollider was added. Hand sockets follow their humanoid bones but no inventory, weapon attachment or equipment state is implemented.

## Selection, composition and lifecycle
`SelectionProbe` still selects the three U05 `SelectableMarker` objects, and now also detects a `CharacterPresenter` through the hit collider hierarchy. It exposes the selected presenter and stable ID as disposable UI/control state. Clearing/destroying selection cannot delete Domain state.

`LocalSceneCompositionRoot` holds an explicit serialized catalog reference. In the development scene it constructs separate domain and presentation registries plus the spawner, creates one male and one female through `Character.CreateNew`, and spawns them at fixed positions. The samples are demonstration data only. Runtime code does not use `FindObjectOfType`, `GameObject.Find`, service locators or per-character `Update` loops.

The verified lifecycle is: Character X exists → spawn/bind presenter → destroy/despawn view → Character X remains in `CharacterRegistry` → spawn a new view → new presenter reports the same X. Scene objects remain disposable and recreateable for future tier transitions/unload.

## Pause and time
The presenter has `SetSimulationPaused(bool)`, which sets only its Animator speed. The chosen U07 rule is that character animation freezes during active simulation pause while the U05 camera remains responsive through unscaled time. No global `Time.timeScale` change is used. Presenter animation does not read or advance GameClock, calendar ticks or biological ticks. A later session pause coordinator will call this API; U07 does not create that coordinator.

## Automated tests
EditMode U07 tests cover binding and ID exposure, unbind/rebind, duplicate rejection, domain survival, deterministic male/female catalog mapping, invalid/missing catalog entries, presenter/Animator/Avatar/controller/renderer/capsule/socket prefab requirements and identity preservation.

PlayMode U07 tests cover male/female scene spawning, domain-to-view binding, valid humanoid Animator, actual Idle state and non-bind-pose hand position, renderer/capsule/ground root, explicit despawn, direct destruction cleanup, same-ID respawn and pointer selection returning the bound StableEntityId. Existing U05 PlayMode tests continue to cover RTS camera composition and marker raycast selection.

The U05 build-scene dependency test was narrowed from the pre-U07 rule “no vendor dependency” to “only approved vendor dependencies”: every unrelated vendor pack remains forbidden, and the only allowed Hodaart runtime subset excludes Documents, Packages, Scene, Scripts and Characters 03–10.

## Manual visual validation
A GPU-enabled PlayMode validation rendered `LocalGameplay` with both demo characters. Inspection confirmed:

- male and female models are visible at comparable human scale;
- feet contact the ground and roots are at Y=0;
- both evaluate Idle with arms lowered, not T-pose;
- source materials render correctly with no pink shader;
- capsule-based raycast selection returns the expected stable ID;
- the existing ground, selection markers and RTS camera remain present.

Automated lifecycle tests confirm destruction does not delete the Character and respawn preserves its ID. Test/build logs contain no new runtime errors.

## Validation commands and results
Primary final command from `C:\serenity_game`:

```powershell
./Tools/Verify-U07.ps1 -ManagedTestCluster
```

It ran the U07 generator, the entire inherited U06 gate and focused U07 suites. Effective commands include:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U07PresentationFoundation.Build
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults C:\serenity_game\Logs\U04A-core-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults C:\serenity_game\Logs\U04A-postgres-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testResults C:\serenity_game\Logs\U05-playmode-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U05AAssetFoundation.Validate
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory U06 -testResults C:\serenity_game\Logs\U06-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory U07 -testResults C:\serenity_game\Logs\U07-editmode-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testCategory U07 -testResults C:\serenity_game\Logs\U07-playmode-tests.xml
```

Final results on 2026-10-05:

- core EditMode excluding PostgreSQL: 130 passed, 0 failed, 0 skipped in 1.3938761 seconds;
- real PostgreSQL integration: 25 passed, 0 failed, 0 skipped in 16.2439658 seconds;
- full PlayMode: 6 passed, 0 failed, 0 skipped in 0.4217231 seconds;
- focused U06 EditMode: 29 passed, 0 failed, 0 skipped in 0.1575998 seconds;
- focused U07 EditMode: 4 passed, 0 failed, 0 skipped in 0.135607 seconds;
- focused U07 PlayMode: 4 passed, 0 failed, 0 skipped in 0.3988627 seconds;
- Windows x64 Mono Development: success, errors=0, warnings=2; `Serenity.exe` 667136 bytes;
- U05A asset generation/validation: success; `LocalGameplay` remains the only enabled player scene;
- final searched logs: no C# compiler error, missing-reference/script, failed assertion, runtime exception or shader-error diagnostic.

The two build warnings are inherited from earlier tasks. No Domain file, save format, SQL migration, PostgreSQL provider or vendor asset was changed by U07.

## Deferred work
U08 is the next unblocked task and has not started. Final Stone Age character art/appearance persistence, object pooling policy at scale, session-wide pause fan-out, navigation/AI, equipment visuals/gameplay, Tier 2/3 transitions and third-person control remain assigned to later tasks.
