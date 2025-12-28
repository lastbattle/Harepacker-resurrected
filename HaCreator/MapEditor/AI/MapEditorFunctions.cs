/* Copyright (C) 2024 HaCreator AI Extension
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Defines the function schemas for AI function calling
    /// </summary>
    public static class MapEditorFunctions
    {
        /// <summary>
        /// Get all available tool definitions for OpenRouter function calling
        /// </summary>
        public static JArray GetToolDefinitions()
        {
            return new JArray
            {
                CreateAddMobTool(),
                CreateAddNpcTool(),
                CreateAddPortalTool(),
                CreateAddChairTool(),
                CreateAddPlatformTool(),
                CreateAddWallTool(),
                CreateAddObjectTool(),
                CreateAddBackgroundTool(),
                CreateAddTileTool(),
                CreateTilePlatformTool(),
                CreateTileStructureTool(),
                CreateRemoveElementTool(),
                CreateMoveElementTool(),
                CreateModifyPortalTool(),
                CreateFlipElementTool(),
                CreateClearElementsTool(),
                CreateGetObjectInfoTool(),
                CreateGetBackgroundInfoTool(),
                CreateGetBgmListTool(),
                CreateSetBgmTool(),
                CreateAddRopeTool(),
                CreateAddLadderTool()
            };
        }

        /// <summary>
        /// Check if a function is a query function (returns data rather than executing a command)
        /// </summary>
        public static bool IsQueryFunction(string functionName)
        {
            return functionName == "get_object_info" || functionName == "get_background_info" || functionName == "get_bgm_list";
        }

        /// <summary>
        /// Execute a query function and return the result
        /// </summary>
        public static string ExecuteQueryFunction(string functionName, JObject arguments)
        {
            switch (functionName)
            {
                case "get_object_info":
                    var oS = arguments["oS"]?.ToString();
                    if (string.IsNullOrEmpty(oS))
                        return "Error: oS (object set name) is required.";
                    return MapAssetCatalog.GetObjectSetDetails(oS);

                case "get_background_info":
                    var bS = arguments["bS"]?.ToString();
                    if (string.IsNullOrEmpty(bS))
                        return "Error: bS (background set name) is required.";
                    return MapAssetCatalog.GetBackgroundSetDetails(bS);

                case "get_bgm_list":
                    return MapAssetCatalog.GetBgmList();

                default:
                    return $"Unknown query function: {functionName}";
            }
        }

        private static JObject CreateAddMobTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_mob",
                    ["description"] = "Add a monster spawn point to the map",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["mob_id"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The mob ID (e.g., '100100' for Blue Snail, '1210100' for Slime)"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the mob spawn"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the mob spawn"
                            },
                            ["flip"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether the mob faces left (true) or right (false)"
                            },
                            ["respawn_time"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Respawn time in milliseconds (optional)"
                            }
                        },
                        ["required"] = new JArray { "mob_id", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateAddNpcTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_npc",
                    ["description"] = "Add an NPC to the map",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["npc_id"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The NPC ID (e.g., '9000000' for Maple Administrator)"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the NPC"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the NPC"
                            },
                            ["flip"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether the NPC faces left (true) or right (false)"
                            }
                        },
                        ["required"] = new JArray { "npc_id", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateAddPortalTool()
        {
            // Dynamically get all portal types from the PortalType enum
            var portalTypes = Enum.GetNames(typeof(PortalType));
            var portalTypeEnum = new JArray(portalTypes);

            // Build description with friendly names
            var portalDescriptions = new List<string>();
            foreach (PortalType pt in Enum.GetValues(typeof(PortalType)))
            {
                try
                {
                    var friendlyName = pt.GetFriendlyName();
                    var code = pt.ToCode();
                    portalDescriptions.Add($"{pt} ({code}): {friendlyName}");
                }
                catch { }
            }
            var portalTypeDescription = "Type of portal. Available types:\n" + string.Join("\n", portalDescriptions);

            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_portal",
                    ["description"] = "Add a portal to the map",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["name"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Portal name (e.g., 'sp' for spawn, 'portal1', 'toTown')"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the portal"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the portal"
                            },
                            ["portal_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = portalTypeEnum,
                                ["description"] = portalTypeDescription
                            },
                            ["target_map"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Target map ID (e.g., 100000000 for Henesys)"
                            },
                            ["target_portal"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the portal in the target map to spawn at"
                            }
                        },
                        ["required"] = new JArray { "name", "x", "y", "portal_type" }
                    }
                }
            };
        }

        private static JObject CreateAddChairTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_chair",
                    ["description"] = "Add a chair (sitting spot) to the map",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the chair"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the chair"
                            }
                        },
                        ["required"] = new JArray { "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateAddPlatformTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_platform",
                    ["description"] = "Add a horizontal platform (foothold) that players can stand on. Creates a walkable surface between two or more points.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["points"] = new JObject
                            {
                                ["type"] = "array",
                                ["description"] = "Array of points defining the platform. Each point has x and y coordinates. Minimum 2 points required. Points are connected left-to-right.",
                                ["items"] = new JObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JObject
                                    {
                                        ["x"] = new JObject { ["type"] = "integer", ["description"] = "X coordinate" },
                                        ["y"] = new JObject { ["type"] = "integer", ["description"] = "Y coordinate" }
                                    },
                                    ["required"] = new JArray { "x", "y" }
                                },
                                ["minItems"] = 2
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0. Higher layers render on top."
                            },
                            ["forbid_fall_down"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, players cannot fall through this platform by pressing down. Default is false."
                            },
                            ["cant_through"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, players cannot jump through this platform from below. Default is false."
                            }
                        },
                        ["required"] = new JArray { "points" }
                    }
                }
            };
        }

        private static JObject CreateAddWallTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_wall",
                    ["description"] = "Add a vertical wall (foothold) that blocks horizontal player movement. Creates a barrier between two points at the same X coordinate.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the wall"
                            },
                            ["top_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the top of the wall (smaller Y value)"
                            },
                            ["bottom_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the bottom of the wall (larger Y value)"
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "x", "top_y", "bottom_y" }
                    }
                }
            };
        }

        private static JObject CreateAddObjectTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_object",
                    ["description"] = "Add a decorative object to the map. Objects are visual elements like trees, rocks, signs, etc. The Y coordinate is where the BOTTOM of the object will be placed (ground-snapped by default). Check 'Object Sets' in map context for available objects.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["oS"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Object set name (e.g., 'acc1', 'Christmas', 'citySG'). Check Available Object Sets in map context."
                            },
                            ["l0"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Level 0 category within the object set"
                            },
                            ["l1"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Level 1 subcategory within l0"
                            },
                            ["l2"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Level 2 object number (e.g., '0', '1', '2')"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the object"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the object. By default, this is where the BOTTOM of the object will be (ground level)."
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            },
                            ["z"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Z-order within the layer. Default is 0."
                            },
                            ["flip"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether to flip the object horizontally. Default is false."
                            },
                            ["raw_position"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, place the object's origin at the exact Y coordinate (no ground-snapping adjustment). Default is false."
                            }
                        },
                        ["required"] = new JArray { "oS", "l0", "l1", "l2", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateAddBackgroundTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_background",
                    ["description"] = "Add a background image to the map. Backgrounds can be static, animated, or spine-based. Check 'Background Sets' in map context for available backgrounds.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["bS"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Background set name (e.g., 'Amoria', 'aquarium', 'Christmas'). Check Available Background Sets in map context."
                            },
                            ["no"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Background number within the set (e.g., '0', '1', '2')"
                            },
                            ["type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "back", "ani", "spine" },
                                ["description"] = "Background type: 'back' (static), 'ani' (animated), 'spine' (skeletal animation)"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the background"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the background"
                            },
                            ["rx"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Horizontal parallax factor (0=fixed, 100=moves with camera). Default is 0."
                            },
                            ["ry"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Vertical parallax factor (0=fixed, 100=moves with camera). Default is 0."
                            },
                            ["cx"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Horizontal tiling (0=no tile, >0=tile width). Default is 0."
                            },
                            ["cy"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Vertical tiling (0=no tile, >0=tile height). Default is 0."
                            },
                            ["a"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Alpha/opacity (0-255). Default is 255 (fully opaque)."
                            },
                            ["z"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Z-order for layering backgrounds. Default is 0."
                            },
                            ["front"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "If true, places in front layer (renders above map). Default is false (back layer)."
                            },
                            ["flip"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Whether to flip the background horizontally. Default is false."
                            }
                        },
                        ["required"] = new JArray { "bS", "no", "type", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateRemoveElementTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "remove_element",
                    ["description"] = "Remove an element from the map by type and location or name",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["element_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "mob", "npc", "portal", "reactor", "chair", "object", "foothold", "rope", "ladder" },
                                ["description"] = "Type of element to remove"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the element (optional if name is provided)"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate of the element (optional if name is provided)"
                            },
                            ["name"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the element (for portals)"
                            }
                        },
                        ["required"] = new JArray { "element_type" }
                    }
                }
            };
        }

        private static JObject CreateMoveElementTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "move_element",
                    ["description"] = "Move an element to a new position",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["element_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "mob", "npc", "portal", "reactor", "chair", "object", "rope", "ladder" },
                                ["description"] = "Type of element to move"
                            },
                            ["from_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Current X coordinate (optional if name is provided)"
                            },
                            ["from_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Current Y coordinate (optional if name is provided)"
                            },
                            ["name"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the element (for portals)"
                            },
                            ["to_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "New X coordinate"
                            },
                            ["to_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "New Y coordinate"
                            }
                        },
                        ["required"] = new JArray { "element_type", "to_x", "to_y" }
                    }
                }
            };
        }

        private static JObject CreateModifyPortalTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "modify_portal",
                    ["description"] = "Modify properties of an existing portal",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["name"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Name of the portal to modify"
                            },
                            ["target_map"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "New target map ID"
                            },
                            ["target_portal"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "New target portal name"
                            },
                            ["script"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Script to run when portal is used"
                            }
                        },
                        ["required"] = new JArray { "name" }
                    }
                }
            };
        }

        private static JObject CreateFlipElementTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "flip_element",
                    ["description"] = "Flip an element horizontally (change facing direction)",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["element_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "mob", "npc", "object" },
                                ["description"] = "Type of element to flip"
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the element"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate of the element"
                            }
                        },
                        ["required"] = new JArray { "element_type", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateClearElementsTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "clear_elements",
                    ["description"] = "Remove all elements of a specific type from the map",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["element_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "mob", "npc", "portal", "reactor", "chair", "foothold", "tile", "object", "background", "rope", "ladder" },
                                ["description"] = "Type of elements to clear"
                            }
                        },
                        ["required"] = new JArray { "element_type" }
                    }
                }
            };
        }

        private static JObject CreateGetObjectInfoTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "get_object_info",
                    ["description"] = "Query detailed information about an object set including available paths (l0/l1/l2) and dimensions. Call this before using add_object to get valid paths.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["oS"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Object set name to query (e.g., 'acc1', 'Christmas', 'citySG'). Must be from the Available Object Sets list."
                            }
                        },
                        ["required"] = new JArray { "oS" }
                    }
                }
            };
        }

        private static JObject CreateGetBackgroundInfoTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "get_background_info",
                    ["description"] = "Query detailed information about a background set including available items by type (back/ani/spine) and dimensions. Call this before using add_background to get valid items.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["bS"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Background set name to query (e.g., 'Amoria', 'aquarium', 'Christmas'). Must be from the Available Background Sets list."
                            }
                        },
                        ["required"] = new JArray { "bS" }
                    }
                }
            };
        }

        private static JObject CreateGetBgmListTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "get_bgm_list",
                    ["description"] = "Query the list of available background music (BGM) tracks. Call this before using set_bgm to see valid BGM paths.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject { },
                        ["required"] = new JArray { }
                    }
                }
            };
        }

        private static JObject CreateSetBgmTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "set_bgm",
                    ["description"] = "Change the background music (BGM) for the current map. Use get_bgm_list first to see available BGM paths.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["bgm"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The BGM path to set (e.g., 'Bgm00/GoPicnic', 'Bgm14/Mushroom'). Must be from the available BGM list."
                            }
                        },
                        ["required"] = new JArray { "bgm" }
                    }
                }
            };
        }

        private static JObject CreateAddTileTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_tile",
                    ["description"] = "Add a single tile to the map. For tiling an entire platform, use tile_platform instead. Common tile categories: enH0/enH1 (horizontal ends), bsc (basic/fill), slLU/slRU (slopes).",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["tileset"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The tileset name (e.g., 'grassySoil', 'deepMine', 'snowyRord'). Check Available Tilesets in map context."
                            },
                            ["category"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The tile category: 'bsc' (basic/fill), 'enH0'/'enH1' (horizontal ends), 'slLU'/'slRU' (slopes)"
                            },
                            ["tile_no"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The tile number within the category (e.g., '0', '1', '2'). Default is '0'."
                            },
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the tile"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the tile"
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "tileset", "category", "x", "y" }
                    }
                }
            };
        }

        private static JObject CreateTilePlatformTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "tile_platform",
                    ["description"] = "Automatically tile an entire horizontal platform with proper spacing. Places end tiles at the edges and fill tiles in the middle. Use this instead of manually placing multiple add_tile calls.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["tileset"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The tileset name (e.g., 'grassySoil', 'deepMine', 'snowyRord'). Check Available Tilesets in map context."
                            },
                            ["start_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the left edge of the platform"
                            },
                            ["end_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the right edge of the platform"
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate of the platform"
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "tileset", "start_x", "end_x", "y" }
                    }
                }
            };
        }

        private static JObject CreateTileStructureTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "tile_structure",
                    ["description"] = "Build a complete tile structure with proper tile connections. The code handles all tile placement rules automatically. Just specify position, size, and structure type.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["tileset"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "The tileset name (e.g., 'grassySoil', 'deepMine', 'snowyRord'). Check Available Tilesets in map context."
                            },
                            ["structure_type"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray { "flat", "tall", "pillar", "slope_up_left", "slope_up_right", "slope_down_left", "slope_down_right", "staircase_right", "staircase_left" },
                                ["description"] = "Type of structure: 'flat' (2-row basic platform), 'tall' (multi-row with fill), 'pillar' (1-tile wide column), 'slope_up_left/right' (platform with slope going up), 'slope_down_left/right' (platform with slope going down), 'staircase_left/right' (stepping platforms)"
                            },
                            ["start_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the left edge"
                            },
                            ["end_x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate of the right edge. Use either end_x OR width, not both. Ignored for pillar."
                            },
                            ["width"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Width in pixels from start_x. Alternative to end_x. Ignored for pillar."
                            },
                            ["y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate of the top surface"
                            },
                            ["height"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Height in tile rows for 'tall' and 'pillar' (default 3). For staircase, this is the number of steps."
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "tileset", "structure_type", "start_x", "y" }
                    }
                }
            };
        }

        private static JObject CreateAddRopeTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_rope",
                    ["description"] = "Add a rope that players can climb. Ropes are vertical elements that allow vertical movement.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the rope"
                            },
                            ["top_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the top of the rope (smaller Y value)"
                            },
                            ["bottom_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the bottom of the rope (larger Y value)"
                            },
                            ["uf"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Upper foothold - if true, player can climb over the top of the rope. Default is true."
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "x", "top_y", "bottom_y" }
                    }
                }
            };
        }

        private static JObject CreateAddLadderTool()
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "add_ladder",
                    ["description"] = "Add a ladder that players can climb. Ladders are vertical elements similar to ropes but with a ladder appearance.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["x"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "X coordinate for the ladder"
                            },
                            ["top_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the top of the ladder (smaller Y value)"
                            },
                            ["bottom_y"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Y coordinate for the bottom of the ladder (larger Y value)"
                            },
                            ["uf"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Upper foothold - if true, player can climb over the top of the ladder. Default is true."
                            },
                            ["layer"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Layer number (0-7). Default is 0."
                            }
                        },
                        ["required"] = new JArray { "x", "top_y", "bottom_y" }
                    }
                }
            };
        }

        /// <summary>
        /// Convert a function call result to a command string for the existing parser
        /// </summary>
        public static string FunctionCallToCommand(string functionName, JObject arguments)
        {
            switch (functionName)
            {
                case "add_mob":
                    var mobCmd = $"ADD MOB {arguments["mob_id"]} at ({arguments["x"]}, {arguments["y"]})";
                    if (arguments["flip"]?.Value<bool>() == true)
                        mobCmd += " facing left";
                    if (arguments["respawn_time"] != null)
                        mobCmd += $" respawn_time={arguments["respawn_time"]}";
                    return mobCmd;

                case "add_npc":
                    var npcCmd = $"ADD NPC {arguments["npc_id"]} at ({arguments["x"]}, {arguments["y"]})";
                    if (arguments["flip"]?.Value<bool>() == true)
                        npcCmd += " facing left";
                    return npcCmd;

                case "add_portal":
                    var portalCmd = $"ADD PORTAL \"{arguments["name"]}\" at ({arguments["x"]}, {arguments["y"]}) type={arguments["portal_type"]}";
                    if (arguments["target_map"] != null)
                        portalCmd += $" target_map={arguments["target_map"]}";
                    if (arguments["target_portal"] != null)
                        portalCmd += $" target_name=\"{arguments["target_portal"]}\"";
                    return portalCmd;

                case "add_chair":
                    return $"ADD CHAIR at ({arguments["x"]}, {arguments["y"]})";

                case "add_platform":
                    var points = arguments["points"] as JArray;
                    if (points == null || points.Count < 2)
                        return "# add_platform requires at least 2 points";
                    var pointsStr = string.Join(", ", points.Select(p => $"({p["x"]}, {p["y"]})"));
                    var platformCmd = $"ADD PLATFORM [{pointsStr}]";
                    if (arguments["layer"] != null)
                        platformCmd += $" layer={arguments["layer"]}";
                    if (arguments["forbid_fall_down"]?.Value<bool>() == true)
                        platformCmd += " forbid_fall_down";
                    if (arguments["cant_through"]?.Value<bool>() == true)
                        platformCmd += " cant_through";
                    return platformCmd;

                case "add_wall":
                    var wallCmd = $"ADD WALL at x={arguments["x"]} from y={arguments["top_y"]} to y={arguments["bottom_y"]}";
                    if (arguments["layer"] != null)
                        wallCmd += $" layer={arguments["layer"]}";
                    return wallCmd;

                case "add_object":
                    var objCmd = $"ADD OBJECT oS=\"{arguments["oS"]}\" l0=\"{arguments["l0"]}\" l1=\"{arguments["l1"]}\" l2=\"{arguments["l2"]}\" at ({arguments["x"]}, {arguments["y"]})";
                    if (arguments["layer"] != null)
                        objCmd += $" layer={arguments["layer"]}";
                    if (arguments["z"] != null)
                        objCmd += $" z={arguments["z"]}";
                    if (arguments["flip"]?.Value<bool>() == true)
                        objCmd += " flip";
                    if (arguments["raw_position"]?.Value<bool>() == true)
                        objCmd += " raw_position";
                    return objCmd;

                case "add_background":
                    var bgCmd = $"ADD BACKGROUND bS=\"{arguments["bS"]}\" no=\"{arguments["no"]}\" type=\"{arguments["type"]}\" at ({arguments["x"]}, {arguments["y"]})";
                    if (arguments["rx"] != null)
                        bgCmd += $" rx={arguments["rx"]}";
                    if (arguments["ry"] != null)
                        bgCmd += $" ry={arguments["ry"]}";
                    if (arguments["cx"] != null)
                        bgCmd += $" cx={arguments["cx"]}";
                    if (arguments["cy"] != null)
                        bgCmd += $" cy={arguments["cy"]}";
                    if (arguments["a"] != null)
                        bgCmd += $" a={arguments["a"]}";
                    if (arguments["z"] != null)
                        bgCmd += $" z={arguments["z"]}";
                    if (arguments["front"]?.Value<bool>() == true)
                        bgCmd += " front";
                    if (arguments["flip"]?.Value<bool>() == true)
                        bgCmd += " flip";
                    return bgCmd;

                case "add_tile":
                    var tileCmd = $"ADD TILE tileset=\"{arguments["tileset"]}\" category=\"{arguments["category"]}\" at ({arguments["x"]}, {arguments["y"]})";
                    if (arguments["tile_no"] != null)
                        tileCmd += $" no=\"{arguments["tile_no"]}\"";
                    if (arguments["layer"] != null)
                        tileCmd += $" layer={arguments["layer"]}";
                    if (arguments["z"] != null)
                        tileCmd += $" z={arguments["z"]}";
                    return tileCmd;

                case "tile_platform":
                    var tilePlatformCmd = $"TILE PLATFORM tileset=\"{arguments["tileset"]}\" from x={arguments["start_x"]} to x={arguments["end_x"]} at y={arguments["y"]}";
                    if (arguments["layer"] != null)
                        tilePlatformCmd += $" layer={arguments["layer"]}";
                    return tilePlatformCmd;

                case "tile_structure":
                    var tileStructCmd = $"TILE STRUCTURE tileset=\"{arguments["tileset"]}\" type=\"{arguments["structure_type"]}\" from x={arguments["start_x"]} at y={arguments["y"]}";
                    if (arguments["end_x"] != null)
                        tileStructCmd += $" to x={arguments["end_x"]}";
                    if (arguments["width"] != null)
                        tileStructCmd += $" width={arguments["width"]}";
                    if (arguments["height"] != null)
                        tileStructCmd += $" height={arguments["height"]}";
                    if (arguments["layer"] != null)
                        tileStructCmd += $" layer={arguments["layer"]}";
                    return tileStructCmd;

                case "remove_element":
                    var removeType = arguments["element_type"]?.ToString().ToUpper();
                    if (arguments["name"] != null)
                        return $"DELETE {removeType} \"{arguments["name"]}\"";
                    else if (arguments["x"] != null && arguments["y"] != null)
                        return $"DELETE {removeType} at ({arguments["x"]}, {arguments["y"]})";
                    return $"# Could not parse remove_element: {arguments}";

                case "move_element":
                    var moveType = arguments["element_type"]?.ToString().ToUpper();
                    if (arguments["name"] != null)
                        return $"MOVE {moveType} \"{arguments["name"]}\" to ({arguments["to_x"]}, {arguments["to_y"]})";
                    else if (arguments["from_x"] != null && arguments["from_y"] != null)
                        return $"MOVE {moveType} at ({arguments["from_x"]}, {arguments["from_y"]}) to ({arguments["to_x"]}, {arguments["to_y"]})";
                    return $"# Could not parse move_element: {arguments}";

                case "modify_portal":
                    var modifyCmd = $"SET PORTAL \"{arguments["name"]}\"";
                    if (arguments["target_map"] != null)
                        modifyCmd += $" target_map={arguments["target_map"]}";
                    if (arguments["target_portal"] != null)
                        modifyCmd += $" target_name=\"{arguments["target_portal"]}\"";
                    if (arguments["script"] != null)
                        modifyCmd += $" script=\"{arguments["script"]}\"";
                    return modifyCmd;

                case "flip_element":
                    return $"FLIP {arguments["element_type"]?.ToString().ToUpper()} at ({arguments["x"]}, {arguments["y"]})";

                case "clear_elements":
                    return $"CLEAR ALL {arguments["element_type"]?.ToString().ToUpper()}S";

                case "set_bgm":
                    return $"SET BGM \"{arguments["bgm"]}\"";

                case "add_rope":
                    var ropeCmd = $"ADD ROPE at x={arguments["x"]} from y={arguments["top_y"]} to y={arguments["bottom_y"]}";
                    if (arguments["uf"] != null)
                        ropeCmd += arguments["uf"].Value<bool>() ? " uf=true" : " uf=false";
                    if (arguments["layer"] != null)
                        ropeCmd += $" layer={arguments["layer"]}";
                    return ropeCmd;

                case "add_ladder":
                    var ladderCmd = $"ADD LADDER at x={arguments["x"]} from y={arguments["top_y"]} to y={arguments["bottom_y"]}";
                    if (arguments["uf"] != null)
                        ladderCmd += arguments["uf"].Value<bool>() ? " uf=true" : " uf=false";
                    if (arguments["layer"] != null)
                        ladderCmd += $" layer={arguments["layer"]}";
                    return ladderCmd;

                default:
                    return $"# Unknown function: {functionName}";
            }
        }
    }
}
