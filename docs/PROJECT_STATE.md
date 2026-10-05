# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; AI Navigation 2.0.12; Windows x64 Mono Development.

## Current milestone / last completed task
U10 DONE — Utility AI Tier 1. Active Tier 1 characters now own engine-free AI/needs state, make deterministic batched utility decisions and execute explicit Rest or Haul actions through one central scheduler. Hauling uses U09 canonical world-pile, character-inventory and building-storage transfers.

## Active task
U11 — Work groups and jobs is next and has NOT started.

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
- U04 `SaveSnapshot` version 1, SQL migrations and Npgsql boundaries are unchanged. U10 task/claim execution is session-ephemeral; character, inventories and resource locations retain their existing snapshot-ready state boundaries.
- U09 immediate construction funding remains unchanged. U10 does not add builders, player work groups, professions, production, farming, crafting, combat, DOTS or tier transitions.
- No scene/prefab regeneration was needed for U10. Runtime composition avoids overwriting unrelated user-owned scene, prefab and imported-asset changes already present in the worktree.

## Validation status
U10 validation completed on 2026-10-05:

- baseline before U10: 148/148 non-PostgreSQL EditMode, 13/13 PlayMode and Windows Development build passed at `62a433cb0df0d8479c8c0f72cbc970938f3a3d19`;
- 14/14 focused U10 EditMode and 5/5 focused U10 PlayMode passed headless;
- 162/162 full non-PostgreSQL EditMode and 18/18 full PlayMode passed;
- contained regressions passed: U07 4 EditMode + 4 PlayMode, U08 8 + 3, U09 10 + 4;
- a fresh private loopback SCRAM PostgreSQL cluster passed 25/25 integration tests;
- U05A asset/catalog validation passed;
- GPU-enabled U10 PlayMode passed 5/5; `Logs/U10-utility-ai.png` confirms the LocalGameplay characters, resource piles, Storage Basket and movement slice without visible shader failure;
- 100-agent pure scheduler validation processed all agents with a maximum decision batch of 8 in the test configuration, 0 per-agent AI Updates, 0 measured steady-loop thread allocations and 13.618 ms Stopwatch time (automated test measurement, not a Unity Profiler capture);
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; player entry executable remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

## Next action
Start only U11 from `docs/NEXT_TASK.md`. U11 has not been implemented.
