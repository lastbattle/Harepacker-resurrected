using HaCreator;
using HaCreator.MapEditor.AI;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Moq;
using Newtonsoft.Json.Linq;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIReferenceCatalogTests
{
    [V95PlacementFact]
    public void V95ElNathReferenceResolvesActualTownAndBuildingAssets()
    {
        var previous = Program.DataSource;
        try
        {
            using var source = new ImgFileSystemDataSource(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")!);
            Program.DataSource = source;
            var search = JObject.Parse(MapAIReferenceCatalog.Query(search: "El Nath", limit: 200));
            Assert.Contains(search["maps"]!, map => (string?)map["map_id"] == "211000000");
            var reference = JObject.Parse(MapAIReferenceCatalog.Query(mapId: "211000000", limit: 200));
            Assert.Contains(reference["usages"]!, usage => (string?)usage["asset"]?["oS"] == "houseSLR");
            Assert.Contains(reference["usages"]!, usage => (string?)usage["asset"]?["type"] == "background");
            Assert.Contains(reference["usages"]!, usage => (string?)usage["asset"]?["type"] == "tile");
        }
        finally { Program.DataSource = previous; }
    }

    private static WzSubProperty Node(string name, params WzImageProperty[] children)
    {
        var node = new WzSubProperty(name);
        foreach (var child in children) node.AddProperty(child);
        return node;
    }

    private static WzStringProperty Text(string name, string value) => new(name, value);

    [Fact]
    public void SearchUsesAuthoritativeNamesAndPaginatesWithoutLoadingMaps()
    {
        var previous = Program.DataSource;
        try
        {
            var strings = new WzImage("Map.img") { Parsed = true };
            strings.AddProperty(Node("ossyria", Node("211000001", Text("mapName", "Market"), Text("streetName", "El Nath")),
                Node("211000000", Text("mapName", "El Nath"), Text("streetName", "El Nath Mountains"))));
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(value => value.GetImage("String", "Map.img")).Returns(strings);
            Program.DataSource = source.Object;
            var first = JObject.Parse(MapEditorFunctions.ExecuteQueryFunction("get_reference_map", new JObject { ["search"] = "EL NATH", ["limit"] = 1 }));
            Assert.True(MapEditorFunctions.IsQueryFunction("get_reference_map"));
            Assert.Equal(2, (int)first["total"]!);
            Assert.Equal("211000000", (string)first["maps"]![0]!["map_id"]!);
            var second = JObject.Parse(MapAIReferenceCatalog.Query("el nath", offset: (int)first["nextOffset"]!, limit: 1));
            Assert.Equal("Market", (string)second["maps"]![0]!["mapName"]!);
            Assert.False((bool)second["hasMore"]!);
            Assert.Empty(JObject.Parse(MapAIReferenceCatalog.Query("unmatched"))["maps"]!);
            source.Verify(value => value.GetImageByPath(It.IsAny<string>()), Times.Never);
        }
        finally { Program.DataSource = previous; }
    }

    [Fact]
    public void DetailsUseStandardV95PathsAndReturnCountedPreviewIdentifiersWithoutChanges()
    {
        var previous = Program.DataSource;
        try
        {
            var map = new WzImage("211000000.img") { Parsed = true };
            map.AddProperty(Node("info", Text("bgm", "Bgm04/WhiteChristmas"), new WzIntProperty("VRLeft", -1000)));
            map.AddProperty(Node("0", Node("info", Text("tS", "snowyGround")),
                Node("tile", Node("0", Text("u", "bsc"), new WzIntProperty("no", 0)), Node("1", Text("u", "bsc"), new WzIntProperty("no", 0))),
                Node("obj", Node("0", Text("oS", "houseSLR"), Text("l0", "snow"), Text("l1", "house"), Text("l2", "0")))));
            map.AddProperty(Node("back", Node("0", Text("bS", "snowMountain"), new WzIntProperty("no", 2), new WzIntProperty("ani", 0))));
            map.Changed = false;
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(value => value.GetImageByPath("Map/Map/Map2/211000000.img")).Returns((WzImage)null!);
            source.Setup(value => value.GetImage("Map", "Map/Map2/211000000.img")).Returns(map);
            Program.DataSource = source.Object;
            var result = JObject.Parse(MapAIReferenceCatalog.Query(mapId: "211000000"));
            Assert.Equal(3, (int)result["total"]!);
            var usages = result["usages"]!.ToArray();
            var tile = usages.Single(entry => (string)entry["asset"]!["type"]! == "tile");
            Assert.Equal(2, (int)tile["count"]!);
            Assert.Equal("0", (string)tile["asset"]!["no"]!);
            Assert.Equal("Map/Tile/snowyGround.img/bsc/0", (string)tile["path"]!);
            Assert.Equal("houseSLR", (string)usages.Single(entry => (string)entry["asset"]!["type"]! == "object")["asset"]!["oS"]!);
            Assert.Equal("back", (string)usages.Single(entry => (string)entry["asset"]!["type"]! == "background")["asset"]!["backgroundType"]!);
            Assert.Equal("Bgm04/WhiteChristmas", (string)result["info"]!["bgm"]!);
            Assert.Equal(-1000, (int)result["info"]!["VRLeft"]!);
            Assert.False(map.Changed);
            var page = JObject.Parse(MapAIReferenceCatalog.Query(mapId: "211000000", offset: 1, limit: 1));
            Assert.Single(page["usages"]!);
            Assert.True((bool)page["hasMore"]!);
            source.VerifyAll();
        }
        finally { Program.DataSource = previous; }
    }

    [Fact]
    public void LinksResolveWithPaddingAndCyclesOrInvalidIdsFailExplicitly()
    {
        var previous = Program.DataSource;
        try
        {
            var map = new WzImage("000000001.img") { Parsed = true };
            map.AddProperty(Node("info", Text("link", "2")));
            var linked = new WzImage("000000002.img") { Parsed = true };
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(value => value.GetImageByPath("Map/Map/Map0/000000001.img")).Returns(map);
            source.Setup(value => value.GetImageByPath("Map/Map/Map0/000000002.img")).Returns(linked);
            Program.DataSource = source.Object;
            var result = JObject.Parse(MapAIReferenceCatalog.Query(mapId: "1", offset: int.MaxValue, limit: int.MaxValue));
            Assert.Equal("000000002", (string)result["assetSourceMapId"]!);
            Assert.Equal(200, (int)result["limit"]!);
            Assert.Empty(result["usages"]!);
            linked.AddProperty(Node("info", Text("link", "1")));
            Assert.Contains("cycle", MapAIReferenceCatalog.Query(mapId: "1"));
            Assert.StartsWith("Error:", MapAIReferenceCatalog.Query(mapId: "../1"));
        }
        finally { Program.DataSource = previous; }
    }
}
