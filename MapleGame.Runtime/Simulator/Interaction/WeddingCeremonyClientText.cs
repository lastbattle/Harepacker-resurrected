namespace HaCreator.MapSimulator.Interaction
{
    internal static class WeddingCeremonyClientText
    {
        internal const int WhiteWeddingOpeningBgmStringPoolId = 0x108E;
        internal const int SaintMapleOpeningBgmStringPoolId = 0x108F;
        internal const int SaintMapleGuestBlessPromptStringPoolId = 0x1090;

        private const int WhiteWeddingAltarMapId = 680000110;
        private const string OpeningBgmFallback = "BgmEvent/wedding";
        private const string GuestBlessPromptFallback = "Would you like to give your blessing to the couple?";

        internal static string ResolveOpeningBgmPath(int mapId)
        {
            int stringPoolId = mapId == WhiteWeddingAltarMapId
                ? WhiteWeddingOpeningBgmStringPoolId
                : SaintMapleOpeningBgmStringPoolId;
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                OpeningBgmFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }

        internal static string GetGuestBlessPromptText()
        {
            return MapleStoryStringPool.GetOrFallback(
                SaintMapleGuestBlessPromptStringPoolId,
                GuestBlessPromptFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }
    }
}
