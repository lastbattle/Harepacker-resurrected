#!/usr/bin/python3

# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at http://mozilla.org/MPL/2.0/.

import os
import shutil
import argparse
from pathlib import Path
import subprocess
import tempfile
import zipfile

PROD_DIR = "Production"
RELEASE_DIR = "Release\\AnyCPU"

def whitelist(f):
    return (f.endswith(".exe.config") and not f.endswith(".vshost.exe.config")) or \
           (f.endswith(".exe") and not f.endswith(".vshost.exe")) or \
           f.endswith(".dll") or \
           f == "Help.htm"

def package_client(publish_directory):
    source = Path(publish_directory).resolve()
    required = ["MapleGame.Client.exe", "MapleGame.Runtime.dll",
                "MapleGame.Client.deps.json", "MapleGame.Client.runtimeconfig.json",
                "Content/XnaDefaultFont.xnb", "Content/XnaFont_Chat.xnb",
                "Content/XnaFont_Debug.xnb"]
    missing = [name for name in required if not (source / name).is_file()]
    if missing:
        raise ValueError("Incomplete client publish: " + ", ".join(missing))
    files = sorted(path for path in source.rglob("*") if path.is_file())
    editor_assemblies = {name + extension
                         for name in ("hacreator", "harepacker", "wvsmaps", "wvswzimg")
                         for extension in (".exe", ".dll")}
    if any(path.name.lower() in editor_assemblies for path in files):
        raise ValueError("Client publish unexpectedly contains an editor assembly")
    destination = Path(PROD_DIR)
    destination.mkdir(parents=True, exist_ok=True)
    archive = destination / "MapleGame.Client-win-x64.zip"
    # Stage alongside the final archive so replacement is atomic on this volume.
    with tempfile.NamedTemporaryFile(dir=destination, suffix=".zip", delete=False) as staging:
        staged_path = Path(staging.name)
    try:
        with zipfile.ZipFile(staged_path, "w", zipfile.ZIP_DEFLATED) as output:
            for path in files:
                output.write(path, path.relative_to(source).as_posix())
        os.replace(staged_path, archive)
    finally:
        staged_path.unlink(missing_ok=True)
    print(archive.resolve())


def main():
    parser = argparse.ArgumentParser(description="Package the suite, or publish and package the standalone game client.")
    parser.add_argument("--client", action="store_true", help="Create a client archive without removing existing suite packages")
    parser.add_argument("--client-publish-dir", type=Path, help="Package an existing win-x64 client publish (requires --client)")
    args = parser.parse_args()
    if args.client_publish_dir and not args.client:
        parser.error("--client-publish-dir requires --client")
    if args.client:
        if args.client_publish_dir:
            package_client(args.client_publish_dir)
        else:
            with tempfile.TemporaryDirectory(prefix="maplegame-publish-") as publish_directory:
                subprocess.run(["dotnet", "publish", str(Path(__file__).resolve().parent / "MapleGame.Client/MapleGame.Client.csproj"),
                                "-c", "Release", "-r", "win-x64", "--self-contained", "false", "-o", publish_directory], check=True)
                package_client(publish_directory)
        return
    if os.path.exists(PROD_DIR):
        shutil.rmtree(PROD_DIR)
    os.mkdir(PROD_DIR)
    for (rls_path, dirs, files) in os.walk(RELEASE_DIR):
        prod_path = PROD_DIR + rls_path[len(RELEASE_DIR):]
        for d in dirs:
            os.mkdir(os.path.join(prod_path, d))
        for f in files:
            if whitelist(f):
                shutil.copyfile(os.path.join(rls_path, f), os.path.join(prod_path, f))

if __name__ == "__main__":
    main()
