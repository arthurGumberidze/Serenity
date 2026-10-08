# Strategic AI (U26)

## Authority map

| State | Canonical owner | AI access |
|---|---|---|
| Characters, life and capability | `CharacterRegistry` + `HealthAndFamilyService` | Stable IDs and capability queries only |
| Dynasty and ruler | one existing `DynastyState`/`SuccessionService` per state | AI-owned scope |
| Research | existing `ResearchService` | separate mutable instance per state |
| Resources and production | existing inventory, world-pile and `ProductionService` types | separate mutable instance per state |
| Buildings and occupancy | shared `BuildingRegistry`/placement/damage services | commands carry owner side and deterministic ID |
| Military | shared `MilitaryUnitService` | U21 commands only |
| Locations and settlements | shared `GlobalMapService` | U25 travel/synchronization only |
| Battle | shared U20/U23 encounter and resolution services | observes result; never selects winner |

`AiFactionRuntime` is only a scope bundle. It does not duplicate any row above.

## Normal bootstrap

`AiRuntimeBootstrap.CreateStarterEnemy` binds the existing `enemy_settlement` to six seed-derived canonical adults, House Varek, owner-side inventories, natural production sites and independent Stone Age research. Residents start in Tier3 at a remote runtime position and the U25 settlement stores their stable IDs. Initial quantities and sources are finite; later construction uses normal funding and occupancy rules.

## Decision loop

`AiStrategicScheduler` owns one absolute checkpoint per faction. Due factions are processed in `AiStateId` order. Shared travel/battle services advance once per checkpoint; faction production/research then advance with only alive, capable, present non-military workers. Reconciliation applies succession, removes spent armies, refreshes controlled settlements and processes finished expedition results. Finally, stable priorities choose one command.

The baseline policy first unlocks and funds a shelter, then forms the minimum army, assigns the canonical influence/stable-ID captain, selects the nearest hostile settlement by route duration and issues one U25 route leg at a time. U25 starts a U23 Abstract battle on hostile arrival. The AI waits for the canonical result.

## Capture, loss and defeat

`SettlementCaptureService` requires a finished U23 battle won by the requested new side. U24 prevalidates the entire settlement building set before any mutation. After all existing Building owners change, U25 synchronizes the unchanged settlement owner. Repeated or rejected capture cannot leave partial ownership.

The common MVP defeat rule is `no U25 settlement is owned by the side`. Dynasty extinction is also an explicit AI defeat reason. On a lost expedition, the spent unit is removed from strategic knowledge and surviving membership is released; remaining eligible residents may form a replacement at a later decision tick. No result is manufactured by strategic code.

## Persistence and determinism

Snapshot v13 owns only strategic scope state. Units, travel, battle and buildings remain in their existing snapshot sections. Decision sequence, next/last ticks, pending orders and recent decisions prevent replay. AI unit/building identities are derived from world seed, state ID, purpose and sequence. Saves reject missing rulers, settlements, units and targets.

## Limits

U26 provides one configured rival but supports multiple runtimes. It does not add discovery/fog, diplomacy, trade, taxes, supply equipment, doctrines, final victory UI or production UI. Those remain assigned to U27, U30A and later roadmap tasks.
