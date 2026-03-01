using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor;

namespace HaCreator.Wz
{
    /// <summary>
    /// Information about an asset's usage
    /// </summary>
    public class AssetUsageInfo
    {
        /// <summary>
        /// The asset path (e.g., "Map/Tile/setName.img")
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// Weak references to objects using this asset
        /// </summary>
        public HashSet<WeakReference<object>> Users { get; } = new();

        /// <summary>
        /// Number of active users (cleans up dead references)
        /// </summary>
        public int ActiveUserCount
        {
            get
            {
                CleanupDeadReferences();
                return Users.Count;
            }
        }

        /// <summary>
        /// Removes dead weak references
        /// </summary>
        public void CleanupDeadReferences()
        {
            var deadRefs = Users.Where(wr => !wr.TryGetTarget(out _)).ToList();
            foreach (var dead in deadRefs)
            {
                Users.Remove(dead);
            }
        }

        /// <summary>
        /// Gets all active users
        /// </summary>
        public IEnumerable<object> GetActiveUsers()
        {
            var activeUsers = new List<object>();
            var deadRefs = new List<WeakReference<object>>();

            foreach (var weakRef in Users)
            {
                if (weakRef.TryGetTarget(out var target))
                {
                    activeUsers.Add(target);
                }
                else
                {
                    deadRefs.Add(weakRef);
                }
            }

            // Clean up dead references
            foreach (var dead in deadRefs)
            {
                Users.Remove(dead);
            }

            return activeUsers;
        }
    }

    /// <summary>
    /// Tracks which assets are currently in use by BoardItems and other components.
    /// Used to safely handle asset modifications when assets are actively being used.
    /// </summary>
    public class AssetUsageTracker
    {
        #region Fields
        private readonly ConcurrentDictionary<string, AssetUsageInfo> _assetUsage = new();
        private readonly object _lock = new();
        #endregion

        #region Public Methods
        /// <summary>
        /// Registers that an object is using a specific asset
        /// </summary>
        /// <param name="category">The asset category (e.g., "Map")</param>
        /// <param name="assetPath">The relative asset path (e.g., "Tile/setName.img")</param>
        /// <param name="user">The object using the asset</param>
        public void RegisterAssetInUse(string category, string assetPath, object user)
        {
            if (string.IsNullOrEmpty(assetPath) || user == null)
                return;

            string fullPath = NormalizePath(category, assetPath);

            var usageInfo = _assetUsage.GetOrAdd(fullPath, _ => new AssetUsageInfo { AssetPath = fullPath });

            lock (_lock)
            {
                // Check if user is already registered
                bool alreadyRegistered = usageInfo.Users.Any(wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, user));
                if (!alreadyRegistered)
                {
                    usageInfo.Users.Add(new WeakReference<object>(user));
                }
            }
        }

        /// <summary>
        /// Unregisters an object from using a specific asset
        /// </summary>
        /// <param name="category">The asset category</param>
        /// <param name="assetPath">The relative asset path</param>
        /// <param name="user">The object to unregister</param>
        public void UnregisterAssetInUse(string category, string assetPath, object user)
        {
            if (string.IsNullOrEmpty(assetPath) || user == null)
                return;

            string fullPath = NormalizePath(category, assetPath);

            if (_assetUsage.TryGetValue(fullPath, out var usageInfo))
            {
                lock (_lock)
                {
                    var toRemove = usageInfo.Users.FirstOrDefault(wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, user));
                    if (toRemove != null)
                    {
                        usageInfo.Users.Remove(toRemove);
                    }

                    // Clean up empty entries
                    if (usageInfo.Users.Count == 0)
                    {
                        _assetUsage.TryRemove(fullPath, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if an asset is currently in use
        /// </summary>
        /// <param name="category">The asset category</param>
        /// <param name="assetPath">The relative asset path</param>
        /// <returns>True if the asset has active users</returns>
        public bool IsAssetInUse(string category, string assetPath)
        {
            string fullPath = NormalizePath(category, assetPath);

            if (_assetUsage.TryGetValue(fullPath, out var usageInfo))
            {
                return usageInfo.ActiveUserCount > 0;
            }

            return false;
        }

        /// <summary>
        /// Gets all objects using a specific asset
        /// </summary>
        /// <param name="category">The asset category</param>
        /// <param name="assetPath">The relative asset path</param>
        /// <returns>List of objects using the asset</returns>
        public IEnumerable<object> GetUsersOfAsset(string category, string assetPath)
        {
            string fullPath = NormalizePath(category, assetPath);

            if (_assetUsage.TryGetValue(fullPath, out var usageInfo))
            {
                return usageInfo.GetActiveUsers();
            }

            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Gets the usage info for a specific asset
        /// </summary>
        /// <param name="category">The asset category</param>
        /// <param name="assetPath">The relative asset path</param>
        /// <returns>The usage info, or null if not tracked</returns>
        public AssetUsageInfo GetAssetUsageInfo(string category, string assetPath)
        {
            string fullPath = NormalizePath(category, assetPath);
            _assetUsage.TryGetValue(fullPath, out var usageInfo);
            return usageInfo;
        }

        /// <summary>
        /// Registers all assets used by a board
        /// </summary>
        /// <param name="board">The board to scan</param>
        public void RegisterBoardAssets(Board board)
        {
            if (board?.BoardItems?.AllItemLists == null)
                return;

            foreach (var itemList in board.BoardItems.AllItemLists)
            {
                foreach (var item in itemList)
                {
                    if (item is BoardItem boardItem)
                    {
                        RegisterBoardItemAsset(boardItem);
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters all assets used by a board
        /// </summary>
        /// <param name="board">The board to unregister</param>
        public void UnregisterBoardAssets(Board board)
        {
            if (board?.BoardItems?.AllItemLists == null)
                return;

            foreach (var itemList in board.BoardItems.AllItemLists)
            {
                foreach (var item in itemList)
                {
                    if (item is BoardItem boardItem)
                    {
                        UnregisterBoardItemAsset(boardItem);
                    }
                }
            }
        }

        /// <summary>
        /// Cleans up all dead references across all tracked assets
        /// </summary>
        public void CleanupAllDeadReferences()
        {
            var emptyPaths = new List<string>();

            foreach (var kvp in _assetUsage)
            {
                kvp.Value.CleanupDeadReferences();
                if (kvp.Value.Users.Count == 0)
                {
                    emptyPaths.Add(kvp.Key);
                }
            }

            foreach (var path in emptyPaths)
            {
                _assetUsage.TryRemove(path, out _);
            }
        }

        /// <summary>
        /// Gets statistics about tracked assets
        /// </summary>
        public (int TrackedAssets, int TotalUsers) GetStats()
        {
            int totalUsers = 0;
            foreach (var kvp in _assetUsage)
            {
                totalUsers += kvp.Value.ActiveUserCount;
            }
            return (_assetUsage.Count, totalUsers);
        }
        #endregion

        #region Private Methods
        private string NormalizePath(string category, string assetPath)
        {
            string path = assetPath.Replace('\\', '/').TrimStart('/');
            if (!string.IsNullOrEmpty(category))
            {
                return $"{category}/{path}".ToLowerInvariant();
            }
            return path.ToLowerInvariant();
        }

        private void RegisterBoardItemAsset(BoardItem item)
        {
            // Get asset path based on item type
            var (category, assetPath) = GetAssetPathForBoardItem(item);
            if (!string.IsNullOrEmpty(assetPath))
            {
                RegisterAssetInUse(category, assetPath, item);
            }
        }

        private void UnregisterBoardItemAsset(BoardItem item)
        {
            var (category, assetPath) = GetAssetPathForBoardItem(item);
            if (!string.IsNullOrEmpty(assetPath))
            {
                UnregisterAssetInUse(category, assetPath, item);
            }
        }

        private (string category, string assetPath) GetAssetPathForBoardItem(BoardItem item)
        {
            // Determine asset path based on item type
            // This would need to be expanded based on the actual BoardItem subclasses
            string typeName = item.GetType().Name;

            switch (typeName)
            {
                case "TileInstance":
                    // TileInstance has BaseInfo which contains tS (tile set)
                    var tileInfo = item.BaseInfo as MapEditor.Info.TileInfo;
                    if (tileInfo != null)
                    {
                        return ("Map", $"Tile/{tileInfo.tS}.img");
                    }
                    break;

                case "ObjectInstance":
                    var objInfo = item.BaseInfo as MapEditor.Info.ObjectInfo;
                    if (objInfo != null)
                    {
                        return ("Map", $"Obj/{objInfo.oS}.img");
                    }
                    break;

                case "BackgroundInstance":
                    var bgInfo = item.BaseInfo as MapEditor.Info.BackgroundInfo;
                    if (bgInfo != null)
                    {
                        return ("Map", $"Back/{bgInfo.bS}.img");
                    }
                    break;

                case "MobInstance":
                    var mobInfo = item.BaseInfo as MapEditor.Info.MobInfo;
                    if (mobInfo != null)
                    {
                        return ("Mob", $"{mobInfo.ID}.img");
                    }
                    break;

                case "NpcInstance":
                    var npcInfo = item.BaseInfo as MapEditor.Info.NpcInfo;
                    if (npcInfo != null)
                    {
                        return ("Npc", $"{npcInfo.ID}.img");
                    }
                    break;

                case "ReactorInstance":
                    var reactorInfo = item.BaseInfo as MapEditor.Info.ReactorInfo;
                    if (reactorInfo != null)
                    {
                        return ("Reactor", $"{reactorInfo.ID}.img");
                    }
                    break;
            }

            return (null, null);
        }
        #endregion
    }
}
