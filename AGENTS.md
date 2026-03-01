# AGENTS.md

## Purpose
This file gives coding agents a reliable operating guide for `Harepacker-resurrected`.
Use it to make focused, low-risk changes that match this codebase.

## Project Snapshot
- Solution: `MapleHaSuite.sln`
- Main apps:
  - `HaCreator` (MapleStory map editor)
  - `HaRepacker` (WZ/IMG editor)
- Shared/core libraries:
  - `HaSharedLibrary`
  - `MapleLib`
- Test projects:
  - `MapleLib.Tests`
  - `UnitTest_MapSimulator`
  - `UnitTest_WzFile`
  - `UnitTest_Perf`
- Platform: Windows
- Current target framework in active projects: `net10.0-windows`

## Environment Requirements
- Visual Studio 2022 (Desktop development with C++)
- .NET SDK supporting `net10.0-windows`
- Git with submodule support

## Setup
```powershell
git submodule update --init --recursive
nuget restore MapleHaSuite.sln
dotnet restore MapleHaSuite.sln
```

## Build
```powershell
dotnet build MapleHaSuite.sln -c Debug
dotnet build MapleHaSuite.sln -c Release
```

For native/C++ coverage (CI parity for mixed-language analysis):
```powershell
msbuild MapleHaSuite.sln /p:Configuration=Release /p:Platform="x64" /p:RestorePackages=false
```

## Test
Run targeted tests for the area you changed first:
```powershell
dotnet test MapleLib.Tests/MapleLib.Tests.csproj
dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj
dotnet test UnitTest_WzFile/UnitTest_WzFile.csproj
dotnet test UnitTest_Perf/UnitTest_Perf.csproj
```

Notes:
- `UnitTest_WzFile` depends on bundled test assets and can be slower/heavier.
- `UnitTest_Perf` includes benchmark-related code; run when perf-sensitive code changes.

## Code Map
- `HaCreator/`: editor UI, map editing, MapSimulator, AI map editing components
- `HaRepacker/`: WZ/IMG tree editing, packing/unpacking, editor panels
- `HaSharedLibrary/`: shared rendering/audio/UI utilities
- `MapleLib/MapleLib/`: WZ/IMG parsing, crypto, file structures
- `docs/wz-format/`: WZ format and loading references
- `docs/hacreator-harepacker-architecture/`: architecture and IMG filesystem docs

## Working Rules For Agents
- Keep changes minimal and scoped to the request.
- Do not refactor unrelated files in the same pass.
- Avoid broad formatting-only edits.
- Preserve existing WinForms/WPF patterns and project structure.
- Do not change framework/runtime targets or package baselines unless requested.
- When committing, include only files added or modified by the agent in this task; do not include unrelated pre-existing uncommitted changes.
- When changing packing/extraction/parsing logic, prefer adding or updating tests in related test projects.
- For UI behavior changes, include manual verification steps in your summary.

## High-Risk Areas (Extra Caution)
- WZ parsing/serialization and crypto paths in `MapleLib`
- Packing/extraction flows in `HaCreator/GUI` and `HaRepacker/GUI`
- MapSimulator rendering/input/state code in `HaCreator/MapSimulator`
- File hot-swap and cache lifecycle code

## Documentation Expectations
If behavior changes, update the closest relevant docs:
- WZ internals: `docs/wz-format/`
- Architecture/data flow: `docs/hacreator-harepacker-architecture/`
- Feature plans/design notes: `docs/architecture/`
