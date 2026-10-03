using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class MapSimulatorUiFamilyTests
{
    [Theory]
    [InlineData(true, true, true, MapSimulatorUiFamily.VUpdate)]
    [InlineData(false, true, true, MapSimulatorUiFamily.VUpdate)]
    [InlineData(true, true, false, MapSimulatorUiFamily.BigBang)]
    [InlineData(false, true, false, MapSimulatorUiFamily.BigBang)]
    [InlineData(true, false, false, MapSimulatorUiFamily.LegacyPreBigBang)]
    [InlineData(false, false, false, MapSimulatorUiFamily.LegacyPreBigBang)]
    public void StatusBarOwnerImagesUseNewestAvailableFamily(
        bool hasStatusBar,
        bool hasStatusBar2,
        bool hasStatusBar3,
        MapSimulatorUiFamily expected)
    {
        Assert.Equal(
            expected,
            MapSimulatorUiFamilyResolver.ResolveFromStatusBarImages(
                hasStatusBar,
                hasStatusBar2,
                hasStatusBar3));
    }
}
