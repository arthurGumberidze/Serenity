# Health and Family Events

## Purpose and authority

U18 adds a narrow deterministic foundation for injuries, diseases, temporary/permanent conditions, family relationship events, pregnancy and birth. `Character` remains the only canonical person. `HealthAndFamilyService` owns condition/event/pregnancy progress by stable IDs and mutates the existing `CharacterRegistry`; it never stores GameObject, ECS entity or PostgreSQL references.

## Health conditions

`HealthConditionDefinition` is immutable data: ID, type, initial severity, duration, effective modifiers, worsening chance, permanent-outcome chance and mortality. `HealthConditionInstanceState` stores its deterministic instance ID, target, calendar checkpoints, severity, lifecycle state, progression count and death marker. The initial catalog contains nine definitions: `minor_cut`, `sprain`, `broken_bone`, `serious_wound`, `mild_fever`, `infection`, `severe_fever`, `weakened` and `lasting_limp`.

Temporary conditions move through Active/Improving/Worsening/Critical to Recovered, Permanent or Fatal on absolute game ticks. Permanent conditions stay active. Every random decision uses U14's keyed deterministic stream with world seed, target ID, definition/stream and absolute step. Collections are sorted by stable identity, so traversal and tier route cannot reroll results.

Effective capability is base character eligibility multiplied by every active condition. The reusable query exposes work, movement, research and future combat modifiers, fertility eligibility and incapacitation. Children, dead characters and incapacitated characters cannot perform ordinary work or research. U10 refuses/cancels ordinary AI actions, U15 scales or blocks production work, and U16 scales or blocks research through this query. Temporary effects do not rewrite base attributes.

Fatal resolution calls `SuccessionService.ProcessDeath`; U18 contains no parallel death or succession implementation. The condition's death marker plus U17's processed-monarch marker make repeated advancement exactly once.

## Family events

The event slice contains four immutable definitions: strengthened/weakened marriage bond, family dispute and family support. An instance ID is derived from world seed, definition, both character IDs and tick. It modifies the existing bounded symmetric `Character` relationship once and persists its applied marker. It is deliberately not the general U28 narrative/event system.

## Pregnancy and birth

The MVP requires living mutually married female/male adults, mother age 16 through 50, no concurrent pregnancy and health capability that permits fertility. Conception is a debug/test command, not autonomous romance AI. Default duration is one configured value: 750 biological days; at the project multiplier of 400 this is 1.875 calendar days, or 45 real minutes at x1.

At the due biological tick, the service creates a real canonical `Character`. Child sex/name and the 128-bit ID are deterministic. `StableEntityId.FromDeterministicHash` derives simulation-created IDs without changing `NewId()` semantics for manual entities. The key includes world seed, both parents and conception/due data. The child receives biological parent links, due tick as birth tick, averaged initial attributes, family/dynasty references and is registered immediately. If a parent belongs to the active ruling dynasty, the child is added to that existing dynasty and U17 queries see it without rebuilding.

The newborn begins as a Tier3-compatible domain record and needs no GameObject. `ChildBorn` lets Infrastructure seed the existing Tier3 adapter and population view. Adult capability begins at 16 completed biological years; no childhood simulation or visual growth is added.

## Tier and performance behavior

One scene-level simulation callback advances all health, events and pregnancies. Tier1, Tier2 and Tier3 all read the same canonical service; tier transitions carry no copied health authority. Disease recovery/death and pregnancy/birth therefore work off camera. Automated T3-only, T3-T2-T3 and T3-T1-T3 routes converge on the same state hash, child ID and dynasty membership.

There is no per-character, per-condition or per-pregnancy `Update`. Advancement is batched and stable-ID ordered. The 20-person/three-biological-year engine-independent test completed in about 0.019 seconds on the final validation host; this is a basic observation, not the U29 performance gate.

## Persistence

`SaveSnapshot.CurrentVersion` is 5. The new `healthFamily` payload stores world seed/checkpoints, all condition lifecycle fields, processed events and pregnancy status/child IDs. Canonical children and relationship changes are already included in the U17 character/dynasty payload. Restore validates referenced characters, definitions, unique IDs, one active pregnancy per mother and deterministic world seed before commit. Versions 1 through 4 remain readable with no health payload.

PostgreSQL remains an opaque XML snapshot store; no relational schema or realtime query was added. Its metadata gate now accepts every supported payload version v1-v5 before the strict XML codec validates the body.

## Debug UI and limits

The LocalGameplay development panel shows age/life, active conditions, severity/progress, work modifier/incapacity, pregnancy and spouse/parent/child links. It can add representative injuries/disease, recover temporary conditions and start pregnancy for the selected character's valid spouse. The existing U16A pointer blocker covers the whole panel.

Not implemented: natural-age mortality, transmission/epidemics, genetics/heredity, fertility probabilities, childbirth complications, treatment economy/doctors/hospitals, romance/remarriage/adoption, production UI, combat damage or the U28 general event engine.
