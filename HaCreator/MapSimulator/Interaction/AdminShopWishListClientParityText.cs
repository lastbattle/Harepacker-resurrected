namespace HaCreator.MapSimulator.Interaction
{
    internal static class AdminShopWishListClientParityText
    {
        internal const int RegisterConfirmationFormatStringPoolId = 0xE54;

        internal static string GetRegisterConfirmationText(string itemName)
        {
            string safeItemName = string.IsNullOrWhiteSpace(itemName) ? "this item" : itemName.Trim();
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                RegisterConfirmationFormatStringPoolId,
                "Would you like to register %s in the wish list?",
                1,
                out _);

            try
            {
                return string.Format(format, safeItemName);
            }
            catch
            {
                return $"Would you like to register {safeItemName} in the wish list?";
            }
        }
    }
}
