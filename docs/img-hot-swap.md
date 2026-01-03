# Hot-Swapping System for .img Files

## Overview

The hot-swap system automatically detects when users add, remove, or modify `.img` files via Windows Explorer and updates the internal file lists and UI accordingly. 
This enables a live-editing workflow where external changes are reflected in real-time without restarting the application.

---

## Architecture

### System Layers

```
┌─────────────────────────────────────────────────────────────────-┐
│                        UI Layer (HaCreator)                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│  │TilePanel │ │ ObjPanel │ │LifePanel │ │BackPanel │             │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘             │
│       │            │            │            │                   │
│       └────────────┴─────┬──────┴────────────┘                   │
│                          ▼                                       │
│              ┌─────────────────────────┐                         │
│              │  HotSwapRefreshService  │ ◄── Translates events   │
│              │  (HaCreator/Wz)         │     to UI-specific      │
│              └───────────┬─────────────┘     notifications       │
└──────────────────────────┼───────────────────────────────────────┘
                           │ CategoryIndexChanged event
┌──────────────────────────┼───────────────────────────────────────┐
│                    Data Layer (MapleLib)                         │
│              ┌───────────▼─────────────┐                         │
│              │  ImgFileSystemManager   │ ◄── Core manager with   │
│              │  (MapleLib/Img)         │     LRU cache & index   │
│              └───────────┬─────────────┘                         │
│                          │ OnImgFileChanged                      │
│              ┌───────────▼─────────────┐                         │
│              │ FileSystemWatcherService│ ◄── Monitors filesystem │
│              │  (MapleLib/Img)         │     with debouncing     │
│              └───────────┬─────────────┘                         │
│                          │                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           ▼
              ┌─────────────────────────┐
              │   Windows FileSystem    │
              │   (Mob/, Npc/, Map/...) │
              └─────────────────────────┘
```

### Data Source Hierarchy

```
IDataSource (Interface)
├── ImgFileSystemDataSource (Uses ImgFileSystemManager)
├── WzFileDataSource (Legacy WZ file support)
└── HybridDataSource (IMG + WZ fallback)
```

---

## Component Details

### FileSystemWatcherService (MapleLib/Img)

Monitors directories for .img file changes using `System.IO.FileSystemWatcher`.

**Responsibilities:**
- Watch category directories (Mob/, Npc/, Map/Tile/, etc.)
- Debounce rapid changes (default 500ms)
- Consolidate multiple events for same file
- Raise `ImgFileChanged` event with change details

**Events:**
| Event | Args | Description |
|-------|------|-------------|
| `ImgFileChanged` | `ImgFileChangedEventArgs` | File created/deleted/modified/renamed |
| `VersionDirectoryChanged` | `VersionDirectoryChangedEventArgs` | Version folder added/removed |
| `Error` | `ErrorEventArgs` | Watcher error occurred |

**Debouncing Logic:**
```
File change detected → Add to pending queue → Start/reset 500ms timer
Timer expires → Process all pending changes → Group by path, take final state
```

### ImgFileSystemManager (MapleLib/Img)

Core manager that maintains the category index and LRU cache.

**Key Methods:**
| Method | Description |
|--------|-------------|
| `EnableHotSwap()` | Starts file watching for all categories |
| `DisableHotSwap()` | Stops file watching |
| `AddToCategoryIndex()` | Adds file to category index |
| `RemoveFromCategoryIndex()` | Removes file from category index |
| `ExistsInCategoryIndex()` | Checks if file exists in index |
| `InvalidateCache()` | Removes file from LRU cache |

**Event Handling (OnImgFileChanged):**
```csharp
switch (e.ChangeType)
{
    case Created:
        AddToCategoryIndex() → Raise FileAdded

    case Deleted:
        RemoveFromCategoryIndex() → InvalidateCache() → Raise FileRemoved

    case Changed:
        // Windows quirk: sometimes reports new files as Changed
        if (!ExistsInCategoryIndex())
            AddToCategoryIndex() → Raise FileAdded  // Treat as new
        else
            InvalidateCache() → Raise FileModified

    case Renamed:
        RemoveFromCategoryIndex(oldPath) → AddToCategoryIndex(newPath)
}
```

### HotSwapRefreshService (HaCreator/Wz)

Orchestrates UI refresh by translating data layer events into panel-specific events.

**Event Translation:**
| CategoryIndexChanged | → | Panel Event |
|---------------------|---|-------------|
| Map/Tile/* | → | TileSetChanged |
| Map/Obj/* | → | ObjectSetChanged |
| Map/Back/* | → | BackgroundSetChanged |
| Mob/* | → | LifeDataChanged (Mob) |
| Npc/* | → | LifeDataChanged (Npc) |
| Reactor/* | → | LifeDataChanged (Reactor) |
| Quest/* | → | QuestDataChanged |
| String/* | → | StringDataChanged |

**InfoManager Updates:**
- Updates `WzInformationManager` dictionaries (TileSets, ObjectSets, MobNameCache, etc.)
- For Mob/Npc additions: Looks up names from String.wz via `LookupMobNameFromString()`
- For deletions: Removes entries from cache

**Entity ID Normalization:**
File names use 7-character format with leading zeros (e.g., `0100100.img`), but String.wz uses no leading zeros (e.g., `100100`). The `NormalizeEntityId()` method strips leading zeros for consistent lookups.

```csharp
"0100100" → "100100"  // File name to String.wz key
"0000001" → "1"       // Edge case handling
```

---

## Category to Panel Mapping

| Category Path | InfoManager Dictionary | UI Panel | ID Format |
|---------------|------------------------|----------|-----------|
| `Map/Tile/*.img` | `TileSets` | TilePanel | Set name (no normalization) |
| `Map/Obj/*.img` | `ObjectSets` | ObjPanel | Set name (no normalization) |
| `Map/Back/*.img` | `BackgroundSets` | BackgroundPanel | Set name (no normalization) |
| `Mob/*.img` | `MobNameCache` | LifePanel | Normalized (strip leading zeros) |
| `Npc/*.img` | `NpcNameCache` | LifePanel | Normalized (strip leading zeros) |
| `Reactor/*.img` | `Reactors` | LifePanel | Normalized (strip leading zeros) |
| `Quest/*.img` | Quest dicts | QuestEditor | As-is |
| `String/*.img` | Various caches | Multiple | As-is |

---

## Configuration

### HotSwapConstants (MapleLib/Img/HotSwapConstants.cs)

Compile-time constants (not persisted to config.json):

| Constant | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultEnabled` | bool | `true` | Master switch for hot swap |
| `DefaultDebounceMs` | int | `500` | Debounce delay in milliseconds |
| `DefaultWatchVersions` | bool | `true` | Watch for version folder changes |
| `DefaultWatchCategories` | bool | `true` | Watch category directories |
| `DefaultAutoInvalidateCache` | bool | `true` | Auto-invalidate LRU cache |
| `DefaultAutoRefreshDisplayedAssets` | bool | `true` | Auto-refresh UI panels |
| `DefaultConfirmRefreshPlacedItems` | bool | `true` | Confirm before refreshing board items |
| `DefaultPauseSimulatorOnAssetChange` | bool | `false` | Pause MapSimulator on changes |
| `DefaultDeletedAssetBehavior` | enum | `ShowPlaceholder` | Deleted asset handling |

### HaCreatorPaths (MapleLib/Img/HaCreatorPaths.cs)

Application path constants:

| Constant | Value |
|----------|-------|
| `AppDataRoot` | `%AppData%\HaCreator` |
| `DefaultConfigPath` | `%AppData%\HaCreator\config.json` |
| `DefaultDataPath` | `%AppData%\HaCreator\Data` |
| `VersionsFolderName` | `versions` |
| `CustomFolderName` | `custom` |

---

## Thread Safety

### Synchronization Mechanisms

| Component | Mechanism | Purpose |
|-----------|-----------|---------|
| `ImgFileSystemManager._cacheLock` | `ReaderWriterLockSlim` | Category index read/write |
| `HotSwapRefreshService._uiContext` | `SynchronizationContext` | Marshal events to UI thread |
| `LifePanel.RefreshMobList()` | `.ToList()` snapshot | Prevent enumeration errors |
| Pending changes queue | `ConcurrentQueue` | Thread-safe change buffering |

### UI Thread Marshaling

All UI updates are marshaled via `SynchronizationContext.Post()`:

```csharp
private void RaiseOnUIThread(Action action)
{
    if (_uiContext != null)
        _uiContext.Post(_ => action(), null);
    else
        action();
}
```

### Collection Snapshots

To prevent "Collection was modified during enumeration" exceptions, UI refresh methods create snapshots:

```csharp
public void RefreshMobList()
{
    var mobSnapshot = Program.InfoManager.MobNameCache.ToList();
    foreach (var entry in mobSnapshot)
    {
        // Safe enumeration
    }
}
```

---

## Edge Cases & Solutions

### 1. Windows Reports "Changed" Instead of "Created"

**Problem:** Windows FileSystemWatcher sometimes reports new files as `Changed` instead of `Created`.

**Solution:** Check if file exists in category index; if not, treat as `Added`:

```csharp
case WatcherChangeTypes.Changed:
    if (!ExistsInCategoryIndex(category, relativePath))
    {
        AddToCategoryIndex(category, relativePath);
        OnCategoryIndexChanged(category, CategoryChangeType.FileAdded, relativePath);
    }
    else
    {
        InvalidateCache(category, relativePath);
        OnCategoryIndexChanged(category, CategoryChangeType.FileModified, relativePath);
    }
```

### 2. Entity ID Format Mismatch

**Problem:** Mob/Npc file names have leading zeros (`0100100.img`) but String.wz keys don't (`100100`).

**Solution:** Normalize IDs before cache operations:

```csharp
private static string NormalizeEntityId(string entityId)
{
    string normalized = entityId.TrimStart('0');
    return string.IsNullOrEmpty(normalized) ? "0" : normalized;
}
```

### 3. Restoring Names After Delete/Add Cycle

**Problem:** When a mob file is deleted and re-added, the original name from String.wz was lost.

**Solution:** Look up names directly from String.wz when files are added:

```csharp
case AssetChangeType.Added:
    string mobName = LookupMobNameFromString(normalizedId);
    _infoManager.MobNameCache[normalizedId] = mobName ?? $"Mob {normalizedId}";
```

### 4. Rapid Sequential Changes

**Problem:** Bulk file operations trigger many events in quick succession.

**Solution:** Debounce with 500ms delay, consolidate by file path, take final state.

### 5. File Locked During Refresh

**Problem:** File may be locked by external process during hot swap.

**Solution:** Retry with exponential backoff (100ms, 200ms, 400ms), show error after max retries.

---

## Initialization Flow

```
HaEditor.xaml.cs
    │
    ├── tilePanel.Initialize(hcsm)
    ├── objPanel.Initialize(hcsm)
    ├── lifePanel.Initialize(hcsm)      ◄── Calls hcsm.SetLifePanel(this)
    ├── bgPanel.Initialize(hcsm)        ◄── Calls hcsm.SetBackgroundPanel(this)
    │
    └── hcsm.InitializeHotSwap()        ◄── Must be called AFTER all panels registered
            │
            ├── Create HotSwapRefreshService
            ├── Subscribe to panel events
            └── Call dataSource.EnableHotSwap()
                    │
                    └── FileSystemWatcherService.Start()
```

---

## File Structure

### MapleLib/Img/
| File | Purpose |
|------|---------|
| `FileSystemWatcherService.cs` | File system monitoring with debouncing |
| `HaCreatorConfig.cs` | Configuration loading/saving |
| `HaCreatorPaths.cs` | Path constants |
| `HotSwapConstants.cs` | Hot swap default values |
| `ImgFileSystemManager.cs` | Core manager with category index |
| `VersionManager.cs` | Version folder management |

### HaCreator/Wz/
| File | Purpose |
|------|---------|
| `HotSwapEventArgs.cs` | Event argument classes |
| `HotSwapRefreshService.cs` | UI refresh orchestration |
| `AssetUsageTracker.cs` | Track assets in use by BoardItems |
| `WzInformationManager.cs` | Asset info dictionaries |

### HaCreator/GUI/EditorPanels/
| File | Hot Swap Handler |
|------|------------------|
| `TilePanel.cs` | `OnTileSetChanged()` |
| `ObjPanel.cs` | `OnObjectSetChanged()` |
| `BackgroundPanel.cs` | `OnBackgroundSetChanged()` |
| `LifePanel.cs` | `OnLifeDataChanged()` |

---

## Testing Scenarios

### Basic Operations
- [ ] Add single .img file → appears in panel
- [ ] Delete single .img file → removed from panel
- [ ] Modify .img file → panel refreshes with new content
- [ ] Rename .img file → old removed, new added

### Mob/Npc Specific
- [ ] Delete mob → disappears from list
- [ ] Add mob back → appears with correct name from String.wz
- [ ] Add new mob (no String.wz entry) → appears with placeholder name

### Edge Cases
- [ ] Rapid bulk copy (10+ files) → all appear after debounce
- [ ] Delete + add same file quickly → correct final state
- [ ] File locked by external app → graceful error handling

### Thread Safety
- [ ] Modify files while scrolling panel → no crash
- [ ] Add/delete during map editing → no enumeration errors
