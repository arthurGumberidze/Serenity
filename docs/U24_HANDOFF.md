# U24 Handoff — Destruction, Fire and Building Capture

## Status

U24 is DONE and committed only after every configured mandatory gate passed. U25 has not started.

## Implementation and validation

1. Summary: the existing canonical building now supports bounded integrity, deterministic fire, exactly-once destruction, ruins, safe storage spill and battle-validated in-place capture.
2. Files added: `BuildingStructuralModels.cs` (+ `.meta`), `BuildingDamageService.cs` (+ `.meta`), `BuildingDamageDebugOverlay.cs` (+ `.meta`), focused EditMode/PlayMode tests (+ `.meta`), `Tools/Verify-U24.ps1`, this handoff and `BUILDING_DESTRUCTION_FIRE_CAPTURE.md`.
3. Files modified: building/resource/save domain, placement/production/resource/haul/save services, XML/PostgreSQL acceptance, presentation catalog/presenter, local composition, two legacy version tests and protocol docs.
4. Structural model: `Building` owns integrity, status, owner, fire/checkpoints, destruction/spill markers, processed damage IDs and history.
5. Integrity: max is definition-driven; current is bounded from max to zero.
6. Damage types: Generic, Combat and Fire; Siege is only a future enum hook if needed.
7. Damage application: one engine-independent service, stable event IDs, integer resistance and exactly-once delivery.
8. Destruction: zero integrity produces one Destroyed transition and keeps the canonical entity.
9. Ruins: destroyed grid occupancy is retained; no overlap or replacement ruins copy.
10. Production: uncommitted tasks cancel; destroyed sites cannot reserve, advance, deposit or receive normal delivery.
11. Crop plots: growth and autonomous production stop without a double harvest.
12. Storage: all positive quantities move atomically to deterministic WorldPiles at the building position.
13. Resource ownership: source is emptied only after prevalidation; rollback and spill markers prevent loss or duplication.
14. Fire: persistent None/Burning/Extinguished/BurnedOut state with start, intensity and checkpoint.
15. Progression: centralized one-game-hour fixed steps over active fires only.
16. Determinism: stable-ID ordering, absolute checkpoints and stable fire damage IDs; no Unity RNG.
17. Extinguish: reusable canonical command; no firefighter or water-logistics system.
18. Weather: fixed-point rain/snow suppression and hot-weather modifier consume existing deterministic U19 weather.
19. Ownership: canonical `CombatSideId` on the existing building.
20. Capture eligibility: intact, capturable, different valid side and finished battle won by that hostile side.
21. Capture flow: owner changes in place and one compact event is recorded.
22. Captured storage: same quantities stay in the same building storage.
23. Captured production: active tasks cancel for revalidation; intact site can subsequently operate.
24. U20 physical integration: `ApplyCombatDamage(..., Physical, ...)` reaches the common service.
25. U23 abstract integration: `ApplyCombatDamage(..., Abstract, ...)` reaches the identical service and integrity.
26. Off-camera: fire advances without GameObjects and direct catch-up equals incremental stepping.
27. Save/load: structural/fire/owner/history/checkpoint state is detached and strict; resource/task authorities remain separate.
28. Snapshot: `SaveSnapshot.CurrentVersion == 11`; v1-v10 compatibility is covered.
29. PostgreSQL: opaque XML version acceptance includes v11; integration was not run because `DYNASTYGAME_PG_TEST_DATABASE` is absent.
30. Debug UI: click-blocked building selector/status and canonical damage, destroy, ignite, extinguish and capture commands.
31. Presentation: property-block damage/critical/destroyed/owner tint and lightweight burning ParticleSystem.
32. Tests added: 14 EditMode and 3 PlayMode U24 cases covering structure, production/crops, spill, fire/weather, capture/combat hooks, persistence, invalid data, long run and stress.
33. Exact results: 14/14 focused EditMode; 3/3 headless and 3/3 GPU PlayMode; 107/107 required EditMode and 22/22 required PlayMode regressions; persistence 35/35; full EditMode 346/346; full PlayMode 71/71.
34. Fire result: 100 concurrent fires deterministic hash `0C6853F8DB3EC535`.
35. Catch-up: direct and hourly paths were equal; combined hash `C07BBB21A1362B1C`.
36. Collection order: reversed building registration produced the same fire state/hash `C07BBB21A1362B1C`.
37. Long run: deterministic hash `1BFF2BF299CD2678`, resources 74, piles 0, produced 24; no duplicates or resurrection.
38. Stress: 1,000 buildings with 100 burning completed with hash `DB7D823CB247BF78`.
39. Performance: 23.922 ms and 0 measured current-thread managed bytes for the stress advance.
40. GPU/manual: GPU PlayMode 3/3 and headless 3/3 passed. Computer Use returned no native Windows apps, so no manual clicks are claimed.
41. Windows build: x64 Mono Development succeeded; `Builds/Windows/Serenity.exe` exists.
42. Logs: build reported 0 errors and 2 inherited warnings; test suites reported no failures/skips.
43. Documentation: PROJECT_STATE, TASK_GRAPH, NEXT_TASK, ARCHITECTURE and DECISIONS updated; this handoff/design document added.
44. Whitespace: `git diff --check` and staged `git diff --cached --check` passed before commit.
45. Commit: `U24: implement building destruction fire and capture` (hash recorded in the final report).
46. Known limits: no propagation, repair economy, siege, physical debris, occupation/territory model, owner-aware faction economy or final UI.
47. Definition of Done: every mandatory U24 item passed; U25 was not started.

## Reproduction

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Verify-U24.ps1 -FullRegression -GpuValidation -BuildPlayer
```

The verifier has per-step timeouts and writes exact logs/results beneath `Logs/`. PostgreSQL can be added with `-PostgresIntegration` only when `DYNASTYGAME_PG_TEST_DATABASE` points to an isolated test database.
