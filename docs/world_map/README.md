# World map package

Каноническая версия: serenity-world-map-v1. Сгенерировано 2026-10-23.

Начните с WORLD_MAP_OVERVIEW.md, затем WORLD_MAP_MASTER_SPEC.md и WORLD_MAP_COORDINATES.json. Все layer JSON используют метры и север +Z. PNG в images/ созданы из тех же Python-структур. world_map_concept_art.png — единственный неканонический художественный референс.

Повторная генерация:

    C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe docs/world_map/generate_world_map_package.py

Скрипт не читает и не изменяет Unity scene/assets/saves.
