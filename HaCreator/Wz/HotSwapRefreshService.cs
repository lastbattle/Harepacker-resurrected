using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using HaCreator.MapEditor.Info;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.Wz
{
    /// <summary>
    /// Service that orchestrates hot swap refresh operations across UI components.
    /// Subscribes to data source change events and translates them into UI-specific events.
    /// </summary>
    public class HotSwapRefreshService : IDisposable
    {
        #region Fields
        private readonly WzInformationManager _infoManager;
        private readonly SynchronizationContext _uiContext;
        private ImgFileSystemDataSource _dataSource;
        private bool _disposed;
        private bool _isEnabled;
        #endregion

        #region Events
        /// <summary>
        /// Raised when a tile set is added, removed, or modified
        /// </summary>
        public event EventHandler<TileSetChangedEventArgs> TileSetChanged;

        /// <summary>
        /// Raised when an object set is added, removed, or modified
        /// </summary>
        public event EventHandler<ObjectSetChangedEventArgs> ObjectSetChanged;

        /// <summary>
        /// Raised when a background set is added, removed, or modified
        /// </summary>
        public event EventHandler<BackgroundSetChangedEventArgs> BackgroundSetChanged;

        /// <summary>
        /// Raised when life data (mob, npc, reactor) changes
        /// </summary>
        public event EventHandler<LifeDataChangedEventArgs> LifeDataChanged;

        /// <summary>
        /// Raised when quest data changes
        /// </summary>
        public event EventHandler<QuestDataChangedEventArgs> QuestDataChanged;

        /// <summary>
        /// Raised when string data changes
        /// </summary>
        public event EventHandler<StringDataChangedEventArgs> StringDataChanged;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new HotSwapRefreshService
        /// </summary>
        /// <param name="infoManager">The WzInformationManager instance</param>
        /// <param name="uiContext">The UI synchronization context for marshaling events</param>
        public HotSwapRefreshService(WzInformationManager infoManager, SynchronizationContext uiContext = null)
        {
            _infoManager = infoManager ?? throw new ArgumentNullException(nameof(infoManager));
            _uiContext = uiContext ?? SynchronizationContext.Current;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Gets whether hot swap refresh is enabled
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// Subscribes to change events from an ImgFileSystemDataSource
        /// </summary>
        /// <param name="dataSource">The data source to subscribe to</param>
        public void SubscribeToDataSource(ImgFileSystemDataSource dataSource)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HotSwapRefreshService));

            // Unsubscribe from previous data source if any
            if (_dataSource != null)
            {
                _dataSource.CategoryIndexChanged -= OnCategoryIndexChanged;
            }

            _dataSource = dataSource;

            if (_dataSource != null)
            {
                _dataSource.CategoryIndexChanged += OnCategoryIndexChanged;
                _isEnabled = true;
            }
        }

        /// <summary>
        /// Manually triggers a refresh for a specific category
        /// </summary>
        /// <param name="category">The category to refresh</param>
        /// <param name="relativePath">The relative path of the changed file</param>
        /// <param name="changeType">The type of change</param>
        public void TriggerRefresh(string category, string relativePath, AssetChangeType changeType)
        {
            ProcessCategoryChange(category, relativePath, changeType);
        }
        #endregion

        #region Event Handlers
        private void OnCategoryIndexChanged(object sender, CategoryIndexChangedEventArgs e)
        {
            if (_disposed || !_isEnabled)
                return;

            // Convert CategoryChangeType to AssetChangeType
            AssetChangeType assetChangeType = e.ChangeType switch
            {
                CategoryChangeType.FileAdded => AssetChangeType.Added,
                CategoryChangeType.FileRemoved => AssetChangeType.Removed,
                CategoryChangeType.FileModified => AssetChangeType.Modified,
                CategoryChangeType.FileRenamed => AssetChangeType.Modified,
                CategoryChangeType.IndexRefreshed => AssetChangeType.Modified,
                _ => AssetChangeType.Modified
            };

            ProcessCategoryChange(e.Category, e.RelativePath, assetChangeType);
        }

        private void ProcessCategoryChange(string category, string relativePath, AssetChangeType changeType)
        {
            if (string.IsNullOrEmpty(category))
                return;

            string categoryLower = category.ToLower();

            // Determine which type of asset changed based on the path
            if (categoryLower == "map" && !string.IsNullOrEmpty(relativePath))
            {
                ProcessMapCategoryChange(relativePath, changeType);
            }
            else if (categoryLower == "mob")
            {
                ProcessLifeChange(LifeType.Mob, relativePath, changeType);
            }
            else if (categoryLower == "npc")
            {
                ProcessLifeChange(LifeType.Npc, relativePath, changeType);
            }
            else if (categoryLower == "reactor")
            {
                ProcessLifeChange(LifeType.Reactor, relativePath, changeType);
            }
            else if (categoryLower == "quest")
            {
                ProcessQuestChange(relativePath, changeType);
            }
            else if (categoryLower == "string")
            {
                ProcessStringChange(relativePath, changeType);
            }
        }

        private void ProcessMapCategoryChange(string relativePath, AssetChangeType changeType)
        {
            // Parse the relative path to determine if it's a Tile, Obj, or Back
            // Expected formats: "Tile/setName.img", "Obj/setName.img", "Back/setName.img"
            string normalizedPath = relativePath.Replace('\\', '/');
            string[] parts = normalizedPath.Split('/');

            if (parts.Length < 2)
                return;

            string subCategory = parts[0].ToLower();
            string fileName = parts[parts.Length - 1];
            string setName = Path.GetFileNameWithoutExtension(fileName);

            switch (subCategory)
            {
                case "tile":
                    // Update InfoManager
                    UpdateInfoManagerForTile(setName, changeType);
                    // Raise event
                    RaiseOnUIThread(() => TileSetChanged?.Invoke(this, new TileSetChangedEventArgs(changeType, setName, relativePath)));
                    break;

                case "obj":
                    UpdateInfoManagerForObject(setName, changeType);
                    RaiseOnUIThread(() => ObjectSetChanged?.Invoke(this, new ObjectSetChangedEventArgs(changeType, setName, relativePath)));
                    break;

                case "back":
                    UpdateInfoManagerForBackground(setName, changeType);
                    RaiseOnUIThread(() => BackgroundSetChanged?.Invoke(this, new BackgroundSetChangedEventArgs(changeType, setName, relativePath)));
                    break;
            }
        }

        private void ProcessLifeChange(LifeType lifeType, string relativePath, AssetChangeType changeType)
        {
            string entityId = null;
            if (!string.IsNullOrEmpty(relativePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(relativePath);
                entityId = fileName;
            }

            // Update InfoManager based on life type
            switch (lifeType)
            {
                case LifeType.Mob:
                    UpdateInfoManagerForMob(entityId, changeType);
                    break;
                case LifeType.Npc:
                    UpdateInfoManagerForNpc(entityId, changeType);
                    break;
                case LifeType.Reactor:
                    UpdateInfoManagerForReactor(entityId, changeType);
                    break;
            }

            RaiseOnUIThread(() => LifeDataChanged?.Invoke(this, new LifeDataChangedEventArgs(changeType, lifeType, entityId, relativePath)));
        }

        private void UpdateInfoManagerForMob(string mobId, AssetChangeType changeType)
        {
            if (string.IsNullOrEmpty(mobId))
                return;

            // Normalize mob ID - file names have leading zeros (0100100.img) but String.wz uses no leading zeros (100100)
            string normalizedId = NormalizeEntityId(mobId);

            switch (changeType)
            {
                case AssetChangeType.Added:
                case AssetChangeType.Modified:
                    // Look up name from String.wz via data source
                    string mobName = LookupMobNameFromString(normalizedId);
                    _infoManager.MobNameCache[normalizedId] = mobName ?? $"Mob {normalizedId}";
                    break;

                case AssetChangeType.Removed:
                    _infoManager.MobNameCache.Remove(normalizedId);
                    break;
            }
        }

        private void UpdateInfoManagerForNpc(string npcId, AssetChangeType changeType)
        {
            if (string.IsNullOrEmpty(npcId))
                return;

            // Normalize NPC ID - file names have leading zeros but String.wz uses no leading zeros
            string normalizedId = NormalizeEntityId(npcId);

            switch (changeType)
            {
                case AssetChangeType.Added:
                case AssetChangeType.Modified:
                    // Look up name from String.wz via data source
                    var npcInfo = LookupNpcNameFromString(normalizedId);
                    _infoManager.NpcNameCache[normalizedId] = npcInfo ?? Tuple.Create($"NPC {normalizedId}", string.Empty);
                    break;

                case AssetChangeType.Removed:
                    _infoManager.NpcNameCache.Remove(normalizedId);
                    break;
            }
        }

        private void UpdateInfoManagerForReactor(string reactorId, AssetChangeType changeType)
        {
            if (string.IsNullOrEmpty(reactorId))
                return;

            // Reactor IDs don't need normalization - they use the file name as-is
            string id = reactorId;

            switch (changeType)
            {
                case AssetChangeType.Added:
                case AssetChangeType.Modified:
                    // Load ReactorInfo from the .img file
                    var reactorInfo = LoadReactorFromDataSource(id);
                    if (reactorInfo != null)
                    {
                        _infoManager.Reactors[reactorInfo.ID] = reactorInfo;
                    }
                    break;

                case AssetChangeType.Removed:
                    _infoManager.Reactors.Remove(id);
                    break;
            }
        }

        private void ProcessQuestChange(string relativePath, AssetChangeType changeType)
        {
            string questDataType = "QuestInfo";
            if (!string.IsNullOrEmpty(relativePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(relativePath).ToLower();
                if (fileName.Contains("act"))
                    questDataType = "Act";
                else if (fileName.Contains("check"))
                    questDataType = "Check";
                else if (fileName.Contains("say"))
                    questDataType = "Say";
            }

            RaiseOnUIThread(() => QuestDataChanged?.Invoke(this, new QuestDataChangedEventArgs(changeType, questDataType, relativePath)));
        }

        private void ProcessStringChange(string relativePath, AssetChangeType changeType)
        {
            string stringDataType = "Unknown";
            if (!string.IsNullOrEmpty(relativePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(relativePath).ToLower();
                if (fileName.Contains("map"))
                    stringDataType = "Map";
                else if (fileName.Contains("mob"))
                    stringDataType = "Mob";
                else if (fileName.Contains("npc"))
                    stringDataType = "Npc";
                else if (fileName.Contains("item"))
                    stringDataType = "Item";
                else if (fileName.Contains("skill"))
                    stringDataType = "Skill";
            }

            RaiseOnUIThread(() => StringDataChanged?.Invoke(this, new StringDataChangedEventArgs(changeType, stringDataType, relativePath)));
        }
        #endregion

        #region InfoManager Updates
        private void UpdateInfoManagerForTile(string setName, AssetChangeType changeType)
        {
            switch (changeType)
            {
                case AssetChangeType.Added:
                    if (!_infoManager.TileSets.ContainsKey(setName))
                    {
                        _infoManager.TileSets[setName] = null; // Will be lazy-loaded
                    }
                    break;

                case AssetChangeType.Removed:
                    _infoManager.TileSets.Remove(setName);
                    break;

                case AssetChangeType.Modified:
                    // If set doesn't exist, treat as Added (Windows sometimes reports new files as Changed)
                    if (!_infoManager.TileSets.ContainsKey(setName))
                    {
                        _infoManager.TileSets[setName] = null;
                    }
                    else
                    {
                        _infoManager.TileSets[setName] = null; // Clear cached value, will reload on next access
                    }
                    break;
            }
        }

        private void UpdateInfoManagerForObject(string setName, AssetChangeType changeType)
        {
            switch (changeType)
            {
                case AssetChangeType.Added:
                    if (!_infoManager.ObjectSets.ContainsKey(setName))
                    {
                        _infoManager.ObjectSets[setName] = null;
                    }
                    break;

                case AssetChangeType.Removed:
                    _infoManager.ObjectSets.Remove(setName);
                    break;

                case AssetChangeType.Modified:
                    // If set doesn't exist, treat as Added (Windows sometimes reports new files as Changed)
                    if (!_infoManager.ObjectSets.ContainsKey(setName))
                    {
                        _infoManager.ObjectSets[setName] = null;
                    }
                    else
                    {
                        _infoManager.ObjectSets[setName] = null; // Clear cache for reload
                    }
                    break;
            }
        }

        private void UpdateInfoManagerForBackground(string setName, AssetChangeType changeType)
        {
            switch (changeType)
            {
                case AssetChangeType.Added:
                    if (!_infoManager.BackgroundSets.ContainsKey(setName))
                    {
                        _infoManager.BackgroundSets[setName] = null;
                    }
                    break;

                case AssetChangeType.Removed:
                    _infoManager.BackgroundSets.Remove(setName);
                    break;

                case AssetChangeType.Modified:
                    // If set doesn't exist, treat as Added (Windows sometimes reports new files as Changed)
                    if (!_infoManager.BackgroundSets.ContainsKey(setName))
                    {
                        _infoManager.BackgroundSets[setName] = null;
                    }
                    else
                    {
                        _infoManager.BackgroundSets[setName] = null; // Clear cache for reload
                    }
                    break;
            }
        }
        #endregion

        #region Helpers
        private void RaiseOnUIThread(Action action)
        {
            if (_uiContext != null)
            {
                _uiContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Normalizes an entity ID by removing leading zeros.
        /// File names use 7-char format with leading zeros (e.g., "0100100.img")
        /// but String.wz uses no leading zeros (e.g., "100100").
        /// </summary>
        /// <param name="entityId">The entity ID (possibly with leading zeros)</param>
        /// <returns>Normalized ID without leading zeros</returns>
        private static string NormalizeEntityId(string entityId)
        {
            if (string.IsNullOrEmpty(entityId))
                return entityId;

            // Remove leading zeros but keep at least one character
            string normalized = entityId.TrimStart('0');
            return string.IsNullOrEmpty(normalized) ? "0" : normalized;
        }

        /// <summary>
        /// Looks up a mob name from String/Mob.img via the data source
        /// </summary>
        /// <param name="mobId">The normalized mob ID</param>
        /// <returns>The mob name, or null if not found</returns>
        private string LookupMobNameFromString(string mobId)
        {
            if (_dataSource == null || string.IsNullOrEmpty(mobId))
                return null;

            try
            {
                var mobImg = _dataSource.GetImage("String", "Mob.img");
                if (mobImg != null)
                {
                    mobImg.ParseImage();
                    var mobProp = mobImg[mobId];
                    if (mobProp != null)
                    {
                        return (mobProp["name"] as WzStringProperty)?.Value;
                    }
                }
            }
            catch
            {
                // Ignore errors, return null to use placeholder
            }

            return null;
        }

        /// <summary>
        /// Looks up an NPC name and description from String/Npc.img via the data source
        /// </summary>
        /// <param name="npcId">The normalized NPC ID</param>
        /// <returns>Tuple of (name, description), or null if not found</returns>
        private Tuple<string, string> LookupNpcNameFromString(string npcId)
        {
            if (_dataSource == null || string.IsNullOrEmpty(npcId))
                return null;

            try
            {
                var npcImg = _dataSource.GetImage("String", "Npc.img");
                if (npcImg != null)
                {
                    npcImg.ParseImage();
                    var npcProp = npcImg[npcId];
                    if (npcProp != null)
                    {
                        string name = (npcProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                        string desc = (npcProp["func"] as WzStringProperty)?.Value ?? string.Empty;
                        return Tuple.Create(name, desc);
                    }
                }
            }
            catch
            {
                // Ignore errors, return null to use placeholder
            }

            return null;
        }

        /// <summary>
        /// Loads a ReactorInfo from the Reactor category via the data source
        /// </summary>
        /// <param name="reactorId">The reactor ID (file name without extension)</param>
        /// <returns>ReactorInfo, or null if not found</returns>
        private ReactorInfo LoadReactorFromDataSource(string reactorId)
        {
            if (_dataSource == null || string.IsNullOrEmpty(reactorId))
                return null;

            try
            {
                var reactorImage = _dataSource.GetImage("Reactor", $"{reactorId}.img");
                if (reactorImage != null)
                {
                    reactorImage.ParseImage();
                    WzSubProperty infoProp = reactorImage["info"] as WzSubProperty;

                    string name = "NO NAME";
                    if (infoProp != null)
                    {
                        name = (infoProp["info"] as WzStringProperty)?.Value ??
                               (infoProp["viewName"] as WzStringProperty)?.Value ?? string.Empty;
                    }

                    return new ReactorInfo(null, new System.Drawing.Point(), reactorId, name, reactorImage);
                }
            }
            catch
            {
                // Ignore errors, return null
            }

            return null;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isEnabled = false;

            if (_dataSource != null)
            {
                _dataSource.CategoryIndexChanged -= OnCategoryIndexChanged;
                _dataSource = null;
            }
        }
        #endregion
    }
}
