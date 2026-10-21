"""Read-only Unity package inventory; never extracts or runs vendor payloads."""
import hashlib
import gzip
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Artifacts/P109/compact-world'


def main():
    source_folder = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'Assets'
    output = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else OUT
    if not source_folder.is_relative_to(ROOT) or not output.is_relative_to(ROOT / 'Artifacts'):
        raise ValueError('Audit inputs and outputs must remain in the task workspace')
    output.mkdir(parents=True, exist_ok=True)
    existing = {}
    for path in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(rb'(?m)^guid: ([a-f0-9]{32})', path.read_bytes())
        if match:
            existing[match[1].decode()] = str(path.relative_to(ROOT)).replace('\\', '/')[:-5]
    reports = []
    for source in sorted(source_folder.glob('*.unitypackage')):
        print('AUDIT ' + source.name, flush=True)
        records = {}
        with gzip.open(source, 'rb') as compressed, tarfile.open(fileobj=compressed, mode='r|') as archive:
            for item in archive:
                name = item.name.removeprefix('./').rstrip('/')
                if name in ('', '.', '.icon.png', 'packagemanagermanifest') or name.startswith('packagemanagermanifest/'):
                    continue
                if not re.fullmatch(r'[a-fA-F0-9]{32}(/(\._)?(asset|asset.meta|pathname|preview.png))?', name):
                    raise ValueError('Unexpected archive member: ' + name)
                if not (item.isfile() or item.isdir()):
                    raise ValueError('Unsupported archive entry: ' + name)
                guid = name.split('/')[0]
                record = records.setdefault(guid, {'guid': guid})
                if name.endswith('/pathname'):
                    value = archive.extractfile(item).read().decode('utf-8-sig').strip()
                    # Recent Unity exporters append the export flags after the pathname.
                    value = value.removesuffix('\n00').rstrip('\r')
                    if not value.startswith('Assets/') or any(p in ('..', '.') or ':' in p or '\\' in p or '\n' in p or '\r' in p for p in PurePosixPath(value).parts):
                        record['blockedPath'] = value
                        continue
                    record['path'] = value
                elif name.endswith('/asset'):
                    record['bytes'] = item.size
        entries = [r for r in records.values() if 'path' in r]
        report = {'source': source.name, 'sourcePath': str(source), 'entries': entries,
                  'blockedPaths': [r['blockedPath'] for r in records.values() if 'blockedPath' in r],
                  'code': [r['path'] for r in entries if PurePosixPath(r['path']).suffix.lower() in ('.cs', '.dll', '.exe', '.asmdef', '.rsp', '.bat', '.ps1')],
                  'collisions': [{'path': r['path'], 'existing': existing[r['guid']]} for r in entries if r['guid'] in existing],
                  'roots': sorted(set('/'.join(r['path'].split('/')[:2]) for r in entries))}
        with source.open('rb') as stream:
            report['sha256'] = hashlib.file_digest(stream, 'sha256').hexdigest()
        reports.append(report)
        (output / 'package-audit.json').write_text(json.dumps(reports, indent=2), encoding='utf-8')
        print(json.dumps({k: (len(v) if k in ('entries', 'code', 'collisions') else v) for k, v in report.items() if k != 'sha256'}), flush=True)


if __name__ == '__main__':
    main()
