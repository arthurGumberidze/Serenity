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
