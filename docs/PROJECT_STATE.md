# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; Windows x64 Mono Development.

## Current milestone / last completed task
U09 DONE — Resources, inventories and storage. LocalGameplay now owns canonical bulk-resource inventories, physical world-pile projections, character carrying capacity, Storage Basket storage and construction confirmation that consumes real resources atomically.

## Active task
U10 — Utility AI Tier 1 is next and has NOT started.

## U09 resource foundation
- `ResourceId`, `ResourceQuantity`, `ResourceAmount`, `ResourceDefinition` and `ResourceCatalog` are pure Domain value/data types. Initial definitions are wood log, stone, plant fiber and hide.
- `ResourceInventory` is the only canonical mutable bulk quantity owner. It enforces total-unit capacity, optional category filters, exact add/remove semantics and detached capture/restore state.
- `InventoryOwner` combines WorldPile, Character or BuildingStorage with `StableEntityId`. Character inventories reuse character IDs; storage reuses the building ID; world piles own their own stable IDs and grid coordinates.
- `ResourceTransferService` moves exact amounts atomically and conserves stock on failure. `SettlementResourceView` derives totals from storage without introducing a second stockpile.
- `ConstructionFundingService` separates spatial and funding evaluation, charges exact costs on successful confirm and compensates charges, storage, registry and occupancy on any later failure.
- Data-driven `BALANCE_TBD` values are Shelter = 8 wood + 3 fiber, Basket = 3 wood + 5 fiber, Basket capacity = 40 bulk units.
- LocalGameplay `DEV_BOOTSTRAP_ONLY` resources are 30 wood, 20 fiber, 10 stone and 5 hide in four physical pile projections plus two capacity-8 character inventories.
- `ResourceDebugOverlay` is a development-only read-only aggregate display. Presentation binds to canonical inventory objects and does not serialize quantities.

## U08 building foundation
- `BuildingDefinitionId` is a validated lowercase content key and is distinct from every placed instance's GUID-backed `StableEntityId`.
- `Building`, `BuildingState`, `GridCoordinate`, quarter-turn `BuildingOrientation`, rectangular `BuildingFootprint` and `ConstructionState` are pure Domain types with no Unity references.
- `BuildingPlacementService` evaluates the rotated footprint against local build bounds and a logical `BuildingOccupancyGrid`; physics overlap is not canonical occupancy.
- `BuildingRegistry` owns canonical session instances and rejects duplicate stable IDs. Capture/restore preserves ID, definition, coordinate, orientation and planned/completed state.
- `BuildingPresentationCatalog` is a ScriptableObject authoring adapter for `primitive_shelter` (4x3) and `storage_basket` (2x1). Prefabs and catalog object identity are not saved state.
- `BuildingPresentationSpawner`, `BuildingPresenter` and `BuildingPresentationRegistry` are disposable projections. Despawn/respawn retains the same aggregate and ID.
- `BuildingPlacementController` is one scene-level Update. It reuses U05 `WorldPointerRaycaster` and its UI block, accepts `BuildableGround`, snaps to a configurable 1 m grid, rotates by R, confirms with left click and cancels with right click/Escape. B toggles the default shelter preview.
- Preview clones have no Domain aggregate or StableEntityId, disable colliders, use Ignore Raycast and display valid/invalid state through MaterialPropertyBlock tint without changing source materials.
- `LocalInteractionMode` prevents selection from consuming build clicks; the unscaled RTS camera remains active. Q/E continue to rotate the camera.
- Confirm now uses U09 atomic funding and completes immediately only after exact resource consumption. Worker delivery/reservations and Utility AI remain deferred to U10/U11.

## Preserved architecture and scope
- `Game.Domain` and `Game.Simulation` remain `noEngineReferences`; building position uses integer grid data and rotation uses an enum, not Vector3/Quaternion/Transform.
- U04 `SaveSnapshot` version 1 and PostgreSQL schema are unchanged. `BuildingState` and `ResourceInventoryState` are explicit extractable boundaries for a later world-save schema task.
- LocalGameplay remains the sole enabled player scene and still spawns the two U07 Tier 1 demo characters.
- No worker Utility AI, reservations/delivery, individual durable items, spoilage/quality, production, rooms, doors/windows, walls, multi-floor construction, destruction/fire, global map or production gameplay UI was implemented.

## Validation status
U09 validation completed on 2026-10-05 without rerunning the previously successful PostgreSQL gate:

- 148/148 core EditMode tests passed, including inherited U06/U07/U08 regressions and architecture boundaries;
- 10/10 focused U09 EditMode tests passed;
- 13/13 full PlayMode tests passed, including inherited U07/U08 regressions;
- 4/4 focused U09 PlayMode tests passed in headless and GPU-enabled runs;
- the latest preserved real PostgreSQL gate remains 25/25 passed; U09 changes no persistence/SQL/Npgsql code or assembly edge;
- U09 asset generation/catalog validation passed; the resource catalog, costs, capacity and catalog linkage reload successfully;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited warnings; `Serenity.exe` is 667136 bytes;
- GPU validation image `Logs/U09-runtime-resources.png` confirmed two Tier 1 characters and four distinct physical pile projections in LocalGameplay;
- logs contain no new compiler error, failed assertion, missing-script/reference diagnostic, runtime exception or shader error.

## Next action
Start only U10 from `docs/NEXT_TASK.md`. U10 has not been implemented.
