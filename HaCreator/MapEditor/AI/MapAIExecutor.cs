/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Executes parsed AI commands on a map Board.
    /// All changes are tracked through the undo/redo system.
    /// </summary>
    public class MapAIExecutor
    {
        private readonly Board board;
        private readonly List<string> executionLog;

        public MapAIExecutor(Board board)
        {
            this.board = board;
            this.executionLog = new List<string>();
        }

        /// <summary>
        /// Get the execution log from the last batch of commands
        /// </summary>
        public IReadOnlyList<string> ExecutionLog => executionLog.AsReadOnly();

        /// <summary>
        /// Execute a single command
        /// </summary>
        public bool ExecuteCommand(MapAICommand command)
        {
            if (!command.IsValid)
            {
                Log($"Invalid command: {command.ErrorMessage}");
                return false;
            }

            try
            {
                switch (command.Type)
                {
                    case CommandType.Add:
                        return ExecuteAdd(command);
                    case CommandType.Remove:
                        return ExecuteRemove(command);
                    case CommandType.Move:
                        return ExecuteMove(command);
                    case CommandType.Modify:
                    case CommandType.SetProperty:
                        return ExecuteModify(command);
                    case CommandType.Flip:
                        return ExecuteFlip(command);
                    case CommandType.Duplicate:
                        return ExecuteDuplicate(command);
                    case CommandType.Clear:
                        return ExecuteClear(command);
                    case CommandType.Select:
                        return ExecuteSelect(command);
                    case CommandType.TilePlatform:
                        return ExecuteTilePlatform(command);
                    case CommandType.TileStructure:
                        return ExecuteTileStructure(command);
                    default:
                        Log($"Unsupported command type: {command.Type}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Error executing command: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute multiple commands
        /// </summary>
        public ExecutionResult ExecuteCommands(IEnumerable<MapAICommand> commands)
        {
            executionLog.Clear();
            int successCount = 0;
            int failCount = 0;

            foreach (var command in commands)
            {
                if (ExecuteCommand(command))
                    successCount++;
                else
                    failCount++;
            }

            return new ExecutionResult
            {
                SuccessCount = successCount,
                FailCount = failCount,
                Log = new List<string>(executionLog)
            };
        }

        #region Command Implementations

        private bool ExecuteAdd(MapAICommand command)
        {
            // Platform and Wall have different coordinate requirements
            if (command.ElementType == ElementType.Platform)
            {
                return AddPlatform(command);
            }
            if (command.ElementType == ElementType.Wall)
            {
                return AddWall(command);
            }

            if (!command.TargetX.HasValue || !command.TargetY.HasValue)
            {
                Log($"ADD command requires coordinates: {command.OriginalText}");
                return false;
            }

            int x = command.TargetX.Value;
            int y = command.TargetY.Value;

            switch (command.ElementType)
            {
                case ElementType.Mob:
                    return AddMob(command, x, y);
                case ElementType.NPC:
                    return AddNpc(command, x, y);
                case ElementType.Portal:
                    return AddPortal(command, x, y);
                case ElementType.Chair:
                    return AddChair(x, y);
                case ElementType.Tile:
                    return AddTile(command, x, y);
                default:
                    Log($"ADD not supported for element type: {command.ElementType}");
                    return false;
            }
        }

        private bool AddMob(MapAICommand command, int x, int y)
        {
            if (!command.Parameters.TryGetValue("id", out var idObj))
            {
                Log("ADD MOB requires mob ID");
                return false;
            }

            string mobId = idObj.ToString();
            var mobInfo = MobInfo.Get(mobId);
            if (mobInfo == null)
            {
                Log($"Mob ID {mobId} not found");
                return false;
            }

            bool flip = command.Parameters.TryGetValue("flip", out var flipObj) && (bool)flipObj;
            int? mobTime = command.Parameters.TryGetValue("respawn_time", out var rtObj) ? (int?)Convert.ToInt32(rtObj) : null;

            var mob = new MobInstance(
                mobInfo, board, x, y,
                rx0Shift: 0, rx1Shift: 0, yShift: 0,
                limitedname: null, mobTime: mobTime,
                flip: flip, hide: false,
                info: null, team: null);

            mob.AddToBoard(null);
            Log($"Added mob {mobId} at ({x}, {y})");
            return true;
        }

        private bool AddNpc(MapAICommand command, int x, int y)
        {
            if (!command.Parameters.TryGetValue("id", out var idObj))
            {
                Log("ADD NPC requires NPC ID");
                return false;
            }

            string npcId = idObj.ToString();
            var npcInfo = NpcInfo.Get(npcId);
            if (npcInfo == null)
            {
                Log($"NPC ID {npcId} not found");
                return false;
            }

            bool flip = command.Parameters.TryGetValue("flip", out var flipObj) && (bool)flipObj;

            var npc = new NpcInstance(
                npcInfo, board, x, y,
                rx0Shift: 0, rx1Shift: 0, yShift: 0,
                limitedname: null, mobTime: null,
                flip: flip, hide: false,
                info: null, team: null);

            npc.AddToBoard(null);
            Log($"Added NPC {npcId} at ({x}, {y})");
            return true;
        }

        private bool AddPortal(MapAICommand command, int x, int y)
        {
            string portalName = command.TargetIdentifier ?? $"p{board.BoardItems.Portals.Count}";

            PortalType pt = PortalType.StartPoint;
            if (command.Parameters.TryGetValue("portal_type", out var ptObj))
            {
                if (Enum.TryParse(ptObj.ToString(), true, out PortalType parsedType))
                    pt = parsedType;
            }

            int targetMap = 999999999;
            if (command.Parameters.TryGetValue("target_map", out var tmObj))
                targetMap = Convert.ToInt32(tmObj);

            string targetName = "";
            if (command.Parameters.TryGetValue("target_name", out var tnObj))
                targetName = tnObj.ToString();

            string script = null;
            if (command.Parameters.TryGetValue("script", out var scriptObj))
                script = scriptObj.ToString();

            var portalInfo = PortalInfo.GetPortalInfoByType(pt);

            var portal = new PortalInstance(
                portalInfo, board, x, y,
                pn: portalName, pt: pt, tn: targetName, tm: targetMap,
                script: script, delay: null,
                hideTooltip: false, onlyOnce: false,
                horizontalImpact: null, verticalImpact: null,
                image: null, hRange: null, vRange: null);

            portal.AddToBoard(null);
            Log($"Added portal \"{portalName}\" at ({x}, {y}) type={pt}");
            return true;
        }

        private bool AddChair(int x, int y)
        {
            var chair = new Chair(board, x, y);
            chair.AddToBoard(null);
            Log($"Added chair at ({x}, {y})");
            return true;
        }

        private bool AddPlatform(MapAICommand command)
        {
            // Get points from parameters
            if (!command.Parameters.TryGetValue("points", out var pointsObj) ||
                !(pointsObj is List<(int x, int y)> points) ||
                points.Count < 2)
            {
                Log("ADD PLATFORM requires at least 2 points");
                return false;
            }

            // Get layer (default to current selected layer or 0)
            int layer = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layer = Convert.ToInt32(layerObj);

            // Get platform number (zm) - use a new platform number
            int platformNumber = GetNextPlatformNumber(layer);

            // Get foothold properties
            bool forbidFallDown = command.Parameters.TryGetValue("forbid_fall_down", out var ffdObj) && Convert.ToBoolean(ffdObj);
            bool cantThrough = command.Parameters.TryGetValue("cant_through", out var ctObj) && Convert.ToBoolean(ctObj);

            // Create anchors for all points
            var anchors = new List<FootholdAnchor>();
            foreach (var point in points)
            {
                var anchor = new FootholdAnchor(board, point.x, point.y, layer, platformNumber, true);
                board.BoardItems.FHAnchors.Add(anchor);
                anchors.Add(anchor);
            }

            // Connect anchors with foothold lines
            for (int i = 0; i < anchors.Count - 1; i++)
            {
                var line = new FootholdLine(board, anchors[i], anchors[i + 1],
                    forbidFallDown ? MapleBool.True : MapleBool.False,
                    cantThrough ? MapleBool.True : MapleBool.False,
                    null, null);
                board.BoardItems.FootholdLines.Add(line);
            }

            Log($"Added platform with {points.Count} points on layer {layer}");
            return true;
        }

        private bool AddWall(MapAICommand command)
        {
            // Get wall coordinates
            if (!command.Parameters.TryGetValue("wall_x", out var xObj))
            {
                // Try to get x from TargetX
                if (!command.TargetX.HasValue)
                {
                    Log("ADD WALL requires x coordinate");
                    return false;
                }
                xObj = command.TargetX.Value;
            }

            if (!command.Parameters.TryGetValue("top_y", out var topYObj) ||
                !command.Parameters.TryGetValue("bottom_y", out var bottomYObj))
            {
                Log("ADD WALL requires top_y and bottom_y coordinates");
                return false;
            }

            int x = Convert.ToInt32(xObj);
            int topY = Convert.ToInt32(topYObj);
            int bottomY = Convert.ToInt32(bottomYObj);

            // Get layer (default to 0)
            int layer = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layer = Convert.ToInt32(layerObj);

            // Get platform number
            int platformNumber = GetNextPlatformNumber(layer);

            // Create two anchors for the wall
            var topAnchor = new FootholdAnchor(board, x, topY, layer, platformNumber, true);
            var bottomAnchor = new FootholdAnchor(board, x, bottomY, layer, platformNumber, true);

            board.BoardItems.FHAnchors.Add(topAnchor);
            board.BoardItems.FHAnchors.Add(bottomAnchor);

            // Create the wall line
            var line = new FootholdLine(board, topAnchor, bottomAnchor);
            board.BoardItems.FootholdLines.Add(line);

            Log($"Added wall at x={x} from y={topY} to y={bottomY} on layer {layer}");
            return true;
        }

        private bool AddTile(MapAICommand command, int x, int y)
        {
            // Get required tile properties
            if (!command.Parameters.TryGetValue("tileset", out var tilesetObj))
            {
                Log("ADD TILE requires tileset parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("category", out var categoryObj))
            {
                Log("ADD TILE requires category parameter");
                return false;
            }

            string tileset = tilesetObj.ToString();
            string category = categoryObj.ToString();
            string tileNo = command.Parameters.TryGetValue("tile_no", out var noObj) ? noObj.ToString() : "0";

            // Get layer (default to 0)
            int layerNum = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layerNum = Convert.ToInt32(layerObj);

            // Get z-order
            int z = 0;
            if (command.Parameters.TryGetValue("z", out var zObj))
                z = Convert.ToInt32(zObj);

            // Validate tileset exists
            if (!Program.InfoManager.TileSets.ContainsKey(tileset))
            {
                // List available tilesets to help the user
                var availableTilesets = Program.InfoManager.TileSets.Keys.Take(20).ToList();
                var tilesetList = string.Join(", ", availableTilesets);
                var moreText = Program.InfoManager.TileSets.Count > 20 ? $" ... and {Program.InfoManager.TileSets.Count - 20} more" : "";
                Log($"Tileset '{tileset}' not found. Available tilesets: {tilesetList}{moreText}");
                return false;
            }

            try
            {
                // Get tile info
                var tileInfo = TileInfo.Get(tileset, category, tileNo);
                if (tileInfo == null)
                {
                    Log($"Tile not found: tileset={tileset}, category={category}, no={tileNo}");
                    return false;
                }

                // Get the layer
                if (layerNum < 0 || layerNum >= board.Layers.Count)
                {
                    Log($"Invalid layer number: {layerNum}. Valid range is 0-{board.Layers.Count - 1}");
                    return false;
                }
                Layer layer = board.Layers[layerNum];

                // IMPORTANT: Set the layer's tileset if not already set, or check if it matches
                // Each layer can only have one tileset - all tiles on a layer must use the same tileset
                if (string.IsNullOrEmpty(layer.tS))
                {
                    // Layer has no tileset yet, set it
                    layer.tS = tileset;
                    Log($"Set layer {layerNum} tileset to '{tileset}'");
                }
                else if (layer.tS != tileset)
                {
                    // Layer has a different tileset - find a layer with matching tileset or use another layer
                    Log($"Warning: Layer {layerNum} uses tileset '{layer.tS}', but tile uses '{tileset}'. Searching for compatible layer...");

                    // Try to find a layer with matching tileset
                    bool found = false;
                    for (int i = 0; i < board.Layers.Count; i++)
                    {
                        if (board.Layers[i].tS == tileset)
                        {
                            layer = board.Layers[i];
                            layerNum = i;
                            found = true;
                            Log($"Using layer {layerNum} which has matching tileset '{tileset}'");
                            break;
                        }
                        else if (string.IsNullOrEmpty(board.Layers[i].tS))
                        {
                            // Found an empty layer, use it
                            layer = board.Layers[i];
                            layer.tS = tileset;
                            layerNum = i;
                            found = true;
                            Log($"Using empty layer {layerNum} and setting tileset to '{tileset}'");
                            break;
                        }
                    }

                    if (!found)
                    {
                        Log($"Error: No available layer for tileset '{tileset}'. All layers have different tilesets assigned.");
                        return false;
                    }
                }

                // Create the tile instance
                var tile = (TileInstance)tileInfo.CreateInstance(layer, board, x, y, z, layer.zMDefault, false, false);
                tile.AddToBoard(null);

                Log($"Added tile tileset={tileset} category={category} no={tileNo} at ({x}, {y}) layer={layerNum}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to add tile: {ex.Message}");
                return false;
            }
        }

        private int GetNextPlatformNumber(int layer)
        {
            // Find the maximum platform number used in this layer
            int maxPlatform = 0;
            foreach (var anchor in board.BoardItems.FHAnchors)
            {
                if (anchor.LayerNumber == layer && anchor.PlatformNumber > maxPlatform)
                    maxPlatform = anchor.PlatformNumber;
            }
            return maxPlatform + 1;
        }

        private bool ExecuteTilePlatform(MapAICommand command)
        {
            // Get required parameters
            if (!command.Parameters.TryGetValue("tileset", out var tilesetObj))
            {
                Log("TILE PLATFORM requires tileset parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("start_x", out var startXObj))
            {
                Log("TILE PLATFORM requires start_x parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("end_x", out var endXObj))
            {
                Log("TILE PLATFORM requires end_x parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("y", out var yObj))
            {
                Log("TILE PLATFORM requires y parameter");
                return false;
            }

            string tileset = tilesetObj.ToString();
            int startX = Convert.ToInt32(startXObj);
            int endX = Convert.ToInt32(endXObj);
            int y = Convert.ToInt32(yObj);

            // Ensure start_x < end_x
            if (startX > endX)
            {
                int temp = startX;
                startX = endX;
                endX = temp;
            }

            // Get layer (default to 0)
            int layerNum = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layerNum = Convert.ToInt32(layerObj);

            // Validate tileset exists
            if (!Program.InfoManager.TileSets.ContainsKey(tileset))
            {
                var availableTilesets = Program.InfoManager.TileSets.Keys.Take(20).ToList();
                var tilesetList = string.Join(", ", availableTilesets);
                Log($"Tileset '{tileset}' not found. Available: {tilesetList}");
                return false;
            }

            try
            {
                // Get the layer
                if (layerNum < 0 || layerNum >= board.Layers.Count)
                {
                    Log($"Invalid layer number: {layerNum}");
                    return false;
                }
                Layer layer = board.Layers[layerNum];

                // Set the layer's tileset
                if (string.IsNullOrEmpty(layer.tS))
                {
                    layer.tS = tileset;
                }
                else if (layer.tS != tileset)
                {
                    // Find a compatible layer
                    bool found = false;
                    for (int i = 0; i < board.Layers.Count; i++)
                    {
                        if (board.Layers[i].tS == tileset || string.IsNullOrEmpty(board.Layers[i].tS))
                        {
                            layer = board.Layers[i];
                            if (string.IsNullOrEmpty(layer.tS)) layer.tS = tileset;
                            layerNum = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Log($"No available layer for tileset '{tileset}'");
                        return false;
                    }
                }

                // Tile categories for a complete platform structure:
                // Top row: enH0 (horizontal enclosure top) - caps the top
                // Middle: bsc (basic fill) - only if platform is tall enough
                // Bottom row: enH1 (horizontal enclosure bottom) - caps the bottom
                // Left edge: enV0 (vertical enclosure left)
                // Right edge: enV1 (vertical enclosure right)

                TileInfo topTile = null;      // enH0
                TileInfo bottomTile = null;   // enH1
                TileInfo fillTile = null;     // bsc
                TileInfo leftTile = null;     // enV0
                TileInfo rightTile = null;    // enV1

                try { topTile = TileInfo.Get(tileset, "enH0", "0"); } catch { }
                try { bottomTile = TileInfo.Get(tileset, "enH1", "0"); } catch { }
                try { fillTile = TileInfo.Get(tileset, "bsc", "0"); } catch { }
                try { leftTile = TileInfo.Get(tileset, "enV0", "0"); } catch { }
                try { rightTile = TileInfo.Get(tileset, "enV1", "0"); } catch { }

                // We need at least enH0 for top and bsc or enH1 for structure
                if (topTile == null && fillTile == null)
                {
                    Log($"Tileset '{tileset}' doesn't have required categories (enH0, bsc). Cannot auto-tile.");
                    return false;
                }

                int tilesAdded = 0;
                int platformWidth = endX - startX;

                // Determine tile dimensions
                int tileWidth = fillTile?.Width ?? topTile?.Width ?? 90;
                int tileHeight = fillTile?.Height ?? topTile?.Height ?? 90;

                // For a proper platform, we create 2 rows:
                // Row 1 (top): enH0 tiles to cap the top
                // Row 2 (bottom): enH1 tiles to cap the bottom
                // The Y coordinate given is the platform surface, so:
                // - Top row is at y (surface level)
                // - If we have a bottom row, it's below

                // === TOP ROW (enH0) - at the surface Y ===
                if (topTile != null)
                {
                    int currentX = startX;
                    int topTileWidth = topTile.Width;

                    // Place left cap if available
                    if (leftTile != null)
                    {
                        var tile = (TileInstance)leftTile.CreateInstance(layer, board, currentX, y, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        currentX += leftTile.Width;
                        tilesAdded++;
                    }

                    // Fill with top tiles
                    int rightWidth = rightTile?.Width ?? 0;
                    while (currentX + topTileWidth <= endX - rightWidth)
                    {
                        var tile = (TileInstance)topTile.CreateInstance(layer, board, currentX, y, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        currentX += topTileWidth;
                        tilesAdded++;
                    }

                    // Place right cap if available
                    if (rightTile != null && currentX < endX)
                    {
                        var tile = (TileInstance)rightTile.CreateInstance(layer, board, currentX, y, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        tilesAdded++;
                    }
                }

                // === BOTTOM ROW (enH1) - below the top row ===
                if (bottomTile != null && topTile != null)
                {
                    int bottomY = y + topTile.Height;
                    int currentX = startX;
                    int bottomTileWidth = bottomTile.Width;

                    // Place left cap if available
                    if (leftTile != null)
                    {
                        var tile = (TileInstance)leftTile.CreateInstance(layer, board, currentX, bottomY, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        currentX += leftTile.Width;
                        tilesAdded++;
                    }

                    // Fill with bottom tiles
                    int rightWidth = rightTile?.Width ?? 0;
                    while (currentX + bottomTileWidth <= endX - rightWidth)
                    {
                        var tile = (TileInstance)bottomTile.CreateInstance(layer, board, currentX, bottomY, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        currentX += bottomTileWidth;
                        tilesAdded++;
                    }

                    // Place right cap if available
                    if (rightTile != null && currentX < endX)
                    {
                        var tile = (TileInstance)rightTile.CreateInstance(layer, board, currentX, bottomY, 0, layer.zMDefault, false, true);
                        board.BoardItems.TileObjs.Add(tile);
                        tilesAdded++;
                    }
                }

                Log($"Auto-tiled platform from x={startX} to x={endX} at y={y} with {tilesAdded} tiles (2 rows) using '{tileset}'");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to tile platform: {ex.Message}");
                return false;
            }
        }

        private bool ExecuteTileStructure(MapAICommand command)
        {
            // Get required parameters
            if (!command.Parameters.TryGetValue("tileset", out var tilesetObj))
            {
                Log("TILE STRUCTURE requires tileset parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("structure_type", out var structTypeObj))
            {
                Log("TILE STRUCTURE requires structure_type parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("start_x", out var startXObj))
            {
                Log("TILE STRUCTURE requires start_x parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("y", out var yObj))
            {
                Log("TILE STRUCTURE requires y parameter");
                return false;
            }

            string tileset = tilesetObj.ToString();
            string structureType = structTypeObj.ToString().ToLowerInvariant();
            int startX = Convert.ToInt32(startXObj);
            int y = Convert.ToInt32(yObj);
            int endX = command.Parameters.TryGetValue("end_x", out var endXObj) ? Convert.ToInt32(endXObj) : startX + 200;
            int height = command.Parameters.TryGetValue("height", out var heightObj) ? Convert.ToInt32(heightObj) : 3;

            // Ensure start_x < end_x
            if (startX > endX)
            {
                int temp = startX;
                startX = endX;
                endX = temp;
            }

            // Get layer (default to 0)
            int layerNum = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layerNum = Convert.ToInt32(layerObj);

            // Validate tileset exists
            if (!Program.InfoManager.TileSets.ContainsKey(tileset))
            {
                var availableTilesets = Program.InfoManager.TileSets.Keys.Take(20).ToList();
                var tilesetList = string.Join(", ", availableTilesets);
                Log($"Tileset '{tileset}' not found. Available: {tilesetList}");
                return false;
            }

            try
            {
                // Get or prepare layer
                if (layerNum < 0 || layerNum >= board.Layers.Count)
                {
                    Log($"Invalid layer number: {layerNum}");
                    return false;
                }
                Layer layer = GetOrPrepareLayer(tileset, layerNum);
                if (layer == null)
                {
                    Log($"No available layer for tileset '{tileset}'");
                    return false;
                }

                // Load all possible tile types
                var tiles = LoadTileTypes(tileset);
                if (tiles.enH0 == null && tiles.bsc == null)
                {
                    Log($"Tileset '{tileset}' doesn't have required categories (enH0, bsc). Cannot build structure.");
                    return false;
                }

                int tilesAdded = 0;

                // Offset tiles to the right by half edU width to align with foothold
                int tileOffset = (tiles.edU?.Width ?? tiles.TileWidth) / 2;
                int adjustedStartX = startX + tileOffset;
                int adjustedEndX = endX + tileOffset;

                switch (structureType)
                {
                    case "flat":
                        tilesAdded = BuildFlatPlatform(layer, tiles, adjustedStartX, adjustedEndX, y);
                        break;
                    case "tall":
                        tilesAdded = BuildTallPlatform(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        break;
                    case "pillar":
                        tilesAdded = BuildPillar(layer, tiles, adjustedStartX, y, height);
                        break;
                    case "slope_up_left":
                        tilesAdded = BuildSlopeUpLeft(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        break;
                    case "slope_up_right":
                        tilesAdded = BuildSlopeUpRight(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        break;
                    case "slope_down_left":
                        tilesAdded = BuildSlopeDownLeft(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        break;
                    case "slope_down_right":
                        tilesAdded = BuildSlopeDownRight(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        break;
                    case "staircase_right":
                        tilesAdded = BuildStaircaseRight(layer, tiles, adjustedStartX, y, height);
                        break;
                    case "staircase_left":
                        tilesAdded = BuildStaircaseLeft(layer, tiles, adjustedStartX, y, height);
                        break;
                    default:
                        Log($"Unknown structure type: {structureType}");
                        return false;
                }

                Log($"Built {structureType} structure at x={startX} y={y} with {tilesAdded} tiles using '{tileset}'");
                return tilesAdded > 0;
            }
            catch (Exception ex)
            {
                Log($"Failed to build tile structure: {ex.Message}");
                return false;
            }
        }

        #region Tile Structure Helpers

        /*
         * ============================================================================
         * MAPLESTORY TILE SYSTEM DOCUMENTATION
         * ============================================================================
         *
         * MapleStory uses a tile-based system for creating platform visuals. Each
         * tileset (e.g., "grassySoil", "snowyRord", "deepMine") contains a set of
         * tile categories that fit together to form complete platform structures.
         *
         * TILE CATEGORIES:
         * ----------------
         *
         * FILL TILES:
         *   bsc    - Basic/fill tile. Used to fill the interior of platforms.
         *            Has origin at (0, 0) - anchor is at top-left.
         *
         * HORIZONTAL ENCLOSURES (Top/Bottom caps):
         *   enH0   - Horizontal enclosure TOP. Forms the top surface of platforms.
         *            Has origin offset (e.g., (0, 38)) - anchor is below the top edge.
         *   enH1   - Horizontal enclosure BOTTOM. Forms the bottom edge of platforms.
         *            Has origin at (0, 0) - anchor is at top-left.
         *
         * VERTICAL ENCLOSURES (Left/Right edges):
         *   enV0   - Vertical enclosure LEFT. Decorates the left edge of platforms.
         *            Overlays on top of bsc/enH1 tiles at the leftmost position.
         *   enV1   - Vertical enclosure RIGHT. Decorates the right edge of platforms.
         *            Overlays on top of bsc/enH1 tiles at the rightmost position.
         *
         * CORNER EDGES:
         *   edU    - Edge Up (top corners). Placed at top-left and top-right corners.
         *            Overlays on top of enH0 tiles at corner positions.
         *   edD    - Edge Down (bottom corners). Placed at bottom-left and bottom-right.
         *            Overlays on top of enH1 tiles at corner positions.
         *
         * SLOPE TILES:
         *   slLU   - Slope Left-Up. Ascending slope going up to the left.
         *   slRU   - Slope Right-Up. Ascending slope going up to the right.
         *   slLD   - Slope Left-Down. Descending slope going down to the left.
         *   slRD   - Slope Right-Down. Descending slope going down to the right.
         *
         *
         * FLAT PLATFORM STRUCTURE (3 rows):
         * ---------------------------------
         *
         *   Position:    [0]    [1]    [2]    [3]    (tile positions)
         *
         *   Row 1 (enH0): enH0   enH0   enH0   enH0   <- Top surface
         *   Overlay:      edU                  edU    <- Top corners (on top of enH0)
         *
         *   Row 2 (bsc):  bsc    bsc    bsc    bsc    <- Fill/interior
         *   Overlay:      enV0                 enV1   <- Left/right edges (on top of bsc)
         *
         *   Row 3 (enH1): enH1   enH1   enH1   enH1   <- Bottom surface
         *   Overlay:      edD                  edD    <- Bottom corners (on top of enH1)
         *
         *
         * TILE ORIGIN POINTS:
         * -------------------
         * Tiles have different origin (anchor) points that affect positioning:
         *
         *   - enH0: Origin typically at (0, 38) - anchor is 38px below the tile's top.
         *           When placed at Y, the visual top is at Y - 38.
         *
         *   - bsc, enH1: Origin at (0, 0) - anchor is at the tile's top-left.
         *                When placed at Y, the visual top is at Y.
         *
         *   - enV0: Origin may have X offset (e.g., (33, 0)) for proper edge alignment.
         *
         * To connect tiles visually, calculate the visual bottom of one row and
         * place the next row so its visual top aligns with that bottom:
         *
         *   Visual Top    = PlacementY - Origin.Y
         *   Visual Bottom = PlacementY - Origin.Y + Height
         *   Next Row Y    = Previous Visual Bottom + Next Origin.Y
         *
         *
         * TILE SPACING:
         * -------------
         * Standard spacing values (multiplied by tileset magnification 'mag'):
         *   - Horizontal (TileWidth): 90 * mag pixels between tile positions
         *   - Vertical (RowHeight):   60 * mag pixels between row positions
         *
         * These values are found in TileInfo.cs and ensure proper tile connections.
         *
         * ============================================================================
         */

        /// <summary>
        /// Holds references to all tile types for a tileset, along with spacing information.
        /// See documentation above for detailed explanation of each tile category.
        /// </summary>
        private class TileSet
        {
            // Horizontal enclosures (top/bottom surfaces)
            public TileInfo enH0;    // Top surface - has Y origin offset for proper alignment
            public TileInfo enH1;    // Bottom surface

            // Vertical enclosures (left/right edges) - overlay on fill tiles
            public TileInfo enV0;    // Left edge decoration
            public TileInfo enV1;    // Right edge decoration

            // Fill tile
            public TileInfo bsc;     // Basic/interior fill

            // Slope tiles
            public TileInfo slLU;    // Slope ascending to the left
            public TileInfo slRU;    // Slope ascending to the right
            public TileInfo slLD;    // Slope descending to the left
            public TileInfo slRD;    // Slope descending to the right

            // Corner edges - overlay on enH0/enH1 at corners
            public TileInfo edU;     // Top corners (left and right)
            public TileInfo edD;     // Bottom corners (left and right)

            /// <summary>
            /// Tileset magnification factor. All spacing values are multiplied by this.
            /// </summary>
            public int mag = 1;

            /// <summary>
            /// Vertical spacing between tile rows (60 * mag pixels).
            /// </summary>
            public int RowHeight => 60 * mag;

            /// <summary>
            /// Horizontal width of each tile position (90 * mag pixels).
            /// </summary>
            public int TileWidth => 90 * mag;
        }

        private TileSet LoadTileTypes(string tileset)
        {
            var tiles = new TileSet();

            // Get tileset magnification
            try
            {
                var magProp = Program.InfoManager.TileSets[tileset]["info"]?["mag"];
                if (magProp != null)
                    tiles.mag = ((MapleLib.WzLib.WzProperties.WzIntProperty)magProp).Value;
            }
            catch { tiles.mag = 1; }

            // Try to load each tile type, logging failures
            tiles.enH0 = TryGetTile(tileset, "enH0");
            tiles.enH1 = TryGetTile(tileset, "enH1");
            tiles.enV0 = TryGetTile(tileset, "enV0");
            tiles.enV1 = TryGetTile(tileset, "enV1");
            tiles.bsc = TryGetTile(tileset, "bsc");
            tiles.slLU = TryGetTile(tileset, "slLU");
            tiles.slRU = TryGetTile(tileset, "slRU");
            tiles.slLD = TryGetTile(tileset, "slLD");
            tiles.slRD = TryGetTile(tileset, "slRD");
            tiles.edU = TryGetTile(tileset, "edU");
            tiles.edD = TryGetTile(tileset, "edD");

            // Log what was loaded
            var loaded = new List<string>();
            if (tiles.enH0 != null) loaded.Add("enH0");
            if (tiles.enH1 != null) loaded.Add("enH1");
            if (tiles.enV0 != null) loaded.Add("enV0");
            if (tiles.enV1 != null) loaded.Add("enV1");
            if (tiles.bsc != null) loaded.Add("bsc");
            if (tiles.slLU != null) loaded.Add("slLU");
            if (tiles.slRU != null) loaded.Add("slRU");
            if (tiles.slLD != null) loaded.Add("slLD");
            if (tiles.slRD != null) loaded.Add("slRD");

            Log($"Tileset '{tileset}' loaded (mag={tiles.mag}): {string.Join(", ", loaded)}");

            // Log origin points to debug alignment
            if (tiles.enH0 != null) Log($"  enH0 origin: ({tiles.enH0.Origin.X}, {tiles.enH0.Origin.Y})");
            if (tiles.bsc != null) Log($"  bsc origin: ({tiles.bsc.Origin.X}, {tiles.bsc.Origin.Y})");
            if (tiles.enH1 != null) Log($"  enH1 origin: ({tiles.enH1.Origin.X}, {tiles.enH1.Origin.Y})");
            if (tiles.enV0 != null) Log($"  enV0 origin: ({tiles.enV0.Origin.X}, {tiles.enV0.Origin.Y})");
            if (tiles.enV1 != null) Log($"  enV1 origin: ({tiles.enV1.Origin.X}, {tiles.enV1.Origin.Y})");

            return tiles;
        }

        private TileInfo TryGetTile(string tileset, string category)
        {
            // Try tile numbers 0, 1, 2 in case "0" doesn't exist
            foreach (var tileNo in new[] { "0", "1", "2" })
            {
                try
                {
                    var tile = TileInfo.Get(tileset, category, tileNo);
                    if (tile != null) return tile;
                }
                catch { }
            }
            return null;
        }

        private Layer GetOrPrepareLayer(string tileset, int preferredLayer)
        {
            Layer layer = board.Layers[preferredLayer];

            if (string.IsNullOrEmpty(layer.tS))
            {
                layer.tS = tileset;
                return layer;
            }
            else if (layer.tS == tileset)
            {
                return layer;
            }

            // Find a compatible layer
            for (int i = 0; i < board.Layers.Count; i++)
            {
                if (board.Layers[i].tS == tileset)
                    return board.Layers[i];
                if (string.IsNullOrEmpty(board.Layers[i].tS))
                {
                    board.Layers[i].tS = tileset;
                    return board.Layers[i];
                }
            }

            return null;
        }

        private int PlaceTile(Layer layer, TileInfo tileInfo, int x, int y)
        {
            if (tileInfo == null) return 0;
            lock (board.ParentControl)
            {
                var tile = (TileInstance)tileInfo.CreateInstance(layer, board, x, y, 0, layer.zMDefault, false, false);
                tile.AddToBoard(null);
            }
            return 1;
        }

        /// <summary>
        /// Place a horizontal row of tiles.
        /// Note: enV0/enV1 are vertical edge caps for tall structures, not used in horizontal rows.
        /// </summary>
        private int PlaceRow(Layer layer, TileSet tiles, TileInfo fillTile, int startX, int endX, int y)
        {
            if (fillTile == null) return 0;

            int count = 0;
            int currentX = startX;
            int tileWidth = tiles.TileWidth;  // Use standard tile width (90 * mag)

            // Fill the entire row with the fill tile
            while (currentX + tileWidth < endX)
            {
                count += PlaceTile(layer, fillTile, currentX, y);
                currentX += tileWidth;
            }

            return count;
        }

        /// <summary>
        /// Build a flat 3-row platform with proper tile layering.
        ///
        /// Structure created:
        /// <code>
        ///   Row 1: [enH0][enH0][enH0][enH0]  with edU overlay at corners
        ///   Row 2: [bsc ][bsc ][bsc ][bsc ]  with enV0/enV1 overlay at edges
        ///   Row 3: [enH1][enH1][enH1][enH1]  with edD overlay at corners
        /// </code>
        ///
        /// Key behaviors:
        /// - Fill tiles are placed first, then corner/edge decorations overlay on top
        /// - Row Y positions account for different tile origin offsets (enH0 has Y origin of ~38)
        /// - Right edge tiles are placed at currentX (one position past the last fill tile)
        /// </summary>
        /// <param name="layer">The layer to place tiles on</param>
        /// <param name="tiles">The loaded tileset</param>
        /// <param name="startX">Left edge X coordinate</param>
        /// <param name="endX">Right boundary X coordinate</param>
        /// <param name="y">Y coordinate for the top row (enH0)</param>
        /// <returns>Number of tiles placed</returns>
        private int BuildFlatPlatform(Layer layer, TileSet tiles, int startX, int endX, int y)
        {
            int count = 0;
            var middleTile = tiles.bsc ?? tiles.enH0;  // Fallback to enH0 if bsc not available
            int tileWidth = tiles.TileWidth;

            // Row 1: Top row with edU corners and enH0 fill
            int enH0Y = y;
            int enH0Count = 0;
            int currentX = startX;
            int lastFillX = startX;

            // Fill entire row with enH0 first
            while (currentX + tileWidth < endX)
            {
                count += PlaceTile(layer, tiles.enH0, currentX, enH0Y);
                enH0Count++;
                lastFillX = currentX;
                currentX += tileWidth;
            }

            // Top-left corner: edU (on top of first enH0, shifted down 1px for alignment)
            if (tiles.edU != null)
            {
                count += PlaceTile(layer, tiles.edU, startX, enH0Y + 1);
            }

            // Top-right corner: edU (at rightmost position)
            if (tiles.edU != null)
            {
                count += PlaceTile(layer, tiles.edU, currentX, enH0Y);
            }

            // Calculate visual bottom of enH0:
            int enH0OriginY = tiles.enH0?.Origin.Y ?? 0;
            int enH0Height = tiles.enH0?.Height ?? tiles.RowHeight;
            int enH0VisualBottom = enH0Y - enH0OriginY + enH0Height;

            // Row 2: bsc with enV0/enV1 vertical edges
            int bscOriginY = middleTile?.Origin.Y ?? 0;
            int bscHeight = middleTile?.Height ?? tiles.RowHeight;
            int bscY = enH0VisualBottom + bscOriginY;
            int bscCount = 0;

            // Fill entire row with bsc first
            currentX = startX;
            lastFillX = startX;
            while (currentX + tileWidth < endX)
            {
                count += PlaceTile(layer, middleTile, currentX, bscY);
                bscCount++;
                lastFillX = currentX;
                currentX += tileWidth;
            }

            // Left edge: enV0 (on top of first bsc)
            if (tiles.enV0 != null)
            {
                count += PlaceTile(layer, tiles.enV0, startX, bscY);
            }

            // Right edge: enV1 (at rightmost position)
            if (tiles.enV1 != null)
            {
                count += PlaceTile(layer, tiles.enV1, currentX, bscY);
            }

            // Calculate visual bottom of bsc
            int bscVisualBottom = bscY - bscOriginY + bscHeight;

            // Row 3: enH1 with edD corners (bottom corners)
            int enH1Count = 0;
            if (tiles.enH1 != null)
            {
                int enH1OriginY = tiles.enH1.Origin.Y;
                int enH1Y = bscVisualBottom + enH1OriginY;

                // Fill entire row with enH1 first
                currentX = startX;
                lastFillX = startX;
                while (currentX + tileWidth < endX)
                {
                    count += PlaceTile(layer, tiles.enH1, currentX, enH1Y);
                    enH1Count++;
                    lastFillX = currentX;
                    currentX += tileWidth;
                }

                // Bottom-left corner: edD (on top of first enH1)
                if (tiles.edD != null)
                {
                    count += PlaceTile(layer, tiles.edD, startX, enH1Y);
                }

                // Bottom-right corner: edD (at rightmost position)
                if (tiles.edD != null)
                {
                    count += PlaceTile(layer, tiles.edD, currentX, enH1Y);
                }
            }

            Log($"Flat structure rows: enH0={enH0Count} at y={enH0Y}, bsc={bscCount} at y={bscY}, enH1={enH1Count}");

            return count;
        }

        /// <summary>
        /// Build a tall multi-row platform with variable height.
        ///
        /// Structure created (example with rows=4):
        /// <code>
        ///   Row 1: [enH0][enH0][enH0]  <- Top surface
        ///   Row 2: [bsc ][bsc ][bsc ]  <- Fill rows (repeated for height)
        ///   Row 3: [bsc ][bsc ][bsc ]  <- More fill
        ///   Row 4: [enH1][enH1][enH1]  <- Bottom surface
        /// </code>
        ///
        /// Note: This method does not add edge/corner overlays like BuildFlatPlatform.
        /// </summary>
        /// <param name="layer">The layer to place tiles on</param>
        /// <param name="tiles">The loaded tileset</param>
        /// <param name="startX">Left edge X coordinate</param>
        /// <param name="endX">Right boundary X coordinate</param>
        /// <param name="y">Y coordinate for the top row</param>
        /// <param name="rows">Total number of rows (minimum 2)</param>
        /// <returns>Number of tiles placed</returns>
        private int BuildTallPlatform(Layer layer, TileSet tiles, int startX, int endX, int y, int rows)
        {
            if (rows < 2) rows = 2;
            int count = 0;

            // Warn if bsc is missing for tall structures
            if (tiles.bsc == null && rows > 2)
            {
                Log("Warning: 'bsc' tile not found in tileset. Middle rows will use enH0 as fallback.");
            }

            var middleTile = tiles.bsc ?? tiles.enH0;

            // Row 1: enH0 at the specified Y
            int enH0Y = y;
            count += PlaceRow(layer, tiles, tiles.enH0, startX, endX, enH0Y);

            // Calculate visual bottom of enH0
            int enH0OriginY = tiles.enH0?.Origin.Y ?? 0;
            int enH0Height = tiles.enH0?.Height ?? tiles.RowHeight;
            int currentVisualBottom = enH0Y - enH0OriginY + enH0Height;

            // Middle rows with bsc
            int bscOriginY = middleTile?.Origin.Y ?? 0;
            int bscHeight = middleTile?.Height ?? tiles.RowHeight;
            for (int i = 0; i < rows - 2; i++)
            {
                int bscY = currentVisualBottom + bscOriginY;
                count += PlaceRow(layer, tiles, middleTile, startX, endX, bscY);
                currentVisualBottom = bscY - bscOriginY + bscHeight;
            }

            // Bottom row with enH1
            if (tiles.enH1 != null)
            {
                int enH1OriginY = tiles.enH1.Origin.Y;
                int enH1Y = currentVisualBottom + enH1OriginY;
                count += PlaceRow(layer, tiles, tiles.enH1, startX, endX, enH1Y);
            }

            return count;
        }

        /// <summary>
        /// Build a 1-tile wide vertical pillar/column.
        ///
        /// Structure created (example with rows=4):
        /// <code>
        ///   [enH0]  <- Top cap
        ///   [bsc ]  <- Fill (repeated for height)
        ///   [bsc ]  <- More fill
        ///   [enH1]  <- Bottom cap
        /// </code>
        ///
        /// Pillars are single-tile wide, useful for narrow vertical structures.
        /// </summary>
        /// <param name="layer">The layer to place tiles on</param>
        /// <param name="tiles">The loaded tileset</param>
        /// <param name="x">X coordinate for the pillar</param>
        /// <param name="y">Y coordinate for the top tile</param>
        /// <param name="rows">Total number of rows (minimum 2)</param>
        /// <returns>Number of tiles placed</returns>
        private int BuildPillar(Layer layer, TileSet tiles, int x, int y, int rows)
        {
            if (rows < 2) rows = 2;
            int count = 0;

            // Warn if bsc is missing for tall pillars
            if (tiles.bsc == null && rows > 2)
            {
                Log("Warning: 'bsc' tile not found in tileset. Middle rows will use enH0 as fallback.");
            }

            var middleTile = tiles.bsc ?? tiles.enH0;

            // Top (enH0) at the specified Y
            int enH0Y = y;
            count += PlaceTile(layer, tiles.enH0, x, enH0Y);

            // Calculate visual bottom of enH0
            int enH0OriginY = tiles.enH0?.Origin.Y ?? 0;
            int enH0Height = tiles.enH0?.Height ?? tiles.RowHeight;
            int currentVisualBottom = enH0Y - enH0OriginY + enH0Height;

            // Middle (bsc) rows
            int bscOriginY = middleTile?.Origin.Y ?? 0;
            int bscHeight = middleTile?.Height ?? tiles.RowHeight;
            for (int i = 0; i < rows - 2; i++)
            {
                int bscY = currentVisualBottom + bscOriginY;
                count += PlaceTile(layer, middleTile, x, bscY);
                currentVisualBottom = bscY - bscOriginY + bscHeight;
            }

            // Bottom (enH1)
            if (tiles.enH1 != null)
            {
                int enH1OriginY = tiles.enH1.Origin.Y;
                int enH1Y = currentVisualBottom + enH1OriginY;
                count += PlaceTile(layer, tiles.enH1, x, enH1Y);
            }

            return count;
        }

        /// <summary>
        /// Build a platform with slope going UP on the LEFT side
        /// The slope extends upward from the left edge of the main platform
        /// </summary>
        private int BuildSlopeUpLeft(Layer layer, TileSet tiles, int startX, int endX, int y, int slopeHeight)
        {
            if (slopeHeight < 1) slopeHeight = 2;
            int count = 0;

            // The main platform is at y, the slope goes up (decreasing Y)
            // Build from bottom to top

            // Bottom row of main platform (full width)
            count += PlaceRow(layer, tiles, tiles.enH1, startX, endX, y + tiles.RowHeight);

            // Main platform top row
            count += PlaceRow(layer, tiles, tiles.enH0, startX + tiles.TileWidth * slopeHeight, endX, y);

            // Slope tiles going up-left
            for (int i = 0; i < slopeHeight; i++)
            {
                int slopeX = startX + tiles.TileWidth * (slopeHeight - 1 - i);
                int slopeY = y - tiles.RowHeight * i;
                count += PlaceTile(layer, tiles.slLU, slopeX, slopeY);
            }

            return count;
        }

        /// <summary>
        /// Build a platform with slope going UP on the RIGHT side
        /// </summary>
        private int BuildSlopeUpRight(Layer layer, TileSet tiles, int startX, int endX, int y, int slopeHeight)
        {
            if (slopeHeight < 1) slopeHeight = 2;
            int count = 0;

            // Bottom row of main platform (full width)
            count += PlaceRow(layer, tiles, tiles.enH1, startX, endX, y + tiles.RowHeight);

            // Main platform top row (shortened on right)
            count += PlaceRow(layer, tiles, tiles.enH0, startX, endX - tiles.TileWidth * slopeHeight, y);

            // Slope tiles going up-right
            for (int i = 0; i < slopeHeight; i++)
            {
                int slopeX = endX - tiles.TileWidth * (slopeHeight - i);
                int slopeY = y - tiles.RowHeight * i;
                count += PlaceTile(layer, tiles.slRU, slopeX, slopeY);
            }

            return count;
        }

        /// <summary>
        /// Build a platform with slope going DOWN on the LEFT side
        /// </summary>
        private int BuildSlopeDownLeft(Layer layer, TileSet tiles, int startX, int endX, int y, int slopeHeight)
        {
            if (slopeHeight < 1) slopeHeight = 2;
            int count = 0;
            var middleTile = tiles.bsc ?? tiles.enH0;  // Fallback to enH0 if bsc not available

            // Top row of main platform (full width)
            count += PlaceRow(layer, tiles, tiles.enH0, startX, endX, y);

            // Middle fill row
            count += PlaceRow(layer, tiles, middleTile, startX + tiles.TileWidth * slopeHeight, endX, y + tiles.RowHeight);

            // Bottom row (shortened, starting after slope)
            count += PlaceRow(layer, tiles, tiles.enH1, startX + tiles.TileWidth * slopeHeight, endX, y + tiles.RowHeight * 2);

            // Slope tiles going down-left
            for (int i = 0; i < slopeHeight; i++)
            {
                int slopeX = startX + tiles.TileWidth * i;
                int slopeY = y + tiles.RowHeight * (i + 1);
                count += PlaceTile(layer, tiles.slLD, slopeX, slopeY);
            }

            return count;
        }

        /// <summary>
        /// Build a platform with slope going DOWN on the RIGHT side
        /// </summary>
        private int BuildSlopeDownRight(Layer layer, TileSet tiles, int startX, int endX, int y, int slopeHeight)
        {
            if (slopeHeight < 1) slopeHeight = 2;
            int count = 0;
            var middleTile = tiles.bsc ?? tiles.enH0;  // Fallback to enH0 if bsc not available

            // Top row of main platform (full width)
            count += PlaceRow(layer, tiles, tiles.enH0, startX, endX, y);

            // Middle fill row (shortened on right)
            count += PlaceRow(layer, tiles, middleTile, startX, endX - tiles.TileWidth * slopeHeight, y + tiles.RowHeight);

            // Bottom row (shortened on right)
            count += PlaceRow(layer, tiles, tiles.enH1, startX, endX - tiles.TileWidth * slopeHeight, y + tiles.RowHeight * 2);

            // Slope tiles going down-right
            for (int i = 0; i < slopeHeight; i++)
            {
                int slopeX = endX - tiles.TileWidth * (slopeHeight - i);
                int slopeY = y + tiles.RowHeight * (i + 1);
                count += PlaceTile(layer, tiles.slRD, slopeX, slopeY);
            }

            return count;
        }

        /// <summary>
        /// Build a staircase stepping UP to the RIGHT
        /// </summary>
        private int BuildStaircaseRight(Layer layer, TileSet tiles, int startX, int y, int steps)
        {
            if (steps < 2) steps = 3;
            int count = 0;
            int stepWidth = tiles.TileWidth * 2;  // Each step is 2 tiles wide

            // Build each step from bottom-left to top-right
            for (int i = 0; i < steps; i++)
            {
                int stepX = startX + stepWidth * i;
                int stepY = y - tiles.RowHeight * 2 * i;  // Each step is 2 rows high

                // Each step is a mini 2-row platform
                count += PlaceRow(layer, tiles, tiles.enH0, stepX, stepX + stepWidth, stepY);
                count += PlaceRow(layer, tiles, tiles.enH1, stepX, stepX + stepWidth, stepY + tiles.RowHeight);
            }

            return count;
        }

        /// <summary>
        /// Build a staircase stepping UP to the LEFT
        /// </summary>
        private int BuildStaircaseLeft(Layer layer, TileSet tiles, int startX, int y, int steps)
        {
            if (steps < 2) steps = 3;
            int count = 0;
            int stepWidth = tiles.TileWidth * 2;  // Each step is 2 tiles wide

            // Build each step from bottom-right to top-left
            for (int i = 0; i < steps; i++)
            {
                int stepX = startX - stepWidth * i;
                int stepY = y - tiles.RowHeight * 2 * i;  // Each step is 2 rows high

                // Each step is a mini 2-row platform
                count += PlaceRow(layer, tiles, tiles.enH0, stepX, stepX + stepWidth, stepY);
                count += PlaceRow(layer, tiles, tiles.enH1, stepX, stepX + stepWidth, stepY + tiles.RowHeight);
            }

            return count;
        }

        #endregion

        private bool ExecuteRemove(MapAICommand command)
        {
            // Handle foothold removal separately (they don't inherit from BoardItem)
            if (command.ElementType == ElementType.Foothold ||
                command.ElementType == ElementType.Platform ||
                command.ElementType == ElementType.Wall)
            {
                return RemoveFootholdNear(command);
            }

            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log($"No targets found for REMOVE command: {command.OriginalText}");
                return false;
            }

            foreach (var target in targets)
            {
                target.RemoveItem(null);
            }

            Log($"Removed {targets.Count} element(s)");
            return true;
        }

        private bool RemoveFootholdNear(MapAICommand command)
        {
            if (!command.TargetX.HasValue || !command.TargetY.HasValue)
            {
                Log("REMOVE foothold requires coordinates");
                return false;
            }

            int x = command.TargetX.Value;
            int y = command.TargetY.Value;
            int tolerance = 20;

            // Find footholds near the specified coordinates
            var linesToRemove = new List<FootholdLine>();
            foreach (var line in board.BoardItems.FootholdLines)
            {
                // Check if either anchor is near the target
                bool nearFirst = Math.Abs(line.FirstDot.X - x) <= tolerance && Math.Abs(line.FirstDot.Y - y) <= tolerance;
                bool nearSecond = Math.Abs(line.SecondDot.X - x) <= tolerance && Math.Abs(line.SecondDot.Y - y) <= tolerance;

                if (nearFirst || nearSecond)
                {
                    // Filter by wall/platform type if specified
                    if (command.ElementType == ElementType.Wall && !line.IsWall)
                        continue;
                    if (command.ElementType == ElementType.Platform && line.IsWall)
                        continue;

                    linesToRemove.Add(line);
                }
            }

            if (linesToRemove.Count == 0)
            {
                Log($"No footholds found near ({x}, {y})");
                return false;
            }

            foreach (var line in linesToRemove)
            {
                line.Remove(false, null);  // Don't remove dots, they may be shared
            }

            Log($"Removed {linesToRemove.Count} foothold(s)");
            return true;
        }

        private bool ExecuteMove(MapAICommand command)
        {
            if (!command.TargetX.HasValue || !command.TargetY.HasValue)
            {
                Log($"MOVE command requires target coordinates: {command.OriginalText}");
                return false;
            }

            var targets = FindTargets(command, excludeCoordinates: true);
            if (targets.Count == 0)
            {
                Log($"No targets found for MOVE command: {command.OriginalText}");
                return false;
            }

            int newX = command.TargetX.Value;
            int newY = command.TargetY.Value;

            foreach (var target in targets)
            {
                // Record for undo
                var actions = new List<UndoRedoAction>
                {
                    UndoRedoManager.ItemMoved(target, new Microsoft.Xna.Framework.Point(target.X, target.Y), new Microsoft.Xna.Framework.Point(newX, newY))
                };
                board.UndoRedoMan.AddUndoBatch(actions);

                target.Move(newX, newY);
            }

            Log($"Moved {targets.Count} element(s) to ({newX}, {newY})");
            return true;
        }

        private bool ExecuteModify(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log($"No targets found for MODIFY command: {command.OriginalText}");
                return false;
            }

            int modifiedCount = 0;
            foreach (var target in targets)
            {
                if (ApplyProperties(target, command.Parameters))
                    modifiedCount++;
            }

            Log($"Modified {modifiedCount} element(s)");
            return modifiedCount > 0;
        }

        private bool ExecuteFlip(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log($"No targets found for FLIP command: {command.OriginalText}");
                return false;
            }

            int flippedCount = 0;
            foreach (var target in targets)
            {
                if (target is IFlippable flippable)
                {
                    flippable.Flip = !flippable.Flip;
                    flippedCount++;
                }
            }

            Log($"Flipped {flippedCount} element(s)");
            return flippedCount > 0;
        }

        private bool ExecuteDuplicate(MapAICommand command)
        {
            // TODO: Implement duplication
            Log("DUPLICATE command not yet implemented");
            return false;
        }

        private bool ExecuteClear(MapAICommand command)
        {
            int clearedCount = 0;

            switch (command.ElementType)
            {
                case ElementType.Mob:
                    clearedCount = board.BoardItems.Mobs.Count;
                    foreach (var mob in board.BoardItems.Mobs.ToList())
                        mob.RemoveItem(null);
                    break;

                case ElementType.NPC:
                    clearedCount = board.BoardItems.NPCs.Count;
                    foreach (var npc in board.BoardItems.NPCs.ToList())
                        npc.RemoveItem(null);
                    break;

                case ElementType.Portal:
                    clearedCount = board.BoardItems.Portals.Count;
                    foreach (var portal in board.BoardItems.Portals.ToList())
                        portal.RemoveItem(null);
                    break;

                case ElementType.Reactor:
                    clearedCount = board.BoardItems.Reactors.Count;
                    foreach (var reactor in board.BoardItems.Reactors.ToList())
                        reactor.RemoveItem(null);
                    break;

                case ElementType.Chair:
                    clearedCount = board.BoardItems.Chairs.Count;
                    foreach (var chair in board.BoardItems.Chairs.ToList())
                        chair.RemoveItem(null);
                    break;

                case ElementType.Foothold:
                case ElementType.Platform:
                case ElementType.Wall:
                    // Remove all foothold lines and anchors
                    clearedCount = board.BoardItems.FootholdLines.Count;
                    foreach (var line in board.BoardItems.FootholdLines.ToList())
                        line.Remove(true, null);  // removeDots=true to also remove orphaned anchors
                    break;

                case ElementType.Tile:
                    // Remove all tiles (TileInstance objects from TileObjs)
                    var tiles = board.BoardItems.TileObjs.OfType<TileInstance>().ToList();
                    clearedCount = tiles.Count;
                    foreach (var tile in tiles)
                        tile.RemoveItem(null);
                    break;

                default:
                    Log($"CLEAR not supported for element type: {command.ElementType}");
                    return false;
            }

            Log($"Cleared {clearedCount} {command.ElementType} element(s)");
            return true;
        }

        private bool ExecuteSelect(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log($"No targets found for SELECT command: {command.OriginalText}");
                return false;
            }

            // Clear current selection
            foreach (var item in board.SelectedItems.ToList())
            {
                item.Selected = false;
            }
            board.SelectedItems.Clear();

            // Select new targets
            foreach (var target in targets)
            {
                target.Selected = true;
                board.SelectedItems.Add(target);
            }

            Log($"Selected {targets.Count} element(s)");
            return true;
        }

        #endregion

        #region Helper Methods

        private List<BoardItem> FindTargets(MapAICommand command, bool excludeCoordinates = false)
        {
            var results = new List<BoardItem>();

            // Note: Foothold/Platform/Wall are handled separately as they don't inherit from BoardItem
            IEnumerable<BoardItem> source = command.ElementType switch
            {
                ElementType.Mob => board.BoardItems.Mobs.Cast<BoardItem>(),
                ElementType.NPC => board.BoardItems.NPCs.Cast<BoardItem>(),
                ElementType.Portal => board.BoardItems.Portals.Cast<BoardItem>(),
                ElementType.Object => board.BoardItems.TileObjs.OfType<ObjectInstance>().Cast<BoardItem>(),
                ElementType.Tile => board.BoardItems.TileObjs.OfType<TileInstance>().Cast<BoardItem>(),
                ElementType.Background => board.BoardItems.BackBackgrounds.Cast<BoardItem>()
                    .Concat(board.BoardItems.FrontBackgrounds.Cast<BoardItem>()),
                ElementType.Reactor => board.BoardItems.Reactors.Cast<BoardItem>(),
                ElementType.Chair => board.BoardItems.Chairs.Cast<BoardItem>(),
                ElementType.All => board.BoardItems.Items.Cast<BoardItem>(),
                _ => Enumerable.Empty<BoardItem>()
            };

            // Filter by identifier (name)
            if (!string.IsNullOrEmpty(command.TargetIdentifier))
            {
                if (command.ElementType == ElementType.Portal)
                {
                    source = source.OfType<PortalInstance>()
                        .Where(p => p.pn.Equals(command.TargetIdentifier, StringComparison.OrdinalIgnoreCase))
                        .Cast<BoardItem>();
                }
            }

            // Filter by coordinates (for "at (x, y)" targeting)
            if (!excludeCoordinates && command.TargetX.HasValue && command.TargetY.HasValue)
            {
                int tolerance = 10; // pixels
                source = source.Where(item =>
                    Math.Abs(item.X - command.TargetX.Value) <= tolerance &&
                    Math.Abs(item.Y - command.TargetY.Value) <= tolerance);
            }

            // Filter by layer
            if (command.Parameters.TryGetValue("layer", out var layerObj))
            {
                int layer = Convert.ToInt32(layerObj);
                source = source.Where(item =>
                {
                    if (item is LayeredItem layered)
                        return layered.Layer.LayerNumber == layer;
                    return true;
                });
            }

            results.AddRange(source);
            return results;
        }

        private bool ApplyProperties(BoardItem target, Dictionary<string, object> properties)
        {
            bool modified = false;

            foreach (var kvp in properties)
            {
                try
                {
                    switch (kvp.Key.ToLowerInvariant())
                    {
                        case "flip":
                            if (target is IFlippable flippable)
                            {
                                flippable.Flip = Convert.ToBoolean(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "target_map":
                            if (target is PortalInstance portal)
                            {
                                portal.tm = Convert.ToInt32(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "target_name":
                            if (target is PortalInstance portal2)
                            {
                                portal2.tn = kvp.Value.ToString();
                                modified = true;
                            }
                            break;

                        case "script":
                            if (target is PortalInstance portal3)
                            {
                                portal3.script = kvp.Value.ToString();
                                modified = true;
                            }
                            break;

                        case "hide":
                            if (target is LifeInstance life)
                            {
                                life.Hide = Convert.ToBoolean(kvp.Value);
                                modified = true;
                            }
                            else if (target is ObjectInstance obj)
                            {
                                obj.hide = Convert.ToBoolean(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "respawn_time":
                        case "mobtime":
                            if (target is LifeInstance life2)
                            {
                                life2.MobTime = Convert.ToInt32(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "rx":
                            if (target is BackgroundInstance bg)
                            {
                                bg.rx = Convert.ToInt32(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "ry":
                            if (target is BackgroundInstance bg2)
                            {
                                bg2.ry = Convert.ToInt32(kvp.Value);
                                modified = true;
                            }
                            break;

                        case "alpha":
                            if (target is BackgroundInstance bg3)
                            {
                                bg3.a = Convert.ToInt32(kvp.Value);
                                modified = true;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to apply property {kvp.Key}: {ex.Message}");
                }
            }

            return modified;
        }

        private void Log(string message)
        {
            //executionLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            executionLog.Add($"{message}");
        }

        #endregion
    }

    /// <summary>
    /// Result of executing a batch of commands
    /// </summary>
    public class ExecutionResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> Log { get; set; }

        public bool IsSuccess => FailCount == 0;
        public int TotalCount => SuccessCount + FailCount;
    }
}
