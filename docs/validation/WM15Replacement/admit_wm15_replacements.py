from pathlib import Path,PurePosixPath
import tarfile,io,json,re,uuid
r=Path('C:/serenity_game');out=r/'Artifacts/WM15Replacement';out.mkdir(exist_ok=True)
destination=r/'Assets/Game/Art/WM15Replacement/Imported'
def package(data):
 t=tarfile.open(fileobj=io.BytesIO(data),mode='r:gz'); members={m.name:m for m in t.getmembers()}; entries={}
 for name,m in members.items():
  if name.endswith('/pathname'):
   path=t.extractfile(m).read().decode('utf-8-sig').splitlines()[0].strip('\x00 ');g=name.split('/')[-2]
   entries[g]=(path,name.rsplit('/',1)[0])
 return t,members,entries
def raw(t,m,prefix,suffix):
 key=prefix+'/'+suffix
 return t.extractfile(m[key]).read() if key in m else b''
report=[]
def admit(data,alias,names,overlay=None):
 t,m,entries=package(data); replacements={};aliases={}
 if overlay:
  ot,om,oe=package(overlay)
  base_ids={p:g for g,(p,pre) in entries.items()}
  replacements={p:(raw(ot,om,pre,'asset'),raw(ot,om,pre,'asset.meta')) for g,(p,pre) in oe.items() if PurePosixPath(p).suffix}
  aliases={g:base_ids[p] for g,(p,pre) in oe.items() if p in base_ids}
  for p,(data,meta) in list(replacements.items()):
   for old,new in aliases.items():data=data.replace(old.encode(),new.encode());meta=meta.replace(old.encode(),new.encode())
   replacements[p]=(data,meta)
 queue=[g for g,(p,pre) in entries.items() if PurePosixPath(p).name in names];chosen={}
 while queue:
  g=queue.pop()
  if g in chosen or g not in entries:continue
  p,pre=entries[g];ext=PurePosixPath(p).suffix.lower()
  if ext not in {'.prefab','.fbx','.obj','.mat','.png','.tga','.jpg','.psd','.asset'}:continue
  data,meta=replacements.get(p,(raw(t,m,pre,'asset'),raw(t,m,pre,'asset.meta')))
  for old,new in aliases.items():
   meta=meta.replace(old.encode(),new.encode())
   if ext in {'.prefab','.mat','.asset'}:data=data.replace(old.encode(),new.encode())
  if not data:continue
  chosen[g]=(p,data,meta)
  for content in ([data,meta] if ext in {'.prefab','.mat','.asset'} else [meta]):queue.extend(x.decode() for x in re.findall(rb'guid: ([a-f0-9]{32})',content))
 remap={g:uuid.uuid5(uuid.NAMESPACE_URL,'serenity/wm15replacement/'+alias+'/'+g).hex for g in chosen}
 for g,(p,data,meta) in chosen.items():
  parts=PurePosixPath(p).parts
  if parts[0]!='Assets' or '..' in parts:raise ValueError(p)
  dest=destination/alias/Path(*parts[1:]);dest.parent.mkdir(parents=True,exist_ok=True)
  for old,new in remap.items():
   meta=meta.replace(old.encode(),new.encode())
   if dest.suffix in {'.prefab','.mat','.asset'}:data=data.replace(old.encode(),new.encode())
  meta=re.sub(rb'(?m)^guid: [a-f0-9]{32}',b'guid: '+remap[g].encode(),meta)
  dest.write_bytes(data);Path(str(dest)+'.meta').write_bytes(meta)
  report.append({'source':p,'destination':dest.relative_to(r).as_posix(),'alias':alias,'guid':remap[g]})
 print(alias,len(chosen),flush=True)
bundle=r/'Assets/pre3aketxh_Low Poly Asset Bundle v1.20.unitypackage'
t,m,entries=package(bundle.read_bytes())
for needle,alias,names in [('LowPolyTropicalCity_','Fishing',{'FishingRod_01.prefab','FishingRod_02.prefab','Fish_01.prefab'}),('LowPolyDungeons_','Bones',{'Bone_01.prefab','Bone_02.prefab'})]:
 pre=next(pre for p,pre in entries.values() if needle in p and p.endswith('.unitypackage'))
 admit(raw(t,m,pre,'asset'),alias,names)
data=(r/'Assets/26102026/All Nature VFX.unitypackage').read_bytes();t,m,entries=package(data)
pre=next(pre for p,pre in entries.values() if p.endswith('/URP All Nature VFX.unitypackage'))
admit(data,'NatureFX',{'Rain.prefab','Snow1.prefab','Snow2.prefab','Snowstorm.prefab','Fire.prefab','Fog.prefab','Leaf.prefab','Leaf2.prefab','Bee.prefab','Butterfly.prefab','Fluff.prefab'},raw(t,m,pre,'asset'))
(out/'admitted-assets.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
