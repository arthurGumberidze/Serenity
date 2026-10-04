# U06 — Character Domain Tier 1

## Scope and preconditions
Work continued from `268c1cd24c7a5d47d8a89db3a80339cf8165de46 U05A: establish licensed Stone Age asset pipeline`. That commit exists and was HEAD before U06; U05A was DONE, `NEXT_TASK` selected U06, and U06 had not started. The successful pre-change baseline was 97 EditMode, 25 PostgreSQL integration and 2 PlayMode tests plus Windows x64 Mono Development build. A first sandboxed Unity launch crashed inside Mono `HttpListener` initialization before compilation; the identical baseline passed outside the sandbox, so this was recorded as a launch-environment failure rather than a project regression.

U06 implements only Character Domain foundation. No Character GameObject, prefab binding/spawning, Animator, NPC card, AI, needs, navigation, work scheduling, inventory, pregnancy, genetics, dynasty/succession, combat, tier switching, DOTS system, SQL repository or save UI was added. The old task-graph NPC-card wording is deferred with presentation to U07/U27 in accordance with the explicit domain-only scope.

## Aggregate and identity
`Game.Domain.Characters.Character` is a sealed ordinary C# domain entity in the existing `Game.Domain` assembly. It owns no Unity, scene, asset, ECS or persistence-provider reference. Its stable `Id` has no setter and never changes during rename, skill, profession, health, social-value, family or lifecycle changes.

`Character.CreateNew(CharacterCreationData)` is the only U06 character path that calls `StableEntityId.NewId`. All non-identity inputs are supplied explicitly; construction performs no name/sex/trait/skill/parent randomization. `Character.Restore(CharacterState)` instead requires a valid existing ID and never regenerates it. Invalid/empty restored identity is rejected.

`CharacterState` is a detached data-only snapshot containing every U06 field. Collection inputs are copied, capture uses deterministic stable-ID/definition-key ordering, and restore revalidates all invariants. It is suitable as input to a future explicit save DTO/codec and to Tier 1/2/3 projections. U06 intentionally leaves U04 `SaveSnapshot` format version 1 and PostgreSQL migration 001 unchanged; character/world persistence will require an explicit schema evolution task.

## Personal state
- `CharacterName` contains a required given name and optional family name. Parts are canonical printable text, maximum 64 characters, with no leading/trailing whitespace. Names are not identity and duplicates across characters are allowed.
- `CharacterSex` is the FRS biological `Male`/`Female` value needed by future reproduction rules; invalid enum values are rejected.
- `CharacterLifeState` is `Alive` or `Dead`. Death requires a biological tick at or after birth and cannot be applied twice.
- `TraitId`, `SkillId` and `ProfessionId` are typed canonical definition keys (`a-z`, digits, `.`, `_`, `-`, up to 64 characters) that can be resolved from future data-driven definitions without placing ScriptableObject references in Domain.
- A Full NPC has four or five unique traits. Skills are an extensible ID-to-`SkillState` map with levels 0–100. Intelligence and physical strength use typed 0–100 values; U06 adds no genetics algorithm.
- `BodyHealth` covers the FRS minimum head, torso, left/right arm and left/right leg with bounded 0–100 values. Disease, injury, infection, pain and treatment systems are deferred.
- `Wealth` and `Influence` are independent 0–1000 values following the FRS ranges. Property/business/office/dynasty formulas and effects remain U32 work.
- Profession is optional and referenced by typed definition ID. Work assignment and scheduling are not implemented.

## Biological age
`BiologicalBirthTick` is the only canonical age source. `AgeInCompletedYears(currentBiologicalTick)` divides elapsed U03-compatible biological `TimeSpan` ticks by a fixed 365-day biological year. It handles newborns, exact birthday boundaries and `long.MaxValue`, rejects a current tick before birth, and never reads calendar time, `DateTime`, Unity `Time` or a private clock. Dead characters use their death tick, so age cannot continue after death. Mass aging/mortality scheduling remains future Simulation work; there is no per-character `Update` or timer.

## Family, spouse and relationships
Parentage is stored only on the child as `ParentLink(ParentId, Role, Kind, IsKnownToCharacter)`. Stable IDs permit parents to be dead, off-tier, unloaded or absent from the active scene. `Mother`/`Father` and `Biological`/`Legal`/`Adoptive` distinctions allow a hidden biological father and a different known legal father. Self-parent links and duplicate role/kind slots are rejected.

Children are not stored as a second mutable list. `CharacterRegistry.GetChildren(parentId)` derives them from canonical child links in deterministic ID order. Optional `FamilyId` and `DynastyId` reuse `StableEntityId`; no second ID system or speculative dynasty aggregate was created.

Current spouse is an optional stable ID. `CharacterRegistry.LinkSpouses` prevalidates both characters and updates both aggregates symmetrically; self-marriage and replacement of an existing different spouse are rejected. `UnlinkSpouses` requires a mutual link. Marriage history, remarriage rules, divorce and deceased-spouse history remain U17/family-event work.

The minimal relationship foundation is a deterministic map from other `StableEntityId` to bounded affinity `-100..100`. Invalid, self and duplicate restored targets are rejected. Love, hatred, jealousy, secrets, affairs and event logic are intentionally not implemented.

## Registry and tier compatibility
`CharacterRegistry` is a session-owned in-memory index, not a global singleton or PostgreSQL repository. It provides add/remove/get/try-get, stable-ID-sorted enumeration, derived child lookup and symmetric spouse operations. Adding a second active character with the same ID fails without replacing the first.

The aggregate contains no tier flag or presentation handle. A future Tier 1 GameObject, Tier 2 Entity component set or Tier 3 persistent-person record can be destroyed/rebuilt around the same `CharacterState` and `StableEntityId`. Aggregate statistical citizens do not have to allocate full `Character` objects; U06 only defines persistent/full-person state.

## Independence and deferred work
`Game.Domain` still has no engine references and no new assembly edge. Character files reference only framework namespaces and existing `StableEntityId`. They contain no `UnityEngine`, `MonoBehaviour`, `GameObject`, `Transform`, `Animator`, prefab/asset GUID, `Entity`, Npgsql, SQL or database schema type.

Deferred: U07 presentation/binding/animation; U10 needs/AI; U11 work assignment; U12/U13 DOTS and tier transitions; U17/U18 dynasty, marriage history, reproduction, genetics, mortality, disease and injury gameplay; U27 UI; U32 Wealth/Influence effects; world snapshot/codec and normalized character persistence schema.

## Automated tests
`CharacterDomainTests` has 29 NUnit cases covering:

- valid/distinct new IDs, exact restore identity and identity immutability;
- complete capture/restore and detached collection inputs;
- name, sex, birth/lifecycle and invalid-ID rejection;
- newborn, birthday, future-tick, very-large-tick and death-frozen age;
- optional parents, self-parent rejection, duplicate parent slots and hidden biological versus legal father;
- duplicate active ID, deterministic children, symmetric spouse link/unlink and self-spouse rejection;
- four/five unique traits, canonical definition IDs, deterministic skills and bounds;
- invalid/duplicate/self relationships and score bounds;
- six body parts, attributes, Wealth/Influence bounds and optional profession/family/dynasty hooks.

Inherited `ArchitectureTests` continue to verify that Domain and Simulation contain no UnityEngine/UnityEditor references and that assembly edges remain exact and acyclic.

## Validation commands and results
Primary final command from `C:\serenity_game`:

```powershell
./Tools/Verify-U06.ps1 -ManagedTestCluster
```

It invokes the full U05A gate and then the focused U06 category. The effective Unity commands remain:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U05AAssetFoundation.Build
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults C:\serenity_game\Logs\U04A-core-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory PostgresIntegration -testResults C:\serenity_game\Logs\U04A-postgres-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testResults C:\serenity_game\Logs\U05-playmode-tests.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -quit -executeMethod Game.Infrastructure.Editor.U05AAssetFoundation.Validate
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory U06 -testResults C:\serenity_game\Logs\U06-tests.xml
```

Final results:

- EditMode excluding PostgreSQL: 126 passed, 0 failed, 0 skipped in 1.5383799 seconds;
- focused U06 EditMode: 29 passed, 0 failed, 0 skipped in 0.1969921 seconds;
- real PostgreSQL integration: 25 passed, 0 failed, 0 skipped in 17.1574365 seconds;
- PlayMode: 2 passed, 0 failed, 0 skipped in 0.22748 seconds;
- Windows x64 Mono Development: success, errors=0, warnings=2; `Serenity.exe` 667136 bytes;
- asset generation/validation: success; `LocalGameplay` remains the only enabled player scene;
- final logs contain no C# compiler warning/error, failed assertion, missing-reference/script or shader-error match. The two inherited build warnings are unchanged from U05A.

`git diff --cached --check` passed for the U06 allow-list, and staged-path inspection found no vendor pack, `_Recovery`, TutorialInfo, unrelated ProjectSettings, staging tree or credential file. The repository-wide unstaged `git diff --check` still reports two pre-existing user-owned whitespace errors: `Assets/TutorialInfo/Editor/ReadmeEditor.cs:14` and `ProjectSettings/EntitiesClientSettings.asset:13`; neither file is staged or modified by U06. The final allow-list excludes all pre-existing vendor imports, `_Recovery`, TutorialInfo, unrelated ProjectSettings, reference/update documents, local credentials, restored NuGet packages and generated Logs/Builds.
