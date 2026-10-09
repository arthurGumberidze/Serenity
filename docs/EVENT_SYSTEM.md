# Deterministic Event System (U28)

## Authority flow

`GameClock / typed hook / explicit request -> EventService -> immutable EventCatalog -> canonical outcome adapter -> EventSaveState/history -> U27 projection`.

`EventService` is the only general event scheduler. Domain definitions and runtime values have no Unity references. Presentation reads projections and submits typed choice commands; it never owns pending or resolved state. U18 remains authoritative for health conditions, relationship mutations, death and the U17 succession route.

## Domain model

Definitions use typed IDs for definitions, nodes, choices, instances and owners. Targets are typed persistent references; U28's runtime adapter resolves Character targets through `StableEntityId`, never a Unity instance ID, `GameObject`, Transform or ECS entity handle.

An immutable `EventDefinition` owns category, audience/owner, selector, trigger, integer weight, cooldown, conditions, nodes and invalid-target policy. Nodes own choices; choices own at most one authoritative effect and zero or more weighted transitions. `EventCatalog` rejects duplicate/missing IDs, invalid weights/delays/cooldowns, weight overflow, missing transitions, unreachable required nodes and immediate cycles before simulation starts. `EventService` also requires a registered handler for every effect kind.

An active `EventInstanceState` persists the root/instance ID, current node, targets, owner/audience, offer/due ticks, status, occurrence and node sequence, selected choice, resolved-node records, applied-effect markers and outcome code. Terminal entries move to `EventHistoryEntry`.

## Trigger and selection semantics

- Absolute triggers evaluate once at their exact configured tick.
- Periodic and state-predicate triggers evaluate at `firstTick + n * intervalTicks` while catching up through every due checkpoint.
- Canonical hooks are explicit typed `CanonicalEventHookKind` calls.
- Explicit definitions enter through `Enqueue`; the same target, condition, active-instance and cooldown validation applies.
- Repeating `AdvanceTo` at the current tick is a no-op; moving backwards is rejected.

All eligible definition/target candidates are built before selection and sorted by definition ID and stable target key. One candidate is selected per `owner + target scope` checkpoint. Definition weights are relative positive integer weights. `noEventWeight` is an additional integer bucket; zero means that an eligible scope always selects one candidate. Sums use checked integer arithmetic.

An unresolved active instance blocks another offer of the same definition and scope. This bounds pending growth independently of cooldown.

## Determinism and cooldown

The scheduler reuses U14 `DeterministicKeyedRandom`. Strings use an explicit UTF-16 FNV-style stable hash; runtime `GetHashCode`, collection enumeration order, frame time and mutable random cursors are excluded. Offer samples are keyed by world seed, owner/target candidate key, absolute checkpoint tick and offer stream. Transition samples additionally include definition, node, choice, target, tick and node sequence. Instance IDs are deterministically derived from definition, target key, offer tick and occurrence sequence.

Cooldown starts when an instance is offered. Its key is `definition ID + owner + ordered persistent targets`; the exact `NextEligibleTick` is eligible. Cooldowns persist in canonical ticks and are independent for unrelated targets.

`ComputeStateHash` covers the seed/checkpoint/request sequence, sorted active instances, cooldowns, bounded history and archive summary. Tests prove equal hashes for direct catch-up versus stepping, forward versus reversed registration/target order and before versus after save/load; different seeds can select different weighted outcomes.

## State machine and choices

The lifecycle is `eligible -> pending choice -> waiting (optional delayed transition) -> pending choice -> completed`, with deterministic cancelled, expired and invalid terminal paths.

Player commands validate instance, audience, status, choice, current targets and all effect preconditions and return a typed result. AI filters choices by effect applicability, chooses the highest integer `AiScore`, and breaks ties by choice ID. Transition weights are sampled only after a successful effect. A persistent `node:index` effect marker plus resolved-node marker prevents outcome replay across retries and save/load. A chain receives at most 32 immediate resolutions; exceeding the budget terminates it as `Invalid` with `transition_budget_exceeded`.

Delayed transitions persist their next due tick and are reinserted into catch-up checkpoints. The Stone Age content includes a three-node player family-dispute chain with two first-node choices, a weighted branch, a one-day delay and terminal outcomes; it also includes a standalone player support event and two off-camera AI events.

## Targets and outcome adapters

The domain can represent Character, Dynasty, Building, MilitaryUnit, Battle, Settlement, WorldLocation and Faction references. U28 ships Character selectors for player Characters, player spouses and remote AI Characters. Definitions declare Cancel, Expire or AlternateNode invalid-target behavior; there is no random retarget.

`CanonicalEventEffectExecutor` supports narrative no-op outcomes, U18 relationship events and U18 health conditions. It prevalidates the complete single authoritative mutation, calls `HealthAndFamilyService` exactly once and stores no copied health, relationship, death or succession state.

## Runtime order and performance boundary

Normal composition owns one scheduler and advances it once per committed clock change. The deterministic order is strategic AI, events, health/family progression, fire, then player production. Event effects therefore exist before health catch-up reaches the same target tick. Local/Global and Tier1/2/3 presentation modes do not alter scheduling.

There is no per-event or per-NPC `Update`. Definitions and targets are traversed only at due canonical checkpoints, targets are stably batched, immediate transitions are capped at 32, detailed history is capped at 128 entries, and unresolved duplicate offers are suppressed. U28 includes a 100-target/50-checkpoint sanity test; formal 100/300/500/1000 performance gates remain U29.

## Persistence v14

Snapshot v14 adds `EventSaveState`: world seed, last processed checkpoint, request sequence, active chains, cooldowns, applied/resolved markers, bounded terminal history and archive count/checksum. XML remains strict and capped by the existing codec security/size contract. PostgreSQL still stores opaque XML and only its metadata whitelist changes.

Restore builds and validates a detached candidate before replacing scheduler state. It rejects wrong seeds, duplicate IDs/scopes/cooldowns, missing definitions/nodes/choices, graph/owner/audience/target-shape mismatches, invalid deterministic IDs, terminal active instances and duplicate/bad markers. Canonical services are restored before events, so live target references can be checked without replaying outcomes. Loading v1-v13 explicitly resets event state at the restored calendar tick; it cannot leak the replaced session's events.

Detailed history keeps the newest 128 terminal entries in stable order. Older entries contribute to a deterministic archive count and checksum. This is U28 event history, not the full 2000-year chronicle promised by later work.

## U27 UI flow

The existing UI Toolkit controller adds an Events navigation surface, pending counter, pending list, target names, detail text, choice buttons, recent history and selected-Character event summaries. Choice buttons call `EventService.ResolvePlayerChoice`. The existing UI-region blocker prevents world click-through. Event state participates in the hash-gated refresh and offer/resolution changes feed the bounded notification adapter.

Load reuses the same controller and subscription set. The pending instance and choices come from restored canonical state; notification cursors are rebased so restored history does not appear as new spam. Stable IDs, seed, weights and AI scores are not player-facing.

## Known limitations

- Shipped selectors/effect adapters cover Characters, U18 health and U18 relationships; other typed target/effect kinds require future canonical adapters.
- Definitions are immutable C# content rather than a ScriptableObject authoring pipeline.
- Event history is compact and bounded, not a complete chronicle subsystem.
- No manual native Player interaction was claimed; equivalent headless and Direct3D GPU PlayMode paths cover the player flow.
- PostgreSQL integration was discovered but not executed because `DYNASTYGAME_PG_TEST_DATABASE` was absent.
