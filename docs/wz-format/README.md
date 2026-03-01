# WZ Format Documentation

This directory contains documentation about MapleStory WZ file format handling in HaCreator/HaRepacker.

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [WZ File Overview](./wz-file-overview.md) | What WZ/IMG files are, structure, property types, and encryption |
| [WZ Format History](./wz-format-history.md) | How WZ format evolved (Beta, Pre-BB, Post-BB, 64-bit, MS/NM packs) |
| [WzFileManager Reference](./WzFileManager.md) | Central class for WZ file loading, caching, and format detection |
| [Canvas & Outlink System](./canvas-outlink-system.md) | _Canvas directories and _outlink/_inlink resolution |

---

## Quick Links

### Understanding WZ Files
- [What is a .wz file?](./wz-file-overview.md#what-is-a-wz-file)
- [What is a .img file?](./wz-file-overview.md#what-is-a-img-file)
- [IMG Property Types](./wz-file-overview.md#img-property-types)
- [WZ/IMG Encryption](./wz-file-overview.md#wzimg-encryption)

### Format Versions
- [Format Timeline](./wz-format-history.md#format-timeline)
- [Beta/Early (v0.01-v0.30)](./wz-format-history.md#beta--very-early-maplestory-v001---v030)
- [Pre-Big Bang (v0.31-v0.94)](./wz-format-history.md#pre-big-bang-v031---v094)
- [Post-Big Bang (v0.95-v179)](./wz-format-history.md#post-big-bang-v095---v179)
- [64-bit Modern (v180+)](./wz-format-history.md#64-bit-exe-with-modern-format-v180)
- [MS Pack Files (v220+)](./wz-format-history.md#ms-pack-files-v220)

### Detection & Loading
- [Format Detection Flowchart](./wz-format-history.md#format-detection-flowchart)
- [Version Detection from .exe](./wz-format-history.md#version-detection-from-maplestoryexe)
- [WzFileManager Usage](./WzFileManager.md)

---

## See Also

- [HaCreator/HaRepacker Architecture](../hacreator-harepacker-architecture/README.md) - IMG filesystem and hot-swap documentation
