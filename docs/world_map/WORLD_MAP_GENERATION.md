# Алгоритм генерации serenity-world-map-v1

## Детерминированный pipeline

1. Загрузить все JSON и проверить одинаковый mapVersion.
2. Применить дизайн→Unity transform, не изменяя исходные точки.
3. Создать 4×4 shared-coordinate height field: macro noise, meso noise, detail noise.
4. Применить авторские height anchors и ridge mask.
5. Вырезать river/tributary valleys и lake basins; повторно проверить drainage.
6. Сгладить стартовое плато, agricultural clearings, passes и authored crossings.
7. Рассчитать slope, wetness, distance-to-water, soil и buildability rasters.
8. Смешать biome masks в ecotone 35–70 м.
9. Покрасить Terrain layers из одной маски; shared borders берут одинаковые samples.
10. Разместить interactive resources seeded Poisson-disc с StableEntityId от generation+layer+cell+ordinal.
11. Разместить decorative vegetation отдельно, без canonical mutable state.
12. Создать water presentation, dry fishing landings и navigation exclusions.
13. Спроецировать routes по высоте; пересечения воды разрешить только по crossing IDs.
14. Разместить POI и их protection volumes.
15. Сформировать NavMesh, links и habitat graph.
16. Запустить consistency, reachability, save/load и performance gates.

## Seed contract

World seed влияет на микровариации внутри масок, но не перемещает регионы, озёра, реку, дороги, POI и buildability corridors. StableEntityId должен выводиться из mapVersion, world seed, layer ID, quantized cell и ordinal. Порядок загрузки тайлов не влияет на результат.

## Высота

base = 18 м + macro*0.52 + meso*0.32 + detail*0.16. Затем применяются ручные маски. River/lake carve выполняется после noise, но до slope audit. Стартовое плато и проходы применяются последними с smooth blend, чтобы не появлялись резкие террасы.

## Ресурсы и восстановление

Resource masks задают вероятность и ёмкость, но не прямоугольную границу. Depleted interactive node сохраняет состояние и исчезает из presentation. Regrowth проверяет свободное место, occupancy и сезон. Hidden deposits генерируются сразу, но не имеют surface presenter до технологии.

## Животные

Habitat graph связывает food, rest, water и shelter nodes. Aggregate population tick работает вне камеры; Tier1 создаётся в пределах бюджета, Tier2 — для соседних активных зон, Tier3 хранит population cohorts. Охота уменьшает ту же популяцию, а не создаёт отдельную добычу поверх hunt_animal.

## Повторяемость

Генератор технических материалов: generate_world_map_package.py. Он не является Unity-генератором, но служит эталоном координат, изображений и cross-file validation.
