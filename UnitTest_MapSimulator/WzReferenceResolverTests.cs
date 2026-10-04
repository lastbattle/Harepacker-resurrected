using HaCreator;
using HaCreator.MapEditor.AI;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Moq;
using Newtonsoft.Json.Linq;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class WzReferenceResolverTests
{
    private static WzSubProperty Node(string name, params WzImageProperty[] children)
    {
        var node = new WzSubProperty(name);
        foreach (var child in children) node.AddProperty(child);
        return node;
    }

    [Fact]
    public void LiteralPickerReferenceResolvesThroughSharedToolAndSuppliesPreviewArguments()
    {
        var previous = Program.DataSource;
        try
        {
            var image = new WzImage("house.img") { Parsed = true };
            image.AddProperty(Node("snow", Node("house", Node("0", new WzIntProperty("z", 3)))));
            image.Changed = false;
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(s => s.GetImageByPath("Map/Obj/house.img")).Returns(image);
            Program.DataSource = source.Object;
            var result = JObject.Parse(MapEditorFunctions.ExecuteQueryFunction("resolve_wz_reference",
                new JObject { ["path"] = "@{Map/Obj/house.img/snow/house/0}" }));
            Assert.Equal("Map/Obj/house.img/snow/house/0", (string?)result["path"]);
            Assert.Equal("get_object_info", (string?)result["nextQuery"]?["tool"]);
            Assert.Equal("house", (string?)result["nextQuery"]?["arguments"]?["oS"]);
            Assert.Equal("0", (string?)result["previewArguments"]?["assets"]?[0]?["l2"]);
            Assert.Equal("snow", (string?)result["previewArguments"]?["assets"]?[0]?["l0"]);
            Assert.Equal(3, (int)result["children"]![0]!["value"]!);
            Assert.False(image.Changed);
            source.Verify(s => s.GetImageByPath("Map/Obj/house.img"), Times.Once);
        }
        finally { Program.DataSource = previous; }
    }

    [Fact]
    public void GenericPropertiesAreBoundedPaginatedAndMissingPathsAreNotSubstituted()
    {
        var previous = Program.DataSource;
        try
        {
            var image = new WzImage("Etc.img") { Parsed = true };
            image.AddProperty(new WzStringProperty("a", new string('x', 2000)));
            image.AddProperty(new WzIntProperty("b", 42));
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(s => s.GetImageByPath("String/Etc.img")).Returns(image);
            source.Setup(s => s.GetImageByPath("String/Missing.img")).Returns((WzImage)null!);
            Program.DataSource = source.Object;
            var first = JObject.Parse(WzReferenceResolver.Resolve("String/Etc.img", limit: 1));
            Assert.True((bool)first["hasMore"]!);
            Assert.Equal(1024, ((string)first["children"]![0]!["value"]!).Length);
            Assert.True((bool)first["children"]![0]!["valueTruncated"]!);
            var second = JObject.Parse(WzReferenceResolver.Resolve("String/Etc.img", (int)first["nextOffset"]!, 1));
            Assert.False((bool)second["hasMore"]!);
            Assert.Equal(42, (int)second["children"]![0]!["value"]!);
            Assert.StartsWith("Error:", WzReferenceResolver.Resolve("String/Etc.img/missing"));
            Assert.StartsWith("Error:", WzReferenceResolver.Resolve("String/Missing.img"));
            Assert.StartsWith("Error:", WzReferenceResolver.Resolve("../String/Etc.img"));
            Assert.StartsWith("Error:", WzReferenceResolver.Resolve("C:/String/Etc.img"));
        }
        finally { Program.DataSource = previous; }
    }

    [Theory]
    [InlineData("Map/Map/Map2/211000000.img", "get_reference_map", "map_id", "211000000")]
    [InlineData("Npc/0000100.img", "get_npc_list", "search", "0000100")]
    public void ExactIdsSurviveWithoutNameSearch(string path, string tool, string key, string id)
    {
        var previous = Program.DataSource;
        try
        {
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(s => s.GetImageByPath(path)).Returns(new WzImage("test.img") { Parsed = true });
            Program.DataSource = source.Object;
            var result = JObject.Parse(WzReferenceResolver.Resolve(path));
            Assert.Equal(tool, (string?)result["nextQuery"]?["tool"]);
            Assert.Equal(id, (string?)result["nextQuery"]?["arguments"]?[key]);
        }
        finally { Program.DataSource = previous; }
    }

    [Fact]
    public void ResolverIsExposedInBothModelApisAndCompactMcpRegistry()
    {
        using var server = new MapMcpToolServer();
        Assert.True(MapEditorFunctions.IsQueryFunction("resolve_wz_reference"));
        Assert.Contains(server.GetMcpTools(compactOnly: true), t => (string?)t["name"] == "resolve_wz_reference");
        Assert.Contains(server.GetChatCompletionTools(strict: true, compactOnly: true), t => (string?)t["function"]?["name"] == "resolve_wz_reference");
        Assert.Contains(server.GetResponsesTools(strict: true, compactOnly: true), t => (string?)t["name"] == "resolve_wz_reference");
    }
}
