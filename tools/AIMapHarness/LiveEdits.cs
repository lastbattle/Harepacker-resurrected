using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

static class LiveEdits
{
    public static JObject Run(Board board, string id, string output)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var serializer = new MapAISerializer(board);
        var parser = new MapAIParser(); var executor = new MapAIExecutor(board);
        string State()
        {
            int offset = 0; JObject state = null; var elements = new JArray();
            while (true)
            {
                var page = JObject.Parse(serializer.GenerateSpatialState(new JObject { ["offset"] = offset, ["limit"] = 200 }));
                state ??= page;
                foreach (var element in (JArray)page["elements"]) elements.Add(element.DeepClone());
                if (page["nextOffset"].Type == JTokenType.Null) break;
                offset = (int)page["nextOffset"];
            }
            state["elements"] = elements; state["snapshotPaginationCompleted"] = true;
            state["nextOffset"] = null;
            return state.ToString(Formatting.None);
        }
        string Snapshot() => new JArray(board.BoardItems.Items.Cast<BoardItem>().Select(i => new JObject
        {
            ["kind"] = i.GetType().Name,
            ["x"] = i.X,
            ["y"] = i.Y,
            ["z"] = i.Z,
            ["flip"] = i.IsFlipped()
        }))
            .ToString(Formatting.None) + State();
        string before = Snapshot();
        var beforeState = JObject.Parse(State());
        SceneEvidence.Capture(board, beforeState, output, id + "-before");
        int undoBefore = board.UndoRedoMan.UndoList.Count;
        var log = new JArray(); var commands = new JArray();
        void Log(string kind, string name, object detail)
        {
            var item = new JObject { ["kind"] = kind, ["name"] = name, ["detail"] = detail == null ? null : JToken.FromObject(detail) };
            log.Add(item); File.AppendAllText(Path.Combine(output, id + "-live-calls.jsonl"), item.ToString(Formatting.None) + "\n");
        }
        using var server = new MapMcpToolServer();
        server.RichQueryExecutor = (name, args) => dispatcher.Invoke(() =>
        {
            if (name == "get_map_view")
            {
                var result = MapAIVisualRenderer.RenderMap(board, args);
                string stem = id + "-live-view-" + log.Count;
                File.WriteAllBytes(Path.Combine(output, stem + ".png"), Convert.FromBase64String((string)result[1]["data"]));
                Log("image", name, new { args, file = stem + ".png", metadata = JObject.Parse((string)result[0]["text"]) });
                return result;
            }
            if (name == "get_asset_preview") { var result = MapAIVisualRenderer.RenderAssets(args); string file = id + "-asset-" + log.Count + ".png"; if (result.Count > 1) File.WriteAllBytes(Path.Combine(output, file), Convert.FromBase64String((string)result[1]["data"])); Log("asset", name, new { args, file, metadata = result[0] }); return result; }
            return null;
        });
        server.QueryExecutor = (name, args) => dispatcher.Invoke(() =>
        {
            Log("query", name, args);
            var answer = name switch { "get_map_state" => serializer.GenerateSpatialState(args), "get_map_info" => serializer.GenerateAISummary(), _ => MapEditorFunctions.ExecuteQueryFunction(name, args) }; Log("query_result", name, answer); return answer;
        });
        server.CommandExecutor = command => dispatcher.Invoke(() =>
        {
            var result = executor.ExecuteCommands(parser.ParseCommands(command));
            commands.Add(new JObject { ["command"] = command, ["success"] = result.IsSuccess, ["log"] = JArray.FromObject(result.Log) });
            Log("command", command, new { result.IsSuccess, result.SuccessCount, result.FailCount, result.Log });
            return (result.IsSuccess ? "" : "# ERROR: ") + string.Join("\n", result.Log);
        });
        string prompt = id switch
        {
            "scratch" => "Create a small grassy training map from this empty board. First set map dimensions to 2400x1600 and camera VR to left=-550, top=-350, right=1750, bottom=1150. Use existing grassySoil assets. Build a 900-pixel-wide ground platform with walking surface y=500 from x=0 to x=900; a 360-pixel raised platform at y=320 with a rope connecting to the ground. Add appropriate visible rope artwork, a spawn portal on safe ground and one Blue Snail 100100 with patrol bounds on that ground. Add a matching sky background behind the playable artwork. Inspect actual asset images and native footholds before placing, and verify map size, VR, geometry and a close-up afterward. Keep additions inside the VR and correct any alignment errors.",
            "100000000" => "Edit Henesys gently: move the leftmost NPC 32 pixels to the right along the same walking surface, keeping its feet grounded. Add one small flower decoration nearby that fits the village style without covering NPCs, portals, or ropes. Inspect actual map and asset images, make the changes, and verify a close-up. Preserve everything else.",
            "100010000" => "Add one small optional grassy platform and a rope connecting it to an existing walkable surface in an open area of this map. Match existing tile artwork, keep the new platform 300 to 400 pixels wide and 150 to 200 pixels above its access surface. Avoid covering NPCs, monsters and portals. Keep all existing content. Inspect tile footholds and backgrounds, apply, then verify and correct using close-up images and geometry.",
            _ => "Move the existing reactor 24 pixels to the right along the same surface, preserving its type and properties. Add one Blue Snail (100100) on a nearby safe foothold without overlapping the reactor or a portal. Also add one subtle decorative background from an existing background set that fits the scene, using an asset preview first, and verify that it does not obscure playable artwork. Preserve existing backgrounds and all other map content. Apply changes and inspect close-up images to verify them."
        };
        prompt = Environment.GetEnvironmentVariable("HACREATOR_LIVE_PROMPT") ?? prompt;
        File.WriteAllText(Path.Combine(output, id + "-prompt.txt"), prompt);
        var timer = Stopwatch.StartNew();
        var options = AISettings.CreateMapEditorOptions();
        // Use the same persisted runtime options and context as the actual dialog.
        File.WriteAllText(Path.Combine(output, id + "-runtime.json"), new JObject
        {
            ["model"] = options.Model,
            ["reasoningEffort"] = options.ReasoningEffort,
            ["maxToolTurns"] = options.MaxToolTurns,
            ["maxOutputTokens"] = options.MaxOutputTokens
        }.ToString());
        using var client = new OpenAICompatibleClient(options, server);
        client.Progress += message => Log("progress", "client", message);
        var replay = Environment.GetEnvironmentVariable("HACREATOR_REPLAY_DIRECTORY");
        if (!string.IsNullOrEmpty(replay))
            foreach (var line in File.ReadAllLines(Path.Combine(replay, id + "-live-calls.jsonl")))
            {
                var call = JObject.Parse(line); if ((string)call["kind"] == "command") server.CommandExecutor((string)call["name"]);
            }
        string context = serializer.GenerateAISummary();
        File.WriteAllText(Path.Combine(output, id + "-context.txt"), context);
        var task = !string.IsNullOrEmpty(replay) ? Task.FromResult("Replayed recorded commands") : client.ProcessConversationAsync(context, prompt, new JArray(), MapAIVisualRenderer.RenderMap(board, new JObject()), true);
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
        Dispatcher.PushFrame(frame);
        string response = null, error = null;
        try { response = task.GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(output, id + "-response.txt"), response); }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        string after = Snapshot(); var afterState = JObject.Parse(State());
        SceneEvidence.Capture(board, afterState, output, id + "-after", beforeState["globalGeometryBounds"] as JArray);
        var comparison = SceneEvidence.Compare(beforeState, afterState);
        File.WriteAllText(Path.Combine(output, id + "-comparison.json"), comparison.ToString());
        if (Environment.GetEnvironmentVariable("HACREATOR_REQUIRE_RETHEME") == "1")
        {
            var acceptance = SceneEvidence.CheckRetheme(comparison);
            File.WriteAllText(Path.Combine(output, id + "-acceptance.json"), acceptance.ToString());
            if (!(bool)acceptance["passed"]) error ??= "Visual retheme acceptance failed; inspect comparison and crop grids.";
        }
        var view = MapAIVisualRenderer.RenderMap(board, new JObject { ["overlays"] = false });
        File.WriteAllBytes(Path.Combine(output, id + "-after.png"), Convert.FromBase64String((string)view[1]["data"]));
        int batches = board.UndoRedoMan.UndoList.Count - undoBefore;
        for (int i = 0; i < batches; i++) board.UndoRedoMan.Undo();
        bool restored = Snapshot() == before;
        for (int i = 0; i < batches; i++) board.UndoRedoMan.Redo();
        string redo = Snapshot();
        File.WriteAllText(Path.Combine(output, id + "-snapshot-before.txt"), before);
        File.WriteAllText(Path.Combine(output, id + "-snapshot-after.txt"), after);
        File.WriteAllText(Path.Combine(output, id + "-snapshot-redo.txt"), redo);
        var redoView = MapAIVisualRenderer.RenderMap(board, new JObject { ["overlays"] = false });
        File.WriteAllBytes(Path.Combine(output, id + "-redo.png"), Convert.FromBase64String((string)redoView[1]["data"]));
        bool redone = redo == after;
        // Leave the verified result on this disposable board for the actual dialog screenshot.
        return new JObject { ["elapsedSeconds"] = timer.Elapsed.TotalSeconds, ["commands"] = commands, ["error"] = error, ["undoRestored"] = restored, ["redoRestored"] = redone, ["undoBatches"] = batches, ["changed"] = before != after, ["comparison"] = comparison, ["pngRedoIdentical"] = File.ReadAllBytes(Path.Combine(output, id + "-after.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(output, id + "-redo.png"))) };
    }
}
