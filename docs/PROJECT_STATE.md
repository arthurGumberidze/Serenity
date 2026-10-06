# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; AI Navigation 2.0.12; Windows x64 Mono Development.

## Current milestone / last completed task
U13 DONE — session-owned Tier manager coordinates one named character across Tier 1 GameObject presentation, Tier 2 ECS projection and Tier 3 lightweight data without replacing identity or canonical character state.

## Active task
U14 — deterministic off-camera simulation is next and has NOT started.

## U13 tier lifecycle foundation
- `CharacterSimulationTier` is the strongly typed Tier1/Tier2/Tier3 state. `TierManager` is a plain session-owned Simulation service with `GetTier`, explicit controlled transitions, safe same-tier no-ops, re-entrancy protection and a one-active-representation invariant.
- Transitions capture source runtime state, validate the target before release, dematerialize the source, materialize the target and restore the captured source on target failure. Tests force a target failure and verify the old Tier 1 representation remains usable.
- Durable identity, name, sex, birth/death, parents, spouse, family/dynasty, traits, skills, health, profession, Wealth/Influence and relationships remain owned by the existing `Character`. `CharacterRuntimeState` carries only representation continuity: position, lightweight movement/progress, coarse location key and current U10 Hunger/Energy values.
- Tier 1 uses the existing deterministic sex-to-prefab catalog, presenter registry/spawner, centralized AI scheduler and NavMesh adapter. Demotion unregisters Tier 1 execution, stops the NavMesh-only path, releases haul claims and requeues an active manual order while leaving the high-level assignment, canonical inventory and work-group membership intact.
- Tier 2 reuses `Tier2TransferState`, `Tier2Materializer` and `Tier2Runtime`. Extraction and dematerialization complete all tracked jobs first; no second ECS implementation or identity authority exists.
- Tier 3 is one `Tier3CharacterRecord` per named persistent character in a pure C# registry. It stores the same ID plus restoration/runtime continuity and no GameObject/ECS Entity or copied biography/inventory. U14, not U13, will advance remote state.
- `TierDistancePolicy` provides configurable hysteresis, minimum residency evaluations, deterministic stable-ID order, bounded evaluation and transition budgets, direct Tier1↔Tier3 transitions and controlled retry after transient external presentation changes.
- `LocalSceneCompositionRoot` wires the three adapters and advances Tier 2 from the same explicit `GameTimeAdvance` emitted by the single Tier 1 runtime driver. No per-character `Update` was added.

## U12 Tier 2 DOTS foundation
- `Tier2TransferState` is the explicit U12 boundary from an existing `Character`/stable identity into the supported ECS subset and back. Materialization never calls `StableEntityId.NewId`; duplicate active IDs are rejected before entity creation.
- `Tier2StableIdentity`, `Tier2BiologicalState`, `Tier2Position`, `Tier2Movement` and `Tier2SimulationProgress` are unmanaged, chunk-friendly components totalling 92 bytes per entity before ECS chunk overhead. No managed object, GameObject, Animator or NavMeshAgent is stored in hot components.
- `Tier2Materializer` owns a boundary-side stable-ID index, bulk creation and extraction. ECS `Entity` handles are transient and never become canonical identity.
- `Tier2SimulationSystem` schedules one `[BurstCompile] IJobEntity` across the active query. Movement, processed calendar ticks, processed biological ticks and step counts update in one parallel data-oriented pass; there is no per-agent `Update`.
- `Tier2Runtime` owns a private explicit ECS `World` and consumes only U03 `GameTimeAdvance`. A paused/zero advance schedules no agents and mutates no projected state. The world is intentionally not registered with the default player loop; U13 owns automatic tier coordination.
- Authority is field-scoped: Domain retains durable identity, biography, family, relationships, full health and other unprojected state. While a U12 projection is active, its supported position/movement/progress subset is authoritative until extraction at a completed-job barrier.
- The performance bootstrap measured 1,000 entities and also validates safe creation/update of 10,000 entities. It creates zero Tier 2 GameObjects and contains no MonoBehaviour subtype in `Game.ECS`.

## U11 work-group and job foundation
- `WorkGroupId` is a GUID-backed domain identity distinct from character IDs and session-local `JobId`. `WorkGroupRegistry` owns deterministic group state, optional commander and globally unique membership; every member is referenced by canonical character `StableEntityId`, so presenter destruction/respawn does not alter groups.
- `WorkOrder` supports Move and Haul, Low/Normal/High/Urgent priorities, Queued/Assigned/Active/Completed/Cancelled/Failed lifecycle, stable-ID or world-position targets and explicit claim state. `WorkManager` is the session-owned command/query authority, prevents incompatible duplicate open work and reserves exclusive targets.
- Group commands fan out deterministically into per-member jobs. Eligibility is an explicit Simulation policy hook; dead/unregistered members and already occupied workers are skipped so later profession/health/tier rules can replace the current minimal policy without changing group state.
- U10's scheduler now applies the documented U11 precedence: a carrying phase is non-interruptible; critical Energy at or below 0.10 forces Rest; manual/group work then outranks interruptible autonomous work; otherwise normal utility AI runs. Critical rest requeues unfinished manual work instead of losing it.
- Manual Haul reuses U10 quantity/capacity claims and U09 `ResourceTransferService`. Cancellation/failure releases reservations, and physical stock remains at its current canonical owner. Manual Move reuses the same `ITier1MovementDriver`/NavMesh path as autonomous movement.
- `SelectionProbe` supports Shift additive/toggle multi-selection. The development-only work overlay can create groups, change membership/priority, issue Move/Haul orders, cancel work and inspect status/claims; Ctrl+1…9 assigns groups and 1…9 recalls them. It is a command surface, not canonical state or U27 production UI.
- The implementation adds no per-NPC `Update`. The existing single `Tier1AiRuntimeDriver` advances all agents; automated coverage includes 128 registered characters across 8 groups with deterministic distribution and zero per-agent update ownership.

## U11 GUI click-through follow-up
- `WorldPointerRaycaster` now owns one pre-physics UI-block query for both EventSystem UI and registered development IMGUI regions. The U11 work panel, canonical resource panel and selected-entity debug panel register their full visible rectangles.
- `SelectionProbe` distinguishes a UI-blocked pointer from a world miss: UI clicks leave selection unchanged, while an unblocked empty-world click still clears a non-additive selection. World selection and Shift additive/toggle selection remain unchanged outside UI.
- U11 work buttons use the same Input System primary-click path as world selection. The work overlay resolves its actual button rectangles before dispatching Create, Select group, Add/Remove, priority, Move, Haul and Cancel commands, so UI blocking happens before selection and the command reads the current selected IDs.
- PlayMode regression uses a synthetic Mouse bound to the real `LocalGameplay.inputactions` Pointer map. It verifies selected Character -> Create from selection -> one-member group with the same Character and unchanged selection, full-panel click blocking, every other work button, outside-UI world raycasts and Shift multi-selection.

## U10 Utility AI foundation
- Each active agent is keyed by its existing Character `StableEntityId`; `Tier1AiAgentState` and bounded Hunger/Energy needs live outside `CharacterPresenter`. Presenter loss explicitly unmaterializes/unregisters the active Tier 1 agent without deleting the Character or its inventory.
- `Tier1AiScheduler` is the sole decision/need/action scheduler. It uses explicit U03 `GameTimeAdvance`, stable-ID ordering and deterministic phase staggering; pause/zero calendar delta advances nothing.
- Default `BALANCE_TBD` scheduling is a 1-game-minute think interval, 2-game-minute need interval, 5 decisions, 20 need updates and 25 action updates per scheduler advance, spread across 100 phase slots.
- Utility scores are finite and normalized to `[0,1]`. The action set is Idle, Rest and Haul; ties use explicit priority then action enum, while a 0.15 switch margin and a non-interruptible carrying phase prevent thrashing.
- Haul discovery uses canonical U09 registries and deterministic nearest-source/nearest-storage selection. Quantity claims reserve capacity without owning stock; real ownership changes only through `ResourceTransferService` at pickup/dropoff.
- The explicit Haul state machine is MoveToSource → Pickup → MoveToDestination → Dropoff → Complete/Failed. Cancellation before pickup leaves stock at source; after pickup it remains in the character inventory. Missing/full destinations may replan; path failure and a 15-game-minute timeout release claims.
- `NavMeshMovementDriver` is the Presentation bridge from pure `MoveTo` intent to `NavMeshAgent`. It reports Idle/Moving/Arrived/Failed and drives the existing U07 `Speed`/`Moving` animator parameters; root motion remains disabled.
- `Tier1AiRuntimeDriver` is one scene-level `MonoBehaviour.Update`. Per-agent AI Update count is exactly zero. LocalGameplay builds a runtime `NavMeshSurface`, registers both U07 demo characters and creates one free `DEV_BOOTSTRAP_ONLY` completed Storage Basket for the autonomous hauling slice.
- `Tier1AiDebugOverlay` is one development-only read view for selected character ID, action/phase, Hunger, Energy and canonical inventory contents.

## U10 development selection follow-up
- The existing pointer raycast/selection pipeline now recognizes bound world-pile and building presenters in addition to characters and debug markers; it does not introduce a second selection authority.
- The development selection panel captures a fresh read-only snapshot on every draw. Piles show stable ID, resource ID/name, live canonical quantity and active source claims/claimants. Storage shows building ID, capacity, occupied/free units and live quantities for every resource definition. Characters show stable ID, action/phase, source/destination/current target, canonical inventory and active haul job/claim.
- `HaulClaimRegistry` exposes deterministic read-only capture/query methods for debug tooling. Claims, inventories and resource quantities remain owned by Simulation/Domain services.
- The left resource overlay now enumerates the data-driven catalog and computes `world piles / characters / building storage = total` directly from the current inventory registry on every draw. No `DEV_BOOTSTRAP_ONLY` quantity is cached or repeated as UI state.

## Preserved architecture and scope
- `Game.Domain` and `Game.Simulation` remain `noEngineReferences`; no NavMesh, GameObject, Animator, physics query or Unity random source enters decision or action logic.
- U04 `SaveSnapshot` version 1, SQL migrations and Npgsql boundaries are unchanged. U13 runtime continuity is explicit but is not silently added to the old time-only persistence schema.
- Automatic distance policy changes representation only; it performs no births, deaths, events, production, remote jobs or RNG. Deterministic Tier 3/off-camera advancement remains U14.
- No scene asset, prefab, material or catalog data changed. Tier 1 appearance remains the existing deterministic Male/Female mapping; a richer persistent appearance descriptor is deferred.

## Validation status
U13 validation completed on 2026-10-06 with:

`& 'C:\serenity_game\Tools\Verify-U13.ps1' -FullRegression -ManagedPostgres -GpuValidation`

- 193/193 full non-PostgreSQL EditMode and 25/25 full PlayMode tests passed;
- focused U13 passed 9/9 EditMode and 2/2 headless PlayMode; GPU-enabled U13 PlayMode also passed 2/2;
- contained U12 regression passed 11/11 EditMode and 2/2 PlayMode;
- a fresh private loopback SCRAM PostgreSQL cluster passed 25/25 integration tests;
- U05A asset/catalog validation passed;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; updated `Game.ECS.dll` and `Game.Simulation.dll` are present and the player entry executable remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

The first pre-change baseline launch inside the filesystem sandbox hit Unity's known `BuildReportRestService/HttpListener` crash before tests. The identical approved outside-sandbox U12 verifier then passed completely. During U13 validation, the first full PlayMode run exposed a transient legacy presenter-despawn window to the automatic policy; policy retry handling was corrected and the entire final gate above was rerun successfully.

U11 GUI click-through follow-up validation completed on 2026-10-06 with:

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-focused.log' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WorkGroupPlayModeTests' -testResults 'C:\serenity_game\Logs\U11-gui-focused.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-all-playmode.log' -runTests -testPlatform PlayMode -testResults 'C:\serenity_game\Logs\U11-gui-all-playmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-core-tests.log' -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults 'C:\serenity_game\Logs\U11-gui-core-tests.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-gpu.log' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WorkGroupPlayModeTests.CreateFromSelectionUiClickUsesCurrentSelectionWithoutClickThrough' -testResults 'C:\serenity_game\Logs\U11-gui-gpu.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment`

- focused U11 PlayMode passed 6/6;
- full PlayMode passed 28/28;
- full non-PostgreSQL EditMode passed 193/193;
- GPU-enabled primary GUI regression passed 1/1;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited warnings; `Game.Presentation.dll` and `Game.Infrastructure.dll` were rebuilt at 2026-10-06 10:12:41;
- Windows Computer Use could not enumerate/attach to the player because its helper failed during setup with `helper_unknown_error`; the exact manual click sequence could not be independently repeated through desktop automation, while the same Pointer InputAction/UI dispatch path passed in both headless and GPU PlayMode.

## Next action
Start only U14 from `docs/NEXT_TASK.md`. U14 has not been implemented.
