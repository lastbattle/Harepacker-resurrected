# WzFileManager Reference

## Overview

`WzFileManager` is the central class in MapleLib for managing WZ file loading, caching, and lookup. It handles multiple MapleStory formats including:

- **32-bit WZ** - Traditional single-file WZ archives (Map.wz, Mob.wz, etc.)
- **64-bit WZ** - Numbered split files in Data/ directory (Map_000.wz, Map_001.wz, etc.)
- **Pre-Big Bang** - Legacy Data.wz format containing all categories
- **MS/NM Files** - Newer pack formats found in Packs/ directory

**Location**: `MapleLib/MapleLib/WzFileManager.cs`

---

## Static Instance

```csharp
public static WzFileManager fileManager;
```

A static instance is set during construction for global access. This allows any part of the codebase to access loaded WZ files.

---

## Supported Formats

WzFileManager supports all major MapleStory file format eras:

| Era | Detection Method | Key Identifier |
|-----|------------------|----------------|
| Beta (v0.01-v0.30) | Single Data.wz, no category files | Only Data.wz exists |
| Pre-Big Bang (v0.31-v0.92) | List.wz exists | `WzTool.IsListFile()` returns true |
| Post-Big Bang (v0.93-v149) | BigBang marker in UI.wz | `UIWindow2.img["BigBang!!!!..."]` exists |
| 64-bit Modern (v180+) | Data/ directory with 40+ WZ files | `Detect64BitDirectoryWzFileFormat()` |
| MS/NM Packs | Packs/ folder with .ms/.nm files | `LoadPacksFiles()` handles these |

For detailed file structures of each era, see [WZ Format History](./wz-format-history.md#directory-structures-by-era).

---

## Format Detection

### 64-bit Detection

The `Detect64BitDirectoryWzFileFormat()` method identifies 64-bit MapleStory installations:

```csharp
public static bool Detect64BitDirectoryWzFileFormat(string baseDirectoryPath)
```

**Detection Logic:**
1. Check for `Data/` subdirectory
2. Count `.wz` files in Data/ directory
3. If > 40 WZ files found, it's 64-bit format

**Example 64-bit structure:**
```
MapleStory/
├── Data/
│   ├── Base/
│   │   ├── Base_000.wz
│   │   └── Base.ini
│   ├── Map/
│   │   ├── Map_000.wz
│   │   ├── Map_001.wz
│   │   └── _Canvas/
│   │       └── _Canvas_000.wz
```

### Pre-Big Bang Detection

The `DetectIsPreBBDataWZFileFormat()` method identifies pre-Big Bang format:

```csharp
public static bool DetectIsPreBBDataWZFileFormat(string baseDirectoryPath, WzMapleVersion? encryption)
```

**Detection Methods (in order of priority):**

1. **List.wz exists** - Only in pre-BB versions. Uses `WzTool.IsListFile()` to verify.

2. **Data.wz without separate category files** - Very old beta format where all data is in a single Data.wz file. Checks if Skill.wz, String.wz, Character.wz are missing.

3. **UI.wz BigBang marker** - If encryption is provided, parses UI.wz and checks for the `BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!` property in UIWindow2.img.

**Detection Flowchart:**
```
List.wz exists? ──Yes──► Pre-Big Bang
       │
      No
       ▼
Data.wz exists without Skill/String/Character.wz? ──Yes──► Beta Format (Pre-BB)
       │
      No
       ▼
UIWindow2.img has BigBang marker? ──No──► Pre-Big Bang
       │
      Yes
       ▼
Post-Big Bang
```

### Big Bang Version Detection

Constants and methods for detecting Big Bang update status:

```csharp
public const string BIG_BANG_MARKER = "BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";
public const string BIG_BANG_2_MARKER = "BigBang2!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!";

public static bool IsBigBangUpdate(WzImage uiWindow2Image)
public static bool IsBigBang2Update(WzImage uiWindow2Image)
```

Use these with an already-loaded UIWindow2.img to determine if the client is:
- **Pre-Big Bang**: No BigBang marker
- **Post-Big Bang**: Has `BIG_BANG_MARKER` (December 2010)
- **Post-Chaos/Big Bang 2**: Has `BIG_BANG_2_MARKER` (2011)

---

## Constructors

### HaRepacker Constructor

```csharp
public WzFileManager()
```

Simple constructor for HaRepacker. Sets empty base directory and no special format flags.

### HaCreator Constructor

```csharp
public WzFileManager(string directory, bool bIsStandAloneWzFile)
```

| Parameter | Description |
|-----------|-------------|
| `directory` | MapleStory installation directory |
| `bIsStandAloneWzFile` | True if loading a single WZ file (not full installation) |

Automatically detects 64-bit and pre-BB format during construction.

---

## Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `WzBaseDirectory` | string | Returns `baseDir/Data/` for 64-bit, otherwise `baseDir` |
| `Is64Bit` | bool | True if 64-bit format detected |
| `IsPreBBDataWzFormat` | bool | True if pre-Big Bang Data.wz format |
| `WzFileList` | ReadOnlyCollection | All loaded WZ files |
| `WzImagesList` | ReadOnlyCollection | All loaded raw WZ images |
| `MsFiles` | IReadOnlyDictionary | Loaded MS pack files |

---

## Building the WZ File List

### BuildWzFileList()

Scans the MapleStory directory and builds internal lookup dictionaries:

```csharp
public void BuildWzFileList()
```

**For 64-bit clients:**
1. Iterates through all directories in `Data/`
2. Reads `.ini` files to determine WZ file count (e.g., `LastWzIndex|5` means 6 files: 000-005)
3. Builds `_wzFilesList` mapping base name to file list
4. Builds `_wzFilesDirectoryList` mapping file name to directory path
5. Handles `_Canvas` directories specially

**For 32-bit clients:**
1. Scans for all `.wz` files
2. Strips numbers from filenames to get base name (Mob001.wz → "mob")
3. Builds same lookup dictionaries

**INI File Format:**
```
LastWzIndex|5
```

This indicates WZ files from `_000.wz` through `_005.wz` exist.

### Excluded Directories

These directories are excluded from WZ file scanning:

```csharp
private static readonly string[] EXCLUDED_DIRECTORY_FROM_WZ_LIST = {
    "bak", "backup", "original", "xml", "hshield", "blackcipher", "harepacker", "hacreator"
};
```

---

## Loading MS/NM Pack Files

### LoadPacksFiles()

Loads newer pack formats from the Packs/ directory:

```csharp
public void LoadPacksFiles()
```

**MS Files (64-bit MapleStory):**
- Found in `Data/Packs/*.ms`
- Loaded as `WzMsFile` instances
- Converted to `WzFile` via `msFile.LoadAsWzFile()`
- Added to standard WZ lookup dictionaries

**NM Files (MapleStoryN):**
- Found in `Data/Packs/*.nm`
- Currently marked as TODO

---

## Loading WZ Files

### LoadWzFile(baseName, encVersion)

Loads a WZ file by name and encryption version:

```csharp
public WzFile LoadWzFile(string baseName, WzMapleVersion encVersion)
```

1. Calls `GetWzFilePath()` to resolve the file path
2. Creates and parses the WzFile
3. Registers in internal dictionaries
4. Returns the loaded WzFile

### LoadWzFile(baseName, wzFile)

Registers an already-parsed WzFile:

```csharp
public WzFile LoadWzFile(string baseName, WzFile wzf)
```

### LoadCanvasSection(canvasFolder, encVersion)

Loads 64-bit _Canvas WZ files for a category:

```csharp
public void LoadCanvasSection(string canvasFolder, WzMapleVersion encVersion)
```

**Example:**
```csharp
// Loads: Data/Map/Back/_Canvas/_Canvas_000.wz, _Canvas_001.wz, etc.
LoadCanvasSection("map/back", WzMapleVersion.GMS);
```

Uses the .ini file to determine how many canvas files exist.

### LoadLegacyDataWzFile(baseName, encVersion)

Loads pre-BB Data.wz that contains all categories:

```csharp
public bool LoadLegacyDataWzFile(string baseName, WzMapleVersion encVersion)
```

After loading, iterates through subdirectories and registers each as a separate category.

### LoadDataWzHotfixFile(basePath, encVersion)

Loads a hotfix Data.wz file (raw .img file):

```csharp
public WzImage LoadDataWzHotfixFile(string basePath, WzMapleVersion encVersion)
```

---

## Loading List.wz

### LoadListWzFile(fileVersion)

Loads the pre-BB List.wz file:

```csharp
public bool LoadListWzFile(WzMapleVersion fileVersion)
```

List.wz contains an index of all images in the game client. Only exists in pre-Big Bang versions.

---

## Finding WZ Resources

### Directory Lookup

```csharp
public WzDirectory this[string name] { get; }
```

Returns the main directory for a WZ category name.

### GetWzFileNameListFromBase(baseName)

Gets all WZ file names for a base category:

```csharp
public List<string> GetWzFileNameListFromBase(string baseName)
```

**Example:**
```csharp
// For 64-bit: returns ["Mob_000", "Mob_001", "Mob_002"]
// For 32-bit: returns ["Mob", "Mob2", "Mob001"]
var mobFiles = GetWzFileNameListFromBase("mob");
```

### GetWzDirectoriesFromBase(baseName, isCanvas)

Gets all WzDirectory objects for a category:

```csharp
public List<WzDirectory> GetWzDirectoriesFromBase(string baseName, bool isCanvas = false)
```

### FindWzImageByName(baseWzName, imageName)

Finds a specific WzImage across all WZ files in a category:

```csharp
public WzObject FindWzImageByName(string baseWzName, string imageName)
```

**Example:**
```csharp
// Finds 0100100.img in any of the Mob WZ files
var snailImg = FindWzImageByName("mob", "0100100.img");
```

### FindWzImagesByName(baseWzName, imageName)

Returns all matching images (for cases where same image exists in multiple WZ files):

```csharp
public List<WzObject> FindWzImagesByName(string baseWzName, string imageName)
```

---

## Canvas Directory Handling

### CANVAS_DIRECTORY_NAME

```csharp
public static string CANVAS_DIRECTORY_NAME = "_Canvas";
```

### ContainsCanvasDirectory(path)

Checks if a path contains a _Canvas directory:

```csharp
public static bool ContainsCanvasDirectory(string path)
```

### NormaliseWzCanvasDirectory(filePathOrBaseFileName)

Extracts the category path before _Canvas:

```csharp
public static string NormaliseWzCanvasDirectory(string filePathOrBaseFileName)
```

**Example:**
```
"Map/Back/_Canvas/_Canvas_000.wz" → "map/back"
```

---

## Update Tracking

### SetWzFileUpdated(name, img) / SetWzFileUpdated(wzFile)

Marks a WZ file as modified (for save operations):

```csharp
public void SetWzFileUpdated(string name, WzImage img)
public void SetWzFileUpdated(WzFile wzFile)
```

### GetUpdatedWzFiles()

Returns all WZ files that have been modified:

```csharp
public List<WzFile> GetUpdatedWzFiles()
```

---

## Unloading

### UnloadWzFile(wzFile, wzFilePath)

Removes a WZ file from memory:

```csharp
public void UnloadWzFile(WzFile wzFile, string wzFilePath)
```

### UnloadWzImgFile(wzImage)

Removes a raw WZ image from memory:

```csharp
public void UnloadWzImgFile(WzImage wzImage)
```

---

## Thread Safety

WzFileManager uses `ReaderWriterLockSlim` for thread-safe access to internal dictionaries:

```csharp
private readonly ReaderWriterLockSlim _readWriteLock = new();
```

All dictionary modifications use write locks, while reads use read locks.

---

## Common MapleStory Directories

Default paths checked for MapleStory installations:

```csharp
public static readonly string[] COMMON_MAPLESTORY_DIRECTORY = new string[] {
    @"C:\Nexon\MapleStory",
    @"D:\Nexon\Maple",
    @"C:\Program Files\WIZET\MapleStory",
    @"C:\MapleStory",
    @"C:\NEXPACE\MapleStoryN",
    @"C:\Program Files (x86)\Wizet\MapleStorySEA"
};
```

---

## Related Files

| File | Purpose |
|------|---------|
| `MapleLib/WzLib/WzFile.cs` | WZ file parsing |
| `MapleLib/WzLib/WzDirectory.cs` | WZ directory structure |
| `MapleLib/WzLib/WzImage.cs` | WZ image parsing |
| `MapleLib/WzLib/MSFile/WzMsFile.cs` | MS pack file support |
| `MapleLib/WzLib/Util/WzTool.cs` | WZ utilities including List.wz detection |
| `MapleLib/WzLib/ListFileParser.cs` | List.wz parsing |

---

## See Also

- [64-bit Canvas System](./canvas-outlink-system.md) - How _Canvas directories work
- [IMG Filesystem Migration Plan](../hacreator-harepacker-architecture/IMG_FILESYSTEM_MIGRATION_PLAN.md) - Migrating to extracted IMG format
- [Hot-Swap System](../hacreator-harepacker-architecture/img-hot-swap.md) - Live reloading of IMG files
