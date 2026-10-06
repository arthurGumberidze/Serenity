# U16 HANDOFF — Research Framework and Stone Age

Date: 2026-10-06  
Status: Implementation DONE; required repository commit is pending because the current environment denies `.git` writes.

## 1. Summary

U16 adds one reusable, engine-independent settlement research authority and a complete, traversable 14-technology Stone Age branch. Starting content is deliberately restricted; prerequisites and deterministic simulation work unlock the existing U08 buildings, U15 production recipes and typed capability flags. Research survives save/load and follows the same canonical result through Tier 1, Tier 2 and Tier 3.

## 2. Architecture

The dependency path is `TechnologyCatalog -> ResearchService -> ResearchUnlockSet -> existing construction/production availability checks`. Domain owns definitions and detached state. Simulation owns active work, exact completion and unlock derivation. Infrastructure composes the real U08/U15 IDs. Presentation owns only the development overlay. There is no research singleton, SQL loop, copied tier state, technology GameObject or per-researcher `Update`.

## 3. Files added and modified

Added:

- `Assets/Game/Domain/Research/ResearchModels.cs` and folder/file metadata;
- `Assets/Game/Simulation/Research/ResearchService.cs` and metadata;
- `Assets/Game/Presentation/Research/ResearchDebugOverlay.cs` and metadata;
- `Assets/Game/Tests/Editor/ResearchFrameworkTests.cs` and metadata;
- `Assets/Game/Tests/PlayMode/ResearchPlayModeTests.cs` and metadata;
- `Tools/Verify-U16.ps1`;
- `docs/STONE_AGE_TECH_TREE.md`;
- `docs/U16_HANDOFF.md`.

Modified:

- domain/save: `Tier1AiState.cs`, `SaveSnapshot.cs`;
- AI and services: `HaulWorldQuery.cs`, `Tier1AiScheduler.cs`, `UtilityActions.cs`, `ProductionService.cs`, `ConstructionFundingService.cs`, `SaveCoordinator.cs`;
- tiers: `Tier2Materializer.cs`, `Tier2Runtime.cs`, `Tier3CharacterAdapter.cs`;
- infrastructure/persistence: `LocalSceneCompositionRoot.cs`, `SaveXmlCodec.cs`, `PostgresSaveStore.cs`;
- regression tests: `PersistenceTests.cs`, `BuildingPlacementPlayModeTests.cs`, `ProductionPlayModeTests.cs`, `ResourceGameplayPlayModeTests.cs`;
- protocol/docs: `PROJECT_STATE.md`, `TASK_GRAPH.md`, `NEXT_TASK.md`, `DECISIONS.md`, `ARCHITECTURE.md`.

No scene, prefab, material, vendor asset, SQL migration or package dependency was added.

## 4. Technology IDs

`basic_survival`, `gathering`, `controlled_fire`, `shelter_construction`, `stone_tools`, `fiber_working`, `woodworking`, `hunting`, `food_preservation`, `organized_labor`, `primitive_agriculture`, `weaving`, `advanced_stonecraft`, `stone_age_mastery`.

## 5. Complete prerequisite tree

```text
basic_survival [start]
├─ controlled_fire ───────────────┬─ food_preservation ───────────────┐
└─ gathering [start]              │                                   │
   ├─ shelter_construction ───────┐│                                   │
   ├─ fiber_working ──────────────┼┼─ primitive_agriculture ──────────┤
   │                              └┼─ organized_labor ── weaving ─────┤
   └─ stone_tools ─┬─ woodworking ┘│                                   │
                   ├─ hunting ─────┘                                   │
                   └─ advanced_stonecraft ─────────────────────────────┤
                                                                       └─ stone_age_mastery
```

The exact multi-parent prerequisite table and costs are canonical in `docs/STONE_AGE_TECH_TREE.md`.

## 6. Starting technologies

Only `basic_survival` and `gathering` are completed for a new research state. Their derived unlocks are capability `basic_survival` and recipes `gather_berries` / `gather_fiber`. Shelter, storage, wood/stone gathering, crop plots and cultivated berries are initially locked.

## 7. Unlock mapping

| Technology | Unlock |
|---|---|
| `basic_survival` | capability `basic_survival` |
| `gathering` | recipes `gather_berries`, `gather_fiber` |
| `controlled_fire` | capability `controlled_fire` |
| `shelter_construction` | building `primitive_shelter` |
| `stone_tools` | recipes `gather_stone`, `chop_wood` |
| `fiber_working` | capability `fiber_working` |
| `woodworking` | building `storage_basket` |
| `hunting` | capability `hunting` |
| `food_preservation` | capability `food_preservation` |
| `organized_labor` | capability `organized_labor` |
| `primitive_agriculture` | building `primitive_crop_plot`, recipe `cultivate_berries` |
| `weaving` | capability `weaving` |
| `advanced_stonecraft` | capability `advanced_stonecraft` |
| `stone_age_mastery` | capability `stone_age_completed` |

Definitions remain present in the existing catalogs. `ProductionService` ignores locked recipes. `ConstructionFundingService` returns explicit `LockedByResearch`. Isolated tests can omit an unlock view to retain an intentional unlocked sandbox.

## 8. Research progress design

There is one active project for the settlement. It stores technology ID, accumulated/required integer work ticks and start tick. Assigned workers store an absolute last-processed tick and rate remainder. Work uses fixed-point rate-per-thousand arithmetic, no wall clock or RNG. Exact completion derives a reproducible tick and commits once; stale lower tier checkpoints do nothing instead of rewinding or duplicating work.

## 9. Research worker/job integration

Assigned character stable IDs contribute only while their centralized action is Research. The default character hook is `750 + Intelligence*5 + ResearchSkillLevel*5` per thousand, so Intelligence 50 and no skill is rate 1.0. This is deliberately a hook rather than a new RPG balance system. Start, cancel, unassign and changed-worker paths preserve deterministic checkpoints.

## 10. Tier 1 path

Research is an ordinary U10 Utility candidate/action with Hunger/Energy-aware score and normal interruptibility. `Tier1AiScheduler` remains the only AI scheduler and advances all assigned researchers once per explicit game-time advance. The action owns no duplicate progress and adds no MonoBehaviour.

## 11. Tier 2 path

`Tier2Runtime` completes its existing ECS job barrier, reads active stable IDs from `Tier2Materializer` and advances the same `ResearchService`. It creates no research GameObjects or ECS authority and never persists Entity handles.

## 12. Tier 3/off-camera path

`Tier3CharacterAdapter` advances assigned active Tier 3 stable IDs to the current absolute U03 tick through the same service. Catch-up uses no second clock and no tier-specific state. A stale call after a transition is an idempotent no-op.

## 13. Save/load changes

`ResearchSaveState` persists sorted completed technology IDs, optional active project and exact progress, assigned stable worker IDs, each fractional remainder/checkpoint, and the last processed research tick. Restore validates catalog IDs, prerequisites, starting knowledge, project cost/progress and worker uniqueness before commit. Unlocks are reconstructed from completed technologies and the catalog, so there is no redundant mutable unlock truth or reroll.

## 14. Snapshot version

`SaveSnapshot.CurrentVersion` changed from 2 to 3 because durable research state is a real wire-format addition. Version 1 time-only and version 2 production saves remain readable; version 3 stores production plus optional research. Unknown version 4 is rejected in regression coverage.

## 15. PostgreSQL

No schema migration was required. PostgreSQL remains an opaque durable snapshot store, now accepting validated metadata versions 1, 2 and 3. It performs no realtime research query. Managed-cluster integration passed 25/25.

## 16. Tests added

Twelve U16 EditMode tests cover definition/catalog validation, starting knowledge, partial/exact-once progression, typed production/building/capability gating, assignment/cancel/rate hooks, save/restore/no double completion, deterministic and stale checkpoint routes, Tier 1 Utility AI, complete graph/capstone traversal, 20-person two-year U15+U16 integration with crop operation, and 1,000-worker time/allocation observation. Two U16 PlayMode tests cover LocalGameplay composition/gating/overlay and equivalent Tier1/Tier2/Tier3 completion.

## 17. Exact test counts and results

Final validation matrix:

- U16 EditMode: 12 passed, 0 failed, 0 skipped;
- U16 headless PlayMode: 2/2; GPU PlayMode: 2/2;
- full non-PostgreSQL EditMode: 233/233;
- full PlayMode: 39/39;
- U08: 8/8 EditMode, 3/3 PlayMode;
- U09: 10/10 EditMode, 4/4 PlayMode;
- U10: 14/14 EditMode, 5/5 PlayMode;
- U11: 12/12 EditMode, 10/10 PlayMode;
- U12: 11/11 EditMode, 2/2 PlayMode;
- U13: 9/9 EditMode, 2/2 PlayMode;
- U14: 14/14 EditMode, 2/2 headless and 2/2 GPU PlayMode;
- U15: 13/13 EditMode, 3/3 headless and 3/3 GPU PlayMode;
- focused persistence: 35/35;
- PostgreSQL integration: 25/25;
- U05A asset/catalog validation: passed.

## 18. Stone Age end-to-end result

The engine-independent progression test repeatedly chooses the next available technology, validates that the graph never deadlocks, completes all 14 definitions, observes every typed unlock at its defined completion and reaches `stone_age_mastery`. `StoneAgeCompleted` and capability `stone_age_completed` are true; no next era starts.

## 19. Tier route equivalence

Equal initial state, worker/rate, decisions and six elapsed game days were executed as one aggregate route, split Tier 3→Tier 2→Tier 3 checkpoints and split Tier 3→Tier 1→Tier 3 checkpoints. All produced identical project/completion/unlock state and completion tick.

## 20. Deterministic hashes/results

- tier-route research hash: `A9022738CC9580C0`;
- 20-person/730-day research hash: `B0B088B38CCE9E01`;
- matching production hash: `F27F0B25EC99953B`;
- 1,000-worker scale hash: `E135E16085E27656`.

## 21. U15 + U16 long-run integration

The baseline uses population 20, 20 starting berries, separate research/production workers, finite berry/fiber/tree/stone sources, one primitive crop plot, target stocks and 730 game days. Research reached the capstone; agriculture unlocked and the crop plot entered its lifecycle; berries were produced/consumed; wood, stone and fiber production remained active; final food shortage was 0; no quantity became negative and no research deadlock/duplicate completion occurred.

## 22. Performance/allocation observation

One centralized service advanced 1,000 assigned worker IDs through five daily batches in 8 ms on the final focused run, with 0 managed bytes reported by `GC.GetAllocatedBytesForCurrentThread` for the measured interval and hash `E135E16085E27656`. This is an observation, not a U29 production performance budget. There are zero per-worker, per-technology and per-unlock update loops.

## 23. PlayMode, GPU and manual validation

Headless U16 PlayMode passed 2/2 and rendered GPU U16 PlayMode passed 2/2. They verify scene composition, visible overlay component, starting/locked availability, programmatic start/progress/completion, real building/recipe gating and tier paths. The Windows Computer Use helper failed twice with `trusted Node process exited unexpectedly`; after reset it failed with `windows sandbox failed: helper_unknown_error: setup refresh had errors`. Therefore no interactive manual-click claim is made.

## 24. Windows build

`U00Build.WindowsDevelopment` succeeded for Windows x64 Mono Development. `Builds/Windows/Serenity.exe` exists and is 667,136 bytes.

## 25. Console warnings/errors

Focused, full, PostgreSQL and GPU test XML reports contain zero failed/skipped required tests. The final BuildReport is `Succeeded; errors=0; warnings=2`; these are the same inherited BuildReport warnings without emitted C# warning detail. No compiler error, assertion failure, missing script/reference, runtime exception or shader error remained in the successful gates.

## 26. Documentation

Updated `PROJECT_STATE.md`, `TASK_GRAPH.md`, `NEXT_TASK.md`, `DECISIONS.md` and `ARCHITECTURE.md`; added this handoff and canonical `STONE_AGE_TECH_TREE.md`. U16 is DONE and U17 is named but remains not started pending review.

## 27. Diff hygiene

`git diff --check` and `git diff --cached --check` passed. The staged-file review contains only U16 implementation, tests, verifier and protocol documentation.

## 28. Commit

The single required commit is `U16: implement research framework and Stone Age technology tree`; its repository hash is reported in the final handoff message after commit.

## 29. Known limitations

- The research UI is a compact development IMGUI list, not U27 gameplay UI or a Civ-style tree.
- MVP supports one active settlement project and no research queue.
- Research is abstract settlement work with no physical knowledge site, laboratory, university, technology trading or research espionage.
- Capability flags are typed and available to later systems, but several are content markers with no additional U16 mechanic.
- Only Stone Age content exists; U38 owns later eras.
- The current LocalGameplay scene retains a development bootstrap storage basket for U10 hauling; new player construction requests are still research-gated.
- Interactive Windows clicking could not be executed because the helper failed as documented; headless and GPU PlayMode gates passed.

## 30. Definition of Done

Every implementation Definition of Done item passed: reusable/data-driven research and graph validation; working prerequisites and cycle/missing-ID rejection; deterministic game-tick progression; complete Stone Age content and capstone; real building/recipe/capability gating; restricted starting knowledge; Tier1/Tier2/Tier3 and off-camera routes with equivalence; durable v3 save/load with v1/v2 compatibility; end-to-end and long-run integration; U08–U15/persistence/PostgreSQL regressions; GPU scene validation; Windows build; and updated documentation. The only remaining protocol item is the requested clean commit, blocked by the environment's `.git` write ACL and exhausted approval review. The interactive helper failure is separately disclosed; no manual-click claim is made.

## Validation commands

Primary timeout-aware gate:

```powershell
& 'C:\serenity_game\Tools\Verify-U16.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -ManagedPostgres -GpuValidation -StepTimeoutSeconds 900
```

Explicit U08 and persistence regressions:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16-u08-editmode.log' -runTests -testPlatform EditMode -testCategory U08 -testResults 'C:\serenity_game\Logs\U16-u08-editmode.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16-u08-playmode.log' -runTests -testPlatform PlayMode -testCategory U08 -testResults 'C:\serenity_game\Logs\U16-u08-playmode.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U16-persistence.log' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.PersistenceTests' -testResults 'C:\serenity_game\Logs\U16-persistence.xml'
```
