# WM15 asset coverage audit — 2026-10-26

Scope: local read-only content audit; only report/evidence/documentation files added. No Unity process,
Player, compilation, build, gameplay tests or full performance gates invoked. No package imported or
vendor file changed. This is not new runtime acceptance evidence; WM15 stays IN_PROGRESS.

Working directory: `C:\serenity_game`.

Commands used for inventory/inspection (PowerShell):

```powershell
rg --files -uu Assets | Set-Content -Encoding utf8 Artifacts/wm15-all-assets-paths.txt
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 Artifacts/audit_imported_assets.py
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 Artifacts/inspect_asset_candidates.py
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 Artifacts/audit_nested_assets.py
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 Artifacts/inspect_nested_candidates.py
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -X utf8 Artifacts/write_asset_audit_summary.py
git diff --check -- docs
```

Final script runs exited 0. Results: 27,603 file paths including metadata; 14,519 GUID entries;
3,781 prefab files; 3,510 FBX/OBJ/Blend files; 45 top-level directories; 44 outer archives and
60 nested Unity packages; zero archive-read errors in final inventories. Counts include Game-owned
files, variants, collision models and folder path entries where specified, not unique usable models.

Initial tarfile streaming gzip reader rejected some Unity packages. Corrected the audit reader to
`gzip.open` / `gzip.GzipFile` feeding `tarfile` in stream mode; final reads succeeded. Pathname entries
with trailing newline metadata are normalized to their first line. An exploratory one-off read used an
invalid Python encoding alias `utf8-sig`; rerun with `utf-8-sig` succeeded. These were audit-script issues,
not game/Unity failures, and are not reported as corrupt user assets.

Source scripts are copied alongside this file for repeatability; generated large path/GUID/archive
inventories remain in `Artifacts/WM15AssetCoverage`. Nested Knights inspection used the already
extracted Unity package in `Artifacts/AssetAudit/POLYGON_Knights`; the separate source-files ZIP was not
catalogued as a Unity library. Unitypackage thumbnails for fish, rods and bones were inspected with
`view_image`; copies here are original embedded vendor thumbnails, not newly rendered game captures.

Evidence limitations: GUID matches include folder records and shared assets. Prior admission remapped
GUIDs; its manifest and physical destination files were checked separately (Arctic 17, Stylized 36,
Ground 6, Aquas 3). Material references absent from the Assets index can be package/builtin references.
No claim that every model was visually inspected or that every candidate works in current URP without
adjustment. No final in-game integration or visual approval claimed.
