# WM15 living buildings validation — 2026-10-29

## Scope

Delivery evidence for `docs/world_map/WM15_LIVING_BUILDINGS_SPEC.md`. Building upgrade chains are excluded by
the user's explicit decision and remain first in `docs/PAUSED_TASKS.md`. Old-save compatibility is not required.
Parallel imported art and the neighbouring WM15 visual/water work were preserved.

## Final EditMode gate

Command (Unity 6000.6.4f1, launched hidden without `-quit` because Test Runner owns shutdown):

```text
Unity.exe -batchmode -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter "Game.Tests.Editor.WM15LivingBuildingsTests;Game.Tests.Editor.WM15CurrentFeedbackTests;Game.Tests.Editor.P107MilitaryTrainingCaptureTests;Game.Tests.Editor.P104NaturalResourcesTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.Editor.P106DomesticAndSaveTests;Game.Tests.Editor.WM13WorldMapSaveVersionTests;Game.Tests.Editor.WM06WorldMapResourcesTests;Game.Tests.Editor.WM04WorldMapTerrainPaintTests;Game.Tests.Editor.P109ExtendedBuildingsTests.CatalogAuthorsEveryEFacilityWithLivingBuildingCapabilities;Game.Tests.Editor.P109ExtendedBuildingsTests.FacilityAssignmentsAreExclusiveCapacityBoundAndDriveMedicineAndTraining;Game.Tests.Editor.P109ExtendedBuildingsTests.GateOpeningAndDestructionUpdateTheSharedOccupancyGrid;Game.Tests.Editor.P109ExtendedBuildingsTests.WearRepairUpgradeAndDemolitionConserveResourcesExactlyOnce" -testResults C:\serenity_game\Artifacts\WM15LivingBuildings\editor-delivery.xml -logFile C:\serenity_game\Artifacts\WM15LivingBuildings\editor-delivery.log
```

Result: process exit `0`; **86 total / 86 passed / 0 failed**; NUnit duration `57.7275414 s`;
wrapper wall time `141.6 s`. No C# compilation errors or unhandled exceptions.

Earlier focused evidence retained in the same artifact directory:

- `editor-gaps-c.xml`: 33/33 passed, including gate, doctor-shift, horse and promotion gaps.
- `editor-clinic-a.xml`: corrected clinic regression passed 1/1.
- `editor-final.xml`: prior broad checkpoint passed 86/86.

## Windows Development build

```text
Unity.exe -batchmode -projectPath C:\serenity_game -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/WM15LivingBuildings/Serenity.exe -quit -logFile C:\serenity_game\Artifacts\WM15LivingBuildings\build-final.log
```

Result: process exit `0`; `Build Finished, Result: Success`; `U00_BUILD_RESULT: Succeeded; errors=0;
warnings=85`; Unity build duration `140.104 s`, wrapper wall time `192.0 s`. Output:
`Builds/WM15LivingBuildings/Serenity.exe`.

## Startup optimization boundary

The asset fingerprint problem was handled separately by commit `90e9847c` (tracked active-asset baseline;
archives/demos kept outside build scope). During this delivery, experimental additional POI startup changes
were removed after a failed diagnostic test. The user then explicitly stopped further startup optimization.
No startup-performance completion is claimed here, and the passing living-building gate/build do not depend
on that discarded experiment.
