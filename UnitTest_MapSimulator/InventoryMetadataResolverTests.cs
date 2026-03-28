using HaCreator.MapSimulator.UI;
using HaCreator.Wz;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator
{
    public sealed class InventoryMetadataResolverTests
    {
        [Fact]
        public void TryResolveItemName_UsesCachedDisplayNameEntry()
        {
            WzInformationManager previousInfoManager = global::HaCreator.Program.InfoManager;
            try
            {
                var infoManager = new WzInformationManager();
                infoManager.ItemNameCache[2000000] = Tuple.Create("Use", "Red Potion", "Restores HP.");
                global::HaCreator.Program.InfoManager = infoManager;

                bool resolved = InventoryItemMetadataResolver.TryResolveItemName(2000000, out string itemName);

                Assert.True(resolved);
                Assert.Equal("Red Potion", itemName);
            }
            finally
            {
                global::HaCreator.Program.InfoManager = previousInfoManager;
            }
        }

        [Fact]
        public void GetWzIntValue_ParsesNumericStringProperties()
        {
            var property = new WzStringProperty("prop", "10");

            int parsedValue = global::HaCreator.MapSimulator.MapSimulator.GetWzIntValue(property);

            Assert.Equal(10, parsedValue);
        }
    }
}
