using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Minimal preview payload shared between the shop catalog owner and the cash avatar preview window.
    /// </summary>
    public sealed class AdminShopAvatarPreviewSelection
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public InventoryType RewardInventoryType { get; init; } = InventoryType.NONE;
        public int RewardItemId { get; init; }
        public bool IsUserListing { get; init; }
    }
}
