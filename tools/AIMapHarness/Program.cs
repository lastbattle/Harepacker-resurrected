using Newtonsoft.Json.Linq;
using System.IO;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
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
