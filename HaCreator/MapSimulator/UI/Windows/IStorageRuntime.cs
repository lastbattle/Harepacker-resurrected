using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public interface IStorageRuntime
    {
        string AccountLabel { get; }
        string CurrentCharacterName { get; }
        IReadOnlyList<string> SharedCharacterNames { get; }
        bool CanCurrentCharacterAccess { get; }

        IReadOnlyList<InventorySlotData> GetSlots(InventoryType type);
        int GetSlotLimit();
        void SetSlotLimit(int slotLimit);
        int GetUsedSlotCount();
        bool CanExpandSlotLimit(int amount = 4);
        bool TryExpandSlotLimit(int amount = 4);
        long GetMesoCount();
        void SetMeso(long amount);
        void AddItem(InventoryType type, InventorySlotData slotData);
        bool CanAcceptItem(InventoryType type, InventorySlotData slotData);
        bool TryRemoveSlotAt(InventoryType type, int slotIndex, out InventorySlotData slotData);
        void SortSlots(InventoryType type);
        void ConfigureAccess(string accountLabel, string currentCharacterName, IEnumerable<string> sharedCharacterNames);
    }
}
