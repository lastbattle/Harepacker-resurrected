namespace HaCreator.MapSimulator.Interaction
{
    internal static class RandomMorphDialogText
    {
        internal const int RequestBlockedNoticeStringPoolId = 0x136;

        private const string RequestBlockedNoticeFallback = "You cannot make another morph request yet.";

        internal static string GetRequestBlockedNotice(bool appendFallbackSuffix = true)
        {
            return MapleStoryStringPool.GetOrFallback(
                RequestBlockedNoticeStringPoolId,
                RequestBlockedNoticeFallback,
                appendFallbackSuffix);
        }
    }
}
