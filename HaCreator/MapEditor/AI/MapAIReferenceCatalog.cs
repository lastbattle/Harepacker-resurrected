using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MapleLib.WzLib;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>Read-only discovery of named source maps and the exact artwork they use.</summary>
    public static class MapAIReferenceCatalog
    {
        public static string Query(string search = null, string mapId = null, int offset = 0, int limit = 100)
        {
            if (Program.DataSource == null)
                return "Error: No map data source is loaded.";
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 200);
            if (mapId != null)
                return Describe(mapId, offset, limit);

            var strings = Program.DataSource.GetImage("String", "Map.img")
                ?? Program.DataSource.GetImageByPath("String/Map.img");
            if (strings == null)
                return "Error: String/Map.img is unavailable; supply a known map_id to inspect its source assets.";
            strings.ParseImage();
            var names = new List<JObject>();
            foreach (var category in strings.WzProperties)
            {
                if (category.WzProperties == null) continue;
                foreach (var entry in category.WzProperties)
                {
                    if (!TryId(entry.Name, out string id)) continue;
                    string name = Value(entry["mapName"]);
                    string street = Value(entry["streetName"]);
                    if (!string.IsNullOrEmpty(search) && !new[] { id, name, street, category.Name }
                        .Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)) continue;
                    names.Add(new JObject { ["map_id"] = id, ["mapName"] = name, ["streetName"] = street, ["category"] = category.Name });
                }
            }
            var ordered = names.OrderBy(name => (string)name["map_id"], StringComparer.Ordinal).ToList();
            var result = Page(ordered, offset, limit, "maps");
            result["guidance"] = "Names come from String/Map.img; a named map may be absent from the loaded Map data. Select a map_id to inspect its exact asset usage, then preview those assets. No theme aliases or guessed asset names are used.";
            return result.ToString();
        }

        private static string Describe(string requestedId, int offset, int limit)
        {
            if (!TryId(requestedId, out string id))
                return "Error: map_id must contain 1–9 decimal digits.";
            string originalId = id;
            WzImage original = null;
            WzImage map;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (!visited.Add(id) || visited.Count > 16)
                    return "Error: Source map link cycle or excessive link depth.";
                string relativePath = $"Map/Map{id[0]}/{id}.img";
                map = Program.DataSource.GetImageByPath($"Map/{relativePath}")
                    ?? Program.DataSource.GetImage("Map", relativePath);
                if (map == null) return $"Error: Source map {id} was not found at Map/{relativePath}.";
                map.ParseImage();
                original ??= map;
                string linkedId = Value(map["info"]?["link"]);
                if (string.IsNullOrEmpty(linkedId)) break;
                if (!TryId(linkedId, out id)) return "Error: Source map contains an invalid info/link.";
            }

            var usages = new Dictionary<string, JObject>(StringComparer.Ordinal);
            void Add(JObject asset, string path)
            {
                if (asset.Properties().Any(property => property.Value.Type == JTokenType.Null || string.IsNullOrEmpty((string)property.Value))) return;
                if (usages.TryGetValue(path, out var existing)) existing["count"] = (int)existing["count"] + 1;
                else usages[path] = new JObject { ["asset"] = asset, ["path"] = path, ["count"] = 1 };
            }
            foreach (var layer in map.WzProperties.Where(property => property.Name.Length > 0 && property.Name.All(char.IsAsciiDigit)))
            {
                string tileSet = Value(layer["info"]?["tS"]);
                if (layer["tile"]?.WzProperties != null)
                    foreach (var tile in layer["tile"].WzProperties)
                    {
                        string unit = Value(tile["u"]), number = Value(tile["no"]);
                        Add(new JObject { ["type"] = "tile", ["tS"] = tileSet, ["u"] = unit, ["no"] = number }, $"Map/Tile/{tileSet}.img/{unit}/{number}");
                    }
                if (layer["obj"]?.WzProperties != null)
                    foreach (var obj in layer["obj"].WzProperties)
                    {
                        string set = Value(obj["oS"]), l0 = Value(obj["l0"]), l1 = Value(obj["l1"]), l2 = Value(obj["l2"]);
                        Add(new JObject { ["type"] = "object", ["oS"] = set, ["l0"] = l0, ["l1"] = l1, ["l2"] = l2 }, $"Map/Obj/{set}.img/{l0}/{l1}/{l2}");
                    }
            }
            if (map["back"]?.WzProperties != null)
                foreach (var back in map["back"].WzProperties)
                {
                    string set = Value(back["bS"]), number = Value(back["no"]);
                    string type = !string.IsNullOrEmpty(Value(back["spineAni"])) ? "spine" : Value(back["ani"]) == "1" ? "ani" : "back";
                    Add(new JObject { ["type"] = "background", ["bS"] = set, ["no"] = number, ["backgroundType"] = type }, $"Map/Back/{set}.img/{type}/{number}");
                }
            var result = Page(usages.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value).ToList(), offset, limit, "usages");
            result["map_id"] = originalId;
            result["assetSourceMapId"] = id;
            result["sourcePath"] = $"Map/Map/Map{id[0]}/{id}.img";
            var info = new JObject();
            foreach (string key in new[] { "bgm", "VRLeft", "VRTop", "VRRight", "VRBottom" })
                if (original["info"]?[key]?.WzValue is object value) info[key] = JToken.FromObject(value);
            result["info"] = info;
            result["guidance"] = "Exact source-map artwork usages, counted across placements. Pass asset objects to get_asset_preview (up to 16 per call; Spine preview unsupported), inspect dimensions/details before editing. Follow nextOffset for remaining usages. This query does not modify the active map.";
            return result.ToString();
        }

        private static JObject Page(List<JObject> entries, int offset, int limit, string key)
        {
            var page = entries.Skip(offset).Take(limit).ToList();
            bool more = (long)offset + page.Count < entries.Count;
            return new JObject { ["total"] = entries.Count, ["offset"] = offset, ["limit"] = limit,
                ["hasMore"] = more, ["nextOffset"] = more ? new JValue(offset + page.Count) : JValue.CreateNull(), [key] = new JArray(page) };
        }

        private static string Value(WzImageProperty property) => Convert.ToString(property?.WzValue, CultureInfo.InvariantCulture);

        private static bool TryId(string value, out string id)
        {
            id = null;
            if (string.IsNullOrEmpty(value) || value.Length > 9 || !value.All(char.IsAsciiDigit)) return false;
            id = value.PadLeft(9, '0');
            return true;
        }
    }
}
