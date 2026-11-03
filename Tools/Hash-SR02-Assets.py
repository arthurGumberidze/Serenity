"""Hash precisely the dependencies exported by SR02AssetAuditTests, outside the Editor."""
import csv
import hashlib
from pathlib import Path

folder=Path('Artifacts/SR02/assets')
paths=folder.joinpath('dependency-paths.txt').read_text(encoding='utf-8-sig').splitlines()
with folder.joinpath('dependencies.tsv').open('w',newline='',encoding='utf-8') as stream:
    writer=csv.writer(stream,delimiter='\t')
    writer.writerow(['path','sha256','bytes'])
    for name in paths:
        path=Path(name)
        if not path.is_file() and path.parts[0]=='Packages':
            package=path.parts[1]
            candidates=list(Path('Library/PackageCache').glob(package+'@*'))
            resolved=[candidate.joinpath(*path.parts[2:]) for candidate in candidates]
            resolved=[candidate for candidate in resolved if candidate.is_file()]
            if len(resolved)!=1:
                raise RuntimeError(f'Ambiguous or missing package dependency: {name}: {resolved}')
            path=resolved[0]
        with path.open('rb') as source:
            digest=hashlib.file_digest(source,'sha256').hexdigest()
        writer.writerow([name,digest,path.stat().st_size])
print(f'{len(paths)} dependencies hashed; no asset files modified')
