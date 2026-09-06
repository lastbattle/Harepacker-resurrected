using MapleLib.WzLib.WzStructure;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using Newtonsoft.Json.Linq;
using HaCreator.GUI.EditorPanels;

static class ExistingMaps
{
    static string Output => Environment.GetEnvironmentVariable("HACREATOR_EXISTING_MAP_OUTPUT");
    public static void Run()
    {
        if (Directory.Exists(Output) && Directory.EnumerateFileSystemEntries(Output).Any())
            throw new InvalidOperationException("Choose an empty output directory to keep runs independent.");
        Directory.CreateDirectory(Output);
        var data = Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA");
        using var source = new ImgFileSystemDataSource(data);
        var program = typeof(Board).Assembly.GetType("HaCreator.Program");
        program.GetField("DataSource").SetValue(null, source);
        var info = new WzInformationManager();
        program.GetField("InfoManager").SetValue(null, info);
        foreach (var pair in new[] { (info.TileSets, "Tile"), (info.ObjectSets, "Obj"), (info.BackgroundSets, "Back") })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(data, "Map", pair.Item2), "*.img"))
                pair.Item1[Path.GetFileNameWithoutExtension(file)] = null;
        new ImgDataExtractor(source, info).ExtractMapPortals();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        if (Environment.GetEnvironmentVariable("HACREATOR_SCRATCH_TEST") == "1")
        {
            var parent = new MultiBoard();
            var menu = new System.Windows.Controls.ContextMenu();
            for (int i = 0; i < 3; i++) menu.Items.Add(new System.Windows.Controls.MenuItem());
            var b = new Board(new Microsoft.Xna.Framework.Point(1200, 800), new Microsoft.Xna.Framework.Point(600, 400), parent, true, menu,
                MapleLib.WzLib.WzStructure.Data.ItemTypes.All, MapleLib.WzLib.WzStructure.Data.ItemTypes.All);
            b.CreateMapLayers(); b.MapInfo.strMapName = "Scratch validation";
            var result = LiveEdits.Run(b, "scratch", Output);
            File.WriteAllText(Path.Combine(Output, "results.json"), result.ToString()); b.Dispose(); return;
        }
        if (Environment.GetEnvironmentVariable("HACREATOR_SCAN_REACTORS") == "1")
        {
            var found = new JArray();
            foreach (var f in Directory.EnumerateFiles(Path.Combine(data, "Map", "Map"), "*.img", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(Path.Combine(data, "Map"), f).Replace(Path.DirectorySeparatorChar, '/');
                var m = source.GetImage("Map", relative); m.ParseImage();
                int reactors = m["reactor"]?.WzProperties.Count ?? 0;
                if (reactors > 0) { found.Add(new JObject { ["id"] = Path.GetFileNameWithoutExtension(f), ["reactors"] = reactors }); if (found.Count == 5) break; }
            }
            File.WriteAllText(Path.Combine(Output, "reactor-candidates.json"), found.ToString()); return;
        }
        var report = new JArray();
        foreach (string id in (Environment.GetEnvironmentVariable("HACREATOR_TEST_MAPS") ?? "100000000,100000001,100010000,101000000,103000000").Split(','))
        {
            var entry = new JObject { ["mapId"] = id };
            report.Add(entry);
            Board board = null;
            try
            {
                var timer = Stopwatch.StartNew();
                string relative = $"Map/Map{id[0]}/{id}.img";
                string file = Path.Combine(data, "Map", relative);
                string beforeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
                var map = source.GetImage("Map", relative);
                map.ParseImage();
                MapLoader.GetMapDimensions(map, out var vr, out var center, out var size, out _, out _, out _, out _);
                var parent = new MultiBoard();
                board = parent.CreateBoard(size, center, null, false);
                board.Loading = true;
                board.MapInfo = new MapInfo(map, "", id, "") { id = int.Parse(id) };
                var stages = new JObject(); entry["loadStages"] = stages;
                foreach (var stage in new (string, Action<WzImage, Board>)[] {
                    ("layers",MapLoader.LoadLayers), ("life",MapLoader.LoadLife),
                    ("footholds",MapLoader.LoadFootholds), ("ropes",MapLoader.LoadRopes),
                    ("chairs",MapLoader.LoadChairs), ("portals",MapLoader.LoadPortals),
                    ("reactors",MapLoader.LoadReactors), ("backgrounds",MapLoader.LoadBackgrounds) })
                {
                    try { stage.Item2(map, board); stages[stage.Item1] = "ok"; }
                    catch (Exception ex) { stages[stage.Item1] = ex.GetType().Name + ": " + ex.Message; }
                }
                MapLoader.GenerateDefaultZms(board);
                board.BoardItems.Sort(); board.Loading = false;
                entry["loadMs"] = timer.ElapsedMilliseconds;
                entry["loaded"] = Counts(board);
                var expected = new JObject
                {
                    ["tiles"] = Enumerable.Range(0, 8).Sum(i => map[i.ToString()]?["tile"]?.WzProperties.Count ?? 0),
                    ["objects"] = Enumerable.Range(0, 8).Sum(i => map[i.ToString()]?["obj"]?.WzProperties.Count ?? 0),
                    ["npcs"] = map["life"]?.WzProperties.Count(p => p["type"]?.ToString() == "n") ?? 0,
                    ["monsters"] = map["life"]?.WzProperties.Count(p => p["type"]?.ToString() == "m") ?? 0,
                    ["reactors"] = map["reactor"]?.WzProperties.Count ?? 0,
                    ["ropes"] = map["ladderRope"]?.WzProperties.Count ?? 0,
                    ["backgrounds"] = map["back"]?.WzProperties.Count ?? 0,
                    ["footholds"] = map["foothold"]?.WzProperties.Sum(l => l.WzProperties.Sum(g => g.WzProperties.Count)) ?? 0
                };
                entry["sourceCounts"] = expected;
                entry["countsMatch"] = expected.Properties().All(p => JToken.DeepEquals(p.Value, entry["loaded"][p.Name]));
                timer.Restart(); entry["overview"] = Save(board, id + "-overview", new JObject { ["overlays"] = false });
                entry["renderMs"] = timer.ElapsedMilliseconds;
                Save(board, id + "-geometry", new JObject());
                var crops = new JArray(); entry["crops"] = crops;
                foreach (var target in new (string, BoardItem)[] {
                    ("npc",board.BoardItems.NPCs.FirstOrDefault()), ("monster",board.BoardItems.Mobs.FirstOrDefault()),
                    ("reactor",board.BoardItems.Reactors.FirstOrDefault()), ("object",board.BoardItems.TileObjs.OfType<ObjectInstance>().FirstOrDefault()) })
                {
                    if (target.Item2 == null) continue;
                    var item = target.Item2;
                    crops.Add(Save(board, id + "-" + target.Item1, new JObject { ["x"] = item.X - 250, ["y"] = item.Y - 300, ["width"] = 500, ["height"] = 400, ["overlays"] = false }));
                }
                if (Environment.GetEnvironmentVariable("HACREATOR_LIVE_EDITS") == "1") entry["liveEdit"] = LiveEdits.Run(board, id, Output);
                var window = AIMapEditWindow.GetOrCreate(board);
                window.Show(); window.LoadMapContext();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                SaveUi(window, id + "-ui");
                window.Close();
                entry["closeHidesWindow"] = !window.IsVisible;
                var same = AIMapEditWindow.GetOrCreate(board);
                entry["reopenPreservesWindow"] = ReferenceEquals(window, same);
                same.Show();
                same.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                AIMapEditWindow.CloseForBoard(board);
                var instances = (System.Collections.IDictionary)typeof(AIMapEditWindow).GetField("instances", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
                entry["closedWindowRemoved"] = !instances.Contains(board);
                board.Dispose(); board = null;
                entry["closedBoardRemoved"] = parent.Boards.Count == 0;
                entry["sourceUnchanged"] = beforeHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            }
            catch (Exception ex) { entry["error"] = ex.ToString().Replace(data, "<v95>").Replace(Output, "<artifacts>"); }
            finally { if (board != null) { AIMapEditWindow.CloseForBoard(board); board.Dispose(); } }
            File.WriteAllText(Path.Combine(Output, "results.json"), report.ToString());
        }
    }
    static JObject Counts(Board b) => new JObject
    {
        ["tiles"] = b.BoardItems.TileObjs.OfType<TileInstance>().Count(),
        ["objects"] = b.BoardItems.TileObjs.OfType<ObjectInstance>().Count(),
        ["npcs"] = b.BoardItems.NPCs.Count,
        ["monsters"] = b.BoardItems.Mobs.Count,
        ["reactors"] = b.BoardItems.Reactors.Count,
        ["ropes"] = b.BoardItems.Ropes.Count,
        ["footholds"] = b.BoardItems.FootholdLines.Count,
        ["backgrounds"] = b.BoardItems.BackBackgrounds.Count + b.BoardItems.FrontBackgrounds.Count
    };
    static JObject Save(Board b, string name, JObject args)
    {
        var view = MapAIVisualRenderer.RenderMap(b, args);
        var bytes = Convert.FromBase64String((string)view[1]["data"]);
        File.WriteAllBytes(Path.Combine(Output, name + ".png"), bytes);
        var meta = JObject.Parse((string)view[0]["text"]); meta["pngBytes"] = bytes.Length; meta["file"] = name + ".png";
        File.WriteAllText(Path.Combine(Output, name + ".json"), meta.ToString()); return meta;
    }
    internal static void SaveUi(AIMapEditWindow w, string name)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(w); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(Output, name + ".png")); encoder.Save(stream);
    }
}
