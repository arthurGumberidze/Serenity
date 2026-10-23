# P10.9 Stone Age buildings implementation

## Stages E1-E4 complete

Date: 22 October 2026. Fourteen definitions/prefabs complete the planned E set. Clan House, Healer Hut,
Shrine, Chief Hut, Meeting Area and Training Ground use capacity-checked home/workplace assignments;
medical and training actions delegate to existing health and skill authorities. Fishing Camp, Gathering
Area, Primitive Garden and Well use the established production/resource graph; fishing is camp-gated and
the well creates canonical water. Shoreline, groundwater and territory restrictions are evaluated by the
normal placement service. Palisade, Gate, Watchtower and Primitive Path use the shared placement occupancy;
opening a gate releases its cells and closing it reserves them again.

`BuildingLifecycleService` adds deterministic wear, repair, Stone Age Hut to Clan House upgrade and
demolition. Repair/upgrade operations validate elapsed work before atomically charging and use persisted
building operation IDs for exactly-once behavior. Upgrade preserves `StableEntityId`; demolition spills
canonical storage before registry removal and creates physical return piles. The selected-building UI
exposes assignments, treatment/training, repair, upgrade, gate controls and demolition.

Verification commands:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\serenity_game' -executeMethod Game.Infrastructure.Editor.P109BaseBuildingFoundation.Build -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\authoring.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\serenity_game' -executeMethod Game.Infrastructure.Editor.U09ResourceFoundation.Build -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\resources.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testCategory 'P10.9' -testResults 'C:\serenity_game\Artifacts\P109\buildings-e\p109-editmode-final.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\p109-editmode-final.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.Editor.ResearchFrameworkTests;Game.Tests.Editor.P104NaturalResourcesTests;Game.Tests.Editor.P109ConstructionStateTests;Game.Tests.Editor.P109BuildingProductionTests;Game.Tests.Editor.P109BuildingPlacementRulesTests;Game.Tests.Editor.P109ExtendedBuildingsTests' -testResults 'C:\serenity_game\Artifacts\P109\buildings-e\affected-editmode-2.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\affected-editmode-2.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.BuildingPlacementPlayModeTests;Game.Tests.PlayMode.ProductionPlayModeTests' -testResults 'C:\serenity_game\Artifacts\P109\buildings-e\affected-playmode.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\affected-playmode.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Artifacts\P109\buildings-e\build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P109BuildingsE/Serenity.exe'
```

Results: P10.9 EditMode 24/24 passed; directly affected EditMode 66/66 passed. The affected PlayMode
runner stalled during UMA initialization after loading LocalGameplay and produced no NUnit result; its
verified process tree was stopped, so the run is not counted as pass or fail. Windows Development build
succeeded with 0 errors / 53 warnings, GUID `709397d549104081bccd3d70d355d5e7`. Unity recorded build
success before the post-build process was stopped; no `build-provenance.json` was completed and no source
fingerprint is claimed. F1 is the next stage and owns the full multi-settlement, tier, save/load and native
Player acceptance matrix.

## Stages D1-D2 complete

Date: 22 October 2026. The shared building and production catalogs now contain the complete D set:
Hunting Camp, Butcher Station, Food Storage, Drying Rack and Hide Workshop. Hunting requires a completed
camp and yields a whole carcass. The butcher recipe converts exactly one carcass to five meat, two hides
and three bones. Preservation converts three meat to two dried meat and two hides plus one plant fibre
to two cured hides. The food store is a 120-unit ordinary storage inventory restricted to food, and
dried meat is edible only after it reaches an eligible storage or character inventory.

The new definitions reuse canonical construction, research, workplace, bounded production buffers,
physical hauling, inventory transactions, skill progression and snapshot state. Organized Hunting owns
the camp/butcher unlocks; Food Preservation owns the store/rack/workshop and preservation recipes.
No ammunition resource, parallel hunting manager or alternate stock balance was introduced.

Authoring command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod Game.Infrastructure.Editor.U09ResourceFoundation.Build -quit -logFile 'C:\serenity_game\Artifacts\P109\buildings-d2\authoring.log'
```

Focused verification command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P104NaturalResourcesTests;Game.Tests.Editor.P109BuildingProductionTests;Game.Tests.Editor.ResearchFrameworkTests;Game.Tests.Editor.BuildingFoundationTests' -testResults 'C:\serenity_game\Artifacts\P109\buildings-d2\focused-editmode-2.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-d2\focused-editmode-4.log'
```

Result: 37/37 passed. Windows build command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Artifacts\P109\buildings-d2\build-final.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P109BuildingsD/Serenity.exe'
```

Result: succeeded, 0 errors / 53 warnings, GUID `dc35880aece74843b8f7d799bb8576c2`,
source fingerprint `D1F5ED70AAA44A77FC8DEFD6CD8DC0EFE440FEB0D245E3E2F3A16B0057023108`.
The two earlier shell-wrapped build launches were stopped after they remained in the initial import
phase and produced neither a Player nor a BuildReport. The listed direct final build is authoritative.
E1 is next; C5 remains deferred and broad final acceptance remains F1.

## Gameplay feedback stabilization before D1

Date: 22 October 2026. By user direction C5 acceptance is deferred and the reported gameplay
defects were fixed before entering D1. The implementation includes bounded independent gather work,
sleep/indoor visibility with Campfire exception, construction cancellation, clickable notification
focus, animal roaming/hunting HP/arrows, human HP/mood, Campfire production/fire, authored material
color preservation, CityBuilder hut art, favorite activities, skill effects/progression and persisted
weapon durability. See D-167 and the current PROJECT_STATE checkpoint for the exact contracts.

Final affected EditMode command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P104NaturalResourcesTests;Game.Tests.Editor.P105CharacterProductionTests;Game.Tests.Editor.P106DomesticAndSaveTests;Game.Tests.Editor.P109BuildingProductionTests;Game.Tests.Editor.PlayerControlStateTests;Game.Tests.Editor.TacticalCombatTests;Game.Tests.Editor.Tier1UtilityAiTests' -testResults 'C:\serenity_game\Artifacts\P109\feedback-stabilization\affected-final-editmode.xml' -logFile 'C:\serenity_game\Artifacts\P109\feedback-stabilization\affected-final-editmode.log'
```

Result: 46/46 passed. Earlier focused checkpoints passed 17/17, 50/50, 55/55, 23/23,
14/14 and 33/33. Both a headless GameplayUi PlayMode command and a D3D11
BuildingPlacementPlayModeTests command stalled after real-scene entry during UMA initialization;
neither emitted NUnit XML, so neither is claimed as passed. Verified test-owned PIDs were terminated
and their generated InitTestScene/lock artifacts removed.

Windows build command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Artifacts\P109\feedback-stabilization\build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P109FeedbackStabilization/Serenity.exe'
```

Result: succeeded, 0 errors / 53 warnings; executable:
`Builds/P109FeedbackStabilization/Serenity.exe`. Unity recorded `Build Successful` and
`U00_BUILD_RESULT: Succeeded` before its post-build process tree failed to exit; only the verified
build-owned Unity/worker/crash-handler PIDs were stopped and the residual lock was removed. The
tracked Synty/PNB content is repository content from `a8114d70`; it is not part of the excluded
user-owned dirty set.

## Stage B1 complete

Date: 21 October 2026. Commit scope: extensible building definition specifications and validation.

Implemented:

- immutable Domain specifications for construction, placement, housing, work slots, storage,
  production, maintenance/demolition and upgrades;
- era, description, icon key and technology prerequisites on the existing `BuildingDefinition`;
- source-compatible mapping for historical constructor callers;
- one `BuildingDefinitionCatalog` that validates resources, recipes, technologies, upgrade targets
  and upgrade cycles;
- serialized construction work/stages and placement form/constraints in the existing Unity
  `BuildingPresentationCatalog`;
- focused tests for valid specifications and rejection of bad footprints, duplicate recipe and
  technology IDs, missing references and cyclic upgrades.

Not implemented in B1: new save fields, material delivery, new building entries, cooking,
production queues or visible new models. Those belong to B2 and later stages.

## Verification

Final test command (save writes isolated under Artifacts):

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Artifacts\P109\buildings-b1\test-saves'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109BuildingDefinitionTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.ResourceInventoryTests' -testResults C:\serenity_game\Artifacts\P109\buildings-b1\editmode-second.xml -logFile C:\serenity_game\Artifacts\P109\buildings-b1\editmode-second.log
```

Result: 26/26 passed. A first restricted-sandbox invocation crashed before tests in Unity's internal
`BuildReportRestService` socket startup; the same command outside that restriction passed.

Final build command:

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Artifacts\P109\buildings-b1\test-saves'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P109BuildingsB1/Serenity.exe -logFile C:\serenity_game\Artifacts\P109\buildings-b1\build.log
```

Result: succeeded, 0 errors / 53 warnings, build GUID
`38c83a9598994209ad352522fdaeee64`. The established source-provenance pass completed and wrote
`Builds/P109BuildingsB1/build-provenance.json`. Protected baseline: 44/44 unchanged.

## Optional manual regression route

Run `C:/serenity_game/Builds/P109BuildingsB1/Serenity.exe`, start a new game, open the construction
panel, place the existing shelter on flat free ground, cancel a second preview over a rock, then
save and load. B1 deliberately has no new building buttons or visuals; this route only checks that
definition refactoring did not regress the existing presentation. Use the later C-stage build for
manual acceptance of the five new functional buildings.

## Next stage

B2 adds canonical construction delivery/buffer/stage state and an isolated new save generation.
Old save files remain read-only and are not migrated.

## Stage B2 complete

Date: 21 October 2026. Commit scope: canonical construction-site state, shared-registry material
buffers and snapshot v26.

Implemented:

- `ConstructionPhase` and per-resource required/installed progress on the existing `Building`;
- stable settlement ownership, stage index/count, work gating and deterministic per-stage cost slices;
- `InventoryOwnerKind.ConstructionBuffer`, keyed by the building's stable ID, using the existing
  `ResourceInventoryRegistry` and `ResourceTransferService`;
- `ConstructionSiteService` transactions for delivery, readiness, stage consumption and restored
  buffer validation; no resource exists in both installed progress and an inventory;
- snapshot v26 fields for settlement, phase, stages and material progress, while delivered stock
  round-trips through the existing production inventory payload;
- explicit save generation `p109-stone-age-buildings-v1` and normal slot directory
  `Serenity/P109/StoneAgeBuildingsV1/Saves`; the prior `CompactWorldV1` directory is read-only
  legacy metadata and is never migrated or overwritten;
- source-compatible v1-v25 codec handling and legacy building constructors.

Not implemented in B2: physical hauler work orders, cancellation spills, territory/approach
validation, new building definitions, cooking or production UI. Those belong to B3/C stages.

## B2 verification

Focused/expanded regression command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109ConstructionStateTests;Game.Tests.Editor.P109BuildingDefinitionTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.Editor.PersistenceTests;Game.Tests.Editor.P108DiagnosticsTests;Game.Tests.Editor.PlayerControlStateTests;Game.Tests.Editor.P102ConnectedWorldTests;Game.Tests.Editor.P103ContextCommandTests;Game.Tests.Editor.P104NaturalResourcesTests;Game.Tests.Editor.P105CharacterProductionTests;Game.Tests.Editor.P106DomesticAndSaveTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests;Game.Tests.Editor.ProductionAndFarmingTests;Game.Tests.Editor.DynastyAndSuccessionTests;Game.Tests.Editor.HealthAndFamilyTests' -testResults C:\serenity_game\Artifacts\P109\buildings-b2\editmode-save-regression.xml -logFile C:\serenity_game\Artifacts\P109\buildings-b2\editmode-save-regression.log
```

Result: 149/149 passed. The full EditMode diagnostic discovered 641 tests: 611 passed, 25 skipped
and five initially failed. Two failures were stale v25 assertions corrected for the new v26 boundary;
the remaining three are inherited Synty asset-count/shader and P8.2 terrain-scene diagnostics,
outside B2. The focused 30-test construction suite also passed 30/30.

Windows build command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P109BuildingsB2/Serenity.exe -logFile C:\serenity_game\Artifacts\P109\buildings-b2\build-final.log
```

Final exact-source focused rerun: 30/30 passed at
`Artifacts/P109/buildings-b2/editmode-final.xml`.

Result: succeeded, 0 errors / 27 warnings, GUID `525f7a3748aa4356914f184b450b84f1`,
source fingerprint `EB936BC5B7CB43109DBB1EC469D51F92C7C1BEEE3C1E0CD187D2597C3F90CEAD`.
Protected baseline: 44/44 unchanged. B3 is next.

## Stage B3 complete

Date: 22 October 2026. Commit scope: settlement territory, approach perimeter, authored zone and
segment placement, and snapshot v27 exact-cell persistence.

Implemented:

- deterministic rectangular structures, connected zone cells and ordered cardinal segment paths;
- explicit settlement territory regions and whole-shape ownership checks;
- deterministic free approach perimeter with occupancy/bounds/territory filtering;
- injected specialized world-rule hook, with shoreline and groundwater constraints failing closed;
- exact placement cells and settlement ID retained on confirmed buildings and occupancy restore;
- snapshot v27 placement-cell XML; v26 construction snapshots remain readable;
- runtime starter-settlement territory without a parallel grid or manager.

Verification: focused EditMode 25/25
(`Artifacts/P109/buildings-b3/focused-editmode-3.xml`), affected EditMode 154/154
(`expanded-editmode.xml`) and final placement PlayMode 4/4 (`placement-playmode-final.xml`). The two
historical hard-coded preview points were terrain-blocked on the current tiled map; their tests now
find an actual clear footprint. Windows build command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Artifacts\P109\buildings-b3\build-clean.log -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P109BuildingsB3/Serenity.exe
```

Result: succeeded, 0 errors / 53 warnings, GUID `563c001610724baf8e0a7b343e1a9dac`, source
fingerprint `C9C5A6E07E2766373E849E70BEFBA4025F06416A3E0C6A2D8249906739FB21B4`.
Protected baseline: 44/44 unchanged.

## Verification cadence from C1

Per the user's 22 October direction, C1-C4 continue in this chat. C1-C3 receive targeted tests for
their changed rule and directly affected integration; successful unrelated suites are not repeated.
After C4, run one sufficiently broad regression and create one separate Windows Player for the
user's construction/cooking route. Broad control points then remain C5 and F1 unless a concrete
save/resource-loss risk requires an earlier expansion.

## Stage C1 complete

Date: 22 October 2026. Placement now creates physical, saveable haul work instead of removing
construction cost from source inventories. The existing `WorkManager`, `HaulWorldQuery`,
`ResourceTransferService` and Tier1 action path accept world-pile/storage sources and construction
buffer destinations. Delivery batches are bounded to eight units, queued delivery quantities reserve
their source stock during later funding evaluations, and material stages commit only after actual
drop-off. Restored construction buffers are rebound and validated after load.

Cancellation terminates open haul/build orders, returns both buffered and already installed materials
as canonical world piles, removes attached unfinished storage and releases placement occupancy. A
worker carrying stock at cancellation remains covered by the established lossless cargo-drop path.
Legacy isolated simulations without configured physical logistics retain their earlier transaction
contract; LocalGameplay explicitly configures the new path.

Focused command/result: `P109ConstructionStateTests` passed 7/7 at
`Artifacts/P109/buildings-c1/focused-editmode-final.xml`. Directly affected construction, resources,
work orders and Tier1 hauling passed 34/34 at
`Artifacts/P109/buildings-c1/affected-editmode-final.xml`. The earlier 5/6 run exposed a double count
in the new cancellation assertion only; the implementation already conserved stock. Per the agreed
cadence, no broad regression or Windows build was run at C1.

## Next stage

C2 adds the five playable base definitions and their visual construction stages.

## Stage C2 complete

Date: 22 October 2026. The presentation catalog now contains the five first playable objects:
Primitive Shelter, Stone Age Hut, Campfire, Storage Basket and Stone Tool Workshop. Their costs,
work, stage counts, footprints, housing/storage capabilities and research prerequisites are authored
as data. The existing shelter/storage prefabs remain supported; three new project-owned primitive
prefabs add clear silhouettes, colliders and `BuildingConstructionStageVisual` markers. The presenter
derives visible groups from canonical construction stages.

The worker route exposed an integration edge: autonomous cleanup had reserved an entire fibre pile
before construction claims were created. `HaulClaimRegistry` now supports deterministic release of
matching unpicked source claims. `ConstructionFundingService` invokes it only after a construction
transaction is confirmed and only for the exact selected deductions. Carried resources are not
preempted and all ownership changes remain physical transfers.

Verification commands:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109ConstructionStateTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.P109BuildingDefinitionTests;Game.Tests.Editor.ResearchFrameworkTests' -testResults C:\serenity_game\Artifacts\P109\buildings-c2\focused-editmode-final2.xml -logFile C:\serenity_game\Artifacts\P109\buildings-c2\focused-editmode-final2.log

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.BuildingPlacementPlayModeTests.ResidentApproachesAndBuildsInsteadOfInstantCompletion' -testResults C:\serenity_game\Artifacts\P109\buildings-c2\placement-worker-playmode-final3.xml -logFile C:\serenity_game\Artifacts\P109\buildings-c2\placement-worker-playmode-final3.log
```

Results: focused EditMode 36/36 passed; physical delivery/build PlayMode 1/1 passed. Catalog and
prefab generation succeeded in `Artifacts/P109/buildings-c2/generate-final.log`. Per the agreed
cadence, no broad regression or Windows build was run at C2.

## Next stage

C3 adds work slots, production queues and the kitchen panel using the existing production authority.

## Stage C3 complete

Date: 22 October 2026. U15 production now supports `BuildingWorkplace` sites without a parallel
manager. Each recipe site has a deterministic identity and refers to its owning building; the building
shares bounded `ProductionInput` and `ProductionOutput` inventories and a maximum-worker limit across
all its recipes. Campfire queues consume physically delivered raw food plus wood fuel and atomically
place cooked food into the output buffer. Missing inputs, pauses and full outputs preserve queue and
task progress. The standard haul query accepts production inputs as destinations and production outputs
as sources, so both supply and collection use the established work/claim/transfer pipeline.

The building inspector exposes localized recipes, both buffers, queue counts, +1/+5, pause/resume,
waiting reason, worker and progress. Snapshot v28 persists building ownership, queue, pause and slot
limits in the existing production section; v1-v27 readers remain supported. Active work and inventories
already round-trip through their existing sections.

Verification commands:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -executeMethod Game.Infrastructure.Editor.U09ResourceFoundation.Build -quit -logFile 'C:\serenity_game\Artifacts\P109\buildings-c3\generate-resources-2.log'

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109BuildingProductionTests;Game.Tests.Editor.BuildingFoundationTests.CatalogContainsFivePlayableBaseBuildingsWithAuthoredStages;Game.Tests.Editor.ProductionAndFarmingTests;Game.Tests.Editor.WorkGroupAndJobTests;Game.Tests.Editor.PlayerControlStateTests' -testResults 'C:\serenity_game\Artifacts\P109\buildings-c3\focused-suite-final.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-c3\focused-suite-final.log'

& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.BuildingPlacementPlayModeTests.PlacingCampfireRegistersKitchenSlotsAndPersistentBuffers' -testResults 'C:\serenity_game\Artifacts\P109\buildings-c3\placement-playmode.xml' -logFile 'C:\serenity_game\Artifacts\P109\buildings-c3\placement-playmode.log'
```

Results: the final focused EditMode set passed 25/25 and the focused PlayMode route passed 1/1.
The first compiler attempt found a missing LINQ import; an intermediate 24/25 run found the old
`WorkOrder` endpoint whitelist and led to the production-buffer validation fix. The listed final
artifacts are green. Per the agreed cadence, no broad regression or Windows build was run at C3.

## Next stage

C4 unifies edible resources in needs/food consumption, prevents raw-food consumption and proves that
the starter loop cannot deadlock hunger before the first cooked batch. It ends with the scheduled broad
regression and separate Windows Player build.

## Stage C4 complete

Date: 22 October 2026. `FoodConsumptionService` now exposes the catalog-backed cooked set and spends
exactly one cooked unit per meal. Tier1 eating calls it before hunger recovery; aggregate production
handles only non-materialized population. Raw food remains available to campfire recipes but is never
selected as edible. Player storage receives eight cooked-berry starter rations and the rival abstract
inventory receives forty.

Final verification commands:

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109FoodIntegrationTests;Game.Tests.Editor.P109BuildingProductionTests;Game.Tests.Editor.ProductionAndFarmingTests;Game.Tests.Editor.ResearchFrameworkTests' -testResults Artifacts/P109/buildings-c4/focused-editmode-final.xml -logFile Artifacts/P109/buildings-c4/focused-editmode-final.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.BuildingPlacementPlayModeTests -testResults Artifacts/P109/buildings-c4/focused-playmode-final.xml -logFile Artifacts/P109/buildings-c4/focused-playmode-final.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults Artifacts/P109/buildings-c4/broad-editmode-final.xml -logFile Artifacts/P109/buildings-c4/broad-editmode-final.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.BuildingPlacementPlayModeTests;Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.ProductionPlayModeTests;Game.Tests.PlayMode.P105CharacterProductionPlayModeTests;Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.P109FunctionalIntegrationPlayModeTests;Game.Tests.PlayMode.OffCameraSimulationPlayModeTests' -testResults Artifacts/P109/buildings-c4/broad-playmode.xml -logFile Artifacts/P109/buildings-c4/broad-playmode.log
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Artifacts\P109\buildings-c4\test-saves'; Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.OffCameraSimulationPlayModeTests.Tier3CatchUpReturnsLatestStateWithoutChangingCanonicalOwnership;Game.Tests.PlayMode.P109PlayablePatchTests.ChopCommandFellsWholeTreeCommitsExactLogsAndSurvivesIsolatedSave;Game.Tests.PlayMode.ProductionPlayModeTests.CropPlotPlacementUsesU08OccupancyConstructionAndActivation;Game.Tests.PlayMode.ProductionPlayModeTests.Tier1WalksWorksAndCommitsOneCanonicalProductionOutput' -testResults Artifacts/P109/buildings-c4/broad-playmode-failures-rerun.xml -logFile Artifacts/P109/buildings-c4/broad-playmode-failures-rerun.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ProductionPlayModeTests.CropPlotPlacementUsesU08OccupancyConstructionAndRegistration -testResults Artifacts/P109/buildings-c4/crop-placement-final.xml -logFile Artifacts/P109/buildings-c4/crop-placement-final.log
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath C:\serenity_game\Builds\P109BuildingsC4\Serenity.exe -logFile C:\serenity_game\Artifacts\P109\buildings-c4\build.log
```

Results: focused EditMode 32/32; focused PlayMode 6/6. Broad EditMode 631/634 with only the
pre-existing P8.2 geography failure and two failures caused by the user's ignored expanded Synty
import. Broad affected PlayMode passed 27/31 first; exact reruns closed the four stale/setup-sensitive
cases at 3/4 and 1/1. The Windows build succeeded with 0 errors / 53 warnings, GUID
`94165ea0731843f4b0be90bfee0cfc87`, fingerprint
`C023FA2A8D9C26FA550CD8F90739BECDF2500DACF039667311154AA0CC3596C8`. Protected baseline: 44/44.

## Next stage

C5 proves the complete New Game → construction → cooking → storage → eating → save/load route. It has
not started.

## Player shore/lifecycle feedback — 23 October 2026

This supersedes the historical “Next stage” paragraph above: C5 remains user-deferred; current scope is the
P10.9 feedback repair ahead of F1. See D-173 for mechanics and state ownership. No new external assets imported.

Executed with Unity `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$out = 'C:\serenity_game\Artifacts\P109\shore-lifecycle-feedback'
$env:SERENITY_SAVE_DIRECTORY = "$out\saves"
& $unity -batchmode -nographics -projectPath C:\serenity_game -logFile "$out\authoring.log" -quit -executeMethod Game.Infrastructure.Editor.P109BaseBuildingFoundation.BuildFeedbackAssets
& $unity -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109ExtendedBuildingsTests;Game.Tests.Editor.P109CompactWorldTests;Game.Tests.Editor.BuildingPlacementTests;Game.Tests.Editor.ConstructionFundingTests;Game.Tests.Editor.P94StoneAgeSettlementTests' -testResults "$out\editmode.xml" -logFile "$out\editmode.log"
& $unity -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.P94StoneAgeSettlementPlayModeTests.AuthoredSettlementSpawnsDistinctBuildingsAndStoneAgeWear' -testResults "$out\playmode.xml" -logFile "$out\playmode.log"
& $unity -batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.P109ExtendedBuildingsTests;Game.Tests.Editor.P109BuildingPlacementRulesTests;Game.Tests.Editor.P109ConstructionStateTests;Game.Tests.WorkGroupAndJobTests;Game.Tests.BuildingFoundationTests;Game.Tests.BuildingDestructionFireCaptureTests' -testResults "$out\affected-editmode.xml" -logFile "$out\affected-editmode-2.log"
& $unity -batchmode -nographics -projectPath C:\serenity_game -logFile "$out\build.log" -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P109ShoreLifecycle/Serenity.exe
& $unity -batchmode -nographics -projectPath C:\serenity_game -logFile "$out\build-final.log" -quit -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P109ShoreLifecycle/Serenity.exe
$env:SERENITY_SAVE_DIRECTORY = "$out\player-saves"
$env:SERENITY_SETTINGS_PATH = "$out\player-settings.json"
Start-Process -FilePath C:\serenity_game\Builds\P109ShoreLifecycle\Serenity.exe -ArgumentList @('-p109FeedbackSmoke','-p109FeedbackScreenshot',"$out\player.png",'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',"$out\player.log") -WindowStyle Hidden -PassThru
```

The initial non-escalated authoring process did not start or write a log; the escalated run completed. Authoring
generated the imported-model scale fixes and idle/walk controller successfully. EditMode `editmode.xml`: 22/22.
Full-scene `playmode.xml`: 1/1, verifying GameplayUi, both starter residents and actual placement at every marked
lake (not merely loading MainMenu). Expanded `affected-editmode.xml`: 41/41 across P109ExtendedBuildings (13),
P109BuildingPlacementRules (6), P109ConstructionState (9) and WorkGroupAndJob (13). The other namespace filters
in the recorded commands matched no fixtures; no extra passes are claimed for them. The first expanded attempt
failed compilation because optional smoke capture referenced unavailable ScreenCapture; the harness was changed
to the existing Texture2D/ReadPixels pattern before the successful run. The work regression explicitly proves
walking produces zero dismantling progress, cancellation retains the building and sustained work removes it.

First Windows build: 0 errors / 53 warnings, GUID `01e30bddbb55420a897823652219f53d`, fingerprint
`9A644C2FB278A3499CC2A54BC6BDCDD5C57E018CEC5B7B0C7D521511D8219831`. Native smoke launched new game, logged
`P109_FEEDBACK_SMOKE_PASSED: LocalGameplay, UI, starter residents, valid lake landings=11` and wrote `player.png`;
the image was inspected and shows inhabitants and gameplay UI. Player shutdown reports two persistent native
allocations; this is recorded, not treated as proof of leak-free teardown. Final rebuild follows only the honest
Meeting Area tooltip change. Full native interaction/visual acceptance and F1 multi-settlement/tier coverage
are not claimed. Protected baseline manifest `Artifacts/P109/compact-world/protected-before.json`: 44/44 match.

Asset assessment: prefer Winter Environment 3.0 (Unity 6 URP template), Toby Foliage Engine Light (current
Unity 6 URP-compatible release) and Lightning Bolt Effect (small presentation-only lightning candidate).
JustCreate Low-Poly Nature is optional additional art, not required to fix existing building scale mistakes.
HDRP Terrain Tessellation is incompatible with URP. RTS Starter Kit duplicates existing game authorities;
Landscape Builder needs a separate world-generation integration decision. Legacy Tenkoku and Simple Day/Night
are not remedies for the brown damage tint. The exact Simple Water Shader archive/author could not be verified;
do not equate it with the similarly named Simple Water Shader URP without inspecting the package. These are
download/evaluation recommendations only, not permission to import packages or replace simulation systems.

Final build (`build-final.log`): Succeeded, 0 errors / 27 warnings; GUID
`cc026ce5123e4869aec37f24e2d1e20f`, source fingerprint
`C0BE7A75D122E59EFF7E55BF3E731AEE388AD8D861C5E4CD40BBC83F94055CB6`, UTC `2026-10-23T06:59:49Z`.
The previous `Builds/P109VisualFeedback` Player is retained. Test-run temporary Resources JSON files were
automatically removed by the test/build tooling and are not committed. The final smoke uses the same Player
command above with output names `player-final.log` and `player-final.png`.
The final rerun also passed: UI/residents and 11/11 landing placements; its gameplay image was inspected.
