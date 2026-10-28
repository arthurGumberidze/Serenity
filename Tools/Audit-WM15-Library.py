"""Read-only static asset audit. Does not import assets or execute vendor code."""
import collections, gzip, hashlib, io, json, re, tarfile, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Artifacts/WM15LibraryAudit'
OUT.mkdir(parents=True, exist_ok=True)
def save(name, obj):
    (OUT / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
def rel(p): return p.relative_to(ROOT).as_posix()
paths = sorted(p for p in (ROOT/'Assets').rglob('*') if p.is_file())
ids, duplicates = {}, collections.defaultdict(list)
for p in paths:
    if p.suffix == '.meta':
        m = re.search(rb'^guid: ([a-f0-9]{32})', p.read_bytes(), re.M)
        if m:
            g = m[1].decode(); ids[g] = rel(p)[:-5]; duplicates[g].append(rel(p)[:-5])
save('duplicate-guids.json', {g:v for g,v in duplicates.items() if len(v)>1})
refs = {}; text_ext = {'.prefab','.unity','.asset','.mat','.controller','.overridecontroller','.playable','.meta'}
for p in paths:
    if p.suffix in text_ext:
        with p.open('rb') as f:
            head=f.read(256)
            if b'\0' in head: continue
            data=head+f.read()
        found=set(x.decode() for x in re.findall(rb'guid: ([a-f0-9]{32})', data))
        if p.suffix=='.meta':
            key=rel(p)[:-5]; found={g for g in found if ids.get(g)!=key}
        else: key=rel(p)
        refs.setdefault(key,set()).update(ids[g] for g in found if g in ids)
roots=[rel(p) for p in paths if p.suffix!='.meta' and (rel(p).startswith('Assets/Game/Resources/') or ('/Resources/' in rel(p) and '/Editor/' not in rel(p)))]
settings=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text()
roots+=re.findall(r'enabled: 1\s+path: (.+)',settings)
seen=set(); todo=list(roots)
while todo:
    p=todo.pop()
    if p in seen: continue
    seen.add(p); todo.extend(refs.get(p,set())-seen)
direct=set().union(*(v for k,v in refs.items() if k.startswith('Assets/Game/') or k.startswith('Assets/Scenes/')))
save('reachable-static.json', sorted(seen))
save('project-direct-references.json',sorted(direct))
folders={}
for p in paths:
    if p.suffix=='.meta': continue
    name=p.relative_to(ROOT/'Assets').parts[0]
    if len(p.relative_to(ROOT/'Assets').parts)==1: name='[root files]'
    s=folders.setdefault(name,dict(files=0,bytes=0,prefabs=0,models=0,code=0,archives=0,staticReachable=0,projectReferences=0,examples=[]))
    s['files']+=1; s['bytes']+=p.stat().st_size
    s['prefabs']+=p.suffix=='.prefab'; s['models']+=p.suffix.lower() in {'.fbx','.obj','.blend','.glb','.gltf'}
    s['code']+=p.suffix.lower() in {'.cs','.dll'}; s['archives']+=p.suffix.lower() in {'.zip','.unitypackage'}
    s['staticReachable']+=rel(p) in seen; s['projectReferences']+=rel(p) in direct
    if rel(p) in direct and len(s['examples'])<8: s['examples'].append(rel(p))
save('folders.json',folders)
(OUT/'all-files.txt').write_text('\n'.join(rel(p) for p in paths),encoding='utf-8')
with zipfile.ZipFile(ROOT/'docs/reference/FRS_Unity.docx') as z:
    doc=ET.fromstring(z.read('word/document.xml'))
    paras=[''.join(p.itertext()) for p in doc.iter('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}p')]
    (OUT/'frs-relevant.txt').write_text('\n'.join(p for p in paras if re.search('каменн|здани|эпох|график|стил|ландшафт',p,re.I)),encoding='utf-8')
reports={}; errors=[]
def scan(source,label,kind,depth=0):
    if depth>4: errors.append(label+': depth limit'); return
    entries=[]; nested=[]
    try:
        if kind=='.zip':
            with zipfile.ZipFile(source) as z:
                for item in z.infolist():
                    if item.is_dir(): continue
                    entries.append({'path':item.filename,'bytes':item.file_size})
                    if item.filename.lower().endswith('.unitypackage'):
                        nested.append((item.filename,z.read(item)))
        else:
            records={}
            with gzip.GzipFile(fileobj=source) as compressed, tarfile.open(fileobj=compressed,mode='r|') as t:
                for item in t:
                    if not item.isfile(): continue
                    parts=item.name.strip('./').split('/')
                    if len(parts)!=2: continue
                    g,typ=parts; r=records.setdefault(g,{'guid':g})
                    if typ=='pathname': r['path']=t.extractfile(item).read().decode('utf-8-sig',errors='replace').strip().removesuffix('\n00')
                    elif typ=='asset':
                        r['bytes']=item.size
                        # Read only gzip/zip signatures; preserve nested archives in memory.
                        f=t.extractfile(item); head=f.read(4)
                        if head[:2]==b'\x1f\x8b' or head==b'PK\x03\x04': r['_archive']=head+f.read()
            for r in records.values():
                payload=r.pop('_archive',None)
                if 'path' not in r: continue
                r['importedPath']=ids.get(r['guid'])
                r['samePathExists']=(ROOT/r['path']).is_file()
                entries.append(r)
                if payload and Path(r['path']).suffix.lower() in {'.unitypackage','.zip'}: nested.append((r['path'],payload))
        reports[label]={'entries':entries,'entryCount':len(entries),'importedGuidCount':sum(bool(e.get('importedPath')) for e in entries),'roots':sorted(set('/'.join(e['path'].split('/')[:2]) for e in entries))}
        for name,data in nested: scan(io.BytesIO(data),label+'::'+name,Path(name).suffix.lower(),depth+1)
    except Exception as e: errors.append(label+': '+str(e))
archives=[p for p in paths if p.suffix.lower() in {'.unitypackage','.zip'}]
hashes=collections.defaultdict(list)
for i,p in enumerate(archives):
    print(f'{i+1}/{len(archives)} {rel(p)}',flush=True)
    with p.open('rb') as f: digest=hashlib.file_digest(f,'sha256').hexdigest()
    hashes[digest].append(rel(p))
    with p.open('rb') as f: scan(f,rel(p),p.suffix.lower())
    save('archives.json',reports); save('errors.json',errors)
save('duplicate-archives.json',{k:v for k,v in hashes.items() if len(v)>1})
extra=[{'path':rel(p),'bytes':p.stat().st_size} for p in (ROOT/'serenity_games_assets').rglob('*') if p.is_file()]
save('external-library.json',extra)
save('summary.json',{'filesIncludingMeta':len(paths),'folders':len(folders),'outerArchives':len(archives),'archivesIncludingNested':len(reports),'errors':errors,'externalFiles':len(extra),'archiveBytes':sum(p.stat().st_size for p in archives)})
print('DONE '+json.dumps(json.loads((OUT/'summary.json').read_text()),ensure_ascii=False),flush=True)
