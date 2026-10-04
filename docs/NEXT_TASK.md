# NEXT_TASK.md

## Active task: U07 — GameObject presentation Tier 1

### Prerequisites
U05, U05A and U06 are DONE. U05 supplies the local scene/input boundary, U05A supplies licensed placeholder wrappers, and U06 supplies canonical Character Domain state.

### Goal
Implement Tier 1 GameObject presentation that binds a visible/animated character view to an existing U06 `Character` by `StableEntityId`. The view must not own or reroll canonical character state.

### Scope boundary
U07 has not started. Read `docs/U06_HANDOFF.md`, D-016 and the U06 architecture section before implementation. Do not move `CharacterState`, age, family, skills, health or relationship ownership into a MonoBehaviour, prefab, Animator or vendor hierarchy. AI, needs, work scheduling, combat, dynasty gameplay, persistence schema expansion and tier switching remain later tasks.

### Relevant handoffs
Use `docs/U05A_HANDOFF.md` for wrapper/asset limitations and `docs/U06_HANDOFF.md` for the Character contract. Hodaart Characters 01/02 are placeholder presentation candidates only. Bind and unbind them without changing the domain identity or depending on Unity instance IDs.
