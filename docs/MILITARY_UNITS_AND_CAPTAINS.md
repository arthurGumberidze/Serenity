# Military Units and Captains

## Scope

U21 supplies the persistent military-unit foundation between canonical Characters and U20 physical combat. A unit can be Detailed or Aggregate, retains stable identity and roster history across representation/tier changes, and can exist without GameObjects. U21 does not implement third-person control, strategic travel/AI, a national army hierarchy, a formation editor or aggregate battle resolution.

## Authority and identity

`MilitaryUnitService` is the sole mutable unit authority. `MilitaryUnitId` wraps a valid `StableEntityId` and never changes. `MilitarySideId` is the persistent owner/side token used to derive the narrow U20 `CombatSideId` during a local encounter. It is not yet a U26 nation model.

The unit owns only unit state:

- ID and owner/side;
- historical roster references;
- captain reference;
- status and Detailed/Aggregate representation;
- logical order and optional abstract location token;
- morale/cohesion summary;
- optional active U20 encounter link;
- deterministic revision marker.

It owns no Character biography, health, inventory, dynasty data, GameObject, Transform, NavMesh path or ECS handle.

## Roster rules

Roster entries reference existing `Character.StableEntityId` values. Active membership is exclusive: one Character cannot be active in two units. Explicit removal changes the entry to Removed instead of deleting history. Re-adding the same Character creates a new joined sequence in that unit's historical slot.

Death and injuries do not mutate roster identity. Dead members stay in history and are excluded from alive/combat-capable queries. Injured and incapacitated members remain active members. U18 capabilities determine injury/incapacity/combat eligibility; the unit does not heal or copy conditions.

## Captain rules

A captain must be:

- an active roster member;
- an existing living Character;
- an adult;
- not incapacitated;
- combat-capable according to U18.

When the current captain becomes invalid, fallback chooses the eligible active member with greatest canonical Influence. Stable-ID ordinal breaks equal-Influence ties. If there is no candidate, the unit becomes Leaderless. If the captain is the monarch, U17 performs succession independently; the unit only selects its own fallback captain.

## Representation and Character tiers

`MilitaryUnitRepresentationMode` has Detailed and Aggregate values. It is not a fourth Character tier.

Detailed transition asks the existing `TierManager` to move living, non-incapacitated managed members to Tier1. Aggregate transition asks the same manager to use an existing Tier2 or Tier3 adapter; the current default is Tier3. Members that are dead, incapacitated or not managed by the current local TierManager remain canonical without a required view. A failed multi-member route is rolled back to each prior tier.

The unit ID, roster, captain, Character objects, health conditions and dynasty links never regenerate during transition. Aggregate therefore means state/representation compression only.

## Physical combat integration

`MilitaryUnitCombatService` is an adapter to U20, not a combat engine. It requires two hostile Detailed units, maps owner IDs to `CombatSideId`, registers candidates, filters them through `CombatService.IsEligible`, creates the existing U20 encounter and links both units to its stable ID.

While any member is active in physical combat, aggregation is rejected and U20 continues to pin participants to Tier1. Reconciliation reads U20 combatant morale and canonical Character/U18 state to update unit summary, fallback captain and terminal status. When the encounter finishes or is cancelled, the link clears and aggregation is allowed again.

U21 performs no aggregate attacks, casualty rolls, auto-resolve or physical/abstract convergence. Those rules belong to U23.

## Orders and location

The minimal order model is Idle, Move, Hold and Engage. Detailed Move delegates to U11 `ManualMoveCommandService` and its existing group spacing/NavMesh boundary. Aggregate orders are stored as intent only. `AbstractLocationId` is an optional opaque token for future U25 integration; it does not introduce global coordinates or route finding.

## Persistence

`SaveSnapshot.CurrentVersion` is 8. `MilitaryUnitSaveState` stores sorted unit IDs and each unit's side, captain, complete roster history, status, representation, abstract location, summaries, order, encounter link and revision. Restore occurs after Character/U18/U20 restore and rejects:

- duplicate unit IDs;
- missing Characters;
- duplicate active membership;
- captain outside the active roster or no longer eligible;
- invalid enum/range/order state;
- Aggregate plus active encounter;
- missing or terminal linked encounter;
- linked encounter with no active unit member.

Versions 1 through 7 remain readable and return no military payload. PostgreSQL stores the v8 XML opaquely and has no new relational schema or realtime role.

## Presentation and scheduling

LocalGameplay has one development-only, click-blocked military overlay. It can create player/enemy units from selected Characters, inspect/select units, assign a captain, remove a member, change representation, issue Move/Hold and start a hostile U20 encounter. It is not final U27 UI.

There is no Update per unit or roster member. Unit mutation is command/event driven, and LocalGameplay reconciles linked encounters from its existing centralized simulation advance.

## Known limitations

- No U22 direct third-person captain control.
- No U23 aggregate battle resolution or strategic casualty formulas.
- No U25 global-map movement; abstract location is a token only.
- No U26 nation AI or full army hierarchy.
- No large formation editor, supply/logistics, recruitment/training, equipment loadout, wages or desertion system.
- Debug UI is intentionally compact and not final UX.
