# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; AI Navigation 2.0.12; Windows x64 Mono Development.

## Current milestone / last completed task
U14 DONE — deterministic, seed-controlled Tier 3/off-camera catch-up is independent of input order, time chunking and Tier1/Tier2/Tier3 materialization history.

## Active task
U15 — production and farming is next and has NOT started.

## U14 deterministic off-camera simulation
- `OffCameraSimulationService` is a plain engine-free Simulation service with an explicit world seed, absolute U03 calendar/biological targets and a one-calendar-day fixed step. It never reads wall time, frame delta, camera state or Unity random state.
- `CharacterRuntimeState` remains the U13 transition DTO and now carries last committed absolute simulation ticks, completed remote steps, deterministic accumulator and generic abstract activity progress. Persistent identity, biography, family, health, inventory and work remain in their existing canonical owners.
- `DeterministicKeyedRandom` derives each sample from world seed + `StableEntityId` + absolute step + stream ID. There is no shared cursor, so traversal order and neighboring characters cannot reroll outcomes. Stable state hashes exclude GameObject instance IDs, ECS handles, pointers and timestamps.
- Tier 3 catches up before both demotion commit and promotion capture. Active Tier 3 records advance in one batch before distance-policy transitions. Tier 1/Tier 2 retain the same U14 fields, so T3-only, T3→T2→T3 and T3→T1→T3 routes converge without player intervention and do not double time.
- The current U14 abstract stream is intentionally generic: it advances an audited integer progress value and accumulator but does not implement U15 production, U23 battle, U28 events, U18 mortality, remote pathfinding or resource mutation.
- Hunger/Energy are preserved while remote rather than running full Tier 1 Utility AI. Manual work remains assigned/suspended, physical movement/path state is not simulated, and inventory quantities, family links and health remain unchanged.
- Biological aging still uses the canonical U06 birth tick evaluated against the U03 biological timeline. U14 commits the latest biological tick but adds no second age field or natural-death policy.
- Detached constructor capture/restore proves save/restart determinism; `SaveSnapshot` v1, PostgreSQL schema and durable world-save integration remain unchanged pending an explicit migration.
- The development character inspector now shows current tier, configured world seed, last remote calendar/biological ticks, pending catch-up, abstract progress and deterministic state hash without enlarging its U11 UI-blocking rectangle.

## U13 tier lifecycle foundation
- `CharacterSimulationTier` is the strongly typed Tier1/Tier2/Tier3 state. `TierManager` is a plain session-owned Simulation service with `GetTier`, explicit controlled transitions, safe same-tier no-ops, re-entrancy protection and a one-active-representation invariant.
- Transitions capture source runtime state, validate the target before release, dematerialize the source, materialize the target and restore the captured source on target failure. Tests force a target failure and verify the old Tier 1 representation remains usable.
- Durable identity, name, sex, birth/death, parents, spouse, family/dynasty, traits, skills, health, profession, Wealth/Influence and relationships remain owned by the existing `Character`. `CharacterRuntimeState` carries only representation continuity: position, lightweight movement/progress, coarse location key and current U10 Hunger/Energy values.
- Tier 1 uses the existing deterministic sex-to-prefab catalog, presenter registry/spawner, centralized AI scheduler and NavMesh adapter. Demotion unregisters Tier 1 execution, stops the NavMesh-only path, releases haul claims and requeues an active manual order while leaving the high-level assignment, canonical inventory and work-group membership intact.
- Tier 2 reuses `Tier2TransferState`, `Tier2Materializer` and `Tier2Runtime`. Extraction and dematerialization complete all tracked jobs first; no second ECS implementation or identity authority exists.
- Tier 3 is one `Tier3CharacterRecord` per named persistent character in a pure C# registry. It stores the same ID plus restoration/runtime continuity and no GameObject/ECS Entity or copied biography/inventory. U14 now advances that record through the same boundary.
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

## U11 RTS right-click Move follow-up
- The existing U05 `Pointer/SecondaryClick` action now enters one `ManualMoveInputController`. It calls the existing `WorldPointerRaycaster` exactly once, accepts only `BuildableGround`, and inherits the same EventSystem/development-IMGUI blocking boundary. Building-placement right-click cancellation is routed through this same secondary-click coordinator so one physical click cannot both cancel placement and issue Move.
- Selected character IDs take precedence; with no selected character, the currently active U11 work group supplies the targets. `ManualMoveCommandService` is a pure Simulation command boundary that fans the click out into per-character U11 `WorkOrder` Move jobs in stable-ID order.
- Multiple executable Tier 1 characters receive deterministic 1.5 m `BALANCE_TBD` grid slots centred on the clicked world position. UI/selection never calls `NavMeshAgent.SetDestination`; U10's existing manual action and `ITier1MovementDriver` bridge remain the only physical execution path.
- `TierManager` exposes read-only `ICharacterTierLookup`. Tier 2/Tier 3 members receive an explicit `TierUnavailable` command result and no job; a click never auto-promotes a representation or mutates U14 state. Existing open manual work is reported as `AlreadyHasWork` instead of creating an incompatible duplicate.
- The debug "Assign group Move to demo point" button remains as a fallback but now uses the same tier-aware formation command service.

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
- U04 `SaveSnapshot` version 1, SQL migrations and Npgsql boundaries are unchanged. U14 runtime continuity is snapshot-ready but is not silently added to the old time-only persistence schema.
- Automatic distance policy still changes representation only. U14 runs its deterministic batch before policy evaluation; births, deaths, events, production and remote jobs remain outside U14.
- No scene asset, prefab, material or catalog data changed. Tier 1 appearance remains the existing deterministic Male/Female mapping; a richer persistent appearance descriptor is deferred.

## Validation status
U11 RTS right-click Move follow-up validation completed on 2026-10-06 with:

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-editmode.log' -runTests -testPlatform EditMode -testCategory U11 -testResults 'C:\serenity_game\Logs\RTS-move-editmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-playmode.log' -runTests -testPlatform PlayMode -testCategory U11 -testResults 'C:\serenity_game\Logs\RTS-move-playmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u05-edit.log' -runTests -testPlatform EditMode -testFilter 'Game.Tests.U05CameraAndInputTests' -testResults 'C:\serenity_game\Logs\RTS-move-u05-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u13-edit.log' -runTests -testPlatform EditMode -testCategory U13 -testResults 'C:\serenity_game\Logs\RTS-move-u13-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u13-play.log' -runTests -testPlatform PlayMode -testCategory U13 -testResults 'C:\serenity_game\Logs\RTS-move-u13-play.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u14-edit.log' -runTests -testPlatform EditMode -testCategory U14 -testResults 'C:\serenity_game\Logs\RTS-move-u14-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u14-play.log' -runTests -testPlatform PlayMode -testCategory U14 -testResults 'C:\serenity_game\Logs\RTS-move-u14-play.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment`

- focused U11 EditMode passed 12/12 and U11 PlayMode passed 10/10, including two-NPC physical arrival, formation spacing, unchanged StableEntityId, completed jobs, Utility AI return, active WorkGroup fallback, UI click-through blocking and right-click building-placement cancellation without a Move side effect;
- U05 input regression passed 5/5; U13 passed 9/9 EditMode and 2/2 PlayMode; U14 passed 14/14 EditMode and 2/2 PlayMode;
- Windows x64 Mono Development build succeeded with errors=0 and the 2 inherited BuildReport warnings; `Serenity.exe` remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing-reference diagnostic or runtime exception in the successful gates. U15 was not started.

U14 validation completed on 2026-10-06 with:

`& 'C:\serenity_game\Tools\Verify-U14.ps1' -FullRegression -ManagedPostgres -GpuValidation`

- full non-PostgreSQL EditMode passed 207/207 and full PlayMode passed 30/30;
- focused U14 passed 14/14 EditMode and 2/2 headless PlayMode; GPU-enabled U14 PlayMode passed 2/2;
- U13 regression passed 9/9 EditMode and 2/2 PlayMode; U12 passed 11/11 and 2/2; U11 passed 11/11 and 6/6; U10 passed 14/14 and 5/5;
- fresh private loopback SCRAM PostgreSQL passed 25/25; U05A asset/catalog validation passed;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; `Game.Domain.dll`, `Game.Simulation.dll` and `Game.Infrastructure.dll` were rebuilt, and `Serenity.exe` remains 667136 bytes;
- final scale evidence: 1 character × 1 year = 0.106 ms; 1,000 × 365 days = 97.005 ms with 0 measured managed bytes in the timed region and hash `8C41DA6B26E39AB8`; optional 10,000 × 30 days = 163.457 ms with hash `C61F2B8EAE3C22F0`;
- final diagnostics found no compiler error, failed assertion, missing script/reference, runtime exception or shader error in the successful gate logs.

The first post-implementation all-PlayMode gate found that four extra debug labels enlarged the U10/U14 inspector's registered IMGUI blocker enough to cover the complete 640×480 headless test surface together with the existing panels. The U14 fields were compacted into the existing two header lines without changing the blocker rectangle; focused U11 then passed 6/6 and the complete verifier above was rerun successfully from the beginning.

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
Start only U15 from `docs/NEXT_TASK.md`. U15 has not been implemented.
