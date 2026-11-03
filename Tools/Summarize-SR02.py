"""Summarize actual SR02 Player CSV evidence. Missing GPU data never passes."""
import csv
import json
import pathlib
import statistics
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'Artifacts/SR02')

def percentile(values, fraction):
    ordered = sorted(values)
    return ordered[min(len(ordered)-1, int((len(ordered)-1)*fraction))]

report = {}
for folder in sorted(root.iterdir()):
    if not folder.is_dir() or not list(folder.glob('frames-*.csv')):
        continue
    runs = []
    for file in sorted(folder.glob('frames-*.csv')):
        with file.open() as stream:
            rows = list(csv.DictReader(stream))
        metrics = {}
        for key in rows[0] if rows else []:
            values = [float(r[key]) for r in rows if float(r[key]) >= 0]
            if key in ('gpu_ms', 'render_ms', 'draws', 'batches'):
                values = [v for v in values if v > 0]
            metrics[key] = {'samples': len(values), 'p50': percentile(values, .5),
                            'p95': percentile(values, .95), 'mean': statistics.mean(values)} if values else None
        runs.append({'file': str(file), 'frames': len(rows),
                     'seconds': sum(float(r['frame_ms']) for r in rows)/1000, 'metrics': metrics})
    report[folder.name] = {'runs': runs, 'gpu_measured': all(r['metrics'].get('gpu_ms') for r in runs)}
(root / 'measurements.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
