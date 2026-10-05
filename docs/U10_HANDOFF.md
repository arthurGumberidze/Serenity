# U10 handoff — Utility AI Tier 1

## Status and scope
U10 is DONE on 2026-10-05. It adds the first autonomous Tier 1 AI vertical slice: needs, deterministic utility selection, centralized batched scheduling, explicit Rest/Haul execution, U09 quantity claims and real NavMesh movement. U11 work groups, U12 DOTS, U13 tier management, U15 production/farming and the remaining excluded systems were not started.

## 1–6. Architecture, scheduler and identity
- The flow is `Tier1AiAgentState -> Tier1AiScheduler -> UtilitySelector -> action execution -> ITier1MovementDriver/U09 services`.
- AI state is pure Domain data and is not stored in `CharacterPresenter`, `NavMeshAgent`, Animator or a task MonoBehaviour.
- Every agent reuses its existing Character `StableEntityId`; `Tier1AiAgentRegistry` maps that ID to one active state and rejects duplicates.
- `Tier1AiScheduler` is one session-owned service. It keeps a stable-ID-sorted active list, deterministic ID-derived phase staggering and round-robin cursors for decision, need and action work.
- Default `BALANCE_TBD` timing is a one-game-minute think interval and two-game-minute need interval. Default caps are 5 decisions, 20 need updates and 25 action updates per scheduler advance across 100 stagger slots.
- Scheduler time comes only from U03 `GameTimeAdvance.CalendarDelta`. It never reads `DateTime`, wall-clock time, `Time.timeScale` or Unity random state. Zero calendar delta/active pause advances neither needs, decisions nor actions.

## 7–12. Needs and utility selection
- U10 implements only Hunger and Energy, normalized to `[0,1]`. Hunger increases and Energy decreases from explicit game-time delta; Rest recovers Energy. All mutations clamp to bounds and reject invalid rates/values.
- Default `BALANCE_TBD` rates are Hunger +0.35/game-day, Energy −0.45/game-day and Rest +360/game-day, with Rest complete at 0.95 Energy.
- Scores are finite `UtilityScore` values clamped to `[0,1]`; NaN and Infinity are rejected. Idle scores 0.05, Rest scores `1 - energy`, and Haul scores `0.65 * energy * (1 - 0.5 * hunger)` when work and movement are available.
- Selection chooses the highest available score. Equal scores break by explicit priority (Rest, Haul, Idle), then action enum value, with no random reroll.
- A valid current action remains committed until a competitor exceeds it by the configurable 0.15 margin. Once pickup makes a Haul claim `Carrying`, that critical phase is non-interruptible.
- Idle is the safe fallback when no work/need action can start. Food/Eat is intentionally absent because U09 has no food resource and U15 owns production.

## 13–21. Actions, work discovery and claims
- Implemented actions are Idle, Rest and Haul. Rest is stationary and increases Energy; no bed/room system was introduced.
- Haul execution is explicit and testable: MoveToSource → Pickup → MoveToDestination → Dropoff → Completed/Failed.
- `HaulWorldQuery` uses `WorldPileService`, `ResourceInventoryRegistry` and `BuildingRegistry`; it performs no GameObject, tag, physics or scene search.
- Source selection is nearest valid world pile, followed by StableEntityId tie-break. Destination selection is nearest completed building with a registered BuildingStorage inventory, again with StableEntityId tie-break.
- `HaulTaskId` is a typed session-local monotonic ID and is distinct from Character/building/pile stable IDs.
- `HaulClaimRegistry` reserves exact source quantity and destination capacity. It prevents double assignment and capacity races but neither mutates stock nor creates a second inventory.
- Claims release on completion, cancellation, path failure, invalid source/destination, agent unregister and scheduler reset. The registry supports claimant cleanup and destination reassignment.
- Pickup transfers exact units from world-pile inventory to the U09 character inventory. Dropoff transfers exact units from character inventory to the Storage Basket using only `ResourceTransferService`.
- Cancellation before pickup leaves the resource at source. After pickup, unavailable/full storage triggers deterministic replan when possible; otherwise the resource remains canonically in the character inventory. Nothing teleports back or disappears.

## 22–27. Navigation, animation and failure handling
- The package is `com.unity.ai.navigation` 2.0.12, already installed and now referenced explicitly by Infrastructure and PlayMode tests.
- LocalGameplay adds a runtime `NavMeshSurface` to its composition root, collects physics colliders inside a 100 × 8 × 100 volume around the project-owned ground and builds the walkable NavMesh at startup.
- `ITier1MovementDriver` is the engine-free Simulation port: CharacterId, current world position, Idle/Moving/Arrived/Failed, availability, MoveTo and Stop.
- `NavMeshMovementDriver` is a plain C# Presentation adapter over a runtime-added `NavMeshAgent`. Default speed is configurable at 2.5 m/s; it detects invalid/partial paths and arrival independently from animation.
- The adapter writes U07 `Speed` and `Moving`, returning to Idle after arrival. It does not introduce a competing animator system and root motion remains disabled.
- Movement phases have a default 15-game-minute timeout. Failed/unreachable paths end the action in a controlled state, stop movement, release the claim and permit later utility reevaluation.
- `Tier1AiRuntimeDriver` owns the single frame `Update`, advances GameClock, invokes the scheduler and refreshes movement/animation bridges. `PerAgentAiUpdateCount` is 0. Missing presenters are unregistered as active Tier 1 movement without deleting Character Domain state; carried resources remain in their inventory.

## 28–31. Conservation, LocalGameplay and performance
- Hauling conserves `world piles + character inventories + building storages`; all ownership changes pass through the U09 exact atomic transfer service.
- LocalGameplay retains the two U07 characters and four U09 physical resource piles, creates their capacity-8 inventories and adds one free completed `DEV_BOOTSTRAP_ONLY` Storage Basket at grid `(5,5)` so autonomous logistics can run without consuming its own demo stock.
- The male starts with default needs; the female starts at Energy 0.25 to make Rest observable. A development-only selected-character overlay reads action/phase, Hunger, Energy and canonical inventory. RTS camera, selection and building systems remain composed as before.
- The focused pure 100-agent test registers unique agents, processes every agent through the central scheduler, verifies hauling progress and enforces batch limits. The final full-suite measurement reported `agents=100 decisions=100 maxEvaluationsPerAdvance=8 elapsedMs=13.618 allocatedBytes=0 perAgentAiUpdates=0`. This is a `Stopwatch`/`GC.GetAllocatedBytesForCurrentThread` automated steady-loop measurement with fake movement, not a Unity Profiler capture or a U29 1000-agent gate.

## 32. Tests and validation
`Tier1UtilityAiTests` contains 14 EditMode tests covering normalized selection, unavailable actions, deterministic ties, commitment/non-interruptibility, explicit-time needs and bounds, claims/overbooking, scheduler batching/stagger/pause, Rest, complete hauling and conservation, cancel before/after pickup, destination-capacity race, path failure, missing destination after pickup, 100 agents and the absence of per-character AI MonoBehaviour updates.

`Tier1UtilityAiPlayModeTests` contains 3 PlayMode tests covering LocalGameplay NavMesh/agent registration, physical NavMesh movement plus `Moving` animation and canonical haul conservation, and active-pause behavior while the unscaled RTS camera remains independent.

Final commands (Unity 6000.6.4f1):

```powershell
./Tools/Verify-U10.ps1 -FullRegression -ManagedPostgres -GpuValidation
git diff --check -- <U10 allow-list>
git diff --cached --check
```

The verifier runs bounded PID-tracked Unity steps, tails logs on failure/timeout, and terminates only the process it launched. It runs the full non-PostgreSQL suites, focused U10 suites, a fresh private loopback SCRAM PostgreSQL integration cluster, U05A asset validation, GPU PlayMode validation and the Windows x64 Mono Development build.

Final results:
- baseline before changes: 148/148 EditMode, 13/13 PlayMode, Windows build passed;
- focused U10 EditMode: 14/14 passed;
- focused U10 PlayMode: 3/3 passed headless and 3/3 GPU-enabled;
- full non-PostgreSQL EditMode: 162/162 passed;
- full PlayMode: 16/16 passed;
- regressions inside those full suites: U07 4+4, U08 8+3, U09 10+4, all passed;
- PostgreSQL integration: 25/25 passed against a fresh private cluster;
- U05A asset/catalog validation: passed;
- Windows x64 Mono Development: succeeded, errors=0, 2 inherited BuildReport warnings; `Builds/Windows/Serenity.exe` is 667136 bytes;
- GPU artifact: `Logs/U10-utility-ai.png`, 616014 bytes, manually inspected with no visible pink shader or broken composition;
- final logs contain no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

## 33. Deferred boundary
- U11: player-created work groups, assignments, priorities and job policy.
- U12: DOTS Tier 2 execution.
- U13/U14: tier transitions and deterministic off-camera simulation.
- U15: food, Eat, production, farming, crafting and renewable work sources.
- Later explicit work: dynamic NavMesh obstacles/rebake, durable in-flight AI task/claim persistence, carried-item visuals, beds/rest points, professions and U29 profiler/100→1000 performance gates.

## Files
Created: Domain/Simulation/Presentation AI folders, U10 EditMode/PlayMode tests, `Tools/Verify-U10.ps1` and this handoff (with Unity `.meta` files).

Modified for U10: Infrastructure and PlayMode-test assembly references, `LocalSceneCompositionRoot`, three inherited PlayMode fixtures so their expectations include the deliberate demo Storage Basket/NavMesh vertical offset, and project state/task/decision/architecture documents. `SaveSnapshot`, SQL, migrations and Npgsql code are unchanged. User-owned vendor imports, scene/prefab/controller changes and ProjectSettings already present in the dirty worktree are not part of U10.
