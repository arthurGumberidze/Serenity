# DEV01 — исправления по игровому отзыву 2026-11-02/03

Статус: **DONE, 03.11.2026**. База 084b2690. Полный перечень — ../DEV01_FEEDBACK.md.

Итог: обычная партия seed 1380062136 дошла до **дня 31, 03:15**, 10 живых жителей,
cheatsUsed=false / sequence=0. Слот 2 и копия `Artifacts/DEV01Feedback/player/day31-final.xml`.
За добытые материалы построены Дом знаний, казарма, костёр, корзина и новое жильё. Первый военный
прошёл реальный путь до входа; исследования работали по сменам и завершали очередь автоматически.
К финалу суммарно произведено 114 cooked_berries, 508 berries, 200 wood_log, 84 stone, 12 plant_fiber.
Завершены 10 технологий, включая стартовые. Ручные цели и состояния после Continue сохранены.

**315 уникальных целевых PASS**, итоговая сборка build-final: 0 ошибок / 54 предупреждения, 80,49 s.
Запускать `Builds/DEV01Feedback/Serenity.exe`; он побайтно содержит итоговую сборку из DEV01Final.
Native маршрут проходил через исправляемые промежуточные сборки одной партии; финальная сборка
загрузила день 27 и дошла до дня 31. Это не непрерывный месячный прогон одного неизменного бинарника.

Ограничения: не полный test suite; не 1000-NPC performance gate и не 8-часовая выдержка.
Клавиши native automation не подтверждали Escape/Home: соответствующие InputSystem runtime проверки
прошли отдельно. Не объявляем проходимым каждый метр карты или весь цикл войны/соседней державы.
Player штатно закрыт после Save; Exception/Error/Failed в итоговом runtime-логе нет. На выходе
Unity сообщила `Persistent allocates 2 individual allocations` без стека: остаётся диагностикой DEV04.

Далее сохранена хронология, включая неуспешные промежуточные проверки и их исправления.
Проверки выполняются через Tools/UnityRunner.ps1: сначала Editor PID 38240, после восстановления
один Editor 6000.6.4f1 PID 37596; дочерние AssetImportWorker не являются вторым проектным Editor.
Промежуточные результаты ниже сами по себе не закрывали первый месяц; итог подтверждён отдельно выше.

## Реализация

- UI: корректные координаты блокировки колеса над UI; Shift+ПКМ добавляет объектную команду;
  Esc закрывает верхнее окно, затем открывает меню паузы. Настройки применяются в текущей партии.
- Профессиональные группы: сборщики объединены с разнорабочими; виртуализированный список с прямым
  выбором профессии и индивидуальной смены; русские навыки; сохраняется прокрутка/раскрытие карточек.
- Строительство: компактная палитра значков и описание, шаг 45°, доступные исследованиями проекты
  размещаются без полного запаса. Материалы доставляются физически, дефицит создаёт спрос.
  Ручная цель склада хранится отдельно от потребности строек. Материалы видимы на площадке.
- Совместная работа: 1 + 0,6*(N−1) для фактически работающих, без вклада идущего работника.
  Общий прогресс целого дерева не дублирует выход. Стройка допускает несколько заданий рабочих.
- Исследования: три выбранных пункта, автоматический переход, зависимости и очередь сохраняются.
  Соседний AI использует физический допуск в Дом знаний; Ollama не исполняет обычные работы.
- Ночной отдых учитывает смену и скрывает жителя только после входа. Изменены скольжение по препятствиям
  при прямом контроле и расчёт анимации по фактическому перемещению. Нужна визуальная приёмка движения.
- Карта: земляные пятна/луг, проекция GPU-травы на реальную высоту, восстановление текстуры цветов,
  заметные ягоды, больше речных рыб; существующие озёра/снежная гора/сакура сохраняются.
  Небольшие стабильные различия роста/телосложения/оттенка жителей не требуют новой UMA-сборки на каждого.
- События поддержки/конфликта влияют на отношения и временно на работу/исследования; последствия видны.
  План запасов учитывает дрова по типам домов. Поворот/масштаб просмотра персонажа дополнен высотой камеры.
- Сытость <30%: работа/исследования 75%; <20%: 50%, движение 80%; <10%: 25%, движение 80%.
  48 непрерывных игровых часов ниже 10% вызывают смерть; еда до 10% сбрасывает таймер, лечение — нет.
  Таймер хранится в canonical health, предупреждение показано в карточке.

## Команды и промежуточные результаты

Все пути ниже относительно C:/serenity_game. Команды запускались на host для штатного Unity licensing IPC.
Обязательная компиляция выполняется до тестов: команда test сама не обновляет исходники.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-ninth -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'HealthAndFamilyTests|ResearchFrameworkTests|P83WorkGroupTests|WorkGroupAndJobTests|ProductionAndFarmingTests|StockPlanningTests|P109ConstructionStateTests|P109ConstructionClearanceTests|P109BuildingProductionTests|BuildingFoundationTests|WM04WorldMapTerrainPaintTests|WM05WorldMapVegetationTests|UI04ManagementWorkspaceTests' -OutputPath Artifacts/DEV01Feedback/focused-fourth -TimeoutSeconds 1200
```

- compile-ninth: 0 ошибок / 141 предупреждение, 59,765 s.
- focused-fourth: 148 проверок, 146 PASS, 2 FAIL: старый hash изменённой карты и source-string
  проверка старого расположения интерфейса групп. Нормализация/непрерывность покрытия и границы тайлов прошли.
- focused-third: 62 PASS, включая два новых теста голода и очередь. Более ранний focused-second
  был запущен до компиляции новых тестов и НЕ является доказательством проверки голода.
- focused-fifth: обновлённый hash карты и совместная добыча прошли; выявлены устаревшие ожидания
  старого завершения рыбалки и строк в UI; исправление проверяется отдельно.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.DEV01FeedbackRuntimeTests.FreshSettlementBuildsResearchHouseFromGatheredResources' -OutputPath Artifacts/DEV01Feedback/normal-route-first -TimeoutSeconds 1200
```

Первый прогон FAILED на резервировании стройки вторым строителем. До ошибки новая партия seed 1380062136
без выдачи ресурсов поставила Дом знаний и доставила часть реальных материалов (остаток 25 камня / 40 брёвен).
Исправлено эксклюзивное резервирование Build; повторный прогон обязателен.
Дампы residents/jobs/stocks/buildings находятся в Artifacts/DEV01Feedback/runtime.

## Оставшаяся приёмка на промежуточном этапе (история)

Повторный сквозной прогон, Windows build, normal-Player взаимодействия (колесо, Esc, очереди,
прокрутка, перемещения/входы, визуальные материалы, смены, приготовление и месячные запасы).
Не считать массовую производительность, полноценный первый месяц или весь P10 подтверждёнными
по одному узкому сценарию. Итоговый commit/push выполняется после завершения необходимых проверок.

### Повторная проверка 03.11, 00:58

- Последние результаты на каждый testcase из focused-third…harvest-current: 177 уникальных проверок PASS;
  это накопленный результат нескольких запусков, не один полный suite. JSON: latest-test-summary.json.
- normal-route-second: Дом знаний реально построен без выдачи ресурсов. FAIL: два строителя не работали
  одновременно. Обнаружены попытки жителей игрока брать чужие задания перевозки; добавлена проверка
  принадлежности поселению в общем Work.AssignmentScope. У завершённой стройки оставшиеся задания снимаются.
- В живом окне видны белые полигоны травы: ground mesh не содержит tree vertex mask и выбирал пустую
  текстуру коры. Добавлен явный foliage flag для ground cover. Визуальная перепроверка обязательна.
- compile-fourteenth: 0 ошибок / 106 предупреждений, id 20261102215724144-4ca9846931f444b4a2ee8cbbcc4dac40.
- normal-route-third выполняется с HealthAndFamilyTests|P83WorkGroupTests|P109ConstructionClearanceTests|DEV01FeedbackRuntimeTests.

### Дополнительная проверка 03.11, 01:40

- normal-route-third: **34/34 PASS** (HealthAndFamilyTests, P83WorkGroupTests,
  P109ConstructionClearanceTests и реальный fresh-game маршрут). Дом знаний построен за добытые
  ресурсы, два строителя наблюдались одновременно на месте, исследователь вошёл и дал прогресс.
- compile-final: 0 ошибок / 54 предупреждения. Первый Windows build собрал Player, но runner
  отклонил аттестацию: UMA обновила AssetIndexerProject.asset между source-before/source-after.
  Это только preview, обязательна повторная сборка после стабилизации исходников.
- Preview Player запущен через обычное главное меню, seed 1380062136. Проверены загрузка и новая
  партия. Обнаружены MeshCollider errors у инструментов на неравномерно масштабированных жителях.
  Исправление: holder неактивен до Instantiate, коллайдеры отключаются до активации инструмента.
- compile-world-events: 0 ошибок / 141 предупреждение, 74.71 s.
- world-events: **34 PASS / 1 FAIL**. Единственный FAIL — ожидаемый старый hash terrain paint после
  доработки покрытий. Проверки нормализации/соседних тайлов прошли; hash обновлён на
  11588388288129154093. Новый тест событий подтвердил реальные модификаторы работы/исследований,
  замену штрафа поддержкой, восстановление состояния и окончание эффектов.
- compile-movement-ui: 0 ошибок / 9 предупреждений, 32.93 s.
- movement-ui: 0 выбранных тестов из-за пересечения TestNames и GroupNames; это НЕ PASS.
  movement-ui-second запускается только с точным TestNames.

Дополнительные изменения: мягкий стартовый рельеф вне центральной площадки, узкие протоптанные
полосы, выраженные грунтовые/мшистые пятна, меньше случайной россыпи цветов, низкие кусты/камни
в существующем GPU-бюджете. Растительность убирается с занятых строительных площадок.
В стартовом окружении добавлены реальные собираемые грибы и растительное волокно.
Отдельные семейные/лагерные решения больше не используют пустой narrative-only эффект.

Точные команды дополнительных запусков:
```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-world-events -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'EventSystemTests|WM04WorldMapTerrainPaintTests|WM05WorldMapVegetationTests|WM10StartTerritoryTests|P109TiledWorldRuntimeTests' -OutputPath Artifacts/DEV01Feedback/world-events -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-movement-ui -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -TestNames 'Game.Tests.Editor.DEV01FeedbackRuntimeTests.FreshWorldSupportsUiWheelAndPhysicalResidentMovement' -OutputPath Artifacts/DEV01Feedback/movement-ui-second -TimeoutSeconds 1200
```
Фильтр WM10StartTerritoryTests был неточным; корректный класс WM10WorldMapStartTerritoryTests
ещё должен войти в заключительную проверку. Финальные статусы не выставлены до Player-приёмки.

### Заключительные целевые проверки 03.11, 01:50

- compile-final-review: 0 ошибок / 59 предупреждений, 37.39 s.
- final-review: 82 проверки, 81 PASS; единственный FAIL — старое ограничение пути
  префабов `Art/Prefabs` после их прежнего переноса в `Art/WM15Expansion`. Префабы существуют.
- FreshWorldSupportsUiWheelAndPhysicalResidentMovement PASS: seed 1380062136,
  изменение zoom над строительной панелью 0, 8/8 полных NavMesh маршрутов вокруг лагеря,
  физическое перемещение W за 3 секунды 7.749603 м; панели от W не открылись.
  Предыдущий movement-ui-second не учитывал преобразование panel units в screen pixels;
  его ошибка не доказывала дефект обработки колеса. Новый тест проверяет попадание в UI перед вводом.
- compile-art-assertion: 0 ошибок / 7 предупреждений, 28.45 s.
- start-art-final: 3/3 PASS после корректировки допустимого каталога ассетов.
- Накопленные последние результаты по каждому testcase: **224 уникальных PASS**, 0 оставшихся
  FAIL; это целевые запуски, не один полный suite (Artifacts/DEV01Feedback/latest-test-summary.json).

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-final-review -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'FreshWorldSupportsUiWheelAndPhysicalResidentMovement|WM10WorldMapStartTerritoryTests|WM04WorldMapTerrainPaintTests|P103ContextCommandTests|Tier1UtilityAiTests|EventSystemTests|HealthAndFamilyTests' -OutputPath Artifacts/DEV01Feedback/final-review -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-art-assertion -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'WM10WorldMapStartTerritoryTests' -OutputPath Artifacts/DEV01Feedback/start-art-final -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-final -TimeoutSeconds 1200
```
### Проверка ввода и runner 03.11, 02:22

- build-final: PASS, 111.56 s, 0 ошибок / 87 предупреждений, build GUID
  12e09658fe7547948a41a314899d5f08. Fingerprint
  62F077FF301123836CAAC3EE5D74EC58EDED0C097A104DADAA4581DDD33FC453.
- Native Player: новая партия seed 1380062136, обычный старт без выдачи ресурсов; настройки
  применились сразу (масштаб проверен и возвращён 1.02), сохранение в ранее пустой слот 2 успешно.
  Камера создания персонажа имеет zoom и вертикальное смещение. Строительное колесо не двигает карту.
  На карте видны покрытия/растительность; белые полигоны ground cover устранены.
  Player-first-review.log не содержит прежних MeshCollider exceptions инструментов.
- В Player обнаружены дополнительные дефекты: описание строений уходило под сетку, длинная профессия
  переносила смену на следующую строку, раздел наследовал чужую прокрутку. Исправлены:
  отдельная панель описания рядом с иконками, строка без переноса и сброс при смене раздела.
  Прокрутка внутри того же раздела сохраняется.
- player-input-review и player-input-final не приняты: runner выдавал IOException при удалении/
  замене heartbeat, пока PowerShell читал файл. Это инфраструктурная ошибка, а не основание
  скрывать неуспешные проверки. Запись теперь атомарная через File.Replace; только коды
  Windows 32/33/1175 для heartbeat оставляют прежний целый файл до следующего тика.
  Чтение runner открывает FileShare.ReadWrite|Delete. Командные/результатные ошибки не подавляются.
- compile-heartbeat-retry: 0 ошибок / 7 предупреждений, 31.55 s.
- player-input-verified: **2/2 PASS**, id 20261102231610602-41e95c6d3433448f8dd35a00e19a924f,
  338.61 s. Проверены настоящий InputSystem Escape (закрытие панелей/пауза), геометрия
  профессии/смены, начальная прокрутка, колесо, физическое W и маршруты лагеря.
  Отдельный тест удерживает настоящий Windows file handle и проверяет целостность heartbeat.
- Native sky.press_key(Escape) не дал достоверного результата; это не записано как native PASS.
  InputSystem runtime тест проверяет игровой путь клавиатуры. Повторная сборка после UI исправлений обязательна.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-heartbeat-retry -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'FreshWorldSupportsUiWheelAndPhysicalResidentMovement|RunnerHeartbeatRetainsCompleteStateWhileWindowsReaderHoldsFile' -OutputPath Artifacts/DEV01Feedback/player-input-verified -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-player-reviewed -TimeoutSeconds 1200
```
### Native загрузка и исправление времени 03.11, 02:33

- build-player-reviewed отклонён из-за изменения UMA AssetIndexerProject.asset во время сборки.
  Повторный build-player-stable PASS (27.00 s, 0 ошибок/0 предупреждений), fingerprint
  9768CD648BA86C694F97004DE7B79937D5EA2B18BA87EB88E1E76D587087B2A5.
- Native Continue из слота 2 воспроизвёл **Calendar simulation time cannot move backwards**.
  Player безопасно вернулся в главное меню, не подменяя сохранение новой партией.
  Лог сохранён: player/Player-load-failure.log. Эта сборка не считается принятой по Save/Load.
- Причина: TierManager восстанавливал Tier3 через обычную Materialize, которая догоняет текущее
  время ещё не перепривязанной сессии. При позднем checkpoint получалась ошибка, при раннем —
  лишняя симуляция. Добавлен отдельный контракт точного восстановления checkpoint; обычные
  переходы/догоняющая симуляция сохраняют строгую проверку монотонного времени.
  RebaseAfterLoad больше не обнуляет сохранённые детерминированные накопители.
- compile-checkpoint-time: 0 ошибок / 106 предупреждений, 41.64 s.
- checkpoint-time: **33/33 PASS**, включая четыре комбинации раннего/позднего времени и
  существующей/новой Tier3 representation, точное состояние до rebind и следующий шаг после него.
  Также прошли регрессии TierManager и P106DomesticAndSaveTests.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-checkpoint-time -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'OffCameraSimulationTests|TierManagerTests|P106DomesticAndSaveTests' -OutputPath Artifacts/DEV01Feedback/checkpoint-time -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-checkpoint-time -TimeoutSeconds 1200
```

### Native Continue и палитра, 03.11, 02:46

- build-checkpoint-time PASS, 49.26 s, 0 ошибок / 54 предупреждения. Fingerprint
  0032636262C61F99EF0CF55B3DA61F9EB1CA6B2BF08A14463505770DBFB4E1AB,
  GUID 824daa592d8840e4beb97db3adfe3959.
- Native Continue PASS: обычный слот 2 восстановлен на дне 1 в 10:23, 10 жителей, исходные запасы.
  player/07-load-restored.png. В UI назначены Арен/Доран строителями дневной смены,
  Мира разнорабочей ночной смены; сохранено в тот же проверочный слот 2 в 02:45.
  Слоты 1/3 пользователя не перезаписывались.
- Native просмотр нашёл ещё дефект палитры: ScrollView естественной высоты центрировался в строке,
  значки выступали над заголовком. player/08-build-layout-defect.png. Высота обеих колонок
  ограничена 210, родитель выравнивает их по высоте. Добавлены проверки реальной геометрии.
  Смены подписаны целиком «День»/«Ночь», часы вынесены в подсказку; dropdown/hover оформлены темой.
- compile-palette PASS: 49.55 s, 0 ошибок / 9 предупреждений.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-palette -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames FreshWorldSupportsUiWheelAndPhysicalResidentMovement -OutputPath Artifacts/DEV01Feedback/palette-layout -TimeoutSeconds 1200
```

- palette-layout: 1/1 PASS, 335.32 s; реальная геометрия обеих колонок в границах палитры,
  колесо/Esc/W и NavMesh не регрессировали. Накопленные последние результаты: 258 unique PASS.
- build-palette отклонён provenance: UMA AssetIndexerProject.asset обновлён в 23:53:39 UTC
  внутри сборки. Повторный build-palette-stable PASS, 28.16 s, 0 ошибок / 0 предупреждений.
  Fingerprint A513D6C761985F123BFCD7640031C4860F5DB5F642E5787045F680740967526F,
  GUID a76160a421744dc2893fbd1ec6c07a17. Это сборка следующего native-прохода.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-palette -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-palette-stable -TimeoutSeconds 1200
```


### Native строительство и смены, 03.11, 03:20

- На seed 1380062136, без кодов/выдачи ресурсов, поставлен Дом знаний за 80 брёвен/50 камня.
  День 2: материалы поступали в буфер с видимыми предметами на площадке; Арен/Доран дневной
  смены строили одновременно. Дом завершён около 14:30. Скриншоты player/09-build-palette-fixed,
  10-construction-buffer, 11-two-builders. Прокрутка инспектора сохранялась при обновлении прогресса.
- Через новый список профессии/смены назначены Нерис/Пела днём, Мира/Села ночью.
  День 3 00:51: очередь «Управляемый огонь», «Приготовление пищи», «Примитивное оружие» (3/3).
  Ночная смена 2/2 дала 2,2%, затем 24,8%; дневная продолжила до 56,1% и выше.
  player/12-research-queue-three, 13-day-shift-research. Save XML подтвердил активную технологию,
  двух работников и два ожидающих пункта; слот 2 сохранён на дне 4 10:04. Слоты 1/3 не трогались.
- Найдены пропуски переводов рецептов оружия. Добавлены все отсутствующие recipe unlock labels;
  проверка всего открываемого контента StoneAgeResearchPlan прошла, P1RussianLocalization 9/9.
- Найден стартовый пищевой тупик, решение D-250. Production/health/localization 42/42 PASS.
  compile-start-food: 69,38 s, 0 ошибок/106 предупреждений. build-start-food: 55,03 s,
  0 ошибок/54 предупреждения. Партия продолжается на этой сборке.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-unlock-labels -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames P1RussianLocalizationTests -OutputPath Artifacts/DEV01Feedback/unlock-labels -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-start-food -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'ProductionAndFarmingTests|P1RussianLocalizationTests|HealthAndFamilyTests' -OutputPath Artifacts/DEV01Feedback/start-food -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-start-food -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-berry-regression -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'P109FoodIntegrationTests|NeighbourProgressionRepairTests' -OutputPath Artifacts/DEV01Feedback/berry-integration -TimeoutSeconds 1200
```

### Доступная пища и восстановление runner, 03.11, 03:45

- На дне 5, 07:56 партия сохранена в ячейку 2 через обычный Player, коды не использовались.
  Корзина была заполнена 40/40 (снаряжение, шкуры, волокно), 60 ягод лежали на земле.
  Назначение ночного переносчика не могло обойти отсутствие свободной складской ёмкости.
  D-250 дополнен fallback питания из съедобных наземных запасов; CarryLoad исключён.
- ground-food: 20/20 PASS, 12,07 s; проверена реальная цепочка Scheduler → Eat → списание одной
  порции → восстановление сытости, отсутствие списания CarryLoad и повторного потребления.
- berry-integration был отменён: слишком широкий фильтр захватил длительные соседние сценарии.
  Unity Test Framework выдал ошибку восстановления сцены при отмене; итогового XML нет, это НЕ PASS.
  После штатного закрытия Editor runner записал exit 2 / interrupted process. Добавлен IErrorCallbacks,
  чтобы будущая ошибка Test Framework завершала команду ошибкой, а не оставляла очередь занятой.
  Выполнен один необходимый перезапуск persistent Editor (38240 → 37596); остальные команды переиспользуют его.
- compile-ground-food: PASS, 0,79 s, 0 ошибок/0 предупреждений после компиляции при старте Editor.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-ground-food -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'P109FoodIntegrationTests|ProductionAndFarmingTests' -OutputPath Artifacts/DEV01Feedback/ground-food -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-ground-food -TimeoutSeconds 1200
```

### Продолжение native-маршрута, 03.11, 04:20

- build-ground-food PASS: 107,10 s, 0 ошибок / 33 предупреждения; fingerprint
  CAF635B84C4D7B30DC015738559440C6EAB198CAFD0716C3D7C474CBA0A80861.
- День 5: после загрузки фактическое питание списало наземные ягоды (60 → 50), затем обычная
  добыча пополняла запас; до дня 12 живы все 10 жителей. Никаких выдач ресурсов/кодов.
- День 5: выбран мирный исход семейного спора; UI показал +12 к отношениям и +10% работы/
  исследований на 2 дня. К дню 11 соответствующие encouraged уже завершены (state 5).
- День 8: месячный план после открытия cooking показал 900 порций (10 × 3 × 30), чередование
  жареной рыбы/жареного мяса/приготовленных ягод. Ручные цели не изменились.
  Скриншот player/15-month-menu-900.png. Дополнительно вручную поставлена цель cooked_berries=30.
- Исследованы controlled_fire, cooking, woodworking, primitive_weapons; очередь автоматически
  переходила к следующему пункту. На дне 12 militia 34,7%, ожидают shelter_construction/reliable_housing.
- Построена дополнительная корзина 3 брёвна/5 волокон; вместимость выросла с 40 до 80.
  Поставлен костёр; назначен Арен-ремесленник. Готовка пока ещё не подтверждена.
- Ночь дня 11, 02:53: сохранение через UI подтвердило две занятые кровати дневных жителей;
  два других жильца на ночной смене не занимали кровати. player/16-night-sleep-rain.png.
- Найдена остановка доступа к Дому знаний после новой постройки рядом. IndoorWorkplaceService
  теперь повторно вычисляет вход при неудачном пути/прибытии вне входа; прибытие всё ещё обязательно.
  Также OnBuildConfirmed обновляет preview по позиции клика до commit (callback раньше Update).
- HUD свободных жителей теперь исключает занятых автоматическим действием. Compile-hud-idle:
  PASS 59,02 s, 0 ошибок / 47 предупреждений. Compile-arrival-retry PASS 96,61 s, 0 / 54.
- Start-food-playmode сначала FAIL: прежний fixture ожидал 1 кадр и получал null composition.
  Start-food-playmode-ready и start-food-background FAIL: прямой LocalGameplay bootstrap не
  завершил подготовку за 600 s. Это не успешные тесты. Fixture переведён на обычный MainMenu →
  NewGame seed 1380062136, как уже успешные runtime проверки; повторный результат будет записан отдельно.
- Compile-test-readiness FAIL: transient Windows File.Replace error 1175 при занятом файле.
  Добавлен ограниченный retry атомарной замены для 32/33/1175 без неатомарного fallback.
  Compile-test-readiness-retry PASS; heartbeat-lock regression подлежит повторной проверке.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames StarterStorageProvidesCookedRationsAndBerriesBridgeEarlyResearch -TestMode PlayMode -OutputPath Artifacts/DEV01Feedback/start-food-background -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-hud-idle -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-arrival-retry -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-arrival-test -TimeoutSeconds 1200
```

### Блокеры длинного маршрута: производство и порча еды, 03.11, 04:33

- Native Continue дня 12 выявил `World-pile state has no matching canonical inventory`:
  в исходной ягодной куче находился spoiled_food после естественной порчи. Исправлена
  согласованность WorldPileService и порчи: отдельная стабильная куча отходов на той же
  координате, без утраты количества. Допускается восстановление только такого известного
  побочного продукта, прочие ошибки снимка по-прежнему отвергаются.
- Сохранение также показало тысячи повторных неудачных доставок в ProductionInput
  недостроенного костра: они перехватывали работников приоритетом High. Теперь непрерывные
  производственные доставки возникают только для действующего здания. Строительные
  материалы по-прежнему доставляются до завершения стройки.
- `compile-logistics-decay` FAIL — опечатка имени метода в новом тесте, исправлена.
  `compile-logistics-decay-fixed` PASS, 31.34 s, 0 errors / 7 warnings.
- `logistics-decay-regression` **23/23 PASS**, 3.36 s. Проверено отсутствие заявок до
  завершения, их появление после него, сохранение количества и повторный restore испорченной еды.
- `start-food-menu-route` FAIL — стандартный UnityTest timeout 180000 ms, хотя host timeout
  был 1200 s. Тесту явно назначен Timeout(1200000); это не изменение игровой загрузки.
- `arrival-runner-regression` **28/28 PASS**, 3.52 s: повторный вход после изменения пути,
  неверное раннее прибытие и атомарная запись heartbeat при Windows sharing lock.
- `build-arrival-retry` PASS, 48.12 s, 0 errors / 2 warnings. Заменяется сборкой ниже.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'ResearchFrameworkTests|RunnerHeartbeatRetainsCompleteStateWhileWindowsReaderHoldsFile' -OutputPath Artifacts/DEV01Feedback/arrival-runner-regression -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-arrival-retry -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames StarterStorageProvidesCookedRationsAndBerriesBridgeEarlyResearch -TestMode PlayMode -OutputPath Artifacts/DEV01Feedback/start-food-menu-route -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-logistics-decay -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-logistics-decay-fixed -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'P109BuildingProductionTests|ResourceInventoryTests' -OutputPath Artifacts/DEV01Feedback/logistics-decay-regression -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-logistics-decay -TimeoutSeconds 1200
```

### Каталог наземных ресурсов и стабильная сборка, 03.11, 04:54

- start-food-timeout-fixed: PlayMode 1/1 PASS, 515,40 s. Ранее тест был ограничен стандартными
  180 s UnityTest; явный Timeout 1200 s позволяет дождаться обычного MainMenu → NewGame.
- На дне 12 исследования выросли с 34,7% до 56,9%; костёр завершён к дню 13 за реальные материалы.
  Continue восстановил смешанную наземную пищу без потери/дублирования после D-251.
- Найдена missing-view ошибка pile_spoiled_food; добавлен вид мешка из уже импортированных
  лицензированных props. Проверка полного каталога выявила также 15 пропущенных ссылок ресурсов.
  Добавлены варианты упакованных товаров/сырья из существующих props, новых зависимостей нет.
- waste-view-catalog и complete-pile-catalog: FAIL (dressed_carcass). Unity не прочитал добавленный
  хвост YAML с пустыми строками; после нормализации asset pile-yaml: 1/1 PASS, 2,45 s.
- build-pile-catalog: FAIL после автоматической перезаписи UMA AssetIndexerProject.asset во время
  сборки; проверка неизменности исходников не отключалась. Повтор build-pile-catalog-stable:
  PASS, 27,54 s, 0 ошибок/0 предупреждений. Fingerprint
  B7342D3C8593CC482FD7123C81F582D4ADE86B7A7596B99A85326400D8D42964.
- heartbeat-retry: 1/1 PASS, 16,84 s; занятый Windows reader не повреждает атомарный state.
- Слот 2 сохранён обычным UI: день 13, 09:38, 10 живых жителей. Слоты 1/3 не менялись.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames StarterStorageProvidesCookedRationsAndBerriesBridgeEarlyResearch -TestMode PlayMode -OutputPath Artifacts/DEV01Feedback/start-food-timeout-fixed -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-pile-yaml -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames WM15PropReplacementTests.EveryCanonicalResourceHasAnImportedView -OutputPath Artifacts/DEV01Feedback/pile-yaml -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-pile-catalog-stable -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames RunnerHeartbeatRetainsCompleteStateWhileWindowsReaderHoldsFile -OutputPath Artifacts/DEV01Feedback/heartbeat-retry -TimeoutSeconds 1200
```

### Прибытие в казарму и топливо кухни, 03.11, 05:16

- День 15, 17:06: Дорану через обычное контекстное меню казармы назначено обучение. Он прошёл
  от дома к зелёному шатру, статус изменился с «идёт в казарму» на «обучение в казарме» после
  прибытия. К 22:30 обучение завершено; профессия — военные, затрачены 2 единицы снаряжения.
  player/17-barracks-arrival.png; запись completed=true в militaryCampaign.training слота 2.
- На дне 16 reliable_housing 46,2%; militia/shelter_construction завершены автоматически в очереди.
  На дне 19 сохранены все 10 живых жителей, обычные запасы и текущее развитие без читов.
- Топливо костра: отопление забирало единственное доставленное для рецепта полено. Добавлен
  физически доставляемый резерв +1 полено для непрерывного производства (D-252).
- compile-fire-reserve FAIL: неверное использование Value у ID; compile-fire-reserve-fixed FAIL:
  nullable ResourceId в новом тесте. Исправлены. compile-fire-reserve-ready PASS 33,51 s, 0/7.
- fire-reserve-regression 10/10 относится к прежней сборке тестов, НЕ проверяет новый сценарий.
  После явной компиляции fire-reserve-compiled: **11/11 PASS**, 2,55 s, включая отопление + готовку.
- build-fire-reserve: PASS 48,11 s, 0 ошибок / 2 предупреждения.
- В списке «Люди» убраны остаточные жажда и инверсное обозначение голода; показывается сытость.
  Верхняя карточка обучаемого различает путь к казарме и физическое нахождение внутри.
- Ограничение native automation: ввод Escape/Home через sky.press_key не вызвал InputSystem
  реакции в собранном Player. Поэтому native-клавиатурную приёмку этими вызовами не утверждаем.
  Отдельные реальные InputSystem runtime проверки Escape/W/очередей прошли; native мышь,
  меню, прокрутка, перемещение по приказу и физический вход наблюдались.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-fire-reserve-ready -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames P109BuildingProductionTests -OutputPath Artifacts/DEV01Feedback/fire-reserve-compiled -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-fire-reserve -TimeoutSeconds 1200
```
- Соседняя экономика в обычном сохранении дня 19: произведено 268 ягод, потреблено 256 еды;
  исследователей/активного проекта нет, без Дома знаний фиктивного исследования нет. Это
  подтверждает автономную добычу/питание, но не весь цикл развития соседней державы/войны (DEV03).
- Текущая сводка Artifacts/DEV01Feedback/latest-test-summary.json: 290 уникальных целевых
  проверок имеют последний результат Passed. Не полный suite и не массовый performance gate.

### Гражданская работа правителя и повторная приёмка, 03.11, 05:44

- На дне 23 построена новая stone_age_hut за реальные материалы; до этого Эльда переведена
  из фермеров в строителей обычным списком групп. player/19-new-hut-day23.png.
- Найден независимый блокер приготовления: почётный Generalissimo у правителя Арена запрещал
  гражданскую работу, хотя он назначен ремесленником. IsOnMilitaryDuty проверяет текущую профессию
  и незавершённое обучение, а не наличие исторического звания. Регрессия также проверяет,
  что смена профессии посреди обучения не освобождает жителя от курса (D-253).
- Прокрутка вложенного people-roster теперь сохраняется отдельно от основной панели.
  Поиск обновляет существующий список, не пересоздаёт поле; фокус внутреннего text input учитывается.
  Подсказка размещения поднята над нижней палитрой; русский поиск учитывает показанное имя.
- compile-civilian-duty PASS 109,58 s, 0 ошибок / 141 предупреждение.
- civilian-duty-regression: **33/33 PASS**, 3,44 s; сводка теперь **315 уникальных Passed**.
- build-civilian-duty PASS 51,37 s, 0 ошибок / 54 предупреждения;
  fingerprint 217648096DAC48FDB078DED790A8E843E12E10D11305FC0373E94500F33B16FC.
- compile-people-search PASS 72,77 s, 0 ошибок / 2 предупреждения. Это дополнительное
  отображение/поиск русских имён требует включения в заключительную сборку.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-civilian-duty -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 test -GroupNames 'P107MilitaryTrainingCaptureTests|P83WorkGroupTests|MilitaryUnitTests' -OutputPath Artifacts/DEV01Feedback/civilian-duty-regression -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Feedback/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-civilian-duty -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-people-search -TimeoutSeconds 1200
```

### Финальная сборка и приготовление, 03.11, 06:00

- На дне 25 обычного Player произведено 15 cooked_berries, к сохранению в начале дня 27 — 45.
  Это реальные выходы рецепта: сырьё/дрова доставлены жителями, cheatsUsed=false, sequence=0.
  Контрольная копия сохранения: player/day27-before-final.xml. Ручная цель cooked_berries=30 сохранена.
- Простого восстановления scrollOffset оказалось недостаточно для пересозданного ListView:
  native проверка обнаружила сброс после layout. Реестр теперь сохраняет сам экземпляр списка;
  меняются видимые подписи и отдельный блок подробностей. Перестроение списка — только при
  изменении состава/поиска; русские имена участвуют в поиске и сортировке.
- compile-persistent-roster PASS 52,67 s, 0 ошибок / 141 предупреждение.
- build-final PASS 80,49 s, 0 ошибок / 54 предупреждения. Unity 6000.6.4f1, build GUID
  cee096fd5d0341528e1969bf954baeca, fingerprint
  6045E7B9E3AF09C1B642AB37D1B9E55FB6F287449AE9E8E6F4154476F92410FD.
- Сборка создана в Builds/DEV01Final и побайтно скопирована в прежний Builds/DEV01Feedback.
  Game.Presentation.dll в обоих каталогах SHA256
  479FF3CC7AF962C3EEF83D73FB46829D95EDA6EFD11E88DF90C7E8100FB76CD1.
  Native приёмка продолжается из Builds/DEV01Feedback. Новый сетевой запрос Windows был отменён:
  разрешения брандмауэра не менялись, входящий сетевой доступ не предоставлялся.
- Сохранение дня 27 загружено финальной сборкой через обычное «Продолжить»; ресурсы, постройки,
  профессии и реальное производство восстановлены. На дне 27 остаются 10 живых жителей.

```
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 compile -OutputPath Artifacts/DEV01Feedback/compile-persistent-roster -TimeoutSeconds 1200
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 build -BuildPath Builds/DEV01Final/Serenity.exe -OutputPath Artifacts/DEV01Feedback/build-final -TimeoutSeconds 1200
```

### Финальная native-проверка списка жителей, 03.11, 06:12

- В финальном Player нижняя часть реестра жителей сохранена при непрерывной симуляции:
  день 27 22:01 → день 28 03:39, нижние Рован/Села/Тарин/Эльда остаются на месте.
  Доказательство: player/20-roster-scroll-persists.png.
- Поиск «Эльда» возвращает одного нужного жителя; текст и результат сохраняются при обновлении
  симуляции до дня 29. Доказательство: player/21-russian-roster-search.png.
- Прежний snapshot/restore scrollOffset не объявлен достаточным: именно сохранение экземпляра
  ListView прошло повторную native-проверку. В итоговой сборке находятся эти последние изменения.

### Закрытие обычного месяца, 03.11, 06:14–06:18

- День 31 03:15, пауза обычной кнопкой; 10 жителей, HUD еда 54, материалы 65, ремесло 24,
  снаряжение 14, хозяйство 27. Сохранено обычной кнопкой в слот 2, подтверждение показано в UI.
- Сохранение: calendarTicks=26037105000000, paused=true, cheatsUsed=false, sequence=0.
  7 действующих player-зданий: 2 stone_age_hut, 2 storage_basket, research_house,
  military_barracks и campfire. Все constructionState=1. Training Дорана completed=true.
- Завершённые технологии: basic_survival, controlled_fire, cooking, gathering, militia,
  primitive_weapons, reliable_housing, shelter_construction, stone_tools, woodworking.
  Активный проект и очередь пусты после их штатного завершения; не подставлялись через API.
- player/22-month-completed.png, player/23-day31-save.png, player/day31-final.xml,
  player/Player-final.log сохранены локально в Artifacts/DEV01Feedback.
- Штатное закрытие Player через Alt+F4 после сохранения; Input System завершился корректно.
  При выходе есть диагностика двух Persistent allocations без стека владельца; сохранена как долг DEV04.
- `pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/UnityRunner.ps1 stop -QuitOwnedEditor`
  вернул succeeded/exit 0, команда 20261103031232376-e33b23808d3041aeb002c80bc304c187.
  После завершения проверка процессов подтвердила отсутствие Unity, Serenity и Ollama.
- DEV01 закрывается отдельным коммитом после обновления документов. DEV02 подготовлен, не запущен.
  Пользователь разрешил безопасное выключение с задержкой 60 секунд после сохранения Git-результата.
