# U15 HANDOFF — Production and Farming

Date: 2026-10-06
Status: DONE; U16 not started and awaiting review.

## Outcome

U15 adds one reusable, engine-independent production authority rather than a one-off farm. Stone Age gathering and a minimal cultivated crop plot use the existing U09 inventory/transfer ownership, U10 centralized Utility AI and movement bridge, U12/U13 tier boundaries, and U14-style absolute fixed-step catch-up. The recipe model already supports declared inputs and multiple outputs, so later ore -> metal -> weapon content can extend definitions without replacing the production engine.

## Added files

- `Assets/Game/Domain/Production/ProductionModels.cs` and Unity metadata: recipe/site/task/save models.
- `Assets/Game/Simulation/Production/ProductionService.cs` and Unity metadata: reservations, target policy, food consumption, exact commits, deterministic aggregate simulation and save restore.
- `Assets/Game/Presentation/Production/ProductionPresentation.cs` and Unity metadata: source binding and compact canonical debug overlay.
- `Assets/Game/Tests/Editor/ProductionAndFarmingTests.cs` and metadata: 13 U15 EditMode tests.
- `Assets/Game/Tests/PlayMode/ProductionPlayModeTests.cs` and metadata: 3 U15 PlayMode tests.
- `Tools/Verify-U15.ps1`: timeout-aware U14/U15/regression/PostgreSQL/GPU/asset/build verifier.
- `docs/U15_HANDOFF.md`.

## Modified files

- Catalog/config: `Assets/Game/Art/Config/ResourceCatalog.asset`, `BuildingPresentationCatalog.asset`.
- Domain/save: `Tier1AiState.cs`, `ResourceWorld.cs`, `SaveSnapshot.cs`.
- Simulation/AI: `HaulWorldQuery.cs`, `Tier1AiScheduler.cs`, `UtilityActions.cs`, `SaveCoordinator.cs`.
- Tier 2: `Tier2Components.cs`, `Tier2Materializer.cs`, `Tier2Runtime.cs`, `Tier2SimulationSystem.cs`.
- Infrastructure/persistence: `U08BuildingFoundation.cs`, `U09ResourceFoundation.cs`, `FileSaveStore.cs`, `LocalSceneCompositionRoot.cs`, `PostgresSaveStore.cs`, `SaveXmlCodec.cs`.
- Presentation: `BuildingPlacementController.cs`.
- Regression tests: `PersistenceTests.cs`, `PostgresIntegrationTests.cs`, `Tier2EcsTests.cs`.
- Project protocol: `PROJECT_STATE.md`, `TASK_GRAPH.md`, `NEXT_TASK.md`, `DECISIONS.md`, `ARCHITECTURE.md`.

## Production definitions and baseline configuration

Canonical food is `berries`. The immutable Stone Age recipe catalog contains:

| Recipe | Site | Output | Work |
|---|---|---:|---:|
| `gather_berries` | finite natural source | 4 berries | 4 game hours |
| `gather_fiber` | finite natural source | 4 plant fiber | 6 game hours |
| `chop_wood` | finite natural source | 4 wood logs | 8 game hours |
| `gather_stone` | finite natural source | 4 stone | 10 game hours |
| `cultivate_berries` | primitive crop plot | 6 berries | 8 game hours plant/harvest work |

Default target stocks are berries 40, plant fiber 20, stone 20 and wood logs 30. Default consumption is one berry unit per inhabitant per 15 game days. Recipes validate resource definitions and expose declared input/output lists for future chains. Current U15 recipes have no consumed input.

## Job, reservation and ownership flow

Tier 1 flow is `target below policy -> reserve source/task -> MoveToSource -> Working -> batch commit -> CharacterInventory -> DeliverProduction -> BuildingStorage -> Utility AI`. Navigation stays behind `ITier1MovementDriver`; output and deposit use U09 inventories and `ResourceTransferService`.

One nonterminal task owns one worker and one site claim. A second worker cannot reserve the same last batch. Cancellation releases the worker/site claim; completion decrements a finite source and creates outputs exactly once before release. Save restore validates identities, recipe/site compatibility and uniqueness, then reconstructs active claims.

Output always has one canonical owner. The commit prefers enough character capacity, then a valid completed storage owner. Tier 1 delivery transfers the exact batch; if a chosen storage fills meanwhile, transfer fails atomically and the resource remains carried. No visual object, overlay or ECS component owns resource quantity.

## Farming and building placement

`primitive_crop_plot` is a data-driven 3x3 U08 building with a current `BALANCE_TBD` construction cost of 2 wood logs and 2 plant fiber. Confirmed U08/U09 placement emits activation into the production service using the placed building stable ID. Its fixed abstract lifecycle is Unplanted -> plant work -> Growing -> Ready -> harvest work/output -> Unplanted. There is no soil, season, weather, irrigation, seed, disease or fertility simulation. The current Storage Basket prefab is reused only as a temporary project-owned crop-plot visual.

## Tier execution

- Tier 1 uses the existing centralized scheduler and physical movement. Only one physical autonomous producer is exposed at a time in the MVP scene so hauling/rest remains observable; aggregate workers are not restricted by that presentation policy.
- Tier 2 adds the unmanaged `Tier2ProductionProgress.PendingWorkTicks` field to the existing Burst `IJobEntity`. The materializer crosses the job barrier, returns stable IDs in deterministic order, clears the pending counter and commits through the shared service. There is no GameObject per Tier 2 worker.
- Tier 3/off-camera passes stable IDs to the same service. Crop growth, food consumption and worker production advance in one-calendar-day fixed steps from absolute U03 ticks. Workers and sources are deduplicated/sorted; there is no new RNG or tier-specific result path.

## Save/load

`SaveSnapshot.CurrentVersion` is 2. The strict XML payload stores production tick, food remainder/consumed/shortage, stock targets, sites, active task identity/progress, produced totals and canonical inventories. Restore preserves site/task/worker stable IDs and does not replay a committed batch. Version-1 time-only XML remains supported and yields no production state. The size guard is 4 MiB. PostgreSQL validates/stores either v1 or v2 payload metadata; its schema remains unchanged and it is not part of realtime simulation.

## Test coverage

The 13 U15 EditMode tests cover recipe validation/output, finite depletion/over-harvest rejection, partial work/exactly-once completion/cancellation, single ownership/full storage, exclusive reservation, food consumption/shortage, target-stock stop/start, XML save/restore/no replay, determinism/collection order, tier-route chunking, crop lifecycle, five-year sustainability and production scale measurement.

The 3 U15 PlayMode tests cover autonomous Tier 1 selection/movement/work/completion/deposit/return to AI, LocalGameplay debug-overlay/source composition, and U08/U09 crop-plot placement activation.

## Validation commands and results

Primary full gate:

```powershell
& 'C:\serenity_game\Tools\Verify-U15.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

Results from the final full gate before the final restore-validation tightening:

- full non-PostgreSQL EditMode: 221 passed, 0 failed, 0 skipped;
- full PlayMode: 37 passed, 0 failed, 0 skipped;
- U10: 14/14 EditMode, 5/5 PlayMode;
- U11: 12/12 EditMode, 10/10 PlayMode;
- U12: 11/11 EditMode, 2/2 PlayMode;
- U13: 9/9 EditMode, 2/2 PlayMode;
- U14: 14/14 EditMode, 2/2 headless PlayMode and 2/2 GPU PlayMode;
- PostgreSQL integration: 25/25;
- U15: 13/13 EditMode, 3/3 headless PlayMode and 3/3 GPU PlayMode;
- U09 regression: 10/10 EditMode and 4/4 PlayMode;
- U05A asset/catalog validation completed successfully;
- Windows x64 Mono Development build succeeded, errors=0, warnings=2 inherited BuildReport warnings; player entry executable is 667136 bytes.

After tightening restore validation to require task/site recipe agreement, `& 'C:\serenity_game\Tools\Verify-U15.ps1' -GpuValidation` passed again: U14 14/14 + 2/2 + GPU 2/2, U13 9/9 + 2/2, U12 11/11 + 2/2, U11 12/12 + 10/10, U10 14/14 + 5/5, U15 13/13 + 3/3 + GPU 3/3, U09 10/10 + 4/4, both asset validations and both Windows build gates.

## Long-run and performance evidence

Five-year baseline is explicit: 20 inhabitants, one crop plot, 20 starting berries, berries target 80, storage capacity 1000, default consumption rate and 1825 simulated days. It completed with 2490 berries produced, 2433 consumed, 0 shortage, positive final stock, no negative inventory, and hash `2D19219657849A02`.

The performance gate simulates 1,000 worker identities, population equivalent 10,000, four finite sources of capacity 1000, per-resource target 200 and 30 days. The final full gate measured 4551.025 ms, 0 managed bytes on the measured thread, and hash `6E074B86B963FC7A`. This is recorded evidence, not an artificial pass/fail threshold.

## PlayMode/manual inspection and diagnostics

Headless and GPU PlayMode both exercised LocalGameplay production paths and passed. An additional attempt to inspect the standalone player through Windows Computer Use failed twice during helper initialization with `trusted Node process exited unexpectedly`; therefore no unsupported claim of interactive manual clicking is made. The deterministic PlayMode coverage verifies the physical scheduler/movement/placement behavior, and the GPU run verifies the rendered player path.

Successful gate logs contain no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error. BuildReport reports two inherited warnings but does not emit a warning detail or C# warning; build result is Success with zero errors.

## Known limitations / deferred scope

- Crop plot art is a Storage Basket placeholder.
- Production UI is a development IMGUI read view; U27 owns gameplay UI.
- Current recipes are simple bulk batches with no tools, skills, quality, spoilage, seasons/weather, regrowth or advanced processing content.
- Input recipes are supported, but current input selection requires all inputs to exist in one canonical inventory.
- A multi-output recipe commits all outputs to one owner; physical Tier 1 delivery metadata currently identifies the first output because no U15 recipe has multiple outputs.
- The minimal food consumer records shortage only; it does not create starvation, health, morale or death effects.
- U16 and every later task remain unstarted.

## Definition of Done

All U15 acceptance items pass: reusable framework; sustainable food plus wood/stone/fiber; cultivated path; autonomous Utility AI; canonical single ownership; no duplication; full-storage retention; finite depletion; food consumption; target stocks; Tier1/Tier2/Tier3 paths; tier-route equivalence; durable production restore; five-year run; prior-system/PostgreSQL regressions; asset validation; Windows build; and documentation. The only environmental limitation is the separately disclosed interactive Windows helper failure; the equivalent headless and GPU PlayMode checks passed.
