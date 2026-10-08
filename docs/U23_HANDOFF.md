# U23 Handoff — Abstract Battle and Physical Return

## Status

U23 is DONE and committed only after every configured mandatory gate passed. U24 has not started.

## Implementation summary

1. Summary: one U20 encounter now remains canonical across Physical and Abstract resolution; transitions preserve realized battle, Character, health and U21 unit state.
2. Files added: `BattleResolutionService.cs` (+ `.meta`), `AbstractBattleTests.cs` (+ `.meta`), `AbstractBattlePlayModeTests.cs` (+ `.meta`), `Tools/Verify-U23.ps1`, this handoff and `ABSTRACT_AND_PHYSICAL_BATTLE.md`.
3. Files modified: combat/military domain and services, snapshot/XML/PostgreSQL version acceptance, local composition, AI clock debug advance, combat/unit debug panels, persistence/version tests and the project protocol docs.
4. Canonical model: `CombatEncounterState` is the battle; `CombatService` is its sole mutable authority.
5. Identity: `BattleId == EncounterId` for the complete lifetime. Unit and Character IDs are referenced, never copied or regenerated.
6. Physical/U20: Physical mode uses the unchanged position/NavMesh tactical loop and U20 commands.
7. Abstract architecture: engine-independent centralized fixed-step advancement over the same encounter state.
8. U21 integration: unit-linked Physical battles require Detailed units; Abstract battles require Aggregate units and reconcile summaries/captains through U21.
9. Shared parameters: both modes call `CombatRules`, the same weapon catalog/stat query, U18 capability/injury boundary and morale constants.
10. Fixed step: one game minute from last committed tick to target `GameClock` tick.
11. Targeting: valid assigned/current target first; otherwise stable opposing formation-edge ordering.
12. Attack/cooldown: persisted absolute `NextAttackTick` plus monotonic per-combatant attack sequence; mode changes never reset either.
13. Injury/death: U18 owns condition/incapacity/death and delegates fatal monarch processing to U17.
14. Morale/retreat: shared casualty loss/break threshold; Breaking→Retreating→Disengaged after the shared 15-minute duration.
15. Captain death: recorded once in history; U21 picks its normal deterministic fallback or Leaderless state.
16. Monarch death: off-camera fatal injury triggered U17 succession once without a duplicate Character or duplicate succession.
17. Physical→Abstract: mode pin is released, both units transition through U21, then one mode event commits; failure rolls back.
18. Abstract→Physical: both units materialize before Physical mode commits; failure restores Aggregate state.
19. Repeated transitions: battle, unit and participant IDs plus histories remain stable through repeated round trips.
20. Mid-cooldown: next attack tick and sequence survive; tests prove no transition duplicate-hit.
21. Return positioning: existing U13 runtime `WorldPosition` is retained/rematerialized in stable order; no new formation reset. Exact future NavMesh trajectory is not guaranteed.
22. TPS: a directly controlled participant returns `BlockedByDirectControl`; exit TPS makes the transition eligible.
23. Tiers: active Physical fighters are Tier1-pinned; Abstract unit members use existing Aggregate Tier2/Tier3 representation.
24. Save/load: v10 writes mode, side-unit links and events and resumes from the committed tick without replay/reroll.
25. Snapshot: `SaveSnapshot.CurrentVersion == 10`; v1-v9 are explicitly exercised.
26. PostgreSQL: opaque XML version acceptance includes v8/v9/v10; integration was not run because `DYNASTYGAME_PG_TEST_DATABASE` is absent.
27. History: ordered Started, ModeChanged, SideBroken, CaptainDeath and Finished records; attack history remains separate.
28. Debug UI: start Physical/Abstract, inspect ID/mode/tick/units/hash, transition both ways and advance through the canonical clock.
29. Tests added: 12 EditMode and 3 PlayMode U23 cases covering shared rules, abstract resolution, all transition routes/states, persistence, determinism, deaths, convergence, stress, clock/pause/visibility and TPS blocking.
30. Exact results: U23 EditMode 12/12; headless PlayMode 3/3; GPU PlayMode 3/3; required regressions 87/87 EditMode + 25/25 PlayMode; persistence 35/35; full EditMode 332/332; full PlayMode 68/68.
31. Determinism: combined catch-up/order hash `52A0A55A95EB4EB6`.
32. Catch-up: one direct jump equals minute-by-minute stepping exactly.
33. Collection order: reversed character/unit registration produces the same snapshot/hash.
34. Multiple battles: ten simultaneous isolated battles are processing-order independent; hash `6E9C1A83D3AABCC0`.
35. Convergence method: 100 identical paired starts, one controlled Physical path and one Abstract path, comparing winner, casualties and duration.
36. Measured convergence: winner delta 0.0%, mean casualty delta 1, mean duration delta 3 minutes.
37. Stress: 250v250 (500 total) completed with hash `D5DD44B2E9D730AF`.
38. Performance: 3605.191 ms and 0 measured current-thread managed bytes in the timed stress section; this is a test observation, not a U29 frame budget.
39. GPU/manual: Direct3D GPU PlayMode 3/3. No manual click session is claimed; equivalent headless and GPU paths passed.
40. Build: Windows x64 Mono Development succeeded, 0 errors and 2 inherited warnings.
41. Logs: no compiler/test assertion/unhandled errors. Existing obsolete `FindObjectsSortMode` warnings and normal licensing/shader capability diagnostics remain.
42. Documentation: PROJECT_STATE, TASK_GRAPH, NEXT_TASK, ARCHITECTURE and DECISIONS updated; this handoff and the battle design document added.
43. Diff checks: unstaged and staged `git diff --check` must be clean before commit (line-ending conversion notices are informational).
44. Commit: `U23: implement abstract battle and physical transition`; record the resulting hash in the final response.
45. Known limits: exact physical Transform/NavMesh future path and cross-mode future casualty identity are not promised; no global-map/camera policy automation, reinforcement, siege, supply or U24+ feature was added.
46. Definition of Done: every U23 item passed; PostgreSQL was conditional and not configured. U24 remains untouched.

## Exact validation command

```powershell
& 'C:\serenity_game\Tools\Verify-U23.ps1' -FullRegression -GpuValidation -BuildPlayer -StepTimeoutSeconds 900
```

Final result: `U23 validation passed.` The verifier runs compile; U23 EditMode/headless PlayMode; U13/U14/U17/U18/U20/U21/U22 regressions; persistence; full EditMode/PlayMode; GPU U23; and Windows build. PostgreSQL is enabled separately with `-PostgresIntegration` only when its database environment is configured.
