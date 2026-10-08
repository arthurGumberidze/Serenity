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

## D-025 Development UI blocks world pointer input before selection (2026-10-06)
All development command/read panels participate in the existing `WorldPointerRaycaster` UI boundary through registered GUI-space rectangles. Pointer coordinates are converted once from Input System screen space to IMGUI space before physics. The raycaster checks UI before any world raycast, and `SelectionProbe` treats a UI-blocked click as consumed rather than as an empty-world miss; therefore it never clears selection because a debug control was pressed.

The U11 work panel routes its visible buttons through the existing `LocalGameplayInputSource.PrimaryClicked` event and resolves the clicked command from the same rectangles used for drawing. Selection remains the existing disposable Presentation state, and work groups/orders remain Simulation/Domain state. EventSystem UI remains supported by the same raycaster boundary. No second selection authority, workaround selection cache or post-error restoration path is introduced.

## D-026 Deterministic named-character off-camera catch-up (2026-10-06)
U14 uses one engine-free `OffCameraSimulationService` with an explicit saveable world seed and a one-calendar-day fixed step. The service accepts absolute U03 calendar/biological ticks, never wall time, frame delta, camera state or Unity random state. Partial-day calls commit the exact target tick but perform a fixed step only when an absolute day boundary is crossed, so one 24-hour call and twenty-four one-hour calls are identical.

Random samples are counter-keyed by `(world seed, StableEntityId, absolute fixed-step index, stream ID)`. There is no shared mutable RNG cursor; input collection order, neighboring characters and materialization history cannot shift another character's samples. U14's `AbstractActivityProgress` and deterministic accumulator are generic audited long-horizon continuity only. They intentionally do not manufacture resources, execute jobs, change health/family or implement U28 events. The deterministic state hash covers seed, identity, location and U14 continuity fields in stable-ID order and excludes GameObject/ECS handles, wall time and dictionary order.

`CharacterRuntimeState` remains the one U13 transition DTO and now carries the last committed absolute calendar/biological ticks, completed remote steps, deterministic accumulator and abstract activity progress. Tier 1 and Tier 2 adapters retain those fields unchanged. The Tier 3 adapter validates and catches up before materialization and again before capture/promotion; automatic runtime batching traverses active Tier 3 records at most once per fixed day and skips same-day frame calls in O(1). This makes Tier3→Tier1/Tier2 promotion an exact latest-state barrier and makes Tier1/Tier2→Tier3 begin from the carried checkpoint without double time.

The existing `Character`, inventory registry and work registry remain canonical. U14 does not run Tier 1 Utility AI, NavMesh, physical hauling or production while remote; Hunger/Energy, physical resources, group membership and assigned work are preserved. Biological age continues to derive from U06 birth tick evaluated against the U03 biological clock; U14 does not add a second age or implement natural-death policy, disease or injury. Durable world-save/schema integration is deferred, but every U14 continuation field is detached and constructor-restorable, and split-session tests prove restart determinism without serializing an RNG object.

## D-027 Tier-aware RTS manual Move command (2026-10-06)
Right-click movement reuses the existing U05 `Pointer/SecondaryClick` and `WorldPointerRaycaster`; there is no second raycast, camera projection or UI-block authority. One Presentation coordinator distinguishes building-placement cancellation from ground Move, resolves selected characters or the active U11 work group, and submits only world intent. EventSystem and registered IMGUI rectangles block the command before physics.

`ManualMoveCommandService` is an engine-free Simulation boundary. It creates one U11 Move `WorkOrder` per executable member in stable-ID order and assigns deterministic 1.5 m centred formation slots (`BALANCE_TBD`). Physical execution remains the U11 manual action through U10 `ITier1MovementDriver`; Presentation/selection code never calls `NavMeshAgent.SetDestination`.

Only a current Tier 1 representation may receive a physical Move job. `TierManager` implements read-only `ICharacterTierLookup`; Tier 2/Tier 3 members produce explicit `TierUnavailable` results with no job and no hidden promotion, remote pathfinding or U14 mutation. Existing open work produces `AlreadyHasWork`. This preserves U13 representation authority and U14 deterministic off-camera state while leaving future Tier 2/remote order execution for an explicit FRS-backed task.

## D-028 Reusable production authority and canonical food (2026-10-06)

`berries` is the one canonical U15 food resource. Production definitions are immutable engine-independent recipes with declared inputs, outputs, work ticks, enabled state and compatible site kind. Mutable source, crop and task progress belongs to stable-ID domain objects coordinated by one `ProductionService`; no recipe, vendor prefab, presenter or ScriptableObject owns mutable stock. This permits later input chains such as ore -> metal -> weapon to reuse the same catalog and commit rules without implementing those U16+ contents now.

Production creates output for exactly one canonical U09 owner. A Tier 1 batch prefers its character inventory, then a valid completed building storage if the character cannot accept the whole batch. Physical delivery uses the existing U09 transfer service; if storage becomes full, stock remains in the character inventory. Aggregate tiers may deposit after completion, but never create a second balance. Recipe inputs and outputs are all-or-nothing at the batch boundary; natural source capacity decrements exactly once.

## D-029 Production reservations, tiers and deterministic time (2026-10-06)

One active `ProductionTask` exclusively reserves one `ProductionSite` and one worker. Cancellation releases both claims; completion releases them only after the batch commit. Active task IDs, worker/site IDs and accumulated/required work are persisted, and restore reconstructs claims only after validating unique IDs and matching site/recipe relationships. Reservations are session-owned maps, not database locks.

Tier 1 physically moves, works and delivers through the centralized U10 scheduler and movement bridge. Tier 2 accumulates `PendingWorkTicks` in the existing Burst ECS pass, then crosses a completed-job barrier and commits sorted stable IDs through `ProductionService`. Tier 3 supplies its sorted stable IDs directly to the same fixed-step service. All aggregate production, crop growth and food consumption advance on absolute U03 calendar ticks in one-day steps; traversal input is deduplicated/sorted, hashes exclude representation handles, and U15 introduces no random stream. Tier changes and time chunking therefore do not alter the economic result.

## D-030 Primitive farming and production persistence v2 (2026-10-06)

The first cultivated site is `primitive_crop_plot`, a U08 `BuildingDefinition` whose placed stable ID is also its production-site ID. U08 occupancy and U09 construction/activation remain the only placement path. Its abstract deterministic lifecycle is Unplanted -> planted work -> fixed growth -> Ready -> harvest work -> Unplanted. Soil, seeds, seasons, weather, irrigation and crop disease remain explicitly deferred. The Storage Basket prefab is a temporary project-owned placeholder visual, not crop state.

`SaveSnapshot.CurrentVersion` is 2. The production payload stores absolute production tick, food remainder/consumed/shortage, targets, sites, active tasks, produced totals and all canonical inventory states. XML decoding remains strict and capped at 4 MiB; legacy version 1 time-only saves remain readable with no production payload. PostgreSQL stores and validates the versioned XML as before, so no SQL schema migration or realtime database authority was introduced.

## D-031 Canonical research authority and typed unlock availability (2026-10-06)

One session-owned, engine-independent `ResearchService` is the canonical settlement research authority. Definitions use stable `TechnologyId` values, explicit prerequisite IDs, tick costs, Stone Age era metadata and typed building/production-recipe/capability unlocks. `TechnologyCatalog` fails fast on duplicate or unresolved IDs, self-dependency, cycles and unlocks that do not resolve against the real U08/U15 catalogs.

Research never removes, clones or mutates building/recipe definitions. U08 construction and U15 production accept a read-only `IResearchUnlockView` and decide availability at the action boundary. A programmatic locked construction request returns `LockedByResearch`; an isolated system fixture with no injected research view preserves its intentional unlocked sandbox behavior. Unlock state is derived from completed technologies rather than persisted as redundant truth.

## D-032 Research labor uses absolute deterministic checkpoints across tiers (2026-10-06)

Research progress is integer game-time work with a fixed-point rate-per-thousand remainder per assigned stable worker. The default character rate is a small explicit hook over Intelligence and optional `research` skill; no wall clock, coroutine or random stream contributes. Completion computes the reproducible absolute tick and commits a technology once. A lower stale checkpoint from a tier transition or teardown is an idempotent no-op, while forward calls consume only their unprocessed interval.

Tier 1 adds Research to U10's centralized Utility AI; Tier 2 and Tier 3 submit stable IDs after their existing batch barriers to the same service. There is no research MonoBehaviour per worker/project, no GameObject in Tier 2 and no off-camera copy. One active settlement project is the deliberate MVP scope; queueing and physical libraries/universities are deferred.

## D-033 Research persistence version 3 and Stone Age completion (2026-10-06)

`SaveSnapshot.CurrentVersion` is 3 because research adds durable completed IDs, active project/progress, assignments, fractional remainders and the last processed absolute tick. Versions 1 and 2 remain strictly readable; missing research payload means no research state to restore. PostgreSQL continues to validate and store the opaque versioned XML, accepts metadata versions 1–3 and requires no SQL migration or realtime query path.

The current catalog is Stone Age only. `stone_age_mastery` depends on all required late branches and unlocks `stone_age_completed`; `ResearchService.StoneAgeCompleted` exposes that capstone without starting a later era. Post-Stone-Age content remains U38.

## D-034 Player-controlled research and disposable debug controls (2026-10-07)

Research assignment is an explicit player/debug command over the canonical `ResearchService`; LocalGameplay composition does not implicitly assign demo characters. Batch assignment is idempotent and stable-ID ordered. Assigned means present in `ResearchService.AssignedWorkers`. Active means that same assigned ID currently has authoritative Tier 1 AI action `Research` in phase `Researching`; visual state and assignment alone are insufficient. The research panel derives its rate and ETA from Active workers and explains a zero-active stall.

The U16A building selector is development-only until U27. It enumerates the existing `BuildingPresentationCatalog`, derives availability and unlock reasons from the live U16 catalog/service, and passes the selected `BuildingDefinitionId` to the existing U08 controller. The controller itself enforces `LockedByResearch`, so UI state is explanatory rather than authoritative. Current U16 starting knowledge remains unchanged; shelter, storage and crop availability follow their canonical technologies.

Time input remains in the Unity Input System. Space toggles pause, Minus steps slower and Equals/Plus steps faster through x1/x2/x3/x5/x10. Digits 1–9 remain exclusively U11 work-group recall keys. `Tier1AiRuntimeDriver` is the debug command coordinator for pause/speed and mutates the one `GameClock`; its zero-delta path is the shared pause barrier for AI, tier batches, production and research. No second time service or final U27 HUD was introduced.

## D-035 Named agnatic primogeniture policy (2026-10-07)

The first succession policy is `agnatic_primogeniture_v1`. This follows FR-MON-002/003: only living male members of the ruling dynasty can inherit under the base law. Eligibility is separate from traversal: an ineligible or dead child is skipped, but living male descendants of that branch keep its primogeniture position. Branches sort by biological birth tick and then canonical `StableEntityId` text; no list insertion, scene spawn, GameObject or random order participates. Naming the policy leaves a clean replacement seam for later law changes without embedding gender rules in `DynastyState`.

## D-036 Event-driven exactly-once succession and extinction (2026-10-07)

`SuccessionService` is the single authoritative boundary from U06 `Character.MarkDead` to ruler succession. It queries canonical characters through stable IDs and updates `DynastyState`, `PlayerRealmState` and `GameRunState` in one transaction result. Each deceased monarch ID is persisted beside exactly one ordered history entry. Reprocessing the same death is a no-op; it cannot create another history entry or reroll the heir. No valid heir sets the dynasty Extinct and the run to GameOver/DynastyExtinction; no unrelated NPC or generated replacement is permitted.

## D-037 Version 4 character and dynasty persistence (2026-10-07)

`SaveSnapshot` version 4 persists complete detached `CharacterState` records together with dynasty identity/members/rule/status/history/processed markers, the player ruler ID and game-run state. Family relationships remain owned by Character rather than duplicated into a dynasty graph. Load validates duplicate/missing/self/cyclic/asymmetric references before mutating the active registry and preserves existing canonical Character objects when identities match. Versions 1–3 remain readable. PostgreSQL continues storing the strict XML payload as opaque bytes, so no relational migration is added.

## D-038 Canonical batched health and capability authority (2026-10-07)

`HealthAndFamilyService` owns condition/event/pregnancy progress by `StableEntityId` and mutates the existing Character aggregate. Effective work, movement, research, combat and fertility capability is derived from base age/life plus active data-driven conditions. U10/U15/U16 consume that query; no tier, presenter, MonoBehaviour or ECS projection owns copied health truth or a per-character update.

## D-039 Deterministic simulation-derived identity and birth (2026-10-07)

Manual entity creation keeps `StableEntityId.NewId()`. Simulation-derived condition/event/pregnancy/child IDs use the narrow `FromDeterministicHash` factory over world seed, purpose, canonical parent/target IDs and absolute ticks. Birth creates one canonical Character, writes U06 parent links, enters the active U17 dynasty when applicable and publishes only a materialization notification. This makes save/restart, collection order and tier routes identity-equivalent.

## D-040 Health death and family events reuse existing authorities (2026-10-07)

Fatal conditions call `SuccessionService.ProcessDeath`; they do not duplicate life/succession state. Family events update existing bounded Character relationships and persist deterministic event markers. U18 remains a narrow foundation rather than the U28 event engine, medical economy, epidemiology or genetics system.

## D-041 Version 5 health and family persistence (2026-10-07)

Snapshot v5 adds health/family world seed and checkpoints, condition lifecycle/death markers, relationship-event markers and pregnancy/birth markers. Canonical children, parent links and relationship results remain in U17's character/dynasty payload. Versions 1 through 4 load with no health payload. PostgreSQL continues storing opaque XML and needs no relational migration.

## D-042 Canonical keyed daily weather and derived seasons (2026-10-08)

Season is derived from the existing U03 calendar and is never independently mutable. The initial configurable 365-day mapping begins Spring/Summer/Autumn/Winter on days 1/92/183/274. `WeatherService` derives a day from `(world seed, climate profile ID, absolute calendar day, stream ID)` with U14's keyed RNG; it keeps no sequence cursor or daily history. Camera, scene, tier, render frame and collection traversal cannot reroll weather.

Temperature is integer deci-Celsius. One validated `ClimateProfile` owns four seasonal temperature/weight/growth definitions and a complete, unique weather-effect table for anomaly, precipitation range, wind range and gameplay modifiers. The temperate profile is current content, while the catalog rejects duplicate profile IDs. Rain is converted to snow at or below the configured threshold and configured snow is converted to rain above it; no accumulation, moisture or physical climate simulation is introduced.

## D-043 Weather modifiers cross existing production, movement and health authorities (2026-10-08)

U15 recipes now declare only `Outdoor`/`Sheltered` exposure and optional seasonal-wild-source metadata. The existing `ProductionService` multiplies base crop progress by seasonal/weather crop rate, and worker capability by outdoor-weather and optional wild-food rates. Aggregate workers consume raw elapsed time even when effective work is slowed, preserving Tier1/Tier2/Tier3 equivalence and output ownership. Winter has a zero seasonal crop rate; Ready crops and stored food remain intact.

Movement reads one weather interval modifier: Tier 1 applies it through the existing NavMesh movement bridge and Tier 2 applies it in the batched ECS job. U19 adds no per-character update. Severe outdoor cold/heat calls the U18 authority with `cold_stress` or `heat_stress`; a stable-ID/day checkpoint makes exposure exactly once. Because canonical indoor/outdoor location does not yet exist, exposure is limited to active outdoor production workers and aggregate Tier2/Tier3 workers rather than inventing shelter or clothing state.

## D-044 Version 6 weather persistence and disposable presentation (2026-10-08)

Snapshot v6 persists world seed, climate profile ID and sorted last-processed exposure day per character. Daily weather and season remain derived, so load on the same day cannot reroll and no redundant history is stored. Versions 1-5 remain readable with no weather payload. PostgreSQL accepts v6 opaque XML metadata and needs no schema migration or realtime weather query.

Environment fog/ambient/directional-light changes and the debug overlay are projections only. They refresh centrally, own no canonical state and are not saved. The debug panel shows current state/modifiers and a deterministic next-day preview, registers with the U16A pointer blocker and exposes no persistent forced-weather override.

## D-045 Stable-ID local combat authority and tactical sides (2026-10-08)

`CombatService` is the only mutable U20 encounter authority. It references canonical Characters by `StableEntityId`, owns deterministic participant/target/cooldown/morale/retreat/outcome state and advances from the existing absolute game clock in one central batch. `CombatSideId` is a narrow encounter allegiance used to distinguish two hostile sides; it is not a U21 army, captain or strategic-faction aggregate. Presentation, NavMesh agents and selected GameObjects cannot own combat truth.

## D-046 Deterministic melee reuses U18 death and pins Tier 1 (2026-10-08)

U20 resolves melee as deterministic always-hit attacks with immutable weapon profiles, stable-ID ordering and a fixed impact-to-injury ladder. All persistent injury, incapacity and death mutation goes through `HealthAndFamilyService.ApplyCombatInjury`; fatal resolution therefore continues through U17 `SuccessionService` exactly once. While an encounter is active, combat owns the character's external activity and blocks Tier1 demotion. This keeps physical movement local without inventing U21/U23 aggregate battle state.

## D-047 Combat snapshot v7 and disposable tactical controls (2026-10-08)

Snapshot v7 persists detached combat encounters, participants, targets, attack sequence/cooldown, morale/retreat, attack history and outcome. Restore follows canonical Character/dynasty and U18 health restore, validates every cross-reference and does not replay attacks or deaths. Versions 1-6 remain readable; PostgreSQL stores v7 as the existing opaque validated XML with no schema migration. Right-click Attack and the IMGUI combat panel are adapters over the service and existing pointer blocker; final UX remains U27.

## D-048 Military units reference canonical Characters and preserve roster history (2026-10-08)

`MilitaryUnitService` owns unit identity, owner/side, membership history, captain, representation, order, abstract-location hook, summary and encounter link. A roster member is only an existing `Character.StableEntityId`; the unit never copies biography, health, dynasty, inventory, presenter or ECS state. Death does not delete membership history. Explicit removal marks the historical record removed, and one Character may be active in at most one unit.

## D-049 Captain eligibility and fallback are deterministic (2026-10-08)

A captain must be an active roster member whose canonical Character is alive, adult and combat-capable through U18. Assignment rejects missing, dead, child, incapacitated and non-member candidates. If a captain becomes invalid, the existing eligible member with greatest Influence wins; stable-ID ordinal breaks ties. If none exists, the unit is Leaderless. U18/U17 remain the death and monarch-succession authorities, and fallback creates no Character.

## D-050 Unit representation composes existing tiers and U20 (2026-10-08)

Detailed/Aggregate is unit representation, not Character tier identity. `MilitaryUnitRepresentationService` performs rollback-safe transitions through the one `TierManager`: living non-incapacitated members use Tier1 for Detailed and the requested existing Tier2/Tier3 adapter for Aggregate. Active U20 participants block aggregation. `MilitaryUnitCombatService` only supplies U20-eligible members and later derives summary/status from U20 plus canonical Character/U18 state; it does not resolve attacks or aggregate outcomes. U23 owns all abstract combat.

## D-051 Version 8 military persistence and disposable controls (2026-10-08)

Snapshot v8 stores stable unit/side/captain IDs, sorted roster history, status, representation, abstract location, order, morale/cohesion summary, active U20 encounter link and revision markers. Restore validates Characters, unique active membership, captain eligibility and encounter cross-references after combat restore. Versions 1-7 remain readable. PostgreSQL continues opaque XML storage and requires no relational migration.

The LocalGameplay unit overlay is development-only. It issues commands to Simulation, reuses U11 Move and the existing pointer blocker, and owns no persistent state. Final army UI remains U27.

## D-052 Stable-ID player control and exclusive movement ownership (2026-10-08)

`PlayerControlService` is the engine-independent authority for the temporary RTS/ThirdPerson ownership mode. It stores only the existing Character `StableEntityId`, admits the current monarch, first eligible heir or player-unit captain, pins that ID in Tier1 and never creates a person, presenter or copied gameplay state. Direct control composes with the existing combat transition guard and external-activity owner rather than replacing them.

The existing `NavMeshMovementDriver` has an explicit `Autonomous`/`DirectPlayer` authority. Direct movement uses the same `NavMeshAgent.Move`; there is no second CharacterController or Transform writer. On entry the current Utility action is cancelled through its existing contract. A U11 manual Move becomes `Assigned` again, while assignment/group metadata is retained. U20 continues logical combat but its movement requests are gated while player control owns locomotion. On exit the scheduler may resume valid work through normal selection.

## D-053 Reusable TPS presentation and mode-scoped input (2026-10-08)

One scene-level TPS Cinemachine rig and target are created once and rebound to the existing Character presenter. Camera priorities switch while the RTS controller and transform are retained for exact in-session return. Camera state is disposable and not a gameplay/save authority.

The Unity Input System owns all bindings. Camera/Pointer/Building maps are disabled in TPS, the ThirdPerson map is enabled, and global Time plus Mode maps remain active. T toggles, Escape exits, WASD moves camera-relative, pointer delta looks and Shift runs. Cursor locking is presentation-only and skipped in batch mode. U22 deliberately adds no second damage or attack input path.

## D-054 Version 9 player-control persistence and safe fallback (2026-10-08)

Snapshot v9 adds only `PlayerControlSaveState`: mode and optional controlled Character stable ID. Load re-enters through normal eligibility/materialization/arbitration. A missing, dead, incapacitated, hostile or otherwise invalid identity falls back to RTS; no GameObject or camera reference is restored. Versions 1–8 remain readable. PostgreSQL continues storing the validated XML opaquely and requires no relational migration or realtime role.

## D-055 U20 encounter state is the canonical cross-mode battle (2026-10-08)

U23 extends `CombatEncounterState` instead of introducing a second aggregate-combat authority. The existing stable encounter ID is also the `BattleId`; one record owns mode, U21 side-unit links, participants, targets, cooldowns, attack sequences, morale, retreat, attacks, outcome and compact battle events. Physical mode continues through U20 movement and position boundaries. Abstract mode changes only how the same state advances. A mode transition therefore cannot reroll health, replace a Character or reset combat progress.

## D-056 Abstract combat uses shared rules and a fixed one-minute step (2026-10-08)

`CombatRules` contains the impact, injury and morale/retreat constants used by both modes. Abstract battles advance from their last committed tick to the target `GameClock` tick in exact one-game-minute steps, respect persisted weapon intervals and choose targets from a stable hostile ordering while retaining valid assigned/current targets. No render delta, Transform, NavMesh or `UnityEngine.Random` participates. Direct catch-up, step-by-step advancement and reversed registration order must produce the same state hash.

Physical/abstract convergence is a distribution gate rather than an identity-equality claim. The accepted U23 evidence is 100 paired starts with 0.0% winner delta, mean casualty delta 1 and mean duration delta 3 minutes. Exact abstract state and transfers are guaranteed; future physical Transform/NavMesh paths and the identity of future casualties across alternate modes are not.

## D-057 Battle transitions compose U21/U22 and snapshot v10 (2026-10-08)

`BattleResolutionService` changes representations through U21's rollback-safe service. Physical units are Detailed and active fighters are Tier1-pinned; abstract units are Aggregate and use their existing Tier2/Tier3 canonical projections. A directly controlled U22 participant blocks Physical→Abstract. Returning to Physical rematerializes the same living, capable Characters from retained U13 runtime state; dead members remain absent and incapacitated members remain canonical but unmaterialized. Exact logical position state is retained, while exact future centimetre-level path continuity is not promised.

Snapshot v10 adds mode, optional unit IDs and compact started/mode-changed/side-broken/captain-death/finished events to the existing combat payload. Attack history remains the detailed casualty evidence. Versions 1–9 default to Physical/no unit links when those fields are absent. PostgreSQL keeps the opaque XML design and requires no schema or realtime-query change.

## D-058 Canonical structural state and retained ruins (2026-10-08)

U24 extends the existing `Building` rather than introducing destroyed, captured or combat copies. The canonical record owns data-driven max/current integrity, structural status, `CombatSideId` owner, fire/checkpoint state, processed damage IDs and compact history. One engine-independent `BuildingDamageService` applies Generic, Combat and Fire damage using integer definition multipliers. Stable event IDs make repeated delivery idempotent; integrity is bounded and destruction commits once.

A destroyed building remains in `BuildingRegistry` and keeps its grid occupancy as ruins. This prevents free overlap and preserves a stable future repair/reconstruction target without implementing repair. Destruction cancels uncommitted site work and makes production/crop progression non-operational. Storage contents move atomically from the building inventory into deterministic WorldPiles at its position; an explicit spill marker makes retries and load continuation non-duplicating.

## D-059 Central fixed-step fire and battle-validated in-place capture (2026-10-08)

Fire is a building state, never a ParticleSystem. The centralized service processes active fires in stable-ID order at a fixed one-game-hour step from an absolute persisted checkpoint. Fixed-point settings own base damage/decay plus U19 rain, snow and heat modifiers. Stable building/tick-derived damage IDs make direct catch-up, incremental advancement, collection reorder and save/load continuation converge without a new RNG stream. U24 deliberately omits spatial propagation, firefighting and smoke/evacuation systems.

Capture mutates owner on the same intact, definition-capturable building only after a supplied canonical U20/U23 battle finishes with the hostile new side victorious. Identity, definition, position, integrity, fire and storage remain unchanged. Active production is cancelled for deterministic ownership revalidation, while the intact captured site may later operate even if its new owner lacks the technology required to place another one. Physical and abstract combat call the same building-damage API; no siege or territory authority is introduced.

## D-060 Building snapshot v11 and disposable U24 presentation (2026-10-08)

Snapshot v11 appends a sorted detached `BuildingWorldSaveState` with structural, owner, fire/checkpoint, processed-event, destruction/spill and history fields. Inventories/WorldPiles and production tasks remain in their existing snapshots. Restore validates the graph before replacement and does not replay destruction, spill or fire intervals. Versions 1–10 remain readable; PostgreSQL continues opaque XML storage and needs no relational migration.

The development overlay and presenter are projections only. The click-blocked panel submits service commands; material-property tint and a lightweight burning ParticleSystem read canonical state. They own no damage, capture, storage or production truth and are not U27 final UI.

## D-061 Stable-ID world graph and canonical settlement references (2026-10-09)

U25 adds an engine-independent validated graph of `WorldLocationId`, routes and `SettlementId`. Settlements reference existing Character and Building stable IDs, U21 units retain their canonical identity/location/order, and U20/U23 encounter IDs remain battles. Local and global presentation never create copies of those authorities. The initial six-node/two-settlement graph is fixed authored data, not procedural generation or a territory system.

## D-062 Absolute-tick travel and rollback-safe representation transitions (2026-10-09)

Global travel is a persisted logical order resolved by absolute canonical game ticks in arrival-tick/unit-ID order. Exactly-once arrival updates the existing U21 abstract location; deterministic hostile co-location starts the existing U23 Abstract battle. Local↔Global mode changes commit mode last and compensate U22 control, U23 battle mode, U21 representation and U13 tier changes on failure. No per-army or per-character global Update exists.

## D-063 Snapshot v12 and additive disposable map presentation (2026-10-09)

Snapshot v12 appends validated graph, settlement, mode and in-progress travel state; v1-v11 remain readable and PostgreSQL needs no relational migration. `GlobalMap.unity` is an additive presentation scene. `LocalGameplay` and its composition root stay loaded off-camera so the existing clock and simulation services remain authoritative. Schematic markers, map camera and controls are disposable U25 development UX; U27 owns final interface design.

## D-064 U30 requires an actual external eight-hour soak (2026-10-09)

U30 is not complete when only a short soak passes or an eight-hour runner exists. The mandatory gate is one real uninterrupted or checkpoint-resumed run whose measured wall-clock duration is at least eight hours and whose structured result reports no critical exception, invariant failure, watchdog failure or runaway-memory signal. The task implementation first prepares and commits the harness as IN_PROGRESS. The night orchestrator then runs PowerShell/Unity without an active Codex session, validates the structured result, and invokes a fresh Codex finalizer to update documentation and create the U30 DONE commit. This avoids spending Codex usage on idle polling while retaining auditable evidence.

## D-065 Pre-U31 FRS compliance patch (2026-10-09)

U31 remains an integration and release gate rather than a feature-development task. Confirmed FRS-to-roadmap gaps are assigned to U30A after the U30 reliability gate: a canonical new-game bootstrap with ten starting player characters and a monarch; normal player save/load access; deterministic AI discovery; AI construction over existing building/resource authorities; minimal Stone Age military supply over canonical inventories; and TPS aiming/attack through the existing U20 combat, weapon, injury and cooldown rules. U30A must also preserve continued sandbox play after military victory. U26, U27 and U28 still own their natural prerequisites—AI state scoping/construction, save/load and work UI, and event chains/cooldowns—so U30A verifies and closes only remaining cross-system gaps.

## D-066 Detect Codex CLI automation flags before task execution (2026-10-09)

The night orchestrator must not hard-code one historical approval flag. On Windows it prefers the npm `codex.cmd` shim to avoid PowerShell Execution Policy blocking `codex.ps1`, reads the installed `codex exec --help`, selects standalone `--approve-for-me` when supported and retains `--sandbox workspace-write --ask-for-approval never` only as a legacy fallback. In CLI 0.160.0, `--approve-for-me` already includes the workspace-write preset and conflicts with a separate `--sandbox`. A no-agent preflight therefore exercises the complete real argument combination in a captured child process until a deliberately missing output-schema file stops execution locally; captured stderr avoids PowerShell `NativeCommandError` under stop-on-error semantics. Real agents similarly capture their native exit code with only the invocation scope set to non-terminating stderr handling, then restore global fail-fast behavior. `--help` alone is insufficient because it can bypass argument-conflict validation. Future CLI drift must fail before any implementation, commit or long soak begins.

## D-067 Strategic AI composes owner-scoped canonical authorities (2026-10-09)

An AI state is a stable-reference orchestration record, not a second game model. Player and AI share canonical Characters, Buildings, military units, settlements and battles. Each state owns a separate instance of the existing research, inventory, production and world-pile authorities. `AiStrategicScheduler` handles multiple states in stable ID order on absolute decision ticks and submits every action through U09/U15/U16/U21/U23/U24/U25 services. There is no `AiResearchService`, generated resource grant, teleported army, forced battle result or per-NPC strategic `Update`.

## D-068 Atomic settlement capture and common defeat rule (2026-10-09)

The capture transaction first validates the finished hostile U23 winner and every U24 building in the settlement. Only after all buildings accept capture are their owners changed; U25 settlement ownership is synchronized last. Any rejection leaves all building and settlement owners unchanged. The MVP defeat rule is identical for every capture caller: a side is defeated when no canonical U25 settlement has that `CombatSideId`. Final victory presentation and continued-sandbox policy remain U27/U30A/U31 work.

## D-069 Multi-dynasty health routing and remote Tier3 bootstrap (2026-10-09)

One health service may bind one succession authority per dynasty ID and routes fatal events by the canonical Character dynasty. This generalizes U17 without copying health or Character state and keeps player/AI histories independent. The normal rival's residents are deterministic seed-derived Characters materialized in Tier3 at a remote logical position; explicit U25 location entry, not active-scene camera distance, is the path back to Tier1.

## D-070 Snapshot v13 and deterministic strategic identities (2026-10-09)

Snapshot v13 stores each AI faction's strategic checkpoint, targets, orders, bounded history, sequence counters, defeat state, dynasty, research, production/inventories and world piles. Existing sections still own travel, units, battles and buildings. Restore rejects identity, ownership and target mismatches before applying faction state. AI-created building and unit identities are derived from world seed, state ID, purpose and decision sequence; direct catch-up and stepped execution therefore converge without replaying a decision. Versions 1–12 remain readable and PostgreSQL remains opaque XML.
