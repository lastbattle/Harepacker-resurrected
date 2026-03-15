using HaCreator.MapSimulator.Fields;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class EscortProgressionControllerTests
    {
        [Fact]
        public void CanFollowIndex_AllowsAllEscortsWhenNoIndexedEscortsExist()
        {
            EscortProgressionState state = EscortProgressionState.None;

            Assert.True(EscortProgressionController.CanFollowIndex(null, state));
            Assert.True(EscortProgressionController.CanFollowIndex(0, state));
            Assert.True(EscortProgressionController.CanFollowIndex(3, state));
        }

        [Fact]
        public void ResolveState_UsesLowestPositiveEscortIndex()
        {
            EscortProgressionState state = EscortProgressionController.ResolveState(new int?[] { 4, null, 2, 7, 0, -1 });

            Assert.True(state.HasIndexedEscorts);
            Assert.Equal(2, state.ActiveIndex);
        }

        [Fact]
        public void CanFollowIndex_OnlyAllowsCurrentEscortStageWhenIndexedEscortsExist()
        {
            EscortProgressionState state = new EscortProgressionState(true, 2);

            Assert.False(EscortProgressionController.CanFollowIndex(null, state));
            Assert.False(EscortProgressionController.CanFollowIndex(1, state));
            Assert.True(EscortProgressionController.CanFollowIndex(2, state));
            Assert.False(EscortProgressionController.CanFollowIndex(3, state));
        }
    }
}
