"""Produce compact, reviewable inventory tables from the read-only audit."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
A=ROOT/'Artifacts/WM15LibraryAudit'
read=lambda n:json.loads((A/n).read_text(encoding='utf-8'))
folders=read('folders.json'); archives=read('archives.json'); summary=read('summary.json')
def verdict(name,s=None):
    n=name.lower()
    if any(x in n for x in ['hdrp','built-in','hd srp']): return 'Другой pipeline; не импортировать в текущий URP'
    if 'medieval europe' in n: return '5 prefab персонажей/топора; зданий нет; отложить'
    if any(x in n for x in ['miami','industrial factory','military_warehouse','war pack']): return 'Поздние эпохи; сейчас отложить'
    if any(x in n for x in ['microverse','toby','terraincomposer','80cef']): return 'Инструмент авторинга; только отдельная задача интеграции'
    if any(x in n for x in ['knights','armors','rpgweapons','assassin','vexa','vefects','ganzse','toonytiny','sunbox','caveman']): return 'Резерв персонажей/экипировки или поздних эпох; не менять текущую систему'
    if 'dogs' in n: return 'Резерв домашних собак; отдельная задача поведения'
    if any(x in n for x in ['all nature','fish.zip','animal hide','human','kevin']): return 'Уже выборочно использован/скопирован; проверить оставшиеся варианты'
    if any(x in n for x in ['winter','snow_mountain','angry mesh','naturemanufacture','rowlan','terrain asset','terraindemoscene']): return 'Текущий источник зимы/рельефа; повторный массовый импорт не нужен'
    if 'procedural water' in n: return 'Вода уже вызывается по имени шейдера; GUID-ноль не означает неиспользование'
    if any(x in n for x in ['aquas','7b01']): return 'Только выбранные wave-текстуры; полный рендерер отложить'
    if 'classic city' in n or 'softleitner' in n: return 'Действующие 3D-здания + 2D UI; не импортировать ProjectSettings'
    if any(x in n for x in ['emaceart','lowpolyfantasyvillage','triforge']): return 'Кандидат: детали поселения сейчас, готовые дома для поздней эпохи'
    if any(x in n for x in ['rts','lowpolyfarm','toon farm','medieval fortification']): return 'Используется частично; приоритет для зданий, хозяйства и декора'
    if any(x in n for x in ['ground','terrain textures','nature materials','grass and flowers','vegetation']): return 'Выборочные поверхности/растения; сравнить с текущими, не импортировать всё'
    if any(x in n for x in ['ambience','sound fx','medieval action']): return 'Звук; полезен атмосфере, не заменяет модели/визуальные VFX'
    if any(x in n for x in ['bear','blink','hodaart','ithappy','animals free']): return 'Часть животных/персонажей уже подключена; сохранять действующие ссылки'
    if 'wild boar' in n or 'wildhunt' in n: return 'Альтернативный кабан; сравнить с действующим, не дублировать автоматически'
    if any(x in n for x in ['nature','synty','polytope','polyone','stylized','66d2','ecc658']): return 'Часть используется или есть альтернативы; выборочно по биому/предмету'
    if 'lightning' in n: return 'Имеются игровые ссылки; сохранить'
    if 'bundle' in n: return 'Много эпох и pipeline; только выбранные предметы с зависимостями'
    if name in ['Game','Scenes','Settings','Editor','Tests','TutorialInfo','Packages','Resources','StreamingAssets','UMAProjectData','_Recovery']: return 'Собственный/служебный контент; не считать сторонним пакетом для удаления'
    if name in ['23102026','25102026','26102026','28102026','[root files]']: return 'Контейнер исходников/архивов; решения по каждому пакету ниже'
    return 'Резерв; выбирать конкретный объект, готовность в игре не подтверждена'
lines=['# Полная инвентаризация ассетов — 28.10.2026','',
       'См. [выводы и приоритеты](WM15_ASSET_LIBRARY_REVIEW.md). Это статический аудит, не список безопасного удаления.','',
       f"Обойдено {summary['filesIncludingMeta']} файлов Assets с .meta; {summary['outerArchives']} внешних архивов, {summary['archivesIncludingNested']} архивных каталогов с вложенными Unity-пакетами. Ошибок чтения: {len(summary['errors'])}. Во внешней serenity_games_assets: {summary['externalFiles']} файлов.",
       f"Внешние архивы внутри Assets занимают {summary['archiveBytes']/1024**3:.2f} GiB. Это место на диске, не размер Player и не измерение времени загрузки.",'',
       'GUID-совпадения включают папки и общие зависимости. Например, совпадения PolygonGeneric не доказывают импорт Knights/Military Warehouse. Копии с новыми GUID и преобразованные высоты учитываются в основном отчёте.','',
       '## Все верхнеуровневые папки Assets','',
       '| Папка | Файлов без meta | MiB | Prefab | Моделей | Прямых ссылок | Достижимо от сцен/Resources | Решение |',
       '|---|---:|---:|---:|---:|---:|---:|---|']
for n,s in folders.items():
    lines.append(f"| {n} | {s['files']} | {s['bytes']/1024**2:.1f} | {s['prefabs']} | {s['models']} | {s['projectReferences']} | {s['staticReachable']} | {verdict(n,s)} |")
lines+=['','## Все внешние архивы','', '| Архив внутри Assets | Записей | GUID-совпадений | Решение |','|---|---:|---:|---|']
for n,s in archives.items():
    if '::' not in n: lines.append(f"| `{n.removeprefix('Assets/')}` | {s['entryCount']} | {s['importedGuidCount']} | {verdict(n)} |")
lines+=['','## Точные дубликаты внешних архивов','']
for h,paths in read('duplicate-archives.json').items(): lines+=['- SHA-256 `'+h+'`: '+ '; '.join('`'+p+'`' for p in paths)]
lines+=['','## Вложенные пакеты','', 'Перечислены отдельно: это не независимые покупки. ZIP исходников Knights каталогизирован как вложенный файл, его модели не распаковывались; поставляемый Unity-пакет прочитан.','', '| Контейнер → вложенный пакет | Записей | GUID-совпадений |','|---|---:|---:|']
for n,s in archives.items():
    if '::' in n: lines.append(f"| `{n}` | {s['entryCount']} | {s['importedGuidCount']} |")
lines+=['','## Внешняя библиотека serenity_games_assets','', '| Каталог/файл | Файлов | MiB |','|---|---:|---:|']
extra={}
for e in read('external-library.json'):
    n=e['path'].split('/')[1]; s=extra.setdefault(n,[0,0]);s[0]+=1;s[1]+=e['bytes']
for n,s in sorted(extra.items()): lines.append(f'| {n} | {s[0]} | {s[1]/1024**2:.1f} |')
(ROOT/'docs/world_map/WM15_ASSET_LIBRARY_INVENTORY.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
