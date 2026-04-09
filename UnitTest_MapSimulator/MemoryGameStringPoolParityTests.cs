using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class MemoryGameStringPoolParityTests
{
    [Theory]
    [InlineData(0x01C4, "[%s] have entered.")]
    [InlineData(0x01D7, "Are you sure you want to give up?")]
    [InlineData(0x01D8, "Will you expel the user?")]
    [InlineData(0x01E0, "Will you call to leave after this game?")]
    [InlineData(0x01E1, "Will you cancel the request\r\nto leave after this game?")]
    [InlineData(0x01E4, "Are you sure you want to leave?")]
    public void MapleStoryStringPool_ResolvesMatchCardsClientStrings(int stringPoolId, string expected)
    {
        Assert.True(MapleStoryStringPool.TryGet(stringPoolId, out string text));
        Assert.Equal(expected, text);
    }

    [Fact]
    public void PromptTexts_UseExactClientStringsThroughMemoryGameField()
    {
        MemoryGameField field = new();
        field.OpenRoom("Match Cards", "Player", "Opponent", 4, 4, 0);

        Assert.True(field.TryPromptGiveUp(0, out string giveUpPrompt));
        Assert.Equal("Are you sure you want to give up?", giveUpPrompt);
        Assert.Equal(giveUpPrompt, field.PendingPromptText);

        Assert.True(field.TryCancelPrompt(out _));
        Assert.True(field.TryBanParticipant(0, out string banPrompt));
        Assert.Equal("Will you expel the user?", banPrompt);
        Assert.Equal(banPrompt, field.PendingPromptText);

        Assert.True(field.TryCancelPrompt(out _));
        Assert.True(field.TryRequestRoomExit(0, out string closePrompt));
        Assert.Equal("Are you sure you want to leave?", closePrompt);
        Assert.Equal(closePrompt, field.PendingPromptText);
    }

    [Fact]
    public void LeaveBookingPrompts_UseExactClientStringsDuringActiveRound()
    {
        MemoryGameField field = new();
        field.OpenRoom("Match Cards", "Player", "Opponent", 4, 4, 0);

        Assert.True(field.TrySetReady(0, true, out _));
        Assert.True(field.TrySetReady(1, true, out _));
        Assert.True(field.TryStartGame(Environment.TickCount, out _));

        Assert.True(field.TryRequestRoomExit(0, out string bookLeavePrompt));
        Assert.Equal("Will you call to leave after this game?", bookLeavePrompt);
        Assert.Equal(bookLeavePrompt, field.PendingPromptText);

        Assert.True(field.TryConfirmPrompt(Environment.TickCount, out _));
        Assert.True(field.TryRequestRoomExit(0, out string cancelBookPrompt));
        Assert.Equal("Will you cancel the request\r\nto leave after this game?", cancelBookPrompt);
        Assert.Equal(cancelBookPrompt, field.PendingPromptText);
    }
}
