using HaCreator.MapSimulator.UI;
using Point = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

public sealed class WorldMapViewportPlacementTests
{
    [Fact]
    public void FittingCompositeIsCenteredInSupportedViewport()
    {
        Point position = WorldMapUI.ResolveViewportPosition(
            viewportWidth: 1024,
            viewportHeight: 768,
            compositeWidth: 641,
            compositeHeight: 529);

        Assert.Equal(new Point(191, 119), position);
    }

    [Fact]
    public void OversizedCompositeAlignsItsVisibleOriginToViewportTopLeft()
    {
        Point position = WorldMapUI.ResolveViewportPosition(
            viewportWidth: 1024,
            viewportHeight: 768,
            compositeWidth: 1200,
            compositeHeight: 900);

        Assert.Equal(Point.Zero, position);
    }

    [Fact]
    public void NegativeCompositeOriginIsCompensatedAfterCentering()
    {
        Point position = WorldMapUI.ResolveViewportPosition(
            viewportWidth: 1024,
            viewportHeight: 768,
            compositeWidth: 700,
            compositeHeight: 500,
            compositeLeft: -20,
            compositeTop: -10);

        Assert.Equal(new Point(182, 144), position);
    }
}
