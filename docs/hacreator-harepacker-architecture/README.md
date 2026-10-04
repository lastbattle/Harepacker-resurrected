# HaCreator & HaRepacker Architecture Documentation

This directory contains architecture documentation for HaCreator (map editor) and HaRepacker (WZ/IMG editor).

## Documentation Index

| Document | Description |
|----------|-------------|
| [IMG_FILESYSTEM_MIGRATION_PLAN.md](./IMG_FILESYSTEM_MIGRATION_PLAN.md) | Migration from WZ files to extracted IMG filesystem |
| [img-hot-swap.md](./img-hot-swap.md) | Hot-swapping system for live asset reloading |
| [MapSimulator extraction plan](../architecture/map-simulator-extraction-plan.md) | Shared game runtime, standalone client, and HaCreator preview integration |

---

## Applications Overview

| Application | Purpose | Data Sources |
|-------------|---------|--------------|
| **HaCreator** | MapleStory map editor | WZ files, IMG filesystem |
| **HaRepacker** | WZ/IMG file editor | WZ archives, IMG directories |

---

## HaCreator Data Source Modes

### 1. Traditional WZ Mode
Loads data directly from MapleStory WZ archive files. Requires:
- MapleStory client installation
- Correct encryption version detection
- WZ files remain read-only

### 2. IMG Filesystem Mode
Loads data from extracted `.img` files in the filesystem. Benefits:
- No MapleStory client required
- Version-agnostic data storage
- Git-trackable assets
- Easy modification via file system
- Hot-swap support for live editing

Lua WZ images use a text representation in the extracted filesystem: a Lua WZ
image such as `BattleScene.lua` (containing a `WzLuaProperty`) is written as
UTF-8 `BattleScene.lua`, not as a binary `BattleScene.lua.img`. When packing,
the `.lua` file is encoded back into `WzLuaProperty`. UTF-8 (with or without a
BOM) and BOM-marked UTF-16 source files are accepted. A legacy `.lua.img` is
accepted only when its
matching `.lua` text file is absent, so an old export cannot override edited
script text.

When packing IMG files back to WZ, the Pack IMG files to WZ dialog uses the
manifest's `isPreBBDataWzFormat` value as the initial suggestion. The user can
change the pre-Big-Bang checkbox; selecting it produces split category WZ
files and preserves `List.wz` when the List category is selected. Beta
packing remains the separate single `Data.wz` format.

---

## Architecture Components

### Data Source Abstraction

```
┌─────────────────────────────────────────────────────────┐
│                    HaCreator UI                          │
│  (TilePanel, ObjPanel, LifePanel, MapBrowser, etc.)     │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
              ┌────────────────────────┐
              │   IDataSource          │ ◄── Abstraction layer
              └────────────┬───────────┘
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ WzFileDataSource│ │ImgFileSystem    │ │ HybridDataSource│
│  (WZ archives)  │ │DataSource       │ │ (IMG + fallback)│
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

### Key Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `IDataSource` | MapleLib/Img/IDataSource.cs | Data source abstraction interface |
| `ImgFileSystemManager` | MapleLib/Img/ImgFileSystemManager.cs | Core IMG filesystem manager |
| `ImgFileSystemDataSource` | MapleLib/Img/ImgFileSystemDataSource.cs | IDataSource for IMG filesystem |
| `WzFileDataSource` | MapleLib/Img/ImgFileSystemDataSource.cs | IDataSource wrapper for WzFileManager |
| `HybridDataSource` | MapleLib/Img/ImgFileSystemDataSource.cs | Tries IMG first, falls back to WZ |
| `VirtualWzDirectory` | MapleLib/Img/VirtualWzDirectory.cs | WzDirectory-compatible filesystem wrapper |
| `VersionManager` | MapleLib/Img/VersionManager.cs | Multi-version management |
| `WzExtractionService` | MapleLib/Img/WzExtractionService.cs | WZ to IMG extraction |
| `StartupManager` | HaCreator/Wz/StartupManager.cs | Startup flow and version selection |
| `HotSwapRefreshService` | HaCreator/Wz/HotSwapRefreshService.cs | UI refresh on file changes |

---

## Data Flow

### Startup Flow

```
HaCreator Launch
       │
       ▼
StartupManager.Initialize()
       │
       ├── Scan for extracted versions in HaCreator_Data/versions/
       │
       ▼
┌──────────────────────────────────────┐
│ Versions found?                       │
├─────────Yes───────────────────────────┤
│                                       │
│  Show VersionSelector                 │
│       │                               │
│       ├── Select existing version     │
│       ├── Import from WZ              │
│       └── Use WZ directly             │
│                                       │
├─────────No────────────────────────────┤
│                                       │
│  Show Initialization dialog           │
│       │                               │
│       ├── Select MapleStory folder    │
│       └── Extract to IMG or use WZ    │
│                                       │
└──────────────────────────────────────┘
       │
       ▼
Create IDataSource (IMG or WZ)
       │
       ▼
ImgDataExtractor.ExtractAll()
       │
       ▼
WzInformationManager populated
       │
       ▼
Launch Map Editor
```

### Hot-Swap Flow (IMG Filesystem)

```
FileSystemWatcher detects change
       │
       ▼
ImgFileSystemManager.OnImgFileChanged()
       │
       ├── Update category index
       └── Invalidate LRU cache
       │
       ▼
HotSwapRefreshService receives event
       │
       ├── Map category change to panel event
       └── Update WzInformationManager
       │
       ▼
UI Panel refreshes (TilePanel, ObjPanel, etc.)
```

---

## Directory Structure

### HaCreator Data Directory

```
%AppData%/HaCreator/           # Or configured location
├── config.json                # Configuration file
├── versions/                  # Extracted MapleStory versions
│   ├── v83/
│   │   ├── manifest.json      # Version metadata
│   │   ├── String/
│   │   ├── Map/
│   │   ├── Mob/
│   │   └── ...
│   ├── v55/
│   └── gms_v230/
└── custom/                    # User-created content
```

### Manifest File

Each extracted version has a `manifest.json`:

```json
{
  "version": "v83",
  "displayName": "GMS v83 (Pre-Big Bang)",
  "sourceRegion": "GMS",
  "extractedDate": "2025-01-15T10:30:00Z",
  "encryption": "GMS",
  "is64Bit": false,
  "isPreBBDataWzFormat": true,
  "isVUpdate": false,
  "categories": {
    "String": { "fileCount": 8 },
    "Map": { "fileCount": 1250 },
    "Mob": { "fileCount": 890 }
  },
  "features": {
    "hasPets": true,
    "hasMount": true,
    "maxLevel": 200
  }
}
```

`isVUpdate` is detected from the presence of `UI.wz/StatusBar3.img` during
WZ extraction. It is independent of the client architecture and lets IMG
versions retain the same UI-family selection as their source WZ files.

### MapleStory V Update

MapleStory's V Update introduced the fifth-job system and its accompanying
modern status-bar assets. The client-owned UI images distinguish the simulator
families by newest owner: `StatusBar.img` identifies the legacy/pre-Big-Bang
family, `StatusBar2.img` identifies the post-Big-Bang family, and
`StatusBar3.img` identifies the V Update family. Because later clients can keep
older assets for compatibility, `StatusBar3.img` takes precedence when more
than one is present. The extraction service records this result as
`isVUpdate`; when an IMG version is opened, `VersionManager` and
`ImgFileSystemManager` deserialize that flag from `manifest.json`. For exports
created before the flag existed, both readers fall back to the presence of
`UI/StatusBar3.img` and write the inferred `isVUpdate: true` value back to the
manifest. If the manifest is read-only, the in-memory version still uses the
inferred value.

For background on the fifth-job release, see the [official V-179 patch
notes](https://www.nexon.com/maplestory/news/update/4250/v-179-v-5th-job-patch-notes)
and the [MapleStory V overview](https://maplestory.fandom.com/wiki/MapleStory:_V).

---

## Performance Optimizations

### LRU Cache
- 512MB default memory limit (configurable)
- Evicts least-recently-used WzImages when limit reached
- Shared across all data sources

### Lazy Loading
- Category discovery does not recursively index every IMG file. A recursive category index is built only for APIs that require a complete category scan; directory-name APIs enumerate only their requested directory.
- Standalone IMG readers parse property headers and retain shareable readers. Canvas and sound payload bytes remain on disk until rendered or played; startup does not calculate whole-file checksums.
- TileSets, ObjectSets, and BackgroundSets register filenames and load images only when accessed.
- MapInfo is created when a map opens.
- A map's BGM path resolves directly to its owning Sound IMG and property. The complete audio catalogue is built only by explicit catalogue/browse workflows.
- Reactor definitions load from IDs referenced by the opened map. Mob/NPC assets load from that map, and skill assets load for the active character.
- Startup reads only `String/Map.img`. MapSimulator loads `String/Npc.img` when it first builds NPC tooltips; other localized String catalogues and Quest metadata load when their selectors or editors open.
- MapSimulator advances NPC animation from update-loop elapsed time; drawing only renders the frame selected by the animation controller.

### Memory Usage Comparison

| Data Type | Traditional WZ | IMG Filesystem |
|-----------|----------------|----------------|
| Startup memory | 40GB+ (all loaded) | About 99 MB working set in the measured post-V probe |
| Tiles/Objects | All at startup | On-demand |
| Maps | All WzImages kept | Metadata only |
| BGM/Reactor | Complete categories parsed | Opened map only |
| NPCs/Mobs | Icons preloaded | IDs from filenames; assets on-demand |

---

## HaRepacker Architecture

HaRepacker is the WZ/IMG file editor component.

---

## MapSimulator Combat Notes

- Mob combat in `HaCreator/MapSimulator` now follows a two-phase flow similar to the v95 client: the mob AI chooses an attack or skill action, then `MapSimulator` resolves delayed projectiles and ground-hit entries separately.
- `MobItem.InitializeAI()` consumes both `Mob.wz/info/attack` and `Mob.wz/info/skill` metadata so skill actions (`skill1/skill2/...`) are distinct from normal attacks instead of being folded into the attack list.
- `PlayerCombat` applies mob hits only when the attack or skill timing window is active, which keeps skill casts and delayed boss attacks from dealing damage continuously during the full animation.
- Boss ranged attacks can now materialize as moving projectiles and delayed ground rectangles in the simulator, matching the client-side split between action playback and later bullet / area-hit processing.

### Supported File Types

| Type | Extension | Description |
|------|-----------|-------------|
| WZ Archive | `.wz` | Packed MapleStory data archive |
| IMG File | `.img` | Individual image/data file (inside WZ or standalone) |
| IMG Directory | folder | Extracted IMG as filesystem directory |

### Opening Files

```
┌─────────────────────────────────────────┐
│            HaRepacker MainForm          │
├─────────────────────────────────────────┤
│  File > Open                            │
│  ├── Open WZ File (.wz)                 │
│  ├── Open IMG File (.img)               │
│  └── Open Version Directory             │◄── IMG filesystem
│                                         │
│  TreeView displays:                     │
│  ├── WzFile nodes                       │
│  ├── WzDirectory nodes                  │
│  ├── WzImage nodes                      │
│  └── VirtualWzDirectory nodes           │◄── Filesystem-backed
└─────────────────────────────────────────┘
```

### VirtualWzDirectory

When opening an IMG filesystem directory, HaRepacker uses `VirtualWzDirectory` to present the filesystem as a WzDirectory-compatible tree:

```csharp
// Filesystem structure
Map/
├── Map0/
│   ├── 100000000.img/
│   │   ├── info/
│   │   └── ...
│   └── 100000100.img/
└── Tile/
    └── grassySoil.img/

// Appears in HaRepacker TreeView as:
[Map] (VirtualWzDirectory)
├── [Map0] (VirtualWzDirectory)
│   ├── [100000000.img] (WzImage)
│   └── [100000100.img] (WzImage)
└── [Tile] (VirtualWzDirectory)
    └── [grassySoil.img] (WzImage)
```

### Hot-Swap in HaRepacker

HaRepacker supports hot-swap for opened IMG directories:
- Detects external file modifications
- Auto-reloads changed files
- Shows brief notification (auto-dismisses in 3 seconds)
- Save operations temporarily disable watching to prevent self-triggered reloads

See [img-hot-swap.md](./img-hot-swap.md) Part 2 for details.

### Key HaRepacker Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `MainForm` | HaRepacker/GUI/MainForm.cs | Main application window |
| `WzNode` | HaRepacker/WzNode.cs | TreeView node wrapper |
| `ContextMenuManager` | HaRepacker/GUI/ContextMenuManager.cs | Context menu handling |
| `VirtualWzDirectory` | MapleLib/Img/VirtualWzDirectory.cs | Filesystem-backed WzDirectory |
| `ImgDirectoryWatcherService` | MapleLib/Img/ImgDirectoryWatcherService.cs | File change monitoring |

### MapSimulator Attack Info

`HaCreator/MapSimulator` now treats `Mob.img/attackN/info` as structured attack data instead of only generic attack animations.
The loader carries `range`, `effectAfter`, `attackAfter`, `areaCount`, `attackCount`, `start`, `areaWarning`, `effect`, and numbered `effect0/effect1/...` nodes into the simulator so boss attacks can place telegraphs and delayed ground effects on footholds with client-style timing.

### Foothold Editing

With the Foothold tool active, a normal left click creates the next anchor and
clicking an existing anchor continues the current polyline. Press `Escape` to
cancel the unfinished segment; clicking the Foothold tool button again
re-enters the mode even when the button is already selected.
---

## See Also

- [WZ Format Documentation](../wz-format/README.md) - WZ file format overview and index
- [MapSimulator Documentation](../mapsimulator/README.md) - Map simulator architecture
