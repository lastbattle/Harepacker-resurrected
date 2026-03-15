using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator
{
    public class InventoryItemMetadataResolverTests
    {
        [Fact]
        public void TryResolveImageSource_UsesCharacterPaths_ForEquipItems()
        {
            bool resolved = InventoryItemMetadataResolver.TryResolveImageSource(1002140, out string category, out string imagePath);

            Assert.True(resolved);
            Assert.Equal("Character", category);
            Assert.Equal("Cap/01002140.img", imagePath);
        }

        [Fact]
        public void ResolveMaxStack_DefaultsEquipItemsToSingleSlot()
        {
            int maxStack = InventoryItemMetadataResolver.ResolveMaxStack(InventoryType.EQUIP);

            Assert.Equal(1, maxStack);
        }

        [Fact]
        public void ResolveMaxStack_UsesSlotMaxForStackableInventories()
        {
            int maxStack = InventoryItemMetadataResolver.ResolveMaxStack(InventoryType.ETC, 200);

            Assert.Equal(200, maxStack);
        }
    }
}
