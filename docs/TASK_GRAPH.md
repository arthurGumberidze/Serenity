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
| U10 | DONE | Utility AI Tier 1 | U06,U07,U09 | MVP | 100 NPC выполняют потребности и работу без 100 Update-heavy scripts; dev selection читает live canonical resources/claims. |
| U11 | DONE | Рабочие группы и задания | U10 | MVP | Игрок управляет >100 NPC через группы. |
| U12 | DONE | DOTS bootstrap Tier 2 | U02,U06 | MVP | Тысячи тестовых entities обновляются без GameObject-per-entity. |
| U13 | DONE | Tier manager 1↔2↔3 | U04,U07,U12 | MVP | Именованный NPC сохраняет ID/семью/здоровье при смене tier. |
| U14 | DONE | Детерминированный off-camera simulation | U03,U04,U13 | MVP | Одинаковый seed даёт тот же итог; zoom не меняет результат. |
| U15 | DONE | Производство и фермерство | U09,U10,U14 | MVP | Поселение устойчиво производит еду/материалы несколько игровых лет. |
| U16 | DONE | Research framework и каменный век | U06,U15 | MVP | Законченная каменная ветка открывает здания/рецепты. |
| U16A | DONE | Debug gameplay controls / usability patch | U05,U08,U10,U11,U15,U16 | MVP | Игрок назначает research workers, выбирает research-gated здания и управляет временем без конфликта с work-group hotkeys. |
| U17 | DONE | Династия и наследование | U03,U04,U04A,U06 | MVP | Смерть монарха передаёт управление допустимому наследнику; extinction=GameOver. |
| U18 | DONE | Семейные события, болезни и травмы | U06,U17 | MVP | Событие может изменить состояние и способности наследника. |
| U19 | DONE | Погода и сезоны | U03,U05,U15 | MVP | Погода имеет измеримые gameplay-модификаторы. |
| U20 | DONE | Тактический RTS-бой | U05,U05A,U06,U07 | MVP | Два отряда завершают бой с детерминируемыми потерями. |
| U21 | DONE | Армия, капитаны и абстрактные отряды | U17,U20 | MVP | Отряд корректно сохраняется и переходит detailed↔aggregate. |
| U22 | DONE | Third-person control | U02,U07,U20,U21 | MVP | Переключение RTS↔TPS не теряет приказы и не ломает state. |
| U23 | DONE | Автобой и возврат в физическую битву | U14,U21 | MVP | Физический и abstract бой используют общие параметры и сходятся по диапазону результатов. |
| U24 | DONE | Разрушение, пожар и захват зданий | U08,U20,U23 | MVP | Здание разрушается/захватывается и правильно меняет производство. |
| U25 | DONE | Глобальная карта как отдельная сцена/режим | U04,U14 | MVP | Local↔Global переход сохраняет состояние. |
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

Completion note (2026-10-06): U14 deterministic Tier 3 catch-up, keyed RNG, tier-path equivalence and 1,000/10,000-character scale coverage are complete. This historical note predates the U15 completion below.

Follow-up note (2026-10-06): U11 RTS right-click Move is complete. Selected characters or the active WorkGroup now issue tier-aware, formation-spaced U11 Move jobs through the U05 pointer pipeline and U10 movement bridge. This note predates the U15 completion recorded below.

Completion note (2026-10-06): U15 reusable recipes/sites/tasks, finite gathering, primitive crop plots, canonical output ownership, food consumption, target-stock automation, tier-independent deterministic execution and version-2 production persistence are complete. Five simulated years remain food-positive with no shortage; full regression, PostgreSQL, GPU, asset and Windows build gates passed. U16 is next but has not started.

Completion note (2026-10-06): U16 engine-independent research definitions/service, validated 14-technology Stone Age graph, typed building/recipe/capability unlocks, centralized Tier1/Tier2/Tier3 work, deterministic checkpoints, version-3 research persistence and debug progression panel are complete. End-to-end capstone, multi-year U15+U16, full regression, PostgreSQL, GPU and Windows build gates passed. U17 is next but has not started.

Completion note (2026-10-07): U16A removes hardcoded demo research assignments, distinguishes assigned from authoritative active research, adds the catalog-backed research-gated building selector and adds canonical pause/speed controls without consuming U11 digit shortcuts. Focused/full/regression/GPU/build gates passed. U17 remains not started pending review.

Completion note (2026-10-07): U17 adds canonical stable-ID dynasty state, read-only family queries over U06 links, deterministic agnatic primogeniture, event-driven exactly-once succession, player-ruler transfer, extinction/Game Over, succession history, tier-independent/off-camera behavior, version-4 character/dynasty persistence with v1-v3 compatibility and a click-blocked LocalGameplay debug overlay. Focused, full, persistence, tier-route, collection-order, GPU, PostgreSQL and Windows build gates passed. U18 is next but has not started.

Completion note (2026-10-07): U18 adds one canonical deterministic health/family authority, nine injury/disease/temporary/permanent definitions, capability effects integrated with AI/production/research, U17 death hooks, four exactly-once relationship events, pregnancy/birth with deterministic child IDs and dynasty membership, Tier1/2/3 off-camera equivalence, version-5 persistence with v1-v4 compatibility and a click-blocked debug panel. Focused, full, regression, persistence, determinism, long-run, GPU and Windows build gates passed. U19 is next but has not started.

Completion note (2026-10-08): U19 adds canonical four-season derivation, keyed deterministic daily temperate weather, fixed-point temperature/precipitation/wind, data-driven crop/outdoor-work/movement/health/wild-food modifiers, weather-aware U15 production and farming, U18 cold/heat exposure with exactly-once markers, Tier1/Tier2/Tier3 hooks, version-6 persistence with v1-v5 compatibility and a click-blocked debug/presentation layer. Focused, full, regression, persistence, catch-up, actual tier-route, long-run, graphics and Windows build gates passed. U20 is next but has not started.

Completion note (2026-10-08): U20 adds a reusable stable-ID two-side combat domain, deterministic eligibility/targeting/cooldown melee, U18 injury/incapacitation/death with U17 succession, bounded morale and retreat, terminal outcomes, player group Attack and centralized hostile tactics, active-combat Tier1 pinning, a physical 4v4 demo, version-7 combat persistence with v1-v6 compatibility and a click-blocked diagnostic panel. Focused, full, regression, persistence, collection-order, save/load, 100-combatant, physical NavMesh, GPU and Windows build gates passed. U21 is next but has not started.

Completion note (2026-10-08): U21 adds canonical stable-ID military units with owner/side, historical Character-ID rosters, eligible captains and deterministic fallback/Leaderless behavior, explicit Detailed/Aggregate transitions over the existing TierManager, U11 Move reuse, U20 encounter integration and active-combat pinning, derived casualty/injury/morale/cohesion summaries, logical aggregate orders/location hooks, version-8 persistence with v1-v7 compatibility and a click-blocked LocalGameplay unit panel. Focused, full, regression, persistence, collection-order, save/load, tier-route, combat-integration, 10-unit/100-member, GPU and Windows build gates passed. U22 is next but has not started.

Completion note (2026-10-08): U22 adds engine-independent RTS/ThirdPerson control state over an existing Character stable ID, explicit NavMesh movement authority, Utility/work suspension with U11 Move requeue, U20 movement suppression without replacing combat, U21 captain/unit continuity, Tier1 pinning, reusable Cinemachine/input-map handoff, safe death/incapacity fallback and snapshot v9 with v1-v8 compatibility. U22 passed 7/7 EditMode, 12/12 headless and GPU PlayMode, 320/320 full EditMode, 65/65 full PlayMode, required regressions, persistence and Windows build. U23 is next and has not started.

Completion note (2026-10-08): U23 extends the canonical U20 encounter into a stable-ID cross-mode battle with shared combat rules, deterministic one-minute abstract steps, compact history and U21 unit links. Rollback-safe Detailed/Physical↔Aggregate/Abstract transitions preserve Character/unit/battle identity, health, morale, targets, attack sequence/cooldowns and retreat state; U22 TPS blocks abstraction, while U18/U17 deaths and U21 captain fallback remain authoritative off-camera. Snapshot v10 reads v1-v9. U23 passed 12/12 EditMode, 3/3 headless and 3/3 GPU PlayMode, 332/332 full EditMode, 68/68 full PlayMode, PostgreSQL 18 integration 25/25, required regressions, persistence, convergence, 500-participant stress and Windows build. U24 is next but has not started.

Completion note (2026-10-08): U24 extends the canonical Building with integrity, structural/fire/checkpoint/owner state and compact history. One centralized service applies generic, combat and fixed-step weather-modified fire damage, leaves destroyed buildings as occupying ruins, atomically spills storage to WorldPiles, stops production/crops and validates in-place capture from a finished hostile victory. Snapshot v11 reads v1-v10. U24 passed 14/14 EditMode, 3/3 headless and GPU PlayMode, 346/346 full EditMode, 71/71 full PlayMode, required regressions, persistence, 100-fire/1,000-building determinism stress and Windows build. U25 is next but has not started.

Completion note (2026-10-09): U25 adds a validated stable-ID world graph, settlements and absolute-tick travel, rollback-safe Local↔Global representation changes over U13/U21/U22/U23, deterministic hostile-arrival handoff to the canonical abstract battle, a separate additive schematic map scene, continued off-camera simulation and snapshot v12 with v1-v11 compatibility. U25 passed 12/12 EditMode, 3/3 headless and GPU PlayMode, 358/358 full EditMode, 74/74 full PlayMode, required regressions, persistence, 100-unit/50-location stress and Windows build. U26 is next but has not started.
