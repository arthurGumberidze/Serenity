# Global Map and Local/Global Mode

## Authority and identity

`GlobalMapService` is the engine-independent U25 authority. Locations, routes, settlements, travel orders and the active world mode use stable domain IDs and never reference a Unity scene object. A settlement references the existing canonical Characters and Buildings by `StableEntityId`; an army is the existing U21 `MilitaryUnitId`; a battle is the existing U20/U23 encounter ID. Local and global views therefore project one state rather than copying it.

`WorldLocationId` values are canonical lower-case tokens. `SettlementId` wraps a stable entity ID. The starter graph is fixed data with six nodes, two settlements and eight bidirectional routes. Constructors reject missing endpoints, duplicate locations/routes, invalid settlement links and invalid travel records.

## Transitions

`GameModeTransitionService` commits the mode last. Local→Global exits U22 direct control, converts active U23 physical battles to Abstract, moves other Detailed U21 units to Aggregate and demotes managed Characters to Tier3. Global→settlement rematerializes the selected settlement's residents and friendly presence with the existing TierManager and U21 representation service. Entering a battle transitions that same U23 battle back to Physical. Compensation restores prior tiers, representations, battle mode and TPS ownership on failure.

The additive `GlobalMap` scene contains only disposable camera, schematic route/location visuals and aggregate army/battle markers. `LocalGameplay` stays loaded so its composition root, clock and canonical services continue to run. The bridge disables local camera/input presentation while Global is active and unloads only the map presentation on return.

## Travel and encounters

Movement is a logical route order with origin, destination, departure and arrival ticks. Duration is route data modified through an optional deterministic fixed-point hook. `AdvanceTo` resolves arrivals in `(arrival tick, unit ID)` order, writes the existing U21 abstract location exactly once and clears the travel record. Direct catch-up and stepped advancement therefore converge.

After arrivals, hostile Aggregate units at one location are paired deterministically by stable unit ID. U23 starts the existing canonical Abstract battle; U25 adds no combat formula and no parallel casualty state. Entering its marker rematerializes that same battle ID through U23.

## Off-camera simulation

The global map is advanced by the existing central absolute clock callback. Research, production, weather, fire, health/family and abstract battle services keep their existing U14/U16/U19/U24/U18/U23 ownership and progression order. Global mode has no per-character or per-army `Update`; presentation refresh is throttled and marker count is per aggregate unit/battle, not per soldier.

## Persistence

Snapshot v12 appends `GlobalMapSaveState`: graph ID, mode, active location, locations, routes, settlements, in-progress travels and last processed tick. Restore validates the detached graph before replacing state. U21 remains authoritative for unit roster/location/order, so U25 stores only travel and world relationships. Versions 1–11 decode without a global-map payload. PostgreSQL continues to store strict opaque XML and needs no SQL migration.

## Current limits

U25 deliberately does not implement U26 strategic AI, diplomacy, territory borders, procedural geography, roads/logistics, fog of war or final U27 UX. The map is a readable schematic. One authored LocalGameplay environment represents the selected settlement; building presenters are scoped by settlement membership on entry, while richer per-location terrain/resource streaming remains future scene/content work.
