# P10.9 — компактный мир и библиотека ассетов

## Запрос и границы

Пользователь 21 октября 2026 разрешил импорт локальных пакетов, внедрение графики,
несколько косяков в каждом озере, физические кучки улова и уменьшение мира до 1 × 1 км.
Старые сохранения не переносятся и не изменяются. Работа относится только к P10.9.

## Реализация

Новая генерация: `p109-compact-world-v1`. Обычные новые сохранения:
`Serenity/P109/CompactWorldV1/Saves`. Тесты используют только каталог Artifacts.
Мир состоит из 4 × 4 Terrain по 250 м. Все 16 участков удерживаются одновременно:
для этой небольшой карты это устраняет открытые края под камерой и потерю коллизий
при командах через границы участков. Это не результат отложенного performance-бенчмарка.

Координаты исходных поселений, сельхоз/охотничьих/ягодных/лесных/рудных зон, десяти
озёр и русла уменьшены в 12 раз. Игрок остаётся в нижнем центральном поселении.
Дополнительный учебный пруд, которого нет на исходной схеме, имеет локальные координаты
(88, 72) и радиусы (18, 15) м: его нельзя уменьшить до лужи внутри стартового лагеря.
Размеры персонажей, зданий и моделей не уменьшены. Высоты и береговые переходы
адаптированы к человеческому масштабу; навигационные переходы между Terrain тоже.
Ячейки разведки — 25 м, то есть 40 × 40 на весь мир.

В каждом из 11 водоёмов четыре независимых канонических источника рыбы, по 48 единиц.
Каждый имеет отдельную точку работы/выхода продукции на сухом берегу и отдельный
кликабельный косяк в воде. В косяке десять видимых рыб, круг движения и небольшие прыжки.
Это 44 источника и 440 визуальных рыб; визуальные объекты не владеют запасами.
Один завершённый вылов даёт отдельную кучку из четырёх рыб за 20 игровых минут до
модификаторов. Кучки сохраняются канонической системой WorldPile, не личным инвентарём.
Существующая очередь приоритетов решает, переносить ли их сейчас; перенос резервирует
только доступную вместимость склада. Остаток лежит на месте. Картинка кучки обновляется
после частичного переноса; несколько уловов у одной точки показаны рядом.

## Ассеты

Оригинальные архивы сохранены в Assets и исключены из Git как локальные исходники.
Проверенные импортированные файлы учитываются отдельно. Проверка tar/path/GUID и
списки исключений находятся в `Artifacts/P109/compact-world`.
Импорт не переносит сторонние C#-скрипты, DLL, сцены, ProjectSettings или PackageManager.
MonoBehaviour-компоненты удалены из импортируемых художественных prefab-копий;
исходные архивы не менялись. Общие существующие файлы Synty сохранены без перезаписи.
Первый импорт: 4656 художественных файлов; вложенные наборы: ещё 2270, всего 6926.
449 новых материалов первого импорта и 26 материалов вложенных наборов переведены в URP.
Контрольные суммы исходных архивов и количества сохранены в `P109_CONTENT_IMPORT_MANIFEST.json`.
У трёх шейдеров Toon Farm
удалены дублирующиеся объявления вариантов теней.

| Пакет | Первое применение / дальнейшее назначение |
|---|---|
| Synty Meadow Forest | Уже установлен ранее; деревья и грунт сохранены, повторного импорта нет. |
| Animals FREE | Олень с idle-анимацией заменяет капсулу ресурса охоты. Поведение стада — отдельный этап. |
| Polytope Nature Free | Каменный выход привязан к существующему кликабельному источнику камня. |
| Toon Farm | Небольшие деревянные ограждения обозначают сельхоззоны вместо больших коричневых плит. Остальные растения/животные — резерв. |
| Synty Tropical Jungle | Рисунок рыбы используется в косяках и улове. Пальмы не добавлены в умеренный лес случайным образом. |
| Synty Nature Pack | Библиотека дополнительных камней, кустов, растительности и природных деталей; массовая замена Meadow не выполняется. |
| Modular Armors Free | Библиотека для поздних эпох; текущая система персонажей не заменяется. |
| RTS Buildings | Библиотека последующих поселений, строительных стадий, причалов и судов. Модели не создают игровую механику строительства. |
| MedievalAssassinGirl | Модель/анимации в резерве; не заменяет жителей и не внедряет HDRP-настройки. |
| Wild Boar | Модель/анимации в резерве; реалистичный стиль нужно согласовать со стилизованным окружением. |
| Classic City Builder Kit | Только художественные ресурсы. Его собственные управление, экономика и сохранения не подключены. |
| Low Poly Asset Bundle | Отобраны шесть вложенных наборов: Nature Gradient, Nature Pack, Nature 4 Seasons, Farm, Fantasy Village, RPG Weapons. Современные города, офисы и FPS-контент не нужны текущей игре. |

Импорт в библиотеку не означает, что все предметы уже присутствуют в мире или работают
как игровые системы. Лицензионное происхождение сторонних архивов техническим импортом
не подтверждается.

## Дальнейшие этапы

1. Закрыть проверку компактного участка: повторные выловы, очередь более приоритетной
   работы, заполненный/частично заполненный склад, сохранение и загрузка кучек, клики
   по каждому косяку, камера у границ карты. Исправить найденные ошибки до расширения.
2. Привести в единый стиль населённые пункты и берега: убрать оставшиеся условные
   кубы/маркеры, подобрать дома по эпохе, добавить пристани и переходы через реку.
   Все действующие здания должны иметь каноническую идентичность и строительные стадии.
3. Оживить охотничьи зоны: небольшие стада, движение, испуг, охота, смерть, туша и перенос.
   Анимации животных подключать к нашим задачам, без отдельной сторонней системы AI.
4. Внедрить сельское хозяйство через настоящие участки: посадка, рост, урожай, истощение,
   сезонность, работа жителей. Модели Toon Farm/Low Poly Farm — только отображение состояния.
5. Разнообразить лес и руды по утверждённым зонам: варианты пород, валежник, кусты,
   небольшие поляны; сохранить кликабельность ресурсов и отсутствие деформации от камеры.
6. Поздние эпохи: оружие, модульная броня, улучшенные дома и корабли. После визуального
   согласования — атласы/LOD/профилирование отдельной разрешённой задачей.

## Проверка

Промежуточный EditMode: 49/49, `editmode.xml`; расширенный — 50/50, `editmode-final.xml`.
Первый PlayMode: 12/13, `playmode.xml`. Одна старая проверка требовала источники у
скрытых деревьев за новой границей мира; проверка обновлена: все деревья на сухой
земле внутри мира обязаны иметь источник, оставшиеся снаружи/в воде обязаны быть скрыты.
Это ещё не приёмка новой сборки.
Окончательные результаты, команды и открытые ограничения дополняются после проверки.

Повторный PlayMode `playmode-final.xml`: 13/14. Новая проверка всех рыбных мест нашла
отсутствующую навигацию у дальнего озера. Перемещение точки берега само по себе не помогло
(`playmode-banks.xml`: 1/2). Причина: объекты Terrain создаются и перемещаются в одном
кадре, а сбор геометрии NavMesh выполнялся до синхронизации физических координат.
Добавлен `Physics.SyncTransforms()` перед построением. Проверка всех 44 источников
после исправления прошла (`playmode-banks-sync.xml`: 1/1). Это дополнительно проверяет
индивидуальные цели клика, наличие десяти визуальных рыб и шестнадцати Terrain-коллайдеров.
Расширенный прогон с синхронизацией `playmode-accepted.xml`: 13/14; при пробном приближении
рабочих точек к воде один берег оказался слишком крутым для NavMesh. Итоговый отступ
возвращён за десятиметровый переход склона. Финальный адресный прогон
`playmode-shore-final.xml`: **2/2, 67.72 с, exit 0** — все 44 цели/берега и физический
вылов с появлением добычи. Остальные 13 сценариев прошли в расширенном прогоне.
Это совокупная проверка сценариев, а не заявление о едином зелёном прогоне 14/14.
Финальный EditMode `editmode-accepted.xml`: **50/50, 2.00 с, exit 0**. Он включает
географию/границы/детерминированность, четыре seed для берегов, материалы подключённых
prefab, повторные уловы и частичный перенос рыбы при нехватке места на складе.
Windows build: **Succeeded, exit 0, 0 errors / 53 warnings**;
GUID `c09db63a70084c12927995abacd7965c`, `Builds/P109CompactWorld/Serenity.exe`.
Первый запуск новой сборки дошёл до главного меню, но ручная проверка остановлена
системным запросом брандмауэра Windows. Инструмент computer-use запрещает взаимодействие
с запросами безопасности; пользователь должен самостоятельно закрыть его кнопкой «Отменить».
До этого момента ручная проверка рыбалки/камеры именно в новом бинарнике не заявляется.

Команда запуска Player:

```text
SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\compact-world\native-saves
C:\serenity_game\Builds\P109CompactWorld\Serenity.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -p109NatureReview -logFile C:\serenity_game\Artifacts\P109\compact-world\player.log
```

`-p109NatureReview` включает только кнопки закладок камеры; это не автоматическая
проверка ввода, команд или сохранений. Полное принятие управления через удержание
средней кнопки и общий визуальный обзор мира этим запуском не подменяются.

## Воспроизведение команд

Все команды выполняются из `C:\serenity_game`. Unity запускается с изолированным
`SERENITY_SAVE_DIRECTORY=C:\serenity_game\Artifacts\P109\compact-world\test-saves`.
Editor: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`.
Для процессов использовался `Start-Process -WindowStyle Hidden -PassThru`, затем
`$unityProcess.WaitForExit()` и проверка `ExitCode`. В PlayMode/EditMode `-quit` не нужен.

```powershell
$taskPython = 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $taskPython Tools/Audit-P109-Content.py
& $taskPython Tools/Prepare-P109-Content.py
& $taskPython Tools/Extract-P109-BundleArt.py
& $taskPython Tools/Audit-P109-Content.py Artifacts/P109/compact-world/bundle Artifacts/P109/compact-world/bundle
& $taskPython Tools/Prepare-P109-Content.py Artifacts/P109/compact-world/bundle
```

Подготовщик намеренно отказывается перезаписывать уже существующий ReviewedContent:
это команды выполненного импорта, а не инструкция повторно импортировать библиотеку.
Точные аргументы Unity (пути логов ниже относительно корня проекта):

```text
-batchmode -nographics -projectPath C:\serenity_game -executeMethod P109ContentImport.Import -logFile Artifacts/P109/compact-world/import-unsandboxed.log
-batchmode -nographics -projectPath C:\serenity_game -executeMethod P109ContentImport.Finish -logFile Artifacts/P109/compact-world/finalize-import-2.log
-batchmode -nographics -projectPath C:\serenity_game -executeMethod P109ContentImport.Import -p109BundleArt -logFile Artifacts/P109/compact-world/bundle/import.log
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod P109CompactArtAuthoring.Generate -logFile Artifacts/P109/compact-world/generate-art.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P109CompactWorldPlayModeTests;Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.P109TiledWorldStage1PlayModeTests;Game.Tests.PlayMode.P109TiledWorldStage2PlayModeTests -testResults Artifacts/P109/compact-world/playmode-accepted.xml -logFile Artifacts/P109/compact-world/playmode-accepted.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P109CompactWorldPlayModeTests;Game.Tests.PlayMode.P109PlayablePatchTests.FishingUsesReachableShoreAndLeavesFishOnGround -testResults Artifacts/P109/compact-world/playmode-shore-final.xml -logFile Artifacts/P109/compact-world/playmode-shore-final.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.Editor.P109CompactWorldTests;Game.Tests.Editor.P109TiledWorldDefinitionTests;Game.Tests.Editor.P109TiledWorldRuntimeTests;Game.Tests.Editor.P109TiledWorldPopulationTests;Game.Tests.Editor.P109HarvestTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.Tier1UtilityAiTests -testResults Artifacts/P109/compact-world/editmode-accepted.xml -logFile Artifacts/P109/compact-world/editmode-accepted.log
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod P109StarterNatureAuthoring.BuildCompactWorld -logFile Artifacts/P109/compact-world/build.log
```

Первый sandbox-запуск импорта завис на лицензировании. Первый несандбоксный импорт
завершил запись ассетов, но потерял callback при перекомпиляции; `Finish` выполнен
отдельно после исправления ссылки между editor-сборками. Исходные архивы не менялись.
Проверка защищённых файлов после импорта: 44/44, включая восемь старых сохранений.
Исходные пакеты и шесть извлечённых вложенных архивов повторно сверены с SHA-256
из манифеста: 18/18 без изменений. Это проверка целостности, не подтверждение лицензии.
