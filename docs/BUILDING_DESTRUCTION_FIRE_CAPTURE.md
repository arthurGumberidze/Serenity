# Building Destruction, Fire and Capture

## Authority and identity

U24 extends the existing canonical `Building`; it does not introduce destroyed, captured or combat shadow copies. `BuildingRegistry` still owns lookup by `StableEntityId`. A building retains that ID, definition and grid position through damage, fire, destruction and capture. `BuildingDamageService` is the only structural mutation boundary. Presentation, occupancy, storage and production remain projections or collaborating authorities.

`BuildingDefinition` supplies `MaxIntegrity`, fire and combat fixed-point damage multipliers, and capture eligibility. A `Building` owns current/max integrity, `BuildingStructuralStatus` (`Intact`, `Damaged`, `Critical`, `Destroyed`), `CombatSideId` owner, fire lifecycle/checkpoints, exactly-once damage-event IDs, destruction/spill markers and compact ordered history. Integrity is bounded at zero and never exceeds the configured maximum.

## Damage and destruction

`ApplyDamage(buildingId, damageEventId, amount, type, tick)` handles Generic, Combat and Fire damage. A stable damage-event ID is accepted once; repeated delivery does not damage or destroy twice. Definition multipliers are applied with integer arithmetic and the result is bounded. `ApplyCombatDamage` is the common U20 physical/U23 abstract hook, so both representations change the same integrity state.

Zero integrity transitions once to Destroyed. The building remains registered and continues to occupy its grid cells as ruins. This prevents immediate overlap and leaves a stable future repair/reconstruction target. U24 intentionally adds no repair economy, procedural fracture, debris physics or alternate ruins entity.

Destruction cancels uncommitted work for the site. A destroyed production or crop site is non-operational: it cannot reserve new work, grow, advance a task, deposit output or accept normal storage deliveries. Previously committed resources remain canonical and are never rolled back.

If the building has storage, `ResourceInventoryTransferService.SpillStorage` moves every positive quantity atomically from its `BuildingStorage` inventory to deterministic `WorldPile` inventories at the building position. It prevalidates the entire transfer and rolls back source quantities if commit fails. Empty storage is safe; the building spill marker prevents a repeated destruction call from creating duplicate piles.

## Fire lifecycle and weather

Fire state is engine-independent: `None`, `Burning`, `Extinguished` or `BurnedOut`, plus start tick, intensity and last processed checkpoint. `IgniteBuilding` and `ExtinguishBuilding` are reusable service commands. The centralized service advances only active fires in stable-ID order with a one-game-hour fixed step. Each step derives a stable damage-event ID from building ID and absolute tick, so catch-up, retries and save/load cannot replay damage.

`BuildingFireSettings` owns fixed-point base decay/damage and weather modifiers. Rain and heavy precipitation reduce effective intensity/damage; snow suppresses it further; hot weather can increase it. Weather is queried from the existing U19 deterministic service for the step tick. U24 adds no wildfire, adjacency propagation, smoke health model, firefighters or water logistics.

Direct advancement from a saved checkpoint and incremental hourly advancement are identical. Fire progression needs no building GameObject and therefore works for off-camera/Tier3 contexts. There is no per-building `Update`; the local composition root invokes one central fire advance from the canonical clock.

## Ownership and capture

The existing building now stores a stable `CombatSideId` owner. Capture mutates that field in place. A capture is valid only when the building exists, is not destroyed, is definition-capturable, the requested owner is different and non-empty, and the supplied canonical U20/U23 battle is finished with that hostile side as winner.

Capture preserves stable ID, definition, position, integrity, fire state and storage quantities. Stored resources stay physically in the captured storage; they are not teleported to the former owner. Any active production task is cancelled so an old-side worker cannot complete into the newly captured site. The intact site remains usable after revalidation even if the new owner lacks the research used for new construction; U16 research gating continues to govern only future placement.

U24 does not create territory control, occupation, pillage, faction economy or strategic AI. Those remain later-scope systems.

## Persistence and presentation

Snapshot v11 appends a sorted `BuildingWorldSaveState` containing structural state, owner, fire state/checkpoints, destruction/spill markers, processed damage IDs and history. Resource/production snapshots continue to own inventories, piles and tasks. Restore validates the detached state before replacing the registry and never reruns destruction or spill. Strict XML v1-v10 decoding remains supported. PostgreSQL continues to store the XML payload opaquely and requires no relational migration.

The development overlay displays identity, definition, owner, integrity/status, fire state, production availability, storage and capture eligibility. Damage, combat damage, destroy, ignite, extinguish and battle-validated capture controls call the service. The panel registers with the existing pointer blocker. Presentation uses a material property block for structural/owner tint and one lightweight ParticleSystem while burning; neither owns simulation state.

## Determinism and scope limits

All ordering uses stable IDs, all damage uses integer arithmetic and all fire checkpoints are absolute game ticks. U24 contains no random fire decision; therefore no new RNG stream is needed. Exact guarantees cover structural/fire/capture state, spills, task cancellation and save/load continuation. Rendering, particle trajectories and NavMesh motion are outside the guarantee.

Known limits: no fire spread, repair/rebuild jobs, siege weapons, breach simulation, destructible meshes, debris damage, evacuation, occupation/territory layer, owner-aware faction inventory or final UI. U25 global-map work was not started.
