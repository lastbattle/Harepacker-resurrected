using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.UI
{
    public interface IInventoryRuntime
    {
        int GetItemCount(InventoryType type, int itemId);
        bool CanAcceptItem(InventoryType type, int itemId, int quantity = 1, int? maxStackSize = null);
        bool TryConsumeItem(InventoryType type, int itemId, int quantity);
        void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1);
        void AddItem(InventoryType type, InventorySlotData slotData);
        Texture2D GetItemTexture(InventoryType type, int itemId);
        int GetSlotLimit(InventoryType type);
        bool CanExpandSlotLimit(InventoryType type, int amount = 4);
        bool TryExpandSlotLimit(InventoryType type, int amount = 4);
        long GetMesoCount();
        void AddMeso(long amount);
        bool TryConsumeMeso(long amount);
    }
}
