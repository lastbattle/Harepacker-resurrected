namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedSocialUtilityStringPoolText
    {
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
    }
}
