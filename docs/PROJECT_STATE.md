# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; AI Navigation 2.0.12; Windows x64 Mono Development.

## Current milestone / last completed task
U11 DONE — Work groups and jobs. The player can form stable-ID work groups, select multiple characters, submit prioritized Move and Haul orders and observe their lifecycle while the centralized U10 scheduler remains the only Tier 1 decision/action authority.

## Active task
U12 — DOTS bootstrap Tier 2 is next and has NOT started.

## U11 work-group and job foundation
- `WorkGroupId` is a GUID-backed domain identity distinct from character IDs and session-local `JobId`. `WorkGroupRegistry` owns deterministic group state, optional commander and globally unique membership; every member is referenced by canonical character `StableEntityId`, so presenter destruction/respawn does not alter groups.
- `WorkOrder` supports Move and Haul, Low/Normal/High/Urgent priorities, Queued/Assigned/Active/Completed/Cancelled/Failed lifecycle, stable-ID or world-position targets and explicit claim state. `WorkManager` is the session-owned command/query authority, prevents incompatible duplicate open work and reserves exclusive targets.
- Group commands fan out deterministically into per-member jobs. Eligibility is an explicit Simulation policy hook; dead/unregistered members and already occupied workers are skipped so later profession/health/tier rules can replace the current minimal policy without changing group state.
- U10's scheduler now applies the documented U11 precedence: a carrying phase is non-interruptible; critical Energy at or below 0.10 forces Rest; manual/group work then outranks interruptible autonomous work; otherwise normal utility AI runs. Critical rest requeues unfinished manual work instead of losing it.
- Manual Haul reuses U10 quantity/capacity claims and U09 `ResourceTransferService`. Cancellation/failure releases reservations, and physical stock remains at its current canonical owner. Manual Move reuses the same `ITier1MovementDriver`/NavMesh path as autonomous movement.
- `SelectionProbe` supports Shift additive/toggle multi-selection. The development-only work overlay can create groups, change membership/priority, issue Move/Haul orders, cancel work and inspect status/claims; Ctrl+1…9 assigns groups and 1…9 recalls them. It is a command surface, not canonical state or U27 production UI.
- The implementation adds no per-NPC `Update`. The existing single `Tier1AiRuntimeDriver` advances all agents; automated coverage includes 128 registered characters across 8 groups with deterministic distribution and zero per-agent update ownership.

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
- U04 `SaveSnapshot` version 1, SQL migrations and Npgsql boundaries are unchanged. U11 group/job/claim execution is session-ephemeral; characters, inventories and resource locations retain their existing snapshot-ready state boundaries.
- U09 immediate construction funding remains unchanged. U11 does not add professions, production, farming, crafting, combat, DOTS, tier transitions or U27 production UI.
- No scene/prefab regeneration was needed for U11. Runtime composition avoids overwriting unrelated user-owned scene, prefab and imported-asset changes already present in the worktree.

## Validation status
U11 validation completed on 2026-10-05 with:

`& 'C:\serenity_game\Tools\Verify-U11.ps1' -FullRegression -ManagedPostgres -GpuValidation`

- 173/173 full non-PostgreSQL EditMode and 21/21 full PlayMode tests passed;
- focused U11 passed 11/11 EditMode and 3/3 headless PlayMode;
- contained regressions passed: U10 14 EditMode + 5 PlayMode and U09 10 + 4;
- a fresh private loopback SCRAM PostgreSQL cluster passed 25/25 integration tests;
- U05A asset/catalog validation passed;
- GPU-enabled U11 PlayMode passed 3/3; manual inspection of `Logs/U11-work-groups.png` confirmed both characters, piles, storage and order markers without visible shader failure;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; the player entry executable remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

## Next action
Start only U12 from `docs/NEXT_TASK.md`. U12 has not been implemented.
