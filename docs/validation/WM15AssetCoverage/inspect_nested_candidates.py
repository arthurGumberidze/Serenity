from pathlib import Path
import gzip,tarfile,json,re
r=Path('C:/serenity_game');o=r/'Artifacts/WM15AssetCoverage';n=json.loads((o/'nested-archive-inventory.json').read_text());a=json.loads((o/'archive-inventory.json').read_text())
outer='Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage'
targets={e['guid']:e['path'] for e in a[outer]['entries'] if any(x in e['path'] for x in ['LowPolyTropicalCity_','LowPolyInterior2_','LowPolyDungeons_']) and e['path'].endswith('.unitypackage')}
report=[]
with gzip.open(r/outer,'rb') as g,tarfile.open(fileobj=g,mode='r|') as t:
 for m in t:
  guid=m.name.split('/')[-2] if '/' in m.name else ''
  if not (m.name.endswith('/asset') and guid in targets):continue
  key=outer+'::'+targets[guid]; entries=n[key]['entries']; index={e['guid']:e['path'] for e in entries}
  selected={e['guid']:e['path'] for e in entries if Path(e['path']).stem in ['FishingRod_01','FishingRod_02','Fish_01','Bone_01','Bone_02']}
  with gzip.GzipFile(fileobj=t.extractfile(m)) as inner,tarfile.open(fileobj=inner,mode='r|') as it:
   for im in it:
    ig=im.name.split('/')[-2] if '/' in im.name else ''
    if ig not in selected:continue
    label=Path(selected[ig]).parts[1]+'_'+Path(selected[ig]).name
    if im.name.endswith('/preview.png'):
     dest=o/(label+'.png');dest.write_bytes(it.extractfile(im).read());print('PREVIEW',dest,flush=True)
    if im.name.endswith('/asset.meta'):
     s=it.extractfile(im).read().decode('utf8',errors='replace'); report.append({'path':selected[ig],'meta':s})
    if im.name.endswith('/asset') and selected[ig].endswith('.prefab'):
     s=it.extractfile(im).read().decode('utf8',errors='replace');report.append({'path':selected[ig],'prefab':s,'refs':[(q,index.get(q)) for q in set(re.findall(r'guid: ([a-f0-9]{32})',s))]})
(o/'nested-candidate-details.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('COMPLETE',flush=True)
