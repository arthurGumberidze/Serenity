"""Stage only the six relevant nested art archives for the same subsequent audit."""
import gzip
import json
from pathlib import Path
import shutil
import tarfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Artifacts/P109/compact-world'
reports = json.loads((OUT / 'package-audit.json').read_text(encoding='utf-8'))
report = next(x for x in reports if 'Low Poly Asset Bundle' in x['source'])
approved = {'NatureGradientPack', 'LowPolyFantasyVillage', 'LowPolyNature (4 seasons)',
            'LowPolyRPGWeapons', 'LowPolyNaturePack', 'LowPolyFarm'}
selected = {x['guid']: x['path'].split('/')[2] for x in report['entries']
            if x['path'].endswith('.unitypackage') and x['path'].split('/')[2] in approved}
folder = OUT / 'bundle'
folder.mkdir(exist_ok=True)
with gzip.open(ROOT / 'Assets' / report['source'], 'rb') as compressed, tarfile.open(fileobj=compressed, mode='r|') as archive:
    for item in archive:
        guid, _, part = item.name.removeprefix('./').partition('/')
        if guid in selected and part == 'asset' and item.isfile():
            target = folder / (selected[guid] + '.unitypackage')
            with target.open('xb') as stream:
                shutil.copyfileobj(archive.extractfile(item), stream)
            print('STAGED', target.name, flush=True)
