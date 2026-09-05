using HaCreator.MapEditor.AI;
using Newtonsoft.Json.Linq;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance.Shapes;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public class AIMapToolTests
{
    [Theory]
    [InlineData("move_element", "{\"element_type\":\"object\",\"to_x\":100,\"to_y\":20}")]
    [InlineData("move_element", "{\"element_type\":\"portal\",\"name\":\"\",\"to_x\":100,\"to_y\":20}")]
    [InlineData("remove_element", "{\"element_type\":\"object\",\"name\":\"anything\"}")]
    [InlineData("remove_element", "{\"element_type\":\"object\",\"x\":10}")]
    [InlineData("remove_element", "{\"element_type\":\"portal\",\"name\":\"sp\",\"x\":10,\"y\":20}")]
    public void IncompleteOrConflictingSelectorsCannotInvokeAnAction(string tool, string json)
    {
        using var server = new MapMcpToolServer();
        int calls = 0;
        server.ActionExecutor = (_, _) => { calls++; return "DELETE ALL"; };
        var result = server.CallTool(tool, JObject.Parse(json));
        Assert.False(result.Success);
        Assert.Equal(0, calls);
        Assert.StartsWith("# ERROR:", MapEditorFunctions.FunctionCallToCommand(tool, JObject.Parse(json)));
    }

    [Fact]
    public void TileMutationPreservesExactSourceAndLayer()
    {
        using var server = new MapMcpToolServer();
        var move = server.CallTool("move_element", new JObject
        {
            ["element_type"] = "tile", ["from_x"] = 290, ["from_y"] = 25,
            ["to_x"] = 60, ["to_y"] = 25, ["layer"] = 2
        });
        Assert.True(move.Success, move.Text);
        Assert.Equal("MOVE TILE at (290, 25) to (60, 25) layer=2", move.Command);
        var remove = server.CallTool("remove_element", new JObject
        {
            ["element_type"] = "tile", ["x"] = 60, ["y"] = 25, ["layer"] = 2
        });
        Assert.True(remove.Success, remove.Text);
        Assert.Equal("DELETE TILE at (60, 25) layer=2", remove.Command);
    }

    [Fact]
    public void SpatialPaginationPreservesGlobalBoundsAndFiltersIntersectingLines()
    {
        var board = new Board(new Microsoft.Xna.Framework.Point(1000, 1000), Microsoft.Xna.Framework.Point.Zero,
            null, false, null, ItemTypes.None, ItemTypes.None);
        for (int i = 0; i < 3; i++)
        {
            var first = new FootholdAnchor(board, i * 100, i * 20, 0, i, true);
            var second = new FootholdAnchor(board, i * 100 + 80, i * 20, 0, i, true);
            board.BoardItems.FootholdLines.Add(new FootholdLine(board, first, second));
        }
        var serializer = new MapAISerializer(board);
        var page = JObject.Parse(serializer.GenerateSpatialState(new JObject { ["offset"] = 1, ["limit"] = 1 }));
        Assert.Equal(3, page["matched"]!.Value<int>());
        Assert.Equal(2, page["nextOffset"]!.Value<int>());
        Assert.Equal(1, page["elements"]![0]!["sourceIndex"]!.Value<int>());
        Assert.Equal(new[] { 0, 0, 280, 40 }, page["globalGeometryBounds"]!.Values<int>());

        var region = JObject.Parse(serializer.GenerateSpatialState(new JObject
        {
            ["x"] = 30, ["y"] = -1, ["width"] = 10, ["height"] = 2, ["element_type"] = "foothold"
        }));
        Assert.Equal(1, region["matched"]!.Value<int>()); // Line crosses the region; neither endpoint is inside.
        Assert.Equal(new[] { 0, 0, 280, 40 }, region["globalGeometryBounds"]!.Values<int>());
        Assert.Equal(JTokenType.Null, region["nextOffset"]!.Type);
    }

    [Fact]
    public void SpatialRegionMustBeComplete()
    {
        Assert.StartsWith("Error:", new MapAISerializer(null).GenerateSpatialState(new JObject { ["x"] = 0 }));
    }

    [Fact]
    public void RichQueryPreservesImagesAndDoesNotInvokeFallback()
    {
        using var server = new MapMcpToolServer();
        var calls = 0;
        server.RichQueryExecutor = (_, _) =>
        {
            calls++;
            return new JArray(
                new JObject { ["type"] = "text", ["text"] = "World crop: 0,0,100,100" },
                new JObject { ["type"] = "image", ["mimeType"] = "image/png", ["data"] = "aW1hZ2U=" });
        };
        server.QueryExecutor = (_, _) => throw new InvalidOperationException("Fallback must not run");
        var result = server.CallTool("get_map_view", new JObject());
        Assert.True(result.Success);
        Assert.Equal(1, calls);
        Assert.Equal("World crop: 0,0,100,100", result.Text);
        Assert.Equal("image", result.Content[1]?["type"]?.ToString());
        Assert.Equal("aW1hZ2U=", result.Content[1]?["data"]?.ToString());
    }

    [Fact]
    public void FailedDiscoveryDoesNotAuthorizeMutation()
    {
        using var server = new MapMcpToolServer();
        server.QueryExecutor = (_, _) => "Error: asset does not exist";
        Assert.False(server.CallTool("get_object_info", new JObject { ["oS"] = "missing" }).Success);
        var result = server.CallTool("add_object", new JObject
        {
            ["oS"] = "missing", ["l0"] = "0", ["l1"] = "0", ["l2"] = "0", ["x"] = 0, ["y"] = 0
        });
        Assert.False(result.Success);
        Assert.Contains("get_object_info", result.Text);
    }

    [Fact]
    public void MissingRequiredCoordinatesCannotReachExecutor()
    {
        using var server = new MapMcpToolServer();
        server.CommandExecutor = _ => throw new InvalidOperationException("Mutation must not run");
        var result = server.CallTool("add_chair", new JObject { ["x"] = 12, ["y"] = JValue.CreateNull() });
        Assert.False(result.Success);
        Assert.Contains("Missing required arguments: y", result.Text);
    }

    [Fact]
    public void StrictOptionalFieldsAreNullableAndNullMeansOmitted()
    {
        using var server = new MapMcpToolServer();
        var view = server.GetResponsesTools(true).OfType<JObject>().Single(t => t["name"]?.ToString() == "get_map_view");
        var schema = view["parameters"]!;
        Assert.Contains("x", schema["required"]!.Values<string>());
        Assert.Equal("null", schema["properties"]?["x"]?["anyOf"]?[1]?["type"]?.ToString());
        var args = new JObject { ["x"] = JValue.CreateNull(), ["maxDimension"] = JValue.CreateNull() };
        server.QueryExecutor = (_, supplied) => supplied.Count == 0 ? "default crop" : "Error: null was not omitted";
        var result = server.CallTool("get_map_view", args);
        Assert.True(result.Success);
        Assert.Equal("default crop", result.Text);
        Assert.Equal(2, args.Count); // Caller-owned arguments are not mutated.
    }

    [Fact]
    public void MalformedCoordinateCannotBecomeACommand()
    {
        using var server = new MapMcpToolServer();
        var result = server.CallTool("add_chair", new JObject { ["x"] = "12)\nCLEAR ALL", ["y"] = 0 });
        Assert.False(result.Success);
        Assert.Contains("must be integer", result.Text);
    }

    [Theory]
    [InlineData("hello\nCLEAR ALL")]
    [InlineData("hello\" extra=\"value")]
    public void CommandStringsCannotEscapeTheirQuotedArgument(string description)
    {
        using var server = new MapMcpToolServer();
        var definition = server.GetMcpTools().OfType<JObject>().Single(t => t["name"]?.ToString() == "set_map_desc");
        var parameter = definition["inputSchema"]!["required"]![0]!.ToString();
        var result = server.CallTool("set_map_desc", new JObject { [parameter] = description });
        Assert.False(result.Success);
        Assert.Contains("cannot contain", result.Text);
    }

    [Fact]
    public void NullRichResultUsesTextQueryOnce()
    {
        using var server = new MapMcpToolServer();
        var count = 0;
        server.RichQueryExecutor = (_, _) => null;
        server.QueryExecutor = (_, _) => { count++; return "map geometry"; };
        var result = server.CallTool("get_map_state", new JObject());
        Assert.True(result.Success);
        Assert.Equal(1, count);
        Assert.Single(result.Content);
        Assert.Equal("map geometry", result.Content[0]?["text"]?.ToString());
    }
}
