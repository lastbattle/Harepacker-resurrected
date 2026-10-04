using HaCreator.MapSimulator.Assets;
using MapleLib.Img;
using MapleLib.WzLib;
using Moq;

namespace UnitTest_MapSimulator;

public sealed class RuntimeAssetSourceLookupTests
{
    [Fact]
    public void NestedImageLookupDoesNotCloneTheWholeCategoryDirectory()
    {
        using var image = new WzImage("00000000.img") { Parsed = true };
        var source = new Mock<IDataSource>(MockBehavior.Strict);
        source.SetupGet(value => value.Name).Returns("fixture");
        source.SetupGet(value => value.VersionInfo).Returns(new VersionInfo());
        source.Setup(value => value.GetImage("Character", "Face/00000000.img"))
            .Returns(image);

        using var runtime = new RuntimeAssetSource(source.Object, RuntimeAssetSourceOwnership.Borrowed);
        Assert.NotNull(runtime.FindImage("Character", "Face/00000000.img"));

        source.Verify(value => value.GetImage("Character", "Face/00000000.img"), Times.Once);
        source.Verify(value => value.GetDirectory(It.IsAny<string>()), Times.Never);
        source.Verify(value => value.GetDirectories(It.IsAny<string>()), Times.Never);
    }
}
