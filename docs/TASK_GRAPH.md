# TASK_GRAPH.md

Statuses: TODO / IN_PROGRESS / DONE / BLOCKED

| ID | Status | Task | Depends on | Phase | Definition of Done |
|---|---|---|---|---|---|
| U00 | DONE | Среда, Unity-проект и Git | — | MVP | Проект открывается без ошибок, создаёт Windows Development build, первый commit создан. |
| U01 | DONE | Архитектура assemblies и слоёв | U00 | MVP | Все assemblies компилируются; dependency graph задокументирован. |
| U02 | DONE | Пакеты и базовая конфигурация | U00,U01 | MVP | Package restore стабилен; проект открывается на чистом checkout. |
| U03 | DONE | Игровое время и календарь | U01 | MVP | Unit tests времени проходят; save/load времени работает. |
| U04 | DONE | Stable IDs и Save/Load | U01,U03 | MVP | Save→load восстанавливает идентичное состояние ключевых сущностей. |
| U04A | DONE | PostgreSQL Persistence Layer | U04 | MVP | Persistence abstraction; versioned migrations; transactional save/rollback; stable-ID round trips; separate integration-test database. |
| U05 | DONE | Локальная сцена, RTS-камера и input | U02 | MVP | Камера/выбор/команды работают в test scene. |
| U05A | DONE | Asset Pipeline + Free Asset Acquisition | U02,U05 | MVP | Runtime/staging folders, asset registry, free/placeholder base set, import normalization and traceable licenses. |
| U06 | DONE | Domain-модель персонажа Tier 1 | U03,U04 | MVP | EditMode tests модели; data-only snapshot готов для будущей карточки NPC. |
| U07 | DONE | GameObject presentation Tier 1 | U05,U05A,U06 | MVP | Tier1 персонаж видим, двигается, взаимодействует; состояние не хранится только в View. |
| U08 | DONE | Строительная сетка и blueprint-система | U04,U05,U05A | MVP | Можно спроектировать и достроить дом; состояние сохраняется. |
| U09 | DONE | Ресурсы, инвентари и склады | U06,U08 | MVP | Нет дублирования ресурсов; стройка требует реальный ресурс. |
| U10 | DONE | Utility AI Tier 1 | U06,U07,U09 | MVP | 100 NPC выполняют потребности и работу без 100 Update-heavy scripts. |
| U11 | TODO | Рабочие группы и задания | U10 | MVP | Игрок управляет >100 NPC через группы. |
| U12 | TODO | DOTS bootstrap Tier 2 | U02,U06 | MVP | Тысячи тестовых entities обновляются без GameObject-per-entity. |
| U13 | TODO | Tier manager 1↔2↔3 | U04,U07,U12 | MVP | Именованный NPC сохраняет ID/семью/здоровье при смене tier. |
| U14 | TODO | Детерминированный off-camera simulation | U03,U04,U13 | MVP | Одинаковый seed даёт тот же итог; zoom не меняет результат. |
| U15 | TODO | Производство и фермерство | U09,U10,U14 | MVP | Поселение устойчиво производит еду/материалы несколько игровых лет. |
| U16 | TODO | Research framework и каменный век | U06,U15 | MVP | Законченная каменная ветка открывает здания/рецепты. |
| U17 | TODO | Династия и наследование | U03,U04,U04A,U06 | MVP | Смерть монарха передаёт управление допустимому наследнику; extinction=GameOver. |
| U18 | TODO | Семейные события, болезни и травмы | U06,U17 | MVP | Событие может изменить состояние и способности наследника. |
| U19 | TODO | Погода и сезоны | U03,U05,U15 | MVP | Погода имеет измеримые gameplay-модификаторы. |
| U20 | TODO | Тактический RTS-бой | U05,U05A,U06,U07 | MVP | Два отряда завершают бой с детерминируемыми потерями. |
| U21 | TODO | Армия, капитаны и абстрактные отряды | U17,U20 | MVP | Отряд корректно сохраняется и переходит detailed↔aggregate. |
| U22 | TODO | Third-person control | U02,U07,U20,U21 | MVP | Переключение RTS↔TPS не теряет приказы и не ломает state. |
| U23 | TODO | Автобой и возврат в физическую битву | U14,U21 | MVP | Физический и abstract бой используют общие параметры и сходятся по диапазону результатов. |
| U24 | TODO | Разрушение, пожар и захват зданий | U08,U20,U23 | MVP | Здание разрушается/захватывается и правильно меняет производство. |
| U25 | TODO | Глобальная карта как отдельная сцена/режим | U04,U14 | MVP | Local↔Global переход сохраняет состояние. |
| U26 | TODO | AI одного государства | U14,U16,U17,U23,U25 | MVP | AI развивается, создаёт армию, атакует и может быть захвачен. |
| U27 | TODO | UI Toolkit/uGUI gameplay UI | U06,U08,U11,U16,U20 | MVP | Все MVP действия доступны без debug меню. |
| U28 | TODO | Событийная система | U04,U04A,U06,U17 | MVP | События сохраняются и воспроизводимы при seed. |
| U29 | TODO | Performance gates: 100→300→500→1000 | U10,U12,U13,U14,U20,U27 | MVP | 1000 NPC test scene достигает согласованного FPS/frametime без runaway allocations. |
| U30 | TODO | Automated tests и soak | U04A,U10,U13,U14,U23,U26,U29 | MVP | 8+ часов ускоренной симуляции без критических ошибок/утечек. |
| U31 | TODO | MVP Integration Gate | U08,U10,U13,U16,U17,U19,U22,U23,U25,U26,U27,U29,U30 | MVP | Новая игра проходит до захвата AI-государства без ручной правки данных. |
| U32 | TODO | Wealth / Influence | U06,U17 | POST | Показатели влияют на статус/события/отношение. |
| U33 | TODO | Министры, губернаторы и ведомства | U11,U17,U32 | POST | Замена чиновника меняет эффективность ведомства. |
| U34 | TODO | Казна, налоги, коррупция | U32,U33 | POST | Ставки меняют казну/стабильность; коррупция измерима. |
| U35 | TODO | Законы и протесты | U33,U34 | POST | Законы влияют на систему; протест может эскалировать. |
| U36 | TODO | Дипломатия | U17,U25,U26 | POST | AI оценивает условия и хранит договоры/последствия. |
| U37 | TODO | Шпионаж | U26,U36 | POST | Миссия имеет время/стоимость/шанс/последствия. |
| U38 | TODO | Эпохи после каменного века | U16,U31 | POST | Следующая эпоха не ломает save/data schemas. |
