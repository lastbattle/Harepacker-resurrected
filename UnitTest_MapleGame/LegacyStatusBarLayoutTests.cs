using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class LegacyStatusBarLayoutTests
{
    [Fact]
    public void LegacyHudUsesPreBigBangFixedOrigins()
    {
        Assert.Equal(new Microsoft.Xna.Framework.Point(198, 40), LegacyStatusBarLayout.GaugeOrigin);
        Assert.Equal(new Microsoft.Xna.Framework.Point(540, 37), LegacyStatusBarLayout.GetPrimaryButtonPosition(0));
        Assert.Equal(new Microsoft.Xna.Framework.Point(702, 37), LegacyStatusBarLayout.GetPrimaryButtonPosition(3));
    }

    [Fact]
    public void LegacyShortcutStripUsesSevenTwentyEightPixelSlots()
    {
        Assert.Equal(new Microsoft.Xna.Framework.Point(523, 17), LegacyStatusBarLayout.GetShortcutPosition(0));
        Assert.Equal(new Microsoft.Xna.Framework.Point(691, 17), LegacyStatusBarLayout.GetShortcutPosition(6));
    }
}
