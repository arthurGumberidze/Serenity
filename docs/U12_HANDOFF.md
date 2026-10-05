# U12 handoff — DOTS bootstrap Tier 2

## Status and scope
U12 is DONE on 2026-10-05. It adds a separate Entities/DOTS runtime representation that materializes and updates thousands of lightweight character projections without GameObject-per-entity. Tier 1 architecture is unchanged. U13 tier transitions, U14 off-camera simulation, Tier 3 aggregation, production, combat, global map and production UI were not started.

## ECS architecture and components
The explicit flow is:

`Character/StableEntityId -> Tier2TransferState -> Tier2Materializer -> ECS entity -> Tier2SimulationSystem -> Tier2TransferState extraction`

Components:
- `Tier2StableIdentity` — 16 bytes, existing GUID identity encoded as two unmanaged `ulong` values;
- `Tier2BiologicalState` — 24 bytes, birth/death ticks, biological sex and lifecycle state;
- `Tier2Position` — 12-byte `float3`;
- `Tier2Movement` — 16 bytes, `float3` units per game day plus moving flag;
- `Tier2SimulationProgress` — 24 bytes, processed calendar/biological ticks and explicit step count.

The validated component total is 92 bytes before ECS chunk/header overhead. No hot component contains a managed reference, GameObject, Animator, NavMeshAgent, ScriptableObject or canonical mutable aggregate.

## Stable identity and materialization
`Tier2TransferState.FromCharacter` captures the existing character ID and the supported lifecycle fields. `Tier2Materializer` never calls `StableEntityId.NewId`; it rejects an already active stable ID and rejects duplicate IDs in a bulk request before structural changes. ECS `Entity` is a transient handle. Extraction converts the unmanaged identity back to the identical Domain ID and preserves every supported field.

The materializer owns a managed stable-ID index only at the structural boundary. The index is not stored per entity and does not participate in the hot simulation job. U13 should reuse this boundary and must complete outstanding jobs before transition/extraction.

## Authoritative state rule
Domain remains authoritative for persistent identity, name, biography, family, relationships, full health, traits, skills, profession and all fields not projected by U12. While a Tier 2 projection is active, ECS is authoritative only for its supported mutable position, movement and processed-step values. Extraction produces the synchronization DTO. U12 does not mutate the Domain `Character` and does not claim that two independent characters exist.

## Time, pause and deterministic workload
`Tier2Runtime.Step` accepts only U03 `GameTimeAdvance`. It writes integer calendar and biological ticks to one world-level singleton, updates the system and completes all tracked jobs before returning. It never reads `DateTime`, `Time.time`, `Time.deltaTime` or `Time.timeScale`.

`Tier2SimulationSystem` schedules one parallel `[BurstCompile] IJobEntity`. Each moving entity advances by velocity × explicit game-day delta; every processed entity records the exact calendar/biological ticks and increments its step count. Identical input states and step sequences produce identical extracted results in automated coverage. A paused clock returns zero deltas, causing the system to schedule zero entities and mutate nothing.

## Scale and measured evidence
- Functional scale gate: 1,000 entities created and every entity updated.
- Additional safety gate: 10,000 entities created and updated in one job-backed pass.
- Tier 2 GameObjects added: 0, measured before/after the 1,000-entity bootstrap.
- Per-entity MonoBehaviour updates: 0; `Game.ECS` contains no `MonoBehaviour` subtype.
- Headless steady measurement after warmup: 1,000 entities × 120 steps in 12.298 ms total (about 0.1025 ms/step), 0 measured managed bytes.
- GPU-enabled steady measurement after warmup: 14.751 ms total (about 0.1229 ms/step), 0 measured managed bytes.
- Both measurements recorded `BurstCompiler.IsEnabled=True`, `[BurstCompile]=True`, `IJobEntity=True`.

The timing uses `Stopwatch` and allocations use `GC.GetAllocatedBytesForCurrentThread` around the steady loop. These values are automated validation evidence, not Unity Profiler, Memory Profiler or Entities Profiler captures; U29 owns formal performance gates and hardware profiling.

## Validation
Exact final command:

```powershell
& 'C:\serenity_game\Tools\Verify-U12.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

Results:
- full non-PostgreSQL EditMode: 184/184 passed;
- full PlayMode: 23/23 passed;
- focused U12: 11/11 EditMode and 2/2 headless PlayMode passed;
- GPU-enabled focused U12 PlayMode: 2/2 passed;
- U11 regression: 11 EditMode + 3 PlayMode passed;
- U10 regression: 14 EditMode + 5 PlayMode passed;
- fresh private loopback SCRAM PostgreSQL integration: 25/25 passed;
- U05A asset/catalog validation: passed;
- Windows x64 Mono Development build: succeeded, errors=0, 2 inherited BuildReport warnings; `Game.ECS.dll` is present and `Serenity.exe` is 667136 bytes;
- final U12 log scan: no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

The first sandboxed compile probe terminated before import and left `Temp/UnityLockfile`; no Unity process remained, so the exact stale temporary lock was removed. The same bounded compile/test path then ran outside the sandbox and passed. A rapid subsequent licensing startup exited before project initialization once; the immediate bounded retry completed normally. These were launch-environment events, not project compilation/test failures.

## Main files
- `Assets/Game/ECS/Tier2/Tier2Components.cs`
- `Assets/Game/ECS/Tier2/Tier2TransferState.cs`
- `Assets/Game/ECS/Tier2/Tier2Materializer.cs`
- `Assets/Game/ECS/Tier2/Tier2SimulationSystem.cs`
- `Assets/Game/ECS/Tier2/Tier2Runtime.cs`
- `Assets/Game/Tests/Editor/Tier2EcsTests.cs`
- `Assets/Game/Tests/PlayMode/Tier2EcsPlayModeTests.cs`
- `Tools/Verify-U12.ps1`

## Deferred to U13 and later
U12 does not register its private world with the default player loop, switch real U07 presenters, automatically decide tiers, create Tier 3 cohorts, persist Tier 2 runtime state, render ECS entities or copy U10/U11 work authority. U13 must implement the one-active-representation transition coordinator using this transfer boundary. U14 owns deterministic off-camera simulation, and U29 owns profiler-backed 100→300→500→1000 gates.
