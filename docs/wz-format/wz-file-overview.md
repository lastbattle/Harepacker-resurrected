# WZ File Overview

This document explains what WZ and IMG files are, their internal structure, and how encryption works.

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

## See Also

- [WZ Format History](./wz-format-history.md) - How WZ format evolved over MapleStory versions
- [WzFileManager Reference](./WzFileManager.md) - Central WZ file loading and management class
- [Canvas & Outlink System](./canvas-outlink-system.md) - _Canvas directories and _outlink/_inlink resolution
