# PROJECT_STATE.md

## Engine
Unity 6000.6.4f1 (12bfff696524), approved override D-008; URP 17.6.0; AI Navigation 2.0.12; Windows x64 Mono Development.

## Current milestone / last completed task
U21 DONE — persistent military units, captains, historical stable-ID rosters, detailed/aggregate transitions, U20 integration and version-8 persistence are complete.

## Active task
No implementation task is active. U22 is next but has not started and awaits review of U21.

## U21 army, captains and abstract units
- `MilitaryUnitService` is the engine-independent canonical authority for multiple units, stable `MilitaryUnitId`, stable owner/side, active and historical roster membership, captain assignment/fallback, orders, abstract location, status and summaries. Units reference existing Character IDs and never copy person, health, dynasty or GameObject state.
- Captain eligibility requires an active roster member whose canonical Character is alive and whose U18 capability can fight. Fallback is highest Influence then stable-ID ordinal; otherwise the unit becomes Leaderless. Captain death remains U18/U17 work and creates no replacement Character.
- Detailed/Aggregate is unit representation, not a fourth Character tier. The explicit transition service uses the existing `TierManager`: eligible living members move to Tier1 for Detailed and Tier3 by the current aggregate adapter default. Membership, captain, injuries, dynasty identity and canonical Character instances survive the route. Incapacitated members remain in roster and are not rematerialized for combat.
- Active U20 physical combat pins a unit to Detailed. `MilitaryUnitCombatService` registers owner sides, filters through U20 eligibility, creates the existing physical encounter, links both units and later derives morale/cohesion/status from U20 plus canonical U18/Character state. It resolves no attacks and adds no aggregate combat; U23 remains untouched.
- Dead members remain in roster history and are excluded from alive/combat-capable queries. Explicit removal also preserves a historical membership record; a Character cannot be active in two units. Detailed Move reuses U11 formation movement, while aggregate orders remain logical intent only.
- `SaveSnapshot.CurrentVersion` is 8. It persists sorted unit identity, side, captain, roster history, representation, status, order, summaries, active encounter link, abstract location and deterministic revision. V1-V7 remain readable. PostgreSQL accepts v8 as opaque XML and needs no schema migration.
- LocalGameplay exposes a click-blocked development unit panel for create/inspect/select, captain assignment, member removal, Detailed/Aggregate, Move/Hold and U20 battle start. It owns no canonical state and is not the U27 UI.
- Final gates: U21 14/14 EditMode, 2/2 headless and 2/2 GPU PlayMode; required regressions 93/93 EditMode and 19/19 PlayMode; persistence 35/35; full non-PostgreSQL EditMode 313/313; full PlayMode 53/53; Windows x64 Mono Development succeeded with 0 errors and 2 inherited warnings. PostgreSQL was not configured (`DYNASTYGAME_PG_TEST_DATABASE` absent).
- Determinism/performance evidence: reversed registration/roster order produced identical captain, XML and state hash. Ten units / 100 members completed Detailed→Aggregate→Detailed in 6.474 ms with 0 measured current-thread managed bytes and hash `A2DFEA74AD2C7F12`.

## U20 tactical RTS combat
- `CombatService` is the engine-independent, stable-ID authority for two-side encounters, eligibility, deterministic targeting, movement requests, melee cooldown attacks, morale, retreat and exactly-once outcomes. Combat adds no per-agent `Update`.
- Combat stats reuse canonical Character Strength/combat skill and U18 capability. Immutable `unarmed`, `stone_axe` and `stone_spear` profiles define range, interval, power and minimum injury. U20 intentionally uses deterministic always-hit melee.
- Attack resolution calls `HealthAndFamilyService.ApplyCombatInjury`; the U18 condition ladder supplies injury/incapacitation and fatal damage delegates to U17 succession exactly once. Repeated death after save/load is a no-op.
- Assigned targets win; otherwise the nearest eligible enemy is selected with stable-ID tie-break. Fixed half-metre approach slots provide small-group spacing, while actual movement uses the existing Tier 1/NavMesh boundary.
- Active combat is local Tier 1 activity: `TierManager` blocks demotion and `Tier1AiScheduler` suspends ordinary AI/work until finish/cancel. U21/U23 armies and aggregate combat were not started.
- Existing selection/work-group right-click routes hostile Character hits to Attack while terrain remains Move. Enemy combatants run the same centralized tactical loop. The click-blocked debug panel exposes a deterministic 4v4 demo and live encounter/combatant state.
- `SaveSnapshot.CurrentVersion` is 7. It persists active/finished encounters, targets, cooldown sequences, morale/retreat state, attack history and outcomes. V1-V6 remain readable. PostgreSQL accepts v7 as opaque XML and needs no schema migration.
- Final gates: U20 17/17 EditMode, 3/3 headless and 3/3 GPU PlayMode; required regressions 123/123 EditMode and 25/25 PlayMode; persistence 35/35; full non-PostgreSQL EditMode 299/299; full PlayMode 51/51; Windows x64 Mono Development succeeded with 0 errors and 2 inherited warnings. PostgreSQL was not configured (`DYNASTYGAME_PG_TEST_DATABASE` absent).
- Determinism/performance evidence: reversed registration/input order produced identical combat and health hashes. The 50v50 logical encounter completed in 203.162 ms with 0 measured current-thread managed bytes, 250 attacks and hash `BC4B733A3E3410B1`. Active and terminal v7 round-trips produced no duplicate attacks, injuries or deaths.
- Interactive Computer Use could not initialize its Windows helper (`setup refresh had errors`), so no manual clicks are claimed; physical 4v4 NavMesh and GPU PlayMode passed.

## U19 weather and seasons
- `SeasonCalendar` derives Spring/Summer/Autumn/Winter from the canonical U03 calendar. The configurable 365-day temperate mapping is Spring 1-91, Summer 92-182, Autumn 183-273 and Winter 274-365.
- `WeatherService` derives each daily fixed-point state from world seed, absolute day, climate-profile ID and keyed U14 streams. Temperate configuration owns seasonal temperature/range, weather weights, snow threshold and per-thousand gameplay modifiers. Camera, tier, frame order and collection traversal do not participate.
- Crop growth, seasonal wild-food gathering and outdoor production consume centralized modifiers. Winter/snow stops crop progress without destroying Ready crops or stored food. Aggregate workers spend raw time correctly under slowdown; Tier 1 and Tier 2 movement consume the same logical hook.
- Severe outdoor cold/heat can apply U18 `cold_stress`/`heat_stress` through stable-ID/day checkpoints exactly once. The current exposure set is intentionally active outdoor production workers plus aggregate Tier2/Tier3 workers; shelter/clothing/thermoregulation are outside U19.
- `SaveSnapshot.CurrentVersion` is 6. It persists climate identity and per-character processed exposure days; weather history is derived rather than stored. V1-V5 remain readable. PostgreSQL accepts v6 opaque XML and needs no migration or realtime authority.
- LocalGameplay adds a once-per-day environment projection and a click-blocked debug panel with date, season, weather, fixed-point temperature, precipitation, wind, modifiers and deterministic next-day preview.
- Final gates: U19 19/19 EditMode, 2/2 headless and 2/2 graphics PlayMode; full non-PostgreSQL EditMode 282/282; full PlayMode 48/48; required regressions 44/44 EditMode and 10/10 PlayMode; persistence 35/35; Windows x64 Mono Development succeeded with 0 errors and 2 inherited obsolete-test-API warnings. PostgreSQL was not configured (`DYNASTYGAME_PG_TEST_DATABASE` absent).
- Determinism evidence: 10,000 daily weather queries allocated 0 measured bytes, hash `000000000000003D`; the final repeatable 20-person five-year weather/farming run produced hash `4CF6296D268E48AA`, food 4000 and shortage 1 in 6.554 seconds for two runs.

## U18 family events, health and birth
- `HealthAndFamilyService` is the one engine-independent, stable-ID-based authority for conditions, narrow relationship events and pregnancy. It advances centrally on absolute game ticks; no condition, pregnancy or character gained an `Update`.
- Nine data-driven conditions cover injuries, diseases, temporary weakness and a permanent limp. Effective per-thousand work/movement/research/combat modifiers and incapacity feed U10, U15 and U16 without rewriting base stats. Fatal resolution delegates exactly once to U17 succession.
- Four deterministic family events mutate the existing bounded symmetric Character relationships once. Pregnancy validates alive married adults, age and health, lasts 750 biological days, persists, and creates a canonical Character with deterministic 128-bit ID, parent links and active-dynasty membership.
- Newborns are valid Tier3 records and cannot work/research before age 16. T3-only, T3-T2-T3 and T3-T1-T3 routes converge on hash `7877019868490988825`; the 20-person three-year hash is `15340608186238284958`.
- `SaveSnapshot.CurrentVersion` is 5. Health conditions, event markers and pregnancies are serialized; U17 character/dynasty state carries children, family links and relationship results. V1-V4 remain readable. PostgreSQL remains opaque and has no schema change.
- Final gates: U18 13/13 EditMode, 2/2 headless and 2/2 GPU PlayMode; full non-PostgreSQL EditMode 263/263; full PlayMode 46/46; required EditMode regressions 99/99; required PlayMode regressions 17/17; persistence 35/35; Windows x64 Mono Development succeeded with 0 errors and 2 inherited warnings. PostgreSQL's 25 tests were discovered but skipped because the test database environment variable was absent.

## U17 dynasty and succession
- `DynastyState` owns one stable dynasty identity, founder, exactly one active monarch ID, member references, succession rule ID, status, processed-death markers and deterministic history. It contains no copied person or GameObject state.
- `FamilyTreeQuery` derives parents, children, siblings and descendants from canonical U06 parent/spouse links. The `agnatic_primogeniture_v1` rule follows the FRS base law: only living male dynasty members are eligible; branches are ordered by biological birth tick then `StableEntityId`.
- `SuccessionService` is the sole event-driven death boundary. It marks canonical `Character` death, chooses an heir without reading tier or presentation, transfers `PlayerRealmState`, records one history entry, or sets `DynastyStatus.Extinct` and `GameRunState/GameOverReason.DynastyExtinction`.
- Dead candidates are skipped while their living male descendants retain branch priority. Childless monarchs fall back to sibling/collateral branches. Missing, duplicate, self/cyclic or asymmetric saved family references fail validation without a null-reference crash.
- `SaveSnapshot.CurrentVersion` is 4. V4 stores complete detached `CharacterState` values, dynasty state/history/processed markers, player ruler and game-run state. V1/V2/V3 remain readable. PostgreSQL still stores the validated opaque XML payload; no SQL schema migration was required.
- LocalGameplay uses fixed development IDs for Aren, Mira and two domain-only sons, exposes a development dynasty overlay, and registers its full panel with the existing pointer blocker. A Tier3 heir can become ruler immediately without materializing a GameObject.
- Final gates: U17 13/13 EditMode and 2/2 headless + 2/2 GPU PlayMode; full non-PostgreSQL EditMode 250/250; full PlayMode 44/44; required U06/U13/U14/U16/U16A EditMode 68/68; required U13/U14/U16/U16A PlayMode 9/9; persistence 35/35. Fresh managed PostgreSQL passed 25/25 after 250/250 core tests; Windows x64 Mono Development build succeeded with 0 errors and 2 inherited warnings.

## U16A debug gameplay controls
- LocalGameplay no longer assigns Aren or Mira to research during composition. The existing selection surface now drives idempotent `Assign selected to Research` and `Remove selected from Research` commands against the one `ResearchService`.
- The research panel reports canonical Assigned workers separately from Active workers. Active means an assigned stable ID whose authoritative Tier 1 AI state is `Research/Researching`; assigned-but-idle workers no longer inflate rate or ETA.
- `B` now opens a development-only selector generated from `BuildingPresentationCatalog`. It lists `primitive_shelter`, `storage_basket` and `primitive_crop_plot`, shows research-derived availability/reasons, and delegates unlocked choices to the existing placement controller. Direct locked requests also stop at the existing `LockedByResearch` boundary.
- The Time Input System map uses Space for pause/resume and Minus/Equals (Plus) for slower/faster. Speeds step through x1/x2/x3/x5/x10. U11 number-row work-group shortcuts remain untouched.
- `Tier1AiRuntimeDriver` remains the canonical pause coordinator over the one `GameClock`. A paused zero delta advances neither Tier 1 AI, Tier 2/3 consumers, production nor research; resume continues from the existing clock state without catch-up duplication.
- Development panels were separated enough for a 1600x900 Game View, and research/build/time panels register with the existing U11 pointer blocker. UI clicks cannot confirm placement, clear selection or create a world command behind a panel.
- Final gates: U16A 4/4 EditMode and 3/3 headless PlayMode; full non-PostgreSQL EditMode 237/237; full PlayMode 42/42; U05 5/5; combined U08/U10/U11/U15/U16 EditMode 63/63; combined U08/U10/U16 PlayMode 13/13; U09 4/4, U11 10/10 and U15 3/3 PlayMode; U16A GPU PlayMode 3/3 on Direct3D 11 / Intel Arc; Windows x64 Mono Development build succeeded with 0 errors and 2 inherited warnings.
- Windows Computer Use initialization failed twice before an application window was available (`helper_unknown_error`, then kernel reset). Interactive manual clicks are therefore not claimed; the equivalent control, lock, pause/resume and click-through paths passed headless and GPU PlayMode.

## U16 research framework and Stone Age
- `TechnologyDefinition`, `TechnologyCatalog`, typed unlocks and research save DTOs are engine-independent. The catalog contains 14 Stone Age technologies, validates duplicate/missing/self/cyclic dependencies and resolves every building, U15 recipe and capability target at construction.
- Starting knowledge is limited to `basic_survival` and `gathering`. One settlement-level `ResearchService` owns the active project, completed IDs, exact progress, assigned stable worker IDs, fractional rate remainders, completion tick and derived `ResearchUnlockSet`.
- Research uses absolute U03 calendar ticks and fixed-point rate-per-thousand hooks. Character Intelligence and optional `research` skill affect rate without creating another skill system. Stale tier checkpoints are deterministic no-ops, so representation changes cannot rewind or duplicate work.
- U10's centralized scheduler exposes Research as one normal Utility action. Tier 2 crosses its completed-job barrier and Tier 3 supplies its active stable IDs to the same service; there is no per-worker, per-technology or per-unlock `Update` and no tier-local research truth.
- U08 construction and U15 recipes keep their canonical definitions. Placement returns explicit `LockedByResearch`; production eligibility consults the live unlock view. Isolated legacy fixtures remain unlocked when no research view is injected.
- `SaveSnapshot` current wire version is 3. It persists completed technologies, active project/progress, assigned workers and fractional checkpoints, and last processed tick; unlocks are reconstructed from catalog + completed IDs. Strict XML still reads versions 1 and 2, and PostgreSQL schema remains unchanged.
- LocalGameplay has one development research overlay with available/locked/completed lists, prerequisites, progress/rate/ETA, unlock preview and Start/Cancel controls. It is a disposable command/read surface and participates in the existing UI pointer blocker.
- Final gates: 233/233 full non-PostgreSQL EditMode, 39/39 full PlayMode, 25/25 PostgreSQL, U16 12/12 EditMode, 2/2 headless and 2/2 GPU PlayMode, U08 8/8 + 3/3, U09 through U15 focused regressions, 35/35 persistence, asset validation and Windows x64 Mono Development build.
- Determinism evidence: route hash `A9022738CC9580C0`; 20-person/730-day research hash `B0B088B38CCE9E01`; matching production hash `F27F0B25EC99953B`, food shortage 0. Scale observation: 1,000 assigned workers over 5 daily batches in 8 ms, 0 measured managed bytes, hash `E135E16085E27656`.

## U15 production and farming foundation
- `ProductionRecipe`, `ProductionCatalog`, `ProductionSite`, `ProductionTask` and `ProductionSaveState` are engine-independent. Recipes declare inputs, outputs, work time, site kind and enabled state; the initial catalog produces `berries`, `plant_fiber`, `wood_log` and `stone` and can accept later processing chains without a parallel inventory/time/job system.
- `ProductionService` is the sole canonical production authority. It uses U09 inventories and atomic transfers, stable-ID-ordered fixed daily steps, exclusive site/task reservations, target-stock policy and finite natural sources. A completed batch is committed once; output is owned by the worker inventory first, can be deposited into completed building storage, and remains with the worker when storage is full.
- `berries` is the canonical U15 food resource. `FoodConsumptionService` consumes one unit per person per 15 game days by default, never makes stock negative and records shortage. Default targets are berries 40, plant fiber 20, stone 20 and wood logs 30.
- `primitive_crop_plot` reuses U08 placement/occupancy and U09 construction funding. The production site has deterministic Unplanted -> Growing -> Ready -> Harvested/Unplanted lifecycle; the current project-owned Storage Basket mesh is only a temporary crop-plot visual.
- Tier 1 extends the centralized U10 scheduler with MoveToSource -> Working -> DeliverProduction and uses the existing movement/transfer bridges. Tier 2 accumulates unmanaged production work ticks in its private ECS world and commits through the shared production service. Tier 3 advances the same service in stable-ID order through one-day fixed steps; no second RNG or tier-specific economy exists.
- `SaveSnapshot` current wire version is 2. The strict XML payload now stores production sites, active task progress and identities, targets, produced totals, canonical inventories, food counters and the last production tick. Legacy version-1 time-only XML remains loadable; PostgreSQL continues storing the opaque validated payload and its relational schema did not change.
- LocalGameplay composes finite berry/fiber/tree/rock sources, one crop plot, target policy, food consumption and a compact canonical production overlay. Production adds no per-worker, per-source or per-building `Update`.
- The final U15 gate passed 221/221 full non-PostgreSQL EditMode, 37/37 full PlayMode, 25/25 PostgreSQL, U15 13/13 EditMode, U15 3/3 headless and 3/3 GPU PlayMode, U09 10/10 EditMode and 4/4 PlayMode, asset validation and Windows x64 Mono Development build.
- Five-year baseline: 20 inhabitants, one crop plot, 20 starting berries, target 80 and storage capacity 1000; produced 2490, consumed 2433, shortage 0, final hash `2D19219657849A02`. Performance evidence: 1,000 workers / population equivalent 10,000 / 30 days completed in 4551.025 ms with 0 measured managed bytes and hash `6E074B86B963FC7A` on the final full gate.

## U14 deterministic off-camera simulation
- `OffCameraSimulationService` is a plain engine-free Simulation service with an explicit world seed, absolute U03 calendar/biological targets and a one-calendar-day fixed step. It never reads wall time, frame delta, camera state or Unity random state.
- `CharacterRuntimeState` remains the U13 transition DTO and now carries last committed absolute simulation ticks, completed remote steps, deterministic accumulator and generic abstract activity progress. Persistent identity, biography, family, health, inventory and work remain in their existing canonical owners.
- `DeterministicKeyedRandom` derives each sample from world seed + `StableEntityId` + absolute step + stream ID. There is no shared cursor, so traversal order and neighboring characters cannot reroll outcomes. Stable state hashes exclude GameObject instance IDs, ECS handles, pointers and timestamps.
- Tier 3 catches up before both demotion commit and promotion capture. Active Tier 3 records advance in one batch before distance-policy transitions. Tier 1/Tier 2 retain the same U14 fields, so T3-only, T3→T2→T3 and T3→T1→T3 routes converge without player intervention and do not double time.
- The current U14 abstract stream is intentionally generic: it advances an audited integer progress value and accumulator but does not implement U15 production, U23 battle, U28 events, U18 mortality, remote pathfinding or resource mutation.
- Hunger/Energy are preserved while remote rather than running full Tier 1 Utility AI. Manual work remains assigned/suspended, physical movement/path state is not simulated, and inventory quantities, family links and health remain unchanged.
- Biological aging still uses the canonical U06 birth tick evaluated against the U03 biological timeline. U14 commits the latest biological tick but adds no second age field or natural-death policy.
- Detached constructor capture/restore proves save/restart determinism; `SaveSnapshot` v1, PostgreSQL schema and durable world-save integration remain unchanged pending an explicit migration.
- The development character inspector now shows current tier, configured world seed, last remote calendar/biological ticks, pending catch-up, abstract progress and deterministic state hash without enlarging its U11 UI-blocking rectangle.

## U13 tier lifecycle foundation
- `CharacterSimulationTier` is the strongly typed Tier1/Tier2/Tier3 state. `TierManager` is a plain session-owned Simulation service with `GetTier`, explicit controlled transitions, safe same-tier no-ops, re-entrancy protection and a one-active-representation invariant.
- Transitions capture source runtime state, validate the target before release, dematerialize the source, materialize the target and restore the captured source on target failure. Tests force a target failure and verify the old Tier 1 representation remains usable.
- Durable identity, name, sex, birth/death, parents, spouse, family/dynasty, traits, skills, health, profession, Wealth/Influence and relationships remain owned by the existing `Character`. `CharacterRuntimeState` carries only representation continuity: position, lightweight movement/progress, coarse location key and current U10 Hunger/Energy values.
- Tier 1 uses the existing deterministic sex-to-prefab catalog, presenter registry/spawner, centralized AI scheduler and NavMesh adapter. Demotion unregisters Tier 1 execution, stops the NavMesh-only path, releases haul claims and requeues an active manual order while leaving the high-level assignment, canonical inventory and work-group membership intact.
- Tier 2 reuses `Tier2TransferState`, `Tier2Materializer` and `Tier2Runtime`. Extraction and dematerialization complete all tracked jobs first; no second ECS implementation or identity authority exists.
- Tier 3 is one `Tier3CharacterRecord` per named persistent character in a pure C# registry. It stores the same ID plus restoration/runtime continuity and no GameObject/ECS Entity or copied biography/inventory. U14 now advances that record through the same boundary.
- `TierDistancePolicy` provides configurable hysteresis, minimum residency evaluations, deterministic stable-ID order, bounded evaluation and transition budgets, direct Tier1↔Tier3 transitions and controlled retry after transient external presentation changes.
- `LocalSceneCompositionRoot` wires the three adapters and advances Tier 2 from the same explicit `GameTimeAdvance` emitted by the single Tier 1 runtime driver. No per-character `Update` was added.

## U12 Tier 2 DOTS foundation
- `Tier2TransferState` is the explicit U12 boundary from an existing `Character`/stable identity into the supported ECS subset and back. Materialization never calls `StableEntityId.NewId`; duplicate active IDs are rejected before entity creation.
- `Tier2StableIdentity`, `Tier2BiologicalState`, `Tier2Position`, `Tier2Movement` and `Tier2SimulationProgress` are unmanaged, chunk-friendly components totalling 92 bytes per entity before ECS chunk overhead. No managed object, GameObject, Animator or NavMeshAgent is stored in hot components.
- `Tier2Materializer` owns a boundary-side stable-ID index, bulk creation and extraction. ECS `Entity` handles are transient and never become canonical identity.
- `Tier2SimulationSystem` schedules one `[BurstCompile] IJobEntity` across the active query. Movement, processed calendar ticks, processed biological ticks and step counts update in one parallel data-oriented pass; there is no per-agent `Update`.
- `Tier2Runtime` owns a private explicit ECS `World` and consumes only U03 `GameTimeAdvance`. A paused/zero advance schedules no agents and mutates no projected state. The world is intentionally not registered with the default player loop; U13 owns automatic tier coordination.
- Authority is field-scoped: Domain retains durable identity, biography, family, relationships, full health and other unprojected state. While a U12 projection is active, its supported position/movement/progress subset is authoritative until extraction at a completed-job barrier.
- The performance bootstrap measured 1,000 entities and also validates safe creation/update of 10,000 entities. It creates zero Tier 2 GameObjects and contains no MonoBehaviour subtype in `Game.ECS`.

## U11 work-group and job foundation
- `WorkGroupId` is a GUID-backed domain identity distinct from character IDs and session-local `JobId`. `WorkGroupRegistry` owns deterministic group state, optional commander and globally unique membership; every member is referenced by canonical character `StableEntityId`, so presenter destruction/respawn does not alter groups.
- `WorkOrder` supports Move and Haul, Low/Normal/High/Urgent priorities, Queued/Assigned/Active/Completed/Cancelled/Failed lifecycle, stable-ID or world-position targets and explicit claim state. `WorkManager` is the session-owned command/query authority, prevents incompatible duplicate open work and reserves exclusive targets.
- Group commands fan out deterministically into per-member jobs. Eligibility is an explicit Simulation policy hook; dead/unregistered members and already occupied workers are skipped so later profession/health/tier rules can replace the current minimal policy without changing group state.
- U10's scheduler now applies the documented U11 precedence: a carrying phase is non-interruptible; critical Energy at or below 0.10 forces Rest; manual/group work then outranks interruptible autonomous work; otherwise normal utility AI runs. Critical rest requeues unfinished manual work instead of losing it.
- Manual Haul reuses U10 quantity/capacity claims and U09 `ResourceTransferService`. Cancellation/failure releases reservations, and physical stock remains at its current canonical owner. Manual Move reuses the same `ITier1MovementDriver`/NavMesh path as autonomous movement.
- `SelectionProbe` supports Shift additive/toggle multi-selection. The development-only work overlay can create groups, change membership/priority, issue Move/Haul orders, cancel work and inspect status/claims; Ctrl+1…9 assigns groups and 1…9 recalls them. It is a command surface, not canonical state or U27 production UI.
- The implementation adds no per-NPC `Update`. The existing single `Tier1AiRuntimeDriver` advances all agents; automated coverage includes 128 registered characters across 8 groups with deterministic distribution and zero per-agent update ownership.

## U11 GUI click-through follow-up
- `WorldPointerRaycaster` now owns one pre-physics UI-block query for both EventSystem UI and registered development IMGUI regions. The U11 work panel, canonical resource panel and selected-entity debug panel register their full visible rectangles.
- `SelectionProbe` distinguishes a UI-blocked pointer from a world miss: UI clicks leave selection unchanged, while an unblocked empty-world click still clears a non-additive selection. World selection and Shift additive/toggle selection remain unchanged outside UI.
- U11 work buttons use the same Input System primary-click path as world selection. The work overlay resolves its actual button rectangles before dispatching Create, Select group, Add/Remove, priority, Move, Haul and Cancel commands, so UI blocking happens before selection and the command reads the current selected IDs.
- PlayMode regression uses a synthetic Mouse bound to the real `LocalGameplay.inputactions` Pointer map. It verifies selected Character -> Create from selection -> one-member group with the same Character and unchanged selection, full-panel click blocking, every other work button, outside-UI world raycasts and Shift multi-selection.

## U11 RTS right-click Move follow-up
- The existing U05 `Pointer/SecondaryClick` action now enters one `ManualMoveInputController`. It calls the existing `WorldPointerRaycaster` exactly once, accepts only `BuildableGround`, and inherits the same EventSystem/development-IMGUI blocking boundary. Building-placement right-click cancellation is routed through this same secondary-click coordinator so one physical click cannot both cancel placement and issue Move.
- Selected character IDs take precedence; with no selected character, the currently active U11 work group supplies the targets. `ManualMoveCommandService` is a pure Simulation command boundary that fans the click out into per-character U11 `WorkOrder` Move jobs in stable-ID order.
- Multiple executable Tier 1 characters receive deterministic 1.5 m `BALANCE_TBD` grid slots centred on the clicked world position. UI/selection never calls `NavMeshAgent.SetDestination`; U10's existing manual action and `ITier1MovementDriver` bridge remain the only physical execution path.
- `TierManager` exposes read-only `ICharacterTierLookup`. Tier 2/Tier 3 members receive an explicit `TierUnavailable` command result and no job; a click never auto-promotes a representation or mutates U14 state. Existing open manual work is reported as `AlreadyHasWork` instead of creating an incompatible duplicate.
- The debug "Assign group Move to demo point" button remains as a fallback but now uses the same tier-aware formation command service.

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
- U04 `SaveSnapshot` version 1, SQL migrations and Npgsql boundaries are unchanged. U14 runtime continuity is snapshot-ready but is not silently added to the old time-only persistence schema.
- Automatic distance policy still changes representation only. U14 runs its deterministic batch before policy evaluation; births, deaths, events, production and remote jobs remain outside U14.
- No scene asset, prefab, material or catalog data changed. Tier 1 appearance remains the existing deterministic Male/Female mapping; a richer persistent appearance descriptor is deferred.

## Validation status
U11 RTS right-click Move follow-up validation completed on 2026-10-06 with:

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-editmode.log' -runTests -testPlatform EditMode -testCategory U11 -testResults 'C:\serenity_game\Logs\RTS-move-editmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-playmode.log' -runTests -testPlatform PlayMode -testCategory U11 -testResults 'C:\serenity_game\Logs\RTS-move-playmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u05-edit.log' -runTests -testPlatform EditMode -testFilter 'Game.Tests.U05CameraAndInputTests' -testResults 'C:\serenity_game\Logs\RTS-move-u05-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u13-edit.log' -runTests -testPlatform EditMode -testCategory U13 -testResults 'C:\serenity_game\Logs\RTS-move-u13-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u13-play.log' -runTests -testPlatform PlayMode -testCategory U13 -testResults 'C:\serenity_game\Logs\RTS-move-u13-play.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u14-edit.log' -runTests -testPlatform EditMode -testCategory U14 -testResults 'C:\serenity_game\Logs\RTS-move-u14-edit.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-u14-play.log' -runTests -testPlatform PlayMode -testCategory U14 -testResults 'C:\serenity_game\Logs\RTS-move-u14-play.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\RTS-move-build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment`

- focused U11 EditMode passed 12/12 and U11 PlayMode passed 10/10, including two-NPC physical arrival, formation spacing, unchanged StableEntityId, completed jobs, Utility AI return, active WorkGroup fallback, UI click-through blocking and right-click building-placement cancellation without a Move side effect;
- U05 input regression passed 5/5; U13 passed 9/9 EditMode and 2/2 PlayMode; U14 passed 14/14 EditMode and 2/2 PlayMode;
- Windows x64 Mono Development build succeeded with errors=0 and the 2 inherited BuildReport warnings; `Serenity.exe` remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing-reference diagnostic or runtime exception in the successful gates. This earlier U11 follow-up predated U15.

U14 validation completed on 2026-10-06 with:

`& 'C:\serenity_game\Tools\Verify-U14.ps1' -FullRegression -ManagedPostgres -GpuValidation`

- full non-PostgreSQL EditMode passed 207/207 and full PlayMode passed 30/30;
- focused U14 passed 14/14 EditMode and 2/2 headless PlayMode; GPU-enabled U14 PlayMode passed 2/2;
- U13 regression passed 9/9 EditMode and 2/2 PlayMode; U12 passed 11/11 and 2/2; U11 passed 11/11 and 6/6; U10 passed 14/14 and 5/5;
- fresh private loopback SCRAM PostgreSQL passed 25/25; U05A asset/catalog validation passed;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; `Game.Domain.dll`, `Game.Simulation.dll` and `Game.Infrastructure.dll` were rebuilt, and `Serenity.exe` remains 667136 bytes;
- final scale evidence: 1 character × 1 year = 0.106 ms; 1,000 × 365 days = 97.005 ms with 0 measured managed bytes in the timed region and hash `8C41DA6B26E39AB8`; optional 10,000 × 30 days = 163.457 ms with hash `C61F2B8EAE3C22F0`;
- final diagnostics found no compiler error, failed assertion, missing script/reference, runtime exception or shader error in the successful gate logs.

The first post-implementation all-PlayMode gate found that four extra debug labels enlarged the U10/U14 inspector's registered IMGUI blocker enough to cover the complete 640×480 headless test surface together with the existing panels. The U14 fields were compacted into the existing two header lines without changing the blocker rectangle; focused U11 then passed 6/6 and the complete verifier above was rerun successfully from the beginning.

U13 validation completed on 2026-10-06 with:

`& 'C:\serenity_game\Tools\Verify-U13.ps1' -FullRegression -ManagedPostgres -GpuValidation`

- 193/193 full non-PostgreSQL EditMode and 25/25 full PlayMode tests passed;
- focused U13 passed 9/9 EditMode and 2/2 headless PlayMode; GPU-enabled U13 PlayMode also passed 2/2;
- contained U12 regression passed 11/11 EditMode and 2/2 PlayMode;
- a fresh private loopback SCRAM PostgreSQL cluster passed 25/25 integration tests;
- U05A asset/catalog validation passed;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited BuildReport warnings; updated `Game.ECS.dll` and `Game.Simulation.dll` are present and the player entry executable remains 667136 bytes;
- final log scan found no compiler error, failed assertion, missing script/reference diagnostic, runtime exception or shader error.

The first pre-change baseline launch inside the filesystem sandbox hit Unity's known `BuildReportRestService/HttpListener` crash before tests. The identical approved outside-sandbox U12 verifier then passed completely. During U13 validation, the first full PlayMode run exposed a transient legacy presenter-despawn window to the automatic policy; policy retry handling was corrected and the entire final gate above was rerun successfully.

U11 GUI click-through follow-up validation completed on 2026-10-06 with:

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-focused.log' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WorkGroupPlayModeTests' -testResults 'C:\serenity_game\Logs\U11-gui-focused.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-all-playmode.log' -runTests -testPlatform PlayMode -testResults 'C:\serenity_game\Logs\U11-gui-all-playmode.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-core-tests.log' -runTests -testPlatform EditMode -testCategory '!PostgresIntegration' -testResults 'C:\serenity_game\Logs\U11-gui-core-tests.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-gpu.log' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.WorkGroupPlayModeTests.CreateFromSelectionUiClickUsesCurrentSelectionWithoutClickThrough' -testResults 'C:\serenity_game\Logs\U11-gui-gpu.xml'`

`& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\U11-gui-build.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment`

- focused U11 PlayMode passed 6/6;
- full PlayMode passed 28/28;
- full non-PostgreSQL EditMode passed 193/193;
- GPU-enabled primary GUI regression passed 1/1;
- Windows x64 Mono Development build succeeded with errors=0 and 2 inherited warnings; `Game.Presentation.dll` and `Game.Infrastructure.dll` were rebuilt at 2026-10-06 10:12:41;
- Windows Computer Use could not enumerate/attach to the player because its helper failed during setup with `helper_unknown_error`; the exact manual click sequence could not be independently repeated through desktop automation, while the same Pointer InputAction/UI dispatch path passed in both headless and GPU PlayMode.

## Next action
Review U21 using `docs/U21_HANDOFF.md`. Do not start U22 until the review explicitly authorizes it.

U16 final validation command:

`& 'C:\serenity_game\Tools\Verify-U16.ps1' -ProjectPath 'C:\serenity_game' -FullRegression -ManagedPostgres -GpuValidation -StepTimeoutSeconds 900`

The U16A gate passed focused U16A, full EditMode/PlayMode, U05/U08/U09/U10/U11/U15/U16 regressions, GPU PlayMode and Windows build. Windows Computer Use failed during helper initialization/reset, so interactive clicks are not claimed; the equivalent scene paths passed headless and GPU PlayMode.
