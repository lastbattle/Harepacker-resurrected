using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public class MemoryGamePromptParityTests
    {
        [Fact]
        public void ClickStartButton_AllowsClientLikeStartRequest_WhenOpponentIsPresentWithoutReady()
        {
            MemoryGameField field = new();
            field.OpenRoom("Match Cards", "Host", "Guest", 4, 4, localPlayerIndex: 0);

            bool handled = field.TryClickStartButton(tickCount: 1000, out string message);

            Assert.True(handled);
            Assert.Equal("Start request packet (61) sent.", message);
        }

        [Fact]
        public void ClickStartButton_BlocksStartRequest_WhenOpponentSeatIsEmpty()
        {
            MemoryGameField field = new();
            field.OpenRoom("Match Cards", "Host", "Opponent", 4, 4, localPlayerIndex: 0);

            bool handled = field.TryClickStartButton(tickCount: 1000, out string message);

            Assert.False(handled);
            Assert.Equal("Start request ignored because no opponent is seated in the Match Cards room yet.", message);
        }

        [Fact]
        public void ClickStartButton_BlocksWhilePromptModalIsActive()
        {
            MemoryGameField field = new();
            field.OpenRoom("Match Cards", "Host", "Guest", 4, 4, localPlayerIndex: 0);

            bool promptOpened = field.TryRequestRoomExit(playerIndex: 0, out _);
            bool handled = field.TryClickStartButton(tickCount: 1000, out string message);

            Assert.True(promptOpened);
            Assert.False(handled);
            Assert.Equal("Finish the current Match Cards confirmation prompt before starting the Match Cards round.", message);
        }
    }
}
