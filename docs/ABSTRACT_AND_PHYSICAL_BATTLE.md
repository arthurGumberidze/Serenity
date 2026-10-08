# Abstract and Physical Battle

## Purpose and authority

U23 supports one battle moving between local physical combat and off-camera abstract resolution without replacing its identity or realized state. `CombatService` is the canonical mutable authority. `BattleResolutionService` validates U21 military context and coordinates representation changes; it does not own a second battle copy.

`CombatEncounterState` is the cross-mode battle model. Its existing `EncounterId` is exposed as `BattleId` and remains stable for the complete lifetime. The record contains:

- `BattleMode.Physical` or `BattleMode.Abstract`;
- optional Side A/Side B `MilitaryUnitId` links;
- the same canonical Character IDs and combat sides;
- combatant status, target, assigned target, weapon, morale, attack sequence and next attack tick;
- start and last committed calendar ticks;
- attack records, outcome and compact battle history.

No mode owns copied Character, health, dynasty, unit roster, GameObject or ECS identity.

## Physical mode

Physical mode is U20. It uses the existing Tier1 position provider, NavMesh movement boundary, assigned/nearest-hostile targeting and tactical retreat behavior. Active combatants remain pinned to Tier1. Presentation is an adapter and never becomes canonical battle state.

## Abstract mode

Abstract mode runs entirely in engine-independent Simulation. It needs no Transform, GameObject, NavMesh or Unity random source. Both units are U21 Aggregate representations and their canonical members may use the existing Tier2/Tier3 adapters.

The step is exactly one game minute (`TimeSpan.FromMinutes(1)`). Advancement walks from the last committed battle tick to the requested canonical `GameClock` tick in fixed steps. Pause supplies no new clock time, so no abstract progress occurs. Long catch-up and step-by-step calls are state-identical.

Target selection retains a valid assigned target, then a valid current target. Otherwise it orders valid hostiles by `StableEntityId`; Side A attacks from the low-ID formation edge and Side B from the high-ID edge. This is the position-free counterpart of the controlled physical test formation and cannot depend on collection enumeration.

An abstract attack occurs only when `stepTick >= NextAttackTick`. Both modes share `CombatRules`, `CombatWeaponCatalog`, `CombatStatQuery`, weapon attack intervals, impact calculation, injury mapping, morale loss, break threshold and retreat duration. The attack increments the existing sequence and schedules the next absolute cooldown tick. Switching modes cannot grant an extra attack.

U18 applies every injury and owns incapacity/death; fatal resolution continues into U17 succession. U21 reconciliation observes captain death and chooses its existing deterministic fallback or Leaderless state. Abstract morale uses the U20 casualty penalty. Broken combatants become Retreating and, after the shared fixed retreat duration, Disengaged. Outcome uses the U20 vocabulary.

## Transition contract

Physical→Abstract preserves battle/unit/Character IDs, health and conditions, morale, combatant/target/retreat state, attack sequence, next attack time, histories, outcome and last committed tick. The battle changes mode, U20's Tier1 pin is released, then both U21 units transition to Aggregate. A mode-history event is committed only after both succeed.

Abstract→Physical transitions both units to Detailed, validates local presence for every active fighter, then changes the existing battle mode. U13 rematerialization uses captured canonical runtime state in stable member order. Living, non-incapacitated survivors return; dead members do not, and incapacitated members remain canonical roster members without an active combat presentation.

The transition service rolls back both unit representations and the battle mode after partial failure. A terminal battle rejects further transitions. Repeating mode changes preserves all realized state. If any participant is currently owned by U22 third-person control, Physical→Abstract returns `BlockedByDirectControl`; after TPS exit the same request may proceed.

The existing tier runtime state retains each Character's logical `WorldPosition`, so return does not reset the group to a new formation. U23 adds no new spawn/formation system. Exact state continuity is guaranteed; centimetre-level future Transform/NavMesh path continuity is not.

## History and persistence

The compact ordered history records Started, ModeChanged, SideBroken, CaptainDeath and Finished events with sequence and calendar tick. Detailed attack/injury evidence remains in the existing attack history; abstract ticks are not individually persisted.

Snapshot v10 adds mode, optional side unit IDs and battle history to the existing combat XML. U21 unit links and U22 control state remain their own canonical sections. Restore validates that an active Physical battle has Detailed units and an active Abstract battle has Aggregate units, then resumes at the stored tick without replay or reroll. Versions 1–9 remain readable and default missing battle fields to legacy Physical state. PostgreSQL continues storing the validated XML opaquely and needs no schema migration.

## Determinism and convergence boundary

Exact guarantees cover abstract advancement, catch-up, collection-order independence, stable IDs, all state transfers, health, cooldown/sequence and deterministic history markers. Multiple battles are sorted by battle ID and cannot leak participants or mutable state.

Physical logical outcomes are reproducible in a controlled test layout. Exact Transform/NavMesh paths are not promised. A cross-mode route preserves everything already realized, but future casualty identity need not match the counterfactual abstract-only route. Convergence is therefore measured over distributions.

The U23 gate runs 100 paired scenarios from identical canonical starts. The observed result was 0.0% winner delta, mean casualty-count delta 1 and mean duration delta 3 game minutes. The gate also verifies route completion and identity continuity across Physical-only, Abstract-only and mixed paths.

## Performance and validation

Abstract work is centralized and allocates no GameObjects/NavMesh agents. The 250v250 stress run completed in 3605.191 ms, measured 0 current-thread managed bytes inside the timed section and produced hash `D5DD44B2E9D730AF`. Direct catch-up/reversed collection order produced `52A0A55A95EB4EB6`; ten isolated battles produced `6E9C1A83D3AABCC0`.

The LocalGameplay development panels can start Physical or Abstract unit battles, display battle ID/mode/tick/unit links/counts/hash, request mode transitions and advance one canonical game-minute step. They are disposable debug surfaces, use the existing pointer blocker and are not U27 final UI.

Known scope limits: no U24 destruction/fire/capture, U25 global map, U26 strategic AI, siege, reinforcements, prisoners, loot, supply, diplomacy, world-map retreat, naval/cavalry expansion, projectile expansion or tactical-AI overhaul.
