# NEXT_TASK.md

## Active task: U09 — Ресурсы, инвентари и склады

### Prerequisites
U06 and U08 are DONE. U08 provides canonical building identity/state, logical grid occupancy, data-driven building definitions and the current LocalGameplay placement flow.

### Goal
Implement resources, inventories and storage without duplicating canonical quantities. Connect construction costs to actual resources while preserving U08 building identity and occupancy.

### Scope boundary
U09 has not started. Read `docs/U08_HANDOFF.md`, the U06 character state boundary, the U04 persistence boundary and the FRS resource/logistics requirements before implementation. Do not implement worker AI (U10/U11), production chains (U15), combat/destruction (U20/U24), global-map streaming (U25) or full gameplay UI (U27).

### Relevant handoffs
Keep resources canonical in Domain/Simulation data and avoid representing one quantity simultaneously in world piles, inventories and storage. The current Storage Basket is only U08 building presentation; U09 must add inventory/storage behavior without moving canonical data into its prefab or presenter.
