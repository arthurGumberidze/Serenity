"""Build a reviewed art/audio-only Unity package from Assets/23102026.

The original downloads remain untouched. Only the small nature set, selected audio clips and
lightning textures are admitted; vendor scripts, demo scenes and project settings are excluded.
"""
import gzip
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/23102026"
OUTPUT = ROOT / "Artifacts/P109/assets-23102026-import"
ART = {".fbx", ".obj", ".png", ".tga", ".jpg", ".jpeg", ".psd", ".tif", ".tiff",
       ".mat", ".prefab", ".anim", ".controller", ".overridecontroller", ".mask",
       ".wav", ".mp3", ".ogg"}


RULES = {
    "prbf27f5ba0ceb676deafbf2a4d0c0a1_Low Poly Nature Pack - assets model v1.0.unitypackage":
        lambda path: path.startswith("Assets/3D Low Poly Nature Pack -assets model/") and
        "/Scene/" not in path and not path.endswith("/land.prefab"),
    "Middle Age - Medieval Action Sound FX Pack.unitypackage":
        lambda path: PurePosixPath(path).name in {
            "Forest ambient (loop).wav", "Forest at night (loop).wav", "Bow.wav",
            "Arrow 1.wav", "Arrow 2.wav", "Arrow 3.wav"
        },
    "Lightning Bolt Effect for Unity.unitypackage":
        lambda path: path.startswith("Assets/LightningBolt/Textures/") and path.endswith(".png"),
}


def inert_prefab(data: bytes) -> bytes:
    if not data.startswith(b"%YAML"):
        raise ValueError("Non-text prefab requires separate review")
    ids = re.findall(rb"(?m)^--- !u!114 &(-?\d+)\r?$", data)
    data = re.sub(rb"(?ms)^--- !u!114 &.*?(?=^--- !u!|\Z)", b"", data)
    for file_id in ids:
        data = re.sub(rb"(?m)^  - component: \{fileID: " + file_id + rb"\}\r?\n", b"", data)
    return data


def read_records(source: Path):
    records = {}
    with gzip.open(source, "rb") as compressed, tarfile.open(fileobj=compressed, mode="r|") as archive:
        for item in archive:
            name = item.name.removeprefix("./").rstrip("/")
            guid, _, part = name.partition("/")
            if not re.fullmatch(r"[a-fA-F0-9]{32}", guid):
                continue
            if part not in ("asset", "asset.meta", "metaData", "pathname") or not item.isfile():
                continue
            data = archive.extractfile(item).read()
            record = records.setdefault(guid, {})
            if part == "pathname":
                decoded = data.decode("utf-8-sig").rstrip("\r\n\x00")
                record[part] = decoded.splitlines()[0]
            else:
                record[part] = data
    return records


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    target = OUTPUT / "ReviewedContent.unitypackage"
    if target.exists():
        raise ValueError("Reviewed package already exists; remove it only after inspecting the prior result")
    existing_guids = set()
    existing_paths = set()
    for meta in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(rb"(?m)^guid: ([a-f0-9]{32})", meta.read_bytes())
        if match:
            existing_guids.add(match[1].decode())
        existing_paths.add(str(meta.relative_to(ROOT)).replace("\\", "/")[:-5].lower())
    admitted = []
    skipped = []
    selected_records = []
    for package_name, rule in RULES.items():
        source = SOURCE / package_name
        if not source.is_file():
            raise FileNotFoundError(source)
        for guid, record in read_records(source).items():
            path = record.get("pathname", "")
            suffix = PurePosixPath(path).suffix.lower()
            if not path.startswith("Assets/") or suffix not in ART or not rule(path):
                continue
            meta_name = "asset.meta" if "asset.meta" in record else "metaData" if "metaData" in record else ""
            if "asset" not in record or not meta_name:
                skipped.append({"package": package_name, "path": path, "reason": "Incomplete archive entry"})
                continue
            if guid in existing_guids or path.lower() in existing_paths or (ROOT / path).exists():
                skipped.append({"package": package_name, "path": path, "reason": "Existing asset wins"})
                continue
            asset = record["asset"]
            if suffix == ".prefab":
                asset = inert_prefab(asset)
            selected_records.append((guid, path, asset, meta_name, record[meta_name]))
            admitted.append({"package": package_name, "guid": guid, "path": path, "bytes": len(asset)})
            existing_guids.add(guid)
            existing_paths.add(path.lower())
    with tarfile.open(target, "w:gz", compresslevel=1) as output:
        for guid, path, asset, meta_name, meta in selected_records:
            for part, data in (("asset", asset), (meta_name, meta), ("pathname", path.encode("utf-8"))):
                info = tarfile.TarInfo(guid + "/" + part)
                info.size = len(data)
                output.addfile(info, io.BytesIO(data))
    manifest = {"admitted": admitted, "skipped": skipped,
                "policy": "Nature art, six reviewed audio clips and lightning textures only; no vendor code/scenes/settings."}
    (OUTPUT / "prepared-content.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"READY {len(admitted)} reviewed assets; {len(skipped)} collisions/incomplete entries")


if __name__ == "__main__":
    main()
