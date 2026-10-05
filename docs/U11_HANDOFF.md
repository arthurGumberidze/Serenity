# U11 handoff — work groups and jobs

## Status
U11 is DONE. The implementation adds engine-free work-group/order state, a session-owned command manager, centralized manual-work scheduling, multi-selection and a development command surface without adding per-NPC frame loops.

## Canonical state and identity
- `WorkGroupId` is GUID-backed and separate from character `StableEntityId` and `JobId`.
- `WorkGroupRegistry` owns unique settlement-wide membership, deterministic member order and an optional commander. Presenter destruction/recreation cannot change membership.
- `JobId` is a typed session-local order ID. `WorkOrder` records Move/Haul type, priority, assignee/group, target, lifecycle and claim state.
- Jobs/groups are intentionally session-ephemeral in U11. `SaveSnapshot` v1, XML wire format, PostgreSQL schema and migrations are unchanged.

## Commands, scheduling and physical logistics
- `WorkManager` creates, distributes, assigns, queries, cancels and fails work. It rejects incompatible open work per character and owns exclusive target reservations.
- `IWorkEligibilityPolicy` is the extension point for later profession/health/tier rules. The current policy accepts only living characters present in `CharacterRegistry`.
- Scheduler precedence is non-interruptible carrying > critical Energy (`<= 0.10`) Rest > manual/group work > autonomous utility. Critical needs requeue unfinished manual orders.
- Manual Move uses `ITier1MovementDriver`. Manual Haul uses `HaulWorldQuery.TryClaimSpecified`, U10 claims and only U09 atomic transfers. Claims are released on completion/cancellation/failure and resources remain with their current canonical inventory owner.

## Input and development vertical slice
- Shift-click adds/removes characters from the current multi-selection while a plain click replaces it.
- The work overlay can create/select groups, add/remove selected members, cycle priority, submit group Move/Haul, cancel orders and inspect recent status/claims.
- Ctrl+1…9 assigns the current selection to a group slot; 1…9 recalls/selects that group.
- The UI and `CharacterPresenter.IsSelected` are disposable presentation state. They do not own groups, jobs or resources and are not the U27 production UI.

## Performance and tests
- There is no per-character work `Update`; the existing single `Tier1AiRuntimeDriver` advances all Tier 1 work.
- EditMode coverage includes group invariants, lifecycle/query/priority, reservations, deterministic distribution, 128 characters across 8 groups, preemption/critical need precedence, cancellation/failure conservation and return to autonomous AI.
- PlayMode coverage includes Shift selection, hotkey/group selection surviving presenter respawn, physical NavMesh group Move and group Haul through canonical inventories followed by autonomous AI.

## Validation
Exact final command:

```powershell
& 'C:\serenity_game\Tools\Verify-U11.ps1' -FullRegression -ManagedPostgres -GpuValidation
```

Results on 2026-10-05:
- full non-PostgreSQL EditMode: 173/173 passed;
- full PlayMode: 21/21 passed;
- focused U11: 11/11 EditMode and 3/3 headless PlayMode passed;
- U10 regression: 14 EditMode and 5 PlayMode passed;
- U09 regression: 10 EditMode and 4 PlayMode passed;
- fresh private loopback SCRAM PostgreSQL integration: 25/25 passed;
- U05A asset/catalog validation passed;
- GPU U11 PlayMode: 3/3 passed; `Logs/U11-work-groups.png` was manually inspected and showed the characters, resource piles, Storage Basket and work markers without visible shader failure;
- Windows x64 Mono Development build: succeeded, errors=0, 2 inherited BuildReport warnings; entry executable size 667136 bytes;
- final log scan: no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

The first sandboxed Unity compile probe hit the known Editor `HttpListener` sandbox crash. The same compile/test path was rerun outside that restriction by the final verifier and passed; this was an execution-environment limitation, not a project test failure.

## Deferred
U12 owns the DOTS Tier 2 bootstrap. U13 owns tier transitions. Profession-aware eligibility, durable work persistence, construction labor, production/farming, combat and production gameplay UI remain their TASK_GRAPH tasks. Do not duplicate work-group or job authority inside ECS entities or presenters when implementing those slices.

## Main files
- `Assets/Game/Domain/Work/WorkModels.cs`
- `Assets/Game/Simulation/Work/WorkManager.cs`
- `Assets/Game/Simulation/AI/ManualWorkActions.cs`
- `Assets/Game/Simulation/AI/Tier1AiScheduler.cs`
- `Assets/Game/Presentation/Interaction/SelectionProbe.cs`
- `Assets/Game/Presentation/Work/WorkDebugOverlay.cs`
- `Assets/Game/Tests/Editor/WorkGroupAndJobTests.cs`
- `Assets/Game/Tests/PlayMode/WorkGroupPlayModeTests.cs`
- `Tools/Verify-U11.ps1`
