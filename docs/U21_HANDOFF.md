# U21 Handoff — Army Captains and Abstract Units

## Status

U21 is DONE and committed only after the checks below. U22 has not started. The next task is U22 — Third-person control, pending review of this handoff.

## Delivered

- Engine-independent military unit IDs, stable owner/side IDs, historical Character-ID rosters, statuses, representations, orders, summary and v8 save models.
- Central `MilitaryUnitService` with multiple units, exclusive active membership, add/remove/transfer, captain validation, deterministic fallback and Leaderless behavior.
- Explicit rollback-safe Detailed/Aggregate transition service over the existing TierManager.
- U11 detailed Move reuse and logical aggregate Move/Hold/Engage intent.
- U20 combat bridge that supplies only eligible Tier1 members, stores the encounter link, observes active-combat pinning and reconciles casualties/injuries/captain/status without resolving attacks.
- Snapshot v8 with strict v1-v7 compatibility and opaque PostgreSQL compatibility.
- Click-blocked LocalGameplay debug overlay for unit creation, inspection/selection, captain/member commands, representation, orders and hostile battle start.
- Timeout-aware `Tools/Verify-U21.ps1`, 14 focused EditMode tests and 2 focused PlayMode tests.

See `docs/MILITARY_UNITS_AND_CAPTAINS.md` for authority and rule details.

## Domain model and rules

`MilitaryUnitId` wraps `StableEntityId`. `MilitaryUnitState` contains owner/side, captain ID, sorted roster history, status, representation, abstract location, bounded morale/cohesion, logical order, active encounter ID and revision. Roster entries are Active or Removed and retain join/removal sequences.

Unit ownership never includes Character data. Character remains canonical; U18 remains health/capability authority; U17 remains succession authority; U20 remains physical-combat authority. Death keeps the member in roster history while active/alive/combat-capable queries exclude the dead as appropriate.

Captain eligibility is active membership plus living adult combat capability. Fallback sorts by Influence descending and StableEntityId ascending. No candidate means Leaderless. A captain/monarch death still passes through U18/U17 exactly once.

Detailed/Aggregate is orthogonal to Tier1/Tier2/Tier3. Detailed uses Tier1 for eligible managed members. Aggregate uses an existing Tier2/Tier3 adapter and stores no duplicate soldier. The same IDs, Character objects, health, roster, captain and dynasty links survive a round trip.

## Validation evidence 2026-10-08

Final command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\serenity_game\Tools\Verify-U21.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -GpuValidation -BuildPlayer -StepTimeoutSeconds 900
```

Final result:

- compile: passed;
- U21 EditMode: 14 passed, 0 failed, 0 skipped;
- U21 headless PlayMode: 2 passed, 0 failed, 0 skipped;
- required U06/U11/U13/U17/U18/U20 EditMode regressions: 93 passed;
- required U11/U13/U17/U18/U20 PlayMode regressions: 19 passed;
- persistence: 35 passed;
- full non-PostgreSQL EditMode: 313 passed;
- full PlayMode: 53 passed;
- U21 GPU PlayMode: 2 passed on Direct3D 11.0 / Intel Arc Graphics;
- Windows x64 Mono Development: succeeded, 0 errors, 2 inherited warnings;
- PostgreSQL integration: not run because `DYNASTYGAME_PG_TEST_DATABASE` is absent; v8 remains opaque XML and required no schema migration.

Determinism, equivalence and scale evidence:

- reversed Character registration and roster input produced the same fallback captain, canonical serialized XML and unit state hash;
- Detailed→Aggregate→Detailed retained the unit ID, captain, roster, Character object identity and U18 condition hash;
- actual T1→T2→T3→T1 retained membership, captain, health and dynasty identity;
- active U20 combat blocked aggregation; cancellation/terminal reconciliation released the block;
- incapacitated members were excluded from U20 participants; canonical injury remained in U18;
- fatal captain injury changed canonical Character state, U17 processed succession, roster history remained and deterministic fallback captain was selected;
- v8 active encounter round-trip preserved unit IDs, roster, captain, representation, order and encounter link;
- versions 1 through 7 decoded with no military payload;
- 10 units / 100 members completed Detailed→Aggregate→Detailed in 6.474 ms with 0 measured current-thread managed bytes and state hash `A2DFEA74AD2C7F12`.

The final logs contained no C# compiler errors, failed assertions, unhandled runtime exceptions or missing scripts. The player build reported the two inherited warnings already present before U21. No interactive manual-click claim is made; headless and real-GPU PlayMode exercised creation, representation, identity continuity, active-combat pin/release and UI blocking.

## Files added

- `Assets/Game/Domain/Military/MilitaryModels.cs`
- `Assets/Game/Simulation/Military/MilitaryUnitService.cs`
- `Assets/Game/Presentation/Military/MilitaryUnitDebugOverlay.cs`
- `Assets/Game/Tests/Editor/MilitaryUnitTests.cs`
- `Assets/Game/Tests/PlayMode/MilitaryUnitPlayModeTests.cs`
- `Tools/Verify-U21.ps1`
- `docs/MILITARY_UNITS_AND_CAPTAINS.md`
- `docs/U21_HANDOFF.md`

Unity `.meta` files for the new folders and scripts are included.

## Files modified

- `Assets/Game/Domain/SaveSnapshot.cs`
- `Assets/Game/Simulation/SaveCoordinator.cs`
- `Assets/Game/Infrastructure/SaveXmlCodec.cs`
- `Assets/Game/Infrastructure/Persistence.Postgres/PostgresSaveStore.cs`
- `Assets/Game/Infrastructure/LocalSceneCompositionRoot.cs`
- `Assets/Game/Tests/Editor/PersistenceTests.cs`
- `Assets/Game/Tests/Editor/TacticalCombatTests.cs`
- `docs/PROJECT_STATE.md`
- `docs/TASK_GRAPH.md`
- `docs/NEXT_TASK.md`
- `docs/DECISIONS.md`
- `docs/ARCHITECTURE.md`

## Known limitations

- Aggregate units store representation/state only; U23 must add abstract battle resolution.
- Owner/side is a stable token, not the future U26 nation model.
- Abstract location is a token, not U25 map coordinates or travel.
- Detailed Move uses current U11 formation spacing; there is no formation editor.
- Recruitment, training, equipment management, logistics, wages, desertion and army hierarchy are outside U21.
- The IMGUI panel is development tooling, not U27 UI.

## Definition of Done

Every U21 Definition of Done item passed. U22 and U23 were not started.
