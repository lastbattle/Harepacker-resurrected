using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Moq;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator;

public class MapObjectSpatialCullingTests
{
    [Fact]
    public void IndexMapObjectForSpatialQueries_CoversLargeObjectScenarioFromMap993210000()
    {
        // Regression for the large map object that was culled while still intersecting the
        // camera view in map 993210000.
        var frame = new Mock<IDXObject>();
        frame.SetupGet(value => value.X).Returns(100);
        frame.SetupGet(value => value.Y).Returns(100);
        frame.SetupGet(value => value.Width).Returns(1_200);
        frame.SetupGet(value => value.Height).Returns(900);

        var item = new BaseDXDrawableItem(frame.Object, flip: false);
        var grid = new SpatialGrid<BaseDXDrawableItem>(new XnaRectangle(0, 0, 2_048, 2_048), cellSize: 512);
        var results = new BaseDXDrawableItem[1];

        MapSimulator.IndexMapObjectForSpatialQueries(grid, item);
        int count = grid.QueryToArray(new XnaRectangle(1_100, 700, 100, 100), results);

        Assert.Equal(1, count);
        Assert.Same(item, results[0]);
    }
}
