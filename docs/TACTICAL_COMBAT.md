# Tactical RTS Combat (U20)

## Scope

U20 supplies the reusable local, physical melee-combat slice required by FR-COMBAT-001 through FR-COMBAT-005. It supports two hostile sides, individual and group attack orders, deterministic target selection, weapon timing, U18 injuries/death, morale, retreat and encounter completion. It intentionally does not implement U21 armies/captains, U23 abstract combat, projectiles, cover/elevation bonuses, friendly fire, building damage or final U27 UI.

## Authority and model

`CombatService` is the sole mutable battle authority. Every participant is referenced by the existing `Character.StableEntityId`; encounters never copy a Character, GameObject, ECS entity or inventory. `CombatEncounterState` owns:

- a persistent encounter ID, two `CombatSideId` values and lifecycle/outcome;
- stable-ID-sorted `CombatantCombatState` entries;
- assigned target, weapon profile, cooldown/sequence, morale, retreat point and current combat state;
- an append-only deterministic attack record used for auditing, save/load and tests.

`CombatSideId` is a narrow tactical allegiance value, not a U21 persistent army or strategic faction. The local demo registers player/enemy sides. Encounter creation requires distinct sides, at least one eligible participant on each side, no duplicate stable ID and no participant already active in another encounter.

## Eligibility and tiers

A participant must exist, be alive, be an adult, not be incapacitated by U18, have a positive effective combat capability and, when a tier lookup is supplied, currently be Tier 1. Children, dead characters, incapacitated characters and remote representations are rejected.

Active combat is physical and local. `CombatService` implements the tier-transition guard used by `TierManager`: an active participant cannot demote to Tier 2 or Tier 3. It also implements the external-activity boundary used by the centralized Tier 1 scheduler, suspending ordinary need/work decisions while combat owns the character. The pin is released when the encounter finishes or is cancelled. There is no combat `Update` per agent.

## Stats and weapons

`CombatStatQuery` derives attack/defence from the canonical Character Strength, combat skill and U18 combat capability modifier. U20 does not add a second attribute or skill store.

`CombatWeaponCatalog` contains immutable profiles for `unarmed`, `stone_axe` and `stone_spear`. A profile supplies range, game-time attack interval, attack power and minimum injury. These are data objects selected by ID; mutable battle/save state stores only the ID. Current balance values are an MVP baseline, not final content balance.

## Targeting, movement and formation spacing

An explicit valid assigned target wins. Otherwise `CombatTargetingService` chooses the nearest active enemy by squared world distance, then uses canonical stable-ID text order as the tie-break. Input collection order cannot change the result.

The engine-free service asks `ICombatPositionProvider` for positions and movement. `Tier1CombatPositionProvider` is the Unity adapter over the existing Tier 1/NavMesh movement bridge. Combatants first approach a deterministic half-metre side slot while far away, then directly close on their selected opponent once near engagement range. This gives readable small-group spacing without adding a formation editor or pathfinding authority.

## Attack and injury resolution

The centralized simulation advance processes active combatants in stable-ID order. A combatant must be active, have a valid enemy target, be in weapon range and have reached its absolute game-tick cooldown. A committed attack increments its persistent sequence and schedules the next attack from the profile interval, preventing same-tick duplicates.

U20 uses deterministic always-hit melee. Impact is attack power minus half the defender defence. Fixed thresholds map impact to the U18 ladder (`minor_cut`, `sprain`, `broken_bone`, `serious_wound`), bounded by the weapon's minimum injury. `HealthAndFamilyService.ApplyCombatInjury` is the only injury/death mutation path. It escalates an already-present combat injury rather than creating unbounded duplicate conditions and delegates fatal resolution to the existing U17 `SuccessionService`. Dead or already-resolved targets are no-ops, so a monarch death and succession occur exactly once.

This deliberate U20 rule establishes deterministic losses. Accuracy, blocks/dodges, armour depth, body-part ballistics, projectiles and friendly fire remain later or explicitly deferred scope.

## Morale, retreat and completion

Morale is a bounded integer from 0 to 1000. A same-side casualty costs 260; a newly severe injury costs 350. Falling below 300 changes an active combatant to Breaking and then Retreating. Player code may also order a retreat explicitly. A retreating combatant stops attacking, moves toward its deterministic retreat point and becomes Disengaged on arrival or after the 15-game-minute timeout.

After casualties and retreats are refreshed, the encounter resolves once:

- Side A victory when Side B has no active/breaking combatant and no pending retreat;
- Side B victory by the symmetric rule;
- mutual disengagement when neither side can continue;
- cancellation only through the explicit administrative command.

Finished encounters never advance attacks again. Outcome, attack history and participant terminal states round-trip through persistence.

## Input, enemy behavior and presentation

`ManualMoveInputController` retains one pointer raycast and the existing UI blocker. Right-click on a hostile Character routes the selected characters or active work group to `CombatCommandService.IssueAttack`; right-click on terrain remains the existing U11 Move command. Building-placement cancellation remains unchanged.

Enemy tactical behavior is service-owned: registered hostile combatants acquire the same deterministic nearest target, approach, attack, react to U18 incapacity/death, lose morale and retreat. It is not a separate MonoBehaviour AI loop.

`CombatDebugOverlay` is a disposable, click-blocked development read/command surface. It shows encounter status/outcome/hash and selected combatant side/state/target/stats/morale/cooldown/recent injury/conditions, and exposes demo start/stop controls. LocalGameplay can materialize four allied and four enemy Tier 1 characters for a deterministic 4v4 demo. Presentation is not saved and owns no combat truth.

## Determinism boundary

The logical boundary is deterministic for the same canonical inputs: stable ordering, target choice, attack sequences, injury requests, morale/state transitions, outcome and `ComputeStateHash`. It does not promise bit-identical NavMesh transform trajectories or frame rendering. Physical tests use actual Tier 1 presenters/NavMesh to prove the local encounter can close and finish, while engine-free tests prove logical collection-order independence.

## Persistence

`SaveSnapshot.CurrentVersion` is 7. `CombatSaveState` stores the detached encounter set, combatants, cooldown/sequence, targets, retreat data, attack history, outcome and last processed tick. Restore validates identities, sides, weapons, uniqueness and references before replacing live state. `SaveCoordinator` restores combat after Character/dynasty and U18 health state.

Versions 1 through 6 remain readable with no combat section. PostgreSQL continues to store the strict validated XML as an opaque payload, accepts version 7 and requires no relational migration or realtime authority.

## Known limitations

- Two sides per encounter only; no persistent armies, captains, strategic factions or abstract units.
- Local Tier 1 physical melee only; Tier 2/3 and off-camera auto-resolve are U21/U23.
- Always-hit deterministic melee; no ranged projectiles, friendly fire, cover, elevation, shields/armour depth, stamina, durability or body-part ballistics.
- Formation behavior is fixed approach spacing, not an editable formation system or advanced flanking.
- No building destruction/fire/capture, loot, prisoners or executions.
- Weapons have logical profiles but no dedicated equipped-weapon presentation in U20.
- The IMGUI combat panel is development tooling; production controls belong to U27.
