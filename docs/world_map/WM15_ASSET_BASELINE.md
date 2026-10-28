# WM15 — tracked asset baseline

Дата фиксации: 2026-10-28.

## Зачем

Unity build fingerprint должен считать изменениями только новую работу, а не повторно хешировать несколько гигабайт однажды импортированных исходников. Поэтому используемый набор сторонних ассетов фиксируется в Git/LFS как baseline, а архивы, demo и неиспользуемые части пакетов исключаются из `Assets` и `Packages`.

## Что входит в baseline

- статическое замыкание зависимостей из `Artifacts/WM15LibraryAudit/reachable-static.json` для `ANGRY MESH`, `NatureManufacture Assets`, `Snow_mountain` и `Kevin Iglesias`;
- исходные heightmap-файлы, используемые `Assets/Game/Art/WM15Current/TerrainSample`;
- runtime-шейдер `FX/ProceduralWater` без demo-сцены, материалов-примеров и `.unitypackage`-архивов.

GUID и соответствующие `.meta` сохранены. Каталоги `Polytope Studio`, уже находившиеся под Git, не дублируются новым baseline.

## Что исключено из build scope

Исходники не удалены безвозвратно. Они перемещены в игнорируемые Git каталоги:

- `serenity_games_assets/SourceArchives/Assets/` — исходные импорты `25102026`, `26102026`, `28102026`;
- `serenity_games_assets/SourceArchives/Packages/` — не подключённый embedded-пакет `com.abstractocclusion.webgpuwater`;
- `serenity_games_assets/ImportedSourceBackup/Assets/` — неиспользуемые части импортированных библиотек, demo, документация и архивы;
- `serenity_games_assets/GeneratedTestScenes/` — временные сцены редакторских тестов.

Эти каталоги не попадают в Unity Asset Database и не участвуют в source manifest/fingerprint.

## Правило дальнейших импортов

1. Новый архив сначала помещается вне `Assets`.
2. В `Assets` импортируются только нужные runtime-ресурсы с их `.meta`.
3. После проверки сцены и сборки этот набор одним коммитом становится tracked baseline.
4. Demo, Samples, документация, исходные `.unitypackage` и промежуточные test-сцены остаются вне build scope.

Если после удаления части библиотеки Unity сообщает об отсутствующей зависимости, следует восстановить только указанный файл и его `.meta` из `ImportedSourceBackup`, повторить проверку и добавить восстановленную пару в baseline.
