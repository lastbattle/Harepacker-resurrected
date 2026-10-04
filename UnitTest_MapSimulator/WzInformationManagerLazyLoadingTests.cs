using HaCreator.Wz;

namespace UnitTest_MapSimulator;

public sealed class WzInformationManagerLazyLoadingTests
{
    [Theory]
    [InlineData("Bgm00/FloralLife", "Bgm00.img", "FloralLife")]
    [InlineData("Sound/Bgm00.img/FloralLife", "Bgm00.img", "FloralLife")]
    [InlineData("Sound/Regional/BgmEvent.img/Nested/Track", "Regional/BgmEvent.img", "Nested/Track")]
    public void ResolveBgmPath_DirectlyMapsImageAndProperty(
        string input,
        string expectedImage,
        string expectedProperty)
    {
        Assert.True(WzInformationManager.TryResolveBgmPath(input, out string image, out string property));
        Assert.Equal(expectedImage, image);
        Assert.Equal(expectedProperty, property);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bgm00")]
    [InlineData("Sound/Bgm00.img")]
    public void ResolveBgmPath_RejectsIncompletePaths(string input)
    {
        Assert.False(WzInformationManager.TryResolveBgmPath(input, out _, out _));
    }
}
