using HaCreator.MapSimulator;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator
{
    public sealed class MapSimulatorWzValueTests
    {
        [Fact]
        public void GetWzIntValue_ParsesNumericStringProperties()
        {
            WzStringProperty property = new("hpR", "50");

            int value = MapSimulator.GetWzIntValue(property);

            Assert.Equal(50, value);
        }

        [Fact]
        public void GetWzIntValue_ReturnsZeroForNonNumericStringProperties()
        {
            WzStringProperty property = new("hpR", "not-a-number");

            int value = MapSimulator.GetWzIntValue(property);

            Assert.Equal(0, value);
        }
    }
}
