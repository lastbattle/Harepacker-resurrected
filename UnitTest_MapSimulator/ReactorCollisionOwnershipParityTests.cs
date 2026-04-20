using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class ReactorCollisionOwnershipParityTests
{
    [Fact]
    public void ShouldPollReactorCollision_UsesClientThresholdAndWraparoundMath()
    {
        Assert.False(MapSimulator.ShouldPollReactorCollision(currentTick: 1999, lastCheckTick: 1000));
        Assert.True(MapSimulator.ShouldPollReactorCollision(currentTick: 2000, lastCheckTick: 1000));

        int nearMaxTick = int.MaxValue - 500;
        int wrappedTick = int.MinValue + 700;
        Assert.True(MapSimulator.ShouldPollReactorCollision(wrappedTick, nearMaxTick));
    }

    [Fact]
    public void DoesClientTouchBoundsContainPosition_UsesPointContainmentAgainstLiveBounds()
    {
        var bounds = new Rectangle(10, 20, 30, 40);

        Assert.True(ReactorPool.DoesClientTouchBoundsContainPosition(bounds, playerX: 10f, playerY: 20f));
        Assert.True(ReactorPool.DoesClientTouchBoundsContainPosition(bounds, playerX: 39.9f, playerY: 59.9f));
        Assert.False(ReactorPool.DoesClientTouchBoundsContainPosition(bounds, playerX: 40f, playerY: 60f));
    }

    [Fact]
    public void CanPollLocalUserTouchReactor_SkipsPacketLeavePending()
    {
        var active = new ReactorRuntimeData { State = ReactorState.Idle, PacketLeavePending = false };
        var leavePending = new ReactorRuntimeData { State = ReactorState.Idle, PacketLeavePending = true };

        Assert.True(ReactorPool.CanPollLocalUserTouchReactor(active));
        Assert.False(ReactorPool.CanPollLocalUserTouchReactor(leavePending));
    }

    [Fact]
    public void DeferredReplayDelayHelpers_PreservePositiveWrapDelta()
    {
        int previous = int.MaxValue - 50;
        int next = int.MinValue + 149;
        int expectedDelta = 200;

        Assert.Equal(expectedDelta, ReactorPoolOfficialSessionBridgeManager.ComputeDeferredTouchReplayDelayMs(previous, next));
        Assert.Equal(expectedDelta, ReactorTouchPacketTransportManager.ComputeDeferredTouchReplayDelayMs(previous, next));
    }

    [Fact]
    public void DeferredReplayTickHelpers_UseDueTickCursorAndRespectSchedule()
    {
        Assert.True(ReactorPoolOfficialSessionBridgeManager.ShouldFlushDeferredTouchAtTick(1000, 900, hasSchedule: true));
        Assert.False(ReactorPoolOfficialSessionBridgeManager.ShouldFlushDeferredTouchAtTick(850, 900, hasSchedule: true));
        Assert.Equal(900, ReactorPoolOfficialSessionBridgeManager.ResolveDeferredTouchReplayTick(1000, 900, hasSchedule: true));
        Assert.Equal(1000, ReactorPoolOfficialSessionBridgeManager.ResolveDeferredTouchReplayTick(1000, 900, hasSchedule: false));

        Assert.True(ReactorTouchPacketTransportManager.ShouldFlushDeferredTouchAtTick(1000, 900, hasSchedule: true));
        Assert.False(ReactorTouchPacketTransportManager.ShouldFlushDeferredTouchAtTick(850, 900, hasSchedule: true));
        Assert.Equal(900, ReactorTouchPacketTransportManager.ResolveDeferredTouchReplayTick(1000, 900, hasSchedule: true));
        Assert.Equal(1000, ReactorTouchPacketTransportManager.ResolveDeferredTouchReplayTick(1000, 900, hasSchedule: false));
    }

    [Fact]
    public void PacketEnterCandidateSelection_AllowsSingleCandidateWhenPacketNameUnresolved()
    {
        PacketEnterAuthoredReactorCandidate[] candidates =
        [
            new PacketEnterAuthoredReactorCandidate(
                Index: 7,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 0)
        ];

        bool admitted = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 0,
            out int selectedIndex,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.True(admitted);
        Assert.Equal(7, selectedIndex);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.ClientSignal, reason);
    }

    [Fact]
    public void PacketEnterCandidateSelection_RefusesMultiCandidateWhenPacketNameUnresolved()
    {
        PacketEnterAuthoredReactorCandidate[] candidates =
        [
            new PacketEnterAuthoredReactorCandidate(
                Index: 2,
                IsPacketNamePresent: true,
                IsLocallyTouched: true,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 1),
            new PacketEnterAuthoredReactorCandidate(
                Index: 8,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: true,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 1)
        ];

        bool admitted = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 1,
            out int selectedIndex,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.False(admitted);
        Assert.Equal(-1, selectedIndex);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.None, reason);
    }

    [Fact]
    public void PacketEnterCandidateSelection_UsesWzAuthoredOrderForTiedStrongestDisjointSignals()
    {
        PacketEnterAuthoredReactorCandidate[] candidates =
        [
            new PacketEnterAuthoredReactorCandidate(
                Index: 5,
                IsPacketNamePresent: false,
                IsLocallyTouched: true,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: true,
                HasExactNameMatch: false,
                VisualState: 3),
            new PacketEnterAuthoredReactorCandidate(
                Index: 3,
                IsPacketNamePresent: false,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: true,
                MatchesPacketName: true,
                HasExactNameMatch: false,
                VisualState: 3),
            new PacketEnterAuthoredReactorCandidate(
                Index: 9,
                IsPacketNamePresent: false,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: true,
                HasExactNameMatch: false,
                VisualState: 4)
        ];

        bool admitted = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 4,
            out int selectedIndex,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.True(admitted);
        Assert.Equal(3, selectedIndex);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback, reason);
    }
}
