using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapEditor.AI
{
    /// <summary>
    /// Generates text-based catalogs of available map assets (objects, backgrounds, tiles)
    /// for AI/LLM awareness. This allows the AI to know what assets can be placed on maps.
    /// </summary>
    public static class MapAssetCatalog
    {
        /// <summary>
        /// Tile category descriptions for AI understanding
        /// </summary>
        public static readonly Dictionary<string, string> TileCategoryDescriptions = new Dictionary<string, string>
        {
            // Basic tiles
            { "bsc", "Basic fill tile - interior/surface of platforms" },

            // Horizontal edges (top/bottom of platforms)
            { "enH0", "Horizontal top edge - top surface of platforms" },
            { "enH1", "Horizontal bottom edge - underside of platforms" },

            // Vertical edges (left/right sides)
            { "enV0", "Vertical left edge - left side of platforms" },
            { "enV1", "Vertical right edge - right side of platforms" },

            // Corners
            { "edU", "Upper corner tiles - top-left and top-right corners" },
            { "edD", "Lower corner tiles - bottom-left and bottom-right corners" },

            // Slopes
            { "slLU", "Slope going up-left (ascending leftward)" },
            { "slRU", "Slope going up-right (ascending rightward)" },
            { "slLD", "Slope going down-left (descending leftward)" },
            { "slRD", "Slope going down-right (descending rightward)" },

            // Slope edges
            { "slLUenH0", "Top edge for left-upward slope" },
            { "slRUenH0", "Top edge for right-upward slope" },
            { "slLDenH0", "Top edge for left-downward slope" },
            { "slRDenH0", "Top edge for right-downward slope" },
        };

        /// <summary>
        /// Background type descriptions
        /// </summary>
        public static readonly Dictionary<string, string> BackgroundTypeDescriptions = new Dictionary<string, string>
        {
            { "back", "Static background image" },
            { "ani", "Animated background (frame-based animation)" },
            { "spine", "Spine skeletal animation background" },
        };

        /// <summary>
        /// Generate a compact summary of available tilesets
        /// </summary>
        public static string GenerateTilesetSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available Tilesets");

            var tilesets = Program.InfoManager.TileSets.Keys.OrderBy(k => k).ToList();
            if (tilesets.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No tilesets loaded. Load Map.wz to access tilesets.");
                return sb.ToString();
            }

            sb.AppendLine($"Total: {tilesets.Count} tilesets");
            sb.AppendLine();

            // Group by apparent category (first part of name)
            var grouped = tilesets.GroupBy(t => GetTilesetCategory(t)).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                sb.AppendLine($"### {group.Key}");
                sb.AppendLine(string.Join(", ", group.Select(t => $"\"{t}\"")));
                sb.AppendLine();
            }

            sb.AppendLine("### Tile Categories (for each tileset):");
            foreach (var kvp in TileCategoryDescriptions)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a summary of available object sets with their hierarchy
        /// </summary>
        public static string GenerateObjectCatalog(int maxItemsPerSet = 10)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available Object Sets");

            var objectSets = Program.InfoManager.ObjectSets;
            if (objectSets.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No object sets loaded. Load Map.wz to access objects.");
                return sb.ToString();
            }

            sb.AppendLine($"Total: {objectSets.Count} object sets");
            sb.AppendLine();
            sb.AppendLine("Format: oS (ObjectSet) > l0 (category) > l1 (subcategory) > l2 (item number)");
            sb.AppendLine();

            // Group object sets by apparent theme
            var grouped = objectSets.Keys.OrderBy(k => k)
                .GroupBy(k => GetObjectSetTheme(k))
                .OrderBy(g => g.Key);

            foreach (var themeGroup in grouped)
            {
                sb.AppendLine($"### Theme: {themeGroup.Key}");

                foreach (var oS in themeGroup.Take(maxItemsPerSet))
                {
                    sb.Append($"- **{oS}**: ");

                    var categories = GetObjectCategories(objectSets[oS]);
                    if (categories.Count > 0)
                    {
                        sb.AppendLine(string.Join(", ", categories.Take(8)));
                        if (categories.Count > 8)
                            sb.AppendLine($"  ... and {categories.Count - 8} more categories");
                    }
                    else
                    {
                        sb.AppendLine("(empty)");
                    }
                }

                if (themeGroup.Count() > maxItemsPerSet)
                {
                    sb.AppendLine($"  ... and {themeGroup.Count() - maxItemsPerSet} more sets in this theme");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a summary of available background sets
        /// </summary>
        public static string GenerateBackgroundCatalog(int maxItemsPerTheme = 15)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available Background Sets");

            var bgSets = Program.InfoManager.BackgroundSets;
            if (bgSets.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No background sets loaded. Load Map.wz to access backgrounds.");
                return sb.ToString();
            }

            sb.AppendLine($"Total: {bgSets.Count} background sets");
            sb.AppendLine();
            sb.AppendLine("Background Types:");
            foreach (var kvp in BackgroundTypeDescriptions)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();

            // Group by theme
            var grouped = bgSets.Keys.OrderBy(k => k)
                .GroupBy(k => GetBackgroundTheme(k))
                .OrderBy(g => g.Key);

            foreach (var themeGroup in grouped)
            {
                sb.AppendLine($"### {themeGroup.Key}");

                var items = themeGroup.Take(maxItemsPerTheme).ToList();
                foreach (var bS in items)
                {
                    var types = GetBackgroundTypes(bgSets[bS]);
                    sb.AppendLine($"- **{bS}**: {string.Join(", ", types)}");
                }

                if (themeGroup.Count() > maxItemsPerTheme)
                {
                    sb.AppendLine($"  ... and {themeGroup.Count() - maxItemsPerTheme} more");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a compact asset summary suitable for AI context
        /// </summary>
        public static string GenerateCompactSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Available Map Assets");
            sb.AppendLine();

            // Tilesets - list all
            sb.AppendLine("## Tilesets");
            var tilesets = Program.InfoManager.TileSets.Keys.OrderBy(k => k).ToList();
            sb.AppendLine($"Available ({tilesets.Count}): {string.Join(", ", tilesets)}");
            sb.AppendLine();

            // Tile categories
            sb.AppendLine("Tile categories: bsc (fill), enH0 (top), enH1 (bottom), enV0 (left edge), enV1 (right edge), edU (top corners), edD (bottom corners), slLU/slRU/slLD/slRD (slopes)");
            sb.AppendLine();

            // Object sets - just list names, use get_object_info for details
            sb.AppendLine("## Object Sets");
            sb.AppendLine("Use get_object_info(oS) to query available paths and dimensions for a specific set.");
            var objectSets = Program.InfoManager.ObjectSets.Keys.OrderBy(k => k).ToList();
            sb.AppendLine($"Available ({objectSets.Count}): {string.Join(", ", objectSets)}");
            sb.AppendLine();

            // Background sets - just list names, use get_background_info for details
            sb.AppendLine("## Background Sets");
            sb.AppendLine("Use get_background_info(bS) to query available items and dimensions for a specific set.");
            var bgSets = Program.InfoManager.BackgroundSets.Keys.OrderBy(k => k).ToList();
            sb.AppendLine($"Available ({bgSets.Count}): {string.Join(", ", bgSets)}");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Get the hierarchy of an object set (l0/l1/l2 structure)
        /// </summary>
        private static string GetObjectSetHierarchy(string oS)
        {
            if (!Program.InfoManager.ObjectSets.TryGetValue(oS, out var wzImage) || wzImage == null)
                return null;

            var sb = new StringBuilder();
            foreach (var l0Prop in wzImage.WzProperties)
            {
                if (l0Prop is WzSubProperty l0Sub)
                {
                    foreach (var l1Prop in l0Sub.WzProperties)
                    {
                        if (l1Prop is WzSubProperty l1Sub)
                        {
                            var l2Items = l1Sub.WzProperties.Select(p => p.Name).ToList();
                            if (l2Items.Count > 0)
                            {
                                sb.AppendLine($"- {l0Prop.Name}/{l1Prop.Name}: [{string.Join(", ", l2Items)}]");
                            }
                        }
                    }
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Get available items in a background set
        /// </summary>
        private static string GetBackgroundSetItems(string bS)
        {
            if (!Program.InfoManager.BackgroundSets.TryGetValue(bS, out var wzImage) || wzImage == null)
                return null;

            var sb = new StringBuilder();
            var types = new[] { "back", "ani", "spine" };
            foreach (var type in types)
            {
                var typeProp = wzImage[type];
                if (typeProp is WzSubProperty typeSub && typeSub.WzProperties.Count > 0)
                {
                    var items = typeSub.WzProperties.Select(p => p.Name).ToList();
                    sb.AppendLine($"- {type}: [{string.Join(", ", items)}]");
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Get detailed info about a specific object set including dimensions and origin
        /// </summary>
        public static string GetObjectSetDetails(string oS)
        {
            if (!Program.InfoManager.ObjectSets.TryGetValue(oS, out var wzImage) || wzImage == null)
            {
                // Check if it's actually a tileset (common mistake)
                if (Program.InfoManager.TileSets.ContainsKey(oS))
                {
                    return $"'{oS}' is a TILESET, not an object set. " +
                           $"For tilesets, use tile_platform() or tile_structure() directly with tileset=\"{oS}\". " +
                           $"You don't need to query tileset info - just use the name directly.";
                }
                return $"Object set '{oS}' not found. Available object sets are listed in the map context. " +
                       $"Note: This is for OBJECTS (Obj.wz), not tiles. For tiles, use tile_platform/tile_structure directly.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"## Object Set: {oS}");
            sb.AppendLine("Format: l2 [WxH, origin:(x,y)]");
            sb.AppendLine("Note: Origin is the anchor point. Objects are placed relative to origin.");
            sb.AppendLine();

            foreach (var l0Prop in wzImage.WzProperties)
            {
                if (l0Prop is WzSubProperty l0Sub)
                {
                    sb.AppendLine($"### {l0Prop.Name}");
                    foreach (var l1Prop in l0Sub.WzProperties)
                    {
                        if (l1Prop is WzSubProperty l1Sub)
                        {
                            var items = new List<string>();
                            foreach (var l2Prop in l1Sub.WzProperties)
                            {
                                var info = GetObjectInfo(l2Prop);
                                if (info.HasValue)
                                    items.Add($"{l2Prop.Name} [{info.Value.width}x{info.Value.height}, origin:({info.Value.originX},{info.Value.originY})]");
                                else
                                    items.Add(l2Prop.Name);
                            }
                            if (items.Count > 0)
                            {
                                sb.AppendLine($"  {l1Prop.Name}: {string.Join(", ", items)}");
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get full info about a specific object (dimensions and origin)
        /// </summary>
        private static (int width, int height, int originX, int originY)? GetObjectInfo(WzObject l2Prop)
        {
            try
            {
                WzCanvasProperty canvas = null;

                // Try to find the first frame (0) or any canvas property
                if (l2Prop is WzSubProperty l2Sub)
                {
                    // Look for frame "0" first
                    var frame0 = l2Sub["0"];
                    if (frame0 is WzCanvasProperty frame0Canvas)
                    {
                        canvas = frame0Canvas;
                    }
                    else if (frame0 is WzSubProperty frameSubProp)
                    {
                        // Could be nested
                        foreach (var child in frameSubProp.WzProperties)
                        {
                            if (child is WzCanvasProperty nestedCanvas)
                            {
                                canvas = nestedCanvas;
                                break;
                            }
                        }
                    }

                    // Otherwise look for any canvas property
                    if (canvas == null)
                    {
                        foreach (var child in l2Sub.WzProperties)
                        {
                            if (child is WzCanvasProperty childCanvas)
                            {
                                canvas = childCanvas;
                                break;
                            }
                        }
                    }
                }
                else if (l2Prop is WzCanvasProperty directCanvas)
                {
                    canvas = directCanvas;
                }

                if (canvas != null)
                {
                    int width = canvas.PngProperty.Width;
                    int height = canvas.PngProperty.Height;
                    int originX = 0;
                    int originY = 0;

                    // Get origin from canvas properties
                    var originProp = canvas["origin"];
                    if (originProp is WzVectorProperty vector)
                    {
                        originX = vector.X.Value;
                        originY = vector.Y.Value;
                    }

                    return (width, height, originX, originY);
                }
            }
            catch
            {
                // Ignore errors getting info
            }
            return null;
        }

        /// <summary>
        /// Get detailed info about a specific background set including dimensions and origin
        /// </summary>
        public static string GetBackgroundSetDetails(string bS)
        {
            if (!Program.InfoManager.BackgroundSets.TryGetValue(bS, out var wzImage) || wzImage == null)
                return $"[HaCreator Connected] Background set '{bS}' not found. Available sets are listed in the map context.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Background Set: {bS}");
            sb.AppendLine("Format: no [WxH, origin:(x,y)]");
            sb.AppendLine("Note: Origin is the anchor point. Backgrounds are placed relative to origin.");
            sb.AppendLine();

            var types = new[] { "back", "ani", "spine" };
            foreach (var type in types)
            {
                var typeProp = wzImage[type];
                if (typeProp is WzSubProperty typeSub && typeSub.WzProperties.Count > 0)
                {
                    sb.AppendLine($"### {type}");
                    var items = new List<string>();
                    foreach (var bgProp in typeSub.WzProperties)
                    {
                        var info = GetBackgroundInfo(bgProp);
                        if (info.HasValue)
                            items.Add($"{bgProp.Name} [{info.Value.width}x{info.Value.height}, origin:({info.Value.originX},{info.Value.originY})]");
                        else
                            items.Add(bgProp.Name);
                    }
                    sb.AppendLine($"  Items: {string.Join(", ", items)}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get full info about a specific background (dimensions and origin)
        /// </summary>
        private static (int width, int height, int originX, int originY)? GetBackgroundInfo(WzObject bgProp)
        {
            try
            {
                WzCanvasProperty canvas = null;

                // Direct canvas
                if (bgProp is WzCanvasProperty directCanvas)
                {
                    canvas = directCanvas;
                }
                // Animated background - look for frame 0
                else if (bgProp is WzSubProperty sub)
                {
                    var frame0 = sub["0"];
                    if (frame0 is WzCanvasProperty frame0Canvas)
                    {
                        canvas = frame0Canvas;
                    }
                    else
                    {
                        // Look for any canvas
                        foreach (var child in sub.WzProperties)
                        {
                            if (child is WzCanvasProperty childCanvas)
                            {
                                canvas = childCanvas;
                                break;
                            }
                        }
                    }
                }

                if (canvas != null)
                {
                    int width = canvas.PngProperty.Width;
                    int height = canvas.PngProperty.Height;
                    int originX = 0;
                    int originY = 0;

                    // Get origin from canvas properties
                    var originProp = canvas["origin"];
                    if (originProp is WzVectorProperty vector)
                    {
                        originX = vector.X.Value;
                        originY = vector.Y.Value;
                    }

                    return (width, height, originX, originY);
                }
            }
            catch
            {
                // Ignore errors getting info
            }
            return null;
        }

        /// <summary>
        /// Get list of available BGMs (filtered to only include paths starting with "Bgm")
        /// </summary>
        public static string GetBgmList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available BGMs");
            sb.AppendLine("Format: path (use exact path with set_bgm)");
            sb.AppendLine();

            var bgms = Program.InfoManager.BGMs.Keys
                .Where(k => k.StartsWith("Bgm", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();

            if (bgms.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No BGMs loaded. Load Sound.wz to access BGM tracks.");
                return sb.ToString();
            }

            // Group by folder (first part of path)
            var grouped = bgms.GroupBy(b => b.Split('/')[0]).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                sb.AppendLine($"### {group.Key}");
                foreach (var bgm in group)
                {
                    sb.AppendLine($"  - {bgm}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get list of available mobs (monsters) from the loaded data
        /// </summary>
        /// <param name="search">Optional search term to filter by name (case-insensitive)</param>
        /// <param name="limit">Maximum number of results to return</param>
        public static string GetMobList(string search = null, int limit = 50)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available Mobs");
            sb.AppendLine("Format: ID = Name");
            sb.AppendLine();

            var mobCache = Program.InfoManager.MobNameCache;
            if (mobCache == null || mobCache.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No mobs loaded yet. The Mob.wz data may not be loaded.");
                sb.AppendLine("To load mobs: File > Load Mob.wz or ensure it's in your data folder.");
                return sb.ToString();
            }

            IEnumerable<KeyValuePair<string, string>> mobs = mobCache;

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                mobs = mobs.Where(m =>
                    m.Key.Contains(search) ||
                    (m.Value != null && m.Value.ToLowerInvariant().Contains(searchLower)));
                sb.AppendLine($"Search: \"{search}\"");
                sb.AppendLine();
            }

            var mobList = mobs.OrderBy(m => m.Key).Take(limit).ToList();

            if (mobList.Count == 0)
            {
                sb.AppendLine("No mobs found matching the search criteria.");
                return sb.ToString();
            }

            sb.AppendLine($"Showing {mobList.Count} of {mobCache.Count} total mobs:");
            sb.AppendLine();

            foreach (var mob in mobList)
            {
                var name = string.IsNullOrEmpty(mob.Value) ? "(unnamed)" : mob.Value;
                sb.AppendLine($"  {mob.Key} = {name}");
            }

            if (mobList.Count == limit && mobCache.Count > limit)
            {
                sb.AppendLine();
                sb.AppendLine($"... and {mobCache.Count - limit} more. Use search to filter or increase limit.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get list of available NPCs from the loaded data
        /// </summary>
        /// <param name="search">Optional search term to filter by name or function (case-insensitive)</param>
        /// <param name="limit">Maximum number of results to return</param>
        public static string GetNpcList(string search = null, int limit = 50)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Available NPCs");
            sb.AppendLine("Format: ID = Name [Function]");
            sb.AppendLine();

            var npcCache = Program.InfoManager.NpcNameCache;
            if (npcCache == null || npcCache.Count == 0)
            {
                sb.AppendLine("[HaCreator Connected] No NPCs loaded. Load Npc.wz to access NPCs.");
                return sb.ToString();
            }

            IEnumerable<KeyValuePair<string, Tuple<string, string>>> npcs = npcCache;

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                npcs = npcs.Where(n =>
                    n.Key.Contains(search) ||
                    (n.Value?.Item1 != null && n.Value.Item1.ToLowerInvariant().Contains(searchLower)) ||
                    (n.Value?.Item2 != null && n.Value.Item2.ToLowerInvariant().Contains(searchLower)));
                sb.AppendLine($"Search: \"{search}\"");
                sb.AppendLine();
            }

            var npcList = npcs.OrderBy(n => n.Key).Take(limit).ToList();

            if (npcList.Count == 0)
            {
                sb.AppendLine("No NPCs found matching the search criteria.");
                return sb.ToString();
            }

            sb.AppendLine($"Showing {npcList.Count} of {npcCache.Count} total NPCs:");
            sb.AppendLine();

            foreach (var npc in npcList)
            {
                var name = string.IsNullOrEmpty(npc.Value?.Item1) ? "(unnamed)" : npc.Value.Item1;
                var func = string.IsNullOrEmpty(npc.Value?.Item2) ? "" : $" [{npc.Value.Item2}]";
                sb.AppendLine($"  {npc.Key} = {name}{func}");
            }

            if (npcList.Count == limit && npcCache.Count > limit)
            {
                sb.AppendLine();
                sb.AppendLine($"... and {npcCache.Count - limit} more. Use search to filter or increase limit.");
            }

            return sb.ToString();
        }

        #region Helper Methods

        private static string GetTilesetCategory(string tilesetName)
        {
            // Categorize tilesets by common prefixes/themes
            var name = tilesetName.ToLowerInvariant();

            if (name.Contains("grass") || name.Contains("forest") || name.Contains("wood"))
                return "Nature/Forest";
            if (name.Contains("snow") || name.Contains("ice") || name.Contains("cold"))
                return "Snow/Ice";
            if (name.Contains("mine") || name.Contains("cave") || name.Contains("rock"))
                return "Cave/Mine";
            if (name.Contains("castle") || name.Contains("dungeon") || name.Contains("stone"))
                return "Castle/Dungeon";
            if (name.Contains("city") || name.Contains("town") || name.Contains("urban"))
                return "Town/City";
            if (name.Contains("beach") || name.Contains("sand") || name.Contains("ocean"))
                return "Beach/Ocean";
            if (name.Contains("ludi") || name.Contains("toy") || name.Contains("clock"))
                return "Ludibrium/Toy";
            if (name.Contains("korea") || name.Contains("china") || name.Contains("japan") || name.Contains("asia"))
                return "Asian Theme";
            if (name.Contains("future") || name.Contains("machine") || name.Contains("factory"))
                return "Future/Machine";

            return "Other";
        }

        private static string GetObjectSetTheme(string objectSetName)
        {
            var name = objectSetName.ToLowerInvariant();

            if (name.Contains("henesys") || name.Contains("ellinia") || name.Contains("perion"))
                return "Victoria Island";
            if (name.Contains("ludibrium") || name.Contains("ludi") || name.Contains("toy"))
                return "Ludibrium";
            if (name.Contains("aqua") || name.Contains("ocean") || name.Contains("underwater"))
                return "Aqua Road";
            if (name.Contains("leafre") || name.Contains("dragon"))
                return "Leafre/Dragon";
            if (name.Contains("nlc") || name.Contains("masteria"))
                return "Masteria";
            if (name.Contains("korean") || name.Contains("china") || name.Contains("taiwan"))
                return "Asian Theme";
            if (name.Contains("event") || name.Contains("cash"))
                return "Event/Special";
            if (name.Contains("dungeon") || name.Contains("boss"))
                return "Dungeon/Boss";

            return "General";
        }

        private static string GetBackgroundTheme(string bgSetName)
        {
            var name = bgSetName.ToLowerInvariant();

            if (name.Contains("henesys") || name.Contains("ellinia") || name.Contains("perion") || name.Contains("victoria"))
                return "Victoria Island";
            if (name.Contains("ludi") || name.Contains("toy"))
                return "Ludibrium";
            if (name.Contains("aqua") || name.Contains("ocean") || name.Contains("underwater"))
                return "Aqua Road";
            if (name.Contains("leafre") || name.Contains("dragon"))
                return "Leafre";
            if (name.Contains("temple") || name.Contains("shrine"))
                return "Temple";
            if (name.Contains("night") || name.Contains("dark"))
                return "Dark/Night";
            if (name.Contains("sky") || name.Contains("cloud"))
                return "Sky";

            return "General";
        }

        private static List<string> GetObjectCategories(WzImage wzImage)
        {
            var categories = new List<string>();

            foreach (var prop in wzImage.WzProperties)
            {
                if (prop is WzSubProperty)
                {
                    categories.Add(prop.Name);
                }
            }

            return categories;
        }

        private static List<string> GetBackgroundTypes(WzImage wzImage)
        {
            var types = new List<string>();
            var possibleTypes = new[] { "back", "ani", "spine" };

            foreach (var type in possibleTypes)
            {
                var prop = wzImage[type];
                if (prop is WzSubProperty sub && sub.WzProperties.Count > 0)
                {
                    types.Add($"{type}({sub.WzProperties.Count})");
                }
            }

            return types;
        }

        #endregion
    }
}
