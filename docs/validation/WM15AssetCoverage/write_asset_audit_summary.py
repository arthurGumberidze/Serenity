from pathlib import Path
import json,csv,shutil,collections
r=Path('C:/serenity_game');o=r/'Artifacts/WM15AssetCoverage';d=r/'docs/validation/WM15AssetCoverage';d.mkdir(exist_ok=True)
folders=json.loads((o/'folder-inventory.json').read_text());ar=json.loads((o/'archive-inventory.json').read_text());nested=json.loads((o/'nested-archive-inventory.json').read_text());ad=json.loads((r/'Artifacts/WM15LateFeedback/admitted-assets.json').read_text())
copies=collections.Counter(e['package'] for e in ad if (r/e['path']).is_file())
with (d/'folder-inventory.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['folder','files_excluding_meta','prefabs','models_fbx_obj_blend','materials','texture_files','terrain_layers','audio'])
 for k,v in sorted(folders.items()):
  if not (r/'Assets'/k).is_dir():continue
  w.writerow([k,sum(v.values()),v.get('.prefab',0),sum(v.get(x,0) for x in ['.fbx','.obj','.blend']),v.get('.mat',0),sum(v.get(x,0) for x in ['.png','.tga','.jpg','.tif','.psd','.exr']),v.get('.terrainlayer',0),sum(v.get(x,0) for x in ['.wav','.mp3','.ogg'])])
with (d/'archive-inventory.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['archive','path_entries_including_folders','guid_matches_in_assets','previous_remapped_copies','error'])
 for k,v in ar.items():w.writerow([k,len(v['entries']),sum(bool(e.get('imported_by_guid')) for e in v['entries']),copies.get(Path(k).name,0),v['error']])
with (d/'nested-archive-inventory.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['archive_chain','path_entries_including_folders','guid_matches_in_assets','error'])
 for k,v in nested.items():w.writerow([k,len(v['entries']),sum(bool(e.get('imported_by_guid')) for e in v['entries']),v['error']])
for name in ['candidate-dependencies.json','work-animation-candidates.json']:
 shutil.copyfile(o/name,d/name)
for name in ['LowPolyTropicalCity_FishingRod_01.prefab.png','LowPolyTropicalCity_FishingRod_02.prefab.png','LowPolyTropicalCity_Fish_01.prefab.png','LowPolyDungeons_Bone_01.prefab.png']:
 shutil.copyfile(o/name,d/name)
folder_lines=[]
for k,v in sorted(folders.items()):
 if (r/'Assets'/k).is_dir():folder_lines.append(f"| `{k}` | {sum(v.values())} | {v.get('.prefab',0)} | {sum(v.get(x,0) for x in ['.fbx','.obj','.blend'])} |")
archive_lines=[]
for k,v in ar.items():
 matches=sum(bool(e.get('imported_by_guid')) for e in v['entries']);c=copies.get(Path(k).name,0)
 state='Есть совпадающие импортированные файлы' if matches else 'Остаётся в архиве'
 if c:state+=f'; {c} выбранных файлов перенесено с новыми GUID'
 if 'Low Poly Asset Bundle' in k:state='Контейнер: часть вложенных наборов импортирована, часть нет'
 if k.endswith('.zip'):state='Unity-пакет Knights проверен отдельно; исходный ZIP — не импорт в Unity'
 archive_lines.append(f"| `{k.removeprefix('Assets/')}` | {len(v['entries'])} | {matches} | {state} |")
p=r/'docs/world_map/WM15_IMPORTED_ASSET_COVERAGE.md';s=p.read_text(encoding='utf8').split('<!-- GENERATED_AUDIT_SUMMARY -->')[0]
s+='''## Полнота обхода

В списке 27 603 пути (включая `.meta`), 3 781 файл `.prefab` и 3 510 файлов моделей FBX/OBJ/Blend. Это количество файлов, а не уникальных игровых объектов: варианты, LOD и служебные префабы учитываются отдельно. В числе есть собственный `Game`.

'''+f"Прочитано {len(ar)} внешних архивов и {len(nested)} вложенных Unity-пакетов (включая Knights из ZIP). Ошибок чтения в итоговых каталогах: {sum(bool(v['error']) for v in ar.values())+sum(bool(v['error']) for v in nested.values())}. Вложенные пакеты разных render pipeline часто повторяют материалы и GUID; это не число независимых наборов. ZIP исходников моделей Knights не исследовался как отдельная библиотека для импорта; проверен его поставляемый Unity-пакет.\n\n"
s+='''### Все верхнеуровневые папки Assets

Таблица включает папки кода/тестов/настроек, чтобы было видно, что они не пропущены. Папки дат содержат архивы; ноль моделей в такой папке не означает пустой архив.

| Папка | Файлов без `.meta` | Prefab | FBX/OBJ/Blend |
|---|---:|---:|---:|
'''+ '\n'.join(folder_lines)
s+='''

### Все внешние архивы

Совпадения подсчитаны по GUID, включая метаданные папок. Это свидетельство наличия содержимого, а не процент готовности/полноты импорта. Выбранные копии с новыми GUID указаны отдельно. Для всех вложенных пакетов есть отдельная CSV-сводка в `docs/validation/WM15AssetCoverage/nested-archive-inventory.csv`.

| Архив внутри Assets | Записей путей | Совпадений GUID | Состояние |
|---|---:|---:|---|
'''+ '\n'.join(archive_lines)+'\n'
p.write_text(s,encoding='utf8')
print('FOLDERS',len(folder_lines),'ARCHIVES',len(ar),'NESTED',len(nested),'ERRORS',sum(bool(v['error']) for v in ar.values())+sum(bool(v['error']) for v in nested.values()))
