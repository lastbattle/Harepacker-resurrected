# WZ Format Documentation

This directory contains documentation about MapleStory WZ file format handling in HaCreator/HaRepacker.

---

## What is a .wz File?

A **WZ file** (`.wz`) is MapleStory's proprietary archive format - similar to a ZIP file but with custom encryption and structure. WZ files contain all game assets: sprites, maps, sounds, strings, skills, items, and more.

### WZ File Structure

```
┌─────────────────────────────────────────────────────────────┐
│                      WZ File Header                         │
├─────────────────────────────────────────────────────────────┤
│  Magic: "PKG1" (4 bytes)                                    │
│  File Size (8 bytes)                                        │
│  Header Size (4 bytes)                                      │
│  Copyright String (varies)                                  │
│  Version Hash (2 bytes) - encrypted version identifier      │
├─────────────────────────────────────────────────────────────┤
│                     Root Directory                          │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Directory Entry Count                              │    │
│  │  ┌───────────────────────────────────────────────┐  │    │
│  │  │ Entry 1: Name, Type, Size, Checksum, Offset   │  │    │
│  │  │ Entry 2: ...                                   │  │    │
│  │  │ Entry N: ...                                   │  │    │
│  │  └───────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                    .img Files (embedded)                    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Mob.img, Map.img, Character.img, etc.              │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

A WZ file is essentially a **container** that holds:
- **Directories** - organizational folders (WzDirectory)
- **Images** - the actual data files with `.img` extension (WzImage)

---

## What is a .img File?

An **IMG file** (`.img`) is the actual data unit inside a WZ archive. Each IMG file is a hierarchical property tree containing game data. Think of it as a structured document (like JSON/XML) but in binary format.

### IMG File Structure

```
┌─────────────────────────────────────────────────────────────┐
│                      IMG Header                             │
├─────────────────────────────────────────────────────────────┤
│  Property Type Marker (1 byte)                              │
│  Property Count (compressed int)                            │
├─────────────────────────────────────────────────────────────┤
│                   Property Tree                             │
│                                                             │
│  Root (SubProperty)                                         │
│  ├── info (SubProperty)                                     │
│  │   ├── origin (Vector) = {X: 45, Y: 72}                   │
│  │   ├── delay (Int) = 120                                  │
│  │   └── z (Int) = 0                                        │
│  ├── 0 (Canvas) = [bitmap data]                             │
│  │   ├── origin (Vector)                                    │
│  │   └── delay (Int)                                        │
│  ├── 1 (Canvas) = [bitmap data]                             │
│  ├── hit (Sound) = [audio data]                             │
│  └── link (UOL) = "../other/path"                           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### IMG Property Types

| Type | Description | Example |
|------|-------------|---------|
| `Null` | Empty placeholder | - |
| `Short` | 16-bit integer | `32767` |
| `Int` | 32-bit integer | `100000` |
| `Long` | 64-bit integer | `9999999999` |
| `Float` | 32-bit floating point | `3.14159` |
| `Double` | 64-bit floating point | `3.141592653589` |
| `String` | Text string | `"Stand1"` |
| `Vector` | X,Y coordinate pair | `{X: 100, Y: -50}` |
| `SubProperty` | Container for child properties | Directory-like node |
| `Canvas` | Bitmap image (PNG internally) | Sprite frames |
| `Sound` | Audio data (MP3/OGG) | Sound effects, BGM |
| `UOL` | Link/reference to another property | `"../../other/0"` |
| `Convex` | Collection of vectors | Hit boxes, collision |

---

## WZ/IMG Encryption

MapleStory uses a multi-layer encryption system combining **AES-256** key generation and **XOR-based** string/data encryption.

### Encryption Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                WZ Encryption Architecture                   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. AES Key Generation (WzMutableKey)                       │
│     ├── 4-byte IV (region-specific)                         │
│     ├── 128-byte UserKey → trimmed to 32-byte AES key       │
│     └── AES-256 ECB mode generates XOR key stream           │
│                                                             │
│  2. Version Hash (Offset Encryption)                        │
│     ├── Computed from MapleStory patch version              │
│     ├── Used with constant 0x581C3F6D for offset XOR        │
│     └── Bit rotation for additional obfuscation             │
│                                                             │
│  3. String Encryption                                       │
│     ├── XOR with key stream + rotating mask                 │
│     ├── ASCII: mask starts at 0xAA, increments              │
│     └── Unicode: mask starts at 0xAAAA, increments          │
│                                                             │
│  4. Canvas Encryption (listWz format)                       │
│     ├── Block-based XOR with key stream                     │
│     └── Data is zlib-compressed after XOR decryption        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Region-Specific Initialization Vectors (IV)

Different MapleStory regions use different 4-byte IVs for key generation:

| Region | Enum | IV (Hex) | Description |
|--------|------|----------|-------------|
| GMS | `WzMapleVersion.GMS` | `0x4D, 0x23, 0xC7, 0x2B` | Old Global MapleStory |
| EMS/MSEA/KMS | `WzMapleVersion.EMS` | `0xB9, 0x7D, 0x63, 0xE9` | Old Europe, MapleSEA, Korean |
| BMS/CLASSIC | `WzMapleVersion.BMS` | `0x00, 0x00, 0x00, 0x00` | Early beta + all modern versions |
| CUSTOM | `WzMapleVersion.CUSTOM` | User-defined | Private servers |

**Source:** `MapleLib/WzLib/WzAESConstant.cs`

### AES Key Generation

The WZ key stream is generated using AES-256 in ECB mode:

```csharp
// From WzMutableKey.cs - simplified
public void EnsureKeySize(int size)
{
    // If IV is all zeros, no encryption (CLASSIC/BMS mode)
    if (BitConverter.ToInt32(this._iv, 0) == 0)
    {
        this._keys = new byte[size]; // All zeros = no encryption
        return;
    }

    using var aes = Aes.Create();
    aes.KeySize = 256;           // AES-256
    aes.BlockSize = 128;         // 16-byte blocks
    aes.Key = _aesUserKey;       // 32-byte key (trimmed from 128-byte UserKey)
    aes.Mode = CipherMode.ECB;   // Electronic Codebook mode
    aes.Padding = PaddingMode.None;

    // Generate key stream by encrypting IV repeatedly
    // First block: encrypt IV (repeated to 16 bytes)
    // Subsequent blocks: encrypt previous ciphertext
    for (int i = 0; i < size; i += 16)
    {
        if (i == 0)
        {
            // First block: IV repeated 4 times (4 bytes → 16 bytes)
            for (int j = 0; j < 16; j++)
                block[j] = _iv[j % 4];
            cs.Write(block);
        }
        else
        {
            // Chain: encrypt previous output
            cs.Write(newKeys.AsSpan(i - 16, 16));
        }
    }
}
```

**Source:** `MapleLib/WzLib/Util/WzMutableKey.cs`

### UserKey (AES Master Key)

The 128-byte UserKey is trimmed to 32 bytes for AES-256:

```csharp
// From MapleCryptoConstants.cs
public static byte[] MAPLESTORY_USERKEY_DEFAULT = new byte[128] {
    0x13, 0x00, 0x00, 0x00, 0x52, 0x00, 0x00, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x5B, 0x00, 0x00, 0x00,
    0x08, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x60, 0x00, 0x00, 0x00,
    // ... 96 more bytes
};

// Trimming: take every 16th byte to create 32-byte AES key
public static byte[] GetTrimmedUserKey(ref byte[] UserKey)
{
    byte[] key = new byte[32];
    for (int i = 0; i < 128; i += 16)
    {
        key[i / 4] = UserKey[i];
    }
    return key;
}
```

**Source:** `MapleLib/MapleCryptoLib/MapleCryptoConstants.cs`

### String Decryption Algorithm

Strings are decrypted using XOR with the key stream and a rotating mask:

```csharp
// From WzBinaryReader.cs - ASCII string decryption
private string DecodeAscii(int length)
{
    byte mask = 0xAA;  // Starting mask
    for (int i = 0; i < length; i++)
    {
        byte encryptedChar = ReadByte();
        encryptedChar ^= mask;           // XOR with rotating mask
        encryptedChar ^= (byte)WzKey[i]; // XOR with key stream
        bytes[i] = encryptedChar;
        mask++;  // Increment mask for next character
    }
    return Encoding.ASCII.GetString(bytes);
}

// Unicode string decryption (2 bytes per character)
private string DecodeUnicode(int length)
{
    ushort mask = 0xAAAA;  // Starting mask (16-bit)
    for (int i = 0; i < length; i++)
    {
        ushort encryptedChar = ReadUInt16();
        ushort afterMask = (ushort)(encryptedChar ^ mask);
        ushort keyPart = (ushort)((WzKey[i * 2 + 1] << 8) + WzKey[i * 2]);
        ushort decrypted = (ushort)(afterMask ^ keyPart);
        chars[i] = (char)decrypted;
        mask++;  // Increment mask
    }
    return new string(chars);
}
```

**Source:** `MapleLib/WzLib/Util/WzBinaryReader.cs`

### Version Hash Computation

The version hash is used to encrypt/decrypt file offsets:

```csharp
// From WzFile.cs
private static uint CheckAndGetVersionHash(int wzVersionHeader, int maplestoryPatchVersion)
{
    uint versionHash = 0;
    foreach (char ch in maplestoryPatchVersion.ToString())
    {
        versionHash = (versionHash * 32) + (byte)ch + 1;
    }

    // Verify against header
    int decryptedVersionNumber = (byte)~(
        (versionHash >> 24) & 0xFF ^
        (versionHash >> 16) & 0xFF ^
        (versionHash >> 8) & 0xFF ^
        versionHash & 0xFF
    );

    if (decryptedVersionNumber == wzVersionHeader)
        return versionHash;
    return 0; // Mismatch
}
```

### Offset Encryption

File offsets within WZ are encrypted using the version hash:

```csharp
// From WzBinaryReader.cs
public long ReadOffset()
{
    uint offset = (uint)BaseStream.Position;
    offset = (offset - Header.FStart) ^ uint.MaxValue;
    offset *= Hash;                              // Multiply by version hash
    offset -= WzAESConstant.WZ_OffsetConstant;   // 0x581C3F6D
    offset = RotateLeft(offset, (byte)(offset & 0x1F));  // Bit rotation
    uint encryptedOffset = ReadUInt32();
    offset ^= encryptedOffset;
    offset += Header.FStart * 2;
    return offset;
}
```

**Source:** `MapleLib/WzLib/Util/WzBinaryReader.cs`, constant from `WzAESConstant.cs`

### Canvas (Image) Encryption

Canvas data can be in two formats:

**Standard zlib format** (header `0x789C`, `0x78DA`, etc.):
- Data is directly zlib-compressed, no additional encryption

**listWz format** (any other header):
- Data is XOR-encrypted in blocks, then zlib-compressed

```csharp
// From WzPngProperty.cs - listWz decryption
ushort header = reader.ReadUInt16();
bool isListWzFormat = header != 0x9C78 && header != 0xDA78 &&
                      header != 0x0178 && header != 0x5E78;

if (isListWzFormat)
{
    // Read and decrypt blocks
    while (reader.BaseStream.Position < endOfPng)
    {
        int blockSize = reader.ReadInt32();
        for (int i = 0; i < blockSize; i++)
        {
            // XOR each byte with corresponding key byte
            dataStream.WriteByte((byte)(reader.ReadByte() ^ wzKey[i]));
        }
    }
    // Result is zlib-compressed, skip 2-byte header for DeflateStream
    dataStream.Position = 2;
    zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
}
```

**Source:** `MapleLib/WzLib/WzProperties/WzPngProperty.cs`

### Version Detection via Encryption

The parser tries each encryption key and measures decryption success:

```csharp
// From WzTool.cs
public static WzMapleVersion DetectMapleVersion(string wzFilePath, out short fileVersion)
{
    // Try each encryption and measure how many decrypted characters are printable ASCII
    mapleVersionSuccessRates.Add(WzMapleVersion.GMS, GetDecryptionSuccessRate(...));
    mapleVersionSuccessRates.Add(WzMapleVersion.EMS, GetDecryptionSuccessRate(...));
    mapleVersionSuccessRates.Add(WzMapleVersion.BMS, GetDecryptionSuccessRate(...));

    // Pick the version with highest success rate
    // If all fail (<70%) and ZLZ.dll exists, extract key from it
    if (maxSuccessRate < 0.7 && File.Exists("ZLZ.dll"))
        return WzMapleVersion.GETFROMZLZ;
}
```

**Source:** `MapleLib/WzLib/Util/WzTool.cs`

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [WzFileManager.md](./WzFileManager.md) | Central class for WZ file loading, caching, and format detection |
| [canvas-outlink-system.md](./canvas-outlink-system.md) | _Canvas directories and _outlink/_inlink resolution |

**See also:** [HaCreator/HaRepacker Architecture](../hacreator-harepacker-architecture/README.md) for IMG filesystem and hot-swap documentation.

---

## MapleStory WZ Format Overview

### Format Timeline

MapleStory has used several WZ file format versions over its 20+ year history:

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

### Related Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `WzFileManager` | MapleLib/WzFileManager.cs | Central WZ management |
| `WzFile` | MapleLib/WzLib/WzFile.cs | WZ file parsing |
| `WzImage` | MapleLib/WzLib/WzImage.cs | IMG structure parsing |
| `WzDirectory` | MapleLib/WzLib/WzDirectory.cs | Directory tree structure |
| `WzMsFile` | MapleLib/WzLib/MSFile/WzMsFile.cs | MS pack file support |
| `ImgFileSystemManager` | MapleLib/Img/ImgFileSystemManager.cs | Extracted IMG management |
| `VirtualWzDirectory` | MapleLib/Img/VirtualWzDirectory.cs | Filesystem-backed WzDirectory |
