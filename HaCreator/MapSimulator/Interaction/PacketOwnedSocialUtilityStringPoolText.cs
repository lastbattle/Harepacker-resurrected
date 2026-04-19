namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedSocialUtilityStringPoolText
    {
        internal const int ParcelArrivalSenderStringPoolId = 0x0F50;
        internal const int ParcelArrivalMesoStringPoolId = 0x0F51;
        internal const int ParcelArrivalItemStringPoolId = 0x0F52;
        internal const int ParcelClaimSuccessStringPoolId = 0x0F64;
        internal const int ParcelDiscardResultStringPoolId = 0x0F65;
        internal const int ParcelDeliveryNoticeStringPoolId = 0x0F67;

        internal static string ResolveParcelRemovalNotice(byte resultCode, out int stringPoolId)
        {
            stringPoolId = resultCode == 3
                ? ParcelClaimSuccessStringPoolId
                : ParcelDiscardResultStringPoolId;

            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                resultCode == 3
                    ? "Parcel claim completed."
                    : "Parcel discard completed.",
                appendFallbackSuffix: true);
        }

        internal static string ResolveParcelDeliveryNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                ParcelDeliveryNoticeStringPoolId,
                "A new parcel has arrived.",
                appendFallbackSuffix: true);
        }

        internal static string ResolveParcelArrivalSenderNotice(string sender)
        {
            return FormatClientTemplate(
                ParcelArrivalSenderStringPoolId,
                "[%s]",
                string.IsNullOrWhiteSpace(sender) ? "Maple Delivery Service" : sender.Trim());
        }

        internal static string ResolveParcelArrivalMesoNotice(int meso)
        {
            return FormatClientTemplate(
                ParcelArrivalMesoStringPoolId,
                "\r\nmeso : %d",
                meso);
        }

        internal static string ResolveParcelArrivalItemNotice(string itemName, int quantity = 1)
        {
            string resolvedNotice = FormatClientTemplate(
                ParcelArrivalItemStringPoolId,
                "\r\nitem : %s",
                string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim());

            if (quantity > 1)
            {
                resolvedNotice += $" x{quantity}";
            }

            return resolvedNotice;
        }

        internal static bool TryResolveTrunkNoticeStringPoolId(int packetSubtype, out int stringPoolId)
        {
            stringPoolId = packetSubtype switch
            {
                10 => 0x0366,
                11 or 16 => 0x1A8B,
                12 => 0x0374,
                17 => 0x0373,
                23 => 0x16ED,
                24 => 0x0369,
                _ => 0x0369
            };

            return true;
        }

        internal static string ResolveTrunkNotice(int packetSubtype, out int stringPoolId)
        {
            TryResolveTrunkNoticeStringPoolId(packetSubtype, out stringPoolId);
            string fallback = packetSubtype switch
            {
                10 => "You do not have enough mesos in storage.",
                11 or 16 => "That item cannot be moved through storage.",
                12 => "Storage is full.",
                17 => "Your inventory is full.",
                23 => "Storage access is restricted.",
                _ => "Storage transaction completed."
            };

            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallback, appendFallbackSuffix: true);
        }

        private static string FormatClientTemplate(int stringPoolId, string fallbackFormat, object value)
        {
            string template = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackFormat, appendFallbackSuffix: true);
            string formattedValue = value?.ToString() ?? string.Empty;
            return template
                .Replace("%s", formattedValue, System.StringComparison.Ordinal)
                .Replace("%d", formattedValue, System.StringComparison.Ordinal);
        }
    }
}
