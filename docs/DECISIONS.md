# DECISIONS.md

## D-001 Engine
Unity 6.3 LTS (6000.3), C#, Windows PC. URP for MVP.

## D-002 Hybrid simulation
Tier1 = permanent important/nearby NPC domain objects + GameObject presentation as needed.
Tier2 = Entities/DOTS simplified agents in active city.
Tier3 = statistical city population stored as aggregate data.
Permanent named persons preserve StableEntityId across tiers.

## D-003 Time
Normal day at x1 = 24 real minutes. Biological age uses separate accelerated clock. Pregnancy target ~30-60 real minutes at x1; childhood several real hours; a generation roughly 8-12 real hours. Death from old age always exists.

## D-004 Off-camera simulation
World sections collapse to aggregate snapshots. Snapshots preserve composition, health, inventories/supply, morale, orders, production, relevant timers and RNG state/seed. Returning reconstructs detail without rerolling.

## D-005 Wealth/Influence
No full salary/market/inflation simulation. Wealth = property + business income + state rewards. Influence = office + dynasty + wealth + achievements + events.

## D-006 Non-negotiable game pillars
War; building; third-person control of monarch/captain; dynasty/family/inheritance; weather gameplay effects.

## D-007 Content architecture
ScriptableObject for immutable/authoring definitions. Runtime state is serializable models/DTOs with explicit save schema version.

## D-008 User-approved Editor version (2026-10-03)
The user explicitly requested keeping the installed Unity version and continuing U00. Target Unity 6000.6.4f1 (12bfff696524), overriding the original 6000.3 LTS requirement. Use the bundled URP blank template (17.2.1), URP 17.6.0, Windows x64 Mono Development builds. Do not upgrade the Editor automatically. No gameplay or U01 architecture is included.
