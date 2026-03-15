using HaCreator.MapSimulator.Managers;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class LoginRuntimeManagerTests
    {
        [Fact]
        public void Initialize_StartsAtTitleAndBlocksFieldSimulation()
        {
            var manager = new LoginRuntimeManager();

            manager.Initialize(10_000);

            Assert.Equal(LoginStep.Title, manager.CurrentStep);
            Assert.Equal(LoginStep.Title, manager.BaseStep);
            Assert.True(manager.BlocksFieldSimulation);
            Assert.False(manager.FieldEntryRequested);
        }

        [Fact]
        public void CheckPasswordPacket_SchedulesWorldSelectTransition()
        {
            var manager = new LoginRuntimeManager();
            manager.Initialize(1_000);

            bool routed = manager.TryDispatchPacket(LoginPacketType.CheckPasswordResult, 1_000, out _);

            Assert.True(routed);
            Assert.Equal(LoginStep.WorldSelect, manager.PendingStep);
            Assert.Equal(1_800, manager.StepChangeAt);

            manager.Update(1_799);
            Assert.Equal(LoginStep.Title, manager.CurrentStep);

            manager.Update(1_800);
            Assert.Equal(LoginStep.WorldSelect, manager.CurrentStep);
            Assert.Equal(LoginStep.WorldSelect, manager.BaseStep);
            Assert.Equal(1, manager.GetPacketCount(LoginPacketType.CheckPasswordResult));
        }

        [Fact]
        public void SelectWorldPacket_SchedulesCharacterSelectTransition()
        {
            var manager = new LoginRuntimeManager();
            manager.Initialize(2_000);
            manager.ForceStep(LoginStep.WorldSelect);
            manager.TryDispatchPacket(LoginPacketType.WorldInformation, 2_000, out _);

            bool routed = manager.TryDispatchPacket(LoginPacketType.SelectWorldResult, 2_050, out _);

            Assert.True(routed);
            Assert.True(manager.HasWorldInformation);
            Assert.True(manager.CharacterSelectReady);
            Assert.Equal(LoginStep.CharacterSelect, manager.PendingStep);

            manager.Update(2_850);
            Assert.Equal(LoginStep.CharacterSelect, manager.CurrentStep);
        }

        [Fact]
        public void SelectCharacterPacket_RequestsFieldEntryImmediately()
        {
            var manager = new LoginRuntimeManager();
            manager.Initialize(3_000);
            manager.ForceStep(LoginStep.CharacterSelect);

            bool routed = manager.TryDispatchPacket(LoginPacketType.SelectCharacterResult, 3_100, out _);

            Assert.True(routed);
            Assert.True(manager.FieldEntryRequested);
            Assert.False(manager.BlocksFieldSimulation);
            Assert.Equal(LoginStep.EnteringField, manager.CurrentStep);
        }
    }
}
