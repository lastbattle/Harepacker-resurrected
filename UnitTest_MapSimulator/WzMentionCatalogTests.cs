using HaCreator;
using HaCreator.MapEditor.AI;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Moq;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class WzMentionCatalogTests
{
    [Fact]
    public void DiscoversMetadataAndBrowsesOnlyRequestedImageWithoutChangingIt()
    {
        var previous = Program.DataSource;
        try
        {
            var source = new Mock<IDataSource>(MockBehavior.Strict);
            source.Setup(s => s.GetCategories()).Returns(new[] { "Map", "String" });
            source.Setup(s => s.GetSubdirectories("Map")).Returns(new[] { "Obj" });
            source.Setup(s => s.GetSubdirectories("String")).Returns(Array.Empty<string>());
            source.Setup(s => s.GetImageNamesInDirectory("Map", "")).Returns(Array.Empty<string>());
            source.Setup(s => s.GetImageNamesInDirectory("Map", "Obj")).Returns(new[] { "house" });
            source.Setup(s => s.GetImageNamesInDirectory("String", "")).Returns(new[] { "Npc" });
            var image = new WzImage("house.img") { Parsed = true };
            var branch = new WzSubProperty("snow");
            branch.AddProperty(new WzStringProperty("label", "Snowy house"));
            image.AddProperty(branch);
            image.Changed = false;
            source.Setup(s => s.GetImageByPath("Map/Obj/house.img")).Returns(image);
            Program.DataSource = source.Object;
            var catalog = new WzMentionCatalog();
            Assert.Equal(new[] { "Map/", "String/" }, catalog.Search("").Select(e => e.Path));
            Assert.Contains(catalog.Search("objects house"), e => e.Path == "Map/Obj/house.img");
            Assert.DoesNotContain(catalog.Search("Map/Obj/"), e => e.Path == "Map/Obj/");
            Assert.Empty(catalog.Search("missing"));
            source.Verify(s => s.GetImageByPath(It.IsAny<string>()), Times.Never);
            Assert.Equal("Map/Obj/house.img/snow", Assert.Single(catalog.Search("Map/Obj/house.img/")).Path);
            var leaf = Assert.Single(catalog.Search("Map/Obj/house.img/snow/Snowy"));
            Assert.Equal("Map/Obj/house.img/snow/label", leaf.Path);
            Assert.False(leaf.CanBrowse);
            Assert.False(image.Changed);
        }
        finally { Program.DataSource = previous; }
    }
}
