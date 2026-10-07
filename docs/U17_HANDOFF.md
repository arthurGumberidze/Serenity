# U17 HANDOFF Dynasty and Succession

## 1 Summary

U17 is complete. The project now has a canonical stable-ID dynasty, deterministic male-only primogeniture, read-only family queries over U06 relationships, event-driven exactly-once succession, player-ruler transfer, extinction/Game Over, saveable history, tier-independent/off-camera behavior, v4 save/load and a click-blocked LocalGameplay debug overlay. U18 was not started.

## 2 Files added

- `Assets/Game/Domain/Dynasties/DynastyModels.cs`
- `Assets/Game/Simulation/Dynasties/FamilyTreeQuery.cs`
- `Assets/Game/Simulation/Dynasties/SuccessionService.cs`
- `Assets/Game/Presentation/Dynasties/DynastyDebugOverlay.cs`
- `Assets/Game/Tests/Editor/DynastyAndSuccessionTests.cs`
- `Assets/Game/Tests/PlayMode/DynastySuccessionPlayModeTests.cs`
- `Tools/Verify-U17.ps1`
- `docs/DYNASTY_AND_SUCCESSION.md`
- `docs/U17_HANDOFF.md`

Unity generated the matching `.meta` files and directory metadata.

## 3 Files modified

Character restore support, snapshot/codec/coordinator, LocalGameplay composition, legacy version assertions, LocalGameplay population expectation and required project protocol documents were updated. PostgreSQL migrations, research/production definitions and tier adapters were not changed.

## 4 Dynasty model and current monarch

`DynastyState` owns dynasty ID/name, founder, optional current monarch, member references, `agnatic_primogeniture_v1`, Active/Extinct status, processed-death markers and history. The one current monarch is an existing Character `StableEntityId`, never a presenter or copied person.

## 5 Family model

Parents and spouse remain on `Character`. `FamilyTreeQuery` derives parents, children, siblings and descendants. Saved missing parents/spouses/members, duplicate IDs, asymmetric spouses, self-links and cycles are rejected before restore.

## 6 Succession and exact ordering

Eligibility is: existing, alive, same dynasty, not the deceased monarch and male under the FRS base rule. Descendant branches come first, followed by sibling branches, ancestor/collateral branches and then remaining members by kinship distance. Every branch sorts by biological birth tick and then StableEntityId. A dead elder heir is skipped while living male descendants retain that branch's priority.

## 7 Death, exactly once and history

`SuccessionService.ProcessDeath` uses the existing Character life/death state. For a monarch it records one processed ID and one history entry before exposing the result. Repeated processing and save/load do not append history or change the chosen ruler. Entries contain sequence, previous/new monarch IDs, calendar tick and reason.

## 8 Player ruler and extinction

Successful succession changes `PlayerRealmState.CurrentRulerId` to the existing heir immediately. With no valid heir, the dynasty becomes Extinct, current ruler is cleared and `GameRunState` becomes GameOver with `DynastyExtinction`. No random replacement or recovery exists.

## 9 Tier behavior

Succession does not query tier or presentation. Tier1, Tier2 and Tier3 characters use the same Character identity/eligibility. Automated route coverage proves Tier3-only, T3-T2-T3 and T3-T1-T3 yield the same monarch/history. PlayMode proves a Tier3 heir becomes ruler while remaining unmaterialized as a GameObject.

## 10 Save load and PostgreSQL

`SaveSnapshot.CurrentVersion` is 4. The payload adds complete Character states, dynasty/history/processed markers, player ruler and game-run state. Versions 1, 2 and 3 remain readable. PostgreSQL remains an opaque payload store; no schema migration was needed.

## 11 Debug UI

The development overlay shows dynasty ID/name/status, monarch/life state, spouse, children, current heir, top five candidates and last succession. Debug-only Kill current monarch and Kill selected character use the canonical service. The panel registers with `WorldPointerRaycaster`, preventing selection, move/work or placement click-through.

## 12 Tests added

Thirteen U17 EditMode tests cover dynasty validation, family queries, eligibility, multiple children, dead-heir descendants, childless fallback, birth/ID ordering, exactly-once completion, extinction, collection-order independence, v1-v4 save/load, SaveCoordinator integration, corrupt data and tier-route equivalence. Two U17 PlayMode tests cover LocalGameplay overlay/blocking/succession/extinction and a Tier3 heir.

## 13 Validation results

- Focused U17 EditMode: 13 passed, 0 failed, 0 skipped.
- Focused U17 headless PlayMode: 2 passed, 0 failed, 0 skipped.
- Full non-PostgreSQL EditMode: 250 passed, 0 failed, 0 skipped.
- Full PlayMode: 44 passed, 0 failed, 0 skipped.
- U06/U13/U14/U16/U16A EditMode regressions: 68 passed.
- U13/U14/U16/U16A PlayMode regressions: 9 passed.
- Persistence: 35 passed.
- U17 GPU PlayMode: 2 passed on Direct3D 11, Intel Arc Graphics.
- Fresh managed SCRAM PostgreSQL: 250 core tests and 25 PostgreSQL integration tests passed, 0 failed; no schema migration was required.
- Windows x64 Mono Development build: succeeded with 0 errors and 2 inherited warnings. The managed PostgreSQL verifier repeated the build successfully.

## 14 Determinism results

Registration-order shuffle produces the same ordered candidate IDs. Equal birth ticks resolve by StableEntityId. The dead-elder branch test deterministically yields its living son before the younger sibling branch. All three tier routes produce the same current monarch and equal history fields.

## 15 Commands

Primary timeout-aware verifier:

```powershell
& 'C:\serenity_game\Tools\Verify-U17.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -GpuValidation -BuildPlayer -StepTimeoutSeconds 900
```

Managed PostgreSQL gate:

```powershell
& 'C:\serenity_game\Tools\Verify-U04A.ps1' -ProjectPath 'C:\serenity_game' -ManagedTestCluster
```

The semantic Unity commands use the pinned `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe` with `-batchmode`, project path, explicit log/result files and focused category/filter arguments.

## 16 Console and diff hygiene

Successful logs contain no C# compiler errors, failed assertions, unhandled runtime exceptions or missing scripts. Unity still emits the inherited access-token notice while resolving the Personal entitlement and inherited package/Entities notices. `git diff --check` and the staged review passed; the required `U17: implement dynasty and succession` commit is the final handoff action.

## 17 Known limitations

Only the base agnatic rule exists. Appointed heirs, disinheritance, adoption/legitimization policy, reproduction, diseases, injuries, natural/random death, production dynasty UI, reign summary and final Game Over UI remain future work. The two demo sons are canonical domain characters but begin without GameObjects; tier APIs may materialize them later.

## 18 Definition of Done

All implementation items are satisfied: canonical dynasty/current monarch, family queries, deterministic eligible-heir order, death-driven exactly-once transfer, dead-heir/fallback/extinction handling, player ruler and Game Over state, history, Tier1/2/3/off-camera independence, route equivalence, v4 save/load with v1-v3 compatibility, collection-order independence and debug UI/blocking. PostgreSQL, build and diff gates passed; the handoff commit is created immediately after this document is staged.
