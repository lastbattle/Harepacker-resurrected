using HaCreator.MapSimulator.Managers;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class GameStateManagerDirectionModeTests
    {
        [Fact]
        public void EnterDirectionMode_BlocksPlayerInputWithoutDisablingPlayerControl()
        {
            var manager = new GameStateManager();

            manager.EnterDirectionMode();

            Assert.True(manager.PlayerControlEnabled);
            Assert.True(manager.DirectionModeActive);
            Assert.False(manager.IsPlayerInputEnabled);
        }

        [Fact]
        public void RequestLeaveDirectionMode_KeepsInputBlockedUntilDelayExpires()
        {
            var manager = new GameStateManager();
            manager.EnterDirectionMode();

            manager.RequestLeaveDirectionMode(1_000, 300);
            manager.UpdateDirectionMode(1_299);

            Assert.True(manager.DirectionModeActive);
            Assert.False(manager.IsPlayerInputEnabled);

            manager.UpdateDirectionMode(1_300);

            Assert.False(manager.DirectionModeActive);
            Assert.True(manager.IsPlayerInputEnabled);
        }

        [Fact]
        public void ReenterDirectionMode_CancelsPendingRelease()
        {
            var manager = new GameStateManager();
            manager.EnterDirectionMode();
            manager.RequestLeaveDirectionMode(1_000, 300);

            manager.EnterDirectionMode();
            manager.UpdateDirectionMode(1_500);

            Assert.True(manager.DirectionModeActive);
            Assert.Equal(int.MinValue, manager.DirectionModeReleaseAt);
            Assert.False(manager.IsPlayerInputEnabled);
        }
    }
}
