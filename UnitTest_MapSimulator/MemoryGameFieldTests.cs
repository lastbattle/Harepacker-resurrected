using HaCreator.MapSimulator.Fields;
using System.Linq;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class MemoryGameFieldTests
{
    [Fact]
    public void PacketDispatch_OpenReadyStart_TransitionsIntoPlaying()
    {
        var field = new MemoryGameField();

        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.OpenRoom, 1000, out _, playerOneName: "Alice", playerTwoName: "Bob"));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, 1000, out _, playerIndex: 0, readyState: true));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, 1000, out _, playerIndex: 1, readyState: true));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.StartGame, 1000, out _));

        Assert.Equal(MemoryGameField.RoomStage.Playing, field.Stage);
        Assert.Equal(16, field.Cards.Count);
        Assert.Equal(1, field.GetPacketCount(MemoryGamePacketType.StartGame));
        Assert.Equal(MemoryGamePacketType.StartGame, field.LastPacketType);
    }

    [Fact]
    public void PacketDispatch_RevealMismatch_HidesCardsAndAdvancesTurn()
    {
        var field = new MemoryGameField();

        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.OpenRoom, 1000, out _));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, 1000, out _, playerIndex: 0, readyState: true));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, 1000, out _, playerIndex: 1, readyState: true));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.StartGame, 1000, out _));

        int firstIndex = 0;
        int secondIndex = Enumerable.Range(1, field.Cards.Count - 1)
            .First(index => field.Cards[index].FaceId != field.Cards[firstIndex].FaceId);

        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.RevealCard, 1000, out _, playerIndex: 0, cardIndex: firstIndex));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.RevealCard, 1000, out _, playerIndex: 0, cardIndex: secondIndex));

        Assert.True(field.Cards[firstIndex].IsFaceUp);
        Assert.True(field.Cards[secondIndex].IsFaceUp);

        field.Update(2000);

        Assert.False(field.Cards[firstIndex].IsFaceUp);
        Assert.False(field.Cards[secondIndex].IsFaceUp);
        Assert.Equal(1, field.CurrentTurnIndex);
    }

    [Fact]
    public void PacketDispatch_EndRoom_ResetsRoomState()
    {
        var field = new MemoryGameField();

        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.OpenRoom, 1000, out _));
        Assert.True(field.TryDispatchPacket(MemoryGamePacketType.EndRoom, 1000, out _));

        Assert.Equal(MemoryGameField.RoomStage.Hidden, field.Stage);
        Assert.Empty(field.Cards);
        Assert.Null(field.LastPacketType);
    }
}
