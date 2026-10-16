# P10.1 handoff — требования и технический baseline

Дата: 2026-10-16
Статус: DONE
Следующая задача: P10.2 — «Единый связный мир и стратегический обзор»

## Выполнено

- полностью прочитаны обязательные project docs, релевантные разделы `docs/reference/FRS_Unity.docx` и пользовательские ответы `C:\Users\Lenovo\Desktop\1.docx`;
- создан полный контракт `docs/P101_REQUIREMENTS_AND_TECHNICAL_BASELINE.md` с world/UI/resource/industry/Character/family/combat/cheat/save требованиями;
- отдельно перечислены семь нынешних типов ресурсов и все 24 текущих production node с индексами, координатами и количеством;
- зафиксированы несовместимые P10 saves, provenance freeze, Character/animation gap и stage gates P10.2–P10.10;
- `PROJECT_STATE`, `TASK_GRAPH`, `DECISIONS`, `ASSET_REGISTRY` и `NEXT_TASK` синхронизированы;
- игровых функций P10.2+ и новых внешних assets нет.

## Player baseline 1920×1080

Использован уже собранный нормальный Windows D3D11 Player P9.6 с отдельными save/settings:

```powershell
$env:SERENITY_SAVE_DIRECTORY='C:\serenity_game\Logs\P101-baseline\player-1920x1080\saves'
$env:SERENITY_SETTINGS_PATH='C:\serenity_game\Logs\P101-baseline\player-1920x1080\settings\user-settings.json'
Start-Process -FilePath 'C:\serenity_game\Builds\P96\Serenity.exe' -ArgumentList @(
  '-logFile','C:\serenity_game\Logs\P101-baseline\player-1920x1080\Player.log',
  '-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-force-d3d11',
  '-p96Smoke','-p96SmokeSeed','96006','-p96SmokeWidth','1920','-p96SmokeHeight','1080',
  '-p96SmokeOutput','C:\serenity_game\Logs\P101-baseline\player-1920x1080'
) -WindowStyle Normal -Wait
```

Результат:

- семь PNG созданы, каждый 1920×1080 и не пуст;
- `menu_new_game`, `editor_uma_generated`, `local_rts`, `local_tps`, `global_map`, `six_locations`, `return_local`, `terrain_biome_settlement`, `finite_resources`, `gather_haul_build`, `weather_day_night`, `save_load_stable_ids`, `stone_age_wardrobe`, `clean_uma_prefab` — true;
- 170 trees, 94 rocks, 930 ground cover, 24 resource formations, 6 worksites, 2 active Character presentations;
- `rain_snow_seasons=false`, поэтому исторический smoke честно дал `technical_passed=false`, `passed=false`;
- `Player.log` не содержит проверяемых compiler/unhandled/null/missing-reference/assertion/crash сигнатур;
- P9.6 route не измеряет frame time. P10.1 не приписывает этому запуску FPS; 1920×1080 p95 gate принадлежит P10.9.

Осмотрены editor, RTS/work, TPS, global map, rain, winter/night и after-load кадры. Они подходят как baseline, но не как финальная P10 visual acceptance: global map схематична и отдельна, work UI перекрывает большую часть мира, в ролях остаются английские подписи, различимость массовых жителей ограничена.

## Проверки

Первая sandboxed попытка EditMode завершилась native Unity crash в `BuildReportRestService`/Mono `wakeup_pipes_init` до NUnit XML (`EXIT=-536870911`). Это ограничение sandbox localhost/thread service, а не test failure. Тот же запуск вне sandbox прошёл.

### EditMode

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'C:\serenity_game' `
  -logFile 'C:\serenity_game\Logs\P101-editmode.log' `
  -runTests -testPlatform EditMode -testCategory 'P84|P93|P94|P95' `
  -testResults 'C:\serenity_game\Logs\P101-editmode.xml'
```

Результат: exit 0; NUnit Passed, 12 passed, 0 failed, 0 skipped.

### PlayMode

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'C:\serenity_game' `
  -logFile 'C:\serenity_game\Logs\P101-playmode.log' `
  -runTests -testPlatform PlayMode -testCategory P96 `
  -testResults 'C:\serenity_game\Logs\P101-playmode.xml'
```

Результат: exit 0; NUnit Passed, 2 passed, 0 failed, 0 skipped.

### Windows Development build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'C:\serenity_game' `
  -logFile 'C:\serenity_game\Logs\P101-build.log' -quit `
  -executeMethod U00Build.WindowsDevelopment `
  -serenityBuildPath 'Builds/P101/Serenity.exe'
```

Результат в логе: `U00_BUILD_RESULT: Succeeded; errors=0; warnings=26`. Файл `Builds/P101/Serenity.exe` создан. Перед build Unity записал два `Curl error 28` timeout внешнего сервиса; после успешного result Editor не завершил wrapper штатно, поэтому ожидающий PowerShell был прерван уже после доказанного build success. Это не скрывается и не меняет результат компиляции/сборки.

### Repository checks

```powershell
git diff --check
git status --short
```

Перед commit `git diff --check` не сообщил whitespace errors. Финальный status и commit hash записаны в итоговом сообщении чата.

## Незакрытые факты, переданные дальше

- exact 1920×1080 P9.6 weather composite assertion сейчас false;
- конкретный 1920×1080 baseline не имеет frame timing;
- нынешняя additive GlobalMap не является same-world strategic view;
- текущие видимые ресурсы/selection не выполняют universal affordance contract P10;
- current Character/animation inventory не закрывает P10.5;
- P8.6/U29/U30/U31 остаются PARTIAL/PARTIAL/IN_PROGRESS/BLOCKED.

P10.2 должна брать требования только из своего раздела спецификации и не начинать P10.3+.
