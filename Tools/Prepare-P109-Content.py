"""Prepare inert art-only imports; never execute vendor scripts or replace existing assets.

The audit manifest is the path authority. Original downloaded archives stay unchanged.
Classic City Builder is a separate game framework, so only its meshes/textures are admitted.
"""
import gzip
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Artifacts/P109/compact-world'
ART = {'.fbx', '.obj', '.png', '.tga', '.jpg', '.jpeg', '.psd', '.tif', '.tiff',
       '.mat', '.prefab', '.anim', '.controller', '.overridecontroller', '.mask',
       '.wav', '.mp3', '.ogg', '.terrainlayer', '.shader', '.shadergraph', '.shadersubgraph', '.hlsl', '.cginc'}


def inert_prefab(data):
    if not data.startswith(b'%YAML'):
        raise ValueError('Non-text prefab requires separate review')
    # Imported presentations must not carry third-party gameplay/editor behaviours.
    ids = re.findall(rb'(?m)^--- !u!114 &(-?\d+)\r?$', data)
    data = re.sub(rb'(?ms)^--- !u!114 &.*?(?=^--- !u!|\Z)', b'', data)
    for file_id in ids:
        data = re.sub(rb'(?m)^  - component: \{fileID: ' + file_id + rb'\}\r?\n', b'', data)
    return data


def main():
    output = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else OUT
    if not output.is_relative_to(ROOT / 'Artifacts'):
        raise ValueError('Prepared imports must remain in task artifacts')
    reports = json.loads((output / 'package-audit.json').read_text(encoding='utf-8'))
    target = output / 'ReviewedContent.unitypackage'
    if target.exists():
        raise ValueError('Prepared package already exists; inspect it instead of replacing')
    seen_guids, seen_paths = set(), set()
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(rb'(?m)^guid: ([a-f0-9]{32})', meta.read_bytes())
        if match:
            seen_guids.add(match[1].decode())
        seen_paths.add(str(meta.relative_to(ROOT)).replace('\\', '/')[:-5].lower())
    admitted, skipped = [], []
    with tarfile.open(target, 'w:gz', compresslevel=1) as result:
        for report in reports:
            source = Path(report['sourcePath']) if 'sourcePath' in report else ROOT / 'Assets' / report['source']
            if not source.resolve().is_relative_to(ROOT):
                raise ValueError('Source archive outside task workspace')
            with source.open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != report['sha256']:
                    raise ValueError('Source changed since audit: ' + source.name)
            selected = {}
            for entry in report['entries']:
                path, guid = entry['path'], entry['guid']
                suffix = PurePosixPath(path).suffix.lower()
                if suffix not in ART or 'HD_Settings/' in path or '/Demo/' in path or '/Scenes/' in path:
                    continue
                if path.startswith('Assets/SoftLeitner/') and suffix not in {'.fbx', '.obj', '.png', '.tga', '.jpg', '.mat'}:
                    continue
                if guid in seen_guids or path.lower() in seen_paths or (ROOT / path).exists():
                    skipped.append({'path': path, 'reason': 'Existing asset wins; no overwrite'})
                    continue
                selected[guid] = entry
                seen_guids.add(guid)
                seen_paths.add(path.lower())
                admitted.append({'package': source.name, **entry})
            print('PREPARE', source.name, len(selected), flush=True)
            with gzip.open(source, 'rb') as compressed, tarfile.open(fileobj=compressed, mode='r|') as archive:
                for item in archive:
                    name = item.name.removeprefix('./')
                    guid, _, part = name.partition('/')
                    if guid not in selected or part not in ('asset', 'asset.meta', 'pathname') or not item.isfile():
                        continue
                    data = archive.extractfile(item).read()
                    path = selected[guid]['path']
                    if part == 'pathname':
                        data = path.encode('utf-8')
                    elif part == 'asset' and path.endswith('.prefab'):
                        data = inert_prefab(data)
                    out = tarfile.TarInfo(guid + '/' + part)
                    out.size = len(data)
                    result.addfile(out, io.BytesIO(data))
    (output / 'prepared-content.json').write_text(json.dumps({'admitted': admitted, 'skipped': skipped}, indent=2), encoding='utf-8')
    print('READY', len(admitted), 'art assets;', len(skipped), 'existing assets preserved', flush=True)


if __name__ == '__main__':
    main()
