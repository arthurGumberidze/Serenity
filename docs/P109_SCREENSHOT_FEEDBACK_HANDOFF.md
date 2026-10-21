# Исправления по скриншотам от 21 октября

Область P10.9: шесть разных скриншотов (последний приложен дважды) и файл
`C:/Users/Lenovo/Desktop/Задачи и промт.txt`. Повторные старые промпты не выполнялись.
Ручную проверку в Player проводит пользователь; автоматические проверки ниже её не заменяют.

## Исправления

| Сообщение | Причина и изменение |
| --- | --- |
| Перенос задом, груз отдельно | Авторская ось рабочих FBX противоположна оси навигации. Прогон на реальном UMA показал dot направления −0.997; после нормализации — +0.99. Рабочие клипы используют ориентацию Humanoid и запечённое перемещение; груз обновляется по середине между кистями в общем LateUpdate |
| «Шишки» в воде/на карте | В новой карте оставались цилиндры воды, капсулы гор и NavMesh-ограничители старой карты P10.2. Они выключаются перед инициализацией Terrain, без изменения исходной сцены |
| Строительство внутри скал и на плохом рельефе | Проверялась одна точка и занятость другими зданиями. Теперь проверяются центр/углы каждой клетки повёрнутого footprint, перепад высот и коллайдеры препятствий; подтверждение повторяет проверку |
| Персонаж под землёй | Убран независимый root motion, вертикальный корень рабочих клипов привязан к стопам. Дополнительно исправлено облегчённое движение: оно обновляло X/Z, но сохраняло старую высоту на склонах |
| Нет наведения у ног | Узкая капсула тела не покрывала вынесенную вперёд стопу. Добавлен отдельный небольшой trigger выбора ног, без влияния на навигацию |
| Рыба слипается плоскими силуэтами | Вместо FX-маски на Quad — общие объёмные низкополигональные меши, отдельные хвосты. Фазы движения равномерны, радиусы чередуются; разные скорости больше не собирают стаю в одну точку. Пауза останавливает движение |
| ПКМ не главнее старых работ | Обычная доступная команда прерывает текущую работу и отменяет прежнюю очередь, включая незавершённый перенос с безопасной выкладкой груза. Shift добавляет в очередь. Невозможная команда не уничтожает очередь. Потребности не откладывают непосредственный приказ; недееспособность и внешнее владение персонажем не отменены |

Рыбки — временная проектная геометрия, не новая импортированная модель.
44 канонических источника рыбы, береговые рабочие точки и физические кучки улова сохранены.
Все запасы остаются в существующей канонической системе; нового личного инвентаря нет.
Большие здания не выравнивают Terrain автоматически. Слишком неровная площадка запрещается.

## Ручной маршрут

Сборка этого прохода: `C:/serenity_game/Builds/P109ScreenshotFeedback/Serenity.exe`.
Открыть именно её и выбрать **новую игру**, не прежний `.exe` и не старое сохранение.
Приёмка этой сборки ещё не выполнена пользователем.

1. В лагере на x1 наведите указатель на стопы и рядом с вынесенной ногой идущего жителя.
   Круг должен появляться; щелчок выбирает этого жителя. Проверьте женщин и мужчин,
   остановку после ходьбы и отсутствие проваливания в землю.
2. Выберите одного взрослого, ПКМ по дереву. После рубки дождитесь переноса брёвен.
   Он должен идти лицом по направлению движения; груз находится между руками, не за спиной.
   На x1 позу легче оценить, на x10 быстрее дождаться результата работы.
3. Во время переноса отправьте этого жителя обычным ПКМ на землю в стороне.
   Груз должен остаться кучкой в месте прерывания, старая очередь отмениться, новый приказ
   выполняться первым. После выполнения всех приказов разрешён обычный автоматический сбор
   свободным жителем. Отдельно проверьте Shift+ПКМ: это добавление, не отмена.
4. Откройте строительство доступного здания. Если шалаш закрыт, сначала завершите
   исследование строительства убежищ в панели исследований. Наводите preview на большой
   камень, дерево, крутой склон, воду, край другого здания и ровную свободную землю.
   Первые места запрещены; последнее разрешено при наличии технологии и материалов.
   Попробуйте развернуть прямоугольную постройку краем внутрь камня — тоже должно запрещаться.
5. Уведите камеру к реке и осмотрите место со второго скриншота. Старых вертикальных
   капсул и овальных цилиндров воды не должно быть. География остаётся 1 × 1 км.
6. Пруд рядом с лагерем расположен восточнее и севернее старта, около X=88, Z=72.
   В каждом круге должны быть отдельные объёмные рыбки и движения хвостов, не плоская
   наложенная полоса. Пауза останавливает рыб. ПКМ по косяку отправляет рыбака на берег;
   после вылова появляется кучка рыбы на суше, которую можно оставить или отнести.

Если что-то не совпадает, достаточно указать номер пункта и прислать короткий фрагмент
из этой сборки. Сохранение старого мира не нужно открывать ради проверки.

## Строения и готовка

Аудит существующих компонентов, архитектура, все 25 объектов, местные художественные
кандидаты, зависимости, сложность и возможные параллельные задачи:
[План строений](P109_STONE_AGE_BUILDINGS_PLAN.md).
Костёр, кухонные рецепты и остальные новые здания ждут согласования этапов B/C согласно
последнему разделу исходного документа. Они не заявляются реализованными этим исправлением.

## Проверки и воспроизведение

Unity: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`.
Все тесты использовали переменную процесса:
`SERENITY_SAVE_DIRECTORY=C:/serenity_game/Artifacts/P109/screenshots-20261021/test-saves`.
Unity запускался через PowerShell `Start-Process -WindowStyle Hidden -PassThru` и
`WaitForExit()` у возвращённого процесса. В `runTests` нет `-quit`; авторинг/сборка используют `-quit`.

```text
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod Game.Infrastructure.Editor.P105QuaterniusFoundation.RepairWorkRootTransforms -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\animation-import.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.P103ContextCommandTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.P109HarvestTests;Game.Tests.Editor.P109CompactWorldTests;Game.Tests.Tier1UtilityAiTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.WorkGroupAndJobTests -testResults C:\serenity_game\Artifacts\P109\screenshots-20261021\editmode-final.xml -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\editmode-final.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.P109ScreenshotFeedbackPlayModeTests;Game.Tests.PlayMode.P109PlayablePatchTests;Game.Tests.PlayMode.P109CompactWorldPlayModeTests;Game.Tests.PlayMode.BuildingPlacementPlayModeTests.ResidentApproachesAndBuildsInsteadOfInstantCompletion -testResults C:\serenity_game\Artifacts\P109\screenshots-20261021\playmode-regression.xml -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\playmode-regression.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.BuildingPlacementPlayModeTests.ResidentApproachesAndBuildsInsteadOfInstantCompletion -testResults C:\serenity_game\Artifacts\P109\screenshots-20261021\playmode-construction.xml -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\playmode-construction.log
-batchmode -nographics -projectPath C:\serenity_game -runTests -testPlatform EditMode -testFilter Game.Tests.P103ContextCommandTests;Game.Tests.Editor.BuildingFoundationTests;Game.Tests.Editor.P109HarvestTests;Game.Tests.Editor.P109CompactWorldTests;Game.Tests.Tier1UtilityAiTests;Game.Tests.Editor.ResourceInventoryTests;Game.Tests.WorkGroupAndJobTests -testResults C:\serenity_game\Artifacts\P109\screenshots-20261021\editmode-release.xml -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\editmode-release.log
-batchmode -nographics -quit -projectPath C:\serenity_game -executeMethod P109StarterNatureAuthoring.BuildScreenshotFeedback -logFile C:\serenity_game\Artifacts\P109\screenshots-20261021\build.log
```

Промежуточный `playmode-first.xml`: 1/3. Он обнаружил противоположную ориентацию Carry;
вторая ошибка была в тесте — изменение скорости не снимает паузу. После переимпорта
и явного Resume адресный `playmode-corrected2.xml` прошёл 4/4. Два промежуточных запуска
не компилировались из-за API NUnit/пространства имён нового теста; исправлены до окончательного прогона.
`editmode-final.xml`: 74/74, без ошибок тестов.
`editmode-accepted.xml`: 75/75 после добавления проверки отмены активного переноса;
команда аналогична EditMode выше, только имена XML/log — `editmode-accepted`.
Расширенный `playmode-regression.xml`: 15/16. Единственный отказ — фиксированная точка
строительного теста (15, 15) теперь корректно запрещена проверкой препятствий.
Тест ищет действительно свободную доступную площадку вместо обхода валидации.
Повторный `playmode-construction.xml`: 1/1, включая подход работника, прогресс и завершение.
Это результаты общего и адресного запусков, а не один общий прогон 16/16.
После них сохранён выбор тела у моделей без исходного коллайдера: область ног не должна
блокировать создание основного коллайдера. Проверка повторного Bind включена в `editmode-release`.
Финальный `editmode-release.xml`: 76/76, exit 0.
Сборка `Builds/P109ScreenshotFeedback/Serenity.exe` успешна: exit 0, 0 ошибок,
53 предупреждения, GUID `ac1af2b6526f4451ae7dd2d1b1822398` (подробности в `build.log`).
Windows Player вручную не запускался; результат тестов не является визуальной приёмкой.

Финальная проверка SHA-256 после сборки: 44/44 защищённых файлов совпадают с
`Artifacts/P109/compact-world/protected-before.json`, включая восемь старых сохранений,
ProjectSettings, Packages и пять пользовательских изменённых материалов.
Никакой push не выполнялся. P10.9 остаётся PARTIAL до ручной приёмки; P10.10 не начат.
Сборка использует текущее рабочее дерево, включая пять ранее изменённых пользователем
материалов. Они сохранены без изменений и не входят в коммит этого прохода.
