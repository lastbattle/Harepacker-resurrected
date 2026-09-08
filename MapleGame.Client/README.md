# MapleGame client

`MapleGame.Client` is the standalone Windows host for `MapleGame.Runtime`. It launches
maps without initializing HaCreator. The same runtime assembly is also used by
HaCreator's detached map preview.

## Interactive launcher

Run `MapleGame.Client.exe` without source arguments to open the launcher. Choose an IMG
export, WZ installation or hybrid source, select the WZ encryption settings, enter a
map ID and launch. Henesys (`100000000`) is the default map.

Keep the executable with every DLL, JSON manifest, native dependency and file in the
published `Content` directory. Game data and user profiles are stored separately from
the client package.

## Command line

The following examples use repository-local placeholder data directories so they are
portable between machines:

```powershell
$imgRoot = Resolve-Path .\game-data\img-v95
dotnet run --project MapleGame.Client -- --img $imgRoot --map 100000000
```

```powershell
$wzRoot = Resolve-Path .\game-data\wz-v95
dotnet run --project MapleGame.Client -- --wz $wzRoot --map 100000000 --wz-version GMS
```

```powershell
$imgRoot = Resolve-Path .\game-data\img-v95
$wzRoot = Resolve-Path .\game-data\wz-v95
dotnet run --project MapleGame.Client -- --hybrid-img $imgRoot --wz $wzRoot --map 100000000
```

For data that uses an explicit four-byte IV, select the custom version and pass eight
hexadecimal digits. Hyphens and a `0x` prefix are accepted.

```powershell
$wzRoot = Resolve-Path .\game-data\custom-wz
dotnet run --project MapleGame.Client -- --wz $wzRoot --wz-version CUSTOM --wz-iv 01-02-03-04 --map 100000000
```

Use `--portal <name>` to select the initial spawn portal and
`--profile-directory <path>` to select a profile directory. Omit the profile option to
use the normal standalone client profile. Run with `--help` for the complete option
list.

The executable is a Windows GUI application and opens no console during an ordinary
desktop launch. Command-line errors are written to stderr when available and are shown
in a desktop dialog otherwise.

## Source and session ownership

Each session owns its IMG, WZ or hybrid source for the session lifetime. WZ and hybrid
sessions create a private `WzFileManager`; they do not consult or replace HaCreator's
source manager or custom-IV settings. Map transitions use the runtime map provider.
Persistence uses the standalone client profile. Shared content fonts are supplied by
HaSharedLibrary.

Built-in sources do not hot-swap during an active session. Restart the client to pick
up replaced backing data.

## Deployment

From the repository root, with the .NET SDK and Python installed:

```powershell
python release.py --client
```

This creates a framework-dependent Windows x64 publish and packages it as
`Production/MapleGame.Client-win-x64.zip`. The target machine needs the matching .NET
Windows Desktop runtime. Extract and keep the entire archive together.

To package an existing publish directory without rebuilding:

```powershell
python release.py --client --client-publish-dir .\publish\MapleGame.Client
```

The packager validates required host manifests and shared fonts and rejects editor
assemblies. The no-argument `release.py` command retains the suite's existing packaging
behavior.

For architecture, validation guidance and remaining compatibility work, see
[MapSimulator extraction and game-client foundation](../docs/architecture/map-simulator-extraction-plan.md).
