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
                    case CommandType.SetBgm:
                        return ExecuteSetBgm(command);
                    case CommandType.SetMapOption:
                        return ExecuteSetMapOption(command);
                    case CommandType.SetFieldLimit:
                        return ExecuteSetFieldLimit(command);
                    case CommandType.SetMapSize:
                        return ExecuteSetMapSize(command);
                    case CommandType.SetVR:
                        return ExecuteSetVR(command);
                    case CommandType.ClearVR:
                        return ExecuteClearVR(command);

                    // New map property commands
                    case CommandType.SetReturnMap:
                        return ExecuteSetReturnMap(command);
                    case CommandType.SetMobRate:
                        return ExecuteSetMobRate(command);
                    case CommandType.SetFieldType:
                        return ExecuteSetFieldType(command);
                    case CommandType.SetTimeLimit:
                        return ExecuteSetTimeLimit(command);
                    case CommandType.SetLevelLimit:
                        return ExecuteSetLevelLimit(command);
                    case CommandType.SetScript:
                        return ExecuteSetScript(command);
                    case CommandType.SetEffect:
                        return ExecuteSetEffect(command);
                    case CommandType.SetHelp:
                        return ExecuteSetHelp(command);
                    case CommandType.SetMapDesc:
                        return ExecuteSetMapDesc(command);
                    case CommandType.SetDropSettings:
                        return ExecuteSetDropSettings(command);
                    case CommandType.SetDecaySettings:
                        return ExecuteSetDecaySettings(command);
                    case CommandType.SetRecovery:
                        return ExecuteSetRecovery(command);

                    // Minimap commands
                    case CommandType.SetMinimapRect:
                        return ExecuteSetMinimapRect(command);
                    case CommandType.ClearMinimapRect:
                        return ExecuteClearMinimapRect(command);

                    // Life/Spawn commands
                    case CommandType.SetPatrolRange:
                        return ExecuteSetPatrolRange(command);
                    case CommandType.SetRespawnTime:
                        return ExecuteSetRespawnTime(command);
                    case CommandType.SetTeam:
                        return ExecuteSetTeam(command);

                    // Layer management
                    case CommandType.SetLayerTileset:
                        return ExecuteSetLayerTileset(command);

                    // Z-Order commands
                    case CommandType.SetZ:
                        return ExecuteSetZ(command);

                    // Miscellaneous
                    case CommandType.Rename:
                        return ExecuteRename(command);

                    // ToolTip commands
                    case CommandType.AddToolTip:
                        return ExecuteAddToolTip(command);
                    case CommandType.RemoveToolTip:
                        return ExecuteRemoveToolTip(command);
                    case CommandType.ModifyToolTip:
                        return ExecuteModifyToolTip(command);

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
            // Platform, Wall, Rope, and Ladder have different coordinate requirements
            if (command.ElementType == ElementType.Platform)
            {
                return AddPlatform(command);
            }
            if (command.ElementType == ElementType.Wall)
            {
                return AddWall(command);
            }
            if (command.ElementType == ElementType.Rope)
            {
                return AddRope(command, isLadder: false);
            }
            if (command.ElementType == ElementType.Ladder)
            {
                return AddRope(command, isLadder: true);
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
                case ElementType.Object:
                    return AddObject(command, x, y);
                case ElementType.Background:
                    return AddBackground(command, x, y);
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

            lock (board.ParentControl)
            {
                var mob = new MobInstance(
                    mobInfo, board, x, y,
                    rx0Shift: 0, rx1Shift: 0, yShift: 0,
                    limitedname: null, mobTime: mobTime,
                    flip: flip, hide: false,
                    info: null, team: null);

                mob.AddToBoard(null);

                // Record for undo
                board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(mob) });
            }

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

            lock (board.ParentControl)
            {
                var npc = new NpcInstance(
                    npcInfo, board, x, y,
                    rx0Shift: 0, rx1Shift: 0, yShift: 0,
                    limitedname: null, mobTime: null,
                    flip: flip, hide: false,
                    info: null, team: null);

                npc.AddToBoard(null);

                // Record for undo
                board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(npc) });
            }

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

            // Adjust Y coordinate so the bottom of the portal sits at the specified Y (ground-snapping)
            // Formula: adjustedY = y - height + origin.Y
            int adjustedY = y;
            if (portalInfo != null)
            {
                int portalHeight = portalInfo.Height;
                int originY = portalInfo.Origin.Y;
                adjustedY = y - portalHeight + originY;

                if (adjustedY != y)
                {
                    Log($"Portal origin adjustment: original Y={y}, adjusted Y={adjustedY} (height={portalHeight}, origin.Y={originY})");
                }
            }

            lock (board.ParentControl)
            {
                var portal = new PortalInstance(
                    portalInfo, board, x, adjustedY,
                    pn: portalName, pt: pt, tn: targetName, tm: targetMap,
                    script: script, delay: null,
                    hideTooltip: false, onlyOnce: false,
                    horizontalImpact: null, verticalImpact: null,
                    image: null, hRange: null, vRange: null);

                portal.AddToBoard(null);

                // Record for undo
                board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(portal) });
            }

            Log($"Added portal \"{portalName}\" at ({x}, {adjustedY}) type={pt}");
            return true;
        }

        private bool AddChair(int x, int y)
        {
            lock (board.ParentControl)
            {
                var chair = new Chair(board, x, y);
                chair.AddToBoard(null);

                // Record for undo
                board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(chair) });
            }

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

            lock (board.ParentControl)
            {
                // Create anchors for all points
                var anchors = new List<FootholdAnchor>();
                var undoActions = new List<UndoRedoAction>();

                foreach (var point in points)
                {
                    var anchor = new FootholdAnchor(board, point.x, point.y, layer, platformNumber, true);
                    board.BoardItems.FHAnchors.Add(anchor);
                    anchors.Add(anchor);
                    undoActions.Add(UndoRedoManager.ItemAdded(anchor));
                }

                // Connect anchors with foothold lines
                for (int i = 0; i < anchors.Count - 1; i++)
                {
                    var line = new FootholdLine(board, anchors[i], anchors[i + 1],
                        forbidFallDown ? MapleBool.True : MapleBool.False,
                        cantThrough ? MapleBool.True : MapleBool.False,
                        null, null);
                    board.BoardItems.FootholdLines.Add(line);
                    undoActions.Add(UndoRedoManager.LineAdded(line, anchors[i], anchors[i + 1]));
                }

                // Record for undo as a single batch
                board.UndoRedoMan.AddUndoBatch(undoActions);
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

            lock (board.ParentControl)
            {
                // Create two anchors for the wall
                var topAnchor = new FootholdAnchor(board, x, topY, layer, platformNumber, true);
                var bottomAnchor = new FootholdAnchor(board, x, bottomY, layer, platformNumber, true);

                board.BoardItems.FHAnchors.Add(topAnchor);
                board.BoardItems.FHAnchors.Add(bottomAnchor);

                // Create the wall line
                var line = new FootholdLine(board, topAnchor, bottomAnchor);
                board.BoardItems.FootholdLines.Add(line);

                // Record for undo
                var undoActions = new List<UndoRedoAction>
                {
                    UndoRedoManager.ItemAdded(topAnchor),
                    UndoRedoManager.ItemAdded(bottomAnchor),
                    UndoRedoManager.LineAdded(line, topAnchor, bottomAnchor)
                };
                board.UndoRedoMan.AddUndoBatch(undoActions);
            }

            Log($"Added wall at x={x} from y={topY} to y={bottomY} on layer {layer}");
            return true;
        }

        private bool AddRope(MapAICommand command, bool isLadder)
        {
            // Get rope/ladder coordinates
            if (!command.Parameters.TryGetValue("rope_x", out var xObj))
            {
                // Try to get x from TargetX
                if (!command.TargetX.HasValue)
                {
                    Log($"ADD {(isLadder ? "LADDER" : "ROPE")} requires x coordinate");
                    return false;
                }
                xObj = command.TargetX.Value;
            }

            if (!command.Parameters.TryGetValue("top_y", out var topYObj) ||
                !command.Parameters.TryGetValue("bottom_y", out var bottomYObj))
            {
                Log($"ADD {(isLadder ? "LADDER" : "ROPE")} requires top_y and bottom_y coordinates");
                return false;
            }

            int x = Convert.ToInt32(xObj);
            int topY = Convert.ToInt32(topYObj);
            int bottomY = Convert.ToInt32(bottomYObj);

            // Get layer (default to 0)
            int layer = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layer = Convert.ToInt32(layerObj);

            // Get uf (upper foothold) - default to true
            bool uf = true;
            if (command.Parameters.TryGetValue("uf", out var ufObj))
                uf = Convert.ToBoolean(ufObj);

            lock (board.ParentControl)
            {
                // Create the rope/ladder
                var rope = new Rope(board, x, topY, bottomY, isLadder, layer, uf);
                board.BoardItems.Ropes.Add(rope);

                // Record for undo
                board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.RopeAdded(rope) });
            }

            string elementType = isLadder ? "ladder" : "rope";
            Log($"Added {elementType} at x={x} from y={topY} to y={bottomY} on layer {layer} (uf={uf})");
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

                lock (board.ParentControl)
                {
                    // Create the tile instance
                    var tile = (TileInstance)tileInfo.CreateInstance(layer, board, x, y, z, layer.zMDefault, false, false);
                    tile.AddToBoard(null);

                    // Record for undo
                    board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(tile) });
                }

                Log($"Added tile tileset={tileset} category={category} no={tileNo} at ({x}, {y}) layer={layerNum}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to add tile: {ex.Message}");
                return false;
            }
        }

        private bool AddObject(MapAICommand command, int x, int y)
        {
            // Get required object properties
            if (!command.Parameters.TryGetValue("oS", out var osObj))
            {
                Log("ADD OBJECT requires oS (object set) parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("l0", out var l0Obj))
            {
                Log("ADD OBJECT requires l0 parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("l1", out var l1Obj))
            {
                Log("ADD OBJECT requires l1 parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("l2", out var l2Obj))
            {
                Log("ADD OBJECT requires l2 parameter");
                return false;
            }

            string oS = osObj.ToString();
            string l0 = l0Obj.ToString();
            string l1 = l1Obj.ToString();
            string l2 = l2Obj.ToString();

            // Get layer (default to 0)
            int layerNum = 0;
            if (command.Parameters.TryGetValue("layer", out var layerObj))
                layerNum = Convert.ToInt32(layerObj);

            // Get z-order
            int z = 0;
            if (command.Parameters.TryGetValue("z", out var zObj))
                z = Convert.ToInt32(zObj);

            // Get flip
            bool flip = false;
            if (command.Parameters.TryGetValue("flip", out var flipObj))
                flip = Convert.ToBoolean(flipObj);

            // Check if raw positioning is requested (place origin at exact coordinates)
            bool rawPosition = false;
            if (command.Parameters.TryGetValue("raw_position", out var rawObj))
                rawPosition = Convert.ToBoolean(rawObj);

            // Validate object set exists
            if (!Program.InfoManager.ObjectSets.ContainsKey(oS))
            {
                var availableSets = Program.InfoManager.ObjectSets.Keys.Take(20).ToList();
                var setsList = string.Join(", ", availableSets);
                Log($"Object set '{oS}' not found. Available: {setsList}");
                return false;
            }

            try
            {
                // Get object info
                var objectInfo = ObjectInfo.Get(oS, l0, l1, l2);
                if (objectInfo == null)
                {
                    Log($"Object not found: oS={oS}, l0={l0}, l1={l1}, l2={l2}");
                    return false;
                }

                // Get the layer
                if (layerNum < 0 || layerNum >= board.Layers.Count)
                {
                    Log($"Invalid layer number: {layerNum}. Valid range is 0-{board.Layers.Count - 1}");
                    return false;
                }
                Layer layer = board.Layers[layerNum];

                // Adjust Y coordinate so the bottom of the object sits at the specified Y
                // This makes it intuitive: when you place an object at Y=ground, its bottom is on the ground
                // Formula: adjustedY = y - height + origin.Y
                // - If origin is at top (origin.Y=0), we shift up by the full height
                // - If origin is at bottom (origin.Y=height), no adjustment needed
                int adjustedY = y;
                if (!rawPosition)
                {
                    int objectHeight = objectInfo.Height;
                    int originY = objectInfo.Origin.Y;
                    adjustedY = y - objectHeight + originY;

                    if (adjustedY != y)
                    {
                        Log($"Object origin adjustment: original Y={y}, adjusted Y={adjustedY} (height={objectHeight}, origin.Y={originY})");
                    }
                }

                lock (board.ParentControl)
                {
                    // Create the object instance using the simple overload
                    var obj = (ObjectInstance)objectInfo.CreateInstance(layer, board, x, adjustedY, z, flip);
                    board.BoardItems.TileObjs.Add(obj);

                    // Record for undo
                    board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(obj) });
                }

                Log($"Added object oS={oS} l0={l0} l1={l1} l2={l2} at ({x}, {adjustedY}) layer={layerNum}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to add object: {ex.Message}");
                return false;
            }
        }

        private bool AddBackground(MapAICommand command, int x, int y)
        {
            // Get required background properties
            if (!command.Parameters.TryGetValue("bS", out var bsObj))
            {
                Log("ADD BACKGROUND requires bS (background set) parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("no", out var noObj))
            {
                Log("ADD BACKGROUND requires no (number) parameter");
                return false;
            }
            if (!command.Parameters.TryGetValue("bg_type", out var typeObj))
            {
                Log("ADD BACKGROUND requires type parameter (back, ani, or spine)");
                return false;
            }

            string bS = bsObj.ToString();
            string no = noObj.ToString();
            string bgType = typeObj.ToString().ToLowerInvariant();

            // Parse background info type (for loading)
            BackgroundInfoType infoType;
            switch (bgType)
            {
                case "back":
                    infoType = BackgroundInfoType.Background;
                    break;
                case "ani":
                    infoType = BackgroundInfoType.Animation;
                    break;
                case "spine":
                    infoType = BackgroundInfoType.Spine;
                    break;
                default:
                    Log($"Invalid background type '{bgType}'. Must be 'back', 'ani', or 'spine'");
                    return false;
            }

            // Get optional properties
            int rx = command.Parameters.TryGetValue("rx", out var rxObj) ? Convert.ToInt32(rxObj) : 0;
            int ry = command.Parameters.TryGetValue("ry", out var ryObj) ? Convert.ToInt32(ryObj) : 0;
            int cx = command.Parameters.TryGetValue("cx", out var cxObj) ? Convert.ToInt32(cxObj) : 0;
            int cy = command.Parameters.TryGetValue("cy", out var cyObj) ? Convert.ToInt32(cyObj) : 0;
            int a = command.Parameters.TryGetValue("a", out var aObj) ? Convert.ToInt32(aObj) : 255;
            int z = command.Parameters.TryGetValue("z", out var zObj) ? Convert.ToInt32(zObj) : 0;
            bool front = command.Parameters.TryGetValue("front", out var frontObj) && Convert.ToBoolean(frontObj);
            bool flip = command.Parameters.TryGetValue("flip", out var flipObj) && Convert.ToBoolean(flipObj);

            // Validate background set exists
            if (!Program.InfoManager.BackgroundSets.ContainsKey(bS))
            {
                var availableSets = Program.InfoManager.BackgroundSets.Keys.Take(20).ToList();
                var setsList = string.Join(", ", availableSets);
                Log($"Background set '{bS}' not found. Available: {setsList}");
                return false;
            }

            try
            {
                // Get background info using the board's graphics device
                var bgInfo = BackgroundInfo.Get(board.ParentControl.GraphicsDevice, bS, infoType, no);
                if (bgInfo == null)
                {
                    Log($"Background not found: bS={bS}, type={bgType}, no={no}");
                    return false;
                }

                // Determine BackgroundType based on tiling settings
                BackgroundType displayType = BackgroundType.Regular;
                if (cx > 0 && cy > 0)
                    displayType = BackgroundType.HVTiling;
                else if (cx > 0)
                    displayType = BackgroundType.HorizontalTiling;
                else if (cy > 0)
                    displayType = BackgroundType.VerticalTiling;

                lock (board.ParentControl)
                {
                    // Create the background instance
                    var bg = (BackgroundInstance)bgInfo.CreateInstance(board, x, y, z, rx, ry, cx, cy,
                        displayType, a, front, flip, 0, null, false);

                    if (front)
                        board.BoardItems.FrontBackgrounds.Add(bg);
                    else
                        board.BoardItems.BackBackgrounds.Add(bg);

                    // Record for undo
                    board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(bg) });
                }

                Log($"Added background bS={bS} no={no} type={bgType} at ({x}, {y}) front={front}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to add background: {ex.Message}");
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

                // Lock the board during tile additions to prevent concurrent access from render thread
                lock (board.ParentControl)
                {
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
                } // End lock

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

            // Calculate endX from end_x or width parameter
            int endX;
            if (command.Parameters.TryGetValue("end_x", out var endXObj))
            {
                endX = Convert.ToInt32(endXObj);
            }
            else if (command.Parameters.TryGetValue("width", out var widthObj))
            {
                endX = startX + Convert.ToInt32(widthObj);
            }
            else
            {
                endX = startX + 200; // Default width
            }

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

                // Foothold Y position adjusted to align with edU visual top
                // Move up by edU origin.Y to match the walking surface at the top of the tile
                int footholdY = y;
                if (tiles.edU != null)
                {
                    footholdY = y - tiles.edU.Origin.Y;
                }

                switch (structureType)
                {
                    case "flat":
                        // If height is specified and > 3, use tall platform builder
                        // Otherwise use the standard 3-row flat platform
                        if (height > 3)
                            tilesAdded = BuildTallPlatform(layer, tiles, adjustedStartX, adjustedEndX, y, height);
                        else
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

                // Automatically create a foothold for flat/tall platforms at the correct Y position
                if (tilesAdded > 0 && (structureType == "flat" || structureType == "tall"))
                {
                    CreateFootholdForTileStructure(layerNum, startX, endX, footholdY);
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
                var magProp = Program.InfoManager.GetTileSet(tileset)?["info"]?["mag"];
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

        private int PlaceTile(Layer layer, TileInfo tileInfo, int x, int y, List<UndoRedoAction> undoActions = null)
        {
            if (tileInfo == null) return 0;
            lock (board.ParentControl)
            {
                var tile = (TileInstance)tileInfo.CreateInstance(layer, board, x, y, 0, layer.zMDefault, false, false);
                tile.AddToBoard(null);

                // Track for undo if list provided, otherwise create individual undo entry
                if (undoActions != null)
                {
                    undoActions.Add(UndoRedoManager.ItemAdded(tile));
                }
                else
                {
                    board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { UndoRedoManager.ItemAdded(tile) });
                }
            }
            return 1;
        }

        /// <summary>
        /// Create a foothold (platform) for a tile structure at the specified position.
        /// The foothold Y position matches the edU tile position for proper alignment.
        /// </summary>
        private void CreateFootholdForTileStructure(int layerNum, int startX, int endX, int y, List<UndoRedoAction> undoActions = null)
        {
            try
            {
                int platformNumber = GetNextPlatformNumber(layerNum);

                // Lock the board during foothold creation to prevent concurrent access from render thread
                lock (board.ParentControl)
                {
                    // Create two anchors at start and end X positions, same Y
                    var startAnchor = new FootholdAnchor(board, startX, y, layerNum, platformNumber, true);
                    var endAnchor = new FootholdAnchor(board, endX, y, layerNum, platformNumber, true);

                    board.BoardItems.FHAnchors.Add(startAnchor);
                    board.BoardItems.FHAnchors.Add(endAnchor);

                    // Create the foothold line connecting them
                    var line = new FootholdLine(board, startAnchor, endAnchor);
                    board.BoardItems.FootholdLines.Add(line);

                    // Track for undo if list provided
                    if (undoActions != null)
                    {
                        undoActions.Add(UndoRedoManager.ItemAdded(startAnchor));
                        undoActions.Add(UndoRedoManager.ItemAdded(endAnchor));
                        undoActions.Add(UndoRedoManager.LineAdded(line, startAnchor, endAnchor));
                    }
                    else
                    {
                        var actions = new List<UndoRedoAction>
                        {
                            UndoRedoManager.ItemAdded(startAnchor),
                            UndoRedoManager.ItemAdded(endAnchor),
                            UndoRedoManager.LineAdded(line, startAnchor, endAnchor)
                        };
                        board.UndoRedoMan.AddUndoBatch(actions);
                    }
                }

                Log($"Created foothold for tile structure: ({startX}, {y}) to ({endX}, {y}) on layer {layerNum}");
            }
            catch (Exception ex)
            {
                Log($"Warning: Failed to create foothold for tile structure: {ex.Message}");
            }
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

            // Handle rope/ladder removal separately
            if (command.ElementType == ElementType.Rope || command.ElementType == ElementType.Ladder)
            {
                return RemoveRopeNear(command);
            }

            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log($"No targets found for REMOVE command: {command.OriginalText}");
                return false;
            }

            lock (board.ParentControl)
            {
                foreach (var target in targets)
                {
                    target.RemoveItem(null);
                }
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

            lock (board.ParentControl)
            {
                foreach (var line in linesToRemove)
                {
                    line.Remove(false, null);  // Don't remove dots, they may be shared
                }
            }

            Log($"Removed {linesToRemove.Count} foothold(s)");
            return true;
        }

        private bool RemoveRopeNear(MapAICommand command)
        {
            if (!command.TargetX.HasValue || !command.TargetY.HasValue)
            {
                Log($"REMOVE {command.ElementType.ToString().ToLower()} requires coordinates");
                return false;
            }

            int x = command.TargetX.Value;
            int y = command.TargetY.Value;
            int tolerance = 20;

            bool isLadder = command.ElementType == ElementType.Ladder;

            // Find ropes/ladders near the specified coordinates
            var ropesToRemove = new List<Rope>();
            foreach (var rope in board.BoardItems.Ropes)
            {
                // Filter by type (rope vs ladder)
                if (rope.ladder != isLadder)
                    continue;

                // Check if the rope X is near the target X
                bool nearX = Math.Abs(rope.FirstAnchor.X - x) <= tolerance;

                // Check if the target Y is within the rope's Y range
                int topY = Math.Min(rope.FirstAnchor.Y, rope.SecondAnchor.Y);
                int bottomY = Math.Max(rope.FirstAnchor.Y, rope.SecondAnchor.Y);
                bool nearY = (y >= topY - tolerance && y <= bottomY + tolerance);

                if (nearX && nearY)
                {
                    ropesToRemove.Add(rope);
                }
            }

            if (ropesToRemove.Count == 0)
            {
                Log($"No {(isLadder ? "ladders" : "ropes")} found near ({x}, {y})");
                return false;
            }

            lock (board.ParentControl)
            {
                foreach (var rope in ropesToRemove)
                {
                    rope.Remove(null);
                }
            }

            Log($"Removed {ropesToRemove.Count} {(isLadder ? "ladder" : "rope")}(s)");
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

            lock (board.ParentControl)
            {
                foreach (var target in targets)
                {
                    int adjustedY = newY;

                    // Apply ground-snapping for portals
                    if (target is PortalInstance portal)
                    {
                        var portalInfo = portal.BaseInfo;
                        if (portalInfo != null)
                        {
                            int height = portalInfo.Height;
                            int originY = portalInfo.Origin.Y;
                            adjustedY = newY - height + originY;

                            if (adjustedY != newY)
                            {
                                Log($"Portal move adjustment: original Y={newY}, adjusted Y={adjustedY} (height={height}, origin.Y={originY})");
                            }
                        }
                    }
                    // Apply ground-snapping for objects
                    else if (target is ObjectInstance obj)
                    {
                        var objInfo = obj.BaseInfo;
                        if (objInfo != null)
                        {
                            int height = objInfo.Height;
                            int originY = objInfo.Origin.Y;
                            adjustedY = newY - height + originY;

                            if (adjustedY != newY)
                            {
                                Log($"Object move adjustment: original Y={newY}, adjusted Y={adjustedY} (height={height}, origin.Y={originY})");
                            }
                        }
                    }

                    // Record for undo
                    var actions = new List<UndoRedoAction>
                    {
                        UndoRedoManager.ItemMoved(target, new Microsoft.Xna.Framework.Point(target.X, target.Y), new Microsoft.Xna.Framework.Point(newX, adjustedY))
                    };
                    board.UndoRedoMan.AddUndoBatch(actions);

                    target.Move(newX, adjustedY);
                }
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

            lock (board.ParentControl)
            {
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

                    case ElementType.Object:
                        // Remove all objects (ObjectInstance objects from TileObjs)
                        var objects = board.BoardItems.TileObjs.OfType<ObjectInstance>().ToList();
                        clearedCount = objects.Count;
                        foreach (var obj in objects)
                            obj.RemoveItem(null);
                        break;

                    case ElementType.Background:
                        // Remove all backgrounds (both back and front)
                        var backBgs = board.BoardItems.BackBackgrounds.ToList();
                        var frontBgs = board.BoardItems.FrontBackgrounds.ToList();
                        clearedCount = backBgs.Count + frontBgs.Count;
                        foreach (var bg in backBgs)
                            bg.RemoveItem(null);
                        foreach (var bg in frontBgs)
                            bg.RemoveItem(null);
                        break;

                    case ElementType.Rope:
                        // Remove all ropes (not ladders)
                        var ropes = board.BoardItems.Ropes.Where(r => !r.ladder).ToList();
                        clearedCount = ropes.Count;
                        foreach (var rope in ropes)
                            rope.Remove(null);
                        break;

                    case ElementType.Ladder:
                        // Remove all ladders (not ropes)
                        var ladders = board.BoardItems.Ropes.Where(r => r.ladder).ToList();
                        clearedCount = ladders.Count;
                        foreach (var ladder in ladders)
                            ladder.Remove(null);
                        break;

                    default:
                        Log($"CLEAR not supported for element type: {command.ElementType}");
                        return false;
                }
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

        private bool ExecuteSetBgm(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("bgm", out var bgmObj))
            {
                Log("SET BGM requires bgm parameter");
                return false;
            }

            string bgm = bgmObj.ToString();

            // Validate the BGM exists
            if (!Program.InfoManager.BGMs.ContainsKey(bgm))
            {
                // Try to find a close match
                var availableBgms = Program.InfoManager.BGMs.Keys
                    .Where(k => k.StartsWith("Bgm", StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();
                Log($"BGM '{bgm}' not found. Available BGMs include: {string.Join(", ", availableBgms)}...");
                return false;
            }

            // Set the BGM
            string oldBgm = board.MapInfo.bgm;
            board.MapInfo.bgm = bgm;

            Log($"Changed BGM from '{oldBgm}' to '{bgm}'");
            return true;
        }

        private bool ExecuteSetMapOption(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("option", out var optionObj))
            {
                Log("SET MAP_OPTION requires option parameter");
                return false;
            }

            if (!command.Parameters.TryGetValue("value", out var valueObj))
            {
                Log("SET MAP_OPTION requires value parameter");
                return false;
            }

            string option = optionObj.ToString();
            bool value = Convert.ToBoolean(valueObj);
            var info = board.MapInfo;

            // Use reflection-like switch to set the appropriate property
            switch (option.ToLower())
            {
                case "cloud": info.cloud = value; break;
                case "snow": info.snow = value; break;
                case "rain": info.rain = value; break;
                case "swim": info.swim = value; break;
                case "fly": info.fly = value; break;
                case "town": info.town = value; break;
                case "partyonly": info.partyOnly = value; break;
                case "expeditiononly": info.expeditionOnly = value; break;
                case "nomapcmd": info.noMapCmd = value; break;
                case "hideminimap": info.hideMinimap = value; break;
                case "minimaponoff": info.miniMapOnOff = value; break;
                case "personalshop": info.personalShop = value; break;
                case "entrustedshop": info.entrustedShop = value; break;
                case "noregenmap": info.noRegenMap = value; break;
                case "blockpbosschange": info.blockPBossChange = value; break;
                case "everlast": info.everlast = value; break;
                case "damagecheckfree": info.damageCheckFree = value; break;
                case "scrolldisable": info.scrollDisable = value; break;
                case "needskillforfly": info.needSkillForFly = value; break;
                case "zakum2hack": info.zakum2Hack = value; break;
                case "allmovecheck": info.allMoveCheck = value; break;
                case "vrlimit": info.VRLimit = value; break;
                case "mirror_bottom": info.mirror_Bottom = value; break;
                default:
                    Log($"Unknown map option: {option}");
                    return false;
            }

            Log($"Set map option '{option}' to {value}");
            return true;
        }

        private bool ExecuteSetFieldLimit(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("limit", out var limitObj))
            {
                Log("SET FIELD_LIMIT requires limit parameter");
                return false;
            }

            if (!command.Parameters.TryGetValue("enabled", out var enabledObj))
            {
                Log("SET FIELD_LIMIT requires enabled parameter");
                return false;
            }

            string limitName = limitObj.ToString();
            bool enabled = Convert.ToBoolean(enabledObj);

            // Parse the field limit type from the enum
            if (!Enum.TryParse<FieldLimitType>(limitName, true, out var limitType))
            {
                Log($"Unknown field limit: {limitName}");
                return false;
            }

            int bitPosition = (int)limitType;
            long currentLimit = board.MapInfo.fieldLimit;

            if (enabled)
            {
                // Set the bit
                board.MapInfo.fieldLimit = currentLimit | (1L << bitPosition);
            }
            else
            {
                // Clear the bit
                board.MapInfo.fieldLimit = currentLimit & ~(1L << bitPosition);
            }

            Log($"Set field limit '{limitName}' to {enabled} (fieldLimit = {board.MapInfo.fieldLimit})");
            return true;
        }

        private bool ExecuteSetMapSize(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("width", out var widthObj) ||
                !command.Parameters.TryGetValue("height", out var heightObj))
            {
                Log("SET MAP_SIZE requires both width and height parameters");
                return false;
            }

            int width = Convert.ToInt32(widthObj);
            int height = Convert.ToInt32(heightObj);

            // Validate dimensions (reasonable bounds for MapleStory maps)
            if (width < 600 || width > 5000)
            {
                Log($"Map width {width} is out of range (600-5000)");
                return false;
            }
            if (height < 400 || height > 5000)
            {
                Log($"Map height {height} is out of range (400-5000)");
                return false;
            }

            var oldSize = board.MapSize;
            board.MapSize = new Microsoft.Xna.Framework.Point(width, height);

            Log($"Changed map size from ({oldSize.X}, {oldSize.Y}) to ({width}, {height})");
            return true;
        }

        private bool ExecuteSetVR(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("left", out var leftObj) ||
                !command.Parameters.TryGetValue("top", out var topObj) ||
                !command.Parameters.TryGetValue("right", out var rightObj) ||
                !command.Parameters.TryGetValue("bottom", out var bottomObj))
            {
                Log("SET VR requires left, top, right, and bottom parameters");
                return false;
            }

            int left = Convert.ToInt32(leftObj);
            int top = Convert.ToInt32(topObj);
            int right = Convert.ToInt32(rightObj);
            int bottom = Convert.ToInt32(bottomObj);

            // Validate VR bounds
            if (right <= left)
            {
                Log($"VR right ({right}) must be greater than left ({left})");
                return false;
            }
            if (bottom <= top)
            {
                Log($"VR bottom ({bottom}) must be greater than top ({top})");
                return false;
            }

            // Calculate dimensions from bounds
            int width = right - left;
            int height = bottom - top;

            lock (board.ParentControl)
            {
                // Remove existing VR if present (VRRectangle properties are read-only, so recreate)
                if (board.VRRectangle != null)
                {
                    board.VRRectangle.RemoveItem(null);
                }

                // Create new VR rectangle
                board.VRRectangle = new VRRectangle(board, new Microsoft.Xna.Framework.Rectangle(left, top, width, height));
            }

            Log($"Set VR to left={left}, top={top}, right={right}, bottom={bottom} (width={width}, height={height})");
            return true;
        }

        private bool ExecuteClearVR(MapAICommand command)
        {
            if (board.VRRectangle == null)
            {
                Log("VR is already not set");
                return true;
            }

            lock (board.ParentControl)
            {
                // Remove VR rectangle properly
                board.VRRectangle.RemoveItem(null);
            }

            Log("Cleared VR (viewing range)");
            return true;
        }

        #endregion

        #region New Map Property Commands

        private bool ExecuteSetReturnMap(MapAICommand command)
        {
            var info = board.MapInfo;

            if (command.Parameters.TryGetValue("return", out var returnObj))
            {
                info.returnMap = Convert.ToInt32(returnObj);
                Log($"Set returnMap to {info.returnMap}");
            }

            if (command.Parameters.TryGetValue("forced", out var forcedObj))
            {
                info.forcedReturn = Convert.ToInt32(forcedObj);
                Log($"Set forcedReturn to {info.forcedReturn}");
            }

            return true;
        }

        private bool ExecuteSetMobRate(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("rate", out var rateObj))
            {
                Log("SetMobRate requires 'rate' parameter");
                return false;
            }

            board.MapInfo.mobRate = Convert.ToSingle(rateObj);
            Log($"Set mobRate to {board.MapInfo.mobRate}");
            return true;
        }

        private bool ExecuteSetFieldType(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("type", out var typeObj))
            {
                Log("SetFieldType requires 'type' parameter");
                return false;
            }

            if (typeObj is string typeStr && Enum.TryParse<FieldType>(typeStr, true, out var fieldType))
            {
                board.MapInfo.fieldType = fieldType;
                Log($"Set fieldType to {fieldType}");
                return true;
            }
            else if (int.TryParse(typeObj.ToString(), out int typeInt))
            {
                board.MapInfo.fieldType = (FieldType)typeInt;
                Log($"Set fieldType to {(FieldType)typeInt}");
                return true;
            }

            Log($"Invalid field type: {typeObj}");
            return false;
        }

        private bool ExecuteSetTimeLimit(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("seconds", out var secondsObj))
            {
                // Allow clearing
                if (command.Parameters.TryGetValue("clear", out var clearObj) && Convert.ToBoolean(clearObj))
                {
                    board.MapInfo.timeLimit = null;
                    Log("Cleared timeLimit");
                    return true;
                }
                Log("SetTimeLimit requires 'seconds' parameter");
                return false;
            }

            board.MapInfo.timeLimit = Convert.ToInt32(secondsObj);
            Log($"Set timeLimit to {board.MapInfo.timeLimit} seconds");
            return true;
        }

        private bool ExecuteSetLevelLimit(MapAICommand command)
        {
            if (command.Parameters.TryGetValue("min", out var minObj))
            {
                board.MapInfo.lvLimit = Convert.ToInt32(minObj);
                Log($"Set lvLimit to {board.MapInfo.lvLimit}");
            }

            if (command.Parameters.TryGetValue("force", out var forceObj))
            {
                board.MapInfo.lvForceMove = Convert.ToInt32(forceObj);
                Log($"Set lvForceMove to {board.MapInfo.lvForceMove}");
            }

            return true;
        }

        private bool ExecuteSetScript(MapAICommand command)
        {
            if (command.Parameters.TryGetValue("onUserEnter", out var onUserObj))
            {
                board.MapInfo.onUserEnter = onUserObj?.ToString();
                Log($"Set onUserEnter to \"{board.MapInfo.onUserEnter}\"");
            }

            if (command.Parameters.TryGetValue("onFirstUserEnter", out var onFirstObj))
            {
                board.MapInfo.onFirstUserEnter = onFirstObj?.ToString();
                Log($"Set onFirstUserEnter to \"{board.MapInfo.onFirstUserEnter}\"");
            }

            return true;
        }

        private bool ExecuteSetEffect(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("effect", out var effectObj))
            {
                Log("SetEffect requires 'effect' parameter");
                return false;
            }

            board.MapInfo.effect = effectObj?.ToString();
            Log($"Set effect to \"{board.MapInfo.effect}\"");
            return true;
        }

        private bool ExecuteSetHelp(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("text", out var textObj))
            {
                Log("SetHelp requires 'text' parameter");
                return false;
            }

            board.MapInfo.help = textObj?.ToString();
            Log($"Set help text to \"{board.MapInfo.help}\"");
            return true;
        }

        private bool ExecuteSetMapDesc(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("desc", out var descObj))
            {
                Log("SetMapDesc requires 'desc' parameter");
                return false;
            }

            board.MapInfo.mapDesc = descObj?.ToString();
            Log($"Set map description to \"{board.MapInfo.mapDesc}\"");
            return true;
        }

        private bool ExecuteSetDropSettings(MapAICommand command)
        {
            if (command.Parameters.TryGetValue("expire", out var expireObj))
            {
                board.MapInfo.dropExpire = Convert.ToInt32(expireObj);
                Log($"Set dropExpire to {board.MapInfo.dropExpire} seconds");
            }

            if (command.Parameters.TryGetValue("rate", out var rateObj))
            {
                board.MapInfo.dropRate = Convert.ToSingle(rateObj);
                Log($"Set dropRate to {board.MapInfo.dropRate}");
            }

            return true;
        }

        private bool ExecuteSetDecaySettings(MapAICommand command)
        {
            if (command.Parameters.TryGetValue("hp", out var hpObj))
            {
                board.MapInfo.decHP = Convert.ToInt32(hpObj);
                Log($"Set decHP to {board.MapInfo.decHP}");
            }

            if (command.Parameters.TryGetValue("interval", out var intervalObj))
            {
                board.MapInfo.decInterval = Convert.ToInt32(intervalObj);
                Log($"Set decInterval to {board.MapInfo.decInterval}ms");
            }

            return true;
        }

        private bool ExecuteSetRecovery(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("rate", out var rateObj))
            {
                Log("SetRecovery requires 'rate' parameter");
                return false;
            }

            board.MapInfo.recovery = Convert.ToSingle(rateObj);
            Log($"Set recovery rate to {board.MapInfo.recovery}");
            return true;
        }

        #endregion

        #region Minimap Commands

        private bool ExecuteSetMinimapRect(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("left", out var leftObj) ||
                !command.Parameters.TryGetValue("top", out var topObj) ||
                !command.Parameters.TryGetValue("right", out var rightObj) ||
                !command.Parameters.TryGetValue("bottom", out var bottomObj))
            {
                Log("SetMinimapRect requires left, top, right, bottom parameters");
                return false;
            }

            int left = Convert.ToInt32(leftObj);
            int top = Convert.ToInt32(topObj);
            int right = Convert.ToInt32(rightObj);
            int bottom = Convert.ToInt32(bottomObj);
            int width = right - left;
            int height = bottom - top;

            if (width <= 0 || height <= 0)
            {
                Log($"Invalid minimap dimensions: {width}x{height}");
                return false;
            }

            lock (board.ParentControl)
            {
                if (board.MinimapRectangle != null)
                {
                    board.MinimapRectangle.RemoveItem(null);
                }

                board.MinimapRectangle = new MinimapRectangle(board, new Microsoft.Xna.Framework.Rectangle(left, top, width, height));
            }

            Log($"Set minimap rect to left={left}, top={top}, right={right}, bottom={bottom}");
            return true;
        }

        private bool ExecuteClearMinimapRect(MapAICommand command)
        {
            if (board.MinimapRectangle == null)
            {
                Log("Minimap rect is already not set");
                return true;
            }

            lock (board.ParentControl)
            {
                board.MinimapRectangle.RemoveItem(null);
            }

            Log("Cleared minimap rect");
            return true;
        }

        #endregion

        #region Life/Spawn Commands

        private bool ExecuteSetPatrolRange(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log("No matching life instances found for SetPatrolRange");
                return false;
            }

            int rx0 = 0, rx1 = 0;
            if (command.Parameters.TryGetValue("rx0", out var rx0Obj))
                rx0 = Convert.ToInt32(rx0Obj);
            if (command.Parameters.TryGetValue("rx1", out var rx1Obj))
                rx1 = Convert.ToInt32(rx1Obj);

            int count = 0;
            foreach (var target in targets)
            {
                if (target is LifeInstance life)
                {
                    life.rx0Shift = rx0;
                    life.rx1Shift = rx1;
                    count++;
                }
            }

            Log($"Set patrol range [{rx0}, {rx1}] on {count} life instance(s)");
            return true;
        }

        private bool ExecuteSetRespawnTime(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log("No matching life instances found for SetRespawnTime");
                return false;
            }

            if (!command.Parameters.TryGetValue("time", out var timeObj))
            {
                Log("SetRespawnTime requires 'time' parameter");
                return false;
            }

            int time = Convert.ToInt32(timeObj);
            int count = 0;
            foreach (var target in targets)
            {
                if (target is LifeInstance life)
                {
                    life.MobTime = time;
                    count++;
                }
            }

            Log($"Set respawn time to {time}ms on {count} life instance(s)");
            return true;
        }

        private bool ExecuteSetTeam(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log("No matching life instances found for SetTeam");
                return false;
            }

            if (!command.Parameters.TryGetValue("team", out var teamObj))
            {
                Log("SetTeam requires 'team' parameter");
                return false;
            }

            int team = Convert.ToInt32(teamObj);
            int count = 0;
            foreach (var target in targets)
            {
                if (target is LifeInstance life)
                {
                    life.Team = team;
                    count++;
                }
            }

            Log($"Set team to {team} on {count} life instance(s)");
            return true;
        }

        #endregion

        #region Layer Management Commands

        private bool ExecuteSetLayerTileset(MapAICommand command)
        {
            if (!command.Parameters.TryGetValue("layer", out var layerObj))
            {
                Log("SetLayerTileset requires 'layer' parameter");
                return false;
            }

            if (!command.Parameters.TryGetValue("tileset", out var tilesetObj))
            {
                Log("SetLayerTileset requires 'tileset' parameter");
                return false;
            }

            int layerNum = Convert.ToInt32(layerObj);
            string tileset = tilesetObj.ToString();

            if (layerNum < 0 || layerNum >= board.Layers.Count)
            {
                Log($"Invalid layer number: {layerNum}");
                return false;
            }

            board.Layers[layerNum].tS = tileset;
            Log($"Set layer {layerNum} tileset to \"{tileset}\"");
            return true;
        }

        #endregion

        #region Z-Order Commands

        private bool ExecuteSetZ(MapAICommand command)
        {
            var targets = FindTargets(command);
            if (targets.Count == 0)
            {
                Log("No matching items found for SetZ");
                return false;
            }

            if (!command.Parameters.TryGetValue("z", out var zObj))
            {
                Log("SetZ requires 'z' parameter");
                return false;
            }

            int z = Convert.ToInt32(zObj);
            int count = 0;
            foreach (var target in targets)
            {
                target.Z = z;
                count++;
            }

            Log($"Set Z to {z} on {count} item(s)");
            return true;
        }

        #endregion

        #region Rename Command

        private bool ExecuteRename(MapAICommand command)
        {
            if (command.ElementType != ElementType.Portal)
            {
                Log("Rename currently only supports portals");
                return false;
            }

            if (!command.Parameters.TryGetValue("from", out var fromObj) ||
                !command.Parameters.TryGetValue("to", out var toObj))
            {
                Log("Rename requires 'from' and 'to' parameters");
                return false;
            }

            string fromName = fromObj.ToString();
            string toName = toObj.ToString();

            var portal = board.BoardItems.Portals.FirstOrDefault(p =>
                p.pn.Equals(fromName, StringComparison.OrdinalIgnoreCase));

            if (portal == null)
            {
                Log($"Portal \"{fromName}\" not found");
                return false;
            }

            portal.pn = toName;
            Log($"Renamed portal \"{fromName}\" to \"{toName}\"");
            return true;
        }

        #endregion

        #region ToolTip Commands

        private bool ExecuteAddToolTip(MapAICommand command)
        {
            // Required parameters
            if (!command.TargetX.HasValue || !command.TargetY.HasValue)
            {
                Log("AddToolTip requires position (x, y)");
                return false;
            }

            int x = command.TargetX.Value;
            int y = command.TargetY.Value;

            // Get size (default to 200x100)
            int width = 200;
            int height = 100;
            if (command.Parameters.TryGetValue("width", out var widthObj))
                width = Convert.ToInt32(widthObj);
            if (command.Parameters.TryGetValue("height", out var heightObj))
                height = Convert.ToInt32(heightObj);

            // Get title and description
            string title = "";
            string desc = "";
            if (command.Parameters.TryGetValue("title", out var titleObj))
                title = titleObj?.ToString() ?? "";
            if (command.Parameters.TryGetValue("desc", out var descObj))
                desc = descObj?.ToString() ?? "";

            lock (board.ParentControl)
            {
                var rect = new Microsoft.Xna.Framework.Rectangle(x, y, width, height);
                var tooltip = new ToolTipInstance(board, rect, title, desc);

                List<UndoRedo.UndoRedoAction> undoPipe = new List<UndoRedo.UndoRedoAction>();
                tooltip.OnItemPlaced(undoPipe);
                board.BoardItems.ToolTips.Add(tooltip);
                board.UndoRedoMan.AddUndoBatch(undoPipe);
            }

            Log($"Added tooltip \"{title}\" at ({x}, {y}) size=({width}x{height})");
            return true;
        }

        private bool ExecuteRemoveToolTip(MapAICommand command)
        {
            ToolTipInstance targetTooltip = null;

            // Find by position
            if (command.TargetX.HasValue && command.TargetY.HasValue)
            {
                int x = command.TargetX.Value;
                int y = command.TargetY.Value;
                targetTooltip = board.BoardItems.ToolTips.FirstOrDefault(t =>
                    Math.Abs(t.X - x) < 50 && Math.Abs(t.Y - y) < 50);
            }
            // Find by title
            else if (command.Parameters.TryGetValue("title", out var titleObj))
            {
                string title = titleObj.ToString();
                targetTooltip = board.BoardItems.ToolTips.FirstOrDefault(t =>
                    t.Title != null && t.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            }
            // Find by index
            else if (command.Parameters.TryGetValue("index", out var indexObj))
            {
                int index = Convert.ToInt32(indexObj);
                if (index >= 0 && index < board.BoardItems.ToolTips.Count)
                    targetTooltip = board.BoardItems.ToolTips[index];
            }

            if (targetTooltip == null)
            {
                Log("No matching tooltip found to remove");
                return false;
            }

            lock (board.ParentControl)
            {
                targetTooltip.RemoveItem(null);
            }

            Log($"Removed tooltip \"{targetTooltip.Title}\"");
            return true;
        }

        private bool ExecuteModifyToolTip(MapAICommand command)
        {
            ToolTipInstance targetTooltip = null;

            // Find by position
            if (command.TargetX.HasValue && command.TargetY.HasValue)
            {
                int x = command.TargetX.Value;
                int y = command.TargetY.Value;
                targetTooltip = board.BoardItems.ToolTips.FirstOrDefault(t =>
                    Math.Abs(t.X - x) < 50 && Math.Abs(t.Y - y) < 50);
            }
            // Find by old title
            else if (command.Parameters.TryGetValue("old_title", out var oldTitleObj))
            {
                string oldTitle = oldTitleObj.ToString();
                targetTooltip = board.BoardItems.ToolTips.FirstOrDefault(t =>
                    t.Title != null && t.Title.Equals(oldTitle, StringComparison.OrdinalIgnoreCase));
            }
            // Find by index
            else if (command.Parameters.TryGetValue("index", out var indexObj))
            {
                int index = Convert.ToInt32(indexObj);
                if (index >= 0 && index < board.BoardItems.ToolTips.Count)
                    targetTooltip = board.BoardItems.ToolTips[index];
            }

            if (targetTooltip == null)
            {
                Log("No matching tooltip found to modify");
                return false;
            }

            // Apply modifications
            if (command.Parameters.TryGetValue("title", out var newTitleObj))
            {
                targetTooltip.Title = newTitleObj?.ToString() ?? "";
                Log($"Set tooltip title to \"{targetTooltip.Title}\"");
            }

            if (command.Parameters.TryGetValue("desc", out var newDescObj))
            {
                targetTooltip.Desc = newDescObj?.ToString() ?? "";
                Log($"Set tooltip description to \"{targetTooltip.Desc}\"");
            }

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
