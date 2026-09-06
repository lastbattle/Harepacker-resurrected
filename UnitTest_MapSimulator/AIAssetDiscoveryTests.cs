using HaCreator;
using HaCreator.MapEditor.AI;
using HaCreator.Wz;
using MapleLib.WzLib;
using Newtonsoft.Json.Linq;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIAssetDiscoveryTests
{
    [Fact]
    public void DiscoveryReturnsExactNamesAcrossPagesAndFiltersWithoutParsingImages()
    {
        var previous = Program.InfoManager;
        try
        {
            Program.InfoManager = new WzInformationManager();
            Program.InfoManager.TileSets["zSnow"] = new WzImage("zSnow.img");
            Program.InfoManager.TileSets["aSnow"] = new WzImage("aSnow.img");
            Program.InfoManager.ObjectSets["snowTown"] = new WzImage("snowTown.img");
            Program.InfoManager.BackgroundSets["winterSky"] = new WzImage("winterSky.img");

            Assert.True(MapEditorFunctions.IsQueryFunction("get_asset_sets"));
            var first = JObject.Parse(MapEditorFunctions.ExecuteQueryFunction("get_asset_sets", new JObject { ["limit"] = 2 }));
            Assert.Equal(4, (int)first["total"]!);
            Assert.Equal(new[] { "winterSky", "snowTown" }, first["sets"]!.Select(set => (string)set["name"]!).ToArray());
            Assert.True((bool)first["hasMore"]!);
            var second = JObject.Parse(MapEditorFunctions.ExecuteQueryFunction("get_asset_sets", new JObject { ["offset"] = first["nextOffset"], ["limit"] = 2 }));
            Assert.Equal(new[] { "aSnow", "zSnow" }, second["sets"]!.Select(set => (string)set["name"]!).ToArray());
            Assert.False((bool)second["hasMore"]!);
            Assert.Equal(JTokenType.Null, second["nextOffset"]!.Type);

            var filtered = JObject.Parse(MapAssetCatalog.GetAssetSets("tile", "SNOW"));
            Assert.Equal(2, (int)filtered["total"]!);
            Assert.All(filtered["sets"]!, set => Assert.Equal("tile", (string)set["type"]!));
            var unmatched = JObject.Parse(MapAssetCatalog.GetAssetSets("all", "El Nath"));
            Assert.Empty(unmatched["sets"]!);
            Assert.Contains("browse without search", (string)unmatched["guidance"]!);
            Assert.All(Program.InfoManager.TileSets.Values, image => Assert.False(image.Parsed));
        }
        finally { Program.InfoManager = previous; }
    }

    [Fact]
    public void DiscoveryBoundsPaginationAndHandlesEmptyOrInvalidRequests()
    {
        var previous = Program.InfoManager;
        try
        {
            Program.InfoManager = new WzInformationManager();
            for (int index = 0; index < 205; index++)
                Program.InfoManager.TileSets[$"tile{index:D3}"] = new WzImage($"tile{index:D3}.img");
            var bounded = JObject.Parse(MapAssetCatalog.GetAssetSets(offset: -1, limit: int.MaxValue));
            Assert.Equal(0, (int)bounded["offset"]!);
            Assert.Equal(200, bounded["sets"]!.Count());
            Assert.Equal(200, (int)bounded["nextOffset"]!);
            Assert.Single(JObject.Parse(MapAssetCatalog.GetAssetSets(limit: 0))["sets"]!);
            var beyond = JObject.Parse(MapAssetCatalog.GetAssetSets(offset: int.MaxValue));
            Assert.Empty(beyond["sets"]!);
            Assert.False((bool)beyond["hasMore"]!);
            Assert.StartsWith("Error:", MapAssetCatalog.GetAssetSets("mob"));
            Assert.Empty(JObject.Parse(MapAssetCatalog.GetAssetSets("background"))["sets"]!);
            Assert.Contains("get_asset_sets", MapAssetCatalog.GenerateCompactSummary());
            Assert.DoesNotContain("tile000", MapAssetCatalog.GenerateCompactSummary());
        }
        finally { Program.InfoManager = previous; }
    }
}
