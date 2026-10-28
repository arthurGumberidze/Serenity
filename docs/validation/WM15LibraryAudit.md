# WM15 local library audit — 2026-10-28

Analysis-only user request. No Unity import, gameplay edit, asset deletion, Unity/Player execution,
test suite or build. WM15 remains IN_PROGRESS; current visual-approval gate stays in place.

## Commands and results

PowerShell, working directory `C:\serenity_game`:

```powershell
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Tools/Audit-WM15-Library.py
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Tools/Summarize-WM15-Library.py
```

Both completed with exit 0. Final audit: 31,088 Assets files including meta; 53 inventory groups
(top-level folders plus root files), 64 outer archives, 172 archive catalogs including nested Unity
packages; 0 archive read errors. External library: 165 files. Outer archives occupy 18,656,043,277 bytes.
Two SHA-256 duplicate pairs; no duplicate GUIDs among scanned Assets meta files.

The first scanner attempt used tarfile's direct streaming gzip adapter, which reported decompression
errors on readable packages. That run was interrupted and discarded. The reader was corrected to use
gzip.GzipFile plus tarfile r|, matching the existing project audit tool; the complete scan above then
succeeded. The temporary first-run errors are not evidence of damaged asset archives.

Evidence: `Artifacts/WM15LibraryAudit/{summary,folders,archives,duplicate-archives,duplicate-guids,
project-direct-references,reachable-static,external-library,errors}.json`, `all-files.txt`,
`frs-relevant.txt`. Human-readable full inventory is committed alongside the review.

GUID reachability is a conservative static approximation from enabled scenes and Resources, not a
Player Build Report or runtime-visible inventory. Shader.Find, copied/remapped assets and preprocessed
heightfields require source/provenance review; examples are explicitly documented. Knights source ZIP
is listed but not expanded; its Unity package is scanned. No claim of visual validation of every model.

The final report incorporates current WM15 authoring code and prior admission records. It corrects
the obsolete missing-fish/hide/ore/VFX/animation claims from the October 26 inventory and the misleading
Medieval Europe package name (five character/axe prefabs, no buildings).
