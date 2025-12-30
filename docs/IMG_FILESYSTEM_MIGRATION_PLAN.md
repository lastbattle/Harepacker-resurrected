# HaCreator IMG Filesystem Migration Plan

## Implementation Status

| Phase | Description | Status |
|-------|-------------|--------|
| **1** | Foundation Layer | ✅ **COMPLETED** |
| **2** | Enhanced Extraction Tool | ✅ **COMPLETED** |
| **3** | Initialization Refactor | ✅ **COMPLETED** |
| **4** | Data Loading Adaptation | ✅ **COMPLETED** |
| **5** | MapLoader Adaptation | ✅ **COMPLETED** |
| **6** | HaRepacker Integration | ✅ **COMPLETED** |
| **7** | Performance Optimization | ✅ **COMPLETED** |
| **8** | Migration Tools | ✅ **COMPLETED** |
| **9** | Backward Compatibility | ✅ **COMPLETED** |
| **10** | Testing & Stabilization | ✅ **COMPLETED** |

### Phase 1 Completed Files
- `MapleLib/MapleLib/Img/VersionInfo.cs` - Version metadata model
- `MapleLib/MapleLib/Img/HaCreatorConfig.cs` - Configuration management
- `MapleLib/MapleLib/Img/IDataSource.cs` - Data source abstraction interface
- `MapleLib/MapleLib/Img/ImgFileSystemManager.cs` - Core filesystem manager
- `MapleLib/MapleLib/Img/VersionManager.cs` - Multi-version management
- `MapleLib/MapleLib/Img/VirtualWzDirectory.cs` - WzDirectory-compatible filesystem wrapper
- `MapleLib/MapleLib/Img/ImgFileSystemDataSource.cs` - IDataSource implementations

### Phase 2 Completed Files
- `MapleLib/MapleLib/Img/WzExtractionService.cs` - Comprehensive WZ extraction service with:
  - Async extraction with cancellation support
  - Progress reporting via events and IProgress<T>
  - Automatic manifest.json generation
  - Post-extraction validation
  - Feature detection (pets, mounts, 5th job, etc.)
  - Support for 64-bit, pre-BB, and standard WZ formats
- `HaCreator/GUI/UnpackWzToImg.cs` - Updated extraction UI with:
  - Version name input
  - Progress bar
  - Extraction log listbox
  - Cancel support
  - Success/error messaging
- `HaCreator/GUI/UnpackWzToImg.Designer.cs` - Enhanced UI layout

### Phase 3 Completed Files
- `HaCreator/Wz/StartupManager.cs` - Startup flow management with:
  - Version scanning and management
  - IDataSource creation for IMG filesystem
  - WzDataSource creation for WZ files
  - Configuration loading/saving
- `HaCreator/Wz/ImgDataExtractor.cs` - Data extraction from IDataSource:
  - Extracts all game data (strings, mobs, NPCs, items, maps, etc.)
  - Progress reporting via events
  - Works with both IMG filesystem and WZ sources
- `HaCreator/GUI/VersionSelector.cs` - Version selection UI:
  - Lists available IMG filesystem versions
  - Shows version details (encryption, format, features)
  - Options to select, delete, extract new, or use WZ
- `HaCreator/GUI/VersionSelector.Designer.cs` - UI layout
- `HaCreator/Program.cs` - Updated with:
  - `IDataSource DataSource` static field
  - `StartupManager` initialization
- `HaCreator/GUI/Initialization.cs` - Dual-mode support:
  - `button_initialiseImg_Click` for IMG filesystem
  - `InitializeFromImgFileSystem` method

### Phase 4 Completed Files
- `HaSharedLibrary/Wz/WzInfoTools.cs` - Added IDataSource overload for `FindMapImage`

### Phase 5 Completed Files
- `HaCreator/Wz/MapLoader.cs` - Updated `LoadToolTips` to use IDataSource
- `HaCreator/CustomControls/MapBrowser.cs` - Updated `InitializeMapsListboxItem` for IDataSource

### Phase 6 Completed Files
- `HaRepacker/GUI/MainForm.cs` - Added "Open Version Directory" menu, handlers, and File > Save support for VirtualWzDirectory
- `HaRepacker/GUI/MainForm.Designer.cs` - Added menu item for version directory
- `HaRepacker/GUI/ContextMenuManager.cs` - Added IMG file operations (Save, Create, Delete)
- `HaRepacker/WzNode.cs` - Added VirtualWzDirectory handling for tree expansion
- `MapleLib/MapleLib/Img/ImgFileSystemManager.cs` - Added SaveImage, CreateImage, DeleteImage methods
- `MapleLib/MapleLib/Img/VirtualWzDirectory.cs` - Added SaveImage, SaveAllChangedImages, WzFileParent override
- `MapleLib/MapleLib/WzLib/WzImageProperty.cs` - Fixed null-safe WzFileParent access for IMG filesystem objects

### Phase 7 Completed Files
- `MapleLib/MapleLib/Img/LRUCache.cs` - Thread-safe LRU cache with size-based eviction
- `MapleLib/MapleLib/Img/CategoryIndex.cs` - Category index files for fast lookup
- `MapleLib/MapleLib/Img/LazyWzImageDictionary.cs` - Lazy-loading dictionary that only loads images on-demand
- `MapleLib/MapleLib/Img/ImgFileSystemManager.cs` - Enhanced with:
  - **LRU Cache Integration**: Replaced unbounded `ConcurrentDictionary` with size-based `LRUCache<string, WzImage>`
  - 512MB default cache limit (configurable via `HaCreatorConfig.Cache.MaxMemoryCacheMB`)
  - `EstimateWzImageSize()` - Calculates actual memory usage per image (canvas pixels, strings, binary data)
  - Cycle detection in size estimation to prevent stack overflow from circular references
  - `GetImageNamesInDirectory()` - Returns file names WITHOUT loading images (for lazy loading)
  - Category index file support (index.json)
  - TrimCache for memory management
  - GenerateCategoryIndices for index generation
  - PreloadCategoriesAsync for parallel preloading
  - PreloadCommonCategoriesAsync for common data
- `MapleLib/MapleLib/Img/IDataSource.cs` - Added `GetImageNamesInDirectory()` interface method
- `MapleLib/MapleLib/Img/ImgFileSystemDataSource.cs` - Implemented `GetImageNamesInDirectory()` for all data sources
- `HaCreator/Wz/WzInformationManager.cs` - Changed TileSets/ObjectSets/BackgroundSets to `IDictionary<string, WzImage>` for lazy dictionary compatibility
- `HaCreator/Wz/ImgDataExtractor.cs` - Memory optimizations:
  - **TileSets**: Uses `LazyWzImageDictionary` - only names registered, tiles load when accessed
  - **ObjectSets**: Uses `LazyWzImageDictionary` - objects load on-demand
  - **BackgroundSets**: Uses `LazyWzImageDictionary` - backgrounds load on-demand
  - **MapsCache**: Stores `null` for WzImage, loads on-demand when map is opened
  - **NPC data**: No longer preloaded, loaded when user views NPC selector
  - Reduced parallel extraction degree to prevent memory spikes
- `HaCreator/CustomControls/MapBrowser.cs` - Added `LoadMapImageOnDemand()` for lazy map loading
- `HaCreator/GUI/Load.cs` - Added on-demand map loading and MapInfo creation
- `HaCreator/MapEditor/HaCreatorStateManager.cs` - Added on-demand map loading for simulator and MapInfo creation
- `HaCreator/GUI/InstanceEditor/LoadNpcSelector.cs` - NPCs now load on-demand when viewed
- `HaCreator/Wz/MapLoader.cs` - Added defensive null check for MapInfo with on-demand creation
- `MapleLib/MapleLib/Img/LRUCache.cs` - Disabled disposal on eviction (other caches hold property references)
- `MapleLib/MapleLib/Img/ImgFileSystemManager.cs` - Uses freeResources=true (lazy loading doesn't work for IMG files)

**Memory Optimization Summary**:
| Data Type | Before | After |
|-----------|--------|-------|
| Tiles (~100s) | All loaded at startup | Only loaded when accessed |
| Objects (~1000s) | All loaded at startup | Only loaded when accessed |
| Backgrounds (~100s) | All loaded at startup | Only loaded when accessed |
| Maps (~1000s) | All WzImages kept in memory | Only metadata kept; WzImage loaded on-demand |
| NPCs (~1000s) | All loaded at startup | Loaded individually when viewed |
| Mobs (~1000s) | All icons loaded at startup | Names from String.wz only; icons load on-demand |
| Items (~10000s) | All icons loaded at startup | Names from String.wz only; icons load on-demand |
| Skills (~1000s) | All loaded at startup | Names from String.wz only; images load on-demand |
| MapInfo | Created for all maps at startup | Created on-demand when map is opened |
| Image Cache | Unbounded (40GB+) | LRU cache limited to 512MB |

**Expected Results**: Initial memory usage reduced from 40GB+ to ~2-4GB for a 4GB extracted directory

**Key Implementation Details**:
- **freeResources=true**: IMG files are fully parsed and file handles closed immediately. Lazy loading (freeResources=false) doesn't work for IMG files because each file has its own reader that gets into a bad state after structure parsing.
- **No disposal on LRU eviction**: When images are evicted from the LRU cache, they are NOT disposed because other caches (MobIconCache, ItemIconCache, etc.) hold references to WzImageProperty objects inside the WzImages. Disposing would invalidate these references.
- **MapInfo on-demand**: MapInfo is created lazily when a map is actually opened, not during ExtractMaps. This is handled in `Load.cs`, `HaCreatorStateManager.cs`, and `MapLoader.CreateMapFromImage`.

### Phase 8 Completed Files
- `MapleLib/MapleLib/Img/BatchConverter.cs` - Batch conversion tool for multiple versions:
  - WzSourceConfig for configuring source paths
  - BatchProgress for tracking overall progress
  - BatchConversionResult for results
  - ScanForMapleInstallations for auto-detecting installations
- `HaCreator/GUI/MigrationWizard.cs` - Step-by-step migration wizard UI:
  - Welcome screen with feature explanation
  - Source selection with auto-detect support
  - Version configuration (name, encryption)
  - Progress tracking with detailed log
- `HaCreator/GUI/MigrationWizard.Designer.cs` - Wizard UI layout
- `HaCreator/GUI/VersionSelector.Designer.cs` - Added help tooltips for all buttons

### Phase 9 Completed Files
- `MapleLib/MapleLib/Img/IDataSource.cs` - DataSourceFactory already existed here (creates data sources based on mode)
- `MapleLib/MapleLib/Img/ImgFileSystemDataSource.cs` - Contains WzFileDataSource and HybridDataSource implementations:
  - `WzFileDataSource` - Wraps WzFileManager for legacy WZ file access
  - `HybridDataSource` - Tries IMG filesystem first, falls back to WZ files
- `HaCreator/Wz/StartupManager.cs` - Enhanced with:
  - `ConfiguredMode` property for accessing configured data source mode
  - `CreateDataSourceFromConfig()` method using DataSourceFactory
  - `CreateHybridDataSource()` for hybrid mode initialization
  - `SetDataSourceMode()` to update mode configuration
  - `ReloadConfig()` to refresh settings from disk
  - `StartupMode.Hybrid` enum value for hybrid mode
- `HaCreator/GUI/DataSourceSettings.cs` - Settings form for data source configuration:
  - Mode selection (IMG Filesystem, WZ Files, Hybrid)
  - Path configuration for IMG and WZ directories
  - Cache settings (max memory, max images, memory-mapped files)
  - Legacy options (WZ fallback, auto-convert)
  - Extraction settings (parallel threads, index generation, validation)
- `HaCreator/GUI/DataSourceSettings.Designer.cs` - Settings form UI layout
- `HaCreator/GUI/Initialization.cs` - Added Settings button click handler
- `HaCreator/GUI/Initialization.designer.cs` - Added Settings button to UI

### Phase 10 Completed Files
- `MapleLib.Tests/MapleLib.Tests.csproj` - Test project configuration with xUnit, Moq, and coverlet
- `MapleLib.Tests/Img/VersionManagerTests.cs` - Unit tests for VersionManager (18 tests):
  - Constructor, ScanVersions, GetVersion, VersionExists, DeleteVersion, AddExternalVersion, Refresh
- `MapleLib.Tests/Img/ImgFileSystemManagerTests.cs` - Unit tests for ImgFileSystemManager (17 tests):
  - Constructor, GetCategories, CategoryExists, GetSubdirectories, GetDirectory, LoadImage, GetStats, ClearCache
- `MapleLib.Tests/Img/DataSourceTests.cs` - Unit tests for data source components (26 tests):
  - DataSourceFactory tests (mode selection, error handling)
  - ImgFileSystemDataSource tests (initialization, categories, images, stats)
  - HybridDataSource tests (initialization, fallback behavior)
- `MapleLib.Tests/Img/HaCreatorConfigTests.cs` - Unit tests for configuration (15 tests):
  - Default values, Save/Load, directory creation, path properties
- `MapleLib.Tests/Img/LRUCacheTests.cs` - Unit tests for LRU cache (21 tests):
  - Add/Remove, eviction, thread safety, statistics, size-based eviction

**Test Results**: 77 tests passing, covering core IMG filesystem functionality

---

## Executive Summary

This document outlines a comprehensive plan to migrate HaCreator from loading MapleStory data directly from `.wz` archive files to using extracted `.img` files stored in the Windows filesystem. This architectural change enables:

- **Version-agnostic data storage**: Support any MapleStory version without requiring the original client
- **Git-trackable assets**: All game data can be version controlled
- **Easy modification**: Developers can add/remove/modify assets directly in the filesystem
- **Reduced complexity**: No need to handle WZ encryption/version detection at runtime
- **Scalability**: Support 20+ years of MapleStory versions without client dependencies

---

## Current Architecture Overview

### Data Flow (Current)
```
MapleStory Installation → WZ Files → WzFileManager → WzInformationManager → HaCreator UI
     (Encrypted)           (.wz)      (Parse/Cache)    (Extract Data)        (Display)
```

### Key Components
| Component | File | Purpose |
|-----------|------|---------|
| Entry Point | `HaCreator/Program.cs` | Initialize managers, launch UI |
| WZ Loader | `HaCreator/GUI/Initialization.cs` | Load WZ files, extract data |
| File Manager | `MapleLib/WzFileManager.cs` | Manage WZ file access |
| Data Cache | `HaCreator/Wz/WzInformationManager.cs` | Cache extracted game data |
| Extraction Tool | `HaCreator/GUI/UnpackWzToImg.cs` | Export WZ to IMG |

### Pain Points
1. Requires MapleStory client installation
2. Different encryption per region/version
3. 64-bit vs 32-bit vs pre-BB format detection
4. Cannot easily modify or track changes
5. Memory-intensive (loads entire WZ archives)

---

## Target Architecture

### Data Flow (New)
```
WZ Extraction Tool → IMG Files → ImgFileManager → WzInformationManager → HaCreator UI
   (One-time)        (Filesystem)   (Load IMG)       (Same caching)       (Display)
```

### Directory Structure
```
HaCreator_Data/
├── config.json                    # Configuration file
├── versions/                      # Multiple version support
│   ├── v83/                       # Example: v83 server
│   │   ├── manifest.json          # Version metadata
│   │   ├── String/
│   │   │   ├── Map.img
│   │   │   ├── Mob.img
│   │   │   ├── Npc.img
│   │   │   ├── Skill.img
│   │   │   ├── Eqp.img
│   │   │   ├── Etc.img
│   │   │   ├── Consume.img
│   │   │   └── Pet.img
│   │   ├── Map/
│   │   │   ├── Map/
│   │   │   │   ├── Map0/           # Victoria Island
│   │   │   │   │   ├── 100000000.img
│   │   │   │   │   ├── 100000100.img
│   │   │   │   │   └── ...
│   │   │   │   ├── Map1/           # Ossyria
│   │   │   │   ├── Map2/           # Ludus Lake
│   │   │   │   └── ...
│   │   │   ├── Obj/
│   │   │   │   ├── acc1/
│   │   │   │   │   ├── acc1.img
│   │   │   │   │   └── ...
│   │   │   │   └── ...
│   │   │   ├── Tile/
│   │   │   │   ├── grassySoil.img
│   │   │   │   ├── blackTile.img
│   │   │   │   └── ...
│   │   │   ├── Back/
│   │   │   │   ├── back.img
│   │   │   │   └── ...
│   │   │   ├── Physics/
│   │   │   │   └── Map000.img
│   │   │   └── WorldMap/
│   │   │       └── WorldMap.img
│   │   ├── Mob/
│   │   │   ├── 0100100.img
│   │   │   ├── 0100101.img
│   │   │   └── ...
│   │   ├── Npc/
│   │   │   ├── 1000000.img
│   │   │   └── ...
│   │   ├── Reactor/
│   │   │   ├── 1000000.img
│   │   │   └── ...
│   │   ├── Skill/
│   │   │   ├── 0.img              # Beginner
│   │   │   ├── 100.img            # Warrior
│   │   │   └── ...
│   │   ├── Sound/
│   │   │   ├── BgmGL.img
│   │   │   └── ...
│   │   ├── Character/
│   │   │   ├── Weapon/
│   │   │   ├── Cap/
│   │   │   └── ...
│   │   ├── Item/
│   │   │   ├── Consume/
│   │   │   ├── Etc/
│   │   │   └── ...
│   │   ├── UI/
│   │   │   ├── UIWindow.img
│   │   │   └── ...
│   │   └── Effect/
│   │       ├── BasicEff.img
│   │       └── ...
│   ├── v55/                       # Another version
│   │   └── ...
│   └── gms_v230/                  # 64-bit version
│       └── ...
└── custom/                        # User-created content
    └── ...
```

### Manifest File Format (manifest.json)
```json
{
  "version": "v83",
  "displayName": "GMS v83 (Pre-Big Bang)",
  "sourceRegion": "GMS",
  "extractedDate": "2025-01-15T10:30:00Z",
  "encryption": "GMS",
  "is64Bit": false,
  "hasCanvasFormat": false,
  "canvasDirectories": [],
  "categories": {
    "String": { "fileCount": 8, "lastModified": "2025-01-15T10:30:00Z" },
    "Map": { "fileCount": 1250, "lastModified": "2025-01-15T10:35:00Z" },
    "Mob": { "fileCount": 890, "lastModified": "2025-01-15T10:40:00Z" }
  },
  "features": {
    "hasPets": true,
    "hasMount": true,
    "hasAndroid": false,
    "maxLevel": 200
  }
}
```

#### Manifest Fields for 64-bit _Canvas Format

| Field | Type | Description |
|-------|------|-------------|
| `is64Bit` | boolean | Whether source was 64-bit MapleStory (v150+) |
| `hasCanvasFormat` | boolean | Whether source used `_Canvas` directory structure |
| `canvasDirectories` | string[] | List of categories with `_Canvas` subdirectories |

**Example for 64-bit MapleStory with _Canvas:**
```json
{
  "version": "v230",
  "displayName": "GMS v230 (64-bit)",
  "sourceRegion": "GMS",
  "extractedDate": "2025-01-15T10:30:00Z",
  "encryption": "GMS",
  "is64Bit": true,
  "hasCanvasFormat": true,
  "canvasDirectories": [
    "Map/_Canvas",
    "Map/Back/_Canvas",
    "Map/Tile/_Canvas",
    "Map/Obj/_Canvas",
    "Mob/_Canvas",
    "Npc/_Canvas",
    "Character/_Canvas",
    "Skill/_Canvas",
    "Item/_Canvas",
    "Effect/_Canvas"
  ],
  "categories": {
    "String": { "fileCount": 12, "lastModified": "2025-01-15T10:30:00Z" },
    "Map": { "fileCount": 3500, "lastModified": "2025-01-15T10:35:00Z", "hasCanvas": true },
    "Mob": { "fileCount": 2500, "lastModified": "2025-01-15T10:40:00Z", "hasCanvas": true }
  },
  "features": {
    "hasPets": true,
    "hasMount": true,
    "hasAndroid": true,
    "has5thJob": true,
    "maxLevel": 300
  }
}
```

---

## Migration Phases

### Phase 1: Foundation Layer (Core Infrastructure)
**Goal**: Create the filesystem-based data loading infrastructure without breaking existing functionality.

#### 1.1 Create ImgFileSystemManager
**New File**: `MapleLib/MapleLib/ImgFileSystemManager.cs`

```csharp
public class ImgFileSystemManager : IDisposable
{
    // Core paths
    public string RootDirectory { get; }
    public string CurrentVersionPath { get; }

    // Version management
    public List<VersionInfo> AvailableVersions { get; }
    public VersionInfo CurrentVersion { get; }

    // Image loading
    public WzImage LoadImage(string category, string relativePath);
    public IEnumerable<WzImage> LoadImagesFromDirectory(string category, string subDir);
    public WzDirectory LoadDirectoryAsVirtual(string category);

    // Caching
    private Dictionary<string, WzImage> _imageCache;
    private LRUCache<string, WzImage> _lruCache;

    // Index for fast lookups
    private Dictionary<string, List<string>> _categoryIndex;
}
```

#### 1.2 Create Version Manager
**New File**: `MapleLib/MapleLib/VersionManager.cs`

```csharp
public class VersionManager
{
    public List<VersionInfo> ScanVersions(string rootDir);
    public VersionInfo LoadVersionManifest(string versionPath);
    public void CreateVersionManifest(string versionPath, VersionInfo info);
    public bool ValidateVersionIntegrity(string versionPath);
}
```

#### 1.3 Create Virtual WzDirectory
**Purpose**: Provide WzDirectory-compatible interface over filesystem directory

```csharp
public class VirtualWzDirectory : WzDirectory
{
    private string _filesystemPath;

    // Override to load from filesystem instead of WZ archive
    public override WzImage this[string name] { get; }
    public override IEnumerable<WzImage> WzImages { get; }
}
```

#### Tasks:
- [ ] Create `ImgFileSystemManager` class
- [ ] Create `VersionManager` class
- [ ] Create `VirtualWzDirectory` class
- [ ] Create `VersionInfo` data class
- [ ] Add configuration file support
- [ ] Write unit tests for new classes

---

### Phase 2: Enhanced Extraction Tool
**Goal**: Improve the WZ-to-IMG extraction tool to produce properly organized output.

#### 2.1 Refactor UnpackWzToImg
**File**: `HaCreator/GUI/UnpackWzToImg.cs`

Changes:
- Add version selection/naming
- Create proper directory structure
- Generate manifest.json
- Progress reporting with detailed status
- Validation after extraction
- Support for incremental extraction (only changed files)

#### 2.2 New Extraction Features
```csharp
public class WzExtractionService
{
    // Main extraction methods
    public async Task ExtractToVersionDirectory(
        string wzSourcePath,
        string outputVersionPath,
        WzMapleVersion encryption,
        IProgress<ExtractionProgress> progress);

    // Category-specific extraction
    public async Task ExtractCategory(
        string wzSourcePath,
        string category,
        string outputPath);

    // Validation
    public ExtractionValidationResult ValidateExtraction(string versionPath);

    // Incremental update
    public async Task UpdateFromWz(
        string wzSourcePath,
        string existingVersionPath,
        IProgress<ExtractionProgress> progress);
}
```

#### 2.3 New Extraction UI
**New File**: `HaCreator/GUI/WzExtractorWindow.xaml`

Features:
- Source WZ folder selection
- Version naming and metadata entry
- Progress bar with detailed status
- Extraction log viewer
- Post-extraction validation report

#### Tasks:
- [ ] Create `WzExtractionService` class
- [ ] Design new extraction UI (WPF)
- [ ] Implement progress reporting
- [ ] Add manifest generation
- [ ] Add extraction validation
- [ ] Add incremental update support
- [ ] Update existing UnpackWzToImg to use new service

---

### Phase 3: Initialization Flow Refactor
**Goal**: Replace WZ-based initialization with IMG filesystem initialization.

#### 3.1 New Initialization Flow
**File**: `HaCreator/GUI/Initialization.cs` (Major Refactor)

```
Old Flow:
1. Select MapleStory folder
2. Detect WZ format (64-bit/pre-BB)
3. Load WZ files sequentially
4. Extract data to caches
5. Launch editor

New Flow:
1. Check for existing versions in HaCreator_Data/versions/
2. If none: Show extraction wizard
3. If exists: Show version selector
4. Load selected version via ImgFileSystemManager
5. Extract data to caches (same process, different source)
6. Launch editor
```

#### 3.2 Version Selector UI
**New File**: `HaCreator/GUI/VersionSelector.xaml`

Features:
- List available versions with metadata
- Version preview (icon, file count, last modified)
- "Import from WZ" button → Opens extraction wizard
- "Create New Version" for custom content
- "Delete Version" with confirmation
- Last-used version remembered

#### 3.3 Initialization Refactoring

```csharp
// Old approach (to be deprecated)
private async Task InitializeFromWzFiles(string mapleStoryPath)
{
    Program.WzManager = new WzFileManager(mapleStoryPath);
    // Load WZ files...
}

// New approach
private async Task InitializeFromImgFileSystem(string versionPath)
{
    Program.ImgManager = new ImgFileSystemManager(versionPath);
    // Load IMG files with same extraction logic...
}
```

#### Tasks:
- [ ] Create `VersionSelector` UI
- [ ] Refactor `Initialization.cs` for dual-mode support
- [ ] Create initialization service abstraction
- [ ] Implement version validation on load
- [ ] Add "first run" experience for new users
- [ ] Preserve backward compatibility with WZ loading

---

### Phase 4: Data Loading Adaptation
**Goal**: Update all data extraction code to work with IMG filesystem.

#### 4.1 Create Data Loading Abstraction
**New File**: `HaCreator/Wz/IDataSource.cs`

```csharp
public interface IDataSource
{
    WzImage GetImage(string category, string imageName);
    IEnumerable<WzImage> GetImagesInCategory(string category);
    IEnumerable<WzImage> GetImagesInDirectory(string category, string subDir);
    bool ImageExists(string category, string imageName);
}

public class WzFileDataSource : IDataSource { /* Wraps WzFileManager */ }
public class ImgFileSystemDataSource : IDataSource { /* Wraps ImgFileSystemManager */ }
```

#### 4.2 Refactor WzInformationManager
**File**: `HaCreator/Wz/WzInformationManager.cs`

Changes:
- Accept `IDataSource` instead of direct WzFileManager dependency
- Update all extraction methods to use abstraction
- Maintain same public API for consumers

```csharp
public class WzInformationManager
{
    private IDataSource _dataSource;

    public WzInformationManager(IDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // All existing methods remain, but use _dataSource internally
    public void ExtractMobFile()
    {
        var mobImages = _dataSource.GetImagesInCategory("Mob");
        foreach (var mobImage in mobImages)
        {
            // Same extraction logic...
        }
    }
}
```

#### 4.3 Update Extraction Methods
Each extraction method needs review:

| Method | Current Source | New Source |
|--------|---------------|------------|
| `ExtractStringFile()` | `Program.WzManager["string"]` | `_dataSource.GetImage("String", "Map.img")` |
| `ExtractMobFile()` | `Program.WzManager.GetWzDirectoriesFromBase("mob")` | `_dataSource.GetImagesInCategory("Mob")` |
| `ExtractNpcFile()` | `Program.WzManager.GetWzDirectoriesFromBase("npc")` | `_dataSource.GetImagesInCategory("Npc")` |
| `ExtractMapMarks()` | `mapWzFile["MapHelper.img"]` | `_dataSource.GetImage("Map", "MapHelper.img")` |
| `ExtractMapTileSets()` | `tileWzFile.WzDirectories` | `_dataSource.GetImagesInDirectory("Map", "Tile")` |
| `ExtractMaps()` | Multiple map WZ files | `_dataSource.GetImagesInDirectory("Map", "Map/Map*")` |

#### Tasks:
- [ ] Create `IDataSource` interface
- [ ] Implement `WzFileDataSource` (backward compatible)
- [ ] Implement `ImgFileSystemDataSource`
- [ ] Refactor `WzInformationManager` to use interface
- [ ] Update all extraction methods
- [ ] Test with both data sources
- [ ] Create integration tests

---

### Phase 5: MapLoader and Editor Adaptation
**Goal**: Update map loading and editing to work with IMG filesystem.

#### 5.1 Refactor MapLoader
**File**: `HaCreator/Wz/MapLoader.cs`

Key methods to update:
- `LoadLayers()` - Load tile/obj from filesystem
- `LoadLife()` - Load mob/npc references
- `LoadFootholds()` - Pure data, minimal changes
- `LoadPortals()` - Load portal properties
- `LoadBackgrounds()` - Load background images

#### 5.2 Resource Resolution
Create a unified resource resolver:

```csharp
public class ResourceResolver
{
    private IDataSource _dataSource;

    // Resolve cross-references (e.g., mob icon from mob ID)
    public WzCanvasProperty ResolveMobIcon(string mobId);
    public WzCanvasProperty ResolveNpcIcon(string npcId);
    public WzImage ResolveTileSet(string tileSetName);
    public WzImage ResolveObjectSet(string objSetName);

    // Handle UOL (unit object link) resolution
    public WzObject ResolveUOL(WzUOLProperty uol);
}
```

#### 5.3 Map Saving Adaptation
When saving maps, ensure proper IMG file handling:

```csharp
public class MapSaver
{
    public void SaveMapToImg(Board board, string outputPath);
    public void SaveMapToVersion(Board board, string versionPath, string mapId);
}
```

#### Tasks:
- [ ] Refactor `MapLoader` to use `IDataSource`
- [ ] Create `ResourceResolver` class
- [ ] Update tile/object loading
- [ ] Update background loading
- [ ] Create `MapSaver` for IMG output
- [ ] Test map load/save cycle
- [ ] Verify visual parity with WZ-based loading

---

### Phase 6: HaRepacker Integration
**Goal**: Ensure HaRepacker can work with IMG filesystem structure.

#### 6.1 Add Filesystem View Mode
**File**: `HaRepacker/GUI/MainForm.cs`

New features:
- Open version directory (not individual WZ files)
- Tree view shows filesystem structure
- Edit IMG files directly
- Create new IMG files
- Delete IMG files with confirmation

#### 6.2 Synchronize with HaCreator Data
- When HaRepacker edits an IMG file, HaCreator can detect changes
- Optional: File watcher for live reload
- Version integrity check after edits

#### Tasks:
- [ ] Add "Open Version Directory" menu option
- [ ] Create filesystem-based tree view
- [ ] Implement direct IMG editing
- [ ] Add new file creation
- [ ] Add file deletion
- [ ] Implement change detection
- [ ] Test integration with HaCreator

---

### Phase 7: Performance Optimization
**Goal**: Ensure IMG filesystem approach performs as well or better than WZ loading.

#### 7.1 Implement Lazy Loading
```csharp
public class LazyWzImage
{
    private string _filePath;
    private WzImage _image;
    private bool _parsed;

    public WzImage Image
    {
        get
        {
            if (!_parsed) LoadAndParse();
            return _image;
        }
    }
}
```

#### 7.2 Add Caching Layers
```csharp
public class ImgCacheManager
{
    // L1: In-memory parsed images (LRU, configurable size)
    private LRUCache<string, WzImage> _memoryCache;

    // L2: Memory-mapped files for large images
    private Dictionary<string, MemoryMappedFile> _mmfCache;

    // Preload commonly used images
    public void PreloadCategory(string category);
    public void PreloadImages(IEnumerable<string> imagePaths);
}
```

#### 7.3 Parallel Loading
```csharp
public async Task<IEnumerable<WzImage>> LoadImagesParallelAsync(
    IEnumerable<string> imagePaths,
    int maxDegreeOfParallelism = 4)
{
    return await Task.WhenAll(
        imagePaths.Select(p => LoadImageAsync(p))
    );
}
```

#### 7.4 Index Files for Fast Lookup
Create index files to avoid directory scanning:

```json
// Map/index.json
{
  "images": [
    { "name": "100000000.img", "size": 45230, "lastModified": "2025-01-15" },
    { "name": "100000100.img", "size": 32100, "lastModified": "2025-01-15" }
  ],
  "directories": ["Map0", "Map1", "Map2"],
  "totalCount": 1250
}
```

#### Tasks:
- [ ] Implement lazy loading wrapper
- [ ] Create LRU cache manager
- [ ] Add memory-mapped file support for large images
- [ ] Implement parallel loading
- [ ] Create category index files
- [ ] Benchmark against WZ loading
- [ ] Profile memory usage
- [ ] Optimize hot paths

---

### Phase 8: Migration Tools and Documentation
**Goal**: Provide tools and documentation for users migrating to the new system.

#### 8.1 Migration Wizard
**New File**: `HaCreator/GUI/MigrationWizard.xaml`

Steps:
1. Welcome screen explaining the new system
2. Detect existing WZ-based projects
3. Select WZ files to convert
4. Configure version metadata
5. Run extraction with progress
6. Validate and report results
7. Update project settings

#### 8.2 Batch Conversion Tool
For users with multiple versions:

```csharp
public class BatchConverter
{
    public async Task ConvertMultipleVersions(
        IEnumerable<WzSourceConfig> sources,
        string outputRoot,
        IProgress<BatchProgress> progress);
}
```

#### 8.3 Documentation
- User guide for new workflow
- Developer guide for API changes
- Migration guide from old versions
- Troubleshooting guide

#### Tasks:
- [ ] Create migration wizard UI
- [ ] Implement batch conversion
- [ ] Write user documentation
- [ ] Write developer documentation
- [ ] Create video tutorial
- [ ] Add in-app help tooltips

---

### Phase 9: Backward Compatibility Layer
**Goal**: Maintain ability to load from WZ files for users who need it.

#### 9.1 Dual-Mode Support
```csharp
public enum DataSourceMode
{
    ImgFileSystem,  // New default
    WzFiles,        // Legacy support
    Hybrid          // IMG with WZ fallback
}

public class DataSourceFactory
{
    public static IDataSource Create(DataSourceMode mode, string path)
    {
        return mode switch
        {
            DataSourceMode.ImgFileSystem => new ImgFileSystemDataSource(path),
            DataSourceMode.WzFiles => new WzFileDataSource(path),
            DataSourceMode.Hybrid => new HybridDataSource(path),
            _ => throw new ArgumentException()
        };
    }
}
```

#### 9.2 Settings Integration
Add settings for:
- Default data source mode
- WZ file path (for legacy)
- IMG directory path
- Auto-convert WZ on load option

#### Tasks:
- [ ] Implement dual-mode initialization
- [ ] Create hybrid data source
- [ ] Add settings UI for mode selection
- [ ] Test backward compatibility
- [ ] Document legacy mode usage

---

### Phase 10: Testing and Stabilization
**Goal**: Comprehensive testing before release.

#### 10.1 Test Categories
1. **Unit Tests**
   - ImgFileSystemManager
   - VersionManager
   - ResourceResolver
   - Cache managers

2. **Integration Tests**
   - Full extraction workflow
   - Full load workflow
   - Map editing cycle
   - HaRepacker integration

3. **Performance Tests**
   - Load time comparison (WZ vs IMG)
   - Memory usage comparison
   - Large version handling (1000+ maps)

4. **Compatibility Tests**
   - v55 (very old)
   - v62
   - v83 (popular private server)
   - v92
   - v176
   - v230+ (64-bit)

#### 10.2 Beta Testing
- Release beta with both modes
- Collect user feedback
- Fix reported issues
- Performance tuning based on real usage

#### Tasks:
- [ ] Write unit tests (target: 80% coverage)
- [ ] Write integration tests
- [ ] Create performance benchmarks
- [ ] Test with multiple MS versions
- [ ] Beta release
- [ ] Bug fixes and stabilization
- [ ] Final release

---

## Implementation Timeline

| Phase | Description | Dependencies | Estimated Complexity |
|-------|-------------|--------------|---------------------|
| 1 | Foundation Layer | None | High |
| 2 | Enhanced Extraction | Phase 1 | Medium |
| 3 | Initialization Refactor | Phases 1, 2 | High |
| 4 | Data Loading Adaptation | Phase 3 | High |
| 5 | MapLoader Adaptation | Phase 4 | Medium |
| 6 | HaRepacker Integration | Phase 4 | Medium |
| 7 | Performance Optimization | Phase 5 | Medium |
| 8 | Migration Tools | Phases 1-6 | Low |
| 9 | Backward Compatibility | Phase 4 | Low |
| 10 | Testing | All | Medium |

---

## Risk Assessment

### High Risk
- **Breaking existing workflows**: Mitigated by backward compatibility layer
- **Performance regression**: Mitigated by caching and lazy loading
- **Data corruption during migration**: Mitigated by validation and checksums

### Medium Risk
- **Disk space usage**: IMG files larger than WZ; document requirements
- **Git repository size**: Large binary files; recommend Git LFS
- **Cross-platform paths**: Use Path.Combine consistently

### Low Risk
- **Learning curve for users**: Provide wizard and documentation
- **HaRepacker compatibility**: Same underlying WzImage format

---

## File Changes Summary

### New Files
```
MapleLib/MapleLib/
├── ImgFileSystemManager.cs
├── VersionManager.cs
├── VirtualWzDirectory.cs
└── Models/
    ├── VersionInfo.cs
    └── ExtractionProgress.cs

HaCreator/
├── Services/
│   ├── IDataSource.cs
│   ├── WzFileDataSource.cs
│   ├── ImgFileSystemDataSource.cs
│   ├── HybridDataSource.cs
│   ├── WzExtractionService.cs
│   └── ResourceResolver.cs
├── GUI/
│   ├── VersionSelector.xaml
│   ├── VersionSelector.xaml.cs
│   ├── WzExtractorWindow.xaml
│   ├── WzExtractorWindow.xaml.cs
│   ├── MigrationWizard.xaml
│   └── MigrationWizard.xaml.cs
└── Cache/
    ├── ImgCacheManager.cs
    └── LRUCache.cs
```

### Modified Files
```
MapleLib/MapleLib/
├── WzFileManager.cs (add IDataSource implementation)
└── WzLib/WzImage.cs (minor updates for standalone loading)

HaCreator/
├── Program.cs (add ImgManager, update initialization)
├── GUI/Initialization.cs (major refactor for dual-mode)
├── GUI/UnpackWzToImg.cs (use new extraction service)
├── Wz/WzInformationManager.cs (use IDataSource)
├── Wz/MapLoader.cs (use IDataSource)
└── App.config (new settings)

HaRepacker/
├── GUI/MainForm.cs (add filesystem view mode)
└── Program.cs (support version directories)
```

---

## Appendix A: Configuration Schema

### config.json
```json
{
  "dataSourceMode": "ImgFileSystem",
  "imgRootPath": "./HaCreator_Data",
  "lastUsedVersion": "v83",
  "cache": {
    "maxMemoryCacheMB": 512,
    "preloadCategories": ["String", "Map/MapHelper.img"],
    "enableMemoryMappedFiles": true
  },
  "extraction": {
    "defaultOutputPath": "./HaCreator_Data/versions",
    "generateIndex": true,
    "validateAfterExtract": true
  },
  "legacy": {
    "wzFilePath": "",
    "allowWzFallback": false
  }
}
```

---

## Appendix B: API Changes

### Deprecated APIs (Phase 9+)
```csharp
// Old
Program.WzManager.LoadWzFile("map", WzMapleVersion.GMS);
Program.WzManager.GetWzDirectoriesFromBase("mob");
Program.WzManager["string"]["Map.img"];

// New
Program.DataSource.GetImagesInCategory("Map");
Program.DataSource.GetImagesInCategory("Mob");
Program.DataSource.GetImage("String", "Map.img");
```

### New APIs
```csharp
// Version management
VersionManager.ScanVersions(rootPath);
VersionManager.LoadVersionManifest(versionPath);

// Data source
DataSourceFactory.Create(mode, path);
IDataSource.GetImage(category, name);
IDataSource.GetImagesInCategory(category);

// Extraction
WzExtractionService.ExtractToVersionDirectory(source, output, progress);

// Cache
ImgCacheManager.PreloadCategory(category);
ImgCacheManager.ClearCache();
```

---

## Appendix C: Migration Checklist

### Before Starting
- [ ] Back up existing projects
- [ ] Ensure sufficient disk space (2-3x WZ size)
- [ ] Note current MapleStory version being used

### During Migration
- [ ] Run extraction wizard
- [ ] Verify all categories extracted
- [ ] Check manifest.json created
- [ ] Validate file counts match expectations

### After Migration
- [ ] Load a map and verify visuals
- [ ] Test mob/npc icons display
- [ ] Test map saving
- [ ] Remove WZ file dependency (optional)

---

## Appendix D: Git LFS Configuration

For repositories storing IMG files:

```bash
# .gitattributes
*.img filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text

# Initialize LFS
git lfs install
git lfs track "*.img"
```

---

## Appendix E: Future Improvements (Backlog)

The following improvements are candidates for future development. Priority and implementation order to be determined.

### 1. Error Recovery & Resilience

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Checksum Validation** | Store SHA256 hash per IMG file in manifest.json; validate on load | Detect corrupted files early |
| **Auto-Repair** | If IMG corrupted and WZ source available, re-extract automatically | Self-healing system |
| **Partial Loading** | If one category fails to load, continue with others and show warnings | Graceful degradation |
| **Backup System** | Auto-backup before destructive operations (delete, overwrite, batch edit) | Data safety |
| **Transaction Log** | Log all modifications with ability to rollback | Undo catastrophic changes |

```csharp
// Example: Checksum validation in manifest.json
{
  "files": {
    "Map/Map0/100000000.img": {
      "size": 45230,
      "sha256": "a1b2c3d4...",
      "lastModified": "2025-01-15T10:30:00Z"
    }
  }
}
```

### 2. Version Diffing & Comparison

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Visual Diff Tool** | Side-by-side comparison of two versions | Understand version differences |
| **Change Detection** | Highlight new/modified/deleted assets between versions | Track content evolution |
| **Diff Report** | Generate HTML/JSON report of differences | Documentation & auditing |
| **Merge Tool** | Combine assets from multiple versions with conflict resolution | Create custom versions |
| **Ancestry Tracking** | Track which version another was derived from | Version lineage |

```csharp
public class VersionComparer
{
    public VersionDiff Compare(string versionA, string versionB);
    public MergeResult Merge(string baseVersion, string[] sourceVersions, MergeStrategy strategy);
}

public class VersionDiff
{
    public List<string> AddedFiles { get; set; }
    public List<string> RemovedFiles { get; set; }
    public List<string> ModifiedFiles { get; set; }
    public Dictionary<string, PropertyDiff> PropertyChanges { get; set; }
}
```

### 3. Search & Discovery

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Full-Text Search** | Search across all IMG files for property names/values | Find assets quickly |
| **Asset Browser** | Visual browser for sprites, sounds, maps with thumbnails | Discover content |
| **Cross-Reference Finder** | "Where is this tile/object used?" functionality | Impact analysis |
| **Tag System** | User-defined tags on assets for organization | Custom categorization |
| **Search Index** | Pre-built search index for instant results | Fast queries |

```csharp
public class AssetSearchService
{
    // Full-text search
    public SearchResults Search(string query, SearchOptions options);

    // Find usages
    public List<UsageReference> FindUsages(string assetPath);

    // Asset browser
    public AssetThumbnail GetThumbnail(string assetPath);
}

public class SearchOptions
{
    public string[] Categories { get; set; }  // Filter by category
    public string PropertyName { get; set; }  // Search specific property
    public bool RegexEnabled { get; set; }    // Regex search
    public int MaxResults { get; set; }
}
```

### 4. CLI Tooling

| Command | Description | Use Case |
|---------|-------------|----------|
| `extract` | Headless WZ extraction | Automation, CI/CD |
| `validate` | Verify version integrity | Pre-deployment checks |
| `diff` | Compare two versions | Release notes generation |
| `search` | Search assets from command line | Scripting |
| `info` | Display version metadata | Quick inspection |
| `export` | Export to portable package | Sharing |

```bash
# Example CLI usage
hacreator-cli extract --wz "C:\MapleStory" --output "./versions/v83" --name "v83" --encryption GMS

hacreator-cli validate "./versions/v83" --checksum --report validation.json

hacreator-cli diff "./versions/v83" "./versions/v92" --output diff-report.html

hacreator-cli search "./versions/v83" --query "mobTime" --category Mob

hacreator-cli info "./versions/v83"

hacreator-cli export "./versions/v83" --output "v83-portable.zip" --include-manifest
```

```csharp
// CLI entry point
public class HaCreatorCli
{
    [Command("extract")]
    public int Extract(
        [Option("wz")] string wzPath,
        [Option("output")] string outputPath,
        [Option("name")] string versionName,
        [Option("encryption")] WzMapleVersion encryption);

    [Command("validate")]
    public int Validate(
        [Argument] string versionPath,
        [Option("checksum")] bool verifyChecksums,
        [Option("report")] string reportPath);
}
```

### 5. Hot Reload Support

| Feature | Description | Benefit |
|---------|-------------|---------|
| **File Watcher** | Detect external IMG edits in real-time | External tool integration |
| **Live Preview** | See changes immediately in MapSimulator | Rapid iteration |
| **Selective Reload** | Only reload changed files, not entire version | Performance |
| **Change Notification** | Notify user when external changes detected | Awareness |
| **Conflict Detection** | Warn if file changed externally while editing in HaCreator | Prevent data loss |

```csharp
public class HotReloadService : IDisposable
{
    private FileSystemWatcher _watcher;

    public event EventHandler<FileChangedEventArgs> FileChanged;
    public event EventHandler<FileConflictEventArgs> ConflictDetected;

    public void StartWatching(string versionPath);
    public void StopWatching();
    public void ReloadFile(string filePath);
    public void ReloadCategory(string category);
}
```

### 6. Cloud & Sharing

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Portable Package Export** | Zip version with manifest and all dependencies | Easy sharing |
| **Import from URL** | Download pre-extracted versions from URL | Community resources |
| **Version Registry** | Browse/download community-shared versions | Asset marketplace |
| **Delta Updates** | Download only changed files when updating | Bandwidth efficiency |
| **Cloud Backup** | Optional backup to cloud storage | Data protection |

```csharp
public class VersionPackageService
{
    // Export
    public async Task ExportToPackage(string versionPath, string outputZip, PackageOptions options);

    // Import
    public async Task ImportFromPackage(string packagePath, string outputVersionPath);
    public async Task ImportFromUrl(string url, string outputVersionPath, IProgress<double> progress);

    // Delta updates
    public async Task<DeltaManifest> CalculateDelta(string localVersion, string remoteManifestUrl);
    public async Task ApplyDelta(string versionPath, DeltaManifest delta);
}

public class PackageOptions
{
    public bool IncludeManifest { get; set; } = true;
    public bool IncludeIndex { get; set; } = true;
    public CompressionLevel Compression { get; set; } = CompressionLevel.Optimal;
    public string[] ExcludeCategories { get; set; }
}
```

### 7. Performance Dashboard

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Memory Monitor** | Real-time cache usage display in UI | Resource awareness |
| **Load Time Metrics** | Track which categories/files are slowest | Optimization targets |
| **Cache Statistics** | Hit/miss ratio, eviction count | Cache tuning |
| **Performance Log** | Record performance metrics over time | Trend analysis |
| **Recommendations** | Suggest optimal cache size based on usage | Auto-tuning |

```csharp
public class PerformanceDashboard
{
    // Real-time metrics
    public CacheStatistics GetCacheStats();
    public MemoryUsage GetMemoryUsage();
    public LoadTimeMetrics GetLoadTimes();

    // Historical data
    public PerformanceHistory GetHistory(TimeSpan period);

    // Recommendations
    public OptimizationRecommendations GetRecommendations();
}

public class CacheStatistics
{
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public long EvictionCount { get; set; }
    public long CurrentSizeBytes { get; set; }
    public long MaxSizeBytes { get; set; }
    public Dictionary<string, int> HitsByCategory { get; set; }
}
```

### 8. Extended Testing

| Test Type | Description | Benefit |
|-----------|-------------|---------|
| **Fuzz Testing** | Test with corrupted/malformed IMG files | Robustness |
| **Memory Leak Detection** | Long-running stress tests | Stability |
| **Cross-Platform Testing** | Verify paths work on Linux (Mono/Wine) | Portability |
| **Regression Testing** | Compare output with known-good baseline | Prevent bugs |
| **Load Testing** | Test with 100+ versions loaded | Scalability |
| **Chaos Testing** | Random file deletions/corruptions during operation | Resilience |

```csharp
// Fuzz testing example
[TestClass]
public class FuzzTests
{
    [TestMethod]
    public void LoadCorruptedImgFile_ShouldNotCrash()
    {
        var corruptedFiles = GenerateCorruptedImgFiles(100);
        foreach (var file in corruptedFiles)
        {
            var ex = Record.Exception(() => ImgFileSystemManager.LoadImage(file));
            Assert.IsNotInstanceOfType(ex, typeof(AccessViolationException));
            Assert.IsNotInstanceOfType(ex, typeof(StackOverflowException));
        }
    }

    [TestMethod]
    public async Task LongRunningStressTest_NoMemoryLeak()
    {
        var initialMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 10000; i++)
        {
            await LoadAndUnloadRandomVersion();
            if (i % 100 == 0) GC.Collect();
        }

        var finalMemory = GC.GetTotalMemory(true);
        var growth = finalMemory - initialMemory;
        Assert.IsTrue(growth < 100_000_000, $"Memory grew by {growth} bytes");
    }
}
```

### 9. Documentation Enhancements

| Document | Description | Audience |
|----------|-------------|----------|
| **Troubleshooting Guide** | Common errors and solutions | End users |
| **Architecture Diagrams** | Visual representation of data flow | Developers |
| **API Reference** | Auto-generated from XML comments | Developers |
| **Video Tutorials** | Step-by-step visual guides | New users |
| **Contributing Guide** | How to contribute to the project | Contributors |
| **Sample Projects** | Example versions and configurations | All users |
| **FAQ** | Frequently asked questions | All users |

```markdown
# Suggested Documentation Structure
docs/
├── user-guide/
│   ├── getting-started.md
│   ├── extracting-wz-files.md
│   ├── version-management.md
│   ├── troubleshooting.md
│   └── faq.md
├── developer-guide/
│   ├── architecture.md
│   ├── api-reference.md
│   ├── contributing.md
│   ├── building.md
│   └── testing.md
├── tutorials/
│   ├── video-links.md
│   ├── creating-custom-maps.md
│   └── sharing-versions.md
└── samples/
    ├── sample-config.json
    └── sample-manifest.json
```

### 10. Plugin System

| Feature | Description | Benefit |
|---------|-------------|---------|
| **Plugin API** | Well-defined extension points | Extensibility |
| **Custom Extractors** | Plugins for new WZ formats | Future-proofing |
| **Custom Data Sources** | Database-backed, network, etc. | Flexibility |
| **UI Extensions** | Custom panels and tools | Customization |
| **Event Hooks** | Subscribe to load/save/edit events | Integration |

```csharp
public interface IHaCreatorPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize(IPluginContext context);
    void Shutdown();
}

public interface IPluginContext
{
    IDataSource DataSource { get; }
    IEventBus EventBus { get; }
    IUIHost UIHost { get; }
    ILogger Logger { get; }
}

public interface ICustomDataSource : IDataSource
{
    // Plugins can implement custom data sources
}

public interface ICustomExtractor
{
    bool CanExtract(string wzFilePath);
    Task ExtractAsync(string wzFilePath, string outputPath, IProgress<double> progress);
}
```

### 11. Multi-User & Collaboration

| Feature | Description | Benefit |
|---------|-------------|---------|
| **File Locking** | Prevent concurrent edits to same file | Conflict prevention |
| **Change Tracking** | Who changed what and when | Audit trail |
| **Merge Conflicts UI** | Visual conflict resolution | Team workflow |
| **Shared Cache Server** | Central cache for team | Resource sharing |
| **Activity Feed** | See recent changes by team members | Awareness |

```csharp
public class CollaborationService
{
    // Locking
    public LockResult AcquireLock(string filePath, string userId);
    public void ReleaseLock(string filePath, string userId);
    public LockInfo GetLockInfo(string filePath);

    // Change tracking
    public void RecordChange(string filePath, string userId, ChangeType type);
    public List<ChangeRecord> GetChangeHistory(string filePath);
    public List<ChangeRecord> GetRecentChanges(TimeSpan period);
}

public class LockInfo
{
    public string FilePath { get; set; }
    public string LockedBy { get; set; }
    public DateTime LockedAt { get; set; }
    public bool IsLocked => !string.IsNullOrEmpty(LockedBy);
}
```

### Priority Matrix

| Improvement | Impact | Effort | Suggested Priority |
|-------------|--------|--------|-------------------|
| CLI Tooling | High | Medium | P1 |
| Error Recovery & Resilience | High | Medium | P1 |
| Search & Discovery | High | High | P2 |
| Hot Reload Support | Medium | Low | P2 |
| Performance Dashboard | Medium | Low | P2 |
| Version Diffing | Medium | High | P3 |
| Extended Testing | Medium | Medium | P3 |
| Documentation | Medium | Low | P3 |
| Cloud & Sharing | Low | High | P4 |
| Plugin System | Low | High | P4 |
| Multi-User Collaboration | Low | Very High | P5 |

---

## Appendix F: _Canvas Format Packing Plan

This appendix details how to pack IMG filesystem data back into the 64-bit MapleStory `_Canvas` WZ format.

### F.1 Overview

The `_Canvas` format is used in 64-bit MapleStory (v150+) to distribute large image assets across multiple WZ files. When packing IMG files back to WZ format for 64-bit clients, we must:

1. **Detect** if source data originated from 64-bit MapleStory with `_Canvas`
2. **Reconstruct** the `_Canvas` directory structure
3. **Resolve** `_outlink` references correctly
4. **Pack** canvas images into separate `Canvas_NNN.wz` files
5. **Maintain** file size limits per Canvas file

### F.2 _Canvas Directory Structure

```
Data/
├── Map.wz                          # Main Map WZ file (metadata, links)
├── Map/
│   ├── _Canvas/                    # Canvas images for Map root
│   │   ├── Canvas_000.wz
│   │   ├── Canvas_001.wz
│   │   └── Canvas_NNN.wz
│   ├── Back/
│   │   └── _Canvas/                # Canvas images for backgrounds
│   │       ├── Canvas_000.wz
│   │       └── ...
│   ├── Tile/
│   │   └── _Canvas/                # Canvas images for tiles
│   │       └── ...
│   └── Obj/
│       └── _Canvas/                # Canvas images for objects
│           └── ...
├── Mob.wz                          # Main Mob WZ file
├── Mob/
│   └── _Canvas/                    # Canvas images for mobs
│       ├── Canvas_000.wz
│       └── ...
└── [Category]/
    └── _Canvas/
        └── Canvas_NNN.wz
```

### F.3 Implementation Plan

#### F.3.1 CanvasPackingService

**New File**: `MapleLib/MapleLib/Img/CanvasPackingService.cs`

```csharp
public class CanvasPackingService
{
    // Configuration
    public long MaxCanvasFileSize { get; set; } = 500_000_000; // 500MB per Canvas file
    public int MaxImagesPerCanvas { get; set; } = 1000;

    /// <summary>
    /// Pack IMG filesystem to 64-bit WZ format with _Canvas structure
    /// </summary>
    public async Task PackTo64BitWzAsync(
        string imgVersionPath,
        string outputWzPath,
        WzMapleVersion encryption,
        IProgress<PackingProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pack a single category with its _Canvas subdirectory
    /// </summary>
    public async Task PackCategoryWithCanvasAsync(
        string categoryPath,
        string outputPath,
        WzMapleVersion encryption,
        CanvasPackingOptions options);

    /// <summary>
    /// Detect which images should be moved to _Canvas files
    /// </summary>
    public CanvasAnalysisResult AnalyzeForCanvasSeparation(
        WzDirectory directory,
        CanvasSeparationStrategy strategy);

    /// <summary>
    /// Create _outlink references in main WZ file pointing to _Canvas files
    /// </summary>
    public void CreateOutlinkReferences(
        WzDirectory mainDirectory,
        Dictionary<string, string> imageToCanvasMapping);
}

public class CanvasPackingOptions
{
    public long MaxCanvasFileSize { get; set; } = 500_000_000;
    public int MaxImagesPerCanvas { get; set; } = 1000;
    public CanvasSeparationStrategy Strategy { get; set; } = CanvasSeparationStrategy.SizeThreshold;
    public long ImageSizeThreshold { get; set; } = 100_000; // Images > 100KB go to _Canvas
    public bool PreserveOriginalStructure { get; set; } = true;
}

public enum CanvasSeparationStrategy
{
    /// <summary>Separate images larger than threshold to _Canvas</summary>
    SizeThreshold,

    /// <summary>Match original _Canvas structure from manifest</summary>
    PreserveOriginal,

    /// <summary>Put all canvas images in _Canvas</summary>
    AllCanvas,

    /// <summary>Keep all images in main WZ (no _Canvas separation)</summary>
    NoSeparation
}
```

#### F.3.2 Canvas Packing Workflow

```csharp
public async Task PackTo64BitWzAsync(
    string imgVersionPath,
    string outputWzPath,
    WzMapleVersion encryption,
    IProgress<PackingProgress> progress,
    CancellationToken ct = default)
{
    // 1. Load manifest to check if source had _Canvas format
    var manifest = LoadManifest(imgVersionPath);
    bool preserveCanvasStructure = manifest.HasCanvasFormat;

    // 2. Get list of categories to pack
    var categories = GetCategories(imgVersionPath);

    foreach (var category in categories)
    {
        ct.ThrowIfCancellationRequested();

        // 3. Create main WZ file for category
        using var mainWzFile = new WzFile(1, encryption);
        mainWzFile.Name = $"{category}.wz";

        // 4. Load all IMG files for this category
        var imgFiles = LoadImgFilesForCategory(imgVersionPath, category);

        // 5. Analyze which images should go to _Canvas
        var canvasAnalysis = AnalyzeForCanvasSeparation(
            imgFiles,
            preserveCanvasStructure
                ? CanvasSeparationStrategy.PreserveOriginal
                : CanvasSeparationStrategy.SizeThreshold);

        // 6. Build main WZ structure with _outlink references
        BuildMainWzStructure(mainWzFile, imgFiles, canvasAnalysis);

        // 7. Create _Canvas WZ files
        if (canvasAnalysis.HasCanvasImages)
        {
            await PackCanvasFilesAsync(
                category,
                outputWzPath,
                canvasAnalysis.CanvasImages,
                encryption,
                progress,
                ct);
        }

        // 8. Save main WZ file
        string mainWzPath = Path.Combine(outputWzPath, $"{category}.wz");
        mainWzFile.SaveToDisk(mainWzPath, saveAs64Bit: true, encryption);

        progress?.Report(new PackingProgress {
            Category = category,
            Status = "Complete"
        });
    }
}
```

#### F.3.3 Canvas Image Separation Logic

```csharp
public CanvasAnalysisResult AnalyzeForCanvasSeparation(
    IEnumerable<WzImage> images,
    CanvasSeparationStrategy strategy)
{
    var result = new CanvasAnalysisResult();
    var canvasImages = new List<CanvasImageInfo>();
    var mainImages = new List<WzImage>();

    foreach (var image in images)
    {
        var canvasProperties = FindAllCanvasProperties(image);

        foreach (var canvas in canvasProperties)
        {
            bool shouldSeparate = strategy switch
            {
                CanvasSeparationStrategy.AllCanvas => true,
                CanvasSeparationStrategy.NoSeparation => false,
                CanvasSeparationStrategy.SizeThreshold =>
                    EstimateCanvasSize(canvas) > ImageSizeThreshold,
                CanvasSeparationStrategy.PreserveOriginal =>
                    WasOriginallyInCanvas(canvas),
                _ => false
            };

            if (shouldSeparate)
            {
                canvasImages.Add(new CanvasImageInfo
                {
                    SourceImage = image,
                    CanvasProperty = canvas,
                    PropertyPath = GetFullPath(canvas),
                    EstimatedSize = EstimateCanvasSize(canvas)
                });
            }
        }
    }

    result.CanvasImages = canvasImages;
    result.HasCanvasImages = canvasImages.Count > 0;
    result.TotalCanvasSize = canvasImages.Sum(c => c.EstimatedSize);
    result.EstimatedCanvasFileCount = (int)Math.Ceiling(
        result.TotalCanvasSize / (double)MaxCanvasFileSize);

    return result;
}
```

#### F.3.4 Creating _outlink References

```csharp
public void CreateOutlinkReferences(
    WzDirectory mainDirectory,
    Dictionary<string, string> imageToCanvasMapping)
{
    // For each image that was moved to _Canvas, replace the canvas
    // property with an _outlink reference

    foreach (var (originalPath, canvasPath) in imageToCanvasMapping)
    {
        // Find the canvas property in main directory
        var canvasProperty = mainDirectory.GetFromPath(originalPath) as WzCanvasProperty;
        if (canvasProperty == null) continue;

        // Create placeholder canvas with _outlink
        var placeholder = new WzCanvasProperty(canvasProperty.Name);

        // Add _outlink string property pointing to _Canvas location
        // Format: "Category/_Canvas/imageName.img/path/to/canvas"
        placeholder.AddProperty(new WzStringProperty("_outlink", canvasPath));

        // Add minimal required properties (width=1, height=1 placeholder)
        placeholder.PngProperty = CreatePlaceholderPng();

        // Replace original canvas with placeholder
        var parent = canvasProperty.Parent;
        parent.RemoveProperty(canvasProperty);
        parent.AddProperty(placeholder);
    }
}
```

#### F.3.5 Packing Canvas Files

```csharp
private async Task PackCanvasFilesAsync(
    string category,
    string outputPath,
    List<CanvasImageInfo> canvasImages,
    WzMapleVersion encryption,
    IProgress<PackingProgress> progress,
    CancellationToken ct)
{
    // Create _Canvas directory
    string canvasDir = Path.Combine(outputPath, category, "_Canvas");
    Directory.CreateDirectory(canvasDir);

    // Group images into Canvas files based on size limits
    var canvasFiles = GroupIntoCanvasFiles(canvasImages);

    int canvasIndex = 0;
    foreach (var canvasGroup in canvasFiles)
    {
        ct.ThrowIfCancellationRequested();

        // Create Canvas_NNN.wz file
        using var canvasWzFile = new WzFile(1, encryption);
        canvasWzFile.Name = $"Canvas_{canvasIndex:D3}.wz";

        // Add each image to the canvas WZ file
        foreach (var imageInfo in canvasGroup)
        {
            // Create directory structure in canvas file
            var imgDirectory = CreateImgStructure(
                canvasWzFile.WzDirectory,
                imageInfo);

            // Add the actual canvas property with image data
            AddCanvasToDirectory(imgDirectory, imageInfo.CanvasProperty);
        }

        // Save canvas WZ file
        string canvasPath = Path.Combine(canvasDir, $"Canvas_{canvasIndex:D3}.wz");
        canvasWzFile.SaveToDisk(canvasPath, saveAs64Bit: true, encryption);

        // Create/update .ini index file
        UpdateCanvasIndexFile(canvasDir, canvasIndex);

        canvasIndex++;
        progress?.Report(new PackingProgress
        {
            Category = category,
            SubCategory = "_Canvas",
            Current = canvasIndex,
            Total = canvasFiles.Count,
            Status = $"Canvas_{canvasIndex:D3}.wz"
        });
    }
}

private List<List<CanvasImageInfo>> GroupIntoCanvasFiles(
    List<CanvasImageInfo> canvasImages)
{
    var result = new List<List<CanvasImageInfo>>();
    var currentGroup = new List<CanvasImageInfo>();
    long currentSize = 0;

    // Sort by size descending for better packing
    var sorted = canvasImages.OrderByDescending(c => c.EstimatedSize);

    foreach (var image in sorted)
    {
        // Check if adding this image exceeds limits
        if (currentSize + image.EstimatedSize > MaxCanvasFileSize ||
            currentGroup.Count >= MaxImagesPerCanvas)
        {
            if (currentGroup.Count > 0)
            {
                result.Add(currentGroup);
                currentGroup = new List<CanvasImageInfo>();
                currentSize = 0;
            }
        }

        currentGroup.Add(image);
        currentSize += image.EstimatedSize;
    }

    if (currentGroup.Count > 0)
        result.Add(currentGroup);

    return result;
}
```

### F.4 Manifest Integration

When packing to 64-bit format, update manifest.json:

```csharp
public void UpdateManifestForCanvasPacking(
    string versionPath,
    CanvasPackingResult result)
{
    var manifest = LoadManifest(versionPath);

    // Update 64-bit and canvas flags
    manifest.Is64Bit = true;
    manifest.HasCanvasFormat = result.HasCanvasFiles;

    // Record which categories have _Canvas directories
    manifest.CanvasDirectories = result.CanvasDirectories;

    // Update category info
    foreach (var category in result.PackedCategories)
    {
        if (manifest.Categories.TryGetValue(category.Name, out var info))
        {
            info.HasCanvas = category.HasCanvasFiles;
            info.CanvasFileCount = category.CanvasFileCount;
        }
    }

    SaveManifest(versionPath, manifest);
}
```

### F.5 Canvas Index File (.ini)

Each `_Canvas` directory contains an index file tracking Canvas files:

```ini
; Canvas_directory.ini
[Canvas]
Count=3
; Canvas_000.wz, Canvas_001.wz, Canvas_002.wz
```

```csharp
private void UpdateCanvasIndexFile(string canvasDir, int maxIndex)
{
    string iniPath = Path.Combine(canvasDir, "Canvas.ini");

    var content = new StringBuilder();
    content.AppendLine("[Canvas]");
    content.AppendLine($"Count={maxIndex + 1}");

    File.WriteAllText(iniPath, content.ToString());
}
```

### F.6 _outlink Path Format

_outlink paths follow this format:
```
[Category]/[Subcategory]/_Canvas/[ImageName].img/[PropertyPath]
```

**Examples:**
- `Map/Back/_Canvas/snowyDarkrock.img/back/0`
- `Map/Obj/_Canvas/acc1.img/normal/0`
- `Mob/_Canvas/0100100.img/stand/0`
- `Character/Weapon/_Canvas/01302000.img/default/0`

### F.7 Integration with WzPackingService

Update existing `WzPackingService.cs` to support 64-bit canvas packing:

```csharp
public class WzPackingService
{
    private readonly CanvasPackingService _canvasService;

    public async Task PackToWzAsync(
        string imgVersionPath,
        string outputPath,
        WzMapleVersion encryption,
        PackingOptions options,
        IProgress<PackingProgress> progress,
        CancellationToken ct = default)
    {
        if (options.SaveAs64Bit && options.UseCanvasFormat)
        {
            // Use canvas packing for 64-bit output
            await _canvasService.PackTo64BitWzAsync(
                imgVersionPath,
                outputPath,
                encryption,
                progress,
                ct);
        }
        else
        {
            // Use standard packing (32-bit or 64-bit without _Canvas)
            await PackToStandardWzAsync(
                imgVersionPath,
                outputPath,
                encryption,
                options.SaveAs64Bit,
                progress,
                ct);
        }
    }
}

public class PackingOptions
{
    public bool SaveAs64Bit { get; set; }
    public bool UseCanvasFormat { get; set; }
    public CanvasSeparationStrategy CanvasStrategy { get; set; }
    public long CanvasImageThreshold { get; set; } = 100_000;
    public long MaxCanvasFileSize { get; set; } = 500_000_000;
}
```

### F.8 UI Integration

Add options in packing UI for _Canvas format:

```csharp
// PackToWz form options
public partial class PackToWzForm : Form
{
    private CheckBox chkSave64Bit;
    private CheckBox chkUseCanvasFormat;
    private ComboBox cmbCanvasStrategy;
    private NumericUpDown numCanvasThreshold;

    private PackingOptions GetPackingOptions()
    {
        return new PackingOptions
        {
            SaveAs64Bit = chkSave64Bit.Checked,
            UseCanvasFormat = chkUseCanvasFormat.Checked && chkSave64Bit.Checked,
            CanvasStrategy = (CanvasSeparationStrategy)cmbCanvasStrategy.SelectedIndex,
            CanvasImageThreshold = (long)numCanvasThreshold.Value * 1024 // KB to bytes
        };
    }
}
```

### F.9 Tasks Checklist

- [ ] Create `CanvasPackingService.cs`
- [ ] Implement `AnalyzeForCanvasSeparation()` method
- [ ] Implement `PackCanvasFilesAsync()` method
- [ ] Implement `CreateOutlinkReferences()` method
- [ ] Add canvas index file generation (.ini)
- [ ] Update manifest.json schema with canvas fields
- [ ] Integrate with existing `WzPackingService`
- [ ] Add 64-bit canvas options to packing UI
- [ ] Update `VersionInfo.cs` model with canvas properties
- [ ] Write unit tests for canvas packing
- [ ] Test round-trip: Extract 64-bit → IMG → Pack to 64-bit
- [ ] Verify _outlink resolution works after re-packing

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-01-XX | Claude | Initial draft |
| 1.1 | 2025-12-30 | Claude | Phase 7 memory optimization - Added LRU cache integration, LazyWzImageDictionary for on-demand loading, reduced memory from 40GB to ~1-2GB |
| 1.2 | 2025-12-30 | Claude | Added Appendix E: Future Improvements backlog with 11 enhancement categories and priority matrix |
| 1.3 | 2025-12-30 | Claude | Fixed IMG filesystem memory issues: reverted to freeResources=true (lazy loading doesn't work for IMG), disabled LRU disposal (other caches hold property references), added on-demand MapInfo creation, skipped Mob/Item/Skill icon loading during ExtractAll |
| 1.4 | 2025-12-30 | Claude | Added Appendix F: _Canvas Format Packing Plan - comprehensive guide for packing IMG files back to 64-bit WZ format with `_Canvas` structure, including manifest.json updates for `is64Bit`, `hasCanvasFormat`, and `canvasDirectories` fields |

