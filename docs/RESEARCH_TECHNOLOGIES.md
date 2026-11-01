# Каталог исследований — предложение UI08-R3

Статус: HTML-согласование, не реализация Unity. 51 технология; сохранены 33 прежних ID.
Столбцы/ряды задаются данными `docs/prototypes/research-review-v3/catalog.js`.
AND означает все перечисленные технологии; OR — любое одно из перечисленных. Стартовые: basic_survival, gathering.

| ID | Технология | Направление | AND | OR | Чел.-дни | Статус интеграции |
|---|---|---|---|---|---|---|---|
| basic_survival | Основы выживания | Общий корень | — | — | 0 | IMPLEMENTED |
| gathering | Собирательство | Выживание | basic_survival | — | 0 | IMPLEMENTED |
| shelter_construction | Простые укрытия | Выживание | gathering | — | 1.5 | PARTIAL |
| controlled_fire | Управляемый огонь | Выживание | basic_survival | — | 1.5 | PARTIAL |
| hunting | Организованная охота | Выживание | gathering, stone_tools | — | 2.5 | IMPLEMENTED |
| fishing | Рыболовство | Выживание | gathering | — | 2.5 | PARTIAL |
| primitive_agriculture | Первое земледелие | Выживание | gathering, stone_tools | — | 4 | IMPLEMENTED |
| cooking | Приготовление пищи | Выживание | controlled_fire | hunting, fishing, primitive_agriculture | 2.5 | PARTIAL |
| reliable_housing | Надёжные жилища | Выживание | shelter_construction, woodworking | — | 4 | PARTIAL |
| food_preservation | Сохранение пищи | Выживание | cooking | — | 4 | PARTIAL |
| seasonal_preparation | Сезонные запасы | Выживание | — | food_preservation, primitive_agriculture | 5 | PARTIAL |
| stone_tools | Каменные орудия | Производство | basic_survival | — | 2.5 | IMPLEMENTED |
| fiber_working | Растительные волокна | Производство | — | — | 1.5 | PROPOSED |
| woodworking | Обработка древесины | Производство | stone_tools | — | 2.5 | IMPLEMENTED |
| clay_working | Работа с глиной | Производство | stone_tools | — | 2.5 | PARTIAL |
| organized_labor | Организация труда | Производство | woodworking | — | 4 | PARTIAL |
| pottery | Гончарное дело | Производство | clay_working | — | 4 | PROPOSED |
| advanced_stonecraft | Точная обработка камня | Производство | stone_tools, woodworking | — | 4 | PARTIAL |
| hide_working | Выделка шкур | Производство | stone_tools | — | 3 | PARTIAL |
| industrial_extraction | Разработка недр | Производство | advanced_stonecraft | — | 5 | PARTIAL |
| weaving | Плетение и ткачество | Производство | fiber_working | — | 4 | PROPOSED |
| restorative_mining | Восстановление выработок | Производство | industrial_extraction | — | 5.5 | PROPOSED |
| standardization | Ремесленные стандарты | Производство | organized_labor, advanced_stonecraft | — | 5.5 | PROPOSED |
| primitive_weapons | Примитивное оружие | Военное дело | basic_survival | — | 2.5 | PARTIAL |
| militia | Ополчение поселения | Военное дело | primitive_weapons | — | 3 | PARTIAL |
| territory_guard | Охрана территории | Военное дело | primitive_weapons | — | 2.5 | PARTIAL |
| formation_training | Строевая подготовка | Военное дело | militia | — | 4 | PARTIAL |
| throwing_weapons | Лук и стрелы | Военное дело | primitive_weapons | — | 3 | PARTIAL |
| military_organization | Военная организация | Военное дело | formation_training, organized_labor | — | 5.5 | IMPLEMENTED |
| fortification | Укрепление поселения | Военное дело | territory_guard, woodworking | — | 5 | PARTIAL |
| military_logistics | Военное снабжение | Военное дело | military_organization, food_preservation | — | 5.5 | PROPOSED |
| observation | Наблюдение природы | Знания и медицина | basic_survival | — | 1.5 | PARTIAL |
| plant_lore | Знание растений | Знания и медицина | — | — | 1.5 | PARTIAL |
| animal_lore | Знание животных | Знания и медицина | observation | — | 2.5 | PARTIAL |
| herbalism | Травничество | Знания и медицина | plant_lore | — | 2.5 | PARTIAL |
| season_calendar | Календарь сезонов | Знания и медицина | observation | — | 3 | PARTIAL |
| wound_care | Уход за ранеными | Знания и медицина | herbalism | — | 3 | PARTIAL |
| record_keeping | Учёт знаний | Знания и медицина | season_calendar | — | 4 | PARTIAL |
| practical_anatomy | Практическая анатомия | Знания и медицина | wound_care, animal_lore | — | 4 | PROPOSED |
| knowledge_transfer | Передача опыта | Знания и медицина | record_keeping | — | 5 | PARTIAL |
| natural_philosophy | Систематизация знаний | Знания и медицина | knowledge_transfer | — | 5.5 | PARTIAL |
| kinship | Родовая община | Общество | basic_survival | — | 1.5 | PARTIAL |
| shared_storage | Общинные пути | Общество | — | — | 1.5 | PARTIAL |
| settlement_council | Совет поселения | Общество | kinship | — | 3 | PARTIAL |
| spiritual_tradition | Общие традиции | Общество | kinship | — | 2.5 | PARTIAL |
| land_administration | Управление землями | Общество | settlement_council | — | 4 | PLANNED |
| exchange_and_measures | Обмен и меры | Общество | shared_storage | — | 3 | PROPOSED |
| customary_law | Обычное право | Общество | land_administration | — | 5 | PLANNED |
| trade_contacts | Торговые связи | Общество | exchange_and_measures | — | 4 | PROPOSED |
| early_diplomacy | Посольства общин | Общество | trade_contacts, settlement_council | — | 5.5 | PLANNED |
| stone_age_mastery | Освоение каменного века | Рубеж эпохи | — | — | 6.5 | PARTIAL |

IMPLEMENTED означает подтверждённый существующий эффект, а не внедрение новой схемы.
PARTIAL означает перенос существующего открытия либо предложенный локальный research-hook; в HTML это дизайн.
PLANNED — документированный будущий этап; PROPOSED — новая идея без готовой механики.
Эффекты, практические условия, материальные затраты и оговорки доступны в карточках HTML и catalog.js.

