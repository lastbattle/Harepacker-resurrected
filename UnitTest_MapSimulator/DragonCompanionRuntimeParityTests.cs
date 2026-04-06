using HaCreator.MapSimulator.Companions;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class DragonCompanionRuntimeParityTests
    {
        [Theory]
        [InlineData("stand", false)]
        [InlineData("move", false)]
        [InlineData("magicmissile", true)]
        [InlineData("dragonBreathe", true)]
        [InlineData("icebreathe_prepare", true)]
        [InlineData("dragonThrust", true)]
        [InlineData(null, false)]
        public void AuxiliaryLayersFollowClientOneTimeActionVisibility(string actionName, bool expectedHidden)
        {
            Assert.Equal(expectedHidden, DragonCompanionRuntime.ShouldHideAuxiliaryLayerForAction(actionName));
        }
    }
}
