using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public class ReactorLayerBuildParityTests
{
    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_PrefersActiveAnimationOwner()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: 4,
            transientHitSourceState: 7,
            visualState: 2,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(4, fallbackAnimationOwnerState);
        Assert.Equal(7, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_UsesTransientWhenActiveMissing()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: -1,
            transientHitSourceState: 7,
            visualState: 2,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(7, fallbackAnimationOwnerState);
        Assert.Equal(-1, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_UsesVisualWhenActiveAndTransientMissing()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: -1,
            transientHitSourceState: -1,
            visualState: 2,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(2, fallbackAnimationOwnerState);
        Assert.Equal(-1, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_SeedsFromFallbackWhenPacketLanesMissing()
    {
        var data = new ReactorPool.ReactorRuntimeData();

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 11,
            fallbackHitOwnerState: 22,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(11, packetAnimationSourceState);
        Assert.Equal(22, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_PreservesDistinctPacketHitOwnerLane()
    {
        var data = new ReactorPool.ReactorRuntimeData
        {
            PacketAnimationSourceState = 44,
            PacketHitAnimationState = 33
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 11,
            fallbackHitOwnerState: 22,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(44, packetAnimationSourceState);
        Assert.Equal(33, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_UsesAnimationFallbackBeforeHitWhenAnimationLaneMissing()
    {
        var data = new ReactorPool.ReactorRuntimeData
        {
            PacketAnimationSourceState = -1,
            PacketHitAnimationState = 33
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 11,
            fallbackHitOwnerState: 22,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(11, packetAnimationSourceState);
        Assert.Equal(33, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_FallsBackToHitForAnimationContinuityWhenNoAnimationFallbackExists()
    {
        var data = new ReactorPool.ReactorRuntimeData
        {
            PacketAnimationSourceState = -1,
            PacketHitAnimationState = 33
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: -1,
            fallbackHitOwnerState: -1,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(33, packetAnimationSourceState);
        Assert.Equal(33, packetHitAnimationState);
    }

    [Fact]
    public void RefuseUnmatchedPacketAutoHitLayer_ClearsStalePacketHitOwnerWhilePreservingAnimationOwnerContinuity()
    {
        var data = new ReactorPool.ReactorRuntimeData
        {
            IsPacketOwned = true,
            State = ReactorState.Active,
            PacketProperEventIndex = -2,
            PacketAnimationSourceState = 9,
            PacketHitAnimationState = 5,
            PacketPendingVisualState = 3,
            PacketAnimationEndTime = 77,
            PacketLeavePending = false
        };

        ReactorPool.RefuseUnmatchedPacketAutoHitLayer(
            reactor: null,
            data: data,
            currentTick: 1234);

        Assert.Equal(9, data.PacketAnimationSourceState);
        Assert.Equal(-1, data.PacketHitAnimationState);
        Assert.Equal(-1, data.PacketPendingVisualState);
        Assert.Equal(0, data.PacketAnimationEndTime);
        Assert.Equal(PacketReactorAnimationPhase.AwaitingAutoHitLayerCompletion, data.PacketAnimationPhase);
        Assert.Equal(ReactorState.Activated, data.State);
        Assert.Equal(1234, data.StateStartTime);
        Assert.Equal(-2, data.PacketProperEventIndex);
    }
}
