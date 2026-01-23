# HaCreator & HaRepacker Architecture Documentation

This directory contains architecture documentation for HaCreator (map editor) and HaRepacker (WZ/IMG editor).

## Documentation Index

| Document | Description |
|----------|-------------|
| [IMG_FILESYSTEM_MIGRATION_PLAN.md](./IMG_FILESYSTEM_MIGRATION_PLAN.md) | Migration from WZ files to extracted IMG filesystem |
| [img-hot-swap.md](./img-hot-swap.md) | Hot-swapping system for live asset reloading |

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

---

## Performance Optimizations

### LRU Cache
- 512MB default memory limit (configurable)
- Evicts least-recently-used WzImages when limit reached
- Shared across all data sources

### Lazy Loading
- TileSets, ObjectSets, BackgroundSets use `LazyWzImageDictionary`
- Images loaded only when accessed
- MapInfo created on-demand when map opened

### Memory Usage Comparison

| Data Type | Traditional WZ | IMG Filesystem |
|-----------|----------------|----------------|
| Startup memory | 40GB+ (all loaded) | 2-4GB (lazy) |
| Tiles/Objects | All at startup | On-demand |
| Maps | All WzImages kept | Metadata only |
| NPCs/Mobs | Icons preloaded | Names only, icons on-demand |

---

## HaRepacker Architecture

HaRepacker is the WZ/IMG file editor component.

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

---

## See Also

- [WZ Format Documentation](../wz-format/README.md) - WZ file format details
- [MapSimulator Documentation](../mapsimulator/README.md) - Map simulator architecture
