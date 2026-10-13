# P2 Главное меню и начало партии

## Результат

P2 завершён 2026-10-13. `Assets/Scenes/MainMenu.unity` — первая сцена Windows Player. Она показывает русские «Продолжить», «Новая игра», «Загрузить», «Настройки» и «Выход», поддерживает возврат/отмену и не включает developer-панели в обычном маршруте.

«Продолжить» выбирает только новейшую пригодную ячейку. Пустые и повреждённые ячейки объясняются отдельно. Для загрузки свежего процесса меню читает world seed из уже валидированного snapshot, создаёт тот же детерминированный набор StableEntityId и только затем вызывает существующий `SaveCoordinator.Load`. Ошибка между просмотром и загрузкой возвращает в меню с русским сообщением; новая партия молча не создаётся. Старые сохранения без сохранённого seed остаются нетронутыми и явно недоступны для этого startup-маршрута.

Новая игра передаёт seed существующим `NewGameLaunchState`, `NewGameConfiguration` и `NewGameBootstrap`. Создание внешности правителя остаётся П4; П2 не вводит временную вторую модель героя.

## Настройки и выход

`UserSettingsStore` хранит отдельный атомарный `user-settings.json`, а не поля snapshot v15. Реализованы общая громкость, 1280×720–2560×1440, окно/полный экран, уровень качества, масштаб UI 100–150%, скорость камеры 50–200% и переназначение движения камеры, паузы, строительства и перехода к управлению персонажем. Изменения в настройках применяются только кнопкой «Применить»; «Отмена» возвращает без записи.

Кнопка «Главное меню» в партии вычисляет SHA-256 текущего канонического snapshot и сравнивает его с baseline последнего успешного Save/Load. Новый или изменившийся мир показывает «Выйти без сохранения» и «Отмена». Сравнение выполняется только при запросе выхода, не каждый кадр.

## Проверки

- `Logs/P2-editmode-final.xml`: P2 EditMode 4/4.
- `Logs/P2-playmode-final.xml`: P2 PlayMode 2/2.
- `Logs/P2-affected-editmode-final.xml`: Gameplay UI slots + persistence 38/38.
- `Logs/P2-affected-playmode-final.xml`: Gameplay UI 6/6, P1 1/1, U30A 5/5, U31 integration 2/2; всего 14/14.
- `Logs/P2-build-delivery.log`: Windows x64 Mono Development, `U00_BUILD_RESULT: Succeeded`, errors=0, warnings=8. Новых ошибок нет; предупреждения относятся к четырём командно-строчным вызовам `FindFirstObjectByType` в P2 smoke-инструменте и двум унаследованным U29 obsolete-диагностикам, повторяемым между компилируемыми сборками. Обычный игровой маршрут от smoke-инструмента не зависит.
- `Logs/P2-player-smoke-final4/p2-player-smoke.json`: PASS при 1280×720, seed 2026101302. Маршрут: чистое меню → настройки/назад → New Game → developer UI скрыт → предупреждение о несохранённом → Save slot 1 → меню → Continue доступно → восстановлена та же SessionId.
- `p2-main-menu.png` и `p2-loaded-game.png` из final4 визуально проверены: кириллица читается, меню не обрезано, кнопка «Главное меню» не пересекается с Development Build watermark.

Точные финальные команды:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P2-editmode-final.log' -runTests -testPlatform EditMode -testCategory P2 -testResults 'C:\serenity_game\Logs\P2-editmode-final.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P2-playmode-final.log' -runTests -testPlatform PlayMode -testCategory P2 -testResults 'C:\serenity_game\Logs\P2-playmode-final.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P2-affected-editmode-final.log' -runTests -testPlatform EditMode -testFilter 'Game.Tests.Editor.GameplayUiTests;Game.Tests.Editor.PersistenceTests' -testResults 'C:\serenity_game\Logs\P2-affected-editmode-final.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P2-affected-playmode-final.log' -runTests -testPlatform PlayMode -testFilter 'Game.Tests.PlayMode.GameplayUiPlayModeTests;Game.Tests.PlayMode.U30ACompliancePlayModeTests;Game.Tests.PlayMode.U31IntegrationPlayModeTests;Game.Tests.PlayMode.P1RussianLocalizationPlayModeTests' -testResults 'C:\serenity_game\Logs\P2-affected-playmode-final.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P2-build-delivery.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath 'Builds/P2/Serenity.exe'
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Logs\P2-player-smoke-final4\saves'
$env:SERENITY_SETTINGS_PATH='C:\serenity_game\Logs\P2-player-smoke-final4\settings\user-settings.json'
& 'C:\serenity_game\Builds\P2\Serenity.exe' -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile 'C:\serenity_game\Logs\P2-player-smoke-final4\Player.log' -p2Smoke -p2SmokeSeed 2026101302 -p2SmokeOutput 'C:\serenity_game\Logs\P2-player-smoke-final4'
```

## Ограничения и следующий этап

P2 не создаёт редактор внешности (П4), не меняет художественную сцену (П3) и не закрывает U29/U30/U31. Два диагностических smoke-каталога сохранены: `P2-player-smoke-final` выявил необходимость bootstrap по сохранённому seed; `P2-player-smoke-final2` функционально загрузил ту же сессию, но выявил слишком строгий smoke-критерий «мир остаётся чистым после первого живого кадра». Оба исправления проверены final3 и повторно final4. Unity batchmode несколько раз ожидал недоступный служебный endpoint по 300 секунд; delivery build после этих timeout завершился exit 0 и с обновлённым provenance. Две поздние попытки повторить тот же build завершились на внутренней инициализации Unity до вызова `U00Build` и не изменили Player; после возврата эквивалентного smoke-only API к уже собранному варианту final4 снова прошёл на delivery-артефакте.

Следующая активная задача — П3. Финальная визуальная приёмка эталонной сцены остаётся за пользователем; автономный чат может подготовить один обратимый вариант из уже разрешённых локальных ресурсов.
