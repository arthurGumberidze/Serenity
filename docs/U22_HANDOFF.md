# U22 handoff — third-person control

## 1–7. Summary, files and control flows

1. U22 implements RTS↔TPS control over the existing Tier1 Character; no Character, health, inventory, dynasty, combatant or military-unit copy is created.
2. Added runtime files: `Domain/Control/PlayerControlModels.cs`, `Simulation/Control/PlayerControlService.cs`, and `Presentation/Control/ThirdPersonControlController.cs`, with Unity metadata.
3. Added tests: `PlayerControlStateTests.cs` and `ThirdPersonControlPlayModeTests.cs`. Added this handoff and `THIRD_PERSON_CONTROL.md`.
4. Modified composition, movement, Utility AI, combat movement gating, input actions/source, save snapshot/coordinator/XML, U05 input expectations and version-sensitive persistence/military tests.
5. `PlayerControlState` stores only `Rts`/`ThirdPerson` plus optional controlled `StableEntityId` and enforces valid mode/identity combinations.
6. Eligibility requires an existing alive adult mobile non-incapacitated managed Character who is current monarch, first U17 heir, or captain of a player U21 unit; aggregate members and enemies are rejected.
7. Enter cancels/requeues the current action, pins/materializes Tier1, gives the existing movement bridge direct authority, switches input/camera and retains the same presenter and Character instance. Exit reverses those adapters and schedules immediate AI reconsideration.

## 8–15. Camera, movement and work

8. One reusable Cinemachine camera uses `CinemachineThirdPersonFollow` and a target parented to the selected existing presenter.
9. Camera handoff changes priority and disables/enables only the RTS controller. The original RTS rig transform remains unchanged and is restored in-session.
10. WASD is camera-relative; Shift runs. Movement calls the existing `NavMeshAgent.Move`, respects NavMesh/world constraints, health movement capability, pause and current game-speed multiplier.
11. `Tier1MovementAuthority` is exclusive: `Autonomous` or `DirectPlayer`. Autonomous destinations no-op while direct authority is held.
12. There is no `CharacterController`/Rigidbody motor and no second agent, so two components never independently move the Transform.
13. Enter cancels the active execution. U11 manual Move cancellation requeues the same job as `Assigned`; on exit the normal scheduler can resume it if still valid. No job or group membership is deleted or duplicated.
14. The controlled Character's Utility action stays Idle and cannot begin haul/research/production/manual Move; other NPCs and the global simulation continue.
15. Research assignment and work-group membership remain. Active research/production contribution is suspended through the existing action cancellation and external-owner gate.

## 16–25. Combat, units, tiers, input and presentation

16. U20 encounter, combatant, target, attack, morale, health/injury and outcome remain canonical. Combat locomotion requests are suppressed only for the controlled Character.
17. U22 intentionally has no manual attack input; existing U20 tactical attacks continue when their range/cooldown rules permit.
18. U21 unit ID, roster, captain, representation and order are unchanged; a controlled captain is the same Character object/member.
19. The Player control service is a `TierManager` guard: the controlled ID cannot demote from Tier1. Exit restores ordinary transition rules.
20. Canonical death exits TPS to RTS and U17 succession proceeds. No replacement Character is generated.
21. Canonical incapacitation exits TPS to RTS and direct movement stops.
22. Pause stops direct movement. x1/x2/x3/x5/x10 remain enabled and direct speed follows the global multiplier; camera look can continue while paused.
23. `T` toggles mode, WASD moves, pointer delta looks, Shift runs, Escape exits. RTS Camera/Pointer/Building maps are disabled in TPS; Time and Mode remain enabled.
24. TPS locks/hides the cursor outside batch mode; RTS unlocks/shows it. Tests do not depend on host cursor state.
25. The existing presenter's movement parameter continues to drive Idle/Walk/Run; no animation graph or asset was added.

## 26–29. Save, database and UI

26. Snapshot v9 adds a strict optional `playerControl` section with mode and controlled Character ID. Valid TPS restores through the same entry path; invalid/dead/incapacitated identity falls back to RTS.
27. `SaveSnapshot.CurrentVersion == PlayerControlVersion == 9`; strict v1–v8 decode remains supported.
28. PostgreSQL schema is unchanged because payload XML remains opaque. Integration was not run: `DYNASTYGAME_PG_TEST_DATABASE` is absent.
29. The click-blocked development overlay shows mode, identity, life/incapacity, movement owner, unit/captain and suspended/assigned order.

## 30–41. Tests and validation results

30. Added 7 U22 EditMode and 12 U22 PlayMode tests covering state/XML, ownership composites, monarch/heir/captain eligibility, camera/input, direct movement, orders/state preservation, valid/invalid/dead save-load, incapacitation, tier pin, 100 switches and U20 death/succession.
31. Results: U22 EditMode 7/7; U22 headless PlayMode 12/12; full non-PostgreSQL EditMode 320/320; full PlayMode 65/65; required EditMode regressions 100/100; required PlayMode regressions 33/33; U05 input 5/5; persistence 35/35; focused corrected U21 v8 round-trip 1/1.
32. RTS→TPS→RTS preserved the exact `Character` object/ID, health, inventory total, dynasty/relationships, research assignment, work group/job, U21 unit roster and captain.
33. One hundred switches retained one TPS rig, two total Cinemachine cameras, three controller subscriptions and the same presenter/state.
34. Direct control pins Tier1 and blocks demotion; exit permits Tier2 then rematerialization to Tier1.
35. Active U20 combat remained linked, did not acquire a NavMesh path while TPS owned movement, fatal injury exited to RTS, marked the same combatant dead and triggered monarch succession.
36. XML round-trip restored the valid controlled ID through the same controller/rig; invalid identity returned RTS. Legacy v8 has no control state and v1–v8 coverage remains green.
37. Tests verify RTS maps in RTS, TPS map in TPS, Time map in both, and no RTS pointer/building map leakage.
38. The controller/rig is scene-level and reused. The repeated-switch test found no component/callback/rig growth. No broad allocation benchmark is claimed.
39. GPU PlayMode passed 12/12 on Direct3D 11. No manual mouse/keyboard click session is claimed; headless and GPU automation cover the LocalGameplay paths.
40. Windows x64 Mono Development build succeeded: 0 errors, 2 inherited BuildReport warnings.
41. Successful gate logs contain no C# compiler errors, failed assertions, `NullReferenceException` or `MissingReferenceException`. The two build warnings are inherited summary warnings without new U22 diagnostic detail.

## 42–46. Documentation, Git and completion

42. Updated `PROJECT_STATE.md`, `TASK_GRAPH.md`, `NEXT_TASK.md`, `ARCHITECTURE.md`, `DECISIONS.md`; added `THIRD_PERSON_CONTROL.md` and this handoff.
43. `git diff --check` passed. Staged `git diff --cached --check` is run immediately before the U22 commit and reported in the final response.
44. Commit: recorded after validation as `U22: implement third-person control`.
45. Known limitations: no TPS manual attack/aim/jump/crouch/IK; camera state is not persisted; camera collision is basic Cinemachine behavior; session-only U11 orders remain session-only; no final HUD/remapping polish.
46. Every mandatory U22 Definition of Done item is covered by implementation/tests/build. PostgreSQL and manual clicks are explicitly not claimed because their environments were unavailable. U23 was not started.

## Exact commands

All commands used Unity `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`, `-batchmode`, `-projectPath C:\serenity_game`, an explicit `-logFile`, and an explicit result path where applicable.

```text
-nographics -runTests -testPlatform EditMode -testCategory U22
-nographics -runTests -testPlatform PlayMode -testCategory U22
-nographics -runTests -testPlatform EditMode -testCategory !PostgresIntegration
-nographics -runTests -testPlatform PlayMode
-nographics -runTests -testPlatform EditMode -testFilter Game.Tests.U05CameraAndInputTests
-nographics -runTests -testPlatform EditMode -testCategory U07;U10;U11;U13;U16A;U17;U18;U20;U21
-nographics -runTests -testPlatform PlayMode -testCategory U07;U10;U11;U13;U16A;U17;U18;U20;U21
-nographics -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.PersistenceTests
-runTests -testPlatform PlayMode -testCategory U22
-nographics -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment
git diff --check
git diff --cached --check
```

Artifacts are under `Logs/U22-*.log` and `Logs/U22-*.xml`; the Windows player is `Builds/Windows/Serenity.exe`.
