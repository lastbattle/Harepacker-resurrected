using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using System.Linq;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedRadioParityTests
{
    [Theory]
    [InlineData(0x14CF, "[%s]'s broadcasting will begin. Please turn up the volume.")]
    [InlineData(0x14D0, "[%s]'s broadcasting has ended.")]
    [InlineData(0x1501, "Sound/Radio.img/%s")]
    [InlineData(0x1502, "Sound/Radio.img/%s/track")]
    public void MapleStoryStringPool_RadioEntriesMatchRecoveredClientText(int stringPoolId, string expected)
    {
        Assert.True(MapleStoryStringPool.TryGet(stringPoolId, out string actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildPacketOwnedRadioDescriptorCandidates_PrefersTrackMetadataBeforePlayableAudio()
    {
        string[] candidates = MapSimulator.BuildPacketOwnedRadioDescriptorCandidates("MapleFM").ToArray();

        Assert.Equal(
            [
                "Radio.img/MapleFM",
                "Radio.img/MapleFM/track",
                "MapleFM"
            ],
            candidates);
    }

    [Fact]
    public void BuildPacketOwnedRadioTrackCandidates_KeepsClientStyleResolutionOrder()
    {
        (string ImageName, string PropertyPath)[] candidates = MapSimulator.BuildPacketOwnedRadioTrackCandidates("MapleFM").ToArray();

        Assert.Equal(
            [
                ("Radio.img", "MapleFM"),
                ("Radio.img", "MapleFM/track")
            ],
            candidates);
    }

    [Fact]
    public void ResolvePacketOwnedRadioCreateLayerLeftState_PrefersContextSlot3562OverMinimapFallback()
    {
        Assert.True(MapSimulator.ResolvePacketOwnedRadioCreateLayerLeftState(true, true, false));
        Assert.False(MapSimulator.ResolvePacketOwnedRadioCreateLayerLeftState(true, false, true));
        Assert.True(MapSimulator.ResolvePacketOwnedRadioCreateLayerLeftState(false, false, true));
    }

    [Fact]
    public void ShouldRefreshPacketOwnedRadioCreateLayerSessionState_OnlyRefreshesOutsideLiveSessions()
    {
        Assert.False(MapSimulator.ShouldRefreshPacketOwnedRadioCreateLayerSessionState(true, 2, 3));
        Assert.False(MapSimulator.ShouldRefreshPacketOwnedRadioCreateLayerSessionState(false, 3, 3));
        Assert.True(MapSimulator.ShouldRefreshPacketOwnedRadioCreateLayerSessionState(false, 2, 3));
    }

    [Fact]
    public void PacketOwnedLocalUtilityContextState_TracksRadioMutationSequenceAcrossSetClearAndCharacterReset()
    {
        PacketOwnedLocalUtilityContextState context = new();

        context.SetRadioCreateLayerLeftContextValue(true, "packet", 100, runtimeCharacterId: 42);
        Assert.True(context.HasRadioCreateLayerLeftContextValue);
        Assert.True(context.RadioCreateLayerLeftContextValue);
        Assert.Equal(42, context.RadioCreateLayerBoundCharacterId);
        Assert.Equal(1, context.RadioCreateLayerMutationSequence);
        Assert.Equal("packet", context.RadioCreateLayerLastMutationSource);

        context.SetRadioCreateLayerLeftContextValue(true, "packet-repeat", 110, runtimeCharacterId: 42);
        Assert.Equal(1, context.RadioCreateLayerMutationSequence);
        Assert.Equal("packet-repeat", context.RadioCreateLayerLastMutationSource);

        context.ClearRadioCreateLayerLeftContextValue("packet-clear", 120, runtimeCharacterId: 42);
        Assert.False(context.HasRadioCreateLayerLeftContextValue);
        Assert.Equal(2, context.RadioCreateLayerMutationSequence);
        Assert.Equal("packet-clear", context.RadioCreateLayerLastMutationSource);

        context.ResetRadioCreateLayerForCharacter(77);
        Assert.Equal(77, context.RadioCreateLayerBoundCharacterId);
        Assert.Equal(77, context.RadioCreateLayerLastObservedRuntimeCharacterId);
        Assert.False(context.HasRadioCreateLayerLeftContextValue);
        Assert.Equal(3, context.RadioCreateLayerMutationSequence);
        Assert.Equal("runtime-character-reset", context.RadioCreateLayerLastMutationSource);
    }
}
