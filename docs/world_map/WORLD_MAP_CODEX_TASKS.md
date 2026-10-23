# WORLD MAP CODEX TASKS

Эти задачи не меняют активный NEXT_TASK.md автоматически. Они становятся исполнимыми только после явной постановки и соблюдения текущего TASK_GRAPH.

## WM01 — Контракты данных карты

- Цель: Контракты данных карты.
- Зависимости: нет.
- Файлы и компоненты: Assets/Game/Domain/World; Resources/World.
- Изменить: Добавить polygon/spline/mask DTO и version parser.
- Готово, когда: Все JSON загружаются, invalid data fail closed.
- Проверка: EditMode schema/ID/bounds.
- Сложность: средне.
- Параллельная работа: нет.

## WM02 — Генератор рельефа

- Цель: Генератор рельефа.
- Зависимости: WM01.
- Файлы и компоненты: TiledWorldHeightSampler; Terrain authoring.
- Изменить: Noise octaves, anchors, ridge/pass/plateau masks.
- Готово, когда: Shared edges bit-identical; slope budget выполнен.
- Проверка: EditMode shared samples и slope histogram.
- Сложность: тяжело.
- Параллельная работа: да, с WM03 после contracts.

## WM03 — Гидрология

- Цель: Гидрология.
- Зависимости: WM01,WM02.
- Файлы и компоненты: World water domain; TiledTerrainWorld.
- Изменить: River widths, tributaries, four lake basins, per-water level policy.
- Готово, когда: Ровно четыре озера, сухие берега, связный drainage.
- Проверка: EditMode hydrology; PlayMode bank checks.
- Сложность: тяжело.
- Параллельная работа: нет.

## WM04 — Биомы и Terrain paint

- Цель: Биомы и Terrain paint.
- Зависимости: WM02,WM03.
- Файлы и компоненты: Terrain paint/material projection.
- Изменить: Blend region/ecotone masks and soil layers.
- Готово, когда: Нет прямоугольных переходов и tile seams.
- Проверка: Golden mask/hash и Player screenshots.
- Сложность: средне.
- Параллельная работа: да, с WM05.

## WM05 — Растительность и LOD

- Цель: Растительность и LOD.
- Зависимости: WM01,WM02.
- Файлы и компоненты: StarterNatureCatalog; environment LOD.
- Изменить: Seeded distribution, interactive/decorative split, collider culling.
- Готово, когда: Плотность в budgets, старт открыт.
- Проверка: EditMode determinism; profiler gates.
- Сложность: тяжело.
- Параллельная работа: да, с WM04.

## WM06 — Природные ресурсы

- Цель: Природные ресурсы.
- Зависимости: WM01,WM04.
- Файлы и компоненты: TiledWorldPopulation; Production/ResourceEcology.
- Изменить: Resource masks, start guarantee, stable IDs, hidden deposits.
- Готово, когда: Все ресурсы доступны логично, depletion persists.
- Проверка: EditMode determinism; Save/Load PlayMode.
- Сложность: тяжело.
- Параллельная работа: да, с WM07.

## WM07 — Рыба и восстановление водоёмов

- Цель: Рыба и восстановление водоёмов.
- Зависимости: WM03,WM06.
- Файлы и компоненты: Production sites; ecology.
- Изменить: 24 populations, bank points, carrying capacity recovery.
- Готово, когда: Каждая landing dry/reachable; overfishing recovers.
- Проверка: PlayMode all landings; save/reload.
- Сложность: средне.
- Параллельная работа: нет.

## WM08 — Видовая экология животных

- Цель: Видовая экология животных.
- Зависимости: WM01,WM04.
- Файлы и компоненты: Domain wildlife; tiers; presenters.
- Изменить: Species populations, habitat graph, migration, reproduction.
- Готово, когда: Animals map-wide; zones only multiply density.
- Проверка: Pure simulation tests; Tier transition PlayMode.
- Сложность: тяжело.
- Параллельная работа: да, с WM06.

## WM09 — Дороги и навигация

- Цель: Дороги и навигация.
- Зависимости: WM02,WM03.
- Файлы и компоненты: NavMesh; route presentation.
- Изменить: Seven routes, crossings, passes, links.
- Готово, когда: Все регионы доступны без воды/клиппинга.
- Проверка: Reachability matrix и Player route.
- Сложность: тяжело.
- Параллельная работа: да, с WM08.

## WM10 — Стартовая территория

- Цель: Стартовая территория.
- Зависимости: WM04,WM05,WM06,WM09.
- Файлы и компоненты: New game bootstrap; population plan.
- Изменить: NW start, clearing, basics, expansion corridors.
- Готово, когда: Первые стройки не заблокированы; river reachable.
- Проверка: PlayMode first-15-min contract.
- Сложность: средне.
- Параллельная работа: нет.

## WM11 — POI и исследование

- Цель: POI и исследование.
- Зависимости: WM01,WM04,WM09.
- Файлы и компоненты: Discovery/event adapters; presenters.
- Изменить: Nine POI, protection, discovery save-state.
- Готово, когда: POI уникальны, доступны, не auto-clear.
- Проверка: EditMode identity; Save/Load PlayMode.
- Сложность: средне.
- Параллельная работа: да, с WM10.

## WM12 — Строительство и расчистка

- Цель: Строительство и расчистка.
- Зависимости: WM02,WM03,WM05,WM10.
- Файлы и компоненты: BuildingPlacement; clearance coordinator.
- Изменить: Map masks into green/amber/red verdict.
- Готово, когда: Clearable blockers use workers; hard blockers fail.
- Проверка: Focused EditMode/PlayMode matrix.
- Сложность: тяжело.
- Параллельная работа: нет.

## WM13 — Версия мира и сохранения

- Цель: Версия мира и сохранения.
- Зависимости: WM06,WM08,WM11,WM12.
- Файлы и компоненты: SaveCoordinator; SaveXmlCodec; StartupSession.
- Изменить: Isolate p109-world-map-v2-proposed and persist new states.
- Готово, когда: Old saves untouched; wrong generation rejected.
- Проверка: Codec compatibility and file-hash checks.
- Сложность: тяжело.
- Параллельная работа: нет.

## WM14 — Оптимизация и профилирование

- Цель: Оптимизация и профилирование.
- Зависимости: WM05,WM08,WM09.
- Файлы и компоненты: LOD/culling/tier schedulers.
- Изменить: Measure budgets and tune density, not arbitrary counts.
- Готово, когда: 30 FPS minimum; no unbounded per-agent Update.
- Проверка: Profiler, memory, 100/300/500 gates.
- Сложность: тяжело.
- Параллельная работа: да, после feature stability.

## WM15 — Финальная интеграция и visual acceptance

- Цель: Финальная интеграция и visual acceptance.
- Зависимости: WM01-WM14.
- Файлы и компоненты: LocalGameplay; build pipeline; docs.
- Изменить: Broad regression, Windows build, visible route and coordinate audit.
- Готово, когда: JSON, runtime and images correspond; user accepts visuals.
- Проверка: Broad Edit/PlayMode, build, manual route.
- Сложность: тяжело.
- Параллельная работа: нет.
