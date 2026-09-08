using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.Character.Skills
{
    public sealed class ItemHotkeyBinding
    {
        public int ItemId { get; set; }
        public InventoryType InventoryType { get; set; }

        public ItemHotkeyBinding Clone()
        {
            return new ItemHotkeyBinding
            {
                ItemId = ItemId,
                InventoryType = InventoryType
            };
        }
    }
}
