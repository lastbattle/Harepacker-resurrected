using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HaCreator.MapEditor.AI
{
    /// <summary>Version 1 JSON-lines transport. Expansion preserves the legacy action contract and order.</summary>
    public static class CompactMapEdits
    {
        public const int MaximumOperations = 512;
        private static readonly Dictionary<string, string> Operations = new()
        {
            ["t"] = "add_tile", ["ts"] = "tile_structure", ["tp"] = "tile_platform",
            ["o"] = "add_object", ["b"] = "add_background", ["m"] = "add_mob", ["n"] = "add_npc",
            ["p"] = "add_portal", ["c"] = "add_chair", ["fh"] = "add_platform", ["w"] = "add_wall",
            ["r"] = "add_rope", ["l"] = "add_ladder", ["del"] = "remove_element", ["mv"] = "move_element",
            ["flip"] = "flip_element", ["clear"] = "clear_elements", ["portal"] = "modify_portal",
            ["skin"] = "change_tileset", ["bgm"] = "set_bgm", ["opt"] = "set_map_option",
            ["limit"] = "set_field_limit", ["size"] = "set_map_size", ["vr"] = "set_vr",
            ["return"] = "set_return_map", ["rate"] = "set_mob_rate", ["level"] = "set_level_limit",
            ["desc"] = "set_map_desc", ["help"] = "set_help", ["tip"] = "add_tooltip"
        };
        private static readonly Dictionary<string, string> Keys = new()
        {
            ["s"] = "tileset", ["u"] = "category", ["l"] = "layer", ["k"] = "structure_type",
            ["sx"] = "start_x", ["ex"] = "end_x", ["w"] = "width", ["h"] = "height",
            ["fh"] = "create_foothold", ["bind"] = "create_bindings", ["raw"] = "raw_position",
            ["e"] = "element_type", ["ty"] = "top_y", ["by"] = "bottom_y", ["tn"] = "tile_no"
        };

        public sealed record Edit(string Tool, JObject Arguments);

        public static IReadOnlyList<Edit> Decode(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || Encoding.UTF8.GetByteCount(code) > 131072)
                throw new ArgumentException("Supply 1–512 edits, at most 128 KiB of code.");
            var edits = new List<Edit>();
            using var lines = new StringReader(code);
            string line;
            while ((line = lines.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var row = JArray.Parse(line, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (row.Count is < 2 or > 3 || row[0].Type != JTokenType.String || row[1] is not JObject values)
                    throw new ArgumentException("Each line must be [opcode,{arguments}] or [opcode,{shared arguments},[[x,y],...]].");
                var op = row[0].Value<string>();
                var tool = Operations.TryGetValue(op, out var name) ? name : op;
                if (!Operations.ContainsValue(tool)) throw new ArgumentException("Unknown edit opcode: " + op);
                var args = new JObject();
                foreach (var property in values.Properties())
                {
                    var key = Keys.TryGetValue(property.Name, out var expanded) ? expanded : property.Name;
                    if (args.ContainsKey(key)) throw new ArgumentException("Duplicate argument: " + key);
                    args[key] = property.Value.DeepClone();
                }
                void Add(JObject arguments)
                {
                    if (edits.Count >= MaximumOperations) throw new ArgumentException("Batch exceeds 512 expanded edits.");
                    edits.Add(new Edit(tool, arguments));
                }
                if (row.Count == 2) Add(args);
                else
                {
                    if (row[2] is not JArray positions || positions.Count == 0 || args.ContainsKey("x") || args.ContainsKey("y"))
                        throw new ArgumentException("Repeated placements need a nonempty coordinate list and no shared x/y.");
                    foreach (var position in positions)
                    {
                        if (position is not JArray xy || xy.Count != 2 || xy.Any(v => v.Type != JTokenType.Integer))
                            throw new ArgumentException("Each position must be [integer x, integer y].");
                        var copy = (JObject)args.DeepClone(); copy["x"] = xy[0].DeepClone(); copy["y"] = xy[1].DeepClone(); Add(copy);
                    }
                }
            }
            if (edits.Count == 0) throw new ArgumentException("The batch contains no edits.");
            return edits;
        }

        public static string Encode(string tool, JObject arguments)
        {
            var op = Operations.First(p => p.Value == tool).Key;
            var args = new JObject();
            foreach (var p in arguments.Properties())
                args[Keys.FirstOrDefault(k => k.Value == p.Name).Key ?? p.Name] = p.Value.DeepClone();
            return new JArray(op, args).ToString(Formatting.None);
        }

        /// <summary>Group only adjacent equivalent placements; never reorder edits or carry hidden mutable defaults.</summary>
        public static string Encode(IEnumerable<Edit> edits)
        {
            var rows = edits.Select(e => JArray.Parse(Encode(e.Tool, e.Arguments))).ToList();
            var output = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; var args = (JObject)row[1];
                if (args["x"]?.Type != JTokenType.Integer || args["y"]?.Type != JTokenType.Integer)
                { output.Add(row.ToString(Formatting.None)); continue; }
                var shared = (JObject)args.DeepClone(); shared.Remove("x"); shared.Remove("y");
                var positions = new JArray { new JArray(args["x"].DeepClone(), args["y"].DeepClone()) };
                while (i + 1 < rows.Count)
                {
                    var next = rows[i + 1]; var nextArgs = (JObject)next[1];
                    if (!JToken.DeepEquals(row[0], next[0]) || nextArgs["x"]?.Type != JTokenType.Integer || nextArgs["y"]?.Type != JTokenType.Integer) break;
                    var nextShared = (JObject)nextArgs.DeepClone(); nextShared.Remove("x"); nextShared.Remove("y");
                    if (!JToken.DeepEquals(shared, nextShared)) break;
                    positions.Add(new JArray(nextArgs["x"].DeepClone(), nextArgs["y"].DeepClone())); i++;
                }
                output.Add((positions.Count > 1 ? new JArray(row[0].DeepClone(), shared, positions) : row).ToString(Formatting.None));
            }
            return string.Join("\n", output);
        }

        public static JObject HelpDefinition() => new JObject
        {
            ["name"] = "get_edit_help", ["description"] = "Get complete parameter descriptions, defaults and geometry semantics for one edit opcode (or original action name). Read before unfamiliar edits.",
            ["inputSchema"] = new JObject { ["type"] = "object", ["properties"] = new JObject { ["op"] = new JObject { ["type"] = "string" } }, ["required"] = new JArray("op"), ["additionalProperties"] = false }
        };

        public static string Help(string op)
        {
            if (op == null) return "Error: Supply an edit opcode.";
            var tool = Operations.TryGetValue(op, out var name) ? name : op;
            var definition = MapEditorFunctions.GetToolDefinitions().Select(t => t["function"]).FirstOrDefault(f => (string)f["name"] == tool && Operations.ContainsValue(tool));
            return definition?.ToString(Formatting.None) ?? "Error: Unknown edit opcode.";
        }

        public static JObject Definition()
        {
            var guide = new StringBuilder("Execute compact edits in order. code is JSON-lines, one [opcode,{args}] per line. " +
                "For repeated placements use [opcode,{shared args},[[x,y],...]]; omit shared x/y. Max 512 expanded edits. " +
                "Omit optional fields; do not send nulls. All lines are validated before execution. Runtime errors stop the batch; prior successes remain. " +
                "Query required assets BEFORE editing. In review mode edits are staged only. Use get_map_view afterward in live mode. " +
                "Never include code in your final answer; users receive a readable checklist. Use get_edit_help(op) for full parameter defaults and geometry semantics before unfamiliar operations. " +
                "World pixels: x right, y down. layer 0–7. raw=true places the object origin, bypassing ground snapping. fh defaults true; h for ts is tile rows, not pixels. " +
                "Argument aliases: " +
                string.Join(",", Keys.Select(p => p.Key + "=" + p.Value)) + ". Other keys keep their original names. * means required.\n");
            foreach (var f in MapEditorFunctions.GetToolDefinitions().Select(t => (JObject)t["function"]).Where(f => Operations.ContainsValue((string)f["name"])))
            {
                guide.Append(Operations.First(p => p.Value == (string)f["name"]).Key).Append(" = ").Append(f["name"]).Append(": ").Append(f["description"]).AppendLine();
                var required = ((JArray)f["parameters"]["required"]).Values<string>().ToHashSet();
                foreach (var p in ((JObject)f["parameters"]["properties"]).Properties())
                {
                    guide.Append(Keys.FirstOrDefault(k => k.Value == p.Name).Key ?? p.Name).Append(required.Contains(p.Name) ? "*" : "").Append(':');
                    guide.Append(p.Value["type"]).Append(' ');
                    // Detailed parameter help is fetched on demand; constraints remain in the compact reference.
                    foreach (var field in new[] { "enum", "minimum", "maximum", "items" })
                        if (p.Value[field] != null) guide.Append(field).Append('=').Append(p.Value[field].ToString(Formatting.None)).Append(' ');
                    guide.AppendLine();
                }
            }
            return new JObject { ["name"] = "edit_map", ["description"] = guide.ToString(), ["inputSchema"] = new JObject
            {
                ["type"] = "object", ["properties"] = new JObject { ["code"] = new JObject { ["type"] = "string" } },
                ["required"] = new JArray("code"), ["additionalProperties"] = false
            }};
        }
    }
}
