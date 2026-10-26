from pathlib import Path
import json,re,collections
r=Path('C:/serenity_game'); o=r/'Artifacts/WM15AssetCoverage'; idx=json.loads((o/'guid-index.json').read_text())
paths=(r/'Artifacts/wm15-all-assets-paths.txt').read_text(encoding='utf-8-sig').splitlines()
patterns={
'resource':r'(StonePile|Berries|CrateOf|BasketOf|Sack|Food_Meat|HayBale|PT_Ore_Rock|PT_River_Rock)',
'craft':r'(Tanning_Rack|Drying|Dryer|Workbench|Work_Bench|Camp_Table|Craft|Meat_Rack)',
'farm':r'(Garden_0[12]|Soil|Furrow|Plant_Bed|Raised_Bed|Trough_01A|Fence.*01A|Wheat)',
'fish':r'(Fish|Fishing|Rod|Bobber)',
'environment':r'(Fireflies|Firefly|Rain|Snow|Smoke|Blood|Impact|Chop|Splinter|Sparks)'}
found={}
for k,pat in patterns.items():
 found[k]=[p.replace('\\','/') for p in paths if Path(p).suffix.lower() in ['.prefab','.fbx','.anim'] and re.search(pat,Path(p).stem,re.I) and '/Game/' not in p.replace('\\','/')]
(o/'candidate-search.json').write_text(json.dumps(found,ensure_ascii=False,indent=2),encoding='utf-8')
for k in ['craft','farm','fish']:
 print(k);print('\n'.join(found[k][:55]))
clips=[]
for p in paths:
 if p.lower().endswith('.fbx.meta') and '/Game/' not in p.replace('\\','/'):
  t=(r/p).read_text(encoding='utf-8',errors='replace')
  names=re.findall(r'^\s+(?:name|takeName): (.*)$',t,re.M)
  for n in sorted(set(names)):
   if re.search(r'(fish|chop|harvest|mine|dig|carry|wood|archer|bow|work|farm|hammer|pickaxe|gather)',n,re.I):
    typ=re.search(r'^\s+animationType: (.*)',t,re.M)
    clips.append({'path':p,'clip':n,'animationType':typ[1] if typ else None})
(o/'work-animation-candidates.json').write_text(json.dumps(clips,ensure_ascii=False,indent=2),encoding='utf-8')
print('CLIPS',json.dumps(clips,ensure_ascii=False))
chosen=['StonePile.prefab','Berries.prefab','CrateOfStone.prefab','SM_Gen_Prop_Food_Meat_01.prefab','SM_Prop_Camp_Tanning_Rack_01.prefab','TFP_Wooden_Pier_01A.prefab','PT_Ore_Rock_01.prefab','PT_Ore_Rock_01_split.prefab','Garden_01.prefab','Construction_Storehouse.prefab','FX_Fish_Small_01.prefab']
evidence=[]
for filename in chosen:
 for p in paths:
  if Path(p).name!=filename:continue
  text=(r/p).read_text(encoding='utf-8',errors='replace'); refs=[]
  for g in sorted(set(re.findall(r'guid: ([0-9a-f]{32})',text))):
   dep=idx.get(g); nested=[]
   if dep and Path(dep).suffix=='.mat':
    mt=(r/dep).read_text(encoding='utf-8',errors='replace')
    nested=[{'guid':q,'path':idx.get(q)} for q in sorted(set(re.findall(r'guid: ([0-9a-f]{32})',mt)))]
   refs.append({'guid':g,'path':dep,'material_dependencies':nested})
  evidence.append({'path':p,'references':refs})
(o/'candidate-dependencies.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
for item in evidence:
 print('PREFAB',item['path']);print('  '+'\n  '.join(str(x['path'])+' '+str(x['material_dependencies']) for x in item['references']))
