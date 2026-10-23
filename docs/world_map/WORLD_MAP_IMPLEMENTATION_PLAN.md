# План внедрения карты в Unity

## Принцип

Не редактировать текущую сцену или p109-compact-world-v1 на месте. Создать p109-world-map-v2-proposed, отдельный Resources JSON и отдельный save directory. Внедрение начинается только после завершения активного P10.9 F1 либо нового явного решения пользователя.

## Использовать существующие authority

- TiledWorldDefinition / TiledWorldDefinitionLoader: расширить polygon/spline layers без параллельного world manager.
- TiledWorldHeightSampler: заменить упрощённый noise pipeline data-driven masks, сохранив pure deterministic API.
- TiledTerrainWorld: проецировать 16 Terrain, water, paint и navigation.
- P109TiledWorldPopulationPresenter: presentation только; stable nodes создаёт Domain plan.
- ProductionService / ResourceEcologyService: depletion, regrowth и save-state.
- BuildingPlacementService / P102WorldPlacementSurface / ConstructionClearanceCoordinator: green/amber/red placement.
- TierManager и OffCameraSimulationService: wildlife/materialization tiers.
- SaveCoordinator / SaveXmlCodec: новая версия payload только при появлении нового canonical state.

## Этапы

1. Data contracts: новые DTO для polygons, spline widths, height masks, vegetation/resource/wildlife layers.
2. Validation: bounds, unique IDs, cross references, non-self-intersecting polygons, shoreline and route rules.
3. Height/hydrology: shared samples, erosion mask, four basins, dry banks.
4. Terrain paint/vegetation: density masks, LOD, instancing, collider budget.
5. Resources: stable natural nodes, hidden deposits, start guarantee, depletion persistence.
6. Wildlife: сначала species/population Domain, затем Tier1/2/3 presentation; не расширять hunt_animal как вечную заглушку.
7. Roads/navigation: terrain projection, three crossings, complete reachability matrix.
8. POI/discovery: protected StableEntityId, discovery and event hooks.
9. Buildability: data layer → existing placement verdict and clearance workflow.
10. Save boundary: isolated generation/path, wrong-generation rejection before rebind.
11. Tests/build/Player: targeted tests, broad affected regression, Windows build and visible route.

## Риски

- Текущий loader жёстко требует p109-compact-world-v1 и 4×4×250; нужен новый parser/version branch.
- Текущий WorldZoneDefinition поддерживает только ellipse/rectangle, поэтому polygons нельзя свести к большим boxes.
- Height sampler сейчас опускает все fishing ellipses до одного WaterLevel; горное озеро требует per-water surfaceY или дизайнерского компромисса.
- Охота сейчас ресурсная, а не видовая мобильная. Не создавать presentation-only популяцию.
- Все 16 Terrain сейчас удерживаются. Это удобно для 1 км², но density budgets должны пройти profiler.
- Существующие пользовательские dirty-файлы не относятся к этой карте и не должны входить в её commit.

## Test matrix

- EditMode: schema, transforms, polygon containment, shared height edges, water connectivity, route/water crossing, start guarantees, StableEntityId determinism.
- PlayMode: all-region reachability, every fishing landing, building green/amber/red, clearable blocker workflow, depletion/regrowth, POI protection, Tier transitions.
- Save/Load: resource remaining, removed trees, wildlife cohorts, discovery, buildings, bridge state, generation rejection.
- Player: камера до всех четырёх краёв, старт→river→lake→ridge→marsh, strategic overview, construction clearance and reload.
- Performance: 100/300/500 NPC gates плюс vegetation/wildlife density tiers.

## Acceptance result for this design stage

Документация, JSON и изображения проверяются локальным генератором. Unity build не требуется на design-only этапе, потому что Assets, Packages, ProjectSettings и сцены не изменены. Runtime implementation не может считаться завершённой без Unity tests/build.
