using HaCreator.MapEditor.AI;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MapAIPlacementParserTests
    {
        [Theory]
        [InlineData("not flipped", false)]
        [InlineData("facing right", false)]
        [InlineData("flipped", true)]
        [InlineData("facing left", true)]
        [InlineData("flip=false", false)]
        public void ObjectDirectionPreservesExplicitChoice(string direction, bool expected)
        {
            var command = new MapAIParser().ParseCommand(
                "ADD OBJECT at (10, 20) oS=\"test\" l0=\"a\" l1=\"b\" l2=\"0\" " + direction);
            Assert.True(command.IsValid, command.ErrorMessage);
            Assert.Equal(expected, command.Parameters["flip"]);
        }

        [Theory]
        [InlineData("raw_position=false", false)]
        [InlineData("raw_position=true", true)]
        [InlineData("raw_position", true)]
        public void ObjectRawPositionPreservesExplicitChoice(string positioning, bool expected)
        {
            var command = new MapAIParser().ParseCommand(
                "ADD OBJECT at (10, 20) oS=\"test\" l0=\"a\" l1=\"b\" l2=\"0\" " + positioning);
            Assert.True(command.IsValid, command.ErrorMessage);
            Assert.Equal(expected, command.Parameters["raw_position"]);
        }

        [Fact]
        public void MovePreservesSourceAndDestinationSeparately()
        {
            var command = new MapAIParser().ParseCommand("MOVE TILE at (10, 20) to (100, 200) layer=2");
            Assert.True(command.IsValid, command.ErrorMessage);
            Assert.Equal(10, command.Parameters["source_x"]);
            Assert.Equal(20, command.Parameters["source_y"]);
            Assert.Equal(100, command.TargetX);
            Assert.Equal(200, command.TargetY);
        }

        [Fact]
        public void RopeReversedEndpointsAreNormalized()
        {
            var command = new MapAIParser().ParseCommand("ADD ROPE x=-30 from y=200 to y=-50 layer=2");
            Assert.True(command.IsValid, command.ErrorMessage);
            Assert.Equal(-30, command.Parameters["rope_x"]);
            Assert.Equal(-50, command.Parameters["top_y"]);
            Assert.Equal(200, command.Parameters["bottom_y"]);
            Assert.Equal(2, command.Parameters["layer"]);
        }
    }
}
