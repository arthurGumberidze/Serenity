from pathlib import Path
import json,gzip,tarfile,collections
r=Path('C:/serenity_game'); o=r/'Artifacts/WM15AssetCoverage'
idx=json.loads((o/'guid-index.json').read_text()); archives=json.loads((o/'archive-inventory.json').read_text())
for a in archives.values():
 for e in a['entries']:
  e['path']=e['path'].splitlines()[0].strip('\x00 ');e['same_path_exists']=(r/e['path']).exists()
(o/'archive-inventory.json').write_text(json.dumps(archives,ensure_ascii=False,indent=2),encoding='utf-8')
nested={}
def scan(file):
 result=[]
 with gzip.GzipFile(fileobj=file) as g,tarfile.open(fileobj=g,mode='r|') as t:
  for m in t:
   if m.name.endswith('/pathname'):
    p=t.extractfile(m).read().decode('utf-8-sig',errors='replace').splitlines()[0].strip('\x00 ')
    guid=m.name.split('/')[-2];result.append({'path':p,'guid':guid,'imported_by_guid':idx.get(guid)})
 return result
for pkg,a in archives.items():
 targets={e.get('guid'):e['path'] for e in a['entries'] if e['path'].endswith('.unitypackage') and e.get('guid')}
 if not targets:continue
 with gzip.open(r/pkg,'rb') as g,tarfile.open(fileobj=g,mode='r|') as t:
  for m in t:
   guid=m.name.split('/')[-2] if '/' in m.name else ''
   if m.name.endswith('/asset') and guid in targets:
    key=pkg+'::'+targets[guid]
    try:nested[key]={'entries':scan(t.extractfile(m)),'error':None}
    except Exception as e:nested[key]={'entries':[],'error':str(e)}
    print(targets[guid],len(nested[key]['entries']),nested[key]['error'],flush=True)
    (o/'nested-archive-inventory.json').write_text(json.dumps(nested,ensure_ascii=False,indent=2),encoding='utf-8')
k=r/'Artifacts/AssetAudit/POLYGON_Knights/POLYGON_Knights_Unity_2022_3_v1_6_1.unitypackage'
if k.exists():
 with k.open('rb') as f:nested['Knights zip::'+k.name]={'entries':scan(f),'error':None}
(o/'nested-archive-inventory.json').write_text(json.dumps(nested,ensure_ascii=False,indent=2),encoding='utf-8')
print('COMPLETE',flush=True)
