using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;

namespace MapleLib.Tests;

public class WzImgSerializerTests
{
    [Fact]
    public void SerializeImage_AllowsStandaloneImageWithoutParentDirectory()
    {
        WzImage image = new("standalone.img");
        WzImgSerializer serializer = new();

        byte[] serialized = serializer.SerializeImage(image);

        Assert.NotEmpty(serialized);
    }
}
