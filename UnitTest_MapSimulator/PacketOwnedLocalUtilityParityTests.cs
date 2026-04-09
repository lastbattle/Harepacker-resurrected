using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedLocalUtilityParityTests
{
    [Fact]
    public void TryApplyPacketOwnedEmotion_KeepsDecodedDurationAndItemOptionFlag()
    {
        PlayerCharacter player = new(null, null, null);

        bool applied = player.TryApplyPacketOwnedEmotion(
            emotionId: 4,
            durationMs: 500,
            byItemOption: true,
            currentTime: 1000,
            out string message);

        Assert.True(applied);
        Assert.Contains("500 ms", message);
        Assert.True(player.TryGetPacketOwnedEmotionState(1499, out PacketOwnedEmotionState state));
        Assert.Equal(4, state.EmotionId);
        Assert.Equal("angry", state.EmotionName);
        Assert.Equal(500, state.DurationMs);
        Assert.Equal(1000, state.AppliedAt);
        Assert.Equal(1500, state.ExpireTime);
        Assert.True(state.ByItemOption);
        Assert.True(state.HasFiniteDuration);
        Assert.False(state.IsExpired(1499));
        Assert.True(state.IsExpired(1500));
        Assert.False(player.TryGetPacketOwnedEmotionState(1500, out _));
    }

    [Fact]
    public void TryApplyPacketOwnedEmotion_NonPositiveDurationRemainsUntilCleared()
    {
        PlayerCharacter player = new(null, null, null);

        bool applied = player.TryApplyPacketOwnedEmotion(
            emotionId: 9,
            durationMs: -1,
            byItemOption: false,
            currentTime: 2000,
            out string message);

        Assert.True(applied);
        Assert.Contains("until cleared", message);
        Assert.True(player.TryGetPacketOwnedEmotionState(999999, out PacketOwnedEmotionState state));
        Assert.Equal(9, state.EmotionId);
        Assert.Equal("cheers", state.EmotionName);
        Assert.Equal(-1, state.DurationMs);
        Assert.Equal(2000, state.AppliedAt);
        Assert.Equal(0, state.ExpireTime);
        Assert.False(state.ByItemOption);
        Assert.False(state.HasFiniteDuration);
        Assert.False(state.IsExpired(999999));
    }

    [Fact]
    public void TryApplyPacketOwnedEmotion_DefaultEmotionClearsOwnedState()
    {
        PlayerCharacter player = new(null, null, null);

        Assert.True(player.TryApplyPacketOwnedEmotion(2, 300, false, 100, out _));
        Assert.True(player.TryGetPacketOwnedEmotionState(200, out _));

        bool cleared = player.TryApplyPacketOwnedEmotion(
            emotionId: 0,
            durationMs: 0,
            byItemOption: false,
            currentTime: 300,
            out string message);

        Assert.True(cleared);
        Assert.Equal("Cleared packet-owned avatar emotion state.", message);
        Assert.False(player.TryGetPacketOwnedEmotionState(300, out _));
    }
}
