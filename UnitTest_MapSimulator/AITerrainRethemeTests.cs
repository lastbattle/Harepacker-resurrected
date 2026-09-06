using System.Threading;
using HaCreator;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AITerrainRethemeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BulkRethemePreservesInstancesGeometryAndExactUndo(bool onlyOneLayer) => OnBoard((board, executor) =>
    {
        var source = Asset("grass", "enH0", "7");
        var target = Asset("snow", "enH0", "2");
        var tiles = new[] { Tile(board, source, 0, -190), Tile(board, source, 2, 530) };
        var lines = board.BoardItems.FootholdLines.ToArray();
        var anchors = board.BoardItems.FHAnchors.ToArray();
        var geometry = tiles.Select(tile => (tile.X, tile.Y, tile.Z, tile.LayerNumber, tile.PlatformNumber)).ToArray();
        var args = new JObject { ["tileset"] = "snow" };
        if (onlyOneLayer) args["layer"] = 0;
        var parsed = new MapAIParser().ParseCommand(MapEditorFunctions.FunctionCallToCommand("change_tileset", args));
        Assert.Equal(CommandType.ChangeTileset, parsed.Type);
        Assert.True(executor.ExecuteCommand(parsed), string.Join("\n", executor.ExecutionLog));
        Assert.Same(target, tiles[0].BaseInfo);
        Assert.Same(onlyOneLayer ? source : target, tiles[1].BaseInfo);
        Assert.Single(board.UndoRedoMan.UndoList);
        board.UndoRedoMan.Undo();
        Assert.All(tiles, tile => Assert.Same(source, tile.BaseInfo));
        Assert.Equal("grass", board.Layers[0].tS);
        Assert.Equal("grass", board.Layers[2].tS);
        board.UndoRedoMan.Redo();
        Assert.Same(target, tiles[0].BaseInfo);
        Assert.Equal("snow", board.Layers[0].tS);
        Assert.Same(onlyOneLayer ? source : target, tiles[1].BaseInfo);
        Assert.Equal(geometry, tiles.Select(tile => (tile.X, tile.Y, tile.Z, tile.LayerNumber, tile.PlatformNumber)).ToArray());
        Assert.Equal(lines, board.BoardItems.FootholdLines.ToArray());
        Assert.Equal(anchors, board.BoardItems.FHAnchors.ToArray());
        Assert.Contains(executor.ExecutionLog, line => line.Contains("fallback variant"));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrIncompatibleCategoryFailsWithoutPartialChanges(bool incompatible) => OnBoard((board, executor) =>
    {
        var source = Asset("grass", "enH0", "7");
        var second = Asset("grass", "edU", "0");
        Asset("snow", "enH0", "0");
        if (incompatible) Asset("snow", "edU", "0").FootholdOffsets.Add(new XnaPoint(0, 100));
        var tiles = new[] { Tile(board, source, 0, 0), Tile(board, second, 2, 300) };
        Assert.False(executor.ExecuteCommand(new MapAIParser().ParseCommand("CHANGE TILESET tileset=\"snow\"")));
        Assert.Same(source, tiles[0].BaseInfo);
        Assert.Same(second, tiles[1].BaseInfo);
        Assert.Equal("grass", board.Layers[0].tS);
        Assert.Equal("grass", board.Layers[2].tS);
        Assert.Empty(board.UndoRedoMan.UndoList);
    });

    [Fact]
    public void TranslatedArtworkPreservesWorldCollisionAndRestoresBindingOffsets() => OnBoard((board, executor) =>
    {
        var source = Asset("grass", "enH0", "7");
        var target = Asset("snow", "enH0", "0");
        for (int i = 0; i < target.FootholdOffsets.Count; i++)
            target.FootholdOffsets[i] += new XnaPoint(15, -23);
        var tile = Tile(board, source, 0, 250);
        var bindings = tile.BoundItems.ToDictionary(pair => pair.Key, pair => pair.Value);
        var anchors = board.BoardItems.FHAnchors.ToDictionary(anchor => anchor, anchor => new XnaPoint(anchor.X, anchor.Y));
        var position = new XnaPoint(tile.X, tile.Y);
        Assert.True(executor.ExecuteCommand(new MapAIParser().ParseCommand("CHANGE TILESET tileset=\"snow\"")), string.Join("\n", executor.ExecutionLog));
        Assert.Equal(position + new XnaPoint(-15, 23), new XnaPoint(tile.X, tile.Y));
        foreach (var anchor in anchors) Assert.Equal(anchor.Value, new XnaPoint(anchor.Key.X, anchor.Key.Y));
        var translatedBindings = tile.BoundItems.ToDictionary(pair => pair.Key, pair => pair.Value);
        board.UndoRedoMan.Undo();
        Assert.Same(source, tile.BaseInfo);
        Assert.Equal(position, new XnaPoint(tile.X, tile.Y));
        foreach (var binding in bindings) Assert.Equal(binding.Value, tile.BoundItems[binding.Key]);
        foreach (var anchor in anchors) Assert.Equal(anchor.Value, new XnaPoint(anchor.Key.X, anchor.Key.Y));
        board.UndoRedoMan.Redo();
        Assert.Same(target, tile.BaseInfo);
        foreach (var binding in translatedBindings) Assert.Equal(binding.Value, tile.BoundItems[binding.Key]);
        foreach (var anchor in anchors) Assert.Equal(anchor.Value, new XnaPoint(anchor.Key.X, anchor.Key.Y));
    });

    [Fact]
    public void DifferentSlopeShapeCannotBeAlignedByTranslation() => OnBoard((board, executor) =>
    {
        var source = Asset("grass", "slLU", "0");
        var target = Asset("snow", "slLU", "0");
        target.FootholdOffsets[1] += new XnaPoint(0, 10);
        var tile = Tile(board, source, 0, 250);
        Assert.False(executor.ExecuteCommand(new MapAIParser().ParseCommand("CHANGE TILESET tileset=\"snow\"")));
        Assert.Same(source, tile.BaseInfo);
        Assert.Equal(250, tile.X);
        Assert.Equal(20, tile.Y);
        Assert.Empty(board.UndoRedoMan.UndoList);
    });

    [Fact]
    public void ExplicitVisualRetexturePreservesAnchorCollisionAndExactUndo() => OnBoard((board, executor) =>
    {
        var original = Asset("wood", "edU", "1");
        var target = Asset("snow", "edU", "1");
        target.FootholdOffsets[1] += new XnaPoint(0, -26);
        var tile = Tile(board, original, 0, 250);
        var lines = board.BoardItems.FootholdLines.ToArray();
        var anchors = board.BoardItems.FHAnchors.ToDictionary(anchor => anchor, anchor => new XnaPoint(anchor.X, anchor.Y));
        var bindings = tile.BoundItems.ToDictionary(pair => pair.Key, pair => pair.Value);
        var args = new JObject { ["tileset"] = "snow", ["allow_shape_mismatch"] = true };
        var command = new MapAIParser().ParseCommand(MapEditorFunctions.FunctionCallToCommand("change_tileset", args));
        Assert.Equal(true, command.Parameters["allow_shape_mismatch"]);
        Assert.True(executor.ExecuteCommand(command), string.Join("\n", executor.ExecutionLog));
        Assert.Same(target, tile.BaseInfo);
        Assert.Equal((250, 20), (tile.X, tile.Y));
        Assert.Contains(executor.ExecutionLog, line => line.Contains("1 tiles have different native foothold templates"));
        board.UndoRedoMan.Undo();
        Assert.Same(original, tile.BaseInfo);
        Assert.Equal("wood", board.Layers[0].tS);
        board.UndoRedoMan.Redo();
        Assert.Same(target, tile.BaseInfo);
        Assert.Equal("snow", board.Layers[0].tS);
        Assert.Equal((250, 20), (tile.X, tile.Y));
        Assert.Equal(lines, board.BoardItems.FootholdLines.ToArray());
        foreach (var anchor in anchors) Assert.Equal(anchor.Value, new XnaPoint(anchor.Key.X, anchor.Key.Y));
        foreach (var binding in bindings) Assert.Equal(binding.Value, tile.BoundItems[binding.Key]);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisualRetextureCannotOverrideMissingCategoriesOrScale(bool wrongScale) => OnBoard((board, executor) =>
    {
        var original = Asset("wood", "edU", "1");
        var target = Asset("snow", wrongScale ? "edU" : "bsc", "1");
        if (wrongScale) target.mag = 2;
        var tile = Tile(board, original, 0, 250);
        Assert.False(executor.ExecuteCommand(new MapAIParser().ParseCommand("CHANGE TILESET tileset=\"snow\" allow_shape_mismatch=true")));
        Assert.Same(original, tile.BaseInfo);
        Assert.Equal((250, 20), (tile.X, tile.Y));
        Assert.Empty(board.UndoRedoMan.UndoList);
    });

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void ObjectDecorationCanSkipNativeBindingsAndUndoExactly(bool? createBindings) => OnBoard((board, executor) =>
    {
        var image = new WzImage("testObjects.img") { Parsed = true };
        var l0 = new WzSubProperty("test");
        var l1 = new WzSubProperty("shape");
        var l2 = new WzSubProperty("0");
        image.AddProperty(l0);
        l0.AddProperty(l1);
        l1.AddProperty(l2);
        var info = new ObjectInfo(new System.Drawing.Bitmap(10, 10), System.Drawing.Point.Empty,
            "testObjects", "test", "shape", "0", l2);
        typeof(ObjectInfo).GetField("footholdOffsets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(info, new List<List<XnaPoint>> { new() { new(0, 0), new(90, 0) } });
        typeof(ObjectInfo).GetField("chairOffsets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(info, new List<XnaPoint> { new(30, -10) });
        l2.HCTag = info;
        Program.InfoManager.ObjectSets["testObjects"] = image;
        // Existing map geometry must survive placement, undo and redo in every mode.
        var existing = Tile(board, Asset("grass", "enH0", "0"), 0, -200);
        var originalAnchors = board.BoardItems.FHAnchors.ToArray();
        var originalLines = board.BoardItems.FootholdLines.ToArray();
        var args = new JObject
        {
            ["oS"] = "testObjects", ["l0"] = "test", ["l1"] = "shape", ["l2"] = "0",
            ["x"] = 100, ["y"] = 50, ["layer"] = 0
        };
        if (createBindings.HasValue) args["create_bindings"] = createBindings.Value;
        var command = new MapAIParser().ParseCommand(MapEditorFunctions.FunctionCallToCommand("add_object", args));
        Assert.True(executor.ExecuteCommand(command), string.Join("\n", executor.ExecutionLog));
        var obj = Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>());
        bool expectedBindings = createBindings ?? true;
        Assert.Equal(expectedBindings ? 3 : 0, obj.BoundItems.Count);
        Assert.Equal(originalAnchors.Length + (expectedBindings ? 2 : 0), board.BoardItems.FHAnchors.Count);
        Assert.Equal(originalLines.Length + (expectedBindings ? 1 : 0), board.BoardItems.FootholdLines.Count);
        Assert.Equal(expectedBindings ? 1 : 0, board.BoardItems.Chairs.Count);
        var placedAnchors = board.BoardItems.FHAnchors.ToArray();
        var placedLines = board.BoardItems.FootholdLines.ToArray();
        var placedChairs = board.BoardItems.Chairs.ToArray();
        board.UndoRedoMan.Undo();
        Assert.Empty(board.BoardItems.TileObjs.OfType<ObjectInstance>());
        Assert.Equal(originalAnchors, board.BoardItems.FHAnchors.ToArray());
        Assert.Equal(originalLines, board.BoardItems.FootholdLines.ToArray());
        Assert.Empty(board.BoardItems.Chairs);
        board.UndoRedoMan.Redo();
        Assert.Same(obj, Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>()));
        Assert.Equal(placedAnchors, board.BoardItems.FHAnchors.ToArray());
        Assert.Equal(placedLines, board.BoardItems.FootholdLines.ToArray());
        Assert.Equal(placedChairs, board.BoardItems.Chairs.ToArray());
        Assert.Contains(existing, board.BoardItems.TileObjs);
    });

    [V95PlacementFact]
    public void WoodMarbleToSnowyTilesetsReportsCompatibilityForEveryVariant() => OnBoard((board, executor) =>
    {
        var previousSource = Program.DataSource;
        try
        {
            using var source = new MapleLib.Img.ImgFileSystemDataSource(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")!);
            Program.DataSource = source;
            Program.InfoManager.TileSets["woodMarble"] = null;
            var targets = source.GetImageNamesInDirectory("Map", "Tile")
                .Select(name => System.IO.Path.GetFileNameWithoutExtension(name)!)
                .Where(name => name.StartsWith("snow", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
            Assert.NotEmpty(targets);
            var originalSet = Program.InfoManager.GetTileSet("woodMarble");
            Assert.NotNull(originalSet);
            int totalCompatible = 0;
            foreach (string targetSetName in targets)
            {
            Program.InfoManager.TileSets[targetSetName] = null;
            var targetSet = Program.InfoManager.GetTileSet(targetSetName);
            Assert.NotNull(targetSet);
            output.WriteLine($"TARGET: {targetSetName}");
            if (targetSet["edU"] != null)
                foreach (var variant in targetSet["edU"].WzProperties.OfType<WzCanvasProperty>())
                {
                    var info = TileInfo.Get(targetSetName, "edU", variant.Name);
                    output.WriteLine($"target edU/{variant.Name}: mag={info.mag}; offsets={string.Join(";", info.FootholdOffsets)}");
                }
            else output.WriteLine("target edU: missing category");
            int tested = 0, compatible = 0;
            foreach (var category in originalSet.WzProperties.Where(prop => prop.Name != "info"))
            foreach (var variant in category.WzProperties.OfType<WzCanvasProperty>())
            {
                var original = TileInfo.Get("woodMarble", category.Name, variant.Name);
                var tile = Tile(board, original, 0, 250);
                var anchors = board.BoardItems.FHAnchors.ToDictionary(anchor => anchor, anchor => new XnaPoint(anchor.X, anchor.Y));
                int logStart = executor.ExecutionLog.Count;
                bool success = executor.ExecuteCommand(new MapAIParser().ParseCommand($"CHANGE TILESET tileset=\"{targetSetName}\" layer=0"));
                tested++;
                output.WriteLine($"{category.Name}/{variant.Name}: {(success ? "compatible" : "incompatible")}; source offsets={string.Join(";", original.FootholdOffsets)}");
                foreach (string line in executor.ExecutionLog.Skip(logStart)) output.WriteLine(line);
                if (success)
                {
                    compatible++;
                    var target = (TileInfo)tile.BaseInfo;
                    output.WriteLine($"target {target.u}/{target.no}: {string.Join(";", target.FootholdOffsets)}, anchor ({tile.X},{tile.Y})");
                    foreach (var anchor in anchors) Assert.Equal(anchor.Value, new XnaPoint(anchor.Key.X, anchor.Key.Y));
                    board.UndoRedoMan.Undo();
                    Assert.Same(original, tile.BaseInfo);
                    Assert.Equal(250, tile.X);
                    Assert.Equal(20, tile.Y);
                }
                else
                {
                    Assert.Same(original, tile.BaseInfo);
                    Assert.Equal(250, tile.X);
                    Assert.Equal(20, tile.Y);
                }
                tile.RemoveItem(null);
            }
            output.WriteLine($"{targetSetName}: compatible variants {compatible}/{tested}");
            totalCompatible += compatible;
            Assert.True(tested > 0, "No woodMarble variants found");
            }
            Assert.True(totalCompatible > 0, "No woodMarble variants can be rethemed to any available snowy tileset by translation.");
        }
        finally { Program.DataSource = previousSource; }
    }, timeoutSeconds: 120);

    private static TileInfo Asset(string set, string category, string number)
    {
        if (!Program.InfoManager.TileSets.TryGetValue(set, out var image))
            Program.InfoManager.TileSets[set] = image = new WzImage(set + ".img") { Parsed = true };
        var group = image[category];
        if (group == null) image.AddProperty(group = new WzSubProperty(category));
        var prop = new WzCanvasProperty(number);
        ((WzSubProperty)group).AddProperty(prop);
        var info = new TileInfo(new System.Drawing.Bitmap(1, 1), System.Drawing.Point.Empty, set, category, number, 1, 0, prop);
        info.FootholdOffsets.Add(new XnaPoint(0, 0));
        info.FootholdOffsets.Add(new XnaPoint(90, 0));
        prop.HCTag = info;
        return info;
    }

    private static TileInstance Tile(Board board, TileInfo info, int layer, int x)
    {
        var tile = (TileInstance)info.CreateInstance(board.Layers[layer], board, x, 20, 11, 3, false, true);
        tile.AddToBoard(null);
        return tile;
    }

    private static void OnBoard(Action<Board, MapAIExecutor> action, int timeoutSeconds = 30)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previous = Program.InfoManager;
            try
            {
                Program.InfoManager = new WzInformationManager();
                var parent = new MultiBoard();
                var menu = new System.Windows.Controls.ContextMenu();
                for (int i = 0; i < 3; i++) menu.Items.Add(new System.Windows.Controls.MenuItem());
                var board = new Board(new XnaPoint(1200, 800), new XnaPoint(600, 400), parent, true, menu, ItemTypes.All, ItemTypes.All);
                board.CreateMapLayers();
                action(board, new MapAIExecutor(board));
            }
            catch (Exception ex) { failure = ex; }
            finally { Program.InfoManager = previous; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(timeoutSeconds)), "Terrain test timed out");
        Assert.Null(failure);
    }
}
