using System.Drawing;
using System.IO;
using HaCreator;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using Moq;
using Newtonsoft.Json.Linq;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIActorAssetPreviewTests
{
    [Fact]
    public void ActorContactSheetUsesExactIdsArtworkAndNativeOrigins()
    {
        var previousSource = Program.DataSource;
        var previousInfo = Program.InfoManager;
        using var art = new Bitmap(24, 18);
        using (var graphics = Graphics.FromImage(art)) graphics.Clear(Color.Lime);
        try
        {
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            var mobImage = new WzImage("0000100.img") { Parsed = true };
            mobImage.HCTag = new MobInfo(art, new Point(7, 16), "100", "test", mobImage);
            var npcImage = new WzImage("0000200.img") { Parsed = true };
            npcImage.HCTag = new NpcInfo(art, new Point(8, 15), "0000200", npcImage);
            source.Setup(value => value.GetImage("Mob", "0000100.img")).Returns(mobImage);
            source.Setup(value => value.GetImage("Npc", "0000200.img")).Returns(npcImage);
            Program.DataSource = source.Object;
            Program.InfoManager = new WzInformationManager();
            Program.InfoManager.Reactors["0000300"] = new ReactorInfo(art, new Point(9, 14), "0000300", "test", null);
            var result = MapAIVisualRenderer.RenderAssets(new JObject
            {
                ["assets"] = new JArray(
                    new JObject { ["type"] = "mob", ["id"] = "100" },
                    new JObject { ["type"] = "npc", ["id"] = "0000200" },
                    new JObject { ["type"] = "reactor", ["id"] = "0000300" })
            });
            var metadata = JObject.Parse((string)result[0]["text"]!);
            using var stream = new MemoryStream(Convert.FromBase64String((string)result[1]["data"]!));
            using var rendered = new Bitmap(stream);
            for (int index = 0; index < 3; index++)
            {
                var entry = metadata["assets"]![index]!;
                Assert.Null(entry["error"]);
                Assert.Equal(24, (int)entry["width"]!);
                Assert.Equal(18, (int)entry["height"]!);
                Assert.Equal(7 + index, (int)entry["origin"]!["x"]!);
                Assert.Equal(16 - index, (int)entry["origin"]!["y"]!);
                int x = (int)(double)entry["sheetBounds"]!["x"]! + 5;
                int y = (int)(double)entry["sheetBounds"]!["y"]! + 5;
                Assert.Equal(Color.Lime.ToArgb(), rendered.GetPixel(x, y).ToArgb());
            }
            Assert.Equal("0000200", (string)metadata["assets"]![1]!["asset"]!["id"]!);
            Assert.Equal("0000300", (string)metadata["assets"]![2]!["asset"]!["id"]!);
            source.VerifyAll();
        }
        finally
        {
            Program.DataSource = previousSource;
            Program.InfoManager = previousInfo;
        }
    }

    [Theory]
    [InlineData("mob")]
    [InlineData("npc")]
    [InlineData("reactor")]
    public void ActorPreviewRequiresExactId(string type)
    {
        var result = MapAIVisualRenderer.RenderAssets(new JObject
            { ["assets"] = new JArray(new JObject { ["type"] = type }) });
        var metadata = JObject.Parse((string)result[0]["text"]!);
        Assert.Equal("Missing required asset field: id.", (string)metadata["assets"]![0]!["error"]!);
    }
}
