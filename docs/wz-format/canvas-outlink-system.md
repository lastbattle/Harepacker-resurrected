# Canvas & Outlink System (_Canvas Directory)

## Overview

Starting with certain MapleStory versions (typically v180+), Nexon introduced a 64-bit WZ file format that splits large WZ files into multiple smaller files and separates canvas (image) data into dedicated `_Canvas` WZ files. This document explains how this system works and how HaCreator/MapleLib handles it.

## Directory Structure

### Traditional (32-bit) Format
```
MapleStory/
├── Map.wz
├── Mob.wz
├── Npc.wz
├── Character.wz
└── ...
```

### 64-bit Format
```
MapleStory/
├── Data/
│   ├── Base/
│   │   ├── Base_000.wz
│   │   └── Base_001.wz
│   ├── Map/
│   │   ├── Map_000.wz
│   │   ├── Map_001.wz
│   │   └── _Canvas/
│   │       ├── _Canvas_000.wz
│   │       ├── _Canvas_001.wz
│   │       └── ...
│   ├── Mob/
│   │   ├── Mob_000.wz
│   │   └── _Canvas/
│   │       └── _Canvas_000.wz
│   └── ...
```

Key differences:
- WZ files are in `Data/` subdirectory
- Each category is split into multiple numbered files (`_000.wz`, `_001.wz`, etc.)
- Canvas/image data is separated into `_Canvas/` subdirectories

## Why _Canvas Exists

The _Canvas system serves several purposes:

1. **File Size Management**: Separating large bitmap data reduces individual WZ file sizes
2. **Streaming/Loading**: Allows the game client to load structural data first, then stream images on demand
3. **Patching**: Smaller, separated files are easier to patch incrementally

## How _Canvas Links Work

### _outlink Property

In the main WZ files, canvas properties that reference _Canvas data contain an `_outlink` string property instead of actual bitmap data:

```
Main WZ (Mob_000.wz):
  8800141.img/
    attack1/
      0 (Canvas)
        _outlink = "Mob/_Canvas/8800141.img/attack1/0"
```

The `_outlink` value specifies the path to the actual canvas data in the _Canvas WZ file.

### _Canvas File Structure

The _Canvas WZ files have a different internal structure than the outlink path suggests:

```
_Canvas_000.wz:
  8800141.img/
    Anims/
      0/
        stand/
          LayerSlots/
            Slot0/
              Segment0/
                AnimReference/
                  0 (Canvas) <- Actual bitmap data
                  1 (Canvas)
                  2 (Canvas)
```

### Path Mapping

| Outlink Path Component | _Canvas Path Component |
|------------------------|------------------------|
| `AnimSet` | `Anims/0` |
| `{animName}` (e.g., "activated") | May differ (e.g., "stand", "hit", "die") |
| `LayerSlots/Slot0/Segment1` | `LayerSlots/Slot0/Segment0` (segment numbers may differ) |
| `AnimReference/{frame}` | `AnimReference/{frame}` |

## Resolution Process

When extracting WZ files to IMG format, the `WzLinkResolver` class handles _Canvas resolution:

### Step 1: Identify _Canvas Links
```csharp
bool hasOutlink = canvas.ContainsOutlinkProperty();
string outlinkPath = canvas["_outlink"]?.Value;
// e.g., "Etc/RoguelikeReactor/_Canvas/1000006.img/AnimSet/activated/LayerSlots/Slot0/Segment1/AnimReference/8"
```

### Step 2: Parse the Outlink Path
```
Category: Etc
Image: 1000006.img
Property Path: AnimSet/activated/LayerSlots/Slot0/Segment1/AnimReference/8
```

### Step 3: Search All _Canvas Files
The same image name may exist in multiple `_Canvas_xxx.wz` files with different frame content:
```csharp
foreach (var wzFile in _categoryWzFiles)
{
    if (wzFile.FilePath.Contains("_Canvas"))
    {
        var img = FindImageInDirectory(wzFile.WzDirectory, imageName);
        if (img != null)
            canvasImages.Add(img);
    }
}
```

### Step 4: Try Multiple Path Strategies

Since the internal _Canvas structure differs from the outlink path, the resolver tries multiple strategies:

1. **Exact Path**: Try the path as specified
2. **AnimSet → Anims/0 Mapping**: Replace `AnimSet` with `Anims/0`
3. **Segment Fallbacks**: Try `Segment0` if `Segment1` not found, or `Segment:All`
4. **Animation Name Fallback**: Try all available animations (stand, hit, die, etc.)
5. **Frame Index Search**: Search all AnimReference containers for the frame number

### Step 5: Copy Canvas Data

Once found, the actual bitmap data is copied to the destination canvas:
```csharp
byte[] compressedBytes = sourcePng.GetCompressedBytesForExtraction();
destPng.SetCompressedBytes(compressedBytes, width, height, format);
canvas.RemoveProperty("_outlink"); // Remove the link, data is now embedded
```

## Detection

The system detects 64-bit format by:
1. Looking for `Data/Base/Base_000.wz` file
2. Counting WZ files in the `Data/` directory (40+ files indicates 64-bit)

```csharp
bool is64Bit = WzFileManager.Detect64BitDirectoryWzFileFormat(mapleStoryPath);
```

## Extraction Behavior

When extracting 64-bit WZ files:

1. **_Canvas files are loaded** but not extracted separately
2. **Links are resolved** by finding the canvas data in _Canvas files
3. **Bitmap data is embedded** into the extracted .img files
4. **Unresolved links remain** as `_outlink` properties (if canvas not found)

This produces self-contained .img files that don't require separate _Canvas data.

## Common Issues

### Missing Frames
Some _Canvas files may not contain all frames referenced by outlinks. This results in unresolved links:
```
[WzLinkResolver] Could not find property path 'AnimSet/activated/.../AnimReference/9' in image '1000006.img'
```

### Path Mismatches
The internal _Canvas structure often differs significantly from outlink paths:
- Different animation names
- Different segment numbers
- Flattened hierarchies

The resolver handles this through multiple fallback strategies.

## Related Classes

- `WzLinkResolver` - Resolves _inlink/_outlink references
- `WzExtractionService` - Orchestrates WZ extraction to IMG format
- `WzFileManager` - Manages WZ file loading and 64-bit detection
- `WzCanvasProperty` - Canvas property with link resolution support

## References

- `MapleLib/WzLib/WzLinkResolver.cs` - Link resolution implementation
- `MapleLib/Img/WzExtractionService.cs` - Extraction service
- `HaCreator/GUI/UnpackWzToImg.cs` - Extraction UI
