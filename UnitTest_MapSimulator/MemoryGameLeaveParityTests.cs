using HaCreator.MapSimulator.Fields;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MemoryGameLeaveParityTests
    {
        [Fact]
        public void RemoteSeatLeave_DoesNotTearDownActiveBoard()
        {
            MemoryGameField field = new();
            int tick = Environment.TickCount;

            Assert.True(field.TryDispatchPacket(
                MemoryGamePacketType.OpenRoom,
                tick,
                out _,
                playerOneName: "Host",
                playerTwoName: "Guest"));
            Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, tick, out _, playerIndex: 0, readyState: true));
            Assert.True(field.TryDispatchPacket(MemoryGamePacketType.SetReady, tick, out _, playerIndex: 1, readyState: true));
            Assert.True(field.TryDispatchPacket(MemoryGamePacketType.StartGame, tick, out _));
            Assert.Equal(MemoryGameField.RoomStage.Playing, field.Stage);

            Assert.True(field.TryDispatchMiniRoomPacket(new byte[] { 10, 1, 0 }, tick, out _));

            Assert.Equal(MemoryGameField.RoomStage.Playing, field.Stage);
            Assert.True(field.Cards.Count > 0);
            Assert.False(field.ReadyStates[1]);
            Assert.Equal("Opponent", field.PlayerNames[1]);
        }

        [Fact]
        public void LocalLeaveReason3_ClosesRoom()
        {
            MemoryGameField field = new();
            int tick = Environment.TickCount;

            Assert.True(field.TryDispatchPacket(MemoryGamePacketType.OpenRoom, tick, out _));
            Assert.True(field.TryDispatchMiniRoomPacket(new byte[] { 10, 0, 3 }, tick, out _));

            Assert.Equal(MemoryGameField.RoomStage.Hidden, field.Stage);
            Assert.False(field.IsVisible);
        }
    }
}
