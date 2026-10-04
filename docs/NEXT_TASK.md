# NEXT_TASK.md

## Active task: U06 — Domain-модель персонажа Tier 1

### Prerequisites
U03 and U04 are DONE. U05A is also DONE and supplies asset-readiness candidates only; it does not define character gameplay state.

### Goal
Implement the pure/testable Tier 1 Character Domain model and its invariants according to `docs/TASK_GRAPH.md` and the relevant FRS sections. The acceptance target includes EditMode coverage for the model and presentation of character data in the NPC card without moving canonical state into a MonoBehaviour or vendor prefab.

### Scope boundary
U06 has not started. Before implementation, read the current project state, decisions, architecture, U05A handoff and Character Domain requirements. Preserve StableEntityId and persistence boundaries. Do not implement AI, needs scheduling, professions, building gameplay, combat, dynasty/inheritance, aging/pregnancy, Tier switching or mass-NPC update loops unless the actual U06 specification explicitly requires a narrow supporting contract.

### U05A handoff
Read `docs/U05A_HANDOFF.md`, `docs/ASSET_REGISTRY.md`, D-015 and the U05A architecture section. Hodaart Characters 01/02 and the Tier 2 capsule are presentation/readiness placeholders only. Canonical character state must not depend on their vendor hierarchy, Animator, Unity instance IDs or scene lifetime.
