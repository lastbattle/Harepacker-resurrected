using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.UI
{
    public interface IInventoryRuntime
    {
        int GetItemCount(InventoryType type, int itemId);
        bool TryConsumeItem(InventoryType type, int itemId, int quantity);
        void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1);
        Texture2D GetItemTexture(InventoryType type, int itemId);
    }
}
