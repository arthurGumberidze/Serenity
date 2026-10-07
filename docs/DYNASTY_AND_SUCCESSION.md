# Dynasty and Succession

## Purpose

U17 provides the canonical ruling-dynasty and succession foundation. It determines a new player ruler from existing persistent characters when the monarch dies, or ends the run through dynasty extinction. It deliberately does not implement reproduction, pregnancy, diseases, injuries, random mortality, political laws UI or chronicles UI.

## Domain model

`Character` remains the canonical person entity. Identity, sex, biological birth/death ticks, parents, spouse, family/dynasty hooks, health, traits, skills and relationships stay on that aggregate. `DynastyState` references characters only by `StableEntityId` and owns:

- dynasty stable ID and debug name;
- founder ID;
- optional current monarch ID, present exactly while Active;
- unique known member IDs;
- succession rule identifier;
- Active or Extinct status;
- processed monarch-death IDs;
- ordered succession history.

`PlayerRealmState.CurrentRulerId` is the engine-independent player-control binding. `GameRunState` supplies Running or GameOver and `GameOverReason.DynastyExtinction`. None of these models depends on Unity, presentation, ECS or PostgreSQL.

## Family query

`FamilyTreeQuery` derives relationships from canonical U06 links. Parents come from the child's `ParentLink` values. Children are reverse queries over those links. Siblings share at least one referenced parent. Descendants are traversed from derived children. Current spouse is read from the symmetric registry-managed spouse link.

Saved data rejects missing parent/spouse/dynasty-member references, duplicate IDs, self-parent/self-spouse links, asymmetric spouses and cyclic parent graphs. Live queries never dereference a missing reference without a controlled validation error.

## Succession rule

The current rule is `agnatic_primogeniture_v1`, matching the FRS default male-only inheritance law.

Eligibility requires that the candidate:

1. exists in `CharacterRegistry`;
2. is a known member of the ruling dynasty and has the same canonical dynasty ID;
3. is Alive in authoritative Character state;
4. is not the deceased monarch;
5. is Male under this rule.

Candidate order is:

1. current monarch descendant branches, oldest child branch first;
2. siblings of the monarch and their descendant branches;
3. ancestors and their collateral branches;
4. remaining known dynasty members by shortest family-graph distance.

At every branch, lower biological birth tick means older and comes first. Equal birth ticks use the canonical `StableEntityId` string as the final tie-break. Traversal continues through an ineligible or dead person, so the living male descendants of a dead elder heir retain that elder branch's priority. Collections are normalized; registration and enumeration order do not affect the result.

## Death and exactly-once processing

`SuccessionService.ProcessDeath(characterId, biologicalDeathTick, calendarTick)` is the reusable U17 death hook. It uses `Character.MarkDead` for a living character. A repeated call for a character already dead leaves the original death tick intact. Non-monarch deaths change no dynasty authority.

For the current monarch, the service resolves candidates once, records the deceased ID in processed markers, appends one history entry and either transfers authority or creates extinction. History stores sequence number, previous monarch, optional new monarch, calendar tick and reason. Markers and history are serialized, so repeated simulation ticks and save/load cannot repeat an election.

## Ruler transfer and extinction

When an heir exists, `DynastyState.CurrentMonarchId` and `PlayerRealmState.CurrentRulerId` both change to that existing character ID. The heir is not cloned, regenerated or materialized solely because of succession.

When no heir exists, current monarch and player ruler become absent, dynasty status becomes Extinct, and the run becomes GameOver with `DynastyExtinction`. The system never picks a random non-dynasty NPC and never automatically resurrects an extinct dynasty.

## Tier behavior

Eligibility reads only authoritative Character data. Tier1 GameObjects, Tier2 entities and Tier3 records are representation details. A Tier2 or Tier3 character remains eligible and can become ruler immediately without presentation. Tier transitions preserve stable identity and therefore cannot change candidate order. Tests compare Tier3-only, Tier3 to Tier2 to Tier3, and Tier3 to Tier1 to Tier3 routes against the same family and death tick.

## Persistence

Snapshot version 4 stores complete detached Character states, dynasty state/history/markers, player ruler and game-run state. Restore validates the entire family/dynasty graph before commit. Existing active registries retain their canonical Character instances when the saved identity set matches; fresh registries restore those same IDs. Versions 1, 2 and 3 remain readable and return no dynasty payload. PostgreSQL stores the same strict XML bytes and has no U17 schema migration.

## LocalGameplay debug setup

The development scene uses four canonical characters: visible spouses Aren and Mira, plus domain-only sons Cale and Doran. Aren begins as monarch. The stable demo IDs and equal birth ticks make the ID tie-break observable and repeatable. The overlay shows dynasty/monarch/spouse/children/heir/top candidates/status/history and exposes debug-only Kill current monarch and Kill selected character commands. Its rectangle participates in the existing world-pointer blocker.

## Known limitations

- The development sons are domain-only until explicitly materialized by tier APIs; this is intentional for off-camera succession coverage.
- Only the base male-only rule exists. Appointed heirs, disinheritance, legitimization, adoption policy and changeable laws remain later work.
- Marriage history, divorce/remarriage policy, reproduction and childbirth are not implemented.
- U17 supplies an explicit death hook but no disease, injury, old-age or random mortality scheduler; those belong to U18 and later tasks.
- There is no production dynasty tree, reign summary or Game Over screen; U27 owns gameplay UI.
