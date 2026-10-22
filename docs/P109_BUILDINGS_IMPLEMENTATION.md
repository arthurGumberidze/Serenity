# P10.9 Stone Age buildings implementation

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
