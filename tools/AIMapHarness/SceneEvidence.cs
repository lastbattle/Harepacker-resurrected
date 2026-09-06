using System.IO;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;

internal static class SceneEvidence
{
    public static void Capture(Board board, JObject state, string output, string stem, JArray region = null)
    {
        File.WriteAllText(Path.Combine(output, stem + "-state.json"), state.ToString());
        var bounds = region ?? state["globalGeometryBounds"] as JArray;
        if (bounds == null) return;
        var views = new JArray();
        // Fixed world-space cells allow side-by-side review even when artwork bounds change.
        int index = 0;
        for (int y = (int)bounds[1]; y < (int)bounds[3] && index < 60; y += 700)
            for (int x = (int)bounds[0]; x < (int)bounds[2] && index < 60; x += 900)
            {
                var result = MapAIVisualRenderer.RenderMap(board, new JObject
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["width"] = 1000,
                    ["height"] = 800,
                    ["maxDimension"] = 1000,
                    ["overlays"] = false
                });
                string file = stem + "-region-" + index++ + ".png";
                File.WriteAllBytes(Path.Combine(output, file), Convert.FromBase64String((string)result[1]["data"]));
                views.Add(new JObject { ["file"] = file, ["metadata"] = JObject.Parse((string)result[0]["text"]) });
            }
        File.WriteAllText(Path.Combine(output, stem + "-regions.json"), views.ToString());
    }

    public static JObject Compare(JObject before, JObject after)
    {
        var result = new JObject();
        foreach (string type in new[] { "tile", "object", "background", "npc", "mob", "portal", "reactor", "foothold", "rope", "ladder" })
        {
            string[] Keys(JObject state) => ((JArray)state["elements"])
                .Where(e => (string)e["type"] == type).Select(e =>
                {
                    var copy = (JObject)e.DeepClone(); copy.Remove("sourceIndex");
                    return copy.ToString(Newtonsoft.Json.Formatting.None);
                }).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var oldItems = Keys(before); var newItems = Keys(after);
            // Multiset comparison counts duplicate scenery correctly.
            var available = newItems.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            int unchanged = 0;
            foreach (var item in oldItems)
                if (available.TryGetValue(item, out int count) && count > 0)
                { unchanged++; available[item] = count - 1; }
            result[type] = new JObject
            {
                ["before"] = oldItems.Length,
                ["after"] = newItems.Length,
                ["unchanged"] = unchanged,
                ["removedOrChanged"] = oldItems.Length - unchanged,
                ["addedOrChanged"] = newItems.Length - unchanged
            };
        }
        return result;
    }

    public static JObject CheckRetheme(JObject comparison)
    {
        bool ChangedMost(string type) => (int)comparison[type]["before"] > 0 &&
            (int)comparison[type]["after"] > 0 &&
            (double)comparison[type]["removedOrChanged"] / (int)comparison[type]["before"] >= 0.8;
        bool Preserved(string type) => (int)comparison[type]["removedOrChanged"] == 0 &&
            (int)comparison[type]["addedOrChanged"] == 0;
        var checks = new JObject
        {
            ["terrainChanged"] = ChangedMost("tile"),
            ["sceneryChanged"] = ChangedMost("object"),
            ["backgroundsChanged"] = ChangedMost("background"),
            ["functionalStatePreserved"] = new[] { "npc", "mob", "portal", "reactor", "foothold", "rope", "ladder" }.All(Preserved),
        };
        checks["passed"] = checks.Properties().All(p => p.Value.Value<bool>());
        checks["scope"] = "Structural acceptance only: at least 80% of each original visual type changed, replacements exist, and functional spatial entries are unchanged. Human crop review still required for theme and alignment.";
        return checks;
    }
}
