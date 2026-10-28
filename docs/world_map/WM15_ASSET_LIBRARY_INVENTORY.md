# Полная инвентаризация ассетов — 28.10.2026

См. [выводы и приоритеты](WM15_ASSET_LIBRARY_REVIEW.md). Это статический аудит, не список безопасного удаления.

Обойдено 31088 файлов Assets с .meta; 64 внешних архивов, 172 архивных каталогов с вложенными Unity-пакетами. Ошибок чтения: 0. Во внешней serenity_games_assets: 165 файлов.
Внешние архивы внутри Assets занимают 17.37 GiB. Это место на диске, не размер Player и не измерение времени загрузки.

GUID-совпадения включают папки и общие зависимости. Например, совпадения PolygonGeneric не доказывают импорт Knights/Military Warehouse. Копии с новыми GUID и преобразованные высоты учитываются в основном отчёте.

## Все верхнеуровневые папки Assets

| Папка | Файлов без meta | MiB | Prefab | Моделей | Прямых ссылок | Достижимо от сцен/Resources | Решение |
|---|---:|---:|---:|---:|---:|---:|---|
| 23102026 | 11 | 11238.5 | 0 | 0 | 0 | 0 | Контейнер исходников/архивов; решения по каждому пакету ниже |
| 25102026 | 11 | 1899.8 | 0 | 0 | 0 | 0 | Контейнер исходников/архивов; решения по каждому пакету ниже |
| 26102026 | 469 | 23.7 | 0 | 304 | 0 | 0 | Контейнер исходников/архивов; решения по каждому пакету ниже |
| 28102026 | 6 | 2091.9 | 0 | 0 | 0 | 0 | Контейнер исходников/архивов; решения по каждому пакету ниже |
| 3D Low Poly Nature Pack -assets model | 207 | 4.6 | 132 | 2 | 2 | 3 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| [root files] | 16 | 2327.4 | 0 | 0 | 0 | 0 | Контейнер исходников/архивов; решения по каждому пакету ниже |
| _Recovery | 1 | 0.0 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| ANGRY MESH | 477 | 1891.9 | 110 | 99 | 42 | 200 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| Blink | 26 | 33.6 | 1 | 19 | 2 | 6 | Часть животных/персонажей уже подключена; сохранять действующие ссылки |
| Editor | 5 | 0.0 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| EmaceArt | 507 | 84.7 | 238 | 213 | 0 | 0 | Кандидат: детали поселения сейчас, готовые дома для поздней эпохи |
| EmbersStorm -  Free Nature Pack | 216 | 3.0 | 207 | 2 | 0 | 0 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| EmbersStorm-Magic Fantazy Armors Pack | 1 | 0.0 | 0 | 0 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| Game | 2754 | 1606.4 | 230 | 79 | 1154 | 576 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| Hodaart | 53 | 11.0 | 10 | 20 | 5 | 17 | Часть животных/персонажей уже подключена; сохранять действующие ссылки |
| ithappy | 49 | 17.1 | 7 | 7 | 5 | 5 | Часть животных/персонажей уже подключена; сохранять действующие ссылки |
| Kevin Iglesias | 147 | 36.6 | 22 | 84 | 1 | 1 | Уже выборочно использован/скопирован; проверить оставшиеся варианты |
| LightningBolt | 4 | 0.7 | 0 | 0 | 1 | 1 | Имеются игровые ссылки; сохранить |
| LowPolyFantasyVillage | 446 | 13.5 | 219 | 223 | 0 | 0 | Кандидат: детали поселения сейчас, готовые дома для поздней эпохи |
| LowPolyFarm | 548 | 16.3 | 272 | 271 | 12 | 13 | Используется частично; приоритет для зданий, хозяйства и декора |
| LowPolyNature (4 Seasons) | 497 | 14.3 | 247 | 247 | 0 | 0 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| LowPolyNatureGradientPack | 209 | 3.7 | 103 | 103 | 2 | 2 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| LowPolyNaturePack | 334 | 7.3 | 163 | 164 | 3 | 8 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| LowPolyRPGWeapons | 236 | 8.6 | 112 | 112 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| Medieval Action - FX Pack 2.0 | 6 | 7.2 | 0 | 0 | 4 | 4 | Звук; полезен атмосфере, не заменяет модели/визуальные VFX |
| Medieval Fortification | 61 | 225.8 | 7 | 6 | 10 | 20 | Используется частично; приоритет для зданий, хозяйства и декора |
| Medieval_Assassin_Girl | 199 | 675.7 | 66 | 78 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| NatureManufacture Assets | 106 | 1637.6 | 6 | 6 | 6 | 36 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| Packages | 234 | 8.8 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| PolygonNatureBiomes | 1446 | 770.2 | 393 | 460 | 55 | 127 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| PolyOne | 35 | 5.5 | 14 | 14 | 0 | 0 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| Polytope Studio | 397 | 131.7 | 135 | 133 | 5 | 8 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| Procedural Water Shader | 7 | 0.1 | 0 | 0 | 0 | 0 | Вода уже вызывается по имени шейдера; GUID-ноль не означает неиспользование |
| Rowlan | 149 | 1314.9 | 0 | 0 | 0 | 0 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| RTSToonBuildings | 229 | 14.6 | 100 | 106 | 17 | 17 | Используется частично; приоритет для зданий, хозяйства и декора |
| Scenes | 5 | 8.4 | 0 | 0 | 0 | 3 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| Settings | 7 | 0.1 | 0 | 0 | 1 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| Snow_mountain | 22 | 415.0 | 1 | 1 | 1 | 5 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| SoftLeitner | 419 | 13.2 | 0 | 98 | 35 | 36 | Действующие 3D-здания + 2D UI; не импортировать ProjectSettings |
| StreamingAssets | 1 | 0.0 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| Stylized Nature Environment | 56 | 74.8 | 13 | 13 | 0 | 0 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| SunboxGames | 263 | 545.1 | 1 | 48 | 0 | 229 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| Synty | 1851 | 208.4 | 722 | 698 | 24 | 34 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| TerrainDemoScene_URP | 352 | 2308.1 | 40 | 21 | 50 | 287 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| Tests | 2 | 0.0 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| Toon Farm Pack | 868 | 300.0 | 188 | 139 | 15 | 18 | Используется частично; приоритет для зданий, хозяйства и декора |
| ToonyTinyPeople | 14 | 1.3 | 1 | 5 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| TriForge Assets | 96 | 337.1 | 12 | 11 | 0 | 0 | Кандидат: детали поселения сейчас, готовые дома для поздней эпохи |
| TutorialInfo | 7 | 0.0 | 0 | 0 | 0 | 0 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| UMAProjectData | 2 | 0.2 | 0 | 0 | 0 | 2 | Собственный/служебный контент; не считать сторонним пакетом для удаления |
| URP GanzSe Free Modular Character Pack | 441 | 65.7 | 217 | 217 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| Vefects | 188 | 326.9 | 36 | 27 | 0 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| Wild Boar | 52 | 115.9 | 7 | 1 | 0 | 0 | Альтернативный кабан; сравнить с действующим, не дублировать автоматически |

## Все внешние архивы

| Архив внутри Assets | Записей | GUID-совпадений | Решение |
|---|---:|---:|---|
| `23102026/FREE Stylized Bear - RPG Forest Animal.unitypackage` | 47 | 26 | Часть животных/персонажей уже подключена; сохранять действующие ссылки |
| `23102026/Lightning Bolt Effect for Unity.unitypackage` | 22 | 4 | Имеются игровые ссылки; сохранить |
| `23102026/MicroVerse - Core Collection v1.7.35.unitypackage` | 1117 | 67 | Инструмент авторинга; только отдельная задача интеграции |
| `23102026/Middle Age - Medieval Action Sound FX Pack.unitypackage` | 37 | 6 | Звук; полезен атмосфере, не заменяет модели/визуальные VFX |
| `23102026/prbf27f5ba0ceb676deafbf2a4d0c0a1_Low Poly Nature Pack - assets model v1.0.unitypackage` | 216 | 207 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `23102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip` | 2 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `23102026/pre22s977398fj8d13csj_POLYGON_Dogs_Unity_2022_3_v1_1_0.unitypackage` | 1887 | 1195 | Резерв домашних собак; отдельная задача поведения |
| `23102026/pre3aketxh_Industrial Factory Factory Warehouse Industrial Factory Modular Factory v1.0.unitypackage` | 1083 | 0 | Поздние эпохи; сейчас отложить |
| `23102026/pre3aketxh_Miami Vice City v1.0.unitypackage` | 1067 | 0 | Поздние эпохи; сейчас отложить |
| `23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage` | 487 | 487 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `23102026/The Toby Foliage Engine Light.unitypackage` | 318 | 0 | Инструмент авторинга; только отдельная задача интеграции |
| `25102026/66d2c3cc80_uassets-dl-gtrsb9.unitypackage` | 97 | 0 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `25102026/7b01a393b0_uassets-dl-skh3th.unitypackage` | 953 | 0 | Только выбранные wave-текстуры; полный рендерер отложить |
| `25102026/80cef589b5_uassets-dl-ysplrj.unitypackage` | 446 | 0 | Инструмент авторинга; только отдельная задача интеграции |
| `25102026/Ambience - Banaue Rice Terraces Free Sample Pack.unitypackage` | 10 | 0 | Звук; полезен атмосфере, не заменяет модели/визуальные VFX |
| `25102026/ecc658483f_uassets-dl-qceo2b.unitypackage` | 791 | 1 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `25102026/FREE 3D VEGETATION PACK.unitypackage` | 109 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `25102026/Grass And Flowers Pack 1.unitypackage` | 101 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `25102026/Nature Materials vol.1.unitypackage` | 38 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `25102026/Nature Materials vol.2.unitypackage` | 22 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `25102026/Outdoor Ground Textures.unitypackage` | 102 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `25102026/Terrain Textures Pack Free.unitypackage` | 16 | 0 | Выборочные поверхности/растения; сравнить с текущими, не импортировать всё |
| `26102026/All Nature VFX.unitypackage` | 66 | 0 | Уже выборочно использован/скопирован; проверить оставшиеся варианты |
| `26102026/Animal Hide by Zsky - GaurgMNrWC.zip` | 2 | 0 | Уже выборочно использован/скопирован; проверить оставшиеся варианты |
| `26102026/fish.zip` | 3 | 0 | Уже выборочно использован/скопирован; проверить оставшиеся варианты |
| `28102026/Low Poly Medieval Europe Starter Pack.unitypackage` | 31 | 0 | 5 prefab персонажей/топора; зданий нет; отложить |
| `28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage` | 267 | 205 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `28102026/POLYGON - War Pack.unitypackage` | 1294 | 0 | Поздние эпохи; сейчас отложить |
| `28102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip` | 2 | 0 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `28102026/pre22s977398fj8d13csj_POLYGON_Military_Warehouse_Map_Unity_2022_3_v1_0_2.unitypackage` | 1483 | 1211 | Поздние эпохи; сейчас отложить |
| `28102026/Terrain Asset Pack Terrain Sample.unitypackage` | 259 | 95 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `8de6baa7a5_polygon-meadow-forest-nature-biomes-low-poly-3d-art-by-synty-v1.unitypackage` | 794 | 793 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 12.1.14.unitypackage` | 307 | 280 | Другой pipeline; не импортировать в текущий URP |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 14.0.10.unitypackage` | 307 | 280 | Другой pipeline; не импортировать в текущий URP |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 17.0.3.unitypackage` | 322 | 288 | Другой pipeline; не импортировать в текущий URP |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 12.1.14.unitypackage` | 309 | 309 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 14.0.10.unitypackage` | 309 | 309 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 17.0.3.unitypackage` | 318 | 318 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 17.2.0.unitypackage` | 318 | 318 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `Animals FREE - Animated Low Poly 3D Models.unitypackage` | 83 | 49 | Часть животных/персонажей уже подключена; сохранять действующие ссылки |
| `Classic City Builder Kit v1.9.1.unitypackage` | 2635 | 419 | Действующие 3D-здания + 2D UI; не импортировать ProjectSettings |
| `EmaceArt/Slavic World Free/URP_Support/URP/URP_Material.unitypackage` | 11 | 11 | Кандидат: детали поселения сейчас, готовые дома для поздней эпохи |
| `Hodaart/HodaartLowPolyCharacterCollection3/Packages/Hodaart Low Poly Character Collection 3 - Built-In.unitypackage` | 6 | 5 | Другой pipeline; не импортировать в текущий URP |
| `Hodaart/HodaartLowPolyCharacterCollection3/Packages/Hodaart Low Poly Character Collection 3 - HDRP.unitypackage` | 6 | 5 | Другой pipeline; не импортировать в текущий URP |
| `Kevin Iglesias/Human Animations/Animations/HumanCraftingAnimationsFREE.zip` | 2 | 0 | Уже выборочно использован/скопирован; проверить оставшиеся варианты |
| `Low Poly Environment - Nature Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage` | 187 | 129 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage` | 267 | 205 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `MedievalAssassinGirl.unitypackage` | 260 | 218 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `NatureManufacture Assets/Winter Background Mountains/Winter Background Mountains HD SRP.unitypackage` | 30 | 18 | Другой pipeline; не импортировать в текущий URP |
| `NatureManufacture Assets/Winter Background Mountains/Winter Background Mountains LW SRP.unitypackage` | 25 | 13 | Текущий источник зимы/рельефа; повторный массовый импорт не нужен |
| `Polytope Studio/Lowpoly_Characters/URP/PT_Peasants_Free_URP_12.unitypackage` | 20 | 11 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `Polytope Studio/Lowpoly_Characters/URP/PT_Peasants_Free_URP_14.unitypackage` | 20 | 11 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `Polytope Studio/Lowpoly_Characters/URP/PT_Peasants_Free_URP_16.unitypackage` | 20 | 11 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `Polytope Studio/Lowpoly_Characters/URP/PT_Peasants_Free_URP_17.unitypackage` | 20 | 11 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `pre22s977398fj8d13csj_POLYGON - Nature Pack - Art by Synty v1.2.1.unitypackage` | 1990 | 1851 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage` | 53 | 0 | Много эпох и pipeline; только выбранные предметы с зависимостями |
| `pre3aketxh_POLYGON Tropical Jungle - Nature Biomes - Low Poly 3D Art by Synty v1.8.0.unitypackage` | 803 | 736 | Часть используется или есть альтернативы; выборочно по биому/предмету |
| `pre3aketxh_RTS Pack - Stylized Buildings Props Ships Set v1.5.unitypackage` | 304 | 229 | Используется частично; приоритет для зданий, хозяйства и декора |
| `pre3aketxh_Toon Farm Pack v1.0.0.unitypackage` | 929 | 868 | Используется частично; приоритет для зданий, хозяйства и декора |
| `Procedural Water Shader/HDRP.unitypackage` | 5 | 5 | Другой pipeline; не импортировать в текущий URP |
| `Procedural Water Shader/URP.unitypackage` | 5 | 5 | Вода уже вызывается по имени шейдера; GUID-ноль не означает неиспользование |
| `URP GanzSe Free Modular Character Pack/Render Pipeline Extras/Built_In_Extract_me_GanzSe_Free_Modular_Character_Pack.unitypackage` | 456 | 450 | Резерв персонажей/экипировки или поздних эпох; не менять текущую систему |
| `URP GanzSe Free Modular Character Pack/Render Pipeline Extras/HDRP_Extract_me_GanzSe_Free_Modular_Character_Pack.unitypackage` | 456 | 451 | Другой pipeline; не импортировать в текущий URP |
| `WIld Boar - Free sample Ultrarealistic Forest Animals.unitypackage` | 122 | 52 | Альтернативный кабан; сравнить с действующим, не дублировать автоматически |

## Точные дубликаты внешних архивов

- SHA-256 `bc00792474e3bf8b96fb552ed9b83df10b18cbf26fb38a33083eabb9c5ef332d`: `Assets/23102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip`; `Assets/28102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip`
- SHA-256 `2876c4f7a65ae86b2c6708f6254042ced2cd788bd6128980ac49b8986784ee73`: `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage`; `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage`

## Вложенные пакеты

Перечислены отдельно: это не независимые покупки. ZIP исходников Knights каталогизирован как вложенный файл, его модели не распаковывались; поставляемый Unity-пакет прочитан.

| Контейнер → вложенный пакет | Записей | GUID-совпадений |
|---|---:|---:|
| `Assets/23102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip::POLYGON_Knights_Unity_2022_3_v1_6_1.unitypackage` | 2383 | 1197 |
| `Assets/23102026/pre3aketxh_Industrial Factory Factory Warehouse Industrial Factory Modular Factory v1.0.unitypackage::Assets/Hivemind/IndustrialFactory/URP/IndustrialFactoryURP.unitypackage` | 1088 | 0 |
| `Assets/23102026/pre3aketxh_Miami Vice City v1.0.unitypackage::Assets/LeartesStudios/MiamiViceCity/Built-in/Built-in_MiamiViceCity.unitypackage` | 897 | 0 |
| `Assets/23102026/pre3aketxh_Miami Vice City v1.0.unitypackage::Assets/LeartesStudios/MiamiViceCity/HDRP/Scene/HDRP_MiamiViceCity/HDRP_ImportExtraFogs_MiamiViceCity.unitypackage` | 16 | 0 |
| `Assets/23102026/pre3aketxh_Miami Vice City v1.0.unitypackage::Assets/LeartesStudios/MiamiViceCity/URP/URP_MiamiViceCity.unitypackage` | 897 | 0 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 14.0.10.unitypackage` | 309 | 309 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 12.1.14.unitypackage` | 309 | 309 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 14.0.10.unitypackage` | 307 | 280 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 17.0.3.unitypackage` | 318 | 318 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 12.1.14.unitypackage` | 307 | 280 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/URP Template 17.2.0.unitypackage` | 318 | 318 |
| `Assets/23102026/pre3aketxh_Winter Environment - Nature Pack v3.0.unitypackage::Assets/ANGRY MESH/Nature Pack - Winter Environment/SRP/HDRP Template 17.0.3.unitypackage` | 322 | 288 |
| `Assets/23102026/The Toby Foliage Engine Light.unitypackage::Assets/Toby Fredson/The Toby Foliage Engine/(TTFEL)_Pipelines/TTFEL - Unity 6.3.12f1+ (HDRP 17+).unitypackage` | 109 | 0 |
| `Assets/23102026/The Toby Foliage Engine Light.unitypackage::Assets/Toby Fredson/The Toby Foliage Engine/(TTFEL)_Pipelines/TTFEL - Unity 6.0.71f1+ (HDRP 17+).unitypackage` | 109 | 0 |
| `Assets/23102026/The Toby Foliage Engine Light.unitypackage::Assets/Toby Fredson/The Toby Foliage Engine/(TTFEL)_Pipelines/TTFEL - Built-In.unitypackage` | 93 | 0 |
| `Assets/23102026/The Toby Foliage Engine Light.unitypackage::Assets/Toby Fredson/The Toby Foliage Engine/(TTFEL)_Pipelines/TTFEL - Unity 6.0.71f1+ (URP 17+).unitypackage` | 101 | 0 |
| `Assets/25102026/ecc658483f_uassets-dl-qceo2b.unitypackage::Assets/StylizedNatureBundle/SNB_URP_7xxx.unitypackage` | 63 | 1 |
| `Assets/25102026/ecc658483f_uassets-dl-qceo2b.unitypackage::Assets/StylizedNatureBundle/SNB_HDRP_10xxx.unitypackage` | 73 | 1 |
| `Assets/25102026/ecc658483f_uassets-dl-qceo2b.unitypackage::Assets/StylizedNatureBundle/SNB_HDRP_7xxx.unitypackage` | 74 | 1 |
| `Assets/25102026/ecc658483f_uassets-dl-qceo2b.unitypackage::Assets/StylizedNatureBundle/SNB_URP_10xxx.unitypackage` | 72 | 1 |
| `Assets/26102026/All Nature VFX.unitypackage::Assets/GreenBugGames/HDRP All Nature VFX.unitypackage` | 52 | 0 |
| `Assets/26102026/All Nature VFX.unitypackage::Assets/GreenBugGames/HDRP All Nature VFX.unitypackage::Assets/GreenBugGames/All Nature VFX/All Nature VFX HDRP.unitypackage` | 55 | 0 |
| `Assets/26102026/All Nature VFX.unitypackage::Assets/GreenBugGames/URP All Nature VFX.unitypackage` | 47 | 0 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Weapons_URP_12.1.10.unitypackage` | 294 | 3 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_16.unitypackage` | 25 | 11 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/SRP/PT_Environment_Pack_URP_12.1.10.unitypackage` | 36 | 28 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_12.unitypackage` | 25 | 11 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_17.unitypackage` | 25 | 11 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Lowpoly_Weapons_URP.unitypackage` | 25 | 6 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Weapons_URP_10.10.1.unitypackage` | 295 | 3 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/SRP/PT_Environment_Pack_URP_10.10.1.unitypackage` | 20 | 13 |
| `Assets/28102026/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_14.unitypackage` | 25 | 11 |
| `Assets/28102026/pre12f980398fj8d13c_POLYGON_Knights_Unity_2022_3_v1_6_1.zip::POLYGON_Knights_Unity_2022_3_v1_6_1.unitypackage` | 2383 | 1197 |
| `Assets/8de6baa7a5_polygon-meadow-forest-nature-biomes-low-poly-3d-art-by-synty-v1.unitypackage::Assets/PolygonNatureBiomes/URP_ExtractMe_Nature_Biomes.unitypackage` | 287 | 222 |
| `Assets/Animals FREE - Animated Low Poly 3D Models.unitypackage::Assets/ithappy/Animals_FREE/Render_Pipeline_Convert/Unity_6_URP_source.unitypackage` | 3 | 3 |
| `Assets/Animals FREE - Animated Low Poly 3D Models.unitypackage::Assets/ithappy/Animals_FREE/Render_Pipeline_Convert/Unity_6_HDRP_source.unitypackage` | 3 | 3 |
| `Assets/Animals FREE - Animated Low Poly 3D Models.unitypackage::Assets/ithappy/Animals_FREE/Render_Pipeline_Convert/Unity_6_Built-In_source.unitypackage` | 3 | 3 |
| `Assets/Classic City Builder Kit v1.9.1.unitypackage::Assets/SoftLeitner/CityBuilderThree/Models/Legacy.zip` | 83 | 0 |
| `Assets/Classic City Builder Kit v1.9.1.unitypackage::Assets/SoftLeitner/CityBuilderTown/CityBuilderTownBlender.zip` | 27 | 0 |
| `Assets/Low Poly Environment - Nature Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_12.unitypackage` | 14 | 7 |
| `Assets/Low Poly Environment - Nature Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_17.unitypackage` | 15 | 7 |
| `Assets/Low Poly Environment - Nature Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_14.unitypackage` | 15 | 7 |
| `Assets/Low Poly Environment - Nature Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_16.unitypackage` | 15 | 7 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Weapons_URP_12.1.10.unitypackage` | 294 | 3 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_16.unitypackage` | 25 | 11 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/SRP/PT_Environment_Pack_URP_12.1.10.unitypackage` | 36 | 28 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_12.unitypackage` | 25 | 11 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_17.unitypackage` | 25 | 11 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Lowpoly_Weapons_URP.unitypackage` | 25 | 6 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Weapons/SRP/PT_Weapons_URP_10.10.1.unitypackage` | 295 | 3 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Environments/SRP/PT_Environment_Pack_URP_10.10.1.unitypackage` | 20 | 13 |
| `Assets/Low Poly Modular Armors - Free - LOWPOLY MEDIEVAL FANTASY SERIES.unitypackage::Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Armors_Free_URP_14.unitypackage` | 25 | 11 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior2/LowPolyInterior2_2019.4.0_v1.0.unitypackage` | 4529 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior2/LowPolyInterior2_2019.4.0_v1.0.unitypackage::Assets/LowPolyInterior2/LowPolyInterior2_2019.4.0_HDRP_v1.0.unitypackage` | 4527 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior2/LowPolyInterior2_2019.4.0_v1.0.unitypackage::Assets/LowPolyInterior2/LowPolyInterior2_2019.4.0_URP_v1.0.unitypackage` | 4526 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior/LowPolyInterior_2020.3_SRP_v1.42.unitypackage` | 888 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior/LowPolyInterior_2020.3_SRP_v1.42.unitypackage::Assets/LowPolyInterior/LowPolyInterior_2020.3_HDRP_v1.42.unitypackage` | 889 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyInterior/LowPolyInterior_2020.3_SRP_v1.42.unitypackage::Assets/LowPolyInterior/LowPolyInterior_2020.3_URP_v1.42.unitypackage` | 890 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyMegapolis/LowPolyMegapolis_1.04_SRP_2020.3.unitypackage` | 1128 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyMegapolis/LowPolyMegapolis_1.04_SRP_2020.3.unitypackage::Assets/LowPolyMegapolis/LowPolyMegapolis_1.04_URP_2020.3.unitypackage` | 1130 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyMegapolis/LowPolyMegapolis_1.04_SRP_2020.3.unitypackage::Assets/LowPolyMegapolis/LowPolyMegapolis_1.04_HDRP_2020.3.unitypackage` | 1130 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/NatureGradientPack/LowPolyNatureGradient_2020.3.0_SRP_v1.41.unitypackage` | 219 | 209 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/NatureGradientPack/LowPolyNatureGradient_2020.3.0_SRP_v1.41.unitypackage::Assets/LowPolyNatureGradientPack/LowPolyNatureGradient_2020.3.0_URP_v1.41.unitypackage` | 221 | 209 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/NatureGradientPack/LowPolyNatureGradient_2020.3.0_SRP_v1.41.unitypackage::Assets/LowPolyNatureGradientPack/LowPolyNatureGradient_2020.3.0_HDRP_v1.41.unitypackage` | 220 | 209 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyVillage/LowPolyFantasyVillage_1.41_SRP_2020.3.unitypackage` | 468 | 446 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyVillage/LowPolyFantasyVillage_1.41_SRP_2020.3.unitypackage::Assets/LowPolyFantasyVillage/LowPolyFantasyVillage_1.41_URP_2020.3.unitypackage` | 470 | 446 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyVillage/LowPolyFantasyVillage_1.41_SRP_2020.3.unitypackage::Assets/LowPolyFantasyVillage/LowPolyFantasyVillage_1.41_HDRP_2020.3.unitypackage` | 470 | 446 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNature (4 seasons)/LowPolyNature_4seasons_2019.4.0_v1.0.unitypackage` | 524 | 497 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNature (4 seasons)/LowPolyNature_4seasons_2019.4.0_v1.0.unitypackage::Assets/LowPolyNature (4 Seasons)/HDRP_LowPolyNature_4seasons_2019.4.0_v1.0.unitypackage` | 531 | 497 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNature (4 seasons)/LowPolyNature_4seasons_2019.4.0_v1.0.unitypackage::Assets/LowPolyNature (4 Seasons)/URP_LowPolyNature_4seasons_2019.4.0_v1.0.unitypackage` | 529 | 497 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyArena/LowPolyFantasyArena_SRP_2018.3.unitypackage` | 157 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyArena/LowPolyFantasyArena_SRP_2018.3.unitypackage::Assets/LowPolyFantasyArena/LowPolyFantasyArena_URP_2019.3.unitypackage` | 158 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyArena/LowPolyFantasyArena_SRP_2018.3.unitypackage::Assets/LowPolyFantasyArena/LowPolyFantasyArena_HDRP_2018.3.unitypackage` | 156 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFantasyArena/LowPolyFantasyArena_SRP_2018.3.unitypackage::Assets/LowPolyFantasyArena/LowPolyFantasyArena_LWRP_2018.3.unitypackage` | 157 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyDungeons/LowPolyDungeons_2020.3.0_SRP_v1.24.unitypackage` | 674 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyDungeons/LowPolyDungeons_2020.3.0_SRP_v1.24.unitypackage::Assets/LowPolyDungeons/LowPolyDungeons_2020.3.0_URP_v1.24.unitypackage` | 677 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyDungeons/LowPolyDungeons_2020.3.0_SRP_v1.24.unitypackage::Assets/LowPolyDungeons/LowPolyDungeons_2020.3.0_HDRP_v1.24.unitypackage` | 676 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonCubeWorld/CartoonCubeWorld_2020.3.0_SRP_v1.01.unitypackage` | 337 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonCubeWorld/CartoonCubeWorld_2020.3.0_SRP_v1.01.unitypackage::Assets/CartoonCubeWorld/CartoonCubeWorld_2020.3.0_URP_v1.01.unitypackage` | 339 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonCubeWorld/CartoonCubeWorld_2020.3.0_SRP_v1.01.unitypackage::Assets/CartoonCubeWorld/CartoonCubeWorld_2020.3.0_HDRP_v1.01.unitypackage` | 338 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyRPGWeapons/LowPolyRPGWeapons_2020.3.0_SRP_v1.0.unitypackage` | 267 | 236 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyRPGWeapons/LowPolyRPGWeapons_2020.3.0_SRP_v1.0.unitypackage::Assets/LowPolyRPGWeapons/LowPolyRPGWeapons_2020.3.0_URP_v1.0.unitypackage` | 268 | 236 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyRPGWeapons/LowPolyRPGWeapons_2020.3.0_SRP_v1.0.unitypackage::Assets/LowPolyRPGWeapons/LowPolyRPGWeapons_2020.3.0_HDRP_v1.0.unitypackage` | 266 | 236 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFPS/LowPolyFPSMap_2020.3_SRP_v1.01.unitypackage` | 175 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFPS/LowPolyFPSMap_2020.3_SRP_v1.01.unitypackage::Assets/LowPolyFPS/LowPolyFPSMap_2020.3_HDRP_v1.01.unitypackage` | 176 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFPS/LowPolyFPSMap_2020.3_SRP_v1.01.unitypackage::Assets/LowPolyFPS/LowPolyFPSMap_2020.3_URP_v1.01.unitypackage` | 177 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyOfficeInteriors/LowPolyOffice_2020.3.0_SRP_v1.01.unitypackage` | 1171 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyOfficeInteriors/LowPolyOffice_2020.3.0_SRP_v1.01.unitypackage::Assets/LowPolyOfficeInterior/LowPolyOffice_2020.3.0_URP_v1.01.unitypackage` | 1173 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyOfficeInteriors/LowPolyOffice_2020.3.0_SRP_v1.01.unitypackage::Assets/LowPolyOfficeInterior/LowPolyOffice_2020.3.0_HDRP_v1.01.unitypackage` | 1172 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyWeapons/LowPolyWeapons_2020.3_SRP_v1.11.unitypackage` | 1219 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyWeapons/LowPolyWeapons_2020.3_SRP_v1.11.unitypackage::Assets/LowPolyWeapons/LowPolyWeapons_2020.3_URP_v1.11.unitypackage` | 1221 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyWeapons/LowPolyWeapons_2020.3_SRP_v1.11.unitypackage::Assets/LowPolyWeapons/LowPolyWeapons_2020.3_HDRP_v1.11.unitypackage` | 1220 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNaturePack/LowPolyNaturePack_2020.3.0_SRP_v1.11.unitypackage` | 351 | 334 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNaturePack/LowPolyNaturePack_2020.3.0_SRP_v1.11.unitypackage::Assets/LowPolyNaturePack/LowPolyNaturePack_2020.3.0_HDRP_v1.11.unitypackage` | 352 | 334 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyNaturePack/LowPolyNaturePack_2020.3.0_SRP_v1.11.unitypackage::Assets/LowPolyNaturePack/LowPolyNaturePack_2020.3.0_URP_v1.11.unitypackage` | 353 | 334 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonLowPolyCity/CartoonLowPolyCity_2020.3.0_SRP_v1.4.unitypackage` | 277 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonLowPolyCity/CartoonLowPolyCity_2020.3.0_SRP_v1.4.unitypackage::Assets/CartoonLowPolyCity/CartoonLowPolyCity_2020.3.0_HDRP_v1.4.unitypackage` | 279 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/CartoonLowPolyCity/CartoonLowPolyCity_2020.3.0_SRP_v1.4.unitypackage::Assets/CartoonLowPolyCity/CartoonLowPolyCity_2020.3.0_URP_v1.4.unitypackage` | 280 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFarm/LowPolyFarm_2020.3_SRP_v1.11.unitypackage` | 573 | 548 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFarm/LowPolyFarm_2020.3_SRP_v1.11.unitypackage::Assets/LowPolyFarm/LowPolyFarm_2020.3_URP_v1.11.unitypackage` | 575 | 548 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyFarm/LowPolyFarm_2020.3_SRP_v1.11.unitypackage::Assets/LowPolyFarm/LowPolyFarm_2020.3_HDRP_v1.11.unitypackage` | 575 | 548 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyTropicalCity/LowPolyTropicalCity_2020.3.0_SRP_v1.01.unitypackage` | 865 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyTropicalCity/LowPolyTropicalCity_2020.3.0_SRP_v1.01.unitypackage::Assets/LowPolyTropicalCity/LowPolyTropicalCity_2020.3.0_HDRP_v1.01.unitypackage` | 866 | 0 |
| `Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage::Assets/LowPolyAssetBundle/LowPolyTropicalCity/LowPolyTropicalCity_2020.3.0_SRP_v1.01.unitypackage::Assets/LowPolyTropicalCity/LowPolyTropicalCity_2020.3.0_URP_v1.01.unitypackage` | 869 | 0 |
| `Assets/pre3aketxh_POLYGON Tropical Jungle - Nature Biomes - Low Poly 3D Art by Synty v1.8.0.unitypackage::Assets/PolygonNatureBiomes/URP_ExtractMe/Unity_2022.unitypackage` | 520 | 222 |
| `Assets/pre3aketxh_POLYGON Tropical Jungle - Nature Biomes - Low Poly 3D Art by Synty v1.8.0.unitypackage::Assets/PolygonNatureBiomes/URP_ExtractMe/Unity_2021.unitypackage` | 520 | 222 |
| `Assets/pre3aketxh_RTS Pack - Stylized Buildings Props Ships Set v1.5.unitypackage::Assets/RTSToonBuildings/SingleFBXBuildingPropsSet.unitypackage` | 1 | 0 |
| `Assets/pre3aketxh_RTS Pack - Stylized Buildings Props Ships Set v1.5.unitypackage::Assets/RTSToonBuildings/NoShaderGraph.unitypackage` | 120 | 0 |

## Внешняя библиотека serenity_games_assets

| Каталог/файл | Файлов | MiB |
|---|---:|---:|
| 3D Retro Medieval Fantasy Kit | 22 | 2.3 |
| CaveManCreation | 51 | 3.5 |
| FBX_LP_NaturePack_V2_1 | 40 | 1.2 |
| FBX_Stoneage_Weapons_V1_1 | 12 | 0.7 |
| FBX_Stoneage_WildHunt_V1_1 | 2 | 2.1 |
| Free Modular Medieval Assets | 4 | 0.4 |
| FreeSurvivalKit | 11 | 5.0 |
| Low Poly Tools | 20 | 0.9 |
| medievalsurvivalprops.blend | 1 | 2.0 |
| model | 1 | 0.1 |
| model.glb | 1 | 0.3 |
