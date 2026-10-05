# U09 handoff — resources, inventories and storage

## Status and scope
U09 is DONE on 2026-10-05. It implements the FRS local physical-resource/storage foundation and connects U08 construction to real canonical resources. U10 Utility AI, U11 work groups/delivery, U15 production, individual durable items, spoilage, condition/quality, off-map aggregation and production UI were not started.

## 1–4. Resource model
- `ResourceId` is a strongly typed, case-sensitive lowercase ASCII content key (`a-z`, digits, `_`, `-`), independent from owner identity.
- `ResourceQuantity` is a checked nonnegative `long` bulk-unit count. `ResourceAmount` requires a strictly positive quantity.
- `ResourceDefinition` contains ID, display name and category; `ResourceCatalog` is immutable after construction and rejects duplicate/unknown IDs.
- Initial Stone Age definitions are `wood_log` (Construction), `stone` (Construction), `plant_fiber` (Construction) and `hide` (Other). `ResourceCatalogAsset` is only the Unity authoring adapter.

## 5–13. Inventories, ownership and transfer
- `ResourceInventory` is the only canonical mutable quantity container. It stores a sparse `ResourceId -> ResourceQuantity` map and removes zero entries.
- Capacity is one total integer bulk-unit ceiling. An optional category allow-list provides storage filtering; both are validated before mutation.
- `InventoryOwner` combines an `InventoryOwnerKind` with a `StableEntityId`. Current kinds are WorldPile, Character and BuildingStorage.
- Character inventory association reuses the character aggregate's stable ID; LocalGameplay creates two capacity-8 inventories.
- Storage association reuses the building stable ID. `StorageService.Attach` creates one canonical inventory only when the definition declares positive storage capacity.
- `WorldResourcePile` owns a stable pile ID, one resource ID, integer grid coordinate and an inventory-owner reference. Quantity is held only in its registered inventory, not in the presenter or pile record.
- `ResourceTransferService.TransferExact` is exact and atomic. The source must contain the entire amount and the destination must accept the entire amount; otherwise neither changes. A self-transfer is explicitly rejected without mutation.
- The conservation invariant is verified by failed-transfer and world→character→storage tests. A successful transfer changes location only; total units remain constant.
- `SettlementResourceView` is a read-only calculation over explicitly selected BuildingStorage inventories. It is not another mutable settlement stockpile.

## 14–21. Storage Basket and construction
- Storage Basket is still a normal U08 building definition/prefab. U09 adds a canonical BuildingStorage inventory with capacity 40 and all current categories; its presenter merely exposes the bound inventory.
- Construction costs are `IReadOnlyList<ResourceAmount>` on pure `BuildingDefinition`, authored by `BuildingPresentationCatalog`.
- `BALANCE_TBD`: Primitive Shelter costs 8 `wood_log` + 3 `plant_fiber`.
- `BALANCE_TBD`: Storage Basket costs 3 `wood_log` + 5 `plant_fiber`.
- Funding sources are an explicit deterministic owner sequence. `ConstructionFundingService.Evaluate` separately reports placement failure or insufficient resources and does not reserve/mutate state.
- U09 uses immediate exact consumption on successful confirm. Worker reservations, delivery and staged progress are deferred to U10/U11.
- Confirm is transactional: validate/plan deductions, confirm U08 placement, attach storage if any, deduct costs, invoke presentation callback, mark Completed.
- If charging, storage attachment or presentation fails, the compensation path refunds every deduction, removes the storage inventory, removes the building and releases its exact occupancy cells.
- Insufficient funding produces no building, no stable building ID in the registry, no occupied cell and no resource mutation.

## 22–24. Persistence boundary, future items and presentation
- `ResourceInventoryState` captures owner, capacity, category filter and deterministic resource amounts; restore validates the current catalog. World piles also have persistent stable identity and data-only coordinates. These are ready for a later world-save DTO.
- U09 intentionally leaves U04 `SaveSnapshot` format 1 and PostgreSQL schema unchanged. Adding world resources/buildings/characters to a durable snapshot requires an explicit versioned persistence task.
- Bulk resources are fungible integer units. A future `IndividualItem` aggregate is required for weapon/tool durability, unique quality, ownership history or other per-object state; those concepts must not be squeezed into `ResourceQuantity`.
- `WorldResourcePilePresenter`, `BuildingPresenter` and `ResourceDebugOverlay` only read/bind canonical objects. No quantity is stored in a MonoBehaviour, prefab or ScriptableObject. The overlay is one development-only `OnGUI`, not one Update per item/NPC.

## 25. Debug vertical slice and manual validation
- LocalGameplay creates `DEV_BOOTSTRAP_ONLY` finite piles: 30 wood, 20 fiber, 10 stone and 5 hide. Their four colored primitive projections are physically separate on the local map.
- Two U07 demo characters each receive a capacity-8 canonical inventory.
- Runtime can fund a Shelter or Basket from the explicit pile/storage owner sequence; failed affordability keeps the preview invalid.
- GPU-enabled U09 PlayMode saved `Logs/U09-runtime-resources.png`. Manual inspection confirmed the two bound Tier 1 characters and four distinct pile projections on the LocalGameplay ground. Automated checks additionally verified canonical quantities and ownership.

## 26. Tests
`ResourceInventoryTests` (10 EditMode tests) covers ID/quantity/catalog validation, capacity/filter/unknown IDs, detached restore, exact transfer and conservation, world→character→storage movement, construction costs, insufficient-funding no-op, forced post-charge rollback, storage respawn binding and authored catalog values.

`ResourceGameplayPlayModeTests` (4 PlayMode tests) covers LocalGameplay pile/character inventories, exact Shelter consumption, unfunded confirm with unchanged registry/occupancy/resources, and Storage Basket inventory identity across view respawn. The first test can also emit the GPU validation image when a graphics device is available.

## 27. Validation evidence
Commands used (Unity 6000.6.4f1):

```powershell
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-generate.log -quit -executeMethod Game.Infrastructure.Editor.U09ResourceFoundation.Build
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-core-tests.log -runTests -testPlatform EditMode -testCategory !Postgres -testResults C:\serenity_game\Logs\U09-core-tests.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-editmode-final2.log -runTests -testPlatform EditMode -testCategory U09 -testResults C:\serenity_game\Logs\U09-editmode-final2.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-all-playmode-tests.log -runTests -testPlatform PlayMode -testResults C:\serenity_game\Logs\U09-all-playmode-tests.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-playmode-final.log -runTests -testPlatform PlayMode -testCategory U09 -testResults C:\serenity_game\Logs\U09-playmode-final.xml
Unity.exe -batchmode -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-playmode-gpu.log -runTests -testPlatform PlayMode -testCategory U09 -testResults C:\serenity_game\Logs\U09-playmode-gpu.xml
Unity.exe -batchmode -nographics -projectPath C:\serenity_game -logFile C:\serenity_game\Logs\U09-build-final2.log -quit -executeMethod U00Build.WindowsDevelopment
```

Results:
- generator/asset validation: passed;
- core EditMode: 148/148 passed (contains inherited U06/U07/U08 and architecture checks);
- focused U09 EditMode: 10/10 passed;
- all PlayMode: 13/13 passed (contains inherited U07/U08 regressions);
- focused U09 PlayMode: 4/4 passed headless and 4/4 passed GPU-enabled;
- PostgreSQL: preserved successful 25/25 real integration result from `Logs/U04A-postgres-tests.xml`; not rerun because U09 changes no persistence/SQL/Npgsql code or dependency edge;
- Windows x64 Mono Development: Succeeded, errors=0, warnings=2 (inherited), output `Builds/Windows/Serenity.exe`, 667136 bytes;
- final logs: no compiler error, failed assertion, missing script/reference, runtime exception or shader error.

The earlier long verifier was not blindly restarted. Diagnostics found no live Unity/PostgreSQL/build process; the prior U07 Unity PID 6844 had already exited successfully. A later sandbox-only Unity crash at PID 22164 was identified from its `HttpListener/BuildReportRestService` stack and left no lock. All final Unity commands were started with a visible PID and bounded individual step. `Tools/Verify-U09.ps1` now prints PID/command, tails 100 log lines on failure/timeout, and terminates only the Unity PID it launched.

## 28. Deferred scope
- U10: central/ticked Utility AI for Tier 1; may query U09 read models and call Simulation actions, never mutate presenters.
- U11: work groups, construction delivery/reservations and player orders.
- U15: production/farming, replenishment and long-term economy.
- Later logistics/persistence: off-map aggregate stock, spoilage, item condition/quality, individual items and versioned durable world snapshots.

## Files
Created: Domain/Simulation/Presentation resource folders, `ResourceCatalog.asset`, U09 generator, two U09 test files, `Tools/Verify-U09.ps1`, this handoff and U09 validation XML copies.

Modified for U09: building definition/registry/placement rollback, building catalog/spawner/presenter/controller, LocalGameplay composition root, U08 regeneration preservation, building catalog asset, project state/task/decision/architecture documents. No vendor assets, `_Recovery`, unrelated scenes or ProjectSettings are part of the U09 commit.
