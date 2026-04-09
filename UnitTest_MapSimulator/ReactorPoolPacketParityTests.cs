using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class ReactorPoolPacketParityTests
{
    [Fact]
    public void ResolvePacketClientHitStartTime_UsesClientCurrentTickSemantics()
    {
        Assert.Equal(500, ReactorPool.ResolvePacketClientHitStartTime(500, 0));
        Assert.Equal(575, ReactorPool.ResolvePacketClientHitStartTime(500, 75));
        Assert.Equal(1, ReactorPool.ResolvePacketClientHitStartTime(0, 0));
    }

    [Fact]
    public void ResolvePacketLeaveHitStartTime_UsesClientFallbackOnlyWithoutAuthoredEvents()
    {
        Assert.Equal(0, ReactorPool.ResolvePacketLeaveHitStartTime(1200, hasAuthoredStateEvents: true));
        Assert.Equal(1600, ReactorPool.ResolvePacketLeaveHitStartTime(1200, hasAuthoredStateEvents: false));
        Assert.Equal(1, ReactorPool.ResolvePacketLeaveHitStartTime(-400, hasAuthoredStateEvents: false));
    }

    [Fact]
    public void ResolvePacketStateEndTime_AlwaysArmsClientClock()
    {
        Assert.Equal(800, ReactorPool.ResolvePacketStateEndTime(800, 0));
        Assert.Equal(1100, ReactorPool.ResolvePacketStateEndTime(800, 3));
        Assert.Equal(800, ReactorPool.ResolvePacketStateEndTime(800, -5));
    }

    [Fact]
    public void ResolvePacketHitAnimationState_PrefersOldVisualStateDuringDeferredHitPlayback()
    {
        ReactorRuntimeData data = new()
        {
            VisualState = 3,
            PacketAnimationSourceState = 4,
            PacketHitAnimationState = 2
        };

        Assert.Equal(2, ReactorPool.ResolvePacketHitAnimationState(data));

        data.PacketHitAnimationState = -1;
        Assert.Equal(4, ReactorPool.ResolvePacketHitAnimationState(data));

        data.PacketAnimationSourceState = -1;
        Assert.Equal(3, ReactorPool.ResolvePacketHitAnimationState(data));
    }
}
