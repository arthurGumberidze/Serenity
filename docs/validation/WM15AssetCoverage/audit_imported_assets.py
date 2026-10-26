from pathlib import Path
import json,re,collections,tarfile,zipfile,time,gzip
root=Path('C:/serenity_game'); out=root/'Artifacts/WM15AssetCoverage'; out.mkdir(exist_ok=True)
files=[root/p.replace('\\','/') for p in (root/'Artifacts/wm15-all-assets-paths.txt').read_text(encoding='utf-8-sig').splitlines()]
index={}; roots={}
for p in files:
    if p.suffix=='.meta':
        match=re.search(r'^guid: ([0-9a-f]{32})',p.read_text(encoding='utf-8',errors='replace'),re.M)
        if match:index[match[1]]=str(p.relative_to(root))[:-5].replace('\\','/')
    else:
        group=p.relative_to(root/'Assets').parts[0]
        roots.setdefault(group,collections.Counter())[p.suffix.lower()]+=1
(out/'folder-inventory.json').write_text(json.dumps(roots,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'guid-index.json').write_text(json.dumps(index,ensure_ascii=False),encoding='utf-8')
print('FILES',len(files),'GUIDS',len(index),'PREFABS',sum(p.suffix=='.prefab' for p in files),'MODELS',sum(p.suffix.lower() in ['.fbx','.obj','.blend'] for p in files),flush=True)
assets=[str(p.relative_to(root)).replace('\\','/') for p in files if p.suffix.lower() in ['.prefab','.fbx','.obj','.blend','.anim','.shader','.shadergraph'] and '/Game/' not in str(p).replace('\\','/')]
(out/'imported-content-paths.txt').write_text('\n'.join(assets),encoding='utf-8')
archives={}
for package in sorted(p for p in files if p.suffix.lower() in ['.unitypackage','.zip','.rar','.7z']):
    key=package.relative_to(root).as_posix(); cached=out/(str(abs(hash(key)))+'.unused')
    entries=[]; error=None
    try:
        if package.suffix.lower()=='.unitypackage':
            with gzip.open(package,'rb') as compressed, tarfile.open(fileobj=compressed,mode='r|') as archive:
                for member in archive:
                    if member.name.endswith('/pathname'):
                        path=archive.extractfile(member).read().decode('utf-8-sig',errors='replace').splitlines()[0].strip('\x00 ')
                        guid=member.name.split('/')[-2]
                        entries.append({'path':path,'guid':guid,'imported_by_guid':index.get(guid),'same_path_exists':(root/path).exists()})
        elif package.suffix.lower()=='.zip':
            with zipfile.ZipFile(package) as archive: entries=[{'path':n} for n in archive.namelist()]
    except Exception as e:error=str(e)
    archives[key]={'bytes':package.stat().st_size,'entries':entries,'error':error}
    (out/'archive-inventory.json').write_text(json.dumps(archives,ensure_ascii=False,indent=2),encoding='utf-8')
    print('ARCHIVE',package.name,len(entries),'IMPORTED_GUID',sum(bool(e.get('imported_by_guid')) for e in entries),'ERROR',error,flush=True)
print('COMPLETE',flush=True)
