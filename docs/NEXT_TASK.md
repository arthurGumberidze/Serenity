# NEXT_TASK.md

## Active task: U10 — Utility AI Tier 1

### Prerequisites
U06, U07 and U09 are DONE. U06 owns the persistent character aggregate, U07 owns disposable Tier 1 presentation, and U09 owns canonical resources/inventories plus atomic construction funding.

### Goal
Implement the first scalable Utility AI scheduling foundation for Tier 1 characters so that 100 NPCs can evaluate needs and work without one Update-heavy script per NPC.

### Scope boundary
U10 has not started. Read `docs/U09_HANDOFF.md`, `docs/U07_HANDOFF.md`, the U06 character boundary and the Utility AI sections of the FRS before implementation. Preserve the central scheduler/ticked-system rule, canonical Domain state and StableEntityId across future tiers. Do not implement U11 work groups/orders, U12 DOTS Tier 2, U15 production chains, U20 combat or U27 full UI.

### Relevant handoffs
AI may query U09 read models and submit explicit transfer/construction actions through Simulation services. It must not mutate presenter fields, duplicate resource quantities, make Unity object identity persistent or add per-character Update loops.
