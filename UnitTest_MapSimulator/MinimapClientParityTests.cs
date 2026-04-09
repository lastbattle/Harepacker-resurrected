using HaCreator.MapSimulator.Loaders;

namespace UnitTest_MapSimulator;

public sealed class MinimapClientParityTests
{
    [Fact]
    public void CollapsedMinimapBarHeightPrefersWzChromeHeight()
    {
        int barHeight = UILoader.ResolveCollapsedMinimapBarHeightForTesting(
            leftHeight: 20,
            centerHeight: 20,
            rightHeight: 20,
            fallbackHeight: 16);

        Assert.Equal(20, barHeight);
    }

    [Fact]
    public void CollapsedMinimapTitleLaneHeightUsesClientTopRowButtonLane()
    {
        int laneHeight = UILoader.ResolveCollapsedMinimapTitleLaneHeightForTesting(
            barHeight: 20,
            laneTop: 4,
            buttonHeight: 12);

        Assert.Equal(12, laneHeight);
    }

    [Fact]
    public void CollapsedMinimapTitleLaneHeightClampsToRemainingBarHeight()
    {
        int laneHeight = UILoader.ResolveCollapsedMinimapTitleLaneHeightForTesting(
            barHeight: 18,
            laneTop: 10,
            buttonHeight: 12);

        Assert.Equal(8, laneHeight);
    }

    [Fact]
    public void CollapsedMinimapVerticalOffsetCentersElementInsideButtonLane()
    {
        int offset = UILoader.ResolveCollapsedMinimapVerticalContentOffsetForTesting(
            contentHeight: 20,
            laneTop: 4,
            laneHeight: 12,
            elementHeight: 10);

        Assert.Equal(5, offset);
    }

    [Fact]
    public void CollapsedMinimapVerticalOffsetDoesNotOverflowBarForTallText()
    {
        int offset = UILoader.ResolveCollapsedMinimapVerticalContentOffsetForTesting(
            contentHeight: 20,
            laneTop: 4,
            laneHeight: 12,
            elementHeight: 16);

        Assert.Equal(4, offset);
    }
}
