namespace HaCreator.MapSimulator.Interaction
{
    internal static class WeddingInvitationDialogText
    {
        internal const int AcceptButtonUolStringPoolId = 0x19CE;
        internal const int DefaultDialogUolStringPoolId = 0xEAF;
        internal const int AlternateDialogUolStringPoolId = 0xEB0;
        internal const int BasicBlackFontFaceStringPoolId = 0x1A25;

        private const string AcceptButtonUolFallback = "UI/UIWindow2.img/Wedding/Invitation/BtOK";
        private const string DefaultDialogUolFallback = "UI/UIWindow.img/Wedding/Invitation/Vegas";
        private const string AlternateDialogUolFallback = "UI/UIWindow.img/Wedding/Invitation/Cathedral";
        private const string BasicBlackFontFaceFallback = "DotumChe";

        internal static string GetAcceptButtonUolText()
        {
            return MapleStoryStringPool.GetOrFallback(
                AcceptButtonUolStringPoolId,
                AcceptButtonUolFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }

        internal static int ResolveDialogUolStringPoolId(int clientDialogType)
        {
            return clientDialogType == WeddingInvitationRuntime.AlternateClientDialogType
                ? AlternateDialogUolStringPoolId
                : DefaultDialogUolStringPoolId;
        }

        internal static string ResolveDialogUolText(int clientDialogType)
        {
            int stringPoolId = ResolveDialogUolStringPoolId(clientDialogType);
            string fallbackText = clientDialogType == WeddingInvitationRuntime.AlternateClientDialogType
                ? AlternateDialogUolFallback
                : DefaultDialogUolFallback;
            return MapleStoryStringPool.GetOrFallback(
                stringPoolId,
                fallbackText,
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }

        internal static string GetBasicBlackFontFaceName()
        {
            return MapleStoryStringPool.GetOrFallback(
                BasicBlackFontFaceStringPoolId,
                BasicBlackFontFaceFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 0);
        }
    }
}
