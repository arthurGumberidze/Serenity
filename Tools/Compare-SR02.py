"""Compare captured runtime Terrain and vegetation from the same Player binary."""
import collections
import csv
import json
import math
from pathlib import Path
import struct
import sys

baseline, candidate = map(Path, sys.argv[1:3])
pois = json.loads(Path('Assets/Game/Resources/World/WorldMapV2/WORLD_MAP_POI.json').read_text(encoding='utf-8-sig'))['points']

def protected(x, z):
    return not (330 < x < 530 and 480 < z < 700) or any(
        math.hypot(x-p['position'][0], z-p['position'][1]) <= p['protectionRadiusMeters'] for p in pois)

def terrains(folder):
    tiles = {}
    with (folder/'terrain-heights.bin').open('rb') as stream:
        count, = struct.unpack('<i', stream.read(4))
        for _ in range(count):
            x,z,n = struct.unpack('<iii',stream.read(12))
            tiles[x,z] = (n,struct.unpack('<'+'f'*(n*n),stream.read(4*n*n)))
        assert not stream.read(), 'Unexpected extra terrain bytes'
    return tiles

a,b = terrains(baseline),terrains(candidate)
assert a.keys()==b.keys()
outside_checked=outside_changed=inside_changed=0
max_normalized_delta=0
for (tx,tz),(n,old) in a.items():
    m,new=b[tx,tz]
    assert n==m
    for i,(before,after) in enumerate(zip(old,new)):
        x=tx*250+(i%n)*250/(n-1)
        z=tz*250+(i//n)*250/(n-1)
        if protected(x,z):
            outside_checked+=1
            outside_changed+=before!=after
        elif before!=after:
            inside_changed+=1
            max_normalized_delta=max(max_normalized_delta,abs(before-after))

def vegetation(folder):
    with (folder/'vegetation.csv').open() as stream:
        return {r[0]:r for r in csv.reader(stream)}

va,vb=vegetation(baseline),vegetation(candidate)
non_ground={key for key,r in va.items() if r[1]!='GroundCover'}
missing_ids=sorted(non_ground-vb.keys())
outside_vegetation_changes=[]
for key,r in va.items():
    x,z=float(r[3])+570,float(r[5])+120
    if protected(x,z) and vb.get(key)!=r:
        outside_vegetation_changes.append(key)
counts=collections.Counter(r[1] for r in vb.values() if 330<=float(r[3])+570<=530 and 480<=float(r[5])+120<=700)
result=dict(baseline=str(baseline),candidate=str(candidate),terrain_tiles=len(a),
    protected_samples_checked=outside_checked,protected_samples_changed=outside_changed,
    inside_samples_changed=inside_changed,max_normalized_height_delta=max_normalized_delta,
    preserved_non_ground_ids=len(non_ground)-len(missing_ids),missing_non_ground_ids=missing_ids,
    outside_vegetation_changes=outside_vegetation_changes,sample_counts=dict(counts))
(candidate/'comparison.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
sys.exit(0 if outside_changed==0 and not missing_ids and not outside_vegetation_changes else 1)
