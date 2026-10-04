namespace HaCreator.MapSimulator.UI
{
    internal interface IEquipmentPendingChangeWindow
    {
        bool HasPendingEquipmentChange { get; }

        void ProcessPendingEquipmentChange(InventoryUI inventoryWindow);
    }
}
