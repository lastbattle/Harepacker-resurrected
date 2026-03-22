using HaCreator.MapSimulator.Managers;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class DirectionModeWindowOwnerRegistryTests
{
    [Fact]
    public void HasVisibleOwnedWindow_ReturnsTrueWhileTrackedWindowIsVisible()
    {
        DirectionModeWindowOwnerRegistry registry = new();
        registry.TrackWindow("MiniRoom");

        bool visible = registry.HasVisibleOwnedWindow(name => name == "MiniRoom");

        Assert.True(visible);
        Assert.True(registry.IsTracking("MiniRoom"));
    }

    [Fact]
    public void HasVisibleOwnedWindow_PrunesHiddenTrackedWindows()
    {
        DirectionModeWindowOwnerRegistry registry = new();
        registry.TrackWindow("ItemUpgrade");
        registry.TrackWindow("MapleTv");

        bool visible = registry.HasVisibleOwnedWindow(name => name == "MapleTv");

        Assert.True(visible);
        Assert.False(registry.IsTracking("ItemUpgrade"));
        Assert.True(registry.IsTracking("MapleTv"));
    }

    [Fact]
    public void HasVisibleOwnedWindow_ReturnsFalseAfterAllTrackedWindowsClose()
    {
        DirectionModeWindowOwnerRegistry registry = new();
        registry.TrackWindow("MemoSend");

        bool visible = registry.HasVisibleOwnedWindow(_ => false);

        Assert.False(visible);
        Assert.False(registry.IsTracking("MemoSend"));
    }
}
