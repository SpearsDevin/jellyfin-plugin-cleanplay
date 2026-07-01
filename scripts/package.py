#!/usr/bin/env python3
"""Package the built plugin DLL into a Jellyfin plugin zip with meta.json."""
import json
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

GUID = "d5f9c8a1-3e47-4b62-9c0d-8f2a6b7e5d13"


def main() -> None:
    version = sys.argv[1]              # e.g. 1.0.0
    publish_dir = Path(sys.argv[2])    # dotnet publish output
    out_zip = Path(sys.argv[3])        # output zip path
    owner = sys.argv[4]                # github owner

    version4 = version + ".0"
    meta = {
        "category": "General",
        "changelog": f"Release {version}",
        "description": "VidAngel-style content filtering (profanity, nudity, violence) using Jellyfin media segments.",
        "guid": GUID,
        "name": "CleanPlay",
        "overview": "Filter profanity, nudity and violence from your media.",
        "owner": owner,
        "targetAbi": "10.11.0.0",
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "version": version4,
        "status": "Active",
        "autoUpdate": True,
        "imagePath": "",
    }

    dll = publish_dir / "Jellyfin.Plugin.CleanPlay.dll"
    if not dll.exists():
        sys.exit(f"DLL not found: {dll}")

    out_zip.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.write(dll, dll.name)
        zf.writestr("meta.json", json.dumps(meta, indent=2))

    print(f"Packaged {out_zip}")


if __name__ == "__main__":
    main()
