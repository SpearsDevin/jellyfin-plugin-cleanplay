#!/usr/bin/env python3
"""Insert a new version entry into manifest.json (Jellyfin plugin repository manifest)."""
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

GUID = "d5f9c8a1-3e47-4b62-9c0d-8f2a6b7e5d13"


def main() -> None:
    version = sys.argv[1]     # e.g. 1.0.0
    zip_path = Path(sys.argv[2])
    repo = sys.argv[3]        # e.g. owner/jellyfin-plugin-cleanplay
    manifest_path = Path(sys.argv[4])

    version4 = version + ".0"
    checksum = hashlib.md5(zip_path.read_bytes()).hexdigest()
    source_url = f"https://github.com/{repo}/releases/download/v{version}/{zip_path.name}"

    entry = {
        "version": version4,
        "changelog": f"See https://github.com/{repo}/releases/tag/v{version}",
        "targetAbi": "10.11.0.0",
        "sourceUrl": source_url,
        "checksum": checksum,
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }

    owner = repo.split("/")[0]
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text())
    else:
        manifest = []

    if not manifest:
        manifest = [{
            "guid": GUID,
            "name": "CleanPlay",
            "description": "VidAngel-style content filtering (profanity, nudity, violence) using Jellyfin media segments.",
            "overview": "Filter profanity, nudity and violence from your media.",
            "owner": owner,
            "category": "General",
            "imageUrl": "",
            "versions": [],
        }]

    versions = [v for v in manifest[0]["versions"] if v["version"] != version4]
    versions.insert(0, entry)
    manifest[0]["versions"] = versions
    manifest[0]["owner"] = owner

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Updated {manifest_path} with version {version4} (md5 {checksum})")


if __name__ == "__main__":
    main()
