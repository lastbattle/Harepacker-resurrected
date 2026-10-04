using HaCreator.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Moq;

namespace UnitTest_MapSimulator;

public sealed class NpcLazyLoadingRegressionTests
{
    [Fact]
    public void ExtractNpcStringData_LoadsOnlyNpcCatalogue()
    {
        var npcImage = new WzImage("Npc.img");
        var npc = new WzSubProperty("1012003");
        npc.AddProperty(new WzStringProperty("name", "Chief Stan"));
        npc.AddProperty(new WzStringProperty("func", "Henesys Chief"));
        npcImage.AddProperty(npc);

        var dataSource = new Mock<IDataSource>(MockBehavior.Strict);
        dataSource
            .Setup(source => source.GetImage("String", "Npc.img"))
            .Returns(npcImage);

        var manager = new WzInformationManager();
        new ImgDataExtractor(dataSource.Object, manager).ExtractNpcStringData();

        Assert.Equal("Chief Stan", manager.NpcNameCache["1012003"].Item1);
        Assert.Equal("Henesys Chief", manager.NpcNameCache["1012003"].Item2);
        dataSource.VerifyAll();
    }

}
