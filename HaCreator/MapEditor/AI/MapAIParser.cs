using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Parses AI-generated instructions for map modifications.
    /// Supports natural language-like commands for map editing.
    ///
    /// Example commands:
    /// - ADD MOB 100100 at (500, 200)
    /// - MOVE portal "sp" to (100, 50)
    /// - DELETE all mobs at layer 0
    /// - SET portal "town" target_map to 100000000
    /// - FLIP object at (300, 400)
    /// - ADD NPC 9000000 at (200, 100) facing left
    /// </summary>
    public class MapAIParser
    {
        // Regex patterns for parsing commands
        private static readonly Regex CoordinatePattern = new Regex(@"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", RegexOptions.Compiled);
        private static readonly Regex QuotedStringPattern = new Regex(@"""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex NumberPattern = new Regex(@"\b(\d+)\b", RegexOptions.Compiled);
        private static readonly Regex PropertyAssignmentPattern = new Regex(@"(\w+)\s*[=:]\s*(.+?)(?=\s+\w+\s*[=:]|$)", RegexOptions.Compiled);

        /// <summary>
        /// Parse a single command line
        /// </summary>
        public MapAICommand ParseCommand(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return new MapAICommand { IsValid = false, ErrorMessage = "Empty command" };

            var command = new MapAICommand
            {
                OriginalText = commandText.Trim()
            };

            try
            {
                // Skip comments
                if (commandText.TrimStart().StartsWith("#") || commandText.TrimStart().StartsWith("//"))
                {
                    command.IsValid = false;
                    command.ErrorMessage = "Comment line";
                    return command;
                }

                // Normalize the command text
                string normalized = commandText.Trim().ToUpperInvariant();

                // Parse command type
                command.Type = ParseCommandType(normalized);
                if (command.Type == CommandType.Unknown)
                {
                    command.IsValid = false;
                    command.ErrorMessage = "Unknown command type";
                    return command;
                }

                // Parse element type
                command.ElementType = ParseElementType(normalized);

                // Parse coordinates
                var coords = CoordinatePattern.Match(commandText);
                if (coords.Success)
                {
                    command.TargetX = int.Parse(coords.Groups[1].Value);
                    command.TargetY = int.Parse(coords.Groups[2].Value);
                }

                // Parse quoted strings (for names like portal names)
                var quotedMatches = QuotedStringPattern.Matches(commandText);
                if (quotedMatches.Count > 0)
                {
                    command.TargetIdentifier = quotedMatches[0].Groups[1].Value;
                    command.Parameters["name"] = command.TargetIdentifier;

                    // If there's a second quoted string, it might be a target
                    if (quotedMatches.Count > 1)
                    {
                        command.Parameters["target_name"] = quotedMatches[1].Groups[1].Value;
                    }
                }

                // Parse element ID (for mobs, npcs, etc.)
                if (command.ElementType == ElementType.Mob ||
                    command.ElementType == ElementType.NPC ||
                    command.ElementType == ElementType.Reactor)
                {
                    ParseElementId(commandText, command);
                }

                // Parse property assignments
                ParseProperties(commandText, command);

                // Parse direction/flip
                if (normalized.Contains("FLIP") ||
                    normalized.Contains("FACING LEFT") ||
                    normalized.Contains("FLIPPED"))
                {
                    command.Parameters["flip"] = true;
                }
                else if (normalized.Contains("FACING RIGHT") || normalized.Contains("NOT FLIPPED"))
                {
                    command.Parameters["flip"] = false;
                }

                // Parse layer
                var layerMatch = Regex.Match(normalized, @"LAYER\s*[=:]?\s*(\d+)");
                if (layerMatch.Success)
                {
                    command.Parameters["layer"] = int.Parse(layerMatch.Groups[1].Value);
                }

                // Parse Z-order
                var zMatch = Regex.Match(normalized, @"Z\s*[=:]?\s*(-?\d+)");
                if (zMatch.Success)
                {
                    command.Parameters["z"] = int.Parse(zMatch.Groups[1].Value);
                }

                // Parse portal-specific properties
                if (command.ElementType == ElementType.Portal)
                {
                    ParsePortalProperties(commandText, command);
                }

                // Parse background-specific properties
                if (command.ElementType == ElementType.Background)
                {
                    ParseBackgroundProperties(commandText, command);
                }

                // Parse object-specific properties
                if (command.ElementType == ElementType.Object)
                {
                    ParseObjectProperties(commandText, command);
                }

                // Parse platform-specific properties (array of points)
                if (command.ElementType == ElementType.Platform)
                {
                    ParsePlatformProperties(commandText, command);
                }

                // Parse wall-specific properties
                if (command.ElementType == ElementType.Wall)
                {
                    ParseWallProperties(commandText, command);
                }

                // Parse rope/ladder-specific properties
                if (command.ElementType == ElementType.Rope || command.ElementType == ElementType.Ladder)
                {
                    ParseRopeProperties(commandText, command);
                }

                // Parse foothold flags
                if (command.ElementType == ElementType.Platform ||
                    command.ElementType == ElementType.Wall ||
                    command.ElementType == ElementType.Foothold)
                {
                    ParseFootholdFlags(commandText, command);
                }

                // Parse tile-specific properties
                if (command.ElementType == ElementType.Tile)
                {
                    ParseTileProperties(commandText, command);
                }

                // Parse tile platform properties
                if (command.Type == CommandType.TilePlatform)
                {
                    ParseTilePlatformProperties(commandText, command);
                }

                // Parse tile structure properties
                if (command.Type == CommandType.TileStructure)
                {
                    ParseTileStructureProperties(commandText, command);
                }

                // Parse BGM properties
                if (command.Type == CommandType.SetBgm)
                {
                    ParseBgmProperties(commandText, command);
                }

                // Parse map option properties
                if (command.Type == CommandType.SetMapOption)
                {
                    ParseMapOptionProperties(commandText, command);
                }

                // Parse field limit properties
                if (command.Type == CommandType.SetFieldLimit)
                {
                    ParseFieldLimitProperties(commandText, command);
                }

                // Parse map size properties
                if (command.Type == CommandType.SetMapSize)
                {
                    ParseMapSizeProperties(commandText, command);
                }

                // Parse VR properties
                if (command.Type == CommandType.SetVR)
                {
                    ParseVRProperties(commandText, command);
                }

                // Parse new map property commands
                if (command.Type == CommandType.SetReturnMap)
                {
                    ParseReturnMapProperties(commandText, command);
                }
                if (command.Type == CommandType.SetMobRate)
                {
                    ParseMobRateProperties(commandText, command);
                }
                if (command.Type == CommandType.SetFieldType)
                {
                    ParseFieldTypeProperties(commandText, command);
                }
                if (command.Type == CommandType.SetTimeLimit)
                {
                    ParseTimeLimitProperties(commandText, command);
                }
                if (command.Type == CommandType.SetLevelLimit)
                {
                    ParseLevelLimitProperties(commandText, command);
                }
                if (command.Type == CommandType.SetScript)
                {
                    ParseScriptProperties(commandText, command);
                }
                if (command.Type == CommandType.SetEffect)
                {
                    ParseEffectProperties(commandText, command);
                }
                if (command.Type == CommandType.SetHelp)
                {
                    ParseHelpProperties(commandText, command);
                }
                if (command.Type == CommandType.SetMapDesc)
                {
                    ParseMapDescProperties(commandText, command);
                }
                if (command.Type == CommandType.SetDropSettings)
                {
                    ParseDropProperties(commandText, command);
                }
                if (command.Type == CommandType.SetDecaySettings)
                {
                    ParseDecayProperties(commandText, command);
                }
                if (command.Type == CommandType.SetRecovery)
                {
                    ParseRecoveryProperties(commandText, command);
                }
                if (command.Type == CommandType.SetMinimapRect)
                {
                    ParseMinimapRectProperties(commandText, command);
                }
                if (command.Type == CommandType.SetPatrolRange)
                {
                    ParsePatrolRangeProperties(commandText, command);
                }
                if (command.Type == CommandType.SetRespawnTime)
                {
                    ParseRespawnTimeProperties(commandText, command);
                }
                if (command.Type == CommandType.SetTeam)
                {
                    ParseTeamProperties(commandText, command);
                }
                if (command.Type == CommandType.SetLayerTileset)
                {
                    ParseLayerTilesetProperties(commandText, command);
                }
                if (command.Type == CommandType.SetZ)
                {
                    ParseSetZProperties(commandText, command);
                }
                if (command.Type == CommandType.Rename)
                {
                    ParseRenameProperties(commandText, command);
                }

                // Parse tooltip properties
                if (command.Type == CommandType.AddToolTip ||
                    command.Type == CommandType.ModifyToolTip ||
                    command.Type == CommandType.RemoveToolTip)
                {
                    ParseToolTipProperties(commandText, command);
                }

                command.IsValid = true;
            }
            catch (Exception ex)
            {
                command.IsValid = false;
                command.ErrorMessage = $"Parse error: {ex.Message}";
            }

            return command;
        }

        /// <summary>
        /// Parse multiple command lines
        /// </summary>
        public List<MapAICommand> ParseCommands(string multiLineText)
        {
            var commands = new List<MapAICommand>();
            var lines = multiLineText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                // Skip markdown headers and comments
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//") || trimmed.StartsWith("-"))
                    continue;

                var command = ParseCommand(trimmed);
                if (command.IsValid)
                {
                    commands.Add(command);
                }
            }

            return commands;
        }

        private CommandType ParseCommandType(string normalized)
        {
            if (normalized.StartsWith("TILE STRUCTURE"))
                return CommandType.TileStructure;
            if (normalized.StartsWith("TILE PLATFORM"))
                return CommandType.TilePlatform;

            // ToolTip commands (check before generic ADD/MODIFY)
            if (normalized.StartsWith("ADD TOOLTIP") || normalized.StartsWith("CREATE TOOLTIP"))
                return CommandType.AddToolTip;
            if (normalized.StartsWith("REMOVE TOOLTIP") || normalized.StartsWith("DELETE TOOLTIP"))
                return CommandType.RemoveToolTip;
            if (normalized.StartsWith("MODIFY TOOLTIP") || normalized.StartsWith("CHANGE TOOLTIP") || normalized.StartsWith("SET TOOLTIP"))
                return CommandType.ModifyToolTip;

            if (normalized.StartsWith("ADD") || normalized.StartsWith("CREATE") || normalized.StartsWith("PLACE"))
                return CommandType.Add;
            if (normalized.StartsWith("REMOVE") || normalized.StartsWith("DELETE"))
                return CommandType.Remove;
            if (normalized.StartsWith("MOVE"))
                return CommandType.Move;
            if (normalized.StartsWith("MODIFY") || normalized.StartsWith("CHANGE") || normalized.StartsWith("UPDATE"))
                return CommandType.Modify;
            if (normalized.StartsWith("DUPLICATE") || normalized.StartsWith("COPY"))
                return CommandType.Duplicate;
            if (normalized.StartsWith("FLIP"))
                return CommandType.Flip;
            if (normalized.StartsWith("SET BGM"))
                return CommandType.SetBgm;
            if (normalized.StartsWith("SET MAP_OPTION"))
                return CommandType.SetMapOption;
            if (normalized.StartsWith("SET FIELD_LIMIT"))
                return CommandType.SetFieldLimit;
            if (normalized.StartsWith("SET MAP_SIZE"))
                return CommandType.SetMapSize;
            if (normalized.StartsWith("SET VR"))
                return CommandType.SetVR;
            if (normalized.StartsWith("CLEAR VR"))
                return CommandType.ClearVR;

            // New map property commands
            if (normalized.StartsWith("SET RETURN_MAP") || normalized.StartsWith("SET RETURNMAP"))
                return CommandType.SetReturnMap;
            if (normalized.StartsWith("SET MOB_RATE") || normalized.StartsWith("SET MOBRATE"))
                return CommandType.SetMobRate;
            if (normalized.StartsWith("SET FIELD_TYPE") || normalized.StartsWith("SET FIELDTYPE"))
                return CommandType.SetFieldType;
            if (normalized.StartsWith("SET TIME_LIMIT") || normalized.StartsWith("SET TIMELIMIT"))
                return CommandType.SetTimeLimit;
            if (normalized.StartsWith("SET LEVEL_LIMIT") || normalized.StartsWith("SET LEVELLIMIT"))
                return CommandType.SetLevelLimit;
            if (normalized.StartsWith("SET SCRIPT"))
                return CommandType.SetScript;
            if (normalized.StartsWith("SET EFFECT"))
                return CommandType.SetEffect;
            if (normalized.StartsWith("SET HELP"))
                return CommandType.SetHelp;
            if (normalized.StartsWith("SET MAP_DESC") || normalized.StartsWith("SET MAPDESC") || normalized.StartsWith("SET DESCRIPTION"))
                return CommandType.SetMapDesc;
            if (normalized.StartsWith("SET DROP"))
                return CommandType.SetDropSettings;
            if (normalized.StartsWith("SET DECAY"))
                return CommandType.SetDecaySettings;
            if (normalized.StartsWith("SET RECOVERY"))
                return CommandType.SetRecovery;

            // Minimap commands
            if (normalized.StartsWith("SET MINIMAP"))
                return CommandType.SetMinimapRect;
            if (normalized.StartsWith("CLEAR MINIMAP"))
                return CommandType.ClearMinimapRect;

            // Life/Spawn commands
            if (normalized.StartsWith("SET PATROL"))
                return CommandType.SetPatrolRange;
            if (normalized.StartsWith("SET RESPAWN"))
                return CommandType.SetRespawnTime;
            if (normalized.StartsWith("SET TEAM"))
                return CommandType.SetTeam;

            // Layer management
            if (normalized.StartsWith("SET LAYER_TILESET") || normalized.StartsWith("SET TILESET"))
                return CommandType.SetLayerTileset;

            // Z-Order commands
            if (normalized.StartsWith("SET Z"))
                return CommandType.SetZ;

            // Rename
            if (normalized.StartsWith("RENAME"))
                return CommandType.Rename;

            if (normalized.StartsWith("SET"))
                return CommandType.SetProperty;
            if (normalized.StartsWith("CLEAR"))
                return CommandType.Clear;
            if (normalized.StartsWith("SELECT"))
                return CommandType.Select;

            return CommandType.Unknown;
        }

        private ElementType ParseElementType(string normalized)
        {
            if (normalized.Contains("MOB") || normalized.Contains("MONSTER"))
                return ElementType.Mob;
            if (normalized.Contains("NPC"))
                return ElementType.NPC;
            if (normalized.Contains("PORTAL"))
                return ElementType.Portal;
            if (normalized.Contains("OBJECT") || normalized.Contains("OBJ"))
                return ElementType.Object;
            if (normalized.Contains("TILE"))
                return ElementType.Tile;
            if (normalized.Contains("BACKGROUND") || normalized.Contains("BG"))
                return ElementType.Background;
            if (normalized.Contains("WALL"))
                return ElementType.Wall;
            if (normalized.Contains("PLATFORM"))
                return ElementType.Platform;
            if (normalized.Contains("FOOTHOLD"))
                return ElementType.Foothold;
            if (normalized.Contains("LADDER"))
                return ElementType.Ladder;
            if (normalized.Contains("ROPE"))
                return ElementType.Rope;
            if (normalized.Contains("CHAIR"))
                return ElementType.Chair;
            if (normalized.Contains("REACTOR"))
                return ElementType.Reactor;
            if (normalized.Contains("ALL") || normalized.Contains("EVERYTHING"))
                return ElementType.All;

            return ElementType.Unknown;
        }

        private void ParseElementId(string commandText, MapAICommand command)
        {
            // Look for ID pattern like "MOB 100100" or "NPC ID 9000000"
            var idMatch = Regex.Match(commandText, @"(?:MOB|NPC|REACTOR)\s+(?:ID\s+)?(\d{5,})", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                command.Parameters["id"] = idMatch.Groups[1].Value;
            }
        }

        private void ParseProperties(string commandText, MapAICommand command)
        {
            // Parse properties in format: property=value or property:value
            var props = PropertyAssignmentPattern.Matches(commandText);
            foreach (Match prop in props)
            {
                var key = prop.Groups[1].Value.ToLowerInvariant();
                var value = prop.Groups[2].Value.Trim().Trim('"');

                // Try to convert to appropriate type
                if (int.TryParse(value, out int intVal))
                {
                    command.Parameters[key] = intVal;
                }
                else if (bool.TryParse(value, out bool boolVal))
                {
                    command.Parameters[key] = boolVal;
                }
                else if (float.TryParse(value, out float floatVal))
                {
                    command.Parameters[key] = floatVal;
                }
                else
                {
                    command.Parameters[key] = value;
                }
            }
        }

        private void ParsePortalProperties(string commandText, MapAICommand command)
        {
            // Portal type
            var typeMatch = Regex.Match(commandText, @"TYPE\s*[=:]?\s*(\w+)", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                command.Parameters["portal_type"] = typeMatch.Groups[1].Value;
            }

            // Target map
            var targetMapMatch = Regex.Match(commandText, @"TARGET_?MAP\s*[=:]?\s*(\d+)", RegexOptions.IgnoreCase);
            if (targetMapMatch.Success)
            {
                command.Parameters["target_map"] = int.Parse(targetMapMatch.Groups[1].Value);
            }

            // Script
            var scriptMatch = Regex.Match(commandText, @"SCRIPT\s*[=:]?\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (scriptMatch.Success)
            {
                command.Parameters["script"] = scriptMatch.Groups[1].Value;
            }
        }

        private void ParseBackgroundProperties(string commandText, MapAICommand command)
        {
            // Parse bS (background set): bS="name"
            var bsMatch = Regex.Match(commandText, @"BS\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (bsMatch.Success)
            {
                command.Parameters["bS"] = bsMatch.Groups[1].Value;
            }

            // Parse no (background number): no="N"
            var noMatch = Regex.Match(commandText, @"NO\s*[=:]\s*""?(\d+)""?", RegexOptions.IgnoreCase);
            if (noMatch.Success)
            {
                command.Parameters["no"] = noMatch.Groups[1].Value;
            }

            // Parse type: type="back|ani|spine"
            var typeMatch = Regex.Match(commandText, @"TYPE\s*[=:]\s*""?(\w+)""?", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                command.Parameters["bg_type"] = typeMatch.Groups[1].Value;
            }

            // Parallax
            var rxMatch = Regex.Match(commandText, @"RX\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (rxMatch.Success)
            {
                command.Parameters["rx"] = int.Parse(rxMatch.Groups[1].Value);
            }

            var ryMatch = Regex.Match(commandText, @"RY\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (ryMatch.Success)
            {
                command.Parameters["ry"] = int.Parse(ryMatch.Groups[1].Value);
            }

            // Tiling
            var cxMatch = Regex.Match(commandText, @"CX\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (cxMatch.Success)
            {
                command.Parameters["cx"] = int.Parse(cxMatch.Groups[1].Value);
            }

            var cyMatch = Regex.Match(commandText, @"CY\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (cyMatch.Success)
            {
                command.Parameters["cy"] = int.Parse(cyMatch.Groups[1].Value);
            }

            // Alpha (a=N or alpha=N)
            var alphaMatch = Regex.Match(commandText, @"(?:ALPHA|(?<!\w)A)\s*[=:]?\s*(\d+)", RegexOptions.IgnoreCase);
            if (alphaMatch.Success)
            {
                command.Parameters["a"] = int.Parse(alphaMatch.Groups[1].Value);
            }

            // Front/back
            if (Regex.IsMatch(commandText, @"\bFRONT\b", RegexOptions.IgnoreCase))
            {
                command.Parameters["front"] = true;
            }
        }

        private void ParseObjectProperties(string commandText, MapAICommand command)
        {
            // Parse oS (object set): oS="name"
            var osMatch = Regex.Match(commandText, @"OS\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (osMatch.Success)
            {
                command.Parameters["oS"] = osMatch.Groups[1].Value;
            }

            // Parse l0: l0="name"
            var l0Match = Regex.Match(commandText, @"L0\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (l0Match.Success)
            {
                command.Parameters["l0"] = l0Match.Groups[1].Value;
            }

            // Parse l1: l1="name"
            var l1Match = Regex.Match(commandText, @"L1\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (l1Match.Success)
            {
                command.Parameters["l1"] = l1Match.Groups[1].Value;
            }

            // Parse l2: l2="N"
            var l2Match = Regex.Match(commandText, @"L2\s*[=:]\s*""?(\w+)""?", RegexOptions.IgnoreCase);
            if (l2Match.Success)
            {
                command.Parameters["l2"] = l2Match.Groups[1].Value;
            }

            // Parse raw_position flag (disable ground-snapping)
            if (Regex.IsMatch(commandText, @"\bRAW_POSITION\b", RegexOptions.IgnoreCase))
            {
                command.Parameters["raw_position"] = true;
            }
        }

        private void ParsePlatformProperties(string commandText, MapAICommand command)
        {
            // Parse platform points in format: [(x1, y1), (x2, y2), ...]
            // Or multiple coordinate pairs
            var allCoords = CoordinatePattern.Matches(commandText);
            if (allCoords.Count >= 2)
            {
                var points = new List<(int x, int y)>();
                foreach (Match coord in allCoords)
                {
                    int x = int.Parse(coord.Groups[1].Value);
                    int y = int.Parse(coord.Groups[2].Value);
                    points.Add((x, y));
                }
                command.Parameters["points"] = points;
            }
        }

        private void ParseWallProperties(string commandText, MapAICommand command)
        {
            // Parse wall: x=N from y=N to y=N
            // Or: at x=N from y=N to y=N
            var xMatch = Regex.Match(commandText, @"(?:AT\s+)?X\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (xMatch.Success)
            {
                command.Parameters["wall_x"] = int.Parse(xMatch.Groups[1].Value);
            }

            // Parse Y range: "from y=N to y=N" or "y=N to y=N"
            var yRangeMatch = Regex.Match(commandText, @"(?:FROM\s+)?Y\s*[=:]?\s*(-?\d+)\s+TO\s+Y\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (yRangeMatch.Success)
            {
                int y1 = int.Parse(yRangeMatch.Groups[1].Value);
                int y2 = int.Parse(yRangeMatch.Groups[2].Value);
                command.Parameters["top_y"] = Math.Min(y1, y2);
                command.Parameters["bottom_y"] = Math.Max(y1, y2);
            }
        }

        private void ParseRopeProperties(string commandText, MapAICommand command)
        {
            // Parse rope/ladder: x=N from y=N to y=N
            // Or: at x=N from y=N to y=N
            var xMatch = Regex.Match(commandText, @"(?:AT\s+)?X\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (xMatch.Success)
            {
                command.Parameters["rope_x"] = int.Parse(xMatch.Groups[1].Value);
            }

            // Parse Y range: "from y=N to y=N" or "y=N to y=N"
            var yRangeMatch = Regex.Match(commandText, @"(?:FROM\s+)?Y\s*[=:]?\s*(-?\d+)\s+TO\s+Y\s*[=:]?\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (yRangeMatch.Success)
            {
                int y1 = int.Parse(yRangeMatch.Groups[1].Value);
                int y2 = int.Parse(yRangeMatch.Groups[2].Value);
                command.Parameters["top_y"] = Math.Min(y1, y2);
                command.Parameters["bottom_y"] = Math.Max(y1, y2);
            }

            // Parse uf (upper foothold): uf=true/false
            var ufMatch = Regex.Match(commandText, @"UF\s*[=:]?\s*(TRUE|FALSE)", RegexOptions.IgnoreCase);
            if (ufMatch.Success)
            {
                command.Parameters["uf"] = ufMatch.Groups[1].Value.ToUpperInvariant() == "TRUE";
            }
            else
            {
                // Default to true if not specified
                command.Parameters["uf"] = true;
            }
        }

        private void ParseFootholdFlags(string commandText, MapAICommand command)
        {
            var normalized = commandText.ToUpperInvariant();

            // forbid_fall_down - players cannot press down to fall through
            if (normalized.Contains("FORBID_FALL_DOWN") ||
                normalized.Contains("NO_FALL") ||
                normalized.Contains("SOLID"))
            {
                command.Parameters["forbid_fall_down"] = true;
            }

            // cant_through - players cannot jump through from below
            if (normalized.Contains("CANT_THROUGH") ||
                normalized.Contains("NO_JUMP_THROUGH") ||
                normalized.Contains("BLOCK_JUMP"))
            {
                command.Parameters["cant_through"] = true;
            }
        }

        private void ParseTileProperties(string commandText, MapAICommand command)
        {
            // Parse tileset: tileset="name" or tS="name"
            var tilesetMatch = Regex.Match(commandText, @"(?:TILESET|TS)\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (tilesetMatch.Success)
            {
                command.Parameters["tileset"] = tilesetMatch.Groups[1].Value;
            }

            // Parse category: category="name" or u="name"
            var categoryMatch = Regex.Match(commandText, @"(?:CATEGORY|U)\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (categoryMatch.Success)
            {
                command.Parameters["category"] = categoryMatch.Groups[1].Value;
            }

            // Parse tile number: no="0" or tile_no="0"
            var noMatch = Regex.Match(commandText, @"(?:NO|TILE_NO)\s*[=:]\s*""?(\w+)""?", RegexOptions.IgnoreCase);
            if (noMatch.Success)
            {
                command.Parameters["tile_no"] = noMatch.Groups[1].Value;
            }
            else
            {
                // Default to "0" if not specified
                command.Parameters["tile_no"] = "0";
            }
        }

        private void ParseTilePlatformProperties(string commandText, MapAICommand command)
        {
            // Parse tileset: tileset="name"
            var tilesetMatch = Regex.Match(commandText, @"TILESET\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (tilesetMatch.Success)
            {
                command.Parameters["tileset"] = tilesetMatch.Groups[1].Value;
            }

            // Parse start_x: from x=N
            var startXMatch = Regex.Match(commandText, @"FROM\s+X\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (startXMatch.Success)
            {
                command.Parameters["start_x"] = int.Parse(startXMatch.Groups[1].Value);
            }

            // Parse end_x: to x=N
            var endXMatch = Regex.Match(commandText, @"TO\s+X\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (endXMatch.Success)
            {
                command.Parameters["end_x"] = int.Parse(endXMatch.Groups[1].Value);
            }

            // Parse y: at y=N
            var yMatch = Regex.Match(commandText, @"(?:AT\s+)?Y\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (yMatch.Success)
            {
                command.Parameters["y"] = int.Parse(yMatch.Groups[1].Value);
            }
        }

        private void ParseTileStructureProperties(string commandText, MapAICommand command)
        {
            // Parse tileset: tileset="name"
            var tilesetMatch = Regex.Match(commandText, @"TILESET\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (tilesetMatch.Success)
            {
                command.Parameters["tileset"] = tilesetMatch.Groups[1].Value;
            }

            // Parse structure type: type="name"
            var typeMatch = Regex.Match(commandText, @"TYPE\s*[=:]\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                command.Parameters["structure_type"] = typeMatch.Groups[1].Value;
            }

            // Parse start_x: from x=N
            var startXMatch = Regex.Match(commandText, @"FROM\s+X\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (startXMatch.Success)
            {
                command.Parameters["start_x"] = int.Parse(startXMatch.Groups[1].Value);
            }

            // Parse end_x: to x=N
            var endXMatch = Regex.Match(commandText, @"TO\s+X\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (endXMatch.Success)
            {
                command.Parameters["end_x"] = int.Parse(endXMatch.Groups[1].Value);
            }

            // Parse y: at y=N
            var yMatch = Regex.Match(commandText, @"(?:AT\s+)?Y\s*[=:]\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (yMatch.Success)
            {
                command.Parameters["y"] = int.Parse(yMatch.Groups[1].Value);
            }

            // Parse height: height=N
            var heightMatch = Regex.Match(commandText, @"HEIGHT\s*[=:]\s*(\d+)", RegexOptions.IgnoreCase);
            if (heightMatch.Success)
            {
                command.Parameters["height"] = int.Parse(heightMatch.Groups[1].Value);
            }

            // Parse width: width=N
            var widthMatch = Regex.Match(commandText, @"WIDTH\s*[=:]\s*(\d+)", RegexOptions.IgnoreCase);
            if (widthMatch.Success)
            {
                command.Parameters["width"] = int.Parse(widthMatch.Groups[1].Value);
            }
        }

        private void ParseBgmProperties(string commandText, MapAICommand command)
        {
            // Parse BGM path: "Bgm00/GoPicnic" or BGM="path"
            var bgmMatch = Regex.Match(commandText, @"(?:BGM\s*[=:]\s*)?""([^""]+)""", RegexOptions.IgnoreCase);
            if (bgmMatch.Success)
            {
                command.Parameters["bgm"] = bgmMatch.Groups[1].Value;
            }
        }

        private void ParseMapOptionProperties(string commandText, MapAICommand command)
        {
            // Parse: SET MAP_OPTION option_name=true/false
            var match = Regex.Match(commandText, @"SET\s+MAP_OPTION\s+(\w+)\s*=\s*(true|false)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                command.Parameters["option"] = match.Groups[1].Value;
                command.Parameters["value"] = bool.Parse(match.Groups[2].Value.ToLower());
            }
        }

        private void ParseFieldLimitProperties(string commandText, MapAICommand command)
        {
            // Parse: SET FIELD_LIMIT limit_name=true/false
            var match = Regex.Match(commandText, @"SET\s+FIELD_LIMIT\s+(\w+)\s*=\s*(true|false)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                command.Parameters["limit"] = match.Groups[1].Value;
                command.Parameters["enabled"] = bool.Parse(match.Groups[2].Value.ToLower());
            }
        }

        private void ParseMapSizeProperties(string commandText, MapAICommand command)
        {
            // Parse: SET MAP_SIZE width=N height=N
            var widthMatch = Regex.Match(commandText, @"WIDTH\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (widthMatch.Success)
            {
                command.Parameters["width"] = int.Parse(widthMatch.Groups[1].Value);
            }

            var heightMatch = Regex.Match(commandText, @"HEIGHT\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (heightMatch.Success)
            {
                command.Parameters["height"] = int.Parse(heightMatch.Groups[1].Value);
            }
        }

        private void ParseVRProperties(string commandText, MapAICommand command)
        {
            // Parse: SET VR left=N top=N right=N bottom=N
            var leftMatch = Regex.Match(commandText, @"LEFT\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (leftMatch.Success)
            {
                command.Parameters["left"] = int.Parse(leftMatch.Groups[1].Value);
            }

            var topMatch = Regex.Match(commandText, @"TOP\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (topMatch.Success)
            {
                command.Parameters["top"] = int.Parse(topMatch.Groups[1].Value);
            }

            var rightMatch = Regex.Match(commandText, @"RIGHT\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (rightMatch.Success)
            {
                command.Parameters["right"] = int.Parse(rightMatch.Groups[1].Value);
            }

            var bottomMatch = Regex.Match(commandText, @"BOTTOM\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (bottomMatch.Success)
            {
                command.Parameters["bottom"] = int.Parse(bottomMatch.Groups[1].Value);
            }
        }

        #region New Command Property Parsers

        private void ParseReturnMapProperties(string commandText, MapAICommand command)
        {
            // Parse: SET RETURN_MAP return=N forced=N
            var returnMatch = Regex.Match(commandText, @"RETURN\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (returnMatch.Success)
                command.Parameters["return"] = int.Parse(returnMatch.Groups[1].Value);

            var forcedMatch = Regex.Match(commandText, @"FORCED\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (forcedMatch.Success)
                command.Parameters["forced"] = int.Parse(forcedMatch.Groups[1].Value);

            // Also support: SET RETURN_MAP 100000000 (single value sets both)
            if (!returnMatch.Success && !forcedMatch.Success)
            {
                var singleMatch = Regex.Match(commandText, @"RETURN_?MAP\s+(\d+)", RegexOptions.IgnoreCase);
                if (singleMatch.Success)
                {
                    int mapId = int.Parse(singleMatch.Groups[1].Value);
                    command.Parameters["return"] = mapId;
                    command.Parameters["forced"] = mapId;
                }
            }
        }

        private void ParseMobRateProperties(string commandText, MapAICommand command)
        {
            // Parse: SET MOB_RATE rate=N or SET MOB_RATE N
            var rateMatch = Regex.Match(commandText, @"RATE\s*=\s*([\d.]+)", RegexOptions.IgnoreCase);
            if (rateMatch.Success)
            {
                command.Parameters["rate"] = float.Parse(rateMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"MOB_?RATE\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["rate"] = float.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseFieldTypeProperties(string commandText, MapAICommand command)
        {
            // Parse: SET FIELD_TYPE type=Name or SET FIELD_TYPE Name
            var typeMatch = Regex.Match(commandText, @"TYPE\s*=\s*(\w+)", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                command.Parameters["type"] = typeMatch.Groups[1].Value;
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"FIELD_?TYPE\s+(\w+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["type"] = valueMatch.Groups[1].Value;
            }
        }

        private void ParseTimeLimitProperties(string commandText, MapAICommand command)
        {
            // Parse: SET TIME_LIMIT seconds=N or SET TIME_LIMIT N
            var secondsMatch = Regex.Match(commandText, @"SECONDS\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (secondsMatch.Success)
            {
                command.Parameters["seconds"] = int.Parse(secondsMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"TIME_?LIMIT\s+(\d+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["seconds"] = int.Parse(valueMatch.Groups[1].Value);
            }

            if (commandText.ToUpperInvariant().Contains("CLEAR"))
                command.Parameters["clear"] = true;
        }

        private void ParseLevelLimitProperties(string commandText, MapAICommand command)
        {
            // Parse: SET LEVEL_LIMIT min=N force=N
            var minMatch = Regex.Match(commandText, @"MIN\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (minMatch.Success)
                command.Parameters["min"] = int.Parse(minMatch.Groups[1].Value);

            var forceMatch = Regex.Match(commandText, @"FORCE\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (forceMatch.Success)
                command.Parameters["force"] = int.Parse(forceMatch.Groups[1].Value);

            // Single value sets min
            if (!minMatch.Success)
            {
                var valueMatch = Regex.Match(commandText, @"LEVEL_?LIMIT\s+(\d+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["min"] = int.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseScriptProperties(string commandText, MapAICommand command)
        {
            // Parse: SET SCRIPT onUserEnter="script" onFirstUserEnter="script"
            var onUserMatch = Regex.Match(commandText, @"ONUSERENTER\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (onUserMatch.Success)
                command.Parameters["onUserEnter"] = onUserMatch.Groups[1].Value;

            var onFirstMatch = Regex.Match(commandText, @"ONFIRSTUSERENTER\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (onFirstMatch.Success)
                command.Parameters["onFirstUserEnter"] = onFirstMatch.Groups[1].Value;

            // Single quoted script name
            var quotedMatches = QuotedStringPattern.Matches(commandText);
            if (quotedMatches.Count > 0 && !onUserMatch.Success)
                command.Parameters["onUserEnter"] = quotedMatches[0].Groups[1].Value;
        }

        private void ParseEffectProperties(string commandText, MapAICommand command)
        {
            // Parse: SET EFFECT "effectName"
            var quotedMatches = QuotedStringPattern.Matches(commandText);
            if (quotedMatches.Count > 0)
                command.Parameters["effect"] = quotedMatches[0].Groups[1].Value;
        }

        private void ParseHelpProperties(string commandText, MapAICommand command)
        {
            // Parse: SET HELP "help text"
            var quotedMatches = QuotedStringPattern.Matches(commandText);
            if (quotedMatches.Count > 0)
                command.Parameters["text"] = quotedMatches[0].Groups[1].Value;
        }

        private void ParseMapDescProperties(string commandText, MapAICommand command)
        {
            // Parse: SET MAP_DESC "description"
            var quotedMatches = QuotedStringPattern.Matches(commandText);
            if (quotedMatches.Count > 0)
                command.Parameters["desc"] = quotedMatches[0].Groups[1].Value;
        }

        private void ParseDropProperties(string commandText, MapAICommand command)
        {
            // Parse: SET DROP expire=N rate=N
            var expireMatch = Regex.Match(commandText, @"EXPIRE\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (expireMatch.Success)
                command.Parameters["expire"] = int.Parse(expireMatch.Groups[1].Value);

            var rateMatch = Regex.Match(commandText, @"RATE\s*=\s*([\d.]+)", RegexOptions.IgnoreCase);
            if (rateMatch.Success)
                command.Parameters["rate"] = float.Parse(rateMatch.Groups[1].Value);
        }

        private void ParseDecayProperties(string commandText, MapAICommand command)
        {
            // Parse: SET DECAY hp=N interval=N
            var hpMatch = Regex.Match(commandText, @"HP\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (hpMatch.Success)
                command.Parameters["hp"] = int.Parse(hpMatch.Groups[1].Value);

            var intervalMatch = Regex.Match(commandText, @"INTERVAL\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (intervalMatch.Success)
                command.Parameters["interval"] = int.Parse(intervalMatch.Groups[1].Value);
        }

        private void ParseRecoveryProperties(string commandText, MapAICommand command)
        {
            // Parse: SET RECOVERY rate=N or SET RECOVERY N
            var rateMatch = Regex.Match(commandText, @"RATE\s*=\s*([\d.]+)", RegexOptions.IgnoreCase);
            if (rateMatch.Success)
            {
                command.Parameters["rate"] = float.Parse(rateMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"RECOVERY\s+([\d.]+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["rate"] = float.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseMinimapRectProperties(string commandText, MapAICommand command)
        {
            // Parse: SET MINIMAP left=N top=N right=N bottom=N
            var leftMatch = Regex.Match(commandText, @"LEFT\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (leftMatch.Success)
                command.Parameters["left"] = int.Parse(leftMatch.Groups[1].Value);

            var topMatch = Regex.Match(commandText, @"TOP\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (topMatch.Success)
                command.Parameters["top"] = int.Parse(topMatch.Groups[1].Value);

            var rightMatch = Regex.Match(commandText, @"RIGHT\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (rightMatch.Success)
                command.Parameters["right"] = int.Parse(rightMatch.Groups[1].Value);

            var bottomMatch = Regex.Match(commandText, @"BOTTOM\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (bottomMatch.Success)
                command.Parameters["bottom"] = int.Parse(bottomMatch.Groups[1].Value);
        }

        private void ParsePatrolRangeProperties(string commandText, MapAICommand command)
        {
            // Parse: SET PATROL rx0=N rx1=N at (x,y) mob|npc
            var rx0Match = Regex.Match(commandText, @"RX0\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (rx0Match.Success)
                command.Parameters["rx0"] = int.Parse(rx0Match.Groups[1].Value);

            var rx1Match = Regex.Match(commandText, @"RX1\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (rx1Match.Success)
                command.Parameters["rx1"] = int.Parse(rx1Match.Groups[1].Value);
        }

        private void ParseRespawnTimeProperties(string commandText, MapAICommand command)
        {
            // Parse: SET RESPAWN time=N at (x,y)
            var timeMatch = Regex.Match(commandText, @"TIME\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (timeMatch.Success)
            {
                command.Parameters["time"] = int.Parse(timeMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"RESPAWN\s+(\d+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["time"] = int.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseTeamProperties(string commandText, MapAICommand command)
        {
            // Parse: SET TEAM team=N at (x,y)
            var teamMatch = Regex.Match(commandText, @"TEAM\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (teamMatch.Success)
            {
                command.Parameters["team"] = int.Parse(teamMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"SET\s+TEAM\s+(\d+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["team"] = int.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseLayerTilesetProperties(string commandText, MapAICommand command)
        {
            // Parse: SET LAYER_TILESET layer=N tileset="name"
            var layerMatch = Regex.Match(commandText, @"LAYER\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (layerMatch.Success)
                command.Parameters["layer"] = int.Parse(layerMatch.Groups[1].Value);

            var tilesetMatch = Regex.Match(commandText, @"TILESET\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (tilesetMatch.Success)
            {
                command.Parameters["tileset"] = tilesetMatch.Groups[1].Value;
            }
            else
            {
                var quotedMatches = QuotedStringPattern.Matches(commandText);
                if (quotedMatches.Count > 0)
                    command.Parameters["tileset"] = quotedMatches[0].Groups[1].Value;
            }
        }

        private void ParseSetZProperties(string commandText, MapAICommand command)
        {
            // Parse: SET Z z=N at (x,y) type
            var zMatch = Regex.Match(commandText, @"Z\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (zMatch.Success)
            {
                command.Parameters["z"] = int.Parse(zMatch.Groups[1].Value);
            }
            else
            {
                var valueMatch = Regex.Match(commandText, @"SET\s+Z\s+(-?\d+)", RegexOptions.IgnoreCase);
                if (valueMatch.Success)
                    command.Parameters["z"] = int.Parse(valueMatch.Groups[1].Value);
            }
        }

        private void ParseRenameProperties(string commandText, MapAICommand command)
        {
            // Parse: RENAME portal "oldName" to "newName"
            var quotedMatches = QuotedStringPattern.Matches(commandText);
            if (quotedMatches.Count >= 2)
            {
                command.Parameters["from"] = quotedMatches[0].Groups[1].Value;
                command.Parameters["to"] = quotedMatches[1].Groups[1].Value;
            }
        }

        private void ParseToolTipProperties(string commandText, MapAICommand command)
        {
            // Parse: ADD TOOLTIP at (x, y) size=(w, h) title="title" desc="description"
            // Parse: MODIFY TOOLTIP at (x, y) title="new title" desc="new desc"
            // Parse: MODIFY TOOLTIP "old title" title="new title"
            // Parse: REMOVE TOOLTIP at (x, y)
            // Parse: REMOVE TOOLTIP "title"

            // Size
            var sizeMatch = Regex.Match(commandText, @"SIZE\s*[=:]\s*\(\s*(\d+)\s*[,x]\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
            if (sizeMatch.Success)
            {
                command.Parameters["width"] = int.Parse(sizeMatch.Groups[1].Value);
                command.Parameters["height"] = int.Parse(sizeMatch.Groups[2].Value);
            }
            else
            {
                // Try individual width/height
                var widthMatch = Regex.Match(commandText, @"WIDTH\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                if (widthMatch.Success)
                    command.Parameters["width"] = int.Parse(widthMatch.Groups[1].Value);
                var heightMatch = Regex.Match(commandText, @"HEIGHT\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                if (heightMatch.Success)
                    command.Parameters["height"] = int.Parse(heightMatch.Groups[1].Value);
            }

            // Title (quoted)
            var titleMatch = Regex.Match(commandText, @"TITLE\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                command.Parameters["title"] = titleMatch.Groups[1].Value;

            // Description (quoted)
            var descMatch = Regex.Match(commandText, @"DESC\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (descMatch.Success)
                command.Parameters["desc"] = descMatch.Groups[1].Value;

            // Old title for modify (quoted string not matched by title=)
            if (command.Type == CommandType.ModifyToolTip || command.Type == CommandType.RemoveToolTip)
            {
                var quotedMatches = QuotedStringPattern.Matches(commandText);
                foreach (Match qm in quotedMatches)
                {
                    string quoted = qm.Groups[1].Value;
                    // If it's not the title= or desc= value, it might be the old title
                    if (!command.Parameters.ContainsKey("title") || !command.Parameters["title"].ToString().Equals(quoted, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!command.Parameters.ContainsKey("desc") || !command.Parameters["desc"].ToString().Equals(quoted, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!command.Parameters.ContainsKey("old_title"))
                                command.Parameters["old_title"] = quoted;
                        }
                    }
                }

                // For remove, if we have a quoted string and no old_title, use first quoted as title
                if (command.Type == CommandType.RemoveToolTip && quotedMatches.Count > 0 && !command.Parameters.ContainsKey("old_title"))
                {
                    command.Parameters["title"] = quotedMatches[0].Groups[1].Value;
                }
            }

            // Index (for referencing by #)
            var indexMatch = Regex.Match(commandText, @"#(\d+)", RegexOptions.IgnoreCase);
            if (indexMatch.Success)
                command.Parameters["index"] = int.Parse(indexMatch.Groups[1].Value);
        }

        #endregion

        /// <summary>
        /// Generate help text describing the command format
        /// </summary>
        public static string GetHelpText()
        {
            return @"
# AI Map Editor Command Reference

## Command Types
- ADD/CREATE/PLACE: Add new elements to the map
- REMOVE/DELETE: Remove elements from the map
- MOVE: Move elements to a new position
- MODIFY/CHANGE: Modify element properties
- FLIP: Flip an element horizontally
- SET: Set a specific property
- CLEAR: Remove all elements of a type
- DUPLICATE/COPY: Copy an element

## Element Types
- MOB/MONSTER: Monster spawns
- NPC: Non-player characters
- PORTAL: Map portals/warps
- OBJECT/OBJ: Map objects (decorations, platforms, etc.)
- TILE: Tile graphics
- BACKGROUND/BG: Background images
- FOOTHOLD/PLATFORM: Walkable platforms
- ROPE/LADDER: Climbable elements
- CHAIR: Sitting spots
- REACTOR: Interactive objects

## Examples

### Adding Elements
ADD MOB 100100 at (500, 200)
ADD NPC 9000000 at (200, 100) facing left
ADD PORTAL ""townPortal"" at (0, 50) type=Visible target_map=100000000

### Moving Elements
MOVE portal ""sp"" to (100, 50)
MOVE mob at (500, 200) to (600, 200)

### Removing Elements
DELETE portal ""unused""
REMOVE all mobs at layer 0
CLEAR all reactors

### Modifying Elements
SET portal ""town"" target_map=100000000
FLIP object at (300, 400)
MODIFY mob at (500, 200) respawn_time=5000

### Properties
- layer=N: Layer number (0-7)
- z=N: Z-order/depth
- flip/facing left/facing right: Horizontal flip
- target_map=N: Portal target map ID
- target_name=""name"": Portal target name
- script=""script"": Portal script name
- rx=N, ry=N: Background parallax values
- alpha=N: Background transparency (0-255)
";
        }
    }
}
