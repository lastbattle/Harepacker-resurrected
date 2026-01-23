# Hot-Swapping System for .img files

## Overview

The hot-swap system automatically detects when users add, remove, or modify files via Windows Explorer and updates the internal file lists and UI accordingly.
This enables a live-editing workflow where external changes are reflected in real-time without restarting the application.

**Two Hot-Swap Systems:**
| Application | Target Files | Purpose |
|-------------|--------------|---------|
| **HaCreator** | Loose `.img` files | Live asset editing for map creation |
| **HaRepacker** | Loose `.img` directories | External modification detection for IMG editing |

---

# Part 1: HaCreator Hot-Swap (.img Files)

## Architecture

### System Layers

```
┌───────────────────────────────────────────────────────────────────────┐
│                        UI Layer (HaCreator)                           │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌─────────────┐  │
│  │TilePanel │ │ ObjPanel │ │LifePanel │ │BackPanel │ │ QuestEditor │  │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘ └──────┬──────┘  │
│       │            │            │            │              │         │
│       └────────────┴─────┬──────┴────────────┴──────────────┘         │
│                          ▼                                            │
│              ┌─────────────────────────┐                              │
│              │  HotSwapRefreshService  │ ◄── Translates events        │
│              │  (HaCreator/Wz)         │     to UI-specific           │
│              └───────────┬─────────────┘     notifications            │
└──────────────────────────┼────────────────────────────────────────────┘
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

### HaCreator/GUI/Quest/
| File | Hot Swap Handler |
|------|------------------|
| `QuestEditor.xaml.cs` | `OnQuestDataChanged()` → `RefreshQuestList()` |

**QuestEditor Hot Swap Details:**
- Subscribes to `HotSwapRefreshService.QuestDataChanged` event via `SubscribeToHotSwap()`
- `OnQuestDataChanged()` marshals to UI thread via `Dispatcher.Invoke()`
- `HandleQuestDataChange()` stores current selection, refreshes list, restores selection
- `RefreshQuestList()` reloads all quest data from `InfoManager`

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

---

# Part 2: HaRepacker Hot-Swap (.img Directories)

## Overview

The HaRepacker hot-swap system monitors currently-opened `.img` directories for external modifications and **automatically applies changes** with a brief notification. External file changes (add, remove, modify, rename) from Windows Explorer are assumed to be intentional and are immediately reflected in the UI.

**Note:** This system only supports loose `.img` file directories (extracted/unpacked format), not packed `.wz` files.

### Design Philosophy
- **Trust External Changes**: Changes made via Windows Explorer are intentional
- **Auto-Apply**: No user confirmation required; changes apply immediately
- **Subtle Notifications**: Brief auto-dismissing messages (3 seconds) inform the user
- **Save Protection**: Save operations temporarily disable watching to prevent self-triggered reloads

### Use Cases
1. **Image Editing Workflow**: Edit textures in Photoshop/GIMP while HaRepacker is open
2. **Batch Processing**: Run scripts that modify multiple .img files
3. **Collaborative Editing**: Multiple developers working on same .img directories
4. **Version Control**: Git/SVN operations that modify .img files

---

## Architecture

### System Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                        UI Layer (HaRepacker)                         │
│  ┌──────────┐  ┌───────────────┐  ┌─────────────────────────────┐   │
│  │ MainForm │──│ TreeView(IMG) │──│ ImgFileModificationNotifier │   │
│  └────┬─────┘  └───────────────┘  └──────────────┬──────────────┘   │
│       │                                          │                   │
│       └──────────────────────────────────────────┘                   │
│                           │ OnImgFileModified event                  │
└───────────────────────────┼──────────────────────────────────────────┘
                            │
┌───────────────────────────┼──────────────────────────────────────────┐
│                     Data Layer (MapleLib)                            │
│              ┌────────────▼───────────────┐                          │
│              │ ImgDirectoryWatcherService │ ◄── Monitors opened IMG  │
│              │   (MapleLib/Img)           │     directories          │
│              └────────────┬───────────────┘                          │
│                           │                                          │
│              ┌────────────▼───────────────┐                          │
│              │   ImgFileChangeTracker     │ ◄── Tracks file hashes   │
│              │   (MapleLib/Img)           │     and timestamps       │
│              └────────────┬───────────────┘                          │
└───────────────────────────┼──────────────────────────────────────────┘
                            ▼
              ┌─────────────────────────────┐
              │     Windows FileSystem      │
              │   (*.img directories)       │
              └─────────────────────────────┘
```

---

## Component Details

### ImgDirectoryWatcherService (MapleLib/Img)

Monitors opened .img directories for external changes using `System.IO.FileSystemWatcher`.

**Responsibilities:**
- Watch .img directories that are currently open in TreeView
- Debounce rapid changes (default 500ms)
- Track file modification timestamps and sizes
- Raise `ImgFileModified` event when external change detected
- Ignore changes triggered by HaRepacker's own save operations

**Events:**
| Event | Args | Description |
|-------|------|-------------|
| `ImgFileModified` | `ImgFileModifiedEventArgs` | .img file modified externally |
| `ImgFileDeleted` | `ImgFileDeletedEventArgs` | .img file deleted while open |
| `ImgFileAdded` | `ImgFileAddedEventArgs` | New .img file added to directory |
| `ImgFileRenamed` | `ImgFileRenamedEventArgs` | .img file renamed/moved |
| `Error` | `ErrorEventArgs` | Watcher error occurred |

**Key Properties:**
```csharp
public class ImgDirectoryWatcherService : IDisposable
{
    // Track which directories are being watched
    Dictionary<string, FileSystemWatcher> _watchers;

    // Ignore our own save operations
    HashSet<string> _ignorePaths;

    // Debounce timers per file
    Dictionary<string, Timer> _debounceTimers;
}
```

### ImgFileChangeTracker (MapleLib/Img)

Tracks file state to detect meaningful changes vs. touch operations.

**Key Methods:**
| Method | Description |
|--------|-------------|
| `RecordFileState(path)` | Store current hash/timestamp/size |
| `HasFileChanged(path)` | Compare current state to recorded |
| `GetChangeDetails(path)` | Return what changed (size, content, timestamp) |
| `ClearTracking(path)` | Stop tracking file (on close) |

**Change Detection:**
```csharp
public enum ImgFileChangeType
{
    None,           // No meaningful change
    SizeChanged,    // File size differs
    ContentChanged, // Content hash differs
    Deleted,        // File no longer exists
    Added,          // New file in directory
    Renamed         // File path changed
}
```

### HotSwapNotificationBar (HaRepacker/GUI/HotSwap)

Minimal UI component that displays brief, auto-dismissing status messages.

**Behavior:**
- Shows a subtle notification bar below the toolbar
- Auto-dismisses after 3 seconds
- Light green background for success (added/reloaded/removed)
- Light red background for errors
- No user interaction required

**Message Types:**
| Change Type | Message Format | Background |
|-------------|----------------|------------|
| `ContentChanged` | "{filename} reloaded" | Green |
| `Deleted` | "{filename} removed" | Green |
| `Added` | "{filename} added" | Green |
| `Renamed` | "{oldname} renamed to {newname}" | Green |
| Error | Error message | Red |

**Implementation:**
```csharp
public class HotSwapNotificationBar : UserControl
{
    private Label _messageLabel;
    private Timer _hideTimer;  // 3 second auto-hide

    public void ShowMessage(string message, bool isError = false);
    public void QueueNotification(FileModificationInfo modification);
}
```

---

## Auto-Handling Behavior

All external file changes are automatically applied without user confirmation:

### File Modified Externally

```
Timeline:
1. User opens Mob/ directory in HaRepacker
2. External tool modifies 0100100.img
3. FileSystemWatcher detects change (after 500ms debounce)
4. HotSwapManager auto-reloads the WzImage
5. Brief notification: "0100100.img reloaded" (auto-dismisses in 3s)
```

### File Deleted Externally

```
Timeline:
1. User opens Item/ directory in HaRepacker
2. User deletes Consume/0200000.img via Windows Explorer
3. FileSystemWatcher detects deletion
4. HotSwapManager removes image from VirtualWzDirectory and TreeView
5. Brief notification: "0200000.img removed" (auto-dismisses in 3s)
```

### File Added Externally

```
Timeline:
1. User opens Mob/ directory in HaRepacker
2. User copies 0100200.img into directory via Windows Explorer
3. FileSystemWatcher detects new file
4. HotSwapManager adds image to VirtualWzDirectory and TreeView
5. Brief notification: "0100200.img added" (auto-dismisses in 3s)
```

### Save Operation Protection

```
Timeline:
1. User clicks File > Save on VirtualWzDirectory
2. HotSwapManager calls BeginDirectorySaveOperation() to ignore directory
3. Files are saved to disk
4. EndDirectorySaveOperation() resumes watching after 500ms delay
5. No self-triggered reload occurs
```

---

## Configuration

### HotSwapConstants (MapleLib/Img/HotSwapConstants.cs)

HaRepacker hot-swap uses compile-time constants (not user-configurable):

| Constant | Type | Value | Description |
|----------|------|-------|-------------|
| `EnableImgFileWatching` | bool | `true` | Master switch for IMG hot swap |
| `DebounceMs` | int | `500` | Debounce delay in milliseconds |
| `ShowNotifications` | bool | `true` | Show brief notification messages |
| `AutoReloadIfNoChanges` | bool | `false` | (Legacy - all changes auto-apply now) |
| `AutoAddNewFiles` | bool | `false` | (Legacy - all additions auto-apply now) |
| `TrackContentHash` | bool | `true` | Use MD5 hash for change detection |
| `MaxQueuedNotifications` | int | `50` | Max pending notifications |

**Note:** All external file changes are now auto-applied. The notification bar only displays brief status messages that auto-dismiss after 3 seconds.

---

## Implementation Status

### Phase 1: Core Infrastructure (COMPLETED)
- [x] `ImgDirectoryWatcherService.cs` - Directory monitoring with debouncing
- [x] Change detection with MD5 hashing
- [x] Event argument classes (ImgFileModifiedEventArgs, etc.)
- [x] Directory ignore support (`IgnoreDirectory`/`UnignoreDirectory`)

### Phase 2: UI Integration (COMPLETED)
- [x] `HotSwapNotificationBar.cs` - Auto-dismissing notification bar (3 seconds)
- [x] `HotSwapManager.cs` - Auto-apply all external changes
- [x] MainForm integration with save protection
- [x] `VirtualWzDirectory.RemoveImage()` - Proper cleanup on file deletion
- [x] HotSwapConstants in MapleLib/Img - Compile-time configuration

### Phase 3: Advanced Features (PLANNED)
- [ ] Consolidated notifications for batch operations
- [ ] Change history log
- [ ] Undo support for auto-applied changes

---

## Thread Safety

### Synchronization Mechanisms

| Component | Mechanism | Purpose |
|-----------|-----------|---------|
| `ImgDirectoryWatcherService._watchers` | `lock` | Watcher collection access |
| `ImgFileChangeTracker._states` | `ConcurrentDictionary` | File state tracking |
| `ImgFileModificationNotifier` | `SynchronizationContext` | Marshal to UI thread |

### UI Thread Marshaling

All UI updates are marshaled via `Control.Invoke()`:

```csharp
private void OnImgFileModified(object sender, ImgFileModifiedEventArgs e)
{
    if (InvokeRequired)
    {
        Invoke(new Action(() => OnImgFileModified(sender, e)));
        return;
    }

    ShowNotification(e.FilePath, e.ChangeType);
}
```

---

## Edge Cases & Solutions

### 1. Rapid File Modifications

**Problem:** External tools may write multiple times during save (temp file, then rename).

**Solution:** Use 500ms debounce and detect "write complete" by checking if file is accessible.

```csharp
private bool IsFileWriteComplete(string path)
{
    try
    {
        using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            return true;
    }
    catch (IOException)
    {
        return false; // Still being written
    }
}
```

### 2. Self-Triggered Changes

**Problem:** HaRepacker's own save operation triggers the watcher, causing unwanted reloads.

**Solution:** Ignore entire directory during save operations:

```csharp
// In MainForm.SaveToolStripMenuItem_Click:
if (node.Tag is VirtualWzDirectory virtualDir)
{
    _hotSwapManager?.BeginDirectorySaveOperation(virtualDir.FilesystemPath);
    try
    {
        int savedCount = virtualDir.SaveAllChangedImages();
        // Show result...
    }
    finally
    {
        _hotSwapManager?.EndDirectorySaveOperation(virtualDir.FilesystemPath);
    }
}

// In ImgDirectoryWatcherService:
public void IgnoreDirectory(string directoryPath)
{
    lock (_ignorePathsLock)
    {
        _ignoreDirectories.Add(Path.GetFullPath(directoryPath));
    }
}

private bool IsPathIgnored(string filePath)
{
    // Check if file is in an ignored directory
    foreach (var ignoredDir in _ignoreDirectories)
    {
        if (fullPath.StartsWith(ignoredDir + Path.DirectorySeparatorChar))
            return true;
    }
    return false;
}
```

### 3. Network/USB Drive Disconnection

**Problem:** FileSystemWatcher fails when drive is disconnected.

**Solution:** Handle `Error` event and gracefully degrade:

```csharp
private void OnWatcherError(object sender, ErrorEventArgs e)
{
    var path = GetPathForWatcher(sender);
    _logger.Warn($"FileSystemWatcher error for {path}: {e.GetException().Message}");

    // Try to recreate watcher
    RecreateWatcher(path);
}
```

### 4. IMG File Locked by Another Process

**Problem:** User tries to reload but file is locked.

**Solution:** Retry with exponential backoff, show clear error:

```csharp
public async Task<bool> TryReloadWithRetry(string path, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            ReloadImgFile(path);
            return true;
        }
        catch (IOException)
        {
            await Task.Delay(100 * (1 << i)); // 100ms, 200ms, 400ms
        }
    }

    ShowError($"Cannot reload {Path.GetFileName(path)} - file is locked by another process.");
    return false;
}
```

### 5. Watching Nested Directory Structures

**Problem:** .img directories may have deep nesting (e.g., Map/Tile/set1.img/0/).

**Solution:** Use `IncludeSubdirectories = true` with proper filtering:

```csharp
var watcher = new FileSystemWatcher(directoryPath)
{
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
    Filter = "*.img",
    IncludeSubdirectories = true,
    EnableRaisingEvents = true
};
```

---

## File Structure

### MapleLib/Img/
| File | Purpose |
|------|---------|
| `ImgDirectoryWatcherService.cs` | Directory monitoring with debouncing and directory ignore |
| `VirtualWzDirectory.cs` | Added `RemoveImage()`/`RemoveImageByPath()` for hot-swap |
| `HotSwapConstants.cs` | Compile-time constants for hot-swap behavior |

### HaRepacker/GUI/HotSwap/
| File | Purpose |
|------|---------|
| `HotSwapManager.cs` | Auto-apply changes, save protection, tree updates |
| `HotSwapNotificationBar.cs` | Auto-dismissing status message bar (3 seconds) |

### HaRepacker/GUI/
| File | Purpose |
|------|---------|
| `MainForm.cs` | Event wiring, save protection integration |

---

## Testing Scenarios

### Basic Operations
- [ ] Open .img directory → watching starts
- [ ] Close .img directory → watching stops
- [ ] External modify .img file → auto-reloads with brief notification
- [ ] External delete .img file → auto-removes from tree with notification

### File Operations
- [ ] Add new .img file externally → auto-adds to tree with notification
- [ ] Delete .img file externally → auto-removes with notification
- [ ] Rename .img file externally → tree updates automatically

### Save Protection
- [ ] Save VirtualWzDirectory → no self-triggered reload
- [ ] Save during external modifications → changes ignored during save
- [ ] Save completes → watching resumes after 500ms

### Edge Cases
- [ ] Rapid external saves → single notification after debounce
- [ ] Delete file with null Name → handled gracefully
- [ ] Network drive disconnect → graceful degradation
- [ ] Deep nested directories → all changes detected

### Thread Safety
- [ ] External modify during tree navigation → no crash
- [ ] Multiple .img files modified simultaneously → all notifications queued
- [ ] Reload during background parsing → proper cancellation
