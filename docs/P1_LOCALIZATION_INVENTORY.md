# P1 — инвентаризация русификации

## Граница

Русский (`ru-RU`) — язык по умолчанию для всех видимых игровых и разработческих поверхностей. Канонические ID, имена классов, XML сохранений и значения командной строки остаются неизменными. Игровой интерфейс не показывает сырые enum, ID контента или технические исключения; режим разработчика может показывать стабильные ID, но его подписи и состояния также русифицированы.

## Единый источник строк

`Assets/Game/Presentation/Localization/RussianTextCatalog.cs` содержит стабильные ключи:

- `ui.*` — HUD, навигация, панели, модальные окна, подсказки, причины отказа, уведомления, победа и поражение;
- `content.*` — ресурсы, постройки, рецепты, технологии, состояния здоровья, оружие, места и профессии;
- `event.*` — заголовки, описания, варианты выбора и исходы событий;
- `enum.*` — видимые состояния домена и симуляции;
- `debug.*` — подписи диагностических поверхностей;
- `name.*` — отображаемая русская форма поставляемых имён и названий династий.

Отсутствующий ключ один раз записывается в журнал как `Missing ru-RU translation key`, а игрок получает нейтральное «Перевод недоступен» без технического ключа. Числа, проценты и краткие длительности форматируются культурой `ru-RU`; множественные формы проверены для 1/2/5/11/21.

## Покрытые поверхности

| Поверхность | Реализация |
|---|---|
| HUD, время, погода, режим, пауза/скорость, уведомления | `GameplayUiController` |
| Строительство и конкретная нехватка ресурсов | `GameplayUiController`, `BuildingDebugSelector` |
| Хозяйство, производство, склады, еда | `GameplayUiController`, `ProductionDebugOverlay`, `ResourceDebugOverlay` |
| Исследования и причины блокировки | `GameplayUiController`, `ResearchDebugOverlay` |
| Рабочие группы и задания | `GameplayUiController`, `WorkDebugOverlay` |
| Персонаж, здоровье, семья, династия, наследование | `GameplayUiController`, `HealthFamilyDebugOverlay`, `DynastyDebugOverlay` |
| Войска, бой, снабжение, TPS | `GameplayUiController`, `MilitaryUnitDebugOverlay`, `CombatDebugOverlay`, `ThirdPersonControlController` |
| Карта мира, места, переходы и сражения | `GameplayUiController`, `GlobalMapPresentationController`, `GlobalMapRuntimeBridge` |
| События и история | `GameplayUiController` + ключи `event.*` |
| Save/Load/New Game и метаданные ячеек | `GameplayUiController`, `LocalGameplaySaveFacade` |
| Погода, Tier1 и стратегический ИИ | `WeatherDebugOverlay`, `Tier1AiDebugOverlay`, `StrategicAiDebugOverlay` |

## UX-правила P1

- Недоступная постройка сообщает требуемое исследование; нехватка сообщает ресурс и количество.
- Ошибки команд не выводят `Exception.Message`; подробность остаётся в журнале, игрок получает безопасное русское действие для исправления.
- Стартовые вымышленные имена транслитерируются только на границе отображения, поэтому сохранения и детерминизм не меняются.
- UI поддерживает масштаб 100–150%; контроль выполнен при 1280×720 и 125%.
- Режим разработчика включается отдельно через F10 и не является частью обычного маршрута.

## Доказательства

- `Logs/P1-editmode.xml`: 8/8.
- `Logs/P1-playmode.xml`: 1/1 headless.
- `Logs/P1-playmode-gpu.xml`: 1/1 D3D11.
- Затронутые наборы: Gameplay UI 6/6, Events 4/4, U30A 5/5, U31 Integration 2/2.
- `Builds/P1/Serenity.exe`: Windows x64 Mono Development build, `errors=0`, `warnings=4` (две унаследованные U29 obsolete-API диагностики, выведенные для двух сборок).
- `Logs/P1-player-smoke-final3/u31-player-smoke.json`: normal Player, New Game → Save → Load, PASS.
- `Logs/P1-player-smoke-final3/u31-player-smoke.png`: визуальный кадр 1280×720 из финальной сборки после проверки и исправления контраста полей.

Первый скрытый Player-smoke также функционально прошёл, но его PNG был полностью чёрным из-за скрытого окна и не используется как визуальное доказательство.

## Точные финальные команды

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P1-final-editmode.log' -runTests -testPlatform EditMode -testCategory P1 -testResults 'C:\serenity_game\Logs\P1-editmode.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P1-final-playmode.log' -runTests -testPlatform PlayMode -testCategory P1 -testResults 'C:\serenity_game\Logs\P1-playmode.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P1-playmode-gpu.log' -runTests -testPlatform PlayMode -testCategory P1 -force-d3d11 -testResults 'C:\serenity_game\Logs\P1-playmode-gpu.xml'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\serenity_game' -logFile 'C:\serenity_game\Logs\P1-final-build-contrast.log' -quit -buildTarget Win64 -executeMethod U00Build.WindowsDevelopment -serenityBuildPath Builds/P1/Serenity.exe
```

Затронутые PlayMode-классы запускались последовательно через `-testFilter`: `Game.Tests.PlayMode.GameplayUiPlayModeTests`, `EventSystemPlayModeTests`, `U30ACompliancePlayModeTests`, `U31IntegrationPlayModeTests`. Финальный Player запущен с `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -u31Smoke -u31SmokeSeed 101004 -u31SmokeOutput C:\serenity_game\Logs\P1-player-smoke-final3`; exit 0, JSON `passed=true`.
