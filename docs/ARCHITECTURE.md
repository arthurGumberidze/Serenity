# Architecture — U01

## Scope and source
Implemented in U01: five runtime assembly boundaries and Game.Tests (EditMode), dependency checks, and this architecture contract. Runtime files contain assembly metadata only; services below are ownership contracts for later tasks, not implemented gameplay. FRS_Unity.docx sections 4, 33, 39–40 and 42.2–42.7 guide the design. D-008 overrides the original Editor target with Unity 6000.6.4f1.

## Compile-time dependencies
An arrow means a direct assembly reference. All runtime assemblies live under Assets/Game/<layer> and use the matching Game.<layer> namespace.

```mermaid
flowchart TD
  Infrastructure[Game.Infrastructure] --> Presentation[Game.Presentation]
  Infrastructure --> ECS[Game.ECS]
  Infrastructure --> Simulation[Game.Simulation]
  Infrastructure --> Domain[Game.Domain]
  Presentation --> Simulation
  Presentation --> Domain
  ECS --> Simulation
  ECS --> Domain
  Simulation --> Domain
  Tests[Game.Tests - Editor only] --> Infrastructure
  Tests --> Presentation
  Tests --> ECS
  Tests --> Simulation
  Tests --> Domain
```

| Assembly | Direct project references | Ownership |
|---|---|---|
| Game.Domain | None | Stable IDs, canonical serializable world/person/cohort models, invariants, pure rules and definition values (U04 onward). |
| Game.Simulation | Domain | Clock/calendar (U03), centralized tick scheduling, use cases, commands/queries, RNG streams, tier transitions and aggregate simulation. Defines ports consumed by orchestration. |
| Game.ECS | Domain, Simulation | Tier2 components/jobs/systems and adapter implementing the Simulation tier execution port. Maps stable IDs to transient Entity handles. DOTS implementation starts U12. |
| Game.Presentation | Domain, Simulation | Tier1 views, cameras/input, UI, animation/audio/VFX; converts input into commands and renders snapshots/events. No ownership of persistent state. |
| Game.Infrastructure | Domain, Simulation, ECS, Presentation | Application composition root, save codecs/storage/migrations, configuration and content loading, scene loading and adapter wiring. |
| Game.Tests | All five runtime assemblies | Editor-only architecture checks now; pure unit and adapter tests as features arrive. |

Domain and Simulation set noEngineReferences=true; both are pure C# with no Unity types in their public contracts. Other runtime layers permit Unity API. All five set autoReferenced=false, so predefined assemblies cannot silently couple to them. Explicit assembly references are required for future clients. Unsafe code is disabled. No runtime assembly references tests or UnityEditor.

Infrastructure is deliberately the outermost composition layer: it may construct views and ECS adapters, while neither references Infrastructure. Persistence/configuration ports belong to Simulation, using Domain values. Infrastructure implements those ports and injects them when starting a session. This avoids a sixth runtime bootstrap assembly or dependency cycles. Editor authoring extensions will need a separate Editor-only assembly when introduced.

U02 installs and pins the FRS package profile (see PACKAGES.md). Game.ECS remains a compiled boundary with metadata only; U12 will add required explicit ECS package references. No runtime dependency edge changes in U02. Changing any edge requires updating this document and its architecture checks.

## State ownership and execution flow
The session owns the canonical Domain state across scene loads. Simulation controls writes and tick order; Presentation receives read models and submits commands, never modifies state through views. Definition assets are authored as ScriptableObjects in Infrastructure and converted into validated immutable Domain values at session creation. They are not mutable save-state.

Planned flow: input -> Simulation command queue -> validate Domain rules -> scheduled simulation step -> commit state -> publish read model/events -> views. A single session driver advances scheduled systems; mass NPCs do not each receive an Update loop. Pause halts advancement while commands remain queueable.

U03 implements the central time foundation. `GameTimeState` is canonical Domain state with separate integer calendar and biological ticks plus selected speed and pause. `GameClock` is a pure Simulation service that accepts an explicit real-time delta; it never reads Unity wall/frame time or changes `Time.timeScale`. This same API can be driven once per session by Tier 1 presentation, by batched Tier 2/DOTS scheduling, or by Tier 3/offline aggregate simulation. Calendar conversion uses a fixed 365-day year for current rules. Biological acceleration is independently configurable and defaults to 400x calendar time; character life stages and pregnancy timers remain future domain features.

Tier2 ECS data is a working projection during a scheduled step, not a second independently writable source of truth. Simulation supplies inputs; the ECS adapter completes jobs and returns validated deltas at a synchronization barrier. Only after committing those deltas can saves or tier transitions run. StableEntityId-to-Entity and StableEntityId-to-view maps are transient and rebuilt. Unity instance IDs and Entity indices never appear in persistent references.

Tier1 views may be pooled or destroyed without deleting their Domain records. Tier3 stores cohorts and aggregate state without per-person GameObjects or mandatory ECS entities. Persistent named characters keep individual records and IDs across all tiers. Transition orchestration flushes pending deltas, captures state, releases the old projection, and constructs the new one. Counts/resources must not be represented twice. Failed reconstruction must leave canonical state recoverable. These behaviors require integration tests in U13.

Off-camera snapshots preserve composition, health, supply/inventory, morale, orders, production, timers and RNG state. Outcomes advance from recorded seed/state and stable tick order; camera changes request representation changes and never reroll outcomes (U14).

## Persistence boundary
Simulation coordinates a consistent checkpoint after all pending commands/jobs for that tick are committed. Infrastructure serializes explicit versioned SaveData DTOs representing Domain state plus clocks, RNG and relevant scheduler/timer state. Scene objects, ScriptableObject instances and ECS handles are excluded; content references use stable definition keys. Loading validates schema/content, migrates supported versions and constructs a new session before attaching projections. Serialization format, ID encoding and migration implementation belong to U04; U01 does not choose them prematurely.

## Scene and lifetime strategy
The existing SampleScene remains the sole build scene in U01. No gameplay bootstrap is installed yet. The planned Infrastructure composition root will own one persistent session and the Simulation driver. A small bootstrap scene will create that root once; Local and Global content scenes will load/unload additively around it (U05/U25). Views register/unregister on scene lifetime and never own the session. Before unloading a scene, complete the simulation barrier and commit projections; then dispose subscriptions/jobs and release scene views. Returning binds views to retained state. A new game/load explicitly replaces the session, avoiding duplicate roots and stale static state. Third-person and RTS input will share Simulation commands and combat rules.

## Testing and validation
U01 Game.Tests uses the Unity compilation graph to verify that every runtime boundary participates in Player compilation, its DLL exists, exact approved project references are used, pure layers have no UnityEngine or UnityEditor dependencies, and Game.Tests is excluded from the Player graph. It also checks project asmdefs for cycles and explicit references. Existing U00 environment tests remain in their own Editor-only assembly and continue checking Editor, URP and scenes.

Pure rule and U03 time tests live in Game.Tests; save/load tests assert state equality including IDs, clocks and RNG. Introduce Game.Tests.PlayMode as a separate test assembly when scene/ECS integration exists; it must remain excluded from normal player builds. Tier transitions, pooling/lifetime and local/global changes need integration coverage. Performance gates and long soak tests belong to U29/U30, not U01.

Run Tools/Verify-U01.ps1 for all current EditMode tests and a Windows x64 Mono Development build using the existing U00Build entry point. This validates assembly boundaries, not gameplay, rendering quality or DOTS functionality. Exact results are recorded in docs/U01_HANDOFF.md.

## U04 implemented persistence
`Game.Domain.StableEntityId` provides immutable GUID identity and canonical N-format encoding; domain owners assign it once and future projections retain that ID. `SaveSnapshot` is immutable data-only version 1: SessionId, CalendarTicks, BiologicalTicks, SpeedMultiplier, IsPaused and BiologicalMultiplier. It creates detached U03 `GameTimeState` instances; no Character Domain or tier implementation is introduced.

`Game.Simulation.ISaveStore` saves/loads snapshots by a separate slot ID. `SaveCoordinator` captures time and settings at a caller-established simulation barrier and validates them through U03 GameClock. Load validates and constructs a replacement clock/settings before replacing its active Clock and SessionId. Consumers must rebind after a successful load; old clock references are detached. Calls are synchronous and owner-thread-only, with no jobs running or ticking concurrently. No Unity APIs or extra assembly edges were introduced.

`Game.Infrastructure.SaveXmlCodec` writes strict version-1 XML using framework XML APIs. Required fields, scalar types, stable IDs, bounds and supported speeds are validated; DTDs are prohibited and version-1 documents are limited to 64 KiB. Unknown versions are rejected explicitly, not migrated. `FileSaveStore` owns codec/storage, validates before writing, flushes a unique same-directory temporary file and atomically replaces/renames it. Failed operations leave the last committed slot available; temporary cleanup is best effort. Missing files and I/O failures remain explicit .NET exceptions. This local adapter exercises the provider boundary that U04A can implement independently.

Run Tools/Verify-U04.ps1 for the full EditMode suite and Windows Development build. See U04_HANDOFF.md for results and limitations.

## U07 Tier 1 character presentation
The Tier 1 flow is `Character -> CharacterPresentationCatalog -> CharacterPresentationSpawner -> CharacterPresenter`. The first object remains canonical Domain state; the latter three belong to Presentation and contain only replaceable configuration or ephemeral runtime bindings. The catalog currently maps biological sex deterministically to the U05A male/female wrappers. It is the replacement seam for future appearance descriptors and final Stone Age art, so prefab, Animator, renderer, Transform and sockets never enter Domain or persistence.

`CharacterPresentationRegistry` is an ordinary session-owned `StableEntityId -> CharacterPresenter` map. It rejects duplicate active views, unregisters only the matching view, and is distinct from the canonical `CharacterRegistry`. `Bind`/`Unbind`, destruction and respawn retain the same aggregate and stable ID. The spawner accepts an existing `Character` plus a presentation transform and never creates a domain person.

The wrappers contain the root presenter and capsule collider, a nested source visual with valid Humanoid Animator/Avatar, and left/right hand sockets parented to humanoid bones. The project-owned controller defaults to Idle and provides `Speed`/`Moving` foundations for idle/walk/run. Presentation refresh is explicit; there is no per-presenter `Update`, GameClock dependency, simulation movement, navigation or AI. Presentation pause sets Animator speed locally and does not use `Time.timeScale`.

`LocalSceneCompositionRoot` constructs a demo `CharacterRegistry`, presentation registry and spawner, then creates one male and one female through `Character.CreateNew`. This is development composition, not world population or persistence. `SelectionProbe` may retain a selected presenter/ID as disposable control state; raycast selection never owns or deletes the aggregate. Assembly arrows remain unchanged: Presentation already depends on Domain, and Domain remains engine-free.

## U04A PostgreSQL persistence
`Game.Infrastructure.Persistence.Postgres` implements the unchanged synchronous `ISaveStore` port. `PostgresSaveStore` accepts and returns only `StableEntityId` and `SaveSnapshot`; it has no scene, GameObject, ECS, presentation or clock references. Domain and Simulation do not reference Npgsql. Runtime state remains authoritative in RAM, and the caller invokes storage only after the same committed simulation barrier required by U04.

Npgsql 8.0.9 is explicitly referenced by Infrastructure and Editor tests. A process-environment configuration object accepts only loopback endpoints and constructs the connection internally. It never exposes the connection string. Calls open and close their own unpooled connection, use finite timeouts and wrap provider failures in a redacted operation/SQLSTATE exception. Missing slots and corrupt/unsupported content retain U04 exception semantics.

Database schema `serenity` is advanced by repository SQL scripts in one locked transaction. `schema_migrations` records the ordered version, filename, normalized-SQL SHA256 and application timestamp. Existing entries must match exactly. Initial migration 001 creates `save_sessions`: slot/session UUIDs, save-format version, binary XML payload and persistence timestamps. Slot UPSERT is one atomic parameterized statement. Load checks relational metadata against the decoded XML before exposing the detached snapshot. Database migration versions and snapshot format versions are deliberately separate.

The current relational schema is intentionally narrow. Future normalized history repositories receive their own Simulation ports and migrations; they must not turn PostgreSQL into a realtime simulation engine. Character/Dynasty/City/HistoricalEvent identity uses the same StableEntityId-to-UUID mapping. `GameClock` replacement/rebinding remains a composition responsibility after successful `SaveCoordinator.Load`.

## U05 local scene and control flow
`LocalGameplay` is the first interactive content scene and the enabled build scene. Its project-owned placeholder ground and markers deliberately avoid all staged/vendor content. `LocalSceneCompositionRoot` belongs to Infrastructure and holds explicit serialized references to the Presentation input source, camera controller, pointer raycaster and selection probe. Components do not discover each other through `FindObjectOfType`, a static registry or a service locator.

The U05 flow is `InputActionAsset -> LocalGameplayInputSource -> navigation/interaction intent -> RtsCameraController or SelectionProbe`. Raw physical bindings remain in the Input System asset. The camera controller consumes only the input interface and serialized settings; Cinemachine is used at the camera presentation edge rather than as the input/gameplay owner. Presentation adds direct package references to `Unity.InputSystem` and `Unity.Cinemachine`; project-layer arrows remain unchanged.

The RTS rig owns horizontal target position and yaw. It combines keyboard, normalized screen-edge and middle-drag movement, applies configurable rectangular bounds, and moves the fixed-pitch Cinemachine mount within min/max zoom. All navigation uses unscaled frame time and is independent of Simulation `GameClock`, `Time.timeScale`, persistence and stable identity. The controller is one ordinary presentation `Update`, not an NPC-scale update pattern.

`WorldPointerRaycaster` translates screen coordinates into physics hits/points through an explicitly assigned output Camera and layer mask. It contains the UI-block boundary through EventSystem without requiring UI infrastructure in U05. `SelectionProbe` listens to primary-click intent and highlights `SelectableMarker`; it neither creates a canonical selected-character model nor submits future orders. U20/U22 may reuse the pointer/input boundaries but must share Simulation commands and combat rules rather than place those rules in callbacks.

## U05A asset source and runtime boundary
The asset flow is `ignored external staging -> explicitly selected third-party source -> Serenity-owned runtime wrapper -> future presentation component`. Each arrow is deliberate and traceable in `docs/ASSET_REGISTRY.md`. Staging is never a Unity asset root. Unknown-license files remain quarantined, and already imported vendor folders are neither relocated nor automatically approved.

Third-party source lives either in its existing vendor path or, for selected staging files, under `Assets/Game/Art/ThirdParty/<publisher>/<package>`. Gameplay-facing prefabs and Serenity URP material variants live under `Assets/Game/Art`. Wrappers own normalized placement roots, simple readiness colliders and project-side animation controllers where needed. They do not own canonical character/animal/building state. Future Presentation code binds Domain read models to wrappers; Domain and Simulation never reference meshes, prefabs, Animators, scenes or vendor GUIDs.

`U05AAssetFoundation` is an Editor-only, manually invoked allow-list. It configures only seven registered FBX imports, builds deterministic project-owned wrappers and regenerates the development gallery. There is no catch-all import hook. Existing Hodaart import settings stay vendor-owned; their wrappers apply a measured 1.8 m presentation scale. Staging-derived scale belongs to the selected ModelImporter. Root wrappers resolve ground and attachment pivots without rewriting mesh data.

`StoneAgeAssetGallery` validates source compatibility and is absent from Build Settings. `LocalGameplay` remains the sole player scene and has no dependency on the gallery or vendor content. Addressables were not introduced because U05A has no runtime loading requirement. The asset tests gate prefab loads, renderers/materials, missing scripts, Avatar/animation readiness, importer scope, registry coverage and build-scene isolation; visual style judgment remains a recorded manual GPU-rendered inspection.

## U06 character domain
`Game.Domain.Characters.Character` is the canonical persistent-person aggregate. It is a sealed pure C# type in the existing no-engine `Game.Domain` assembly; it has no `MonoBehaviour`, `GameObject`, prefab, Animator, ECS Entity, ScriptableObject, Npgsql or SQL reference. `CreateNew` is the only path that generates a new `StableEntityId`; `Restore(CharacterState)` validates and retains an existing one. All mutable operations leave identity unchanged.

`CharacterState` is a detached immutable snapshot boundary. It contains identity, validated name and biological sex, biological birth/lifecycle ticks, parent links, current spouse, optional family/dynasty/profession references, traits, skills, inherited-foundation attributes, six-part health, Wealth, Influence and relationship scores. Capture orders ID/key collections deterministically. It is ready for an explicit future save DTO/codec, but U06 does not silently change the U04 `SaveSnapshot` version-1 wire format or PostgreSQL schema.

Age is derived from one canonical biological birth tick and the U03 biological timeline using fixed 365-day years. It never reads calendar ticks, Unity time, wall time or a private clock. Death records a biological tick and freezes age; mortality rules and aging scheduling remain future simulation work.

Parent links belong to the child and reference `StableEntityId`. Role and kind distinguish mother/father and biological/legal/adoptive parentage, including hidden biological parentage. Children are derived by `CharacterRegistry`, avoiding an independently mutable reverse list. The registry owns active-world uniqueness and symmetric current-spouse linking without becoming a global singleton or persistence repository. Missing/dead/off-tier relatives remain representable by stable IDs even when absent from the active registry.

Trait, skill and profession keys are compact validated definition IDs for later data-driven authoring conversion. U06 stores no ScriptableObject references. Health is only the FR-CHAR-005 body-part foundation; disease, wounds and medicine are deferred. Wealth/Influence store the documented 0–1000 personal values but U32 owns their formulas/effects. Needs, AI, pregnancy, genetics, succession, detailed relationships, inventory and presentation are intentionally outside U06.

`CharacterDomainTests` covers creation/restoration and immutable identity, name/sex/state validation, biological age boundaries and death, parent/spouse invariants, hidden versus legal father, deterministic state capture, trait/skill/relationship bounds and duplicates, body/attribute/social values, optional profession/family/dynasty hooks, registry uniqueness and derived children. Existing architecture tests continue to prove that Domain has no UnityEngine or UnityEditor reference.

## U08 building placement and presentation
The canonical flow is `BuildingDefinition -> BuildingPlacementService -> Building -> BuildingPresentationSpawner -> BuildingPresenter`. Definitions and placed aggregates are engine-free. A definition uses a validated content key; a placed aggregate uses its own `StableEntityId`. `BuildingState` captures coordinate, discrete orientation and planned/completed state without Unity types and can be restored with the same ID. This state is ready for a later world-save DTO, but U08 deliberately does not silently alter the U04 version-1 time snapshot or PostgreSQL migration.

`BuildingPlacementService` evaluates rectangular rotated footprints against `BuildingGridBounds` and `BuildingOccupancyGrid`. Occupancy is the deterministic authority; colliders are only presentation/raycast aids. Confirm evaluates again, generates a new identity, reserves the footprint and adds the aggregate to the session-owned `BuildingRegistry`. Duplicate stable IDs and occupied cells are rejected. Current coordinate data includes an explicit level so later floors/basements are not precluded, while U08 bounds permit only level zero.

`BuildingPresentationCatalog` is a ScriptableObject authoring adapter for the Primitive Shelter and Storage Basket placeholders. It produces pure definitions and maps definition IDs to Serenity-owned prefabs. `BuildingPresentationRegistry` maps stable building IDs to disposable presenters separately from canonical buildings. Spawner restore/respawn accepts an existing aggregate; it never creates Domain state. Preview clones are unbound, collider-disabled and tinted through MaterialPropertyBlock, so they have no ID, occupancy or save presence.

`BuildingPlacementController` is one scene-level Presentation update, not a per-building loop. It reuses `WorldPointerRaycaster`, including its UI-block boundary, and accepts only `BuildableGround` hits. One-metre grid size and origin are serialized configuration. B toggles placement, R rotates, left click confirms and right click/Escape cancels; the U05 Q/E camera binding is unchanged. `LocalInteractionMode` suppresses selection clicks during placement but never disables the unscaled RTS camera.

U08 confirm creates Planned state and immediately calls the explicit completion transition because resources and worker AI do not exist yet. U09 will supply real costs/inventories and U10/U11 can schedule workers without changing identity, occupancy or presentation ownership. Walls, doors, windows, rooms, roofs, multi-floor construction, damage/fire, free placement and edge/socket snapping remain deferred.

## U09 resources, inventories and storage
The canonical resource flow is `ResourceCatalog -> ResourceInventoryRegistry -> ResourceInventory`, orchestrated by Simulation services. Resource definitions, IDs, integer quantities, owners, detached inventory states and world-pile coordinates are pure Domain data. `ResourceCatalogAsset` and building catalog cost entries are authoring adapters only. Presentation receives existing inventories and reads them; it never stores a second mutable quantity.

Every inventory is addressed by `InventoryOwner(kind, StableEntityId)`. Current kinds are WorldPile, Character and BuildingStorage. A `WorldResourcePile` owns stable identity, one resource ID, an integer grid coordinate and a reference to its canonical inventory owner. A building storage owner reuses the building's stable ID. Character inventories reuse character IDs. Capacity is total bulk units; an optional category allow-list is checked before mutation. Zero quantities are omitted from captured state.

`ResourceTransferService` performs all-or-nothing moves and treats source==destination as a validated no-op. `SettlementResourceView` derives totals from explicit building-storage inventories. `WorldPileService` and `StorageService` create the owner-specific capabilities without coupling them to GameObjects. Capture/restore copies quantities, capacity and filters and preserves owner IDs; world save DTO/version work remains a later explicit persistence migration.

`ConstructionFundingService` composes U08 placement with resources. It plans exact deductions across an ordered set of funding owners, confirms logical placement, attaches storage when configured, charges costs, creates presentation through a callback, and completes the aggregate. Its compensation path refunds every charge and releases storage, registry and occupancy. Affordability preview never reserves or consumes resources. This preserves the invariant that failed confirmation changes neither stock nor occupied cells.

LocalGameplay composition creates four finite `DEV_BOOTSTRAP_ONLY` physical pile projections and two character inventories. The Primitive Shelter and Storage Basket costs and basket capacity are data-driven `BALANCE_TBD` values in the existing building catalog. `ResourceDebugOverlay` is one development-only read model showing aggregate quantities; pile and building presenters expose canonical bindings only. No per-resource or per-inventory Update loop was introduced, and project assembly arrows remain unchanged.

## U10 centralized Tier 1 Utility AI
The U10 flow is `Tier1AiAgentState -> Tier1AiScheduler -> utility selection -> action execution -> ITier1MovementDriver/U09 services`. Agent state is pure Domain data keyed by the existing Character `StableEntityId`; it contains bounded Hunger/Energy, simulation position and observable action phase. The scheduler is a session-owned Simulation service with stable ordering, deterministic phase staggering and independent decision/need/action batch caps. It consumes U03 `GameTimeAdvance.CalendarDelta`; a zero delta makes pause a complete simulation no-op. `Tier1AiRuntimeDriver` is the only AI `MonoBehaviour.Update`, so adding Tier 1 agents does not add frame callbacks.

Utility selection is finite, normalized and deterministic. Idle is always available, Rest scores inverse Energy, and Haul combines Energy/Hunger with canonical work availability. Explicit priority and action-enum order break equal scores. A configurable switch margin retains valid actions, and carrying is non-interruptible. Execution objects own the short-lived state machine, while `Tier1AiAgentState` remains independent from Animator or NavMesh callbacks.

`HaulWorldQuery` reads U09 world-pile, inventory and building registries and selects nearest valid source and storage with stable-ID tie-breaks. `HaulClaimRegistry` reserves source quantity and destination capacity without changing ownership. Pickup and dropoff invoke only `ResourceTransferService`; cancellation and failure release claims, and resources already picked up remain in the character inventory. Task IDs are typed session-local counters, distinct from persistent entity IDs. U09 immediate construction funding remains unchanged.

Presentation implements `ITier1MovementDriver` through AI Navigation 2.0.12 `NavMeshAgent`. LocalGameplay constructs one runtime `NavMeshSurface` over the project-owned ground, registers movement bridges for the two U07 demo presenters and drives the existing `Speed`/`Moving` Animator parameters with root motion disabled. Presenter loss unregisters active movement and claims without deleting the Character aggregate. Dynamic NavMesh obstacles/rebakes, saved in-flight tasks, work groups, DOTS execution and production are intentionally deferred.

The U10 development inspection path is `WorldPointerRaycaster -> SelectionProbe -> bound presenter -> canonical registry/object -> ephemeral debug snapshot`. Pile, building-storage and character panels never own quantities or job state. The left resource overlay derives live per-location and conserved totals from every registered inventory and the data-driven resource catalog. These are development-only `OnGUI` read views; they add no production UI, simulation command or per-entity Update.

## U11 work groups and jobs
The U11 command flow is `SelectionProbe/work overlay -> WorkManager -> WorkOrder -> Tier1AiScheduler -> action execution -> movement/U09 transfers`. `WorkManager` is an ordinary session-owned Simulation service. `WorkGroupRegistry` and `WorkOrder` are engine-free Domain state; neither knows about presenters, NavMesh, input callbacks or `GameObject`. Group membership, commanders and targets reference existing stable IDs, while `WorkGroupId` and `JobId` prevent group, entity and order identities from being conflated.

A group is an ordered set of unique character IDs with an optional member commander. The registry enforces settlement-wide single-group membership. Group Move/Haul submissions expand in stable member order into individual work orders, skip ineligible or occupied workers and reserve exclusive targets where required. `IWorkEligibilityPolicy` is the extension seam for later profession, health, tier and jurisdiction constraints; its U11 implementation only verifies that the canonical character exists and is alive.

The job model has an explicit priority and lifecycle. Queued jobs may be assigned deterministically to eligible agents; assigned work becomes Active only when execution starts and ends in Completed, Cancelled or Failed. Cancellation/failure always releases the work target reservation and any U10 haul claim. Haul pickup/dropoff still uses the sole U09 `ResourceTransferService`, preserving physical logistics and inventory conservation. Orders and claims are session-ephemeral in U11, so the U04 save format and PostgreSQL migration remain unchanged.

`Tier1AiScheduler` remains the single Tier 1 scheduling authority. Its precedence is carrying, critical Energy rest, manual work, then autonomous utility. This permits player control without allowing direct input code to manipulate NavMesh agents or inventory. Both manual and autonomous hauling share `HaulActionExecution`; manual movement shares `ITier1MovementDriver`. One scene-level runtime driver advances all agents, and no work-group, job or character component adds an individual `Update`.

Presentation extends the existing U05 selection boundary with Shift additive/toggle selection and a disposable selected-ID list. The development-only work overlay issues commands, cycles priority and supports Ctrl+1…9 assignment plus 1…9 recall. `CharacterPresenter.IsSelected` is visual/ephemeral only. Group membership and job state remain valid when a presenter is destroyed and rebound to the same aggregate. Production selection and UI styling remain U27 work.

Development IMGUI is part of the U05 pointer boundary even though it is not represented by `EventSystem`. Each visible debug overlay registers its GUI-space blocking rectangle with `WorldPointerRaycaster`. A primary click is tested against those regions before physics or selection mutation; a blocked click is consumed, while a genuine unblocked world miss retains the existing clear-selection behavior. U11 work buttons dispatch from the same Input System primary-click event after that guard, so commands observe the current selection rather than a selection already cleared by click-through.

RTS manual movement extends the same boundary: `Pointer/SecondaryClick -> ManualMoveInputController -> WorldPointerRaycaster -> ManualMoveCommandService -> WorkManager -> Tier1AiScheduler -> ITier1MovementDriver`. The controller performs no physics query of its own beyond the one existing raycaster call and accepts only `BuildableGround`. The engine-free command service deterministically assigns centred formation slots and uses `ICharacterTierLookup` only to authorize current Tier 1 execution. Tier 2/Tier 3 targets return explicit unavailable entries; the command neither transitions tiers nor changes U14 continuity. The Presentation layer never calls `NavMeshAgent.SetDestination` directly.

## U12 Tier 2 DOTS bootstrap
The U12 flow is `Character/StableEntityId -> Tier2TransferState -> Tier2Materializer -> ECS components -> Tier2SimulationSystem -> Tier2TransferState extraction`. It lives entirely in `Game.ECS`, whose new package references are one-way to Entities, Burst, Collections and Mathematics. `Game.Domain` and `Game.Simulation` remain engine-free and expose only the existing character and `GameTimeAdvance` contracts.

`Tier2StableIdentity` stores the same canonical GUID identity as two unmanaged 64-bit values. `Tier2BiologicalState` projects only sex, birth tick, life state and optional death tick. `Tier2Position`, `Tier2Movement` and `Tier2SimulationProgress` hold the supported mutable simulation subset. The five components are unmanaged and total 92 bytes per entity before chunk/header overhead. They contain no strings, managed classes, Unity objects or GameObject references.

`Tier2Materializer` is the single U12 structural-change boundary. It prevalidates batch identities, rejects duplicates, creates one shared archetype in bulk and maintains a managed stable-ID-to-Entity index outside hot components. Extraction waits behind the runtime's completed-job barrier and returns the same ID plus every supported field. Entity handles are never persisted or exposed as identity.

`Tier2Runtime` owns a private explicit `World` and a single step singleton. Callers pass U03 `GameTimeAdvance`; the runtime writes its integer calendar/biological ticks, invokes `Tier2SimulationSystem` and completes tracked jobs before returning. The system schedules one parallel `[BurstCompile] IJobEntity` over the whole matching query. A zero/paused advance schedules zero entities and changes no state. No Tier 2 type derives from `MonoBehaviour`, and materializing 1,000 entities adds zero GameObjects.

Authority is field-scoped. The Domain character remains the durable authority for identity, biography, family, relationships, health and all state not represented in U12. During an active Tier 2 projection, ECS owns its projected position, movement and processed-step values until explicit extraction. U12 does not automatically dematerialize Tier 1, materialize Tier 2 from camera distance, write Tier 3 aggregates, update saves or register its world in Unity's default player loop. U13 owns those coordination and transition barriers.

## U13 tier manager and transition barriers
The U13 flow is `Character + active adapter -> CharacterRuntimeState capture -> target validation -> source release -> target materialization`. `TierManager` belongs to engine-free Simulation and knows only `ICharacterTierAdapter`; Presentation implements Tier 1, ECS implements Tier 2, and Simulation implements the pure-data Tier 3 adapter. Infrastructure constructs them for one session. This preserves the existing one-way assembly graph and avoids a singleton or an ECS/Unity dependency in Simulation.

`Character` stays alive and canonical across all tiers. Its durable biography, family, full health, traits, skills, profession and relationships are not duplicated in active representations. U09 inventory and U11 group/order registries continue to reference the same character ID. `CharacterRuntimeState` carries only mutable representation continuity: precise world position, lightweight movement/progress, a coarse location key and U10 needs needed for Tier 1 rematerialization.

Tier 1 capture reads the presenter position and centralized AI state. Dematerialization unregisters the AI/movement bridge before unbinding the presenter; this cancels the NavMesh-only execution, releases claims and requeues active manual work without removing its high-level assignment. Tier 1 materialization uses the existing deterministic sex catalog, so a round trip does not choose a new appearance. NavMesh paths are deliberately not transferred into Tier 2/Tier 3.

Tier 2 uses the U12 `Tier2TransferState` and materializer unchanged as its data path. `Tier2Runtime.Extract` and `Dematerialize` complete all tracked jobs before reading or destroying an entity. ECS remains authoritative only for projected position, movement and progress while active. Tier 3 uses one `Tier3CharacterRecord` per named person and no GameObject or ECS Entity; U14 will provide its deterministic advancement.

Transitions are transactional at the session coordination boundary. Target validation occurs while the source is still valid. After capture, the source is released and the target is committed; if target materialization throws, any partial target is removed and the source is rematerialized from the captured state. Tests cover forced failure rollback, every direct transition, same-tier no-ops and 100 repeated round trips with exactly one active representation.

`TierDistancePolicy` is an optional policy layer over the explicit transition API. It uses ordered hysteresis thresholds, deterministic stable-ID iteration, minimum residency evaluations, an evaluation budget and a transition budget. It may select direct Tier1↔Tier3 transitions but does not mutate simulation outcomes or advance off-camera state. `Tier1AiRuntimeDriver` remains the only scene AI `Update`; it publishes the already computed `GameTimeAdvance` so Infrastructure can step the private Tier 2 world and evaluate policy without advancing the clock twice.

## U14 deterministic off-camera simulation
The U14 flow is `explicit U03 time point -> OffCameraSimulationService -> stable-ID-ordered Tier3CharacterRecord batch -> committed CharacterRuntimeState -> U13 materialization barrier`. Core code remains in `Game.Simulation` with no UnityEngine, ECS, NavMesh, Animator, Npgsql or SQL dependency. `LocalSceneCompositionRoot` supplies the configured development world seed and current absolute U03 clock values; the existing single runtime driver offers a batch point before the distance policy evaluates transitions, while the Tier 3 adapter skips same-day frame calls in O(1) and traverses records at most once per fixed day.

One fixed step is one absolute calendar day. The record stores the exact last committed calendar and biological ticks, so sub-day remainder is implicit and snapshot-ready. Catch-up processes only crossed day boundaries; a one-year gap is 365 deterministic steps per character rather than a frame replay. Each step uses a counter-keyed sample from world seed, stable identity, absolute day index and an explicit stream ID. Stable-ID sorting is used for batch validation/hash output, while per-character samples are traversal-order independent.

U14 adds a deliberately generic `AbstractActivityProgress` plus deterministic accumulator as the first remote long-horizon stream. This is an audited deterministic seam for U15/U23/U28, not production, combat or events. It changes no canonical inventory, family, health, work-group or manual-order state. Tier 3 suspends Tier 1-only navigation/action execution and preserves Hunger/Energy; U15 may add remote production through canonical resource APIs, while U28 owns event effects and U18 owns disease/injury/mortality policy.

Promotion catches up and commits Tier 3 before Tier 1/Tier 2 materialization. Demotion validates the current absolute time and advances from the last carried checkpoint before the Tier 3 record becomes active. Tier 1 and Tier 2 keep U14 fields in the shared transition DTO, so a Tier3-only route and Tier3→Tier1/Tier2→Tier3 route converge under equal seed/time and no player intervention. Repeated targets are no-ops, backwards time is rejected, duplicate batch identities are rejected before commit and pause advances neither tick nor state.

`ComputeStateHash` hashes the configured seed and stable-ID-ordered U14 state only. It never includes Unity instance IDs, ECS entity indices, pointer values, timestamps or collection enumeration order. Constructor-level capture/restore plus continuous-versus-split tests provide the current save/restart boundary; U04 `SaveSnapshot` version 1 and PostgreSQL schema remain unchanged until an explicit durable world-save migration.
