using Newtonsoft.Json.Linq;
using System.IO;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (Environment.GetEnvironmentVariable("HACREATOR_COMPACT_BENCHMARK") is string benchmarkDirectory)
        {
            Directory.CreateDirectory(benchmarkDirectory);
            using var server = new HaCreator.MapEditor.AI.MapMcpToolServer();
            var legacy = server.GetResponsesTools(true);
            foreach (var tool in legacy.Where(t => (string)t["name"] is "edit_map" or "get_edit_help").ToList()) tool.Remove();
            File.WriteAllText(Path.Combine(benchmarkDirectory, "legacy-tools.json"), legacy.ToString(Newtonsoft.Json.Formatting.None));
            File.WriteAllText(Path.Combine(benchmarkDirectory, "compact-tools.json"), server.GetResponsesTools(true, compactOnly: true).ToString(Newtonsoft.Json.Formatting.None));
            if (Environment.GetEnvironmentVariable("HACREATOR_COMPACT_CORPUS") is string corpus)
            {
                var calls = File.ReadLines(corpus).Select(JObject.Parse).Where(c => (string)c["kind"] == "command").ToList();
                var actions = File.ReadLines(corpus).Select(JObject.Parse).Where(c => (string)c["kind"] == "action" && c["detail"]["arguments"] is JObject).ToList();
                File.WriteAllText(Path.Combine(benchmarkDirectory, "grouped-edits.txt"), HaCreator.MapEditor.AI.CompactMapEdits.Encode(actions.Select(c => new HaCreator.MapEditor.AI.CompactMapEdits.Edit((string)c["name"], (JObject)c["detail"]["arguments"]))));
                var receipts = calls.Select(c => HaCreator.MapEditor.AI.MapMcpToolCallResult.Action("action", (string)c["name"], string.Join("\n", ((JArray)c["detail"]["Log"]).Values<string>()))).ToList();
                File.WriteAllText(Path.Combine(benchmarkDirectory, "legacy-receipts.txt"), string.Join("\n", receipts.Select(r => r.Text)));
                File.WriteAllText(Path.Combine(benchmarkDirectory, "compact-receipts.txt"), string.Join("\n", receipts.Select(r => r.CompactFeedback())));
            }
            return 0;
        }
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HACREATOR_EXISTING_MAP_OUTPUT")))
        {
            Console.Error.WriteLine("Set HACREATOR_AI_TEST_DATA and HACREATOR_EXISTING_MAP_OUTPUT. See README.md.");
            return 2;
        }
        try
        {
            ExistingMaps.Run();
            var report = Newtonsoft.Json.Linq.JToken.Parse(File.ReadAllText(Path.Combine(
                Environment.GetEnvironmentVariable("HACREATOR_EXISTING_MAP_OUTPUT"), "results.json")));
            IEnumerable<JToken> entries = report is Newtonsoft.Json.Linq.JArray array ? array.Children() : new[] { report };
            bool failed = entries.Any(entry =>
            {
                var live = entry["liveEdit"] ?? entry;
                return entry["error"]?.Type == JTokenType.String ||
                    new[] { "countsMatch", "sourceUnchanged", "closedWindowRemoved", "closedBoardRemoved" }
                        .Any(key => entry[key]?.Value<bool>() == false) ||
                    live["error"]?.Type == JTokenType.String ||
                    new[] { "undoRestored", "redoRestored", "pngRedoIdentical" }
                        .Any(key => live[key]?.Value<bool>() == false);
            });
            return failed ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.GetType().Name + ": harness failed; inspect local artifacts.");
            return 1;
        }
    }
}
