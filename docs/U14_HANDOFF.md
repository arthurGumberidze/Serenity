# U14 handoff — deterministic off-camera simulation

## Status and baseline
U14 is DONE on 2026-10-06. Work started from clean commit `892f7a2 U11: block debug UI click-through`; U13 was already DONE, U14 was TODO and U15 had not started. The pre-change baseline command was:

```powershell
& 'C:\serenity_game\Tools\Verify-U13.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

It passed 193/193 full non-PostgreSQL EditMode, 28/28 full PlayMode, focused U13 9/9 EditMode and 2/2 headless plus 2/2 GPU PlayMode, U12 11/11 and 2/2, PostgreSQL 25/25, asset validation and the Windows x64 Mono Development build.

U14 implements only named-character deterministic long-horizon/off-camera continuity and its Tier 3 integration. It does not implement U15 production/farming, U17/U18 dynasty/death/disease/injury, U21/U23 armies/battle, U25 global map, U28 events or U29 formal performance gates.

## Architecture and state ownership
The flow is:

`U03 absolute time + world seed -> OffCameraSimulationService -> Tier3CharacterRecord batch -> CharacterRuntimeState -> U13 transition barrier`

`OffCameraSimulationService` belongs to engine-free `Game.Simulation`. It has no UnityEngine, GameObject, MonoBehaviour, ECS, NavMesh, Animator, Npgsql or SQL dependency and is not a singleton. `LocalSceneCompositionRoot` creates one session service from an explicit development world seed and advances active Tier 3 records once from the same U03 clock update already used by Tier 1/Tier 2.

The existing `Character` remains canonical for identity, birth/death, family, spouse, parents, health, traits, skills, profession, Wealth/Influence and relationships. Existing U09 inventories and U11 work/group registries remain canonical. U14 neither clones nor mutates those owners.

## Tier 3 simulation state
The existing `CharacterRuntimeState` now also carries:

- `LastSimulationCalendarTick` — exact committed absolute U03 calendar tick;
- `LastSimulationBiologicalTick` — exact committed absolute U03 biological tick;
- `OffCameraStepCount` — completed fixed long-horizon steps;
- `DeterministicAccumulator` — committed deterministic stream audit state;
- `AbstractActivityProgress` — generic integer progress for future remote systems.

These fields are copied by the existing Tier 1 and Tier 2 continuity adapters and stored by `Tier3CharacterRecord`; no second character identity or biography exists. Constructor capture/restore covers the complete state and is the current save-like DTO boundary. Durable `SaveSnapshot`/PostgreSQL schema evolution was not silently added.

## Fixed step and large time jumps
The fixed step is one absolute calendar day. Calls may target any nonnegative calendar/biological tick. The service commits exact target ticks while running deterministic logic only for crossed absolute day boundaries. Therefore:

- 24 hours once equals 24 calls of one hour;
- repeated/paused target time is a no-op;
- a ten-day continuous run equals five days + capture/restore + five days;
- one year uses 365 steps per character, not a replay of frames;
- backwards time is rejected before mutation;
- checked arithmetic rejects counter/progress overflow.

The batch prevalidates null/duplicate identities and computes all next states before replacing records, so ambiguous or invalid input cannot partially commit earlier records.

## World seed and deterministic RNG
`OffCameraSimulationSettings` requires an explicit `long WorldSeed` and fixed step. The LocalGameplay development composition exposes a serialized `developmentWorldSimulationSeed`; tests supply their own seeds. The seed is ordinary data intended for future world-save DTOs, not a wall-clock or process-derived value.

`DeterministicKeyedRandom` is a stateless counter-keyed generator. Every sample is derived from:

`world seed + StableEntityId + absolute step index + stream ID`

The current stream ID is `AbstractActivityStream`. No global mutable cursor exists, so inserting, removing or reordering other NPCs cannot reroll a character. The keyed value uses stable identity bytes, FNV-style key composition and a SplitMix64 finalizer. U14 applies each daily sample only to generic abstract progress and the accumulator; gameplay events remain U28.

## Ordering and deterministic hash
Batch records are sorted by `StableEntityId` for validation, commit summaries and hashing. Per-character keyed samples are already independent of traversal order. `ComputeStateHash` includes the configured world seed and stable-ID-ordered U14 state: identity, coarse location, last calendar/biological ticks, step count, accumulator and abstract progress.

The hash excludes GameObject instance IDs, ECS `Entity` index/version, pointer addresses, wall-clock timestamps, dictionary enumeration order, position-frame artifacts, physical AI/action state and mutable RNG objects. Replaying the same state/seed/time 100 times produced one exact hash; ascending and reversed 100-character collections produced identical per-character state and batch hash.

## Catch-up and transition barriers
`Tier3CharacterAdapter` accepts the service plus a current-time provider.

Promotion Tier3→Tier1/Tier2:

1. capture requests catch-up to the current U03 time;
2. the Tier 3 record commits the latest deterministic state;
3. U13 validates the target while the source still exists;
4. the source is released and the target materializes that latest state.

Demotion Tier1/Tier2→Tier3:

1. U13 captures the source DTO after its existing presentation/ECS barrier;
2. Tier 3 validates that current time is not behind the carried checkpoint;
3. materialization catches up once from the carried checkpoint to current time;
4. the Tier 3 record becomes active.

Active Tier 3 records are batch-advanced at most once per crossed fixed day before `TierDistancePolicy` evaluates camera-driven representation changes; same-day frame calls are O(1) skips. Transition capture still performs exact sub-day catch-up. Thus policy/zoom has no rule or RNG input. Tests prove that T3-only, T3→T2→T3 and T3→T1→T3 routes converge after ten days with no intervention. A transition at a fixed-step boundary followed by an advance to the same target does not add a second step.

## Aging, needs, jobs and resources
Biological aging continues to use U06 `BiologicalBirthTick` evaluated against the U03 biological timeline. U14 records the latest biological tick but stores no independent age. Natural-death policy remains deferred with later lifecycle work; U14 does not change `CharacterLifeState`.

Tier 3 does not run the U10 Utility AI, NavMesh movement or haul state machine. Hunger/Energy are preserved exactly while remote. Tier 1-only physical work is suspended/requeued by the existing U13 demotion policy; the high-level order and work-group membership survive and work is not silently completed.

U14 performs no resource transfer, production or farming. Character inventories retain the same canonical owner and quantities. Tests verify inventory object/quantity, work group, manual assignment, family links and health survive Tier 3 catch-up and return.

## Debug tooling
The existing development character inspector now reports current tier, world seed, last remote calendar/biological ticks, pending catch-up ticks, abstract progress and deterministic state hash. The values are read from current Simulation state and are not retained by UI.

An initial version added four extra GUI rows and enlarged the registered UI blocker. In the 640×480 headless U11 suite this combined with other panels to cover every sampled world point. The final implementation compacts all U14 data into the existing two header rows, retains the old blocker size and passes U11 click-through coverage.

## Scale and allocation evidence
Final focused EditMode output on this machine:

- one named character × one year: 0.106 ms;
- 1,000 named characters × 365 days: 97.005 ms;
- 1,000 result hash: `8C41DA6B26E39AB8`;
- measured managed allocation in the timed 1,000-character region: 0 bytes via `GC.GetAllocatedBytesForCurrentThread`;
- optional 10,000 named characters × 30 days: 163.457 ms;
- 10,000 result hash: `C61F2B8EAE3C22F0`.

The 1,000-character PlayMode test confirms zero added GameObjects and one unique stable ID per record. `Tier3CharacterRecord` is an ordinary C# object and U14 adds no MonoBehaviour or ECS Entity per remote NPC. These are functional bootstrap measurements, not U29 profiler gates.

## Automated coverage
Focused U14 EditMode has 14 tests covering:

- same state + same seed + same time exact equality;
- 100 identical replays;
- collection-order independence for 100 NPCs;
- continuous versus capture/restore continuation;
- 24-hour versus 24×one-hour chunking;
- keyed-stream neighbor independence and different-seed behavior;
- pause/repeated target no-op;
- at-most-one automatic batch per fixed day with exact transition catch-up;
- one-year jump;
- 1,000 and optional 10,000-character batches;
- T1/T2/T3 route equivalence and no double time;
- canonical family/health preservation and biological-age integration;
- backwards-time and duplicate-ID rejection.

Focused U14 PlayMode has 2 tests covering real LocalGameplay Tier3 catch-up/return with canonical inventory/group/order/health continuity and a 1,000-record zero-GameObject batch.

## Final validation
Exact final command:

```powershell
& 'C:\serenity_game\Tools\Verify-U14.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

Results:

- full non-PostgreSQL EditMode: 207 passed, 0 failed, 0 skipped;
- full PlayMode: 30 passed, 0 failed, 0 skipped;
- focused U14: 14/14 EditMode, 2/2 headless PlayMode and 2/2 GPU-enabled PlayMode;
- U13 regression: 9/9 EditMode and 2/2 PlayMode;
- U12 regression: 11/11 EditMode and 2/2 PlayMode;
- U11 regression: 11/11 EditMode and 6/6 PlayMode;
- U10 regression: 14/14 EditMode and 5/5 PlayMode;
- fresh private loopback SCRAM PostgreSQL: 25/25;
- U05A asset/catalog validation: passed;
- Windows x64 Mono Development build: succeeded, errors=0, 2 inherited BuildReport warnings;
- player entry executable: `Builds/Windows/Serenity.exe`, 667136 bytes;
- rebuilt player assemblies include `Game.Domain.dll` 72704 bytes, `Game.Simulation.dll` 68608 bytes and `Game.Infrastructure.dll` 37888 bytes;
- final successful gate logs contain no compiler error, failed assertion, missing script/reference, unhandled runtime exception or shader error.

## Main files
- `Assets/Game/Simulation/Tiers/OffCameraSimulationService.cs`
- `Assets/Game/Domain/Characters/CharacterTierState.cs`
- `Assets/Game/Simulation/Tiers/Tier3CharacterAdapter.cs`
- `Assets/Game/Infrastructure/LocalSceneCompositionRoot.cs`
- `Assets/Game/Presentation/AI/Tier1AiDebugOverlay.cs`
- `Assets/Game/Tests/Editor/OffCameraSimulationTests.cs`
- `Assets/Game/Tests/PlayMode/OffCameraSimulationPlayModeTests.cs`
- `Tools/Verify-U14.ps1`

## Deferred and next task
U15 must replace the generic activity seam with data-driven canonical production/farming rules where appropriate; it must not treat `AbstractActivityProgress` as a resource quantity. U18 owns disease/injury/lifecycle effects, U23 owns deterministic abstract battle, U25 owns global-map mode, U28 owns event definitions/effects and U29 owns profiler-backed performance gates. Durable world-save schema/version integration remains an explicit future migration.

The next task is U15 — Производство и фермерство. U15 has not started.
