# U13 handoff — Tier manager 1↔2↔3

## Status and scope
U13 is DONE on 2026-10-06. It adds the session-owned lifecycle coordinator that moves one canonical named `Character` among Tier 1 GameObject presentation, the existing U12 Tier 2 ECS projection and a lightweight Tier 3 data record. U14 deterministic off-camera advancement, production, combat, global-map transitions, production UI and formal 100→1000 profiling were not implemented.

## Canonical authority and identity
`CharacterSimulationTier` is the strongly typed tier enum. `TierManager.GetTier` reports the current tier and `Transition` controls every direct pair, including Tier1↔Tier3. Same-tier requests are safe no-ops. The manager rejects re-entrant transitions and asserts that exactly one declared representation is active.

The existing Domain `Character` remains the only durable authority for stable ID, name, sex, birth/death, parents, spouse, family/dynasty hooks, traits, skills, health, profession, Wealth/Influence and relationships. Transitions never call `StableEntityId.NewId`, restore a replacement Character or generate personal data. U09 inventory and U11 group/order registries continue to reference the same character ID and are not copied into tier state.

`CharacterRuntimeState` is the representation-neutral continuity DTO. It contains the same character ID, precise world position, lightweight movement vector/flag, coarse location key, processed calendar/biological ticks, step count and current U10 Hunger/Energy. Tier 3 stores this DTO in `Tier3CharacterRecord`; it does not copy biography, health, family, inventory or work data.

## Transaction order and rollback
The transition sequence is:

1. Assert the declared source is the sole active representation.
2. Capture source state and verify the returned identity.
3. Validate target materialization while the source is still valid.
4. Dematerialize the source.
5. Materialize and verify the target.
6. Commit the manager's tier only after target activation succeeds.

If target materialization fails, any partial target is removed and the source is rematerialized from the captured state. `TierTransitionException.SourceRestored` reports whether rollback succeeded. Automated coverage forces a target failure and proves that Tier 1 remains active and usable with the same runtime state.

## Tier 1 policy
`Tier1CharacterAdapter` uses the existing `CharacterPresentationCatalog`, `CharacterPresentationSpawner`, `CharacterPresentationRegistry`, `Tier1AiRuntimeDriver` and U10 scheduler. The catalog still provides the deterministic Male/Female mapping, so returning to Tier 1 restores the same prefab selection without random appearance reroll.

Demotion explicitly unregisters the centralized AI/movement bridge before presenter unbind. Scheduler cancellation stops the NavMesh path, releases haul claims and requeues active U11 manual work as Assigned. The high-level order and work-group membership remain. A resource already picked up remains in the canonical U09 character inventory. NavMesh path geometry is not transferred to lower tiers; Tier 2 Utility AI remains out of scope.

## Tier 2 barrier
`Tier2CharacterAdapter` maps `CharacterRuntimeState` to the existing `Tier2TransferState` and calls the existing materializer. `Tier2Runtime.Extract` and `Dematerialize` call `CompleteAllTrackedJobs` before reading or destroying an entity. `Tier2Materializer.Dematerialize` removes both the Entity and its boundary index entry. ECS owns only position/movement/progress while the projection is active.

## Tier 3 foundation
`Tier3CharacterRegistry` owns at most one `Tier3CharacterRecord` per named character. A record is ordinary pure C# data with no GameObject, MonoBehaviour or ECS Entity. The location key defaults to `local`; current U13 preserves exact position as well, so there is no incidental teleport or precision loss in the implemented local round trip. U14 may introduce explicit coarse-location advancement rules but must commit them through this state boundary.

## Automatic policy and runtime wiring
`TierDistancePolicy` is separate from `TierManager`. It uses configurable Tier1 enter/exit and Tier3 exit/enter thresholds, deterministic stable-ID traversal, minimum residency evaluations, an evaluation batch and a transition batch. Default values are `BALANCE_TBD`: 18/24 metres for Tier1 hysteresis, 64/80 metres for Tier2↔Tier3, 32 evaluations and 4 transitions per pass, and 2 residency evaluations.

Camera distance chooses representation only. It is not an input to character rules and performs no random draw or Tier 3 progression. A transient externally removed legacy presenter is counted as a policy failure and retried later rather than throwing from the frame loop.

`LocalSceneCompositionRoot` creates the adapters, runtime, manager and policy. `Tier1AiRuntimeDriver` remains the single scene-level AI `Update`; after it advances the U03 clock and Tier 1 scheduler, it publishes the same `GameTimeAdvance` to Infrastructure. Infrastructure steps the private Tier 2 world once and evaluates policy, so the clock is never advanced twice.

## Automated coverage
Focused U13 EditMode tests cover:

- Tier1→Tier2→Tier3→Tier1 identity and complete representative Character-state equality;
- every direct transition and exactly one active representation;
- same-tier no-op behavior;
- forced target failure and source rollback;
- Tier 3 record scope;
- canonical inventory quantity and work-group membership continuity;
- the real U12 adapter, ECS step, extraction barrier and Tier2→Tier3 removal;
- distance hysteresis, residency and transition budget;
- 100 repeated Tier1→Tier2→Tier3→Tier1 cycles without leaked representations.

Focused U13 PlayMode tests cover the real LocalGameplay composition, deterministic presentation restoration, position continuity, active manual-job requeue, claim cleanup, inventory/group persistence and duplicate prevention in every tier.

## Validation
Exact final command from `C:\serenity_game`:

```powershell
& 'C:\serenity_game\Tools\Verify-U13.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

Final results:

- full non-PostgreSQL EditMode: 193 passed, 0 failed, 0 skipped;
- full PlayMode: 25 passed, 0 failed, 0 skipped;
- focused U13: 9/9 EditMode and 2/2 headless PlayMode;
- GPU-enabled focused U13 PlayMode: 2/2;
- focused U12 regression: 11/11 EditMode and 2/2 PlayMode;
- fresh private loopback SCRAM PostgreSQL integration: 25/25;
- U05A asset/catalog validation: passed;
- Windows x64 Mono Development build: succeeded, errors=0, 2 inherited BuildReport warnings; `Serenity.exe` is 667136 bytes and updated `Game.ECS.dll`/`Game.Simulation.dll` are present;
- final U13 log scan: no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

The pre-change sandboxed U12 baseline initially hit Unity's known `BuildReportRestService/HttpListener` crash before tests. The identical outside-sandbox baseline command then passed. The first U13 full PlayMode run found the legacy transient presenter-despawn policy issue described above; after the retry/skip correction, the entire final verifier was rerun successfully.

## Main files
- `Assets/Game/Domain/Characters/CharacterTierState.cs`
- `Assets/Game/Domain/Characters/Tier3CharacterRegistry.cs`
- `Assets/Game/Simulation/Tiers/TierManager.cs`
- `Assets/Game/Simulation/Tiers/TierDistancePolicy.cs`
- `Assets/Game/Simulation/Tiers/Tier3CharacterAdapter.cs`
- `Assets/Game/Presentation/Characters/Tier1CharacterAdapter.cs`
- `Assets/Game/ECS/Tier2/Tier2CharacterAdapter.cs`
- `Assets/Game/Infrastructure/LocalSceneCompositionRoot.cs`
- `Assets/Game/Tests/Editor/TierManagerTests.cs`
- `Assets/Game/Tests/PlayMode/TierManagerPlayModeTests.cs`
- `Tools/Verify-U13.ps1`

## Deferred to U14 and later
U14 must advance Tier 3 deterministically from explicit U03 time and controlled seed/RNG state, prove zoom-independent outcomes and materialize the latest committed remote state through U13. Durable world-save DTO/schema evolution is still explicit future work; U13 did not modify `SaveSnapshot` v1 or PostgreSQL migrations. A richer appearance descriptor, pooled presentation policy, Tier 2 work/utility execution, remote production/jobs/events and formal profiler captures remain later tasks.
