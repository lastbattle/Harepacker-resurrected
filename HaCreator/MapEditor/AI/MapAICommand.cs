/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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
        SetBgm          // Change background music
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
        All
    }
}
