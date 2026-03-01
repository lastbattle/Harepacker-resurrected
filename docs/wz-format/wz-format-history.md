# WZ Format History

This document describes how MapleStory's WZ file format has evolved over its 20+ year history.

---

## Format Timeline

| Era | Versions | Format | Key Characteristics |
|-----|----------|--------|---------------------|
| Beta/Early | v0.01-v0.30 | Single Data.wz | All data in one file |
| Pre-Big Bang | v0.31-v0.94 | Split WZ + List.wz | Separate WZ files, List.wz index |
| Post-Big Bang | v0.95-v179 | Split WZ | No List.wz, BigBang marker in UI.wz |
| Modern 64-bit | v180-v219 | 64-bit .exe Split | Data/ directory, numbered files, _Canvas |
| Modern + MS Packs | v220+ | 64-bit .exe + .ms files | Snowcrypt encryption layer over WZ structure |
| MapleStoryN | N/A | .nm files | MapleStoryN-specific encryption format |

---

## Directory Structures by Era

### Beta / Very Early MapleStory (v0.01 - v0.30)

The earliest versions used a single monolithic Data.wz file containing all game data.

```
MapleStory/
├── MapleStory.exe
├── Data.wz              # Contains ALL categories (Character, Map, Mob, etc.)
└── [no other WZ files]
```

**Characteristics:**
- Single `Data.wz` contains subdirectories for each category
- No separate Skill.wz, String.wz, Character.wz files
- Very small file sizes compared to modern versions
- Simple encryption (or none)

**Detection in code:**
```csharp
// Data.wz exists but no Skill.wz, String.wz, Character.wz
bool isVeryOldPreBB = File.Exists("Data.wz")
    && !File.Exists("Skill.wz")
    && !File.Exists("String.wz");
```

---

### Pre-Big Bang (v0.31 - v0.94)

After the beta period, MapleStory split data into separate WZ files and introduced List.wz.

```
MapleStory/
├── MapleStory.exe
├── List.wz              # Index of all images (unique to pre-BB)
├── Base.wz              # Base game data
├── Character.wz         # Equipment, hair, face, etc.
├── Effect.wz            # Visual effects
├── Etc.wz               # Miscellaneous data
├── Item.wz              # Consumables, equipment stats
├── Map.wz               # Map data, tiles, objects, backgrounds
├── Mob.wz               # Monster data and sprites
├── Morph.wz             # Transformation data
├── Npc.wz               # NPC data and sprites
├── Quest.wz             # Quest information
├── Reactor.wz           # Reactor objects
├── Skill.wz             # Skill data and effects
├── Sound.wz             # Audio files
├── String.wz            # Text strings (names, descriptions)
├── TamingMob.wz         # Mount data
└── UI.wz                # User interface elements
```

**Characteristics:**
- **List.wz** is the key identifier - only exists in pre-BB versions
- List.wz contains an encrypted index of all .img files in the client
- UIWindow2.img does NOT have the `BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!` marker
- Separate WZ files per category
- Single file per category (no Mob2.wz, Map001.wz splits)
- GMS encryption varies by region/version

**Detection in code:**
```csharp
// List.wz only exists in pre-BB
if (File.Exists("List.wz") && WzTool.IsListFile("List.wz"))
    return true; // Pre-BB confirmed
```

---

### Post-Big Bang (v0.95 - v179)

The Big Bang update (December 2010) restructured the game significantly. The WZ format also changed.

```
MapleStory/
├── MapleStory.exe
├── Base.wz
├── Character.wz
├── Effect.wz
├── Etc.wz
├── Item.wz
├── Map.wz               # May have Map2.wz, Map001.wz for large versions
├── Map2.wz              # Additional map data (optional)
├── Mob.wz
├── Mob2.wz              # Additional mob data (optional)
├── Mob001.wz            # Numbered splits for very large categories
├── Morph.wz
├── Npc.wz
├── Quest.wz
├── Reactor.wz
├── Skill.wz
├── Skill001.wz          # Skill splits (5th job era)
├── Sound.wz
├── String.wz
├── TamingMob.wz
└── UI.wz                # Contains UIWindow2.img with BigBang marker
```

**Characteristics:**
- **No List.wz** - removed after Big Bang
- **BigBang marker** in UI.wz/UIWindow2.img: `BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!`
- Later versions add **BigBang2 marker**: `BigBang2!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!`
- Categories may split into numbered files (Mob.wz, Mob2.wz, Mob001.wz)

**Detection in code:**
```csharp
// Check for BigBang marker in UIWindow2.img
var uiWindow2 = uiWzFile.WzDirectory?.GetImageByName("UIWindow2.img");
uiWindow2.ParseImage();
bool isBigBang = uiWindow2["BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"] != null;
```

**BigBang vs BigBang2:**
| Marker | Update | Approximate Date |
|--------|--------|------------------|
| `BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!` | Big Bang | December 2010 |
| `BigBang2!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!` | Chaos/Big Bang 2 | 2011 |

---

### 64-bit .exe with Modern Format (v180+)

Starting around v180+, MapleStory moved to a 64-bit .exe client with a completely restructured file system.

```
MapleStory/
├── MapleStory.exe
├── Data/                          # All WZ files moved here
│   ├── Base/
│   │   ├── Base_000.wz
│   │   ├── Base_001.wz
│   │   └── Base.ini               # Contains "LastWzIndex|1"
│   ├── Character/
│   │   ├── Character_000.wz
│   │   ├── Character_001.wz
│   │   ├── Character_002.wz
│   │   ├── Character.ini
│   │   └── _Canvas/               # Separated image data
│   │       ├── _Canvas_000.wz
│   │       ├── _Canvas_001.wz
│   │       └── _Canvas.ini
│   ├── Map/
│   │   ├── Map_000.wz
│   │   ├── Map_001.wz
│   │   ├── Map.ini
│   │   ├── Back/
│   │   │   └── _Canvas/
│   │   ├── Obj/
│   │   │   └── _Canvas/
│   │   └── Tile/
│   │       └── _Canvas/
│   ├── Mob/
│   │   ├── Mob_000.wz
│   │   ├── Mob.ini
│   │   └── _Canvas/
│   ├── Skill/
│   │   ├── Skill_000.wz
│   │   └── _Canvas/
│   ├── String/
│   │   └── String_000.wz
│   ├── UI/
│   │   ├── UI_000.wz
│   │   └── _Canvas/
│   └── Packs/                     # MS pack files (newer)
│       ├── Mob_00000.ms
│       ├── Character_00000.ms
│       └── Effect_00000.ms
```

**Characteristics:**
- All WZ files in `Data/` subdirectory
- Each category split into numbered files (`_000.wz`, `_001.wz`, etc.)
- **INI files** specify the number of WZ files: `LastWzIndex|5` means 000-005
- **_Canvas directories** contain separated bitmap/image data
- Main WZ files contain `_outlink` properties pointing to _Canvas data
- 40+ WZ files in Data/ directory (used for detection)
- MS pack files (.ms) in Packs/ folder for newer content

**INI File Format:**
```ini
LastWzIndex|3
```
This means WZ files exist from `_000.wz` through `_003.wz` (4 files total).

**Detection in code:**
```csharp
// Count WZ files in Data/ directory
string dataDir = Path.Combine(baseDir, "Data");
int wzCount = Directory.EnumerateFiles(dataDir, "*.wz", SearchOption.AllDirectories).Count();
bool is64Bit = wzCount > 40;
```

---

### MS Pack Files (v220+)

Starting with v220+, MapleStory added `.ms` pack files which provide an additional **Snowcrypt** encryption layer on top of existing WZ/IMG structures.

```
MapleStory/
├── MapleStory.exe
├── Data/
│   ├── [Standard 64-bit .exe structure - .wz files still present]
│   └── Packs/
│       ├── Base_00000.ms
│       ├── Character_00000.ms
│       ├── Character_00001.ms
│       ├── Effect_00000.ms
│       ├── Mob_00000.ms
│       └── Npc_00000.ms
```

**Encryption Layer:**
```
┌──────────────────────────────────────────────────────┐
│                   .ms File                           │
│  ┌────────────────────────────────────────────────┐  │
│  │         Snowcrypt Encryption Layer             │  │
│  │  ┌─────────────────────────────────────-────┐  │  │
│  │  │     Standard WZ/IMG Structure            │  │  │
│  │  │  (Same as 64-bit .exe WZ internally)     │  │  │
│  │  └──────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

**Characteristics:**
- `.ms` files are used by all MapleStory versions v220+
- **Snowcrypt**: Additional encryption algorithm wrapping standard WZ data
- Once decrypted, internal structure is identical to standard 64-bit .exe WZ format
- Contains standard WZ directories, images, and properties inside
- Loaded via `WzMsFile` class which handles Snowcrypt decryption
- Converted to `WzFile` for compatibility with existing codebase

**Decryption Flow:**
```csharp
// 1. Open .ms file and decrypt Snowcrypt layer
var msFile = new WzMsFile(stream, fileName, filePath, leaveOpen: true);
msFile.ReadEntries();  // Decrypts and reads internal structure

// 2. Convert to standard WzFile for compatibility
var wzFile = msFile.LoadAsWzFile();

// 3. Use like any other WzFile
var mobImage = wzFile.WzDirectory["0100100.img"];
```

**Why Additional Encryption?**
- Adds anti-tampering protection for newer clients
- Makes data extraction more difficult

---

### MapleStoryN (.nm Files)

MapleStoryN uses a different encryption format with `.nm` files.

```
MapleStoryN/
├── MapleStoryN.exe
├── Data/
│   └── Packs/
│       ├── Base_00000.nm
│       ├── Character_00000.nm
│       ├── Mob_00000.nm
│       └── ...
```

**Characteristics:**
- `.nm` files are **MapleStoryN-specific** encryption format
- Different encryption implementation than `.ms` files
- Internal structure similar to WZ once decrypted
- Currently marked as TODO in WzFileManager for full support

---

## Key Detection Methods

```csharp
// Detect 64-bit .exe format (Data/ directory with 40+ WZ files)
bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(path);

// Detect pre-Big Bang format (List.wz exists or single Data.wz)
bool isPreBB = WzFileManager.DetectIsPreBBDataWZFileFormat(path);

// Check for Big Bang update via UIWindow2.img marker
bool isBigBang = WzFileManager.IsBigBangUpdate(uiWindow2Image);

// Check for Big Bang 2 / Chaos update
bool isBigBang2 = WzFileManager.IsBigBang2Update(uiWindow2Image);
```

---

## Version Detection from MapleStory.exe

The `WzFile.GetMapleStoryVerFromExe()` method extracts version information directly from the MapleStory executable using Windows `FileVersionInfo`.

### Supported Executables

| Executable | Description | Priority |
|------------|-------------|----------|
| `MapleStoryT.exe` | Test server client | 1 (highest) |
| `MapleStoryA.exe` | Admin client | 2 |
| `MapleStory.exe` | Standard client | 3 |

### Version Info Structure

The executable's file version follows the format: `Major.Minor.Build.Revision`

| Component | Meaning | Example |
|-----------|---------|---------|
| `FileMajorPart` | Region/Locale code | 8 (GMS) |
| `FileMinorPart` | MapleStory patch version | 83 |
| `FileBuildPart` | Minor patch version | 1 |

### Region/Locale Codes

| Code | Region | Enum Value |
|------|--------|------------|
| 1 | Korea (KMS) | `MapleStoryKorea` |
| 2 | Korea Tespia | `MapleStoryKoreaTespia` |
| 5 | Tespia | `MapleStoryTespia` |
| 7 | South East Asia (MSEA) | `MapleStorySEA` |
| 8 | Global (GMS) | `MapleStoryGlobal` |
| 9 | Europe (EMS) | `MapleStoryEurope` |

### Usage Example

```csharp
// In WzFile.cs - automatically called during WZ parsing
short version = GetMapleStoryVerFromExe(wzFilePath, out MapleStoryLocalisation locale);

// Example: GMS v83
// FileVersion: 8.83.1.0
// locale = MapleStoryGlobal (8)
// version = 83
// minorPatch = 1
```

### Detection Flow

```
1. Start from WZ file directory
2. Search up to 4 parent directories for:
   - MapleStoryT.exe (prioritized)
   - MapleStoryA.exe
   - MapleStory.exe
3. Read FileVersionInfo from found executable
4. Skip if version is 1.0.0.x or 0.0.0.x (invalid/old format)
5. Extract locale from Major, version from Minor, patch from Build
```

**Note:** Older clients (pre-v30) may use version 1.0.0.1 which cannot be parsed this way.

**Location:** `MapleLib/WzLib/WzFile.cs` - `GetMapleStoryVerFromExe()` method

---

## Format Detection Flowchart

```
Start
  │
  ▼
Does Data/ directory exist with 40+ WZ files?
  │
  ├─Yes─► 64-bit .exe Format
  │
  ▼ No
Does List.wz exist and is valid?
  │
  ├─Yes─► Pre-Big Bang Format
  │
  ▼ No
Does Data.wz exist WITHOUT Skill.wz/String.wz/Character.wz?
  │
  ├─Yes─► Very Early Beta Format
  │
  ▼ No
Check UI.wz/UIWindow2.img for BigBang marker
  │
  ├─Has marker─► Post-Big Bang Format
  │
  ▼ No marker
  Pre-Big Bang Format (fallback)
```

---

## Related Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `WzFileManager` | MapleLib/MapleLib/WzFileManager.cs | Central WZ management |
| `WzFile` | MapleLib/WzLib/WzFile.cs | WZ file parsing |
| `WzImage` | MapleLib/WzLib/WzImage.cs | IMG structure parsing |
| `WzDirectory` | MapleLib/WzLib/WzDirectory.cs | Directory tree structure |
| `WzMsFile` | MapleLib/WzLib/MSFile/WzMsFile.cs | MS pack file support |
| `ImgFileSystemManager` | MapleLib/Img/ImgFileSystemManager.cs | Extracted IMG management |
| `VirtualWzDirectory` | MapleLib/Img/VirtualWzDirectory.cs | Filesystem-backed WzDirectory |

---

## See Also

- [WZ File Overview](./wz-file-overview.md) - What WZ/IMG files are and how encryption works
- [WzFileManager Reference](./WzFileManager.md) - Central WZ file loading and management class
- [Canvas & Outlink System](./canvas-outlink-system.md) - _Canvas directories and _outlink/_inlink resolution
