# DECISIONS.md

## D-001 Engine
Unity 6.3 LTS (6000.3), C#, Windows PC. URP for MVP.

## D-002 Hybrid simulation
Tier1 = permanent important/nearby NPC domain objects + GameObject presentation as needed.
Tier2 = Entities/DOTS simplified agents in active city.
Tier3 = statistical city population stored as aggregate data.
Permanent named persons preserve StableEntityId across tiers.

## D-003 Time
Normal day at x1 = 24 real minutes. Biological age uses separate accelerated clock. Pregnancy target ~30-60 real minutes at x1; childhood several real hours; a generation roughly 8-12 real hours. Death from old age always exists.

## D-004 Off-camera simulation
World sections collapse to aggregate snapshots. Snapshots preserve composition, health, inventories/supply, morale, orders, production, relevant timers and RNG state/seed. Returning reconstructs detail without rerolling.

## D-005 Wealth/Influence
No full salary/market/inflation simulation. Wealth = property + business income + state rewards. Influence = office + dynasty + wealth + achievements + events.

## D-006 Non-negotiable game pillars
War; building; third-person control of monarch/captain; dynasty/family/inheritance; weather gameplay effects.

## D-007 Content architecture
ScriptableObject for immutable/authoring definitions. Runtime state is serializable models/DTOs with explicit save schema version.

## D-008 User-approved Editor version (2026-10-03)
The user explicitly requested keeping the installed Unity version and continuing U00. Target Unity 6000.6.4f1 (12bfff696524), overriding the original 6000.3 LTS requirement. Use the bundled URP blank template (17.2.1), URP 17.6.0, Windows x64 Mono Development builds. Do not upgrade the Editor automatically. No gameplay or U01 architecture is included.

## D-009 Assembly boundaries and composition (2026-10-03)
Runtime dependencies are one-way: Domain has none; Simulation references Domain; ECS and Presentation reference Domain plus Simulation; Infrastructure references all four as the outer composition layer. Tests reference all runtime layers but compile for Editor only. Domain and Simulation use `noEngineReferences` and must expose pure C# contracts. All project runtime asmdefs disable implicit auto-reference and unsafe code. New dependency edges require an architecture update and test change.

Canonical mutable game state belongs to Domain models and is changed through Simulation orchestration. Presentation and ECS are disposable projections; stable IDs bridge them to state, while Unity instance IDs and ECS entity handles remain transient. Infrastructure implements persistence/configuration/scene adapters and constructs the application. Saves occur only at a synchronization barrier after commands and ECS jobs commit. Package installation and concrete gameplay services remain assigned to their later tasks.

## D-010 Package and baseline configuration (2026-10-03)
The Unity 6000.6.4f1 package profile is pinned directly in Packages/manifest.json and fully resolved in Packages/packages-lock.json: Entities 6.6.0, Burst 2.0.0, Collections 6.6.0, Mathematics 1.4.0, Cinemachine 6.6.0, Addressables 2.11.2, Input System 1.20.0, AI Navigation 2.0.12, URP 17.6.0 and Unity Test Framework 1.8.0. Jobs and parts of the Burst/Mathematics surface are supplied by Unity 6.6 engine modules; no obsolete standalone Jobs package is added. Entities Graphics is deferred until U12 chooses a rendering need.

Package installation alone does not add new project-layer dependency edges. An asmdef references a package assembly only when the task introducing that API requires it. Addressables groups/catalogs and ECS systems are likewise created with their owning feature tasks. Windows x64 Mono Development, Linear color space, URP, Input System-only input, Force Text serialization and Visible Meta Files are the committed baseline. Existing Enter Play Mode settings disable domain and scene reload, so later bootstrap code must reset static state explicitly.

## D-011 Deterministic game clocks (2026-10-03)
Canonical time state is a serializable Domain object containing integer `TimeSpan` ticks for calendar and biological elapsed time, the selected supported speed and pause state. Simulation owns one central `GameClock` and advances it from an explicit elapsed-real-time input; the clock does not read `UnityEngine.Time`, set `Time.timeScale` or require per-entity update loops. A session driver, future DOTS system or offline aggregate runner can therefore supply the same input contract.

At x1, 24 real minutes advance one 24-hour calendar day. Speed is restricted to x1, x2, x3, x5 and x10. Active pause preserves the selected speed and all state while producing zero calendar and biological delta. Checked integer arithmetic rejects overflow before either clock is mutated, so large-step and split-step results are identical within the supported range.

Biological elapsed time is a separate stored clock, not an age derived from calendar time. Its default multiplier is 400x calendar time, configurable through pure Simulation settings. This follows the explicit U03 range of roughly 300–500x. The older FRS target of about nine real minutes per biological year does not numerically match that range, so exact life-stage balance is deferred to Character/Dynasty work. Pregnancy is explicitly a separate gameplay timer and is not implemented by U03.

## D-012 Stable identity and checkpoint persistence (2026-10-03)
U04 uses immutable `StableEntityId`, a non-empty GUID value object. Explicit NewId creates identity only for new domain records; Parse restores the saved value. Wire representation is lowercase 32-digit GUID N format; empty/default IDs are invalid. IDs belong to domain records, never views, names, array indices or transient ECS handles.

`SaveSnapshot` version 1 is immutable and contains session identity, all four U03 time fields and the biological multiplier needed for deterministic continuation with non-default settings. No wall-clock metadata or speculative character records are added. Slot identity is independent of session identity. Simulation owns synchronous `ISaveStore` and `SaveCoordinator`; Infrastructure owns a strict XML codec and local file provider. XML uses framework APIs without new packages, preserves 64-bit ticks and validates required/duplicate/unknown fields. Unsupported versions throw NotSupportedException; malformed or invalid data throws InvalidDataException. No migration is silently attempted.

Save/load runs on the simulation owner thread at a committed tick barrier. Capture copies mutable time state. Load constructs and validates a replacement GameClock before committing the clock/session pair; callers must rebind after successful Load. Storage never mutates runtime objects. The file provider flushes a same-directory temporary file then uses File.Replace (existing slot) or File.Move (new slot); it does not delete the old file or fall back to a non-atomic overwrite. Storage errors propagate. One writer per directory is required. Power-loss durability across all filesystems is not guaranteed. PostgreSQL and asynchronous orchestration remain U04A/later work.

## D-013 PostgreSQL durable persistence (2026-10-04)
U04A implements the existing `ISaveStore` boundary with Npgsql 8.0.9, selected because its .NET Standard 2.1 target compiles in Unity 6000.6.4f1 with the Windows Mono Development backend. NuGetForUnity 4.5.0 and its CLI restore the pinned package graph from nuget.org into an ignored directory; no provider binaries are committed and no ORM is introduced. Only Infrastructure and Editor tests explicitly reference Npgsql. Domain, Simulation, ECS and Presentation remain provider-free.

PostgreSQL is local durable snapshot/history storage. Active simulation remains in RAM. The adapter is synchronous and called only at a committed save/checkpoint barrier, never per frame, movement, HP change, NPC or AI tick. Configuration comes from `DYNASTYGAME_PG_*` process environment variables; only loopback hosts are accepted in U04A. Development and integration databases are separate (`dynasty_game_dev` and `dynasty_game_test`). Credentials and restored packages are excluded from Git, and public errors redact provider details and expose only operation plus SQLSTATE.

Schema `serenity` uses a checksummed `schema_migrations` ledger and one initial `save_sessions` table. Migration application takes a PostgreSQL advisory transaction lock; schema changes and ledger inserts share one transaction. Version/name/checksum drift, gaps and future database versions fail explicitly. Save uses one parameterized UPSERT, retaining creation time and atomically replacing the committed XML payload. Load checks redundant session UUID and save format metadata before returning a snapshot. PostgreSQL schema migration version and `SaveSnapshot.FormatVersion` are independent. StableEntityId maps exactly to PostgreSQL UUID and back.

The XML `SaveSnapshot` remains the canonical U04 payload rather than prematurely normalizing the future world model. Future Character, Dynasty, City and HistoricalEvent repositories will use separate persistence ports/tables and the same identity mapping. Timestamps are persistence metadata, not game time. The provider holds no runtime or GameClock reference; successful load still requires consumer rebinding as described by D-012.

## D-014 Local navigation and input boundary (2026-10-04)
U05 uses one dedicated `LocalGameplay` scene built entirely from project-owned Unity primitives. Vendor asset packs are not scene dependencies and remain U05A scope. Infrastructure provides one explicit serialized `LocalSceneCompositionRoot`; Presentation owns the camera, raw input adaptation, pointer raycasting and debug selection. No scene-wide lookup or service locator is used for runtime composition.

The local Input System asset has only Camera and Pointer maps. Camera bindings are WASD/arrows movement, configurable normalized edge scrolling, middle-mouse drag pan, wheel zoom and Q/E yaw. Pointer bindings expose position and primary/secondary click for future selection/orders. The input component emits state/intent and knows no Domain, simulation, persistence or PostgreSQL details. This boundary can later coexist with a separate U22 third-person map without sharing physical-input callbacks with gameplay rules.

RTS navigation operates in presentation time using `Time.unscaledDeltaTime`; active simulation pause therefore does not freeze camera movement. A yawed target rig is clamped to configurable rectangular XZ bounds. Zoom moves a fixed-pitch Cinemachine camera mount between configured distances and never advances or reads GameClock. Cinemachine drives camera presentation but does not own gameplay input. Camera distance and selection highlight are disposable view state, not canonical save data.

Pointer conversion is an explicit `TryGetWorldHit` / `TryGetWorldPoint` service over the output Camera and physics layers. If an EventSystem exists and reports pointer-over-UI, world interaction is blocked; U05 does not create an EventSystem because the scene contains no UI. The selectable markers and highlight are only an integration probe, not the future character/RTS selection system.

## D-015 Third-party asset ownership and normalization boundary (2026-10-04)
Asset downloads outside Unity live in the ignored `serenity_games_assets` staging tree. A file crosses into `Assets` only after its source, license, commercial-use status, visual role and concrete runtime need are recorded. Unknown provenance is `LICENSE_REVIEW_REQUIRED` and forbids runtime selection; a filename, package resemblance or generator metadata is never license evidence. Existing Unity vendor packages remain in place and are not treated as implicitly approved merely because they import.

Third-party source and Serenity runtime content are separate ownership zones. Vendor/source models, textures, materials and animation may be referenced, but gameplay-facing prefabs belong under `Assets/Game/Art/Prefabs`. Canonical state, StableEntityId, gameplay components and save ownership must not be placed in or coupled to a vendor hierarchy. Replacing a source package may change a wrapper's presentation dependencies without changing Domain or Simulation APIs.

One Unity unit equals one metre. Staging-derived FBX normalization is performed by an explicit allow-listed editor command, never by a global AssetPostprocessor. Model import settings handle scale when Serenity owns the selected imported copy; non-destructive wrapper transforms handle already-imported vendor content. Ground/contact and attachment pivots are solved at wrapper roots unless a source correction is clearly safer. Serenity-specific URP materials are separate from vendor originals.

The U05A gallery is development-only and excluded from Build Settings. `LocalGameplay` remains the only enabled scene. Addressables labels/groups, authored LOD production and final tier art are deferred until loading and presentation tasks establish an actual need. Placeholder classification is asset-readiness metadata and never implements the simulation tier system.

## D-016 Character aggregate and kinship ownership (2026-10-04)
Persistent people are pure `Game.Domain.Characters.Character` aggregates. `StableEntityId` is assigned only by `CreateNew`; `Restore` requires and preserves the saved identity. A detached `CharacterState` captures every U06 field in deterministic collection order and is the boundary for future save codecs and tier projections. U06 deliberately does not expand `SaveSnapshot` version 1 or add SQL; world-level character persistence requires an explicit later schema/version task.

Biological birth tick is the only canonical age source. Completed age uses 365-day years over U03 biological `TimeSpan` ticks; alive characters use the supplied current biological tick, while dead characters stop at their recorded biological death tick. Calendar time, wall-clock time and per-character timers are excluded.

Parentage is stored canonically on the child as stable-ID `ParentLink` values. A link distinguishes mother/father role, biological/legal/adoptive kind and whether the relationship is known to the character, allowing hidden biological and different legal fathers without duplicating mutable child lists. `CharacterRegistry.GetChildren` derives children from those links. Current spouse links are stable IDs and are made symmetric by the session-owned registry; historical marriages remain future dynasty work.

Traits, skills and professions use canonical data-definition keys rather than ScriptableObject or UI strings. Full NPC state requires four or five unique traits; skill and basic attribute values use 0–100 bounds. Body-health foundation covers head, torso, both arms and both legs without implementing injury/disease gameplay. Wealth and Influence are distinct 0–1000 domain values. Family and dynasty are optional `StableEntityId` hooks, not speculative aggregates.

`CharacterRegistry` is an ordinary in-memory session index, not a singleton or PostgreSQL repository. It rejects duplicate active character IDs, provides deterministic lookup/listing and derives children. Presentation, ECS and storage adapters may project the aggregate but cannot own its identity or canonical state. No assembly dependency edge changes in U06.

## D-017 Tier 1 character presentation ownership and pause behavior (2026-10-05)
Tier 1 characters are disposable GameObject projections of existing `Game.Domain.Characters.Character` aggregates. `CharacterPresenter.Bind` receives the aggregate and a session-owned `CharacterPresentationRegistry`; it exposes the aggregate's existing `StableEntityId` and never generates, serializes or replaces identity. The registry rejects simultaneous duplicate views for one character and is cleared by explicit unbind, spawner despawn or presenter destruction. Destroying a view does not remove the character from `CharacterRegistry`.

`CharacterPresentationCatalog` is a presentation ScriptableObject mapping the current deterministic Male/Female descriptors to Serenity-owned wrappers. Hodaart Characters 01/02 remain temporary U05A-approved placeholders; replacing their catalog entries or wrapper visual children must not change Domain, saves, simulation or identity. `CharacterPresentationSpawner` materializes only a supplied existing aggregate and receives world transform separately.

The project-owned Tier 1 Animator controller starts in Idle and exposes `Speed` and `Moving`; U07 uses `Speed` for idle/walk/run presentation only and adds no navigation or AI. Animator evaluation is presentation state. `SetSimulationPaused` freezes/resumes the Animator locally without changing global `Time.timeScale`; camera navigation therefore remains active and no biological/calendar time is advanced by the presenter. A later session pause coordinator may fan this explicit call out to active presenters.

## D-018 Building placement, occupancy and projection boundary (2026-10-05)
Placed structures are pure `Game.Domain.Buildings.Building` aggregates. A lowercase validated `BuildingDefinitionId` identifies content, while each placed instance owns an unrelated GUID-backed `StableEntityId`. `BuildingState` stores only definition ID, integer grid coordinate including an explicit level, quarter-turn orientation and construction state. `CreateNew` generates identity only after valid confirm; `Restore` preserves it. The U04 `SaveSnapshot` version-1 wire format and PostgreSQL schema remain unchanged; U08 provides a detached data-only state boundary for a later world-save schema migration.

Definitions are authored in one `BuildingPresentationCatalog` ScriptableObject, but the catalog and prefabs are not canonical mutable state. The catalog creates engine-free `BuildingDefinition` values with category, rectangular footprint, placement mode and overlap rule. U08 implements the grid mode only; Free and Edge are explicit future modes. Primitive Shelter and Storage Basket are current project-owned U05A placeholders, not hardcoded building subclasses or inventory implementations.

`BuildingPlacementService` owns deterministic bounds and logical occupancy rules in Simulation. Presentation converts pointer hits from the existing U05 raycaster into configurable one-metre grid coordinates. The rectangular footprint rotates at 90-degree increments, all occupied cells must be inside local bounds, and non-overlap definitions reserve cells atomically before entering the domain registry. Physics colliders support pointer/visual interaction but are not canonical occupancy.

The Building input map uses B to toggle the default shelter preview, left click to confirm, right click or Escape to cancel and R to rotate; Q/E remain camera rotation. A session-owned `LocalInteractionMode` prevents selection from consuming placement clicks while leaving the camera enabled. Preview clones are unbound, collider-disabled, ignore raycasts, receive green/red MaterialPropertyBlock tint and never receive an ID or registry entry. Confirm creates one planned aggregate, explicitly completes it for the resource-free U08 vertical slice, reserves occupancy and spawns a bound projection. Resource consumption and worker construction are U09/U10 work.

`BuildingPresentationRegistry` and `BuildingRegistry` are separate session-owned indexes. Destroying or unbinding a `BuildingPresenter` removes only the view mapping; respawn binds the same aggregate and stable ID. No singleton, SQL, Npgsql, GameObject, Transform, Vector3 or Quaternion enters Domain/Simulation building state.

## D-019 Canonical bulk resources and atomic construction funding (2026-10-05)
U09 models MVP resources as integer bulk units. `ResourceId` is a validated lowercase content key; `ResourceQuantity` is a nonnegative 64-bit value and `ResourceAmount` is a positive `(id, quantity)` pair. Definitions are immutable pure data produced from `ResourceCatalogAsset`; mutable quantities exist only in `ResourceInventory`. World piles, characters and building storage use one `InventoryOwner` key containing an owner kind and `StableEntityId`, so presenters and scene objects cannot become canonical inventory state.

Transfers are exact and atomic: the source must contain the whole amount and the destination must accept the whole amount under capacity/category rules before either inventory changes. Failed transfers conserve both inventories. Settlement totals are a read-only projection over selected storage inventories, never a second mutable stockpile. `ResourceInventoryState` and pile identity/coordinate provide detached save-ready boundaries, but U09 deliberately does not change the U04 version-1 time snapshot or PostgreSQL schema.

Construction confirmation is one Simulation transaction. Spatial validity and affordability are evaluated separately. Confirm reserves occupancy and registers the building, creates any storage inventory, deducts exact resource costs, invokes presentation, and completes the building. Any exception refunds deductions and removes storage, registry and occupancy state. Insufficient funding mutates nothing and creates no persistent building identity. U09 consumes resources immediately on successful confirm; worker delivery/reservations and staged progress remain U10/U11 work.

## D-020 Centralized Tier 1 Utility AI and logistics claims (2026-10-05)
Tier 1 AI is session-owned simulation state keyed by the existing Character `StableEntityId`, not a component-owned copy of character state. One `Tier1AiScheduler` advances bounded needs, utility decisions and action state machines from explicit U03 calendar deltas. Stable-ID ordering, deterministic phase staggering, fixed batch caps and finite normalized scores make equal-state decisions predictable. There is no per-character AI `Update`; a single Presentation runtime driver connects the scheduler to Unity frames and passes zero simulation delta while paused.

Utility selection and execution are separate. U10 implements only Idle, Rest and autonomous Haul, with a switch margin and non-interruptible carrying phase. Haul work is discovered from U09 registries, selected by distance with stable-ID tie-breaks and protected by session-ephemeral quantity/capacity claims. Claims do not own resources: pickup and dropoff use only `ResourceTransferService`, so cancellation before pickup leaves stock at source and cancellation after pickup leaves stock in the character inventory. Work groups, player priorities, construction labor and production remain U11/U15 concerns.

Movement is an application boundary. Simulation emits `MoveTo(WorldPosition)` through `ITier1MovementDriver`; Presentation implements it with AI Navigation 2.0.12 `NavMeshAgent`, reports Idle/Moving/Arrived/Failed and drives the existing U07 animation parameters. NavMesh paths, task/claim execution and presentation objects are not serialized. U10 leaves the U04 snapshot/SQL schema unchanged and records dynamic obstacle/rebake and durable in-flight task semantics as future explicit work.

## D-021 Read-only canonical development selection (2026-10-05)
U10 development inspection extends the existing U05 pointer raycast and `SelectionProbe`; it does not create a gameplay selection model or U27 UI. The probe may retain disposable presenter references for the currently hit Character, WorldResourcePile or Building, while all displayed identity, quantities, capacities, action phases and claims are read on demand from their bound Domain/Simulation objects and registries.

Debug snapshots are short-lived immutable render/test values, never canonical storage. `HaulClaimRegistry` exposes deterministic read-only query results, and the resource overlay recomputes location totals from `ResourceInventoryRegistry` for every draw. UI code cannot transfer resources, create claims or retain an independently mutable balance. Player work groups/orders remain U11 scope.

FRS supplies no exact MVP costs or capacities. Current `BALANCE_TBD` authoring values are Primitive Shelter = 8 wood logs + 3 plant fiber, Storage Basket = 3 wood logs + 5 plant fiber, and basket capacity = 40 bulk units. LocalGameplay's 30 wood, 20 fiber, 10 stone and 5 hide piles plus two capacity-8 character inventories are `DEV_BOOTSTRAP_ONLY`, not production new-game balance. Individual durable items, spoilage, weapon condition, quality, off-map aggregation and production chains remain U15/later scope.

## D-022 Work-group identity, manual-order lifecycle and AI precedence (2026-10-05)
Work groups are canonical session-domain data with their own GUID-backed `WorkGroupId`; their members and optional commander are existing character `StableEntityId` references. A character may belong to at most one group. Membership is deterministic and survives disposable presenter loss, while durable group persistence remains a later explicit snapshot/schema migration.

Player work uses typed session-local `JobId` values and explicit Queued, Assigned, Active, Completed, Cancelled and Failed states. Move targets are world positions; resource and actor targets use stable IDs. `WorkManager` owns creation, assignment, priority, status queries and exclusive target reservations. Group commands create deterministic per-member orders rather than a shared mutable presenter task. Eligibility is a replaceable Simulation policy hook; the U11 default requires a living registered character.

Manual orders participate in the one centralized U10 scheduler. Non-interruptible carrying wins first, critical Energy (`<= 0.10`) wins next, then manual/group work, then autonomous utility. Manual work may preempt an interruptible autonomous action; critical rest requeues unfinished manual work. Manual Haul uses the existing quantity/capacity claims and only U09 atomic transfers, so cancellation or failure releases reservations without manufacturing, deleting or teleporting stock.

Shift multi-selection, Ctrl+number group assignment, number-key recall and the work overlay are development command/input surfaces. They may issue Simulation commands and show immutable snapshots but do not own group, job, inventory or AI state. U27 remains responsible for production gameplay UI and persistence of key bindings.

## D-023 Tier 2 projection ownership and explicit DOTS stepping (2026-10-05)
U12 represents a character in Tier 2 as an ECS projection, never as a second independent `Character`. `Tier2StableIdentity` is the unmanaged 16-byte encoding of the existing Domain `StableEntityId`; the materializer generates no identity and rejects duplicates. ECS `Entity` handles and the boundary-side lookup index are session-local. Domain and Simulation retain no `Unity.Entities` dependency.

The U12 component set is deliberately narrow: stable identity, biological/lifecycle facts, position, lightweight movement and explicit simulation progress. The five components total 92 bytes before ECS chunk overhead and contain no managed references. Family, relationships, full health, traits, skills, professions, inventories, U10 utility execution and U11 group/job authority are not copied into ECS. Domain remains authoritative for durable/unprojected state; while a Tier 2 projection is active, ECS is authoritative only for its supported mutable position/movement/progress fields until extraction after all tracked jobs complete.

`Tier2Runtime` owns a private ECS `World`, a materialization/extraction boundary and one `Tier2SimulationSystem`. The system schedules a parallel Burst `IJobEntity` and receives calendar/biological deltas solely through a singleton command populated from U03 `GameTimeAdvance`. Zero deltas are a complete no-op, so active pause cannot advance movement or progress. The runtime does not read wall time, `Time.time`, `Time.deltaTime` or `Time.timeScale`.

U12 intentionally does not attach this private world to the default player loop, create a runtime MonoBehaviour, render entities or transition real Tier 1 characters. U13 must coordinate one active representation, complete jobs at transfer barriers and reuse `Tier2TransferState` rather than inventing another identity/state path. Tier 3 aggregation and deterministic off-camera rules remain U13/U14.

## D-024 Transactional tier lifecycle and representation authority (2026-10-06)
`TierManager` is the single session-owned coordinator for named-character Tier 1, Tier 2 and Tier 3 representations. It lives in engine-free Simulation and calls one adapter per tier, preserving the existing dependency graph. A managed character has exactly one declared active representation. Same-tier transitions are no-ops; re-entrant transitions are rejected. A target is validated before source release, the source state is captured at an explicit barrier, and target failure triggers source rematerialization from the captured state.

Authority is field-scoped. `Character` remains canonical for identity, name, sex, birth/death, parents, spouse, family/dynasty, traits, skills, health, profession, Wealth/Influence and relationships in every tier. U09 character inventory and U11 work-group/order state remain in their existing stable-ID registries and are never copied into ECS or Tier 3. `CharacterRuntimeState` is the transition DTO for position, lightweight velocity/movement, processed time/step progress, coarse location key and current U10 Hunger/Energy continuity. Tier 2 owns its projected position/movement/progress only while active; extraction completes tracked jobs before the entity is destroyed.

Tier 1 demotion stops and discards the NavMesh path because it is presentation-only. Scheduler unregister cancels Tier 1 execution, releases U10 claims and requeues an active U11 manual order while preserving its assignment. Resources already carried remain in the canonical character inventory. Returning to Tier 1 creates one presenter through the existing deterministic sex-to-prefab catalog and rebinds one centralized AI agent with retained needs; no appearance or character data is rerolled.

Tier 3 named-person state is a pure C# `Tier3CharacterRecord` keyed by the same `StableEntityId`. It contains restoration/runtime continuity and a location key, not a copied biography or a GameObject/ECS Entity. U13 does not advance Tier 3 records. U14 owns deterministic off-camera progression and any controlled RNG state.

Automatic representation choice is a separate `TierDistancePolicy`, not a source of truth. Its thresholds define hysteresis bands, minimum residency is counted in deterministic evaluations, stable-ID iteration is bounded, and transitions per evaluation are capped. Camera position selects a representation only; it never changes canonical character results. Default distances and budgets are `BALANCE_TBD` implementation settings pending U29 profiling.
