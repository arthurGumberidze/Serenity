# U22 third-person control

## Ownership model

`PlayerControlState` is an engine-independent value model with two modes: `Rts` and `ThirdPerson`. The only persistent controlled identity is `Character.StableEntityId`; cameras, presenters, `NavMeshAgent` instances and input callbacks are never canonical state.

`PlayerControlService` owns the handoff. A valid target must exist, be alive, be an adult, be mobile and not incapacitated, be managed by `TierManager`, and be one of:

- the current player monarch;
- the current monarch's first eligible succession candidate under the existing U17 policy;
- the captain of a player-side U21 military unit.

An aggregate U21 unit member is rejected until represented in a compatible detailed form. Arbitrary allies/enemies, children, dead or incapacitated Characters are rejected. Exactly one selected eligible presenter is required by the LocalGameplay adapter.

## RTS to TPS handoff

Press `T` with exactly one eligible Character selected. Building placement is cancelled, the existing Character is materialized/pinned as Tier1 if needed, and its centralized Utility AI action is cancelled through the existing action contract. For a U11 manual Move action, cancellation requeues the same order as `Assigned`; group membership and order identity remain intact. Research assignment remains intact, but the worker is not Active while controlled. Production/other Utility actions stop contributing and are reconsidered after control returns.

The existing `CharacterPresenter` and its existing `NavMeshAgent` are reused. `NavMeshMovementDriver` switches from `Autonomous` to `DirectPlayer`, resets the autonomous path and ignores later autonomous destinations. `CombatService` continues to own U20 encounter/target/cooldown/injury/morale state but suppresses its movement requests for that Character. Therefore Utility AI, manual orders, military movement and U20 may retain logical state, while exactly one authority moves the Transform.

Direct movement is camera-relative. WASD supplies a desired direction to `NavMeshAgent.Move`; Shift multiplies the walk speed by 1.65. No `CharacterController`, Rigidbody motor or duplicate transform authority is added. The agent remains on the baked NavMesh and its existing presentation drives Idle/Walk/Run from actual requested speed.

One reusable `CinemachineCamera` with `CinemachineThirdPersonFollow` is created once by the scene controller. Its target is parented to the same presenter. Entering TPS swaps priorities and disables the RTS camera controller without changing the RTS rig transform. Exiting restores the previous RTS controller and priority, so the prior RTS camera context is retained. Camera state is presentation-only and is not saved.

## TPS to RTS handoff

Press `T` again or `Escape`. Direct movement stops, the agent returns to `Autonomous`, the TPS camera releases its target, RTS camera/input maps are restored and the AI scheduler is made immediately eligible to think again. A still-valid requeued order can resume through the normal Utility AI choice; no destination is replayed twice and no Character is respawned.

Death or incapacitation automatically exits to RTS. Death remains U18/U17 authority, including monarch succession. Direct control blocks Tier2/Tier3 demotion; after exit the normal tier rules apply.

## Input, time and cursor

`LocalGameplay.inputactions` has a global `Mode/ToggleControl` action on `T` and a `ThirdPerson` map with `Move` (WASD), `Look` (pointer delta), `Run` (Shift) and `Exit` (Escape). In TPS, Camera/Pointer/Building maps are disabled, so RTS pan, selection, RMB Move/Attack and building commands cannot leak. `Time` and `Mode` remain enabled.

Pause stops direct movement, AI, U20 and the game clock; mouse look may continue. Existing speed x1/x2/x3/x5/x10 remains global, and direct movement scales by the same current multiplier. TPS locks and hides the cursor outside batch mode; returning to RTS unlocks and shows it. Tests assert action-map state rather than OS cursor state.

## Combat, units and persistence

U22 adds no new damage or attack authority. A controlled U20 combatant remains in the same encounter and U20 continues target, cooldown, attack, injury, morale and outcome processing; only its locomotion requests are suppressed. There is no TPS attack input in this foundation.

U21 unit ID, roster history, captain identity, representation and orders remain untouched. A directly controlled captain is the same canonical roster member. Group or military movement may change logical order state but cannot move that Character until TPS exits.

`SaveSnapshot` version 9 appends `PlayerControlSaveState` containing mode and optional controlled stable ID. Versions 1 through 8 remain readable. Restore enters TPS only if the Character is still eligible and materializable; otherwise it returns safely to RTS with `InvalidAfterLoad`. No camera transform or Unity reference is serialized. PostgreSQL remains an opaque XML store and needs no schema migration.

## Diagnostics and determinism boundary

The click-blocked development overlay shows mode, Character/name/short ID, life/incapacity, movement owner, U21 unit/captain and the assigned/suspended U11 order. It is not the U27 HUD.

TPS transform motion is input- and frame-delta-driven. The supported determinism statement is: identical initial canonical state and equivalent timestamped player input produce the same logical ownership/state transitions and stable identities. Bit-identical floating-point camera or NavMesh trajectories are not claimed. Camera state remains disposable.

## Known limits

- No manual TPS attack, aiming, jump, crouch, climbing, IK or action-RPG combat.
- No exact TPS/RTS camera transform in save data; the in-session RTS rig is restored unchanged.
- Camera collision uses the basic Cinemachine third-person follow behavior without a separately tuned deocclusion rig.
- U11 work orders are session state in the existing architecture; U22 preserves/requeues them in-session but does not independently make them durable.
- Final UI, control remapping and accessibility polish remain later work.
