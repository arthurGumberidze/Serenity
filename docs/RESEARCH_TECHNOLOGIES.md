# Каталог исследований — предложение UI08-R4

Статус: HTML-согласование. R4 заменяет горизонтальную схему R3 по новому указанию пользователя.
51 технология, 33 прежних ID. Каждое исследование имеет одного реального предшественника.
Связей ИЛИ и обязательных зависимостей из соседних направлений нет. Стартовые знания: basic_survival и gathering.

| ID | Технология | Направление | Предшественник | Чел.-дни | Интеграция |
|---|---|---|---|---|---|
| basic_survival | Основы выживания | root | — | 0 | IMPLEMENTED |
| gathering | Собирательство | Выживание | basic_survival | 0 | IMPLEMENTED |
| shelter_construction | Простые укрытия | Выживание | gathering | 1.5 | PARTIAL |
| controlled_fire | Управляемый огонь | Выживание | gathering | 1.5 | PARTIAL |
| hunting | Организованная охота | Выживание | controlled_fire | 2.5 | IMPLEMENTED |
| fishing | Рыболовство | Выживание | shelter_construction | 2.5 | PARTIAL |
| primitive_agriculture | Первое земледелие | Выживание | reliable_housing | 4 | IMPLEMENTED |
| cooking | Приготовление пищи | Выживание | controlled_fire | 2.5 | PARTIAL |
| reliable_housing | Надёжные жилища | Выживание | shelter_construction | 4 | PARTIAL |
| food_preservation | Сохранение пищи | Выживание | cooking | 4 | PARTIAL |
| seasonal_preparation | Сезонные запасы | Выживание | food_preservation | 5 | PARTIAL |
| stone_tools | Каменные орудия | Производство | basic_survival | 2.5 | IMPLEMENTED |
| fiber_working | Растительные волокна | Производство | stone_tools | 1.5 | PROPOSED |
| woodworking | Обработка древесины | Производство | stone_tools | 2.5 | IMPLEMENTED |
| clay_working | Работа с глиной | Производство | stone_tools | 2.5 | PARTIAL |
| organized_labor | Организация труда | Производство | woodworking | 4 | PARTIAL |
| pottery | Гончарное дело | Производство | clay_working | 4 | PROPOSED |
| advanced_stonecraft | Точная обработка камня | Производство | woodworking | 4 | PARTIAL |
| hide_working | Выделка шкур | Производство | organized_labor | 3 | PARTIAL |
| industrial_extraction | Разработка недр | Производство | advanced_stonecraft | 5 | PARTIAL |
| weaving | Плетение и ткачество | Производство | fiber_working | 4 | PROPOSED |
| restorative_mining | Восстановление выработок | Производство | industrial_extraction | 5.5 | PROPOSED |
| standardization | Ремесленные стандарты | Производство | advanced_stonecraft | 5.5 | PROPOSED |
| primitive_weapons | Примитивное оружие | Военное дело | basic_survival | 2.5 | PARTIAL |
| militia | Ополчение поселения | Военное дело | primitive_weapons | 3 | PARTIAL |
| territory_guard | Охрана территории | Военное дело | primitive_weapons | 2.5 | PARTIAL |
| formation_training | Строевая подготовка | Военное дело | militia | 4 | PARTIAL |
| throwing_weapons | Лук и стрелы | Военное дело | primitive_weapons | 3 | PARTIAL |
| military_organization | Военная организация | Военное дело | formation_training | 5.5 | IMPLEMENTED |
| fortification | Укрепление поселения | Военное дело | territory_guard | 5 | PARTIAL |
| military_logistics | Военное снабжение | Военное дело | military_organization | 5.5 | PROPOSED |
| observation | Наблюдение природы | Знания и медицина | basic_survival | 1.5 | PARTIAL |
| plant_lore | Знание растений | Знания и медицина | observation | 1.5 | PARTIAL |
| animal_lore | Знание животных | Знания и медицина | observation | 2.5 | PARTIAL |
| herbalism | Травничество | Знания и медицина | plant_lore | 2.5 | PARTIAL |
| season_calendar | Календарь сезонов | Знания и медицина | animal_lore | 3 | PARTIAL |
| wound_care | Уход за ранеными | Знания и медицина | herbalism | 3 | PARTIAL |
| record_keeping | Учёт знаний | Знания и медицина | season_calendar | 4 | PARTIAL |
| practical_anatomy | Практическая анатомия | Знания и медицина | wound_care | 4 | PROPOSED |
| knowledge_transfer | Передача опыта | Знания и медицина | record_keeping | 5 | PARTIAL |
| natural_philosophy | Систематизация знаний | Знания и медицина | knowledge_transfer | 5.5 | PARTIAL |
| kinship | Родовая община | Общество | basic_survival | 1.5 | PARTIAL |
| shared_storage | Общинные пути | Общество | kinship | 1.5 | PARTIAL |
| settlement_council | Совет поселения | Общество | kinship | 3 | PARTIAL |
| spiritual_tradition | Общие традиции | Общество | kinship | 2.5 | PARTIAL |
| land_administration | Управление землями | Общество | settlement_council | 4 | PLANNED |
| exchange_and_measures | Обмен и меры | Общество | shared_storage | 3 | PROPOSED |
| customary_law | Обычное право | Общество | land_administration | 5 | PLANNED |
| trade_contacts | Торговые связи | Общество | exchange_and_measures | 4 | PROPOSED |
| early_diplomacy | Посольства общин | Общество | trade_contacts | 5.5 | PLANNED |
| stone_age_mastery | Освоение каменного века | epoch | — | 6.5 | PARTIAL |

Изменения правил предлагаются на согласование, а не уже действуют в Unity.
Для готовки достаточно огня; охота, рыболовство и земледелие не обязательны для неё.
Рыболовство требует укрытий; земледелие — надёжного жилья; военная организация — строевой подготовки.
Остальные требования перечислены в HTML: практика поселения и выборочные материальные затраты.
IMPLEMENTED — подтверждённый существующий эффект; PARTIAL — нужна привязка/часть локального эффекта;
PLANNED — документированный будущий этап; PROPOSED — идея без готовой механики.

