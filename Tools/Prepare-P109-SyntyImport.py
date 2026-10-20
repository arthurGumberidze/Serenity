"""Audit the user-approved archive and prepare a Meadow/Core-only URP overlay.

Does not extract untrusted paths or execute package content. The generated package
is imported by Unity; original archives and pre-existing project files stay intact.
"""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import tarfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Artifacts/P109/synty-import"
PREFIX = "Assets/PolygonNatureBiomes"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def index(archive):
    members = {}
    for member in archive.getmembers():
        name = member.name.rstrip("/")
        if name == "." and member.isdir():
            continue
        if name.startswith("./"):
            name = name[2:]
        if re.fullmatch(r"[a-fA-F0-9]{32}/\._(asset|asset.meta|pathname|preview.png)", name) and member.isfile():
            continue  # macOS resource forks are not Unity payloads.
        if not re.fullmatch(r"[a-fA-F0-9]{32}(/(asset|asset.meta|pathname|preview.png))?", name):
            raise ValueError(f"Unexpected archive member: {name}")
        if not (member.isfile() or member.isdir()):
            raise ValueError(f"Unsupported archive entry: {name}")
        if name in members:
            raise ValueError(f"Duplicate archive entry: {name}")
        members[name] = member
    # Read the small index records in physical order; seeking backwards through
    # gzip for each asset would repeatedly decompress the entire large archive.
    index_bytes = {name: archive.extractfile(member).read() for name, member in members.items()
                   if name.endswith(("/pathname", "/asset.meta"))}
    records = {}
    for name, member in members.items():
        if not name.endswith("/pathname"):
            continue
        guid = name.split("/")[0]
        path = index_bytes[name].decode("utf-8-sig").strip()
        parts = PurePosixPath(path).parts
        if (path != PREFIX and not path.startswith(PREFIX + "/")) or any(
                p in ("..", ".") or ":" in p or "\\" in p for p in parts):
            raise ValueError(f"Unsafe package path: {path}")
        if path in records:
            raise ValueError(f"Duplicate asset path: {path}")
        if PurePosixPath(path).suffix.lower() in (".cs", ".dll", ".exe", ".asmdef", ".rsp", ".bat", ".ps1"):
            raise ValueError(f"Code requires separate review: {path}")
        meta = index_bytes[guid + "/asset.meta"]
        if not re.search(rb"(?m)^guid: " + guid.encode() + rb"\s*$", meta):
            raise ValueError(f"GUID mismatch: {path}")
        records[path] = (guid, archive, members)
    return records


def main():
    source = Path(sys.argv[1]).resolve(strict=True)
    OUT.mkdir(parents=True, exist_ok=True)
    target = OUT / "MeadowForest-URP-reviewed.unitypackage"
    if target.exists() or (ROOT / PREFIX).exists():
        raise ValueError("Refusing to overwrite an earlier import/checkpoint")
    with tarfile.open(source) as base:
        original = index(base)
        nested_path = PREFIX + "/URP_ExtractMe_Nature_Biomes.unitypackage"
        nested_guid, _, nested_members = original[nested_path]
        with tarfile.open(fileobj=io.BytesIO(base.extractfile(nested_members[nested_guid + "/asset"]).read())) as urp:
            overlay = index(urp)
            selected = {p: v for p, v in original.items() if p != nested_path}
            accepted = {p: v for p, v in overlay.items() if p == PREFIX or
                        p.startswith(PREFIX + "/PNB_Core") or
                        p.startswith(PREFIX + "/PNB_Meadow_Forest")}
            for path, record in accepted.items():
                if path in selected and selected[path][0] != record[0]:
                    raise ValueError(f"Overlay GUID differs: {path}")
                selected[path] = record
            guids = {v[0] for v in selected.values()}
            if len(guids) != len(selected):
                raise ValueError("Multiple imported paths have the same GUID")
            for path in selected:
                if (ROOT / path).exists() or (ROOT / (path + ".meta")).exists():
                    raise ValueError(f"Existing path collision: {path}")
            for meta in (ROOT / "Assets").rglob("*.meta"):
                match = re.search(rb"(?m)^guid: ([a-fA-F0-9]{32})", meta.read_bytes())
                if match and match[1].decode() in guids:
                    raise ValueError(f"Existing GUID collision: {meta}")
            # Back up all existing tracked modifications, without touching the unrelated
            # untracked Unity project. A Git commit records the earlier staged patch.
            dirty = subprocess.check_output(["git", "diff", "--name-only", "-z", "HEAD"], cwd=ROOT)
            dirty_paths = [p.decode("utf-8") for p in dirty.split(b"\0") if p]
            with zipfile.ZipFile(OUT / "pre-import-dirty-files.zip", "x", zipfile.ZIP_DEFLATED) as backup:
                for path in dirty_paths:
                    if (ROOT / path).is_file():
                        backup.write(ROOT / path, path)
            preserved = list((ROOT / "ProjectSettings").glob("*")) + list((ROOT / "Packages").glob("*.json"))
            preserved += list((ROOT / "Assets/Game").rglob("*.unity"))
            preserved += [ROOT / p for p in dirty_paths]
            saves = Path.home() / "AppData/LocalLow/Serenity/Serenity/Serenity"
            preserved += list(saves.rglob("*.xml")) if saves.exists() else []
            baseline = {str(p): digest(p) for p in preserved if p.is_file()}
            with tarfile.open(target, "w:gz") as result:
                for archive in (base, urp):
                    chosen = {guid for guid, owner, _ in selected.values() if owner is archive}
                    members = next(m for _, owner, m in selected.values() if owner is archive)
                    for name, member in members.items():
                        if name.split("/")[0] in chosen:
                            result.addfile(member, archive.extractfile(member) if member.isfile() else None)
            report = {
                "source": str(source), "source_sha256": digest(source),
                "checkpoint_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
                "original_entries": len(original), "urp_entries": len(accepted),
                "import_entries": len(selected), "prepared_sha256": digest(target),
                "excluded": "Nested archive and unrelated Tropical/Jungle/Swamp URP content",
                "assets": sorted(selected), "preserved_sha256": baseline,
            }
            (OUT / "pre-import-audit.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
            print(json.dumps({k: v for k, v in report.items() if k not in ("assets", "preserved_sha256")}, indent=2))


if __name__ == "__main__":
    main()
