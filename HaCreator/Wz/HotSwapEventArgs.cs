using System;

namespace HaCreator.Wz
{
    /// <summary>
    /// Specifies the type of asset change
    /// </summary>
    public enum AssetChangeType
    {
        /// <summary>
        /// A new asset was added
        /// </summary>
        Added,

        /// <summary>
        /// An asset was removed
        /// </summary>
        Removed,

        /// <summary>
        /// An asset was modified
        /// </summary>
        Modified
    }

    /// <summary>
    /// Specifies the type of life entity
    /// </summary>
    public enum LifeType
    {
        Mob,
        Npc,
        Reactor
    }

    /// <summary>
    /// Base class for asset change event args
    /// </summary>
    public abstract class AssetChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The type of change that occurred
        /// </summary>
        public AssetChangeType ChangeType { get; }

        /// <summary>
        /// The category that was affected (e.g., "Map", "Mob", "Npc")
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// The relative path of the file that changed
        /// </summary>
        public string RelativePath { get; }

        protected AssetChangedEventArgs(AssetChangeType changeType, string category, string relativePath)
        {
            ChangeType = changeType;
            Category = category;
            RelativePath = relativePath;
        }
    }

    /// <summary>
    /// Event args for tile set changes
    /// </summary>
    public class TileSetChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The name of the tile set that changed (without .img extension)
        /// </summary>
        public string SetName { get; }

        public TileSetChangedEventArgs(AssetChangeType changeType, string setName, string relativePath)
            : base(changeType, "Map", relativePath)
        {
            SetName = setName;
        }
    }

    /// <summary>
    /// Event args for object set changes
    /// </summary>
    public class ObjectSetChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The name of the object set that changed (without .img extension)
        /// </summary>
        public string SetName { get; }

        public ObjectSetChangedEventArgs(AssetChangeType changeType, string setName, string relativePath)
            : base(changeType, "Map", relativePath)
        {
            SetName = setName;
        }
    }

    /// <summary>
    /// Event args for background set changes
    /// </summary>
    public class BackgroundSetChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The name of the background set that changed (without .img extension)
        /// </summary>
        public string SetName { get; }

        public BackgroundSetChangedEventArgs(AssetChangeType changeType, string setName, string relativePath)
            : base(changeType, "Map", relativePath)
        {
            SetName = setName;
        }
    }

    /// <summary>
    /// Event args for life data changes (mob, npc, reactor)
    /// </summary>
    public class LifeDataChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The type of life entity that changed
        /// </summary>
        public LifeType LifeType { get; }

        /// <summary>
        /// The ID of the specific entity that changed, or null if multiple/all changed
        /// </summary>
        public string EntityId { get; }

        public LifeDataChangedEventArgs(AssetChangeType changeType, LifeType lifeType, string entityId, string relativePath)
            : base(changeType, lifeType == LifeType.Mob ? "Mob" : lifeType == LifeType.Npc ? "Npc" : "Reactor", relativePath)
        {
            LifeType = lifeType;
            EntityId = entityId;
        }
    }

    /// <summary>
    /// Event args for quest data changes
    /// </summary>
    public class QuestDataChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The type of quest data that changed (e.g., "Act", "Check", "Say", "QuestInfo")
        /// </summary>
        public string QuestDataType { get; }

        public QuestDataChangedEventArgs(AssetChangeType changeType, string questDataType, string relativePath)
            : base(changeType, "Quest", relativePath)
        {
            QuestDataType = questDataType;
        }
    }

    /// <summary>
    /// Event args for string data changes
    /// </summary>
    public class StringDataChangedEventArgs : AssetChangedEventArgs
    {
        /// <summary>
        /// The type of string data that changed (e.g., "Map", "Mob", "Npc", "Item")
        /// </summary>
        public string StringDataType { get; }

        public StringDataChangedEventArgs(AssetChangeType changeType, string stringDataType, string relativePath)
            : base(changeType, "String", relativePath)
        {
            StringDataType = stringDataType;
        }
    }
}
