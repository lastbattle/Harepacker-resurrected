using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class ReactorLayerBuildParityTests
{
    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_UsesActiveAnimationOwnerWhenPresent()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: 5,
            transientHitSourceState: 8,
            visualState: 3,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(5, fallbackAnimationOwnerState);
        Assert.Equal(8, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_UsesTransientOwnerWhenActiveMissing()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: -1,
            transientHitSourceState: 8,
            visualState: 3,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(8, fallbackAnimationOwnerState);
        Assert.Equal(-1, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketMutationFallbackVisualOwnershipSourceStates_UsesVisualStateWhenNoActiveOrTransientSource()
    {
        ReactorPool.ResolvePacketMutationFallbackVisualOwnershipSourceStates(
            activeAnimationState: -1,
            transientHitSourceState: -1,
            visualState: 3,
            out int fallbackAnimationOwnerState,
            out int fallbackHitOwnerState);

        Assert.Equal(3, fallbackAnimationOwnerState);
        Assert.Equal(-1, fallbackHitOwnerState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_SeedsFromFallbackWhenPacketLanesMissing()
    {
        ReactorRuntimeData data = new ReactorRuntimeData
        {
            PacketAnimationSourceState = -1,
            PacketHitAnimationState = -1
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 6,
            fallbackHitOwnerState: 9,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(6, packetAnimationSourceState);
        Assert.Equal(9, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_PreservesDistinctPacketHitOwnerLane()
    {
        ReactorRuntimeData data = new ReactorRuntimeData
        {
            PacketAnimationSourceState = 4,
            PacketHitAnimationState = 9
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 6,
            fallbackHitOwnerState: 7,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(4, packetAnimationSourceState);
        Assert.Equal(9, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_WhenPacketHitOwnerExistsWithoutAnimationLane_UsesAnimationFallbackFirst()
    {
        ReactorRuntimeData data = new ReactorRuntimeData
        {
            PacketAnimationSourceState = -1,
            PacketHitAnimationState = 9
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: 6,
            fallbackHitOwnerState: -1,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(6, packetAnimationSourceState);
        Assert.Equal(9, packetHitAnimationState);
    }

    [Fact]
    public void ResolvePacketVisualOwnershipSourceStatesForMutation_WhenNoAnimationFallbackExists_UsesPacketHitOwnerForAnimationContinuity()
    {
        ReactorRuntimeData data = new ReactorRuntimeData
        {
            PacketAnimationSourceState = -1,
            PacketHitAnimationState = 9
        };

        ReactorPool.ResolvePacketVisualOwnershipSourceStatesForMutation(
            data,
            fallbackAnimationOwnerState: -1,
            fallbackHitOwnerState: -1,
            out int packetAnimationSourceState,
            out int packetHitAnimationState);

        Assert.Equal(9, packetAnimationSourceState);
        Assert.Equal(9, packetHitAnimationState);
    }

    [Fact]
    public void RefuseUnmatchedPacketAutoHitLayer_ClearsStalePacketHitOwnerLaneAndPreservesAnimationOwner()
    {
        ReactorRuntimeData data = new ReactorRuntimeData
        {
            PacketAnimationSourceState = 5,
            PacketHitAnimationState = 9,
            PacketPendingVisualState = 7,
            PacketAnimationEndTime = 120,
            PacketAnimationPhase = PacketReactorAnimationPhase.AwaitingLayerCompletion,
            State = ReactorState.Active,
            StateStartTime = 1
        };

        ReactorPool.RefuseUnmatchedPacketAutoHitLayer(
            reactor: null,
            data,
            currentTick: 42);

        Assert.Equal(5, data.PacketAnimationSourceState);
        Assert.Equal(-1, data.PacketHitAnimationState);
        Assert.Equal(-1, data.PacketPendingVisualState);
        Assert.Equal(0, data.PacketAnimationEndTime);
        Assert.Equal(PacketReactorAnimationPhase.AwaitingAutoHitLayerCompletion, data.PacketAnimationPhase);
        Assert.Equal(ReactorState.Activated, data.State);
        Assert.Equal(42, data.StateStartTime);
    }
}
