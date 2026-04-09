namespace HaCreator.MapSimulator.UI
{
    public sealed class InventoryPendingRequestConsumptionSnapshot
    {
        public Microsoft.Xna.Framework.Point DebugPoint { get; init; }
        public MapleLib.WzLib.WzStructure.Data.ItemStructure.InventoryType InventoryType { get; init; }
        public int SlotIndex { get; init; }
        public InventorySlotData SlotData { get; init; }
        public bool RemovedCompletely { get; init; }
    }
}
