using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.UI
{
    public enum EquipmentChangeRequestKind
    {
        InventoryToCharacter,
        CharacterToCharacter,
        CharacterToInventory
    }

    public sealed class EquipmentChangeRequest
    {
        public int RequestId { get; set; }
        public int RequestedAtTick { get; set; }
        public EquipmentChangeRequestKind Kind { get; init; }
        public InventoryType SourceInventoryType { get; init; } = InventoryType.NONE;
        public int SourceInventoryIndex { get; init; } = -1;
        public HaCreator.MapSimulator.Character.EquipSlot? SourceEquipSlot { get; init; }
        public HaCreator.MapSimulator.Character.EquipSlot? TargetEquipSlot { get; init; }
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
    }
}
