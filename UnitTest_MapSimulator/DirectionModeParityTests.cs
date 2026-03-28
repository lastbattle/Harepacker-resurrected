using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class DirectionModeParityTests
{
    [Fact]
    public void RequestLeaveDirectionMode_KeepsDirectionModeActiveUntilDelayExpires()
    {
        GameStateManager state = new();
        state.EnterDirectionMode();

        state.RequestLeaveDirectionMode(1000, 300);

        Assert.True(state.DirectionModeActive);
        Assert.Equal(1300, state.DirectionModeReleaseAt);

        state.UpdateDirectionMode(1299);

        Assert.True(state.DirectionModeActive);
        Assert.Equal(1300, state.DirectionModeReleaseAt);

        state.UpdateDirectionMode(1300);

        Assert.False(state.DirectionModeActive);
        Assert.Equal(int.MinValue, state.DirectionModeReleaseAt);
    }

    [Fact]
    public void EnterDirectionMode_ClearsPendingDelayedRelease()
    {
        GameStateManager state = new();
        state.EnterDirectionMode();
        state.RequestLeaveDirectionMode(2000, 300);

        state.EnterDirectionMode();

        Assert.True(state.DirectionModeActive);
        Assert.Equal(int.MinValue, state.DirectionModeReleaseAt);
    }

    [Fact]
    public void HasVisibleOwnedWindow_PrunesInvisibleOwnersAndKeepsVisibleOnes()
    {
        DirectionModeWindowOwnerRegistry registry = new();
        registry.TrackWindow("MiniRoom");
        registry.TrackWindow("Messenger");

        bool anyVisible = registry.HasVisibleOwnedWindow(windowName => windowName == "Messenger");

        Assert.True(anyVisible);
        Assert.False(registry.IsTracking("MiniRoom"));
        Assert.True(registry.IsTracking("Messenger"));

        anyVisible = registry.HasVisibleOwnedWindow(_ => false);

        Assert.False(anyVisible);
        Assert.False(registry.IsTracking("Messenger"));
    }
}
