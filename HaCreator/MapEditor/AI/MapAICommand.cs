using System.Collections.Generic;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Represents a parsed AI command for map modification
    /// </summary>
    public class MapAICommand
    {
        public CommandType Type { get; set; }
        public ElementType ElementType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// For commands targeting existing elements by name/id
        /// </summary>
        public string TargetIdentifier { get; set; }

        /// <summary>
        /// For commands targeting elements at a specific location
        /// </summary>
        public int? TargetX { get; set; }
        public int? TargetY { get; set; }

        /// <summary>
        /// Original command text for logging/debugging
        /// </summary>
        public string OriginalText { get; set; }

        /// <summary>
        /// Whether the command was successfully parsed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if parsing failed
        /// </summary>
        public string ErrorMessage { get; set; }

        public override string ToString()
        {
            return $"{Type} {ElementType}: {OriginalText}";
        }
    }

    public enum CommandType
    {
        Unknown,
        Add,
        Remove,
        Move,
        Modify,
        Duplicate,
        Flip,
        SetProperty,
        Clear,
        Select,
        TilePlatform,   // Auto-tile a platform with proper spacing (simple flat)
        TileStructure,  // Build complex tile structures (tall, slopes, pillars, stairs)
        SetBgm,         // Change background music
        SetMapOption,   // Set boolean map options (town, swim, fly, etc.)
        SetFieldLimit,  // Set field limit restrictions
        SetMapSize,     // Set map dimensions
        SetVR,          // Set viewing range
        ClearVR,        // Clear viewing range

        // New map property commands
        SetReturnMap,       // Set returnMap and forcedReturn
        SetMobRate,         // Set monster spawn rate multiplier
        SetFieldType,       // Set field type (underwater, flying, etc.)
        SetTimeLimit,       // Set map time limit in seconds
        SetLevelLimit,      // Set level requirement
        SetScript,          // Set onUserEnter/onFirstUserEnter scripts
        SetEffect,          // Set visual effect name
        SetHelp,            // Set help text
        SetMapDesc,         // Set map description
        SetDropSettings,    // Set dropExpire and dropRate
        SetDecaySettings,   // Set decHP and decInterval
        SetRecovery,        // Set HP recovery rate
        SetTimeMob,         // Set time-based mob spawn

        // Minimap commands
        SetMinimapRect,     // Set minimap bounds
        ClearMinimapRect,   // Clear minimap bounds

        // Life/Spawn commands
        SetPatrolRange,     // Set rx0/rx1 for mob/NPC
        SetRespawnTime,     // Set MobTime for spawn
        SetTeam,            // Set team for mob/NPC (PvP maps)

        // Layer management
        CreateLayer,        // Create new layer
        DeleteLayer,        // Remove layer
        SetLayerTileset,    // Change layer tileset
        MoveToLayer,        // Move items between layers

        // Z-Order commands
        SetZ,               // Set explicit Z value
        BringToFront,       // Maximize Z in layer
        SendToBack,         // Minimize Z in layer

        // Miscellaneous
        Rename,             // Rename portal/item
        AddToolTip,         // Add area tooltip
        RemoveToolTip,      // Remove tooltip
        ModifyToolTip       // Modify tooltip text
    }

    public enum ElementType
    {
        Unknown,
        Mob,
        NPC,
        Portal,
        Object,
        Tile,
        Background,
        Foothold,
        Platform,  // Horizontal foothold chain
        Wall,      // Vertical foothold
        Rope,
        Ladder,
        Chair,
        Reactor,
        ToolTip,   // Area tooltip
        Layer,     // Map layer
        Map,       // Map-wide settings
        Life,      // Generic mob or NPC
        All
    }
}
