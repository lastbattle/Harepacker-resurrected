namespace HaCreator.MapSimulator.Interaction
{
    internal static class RandomMorphDialogText
    {
        internal const int RequestBlockedNoticeStringPoolId = 0x136;
        internal const int TargetNotFoundNoticeStringPoolId = 0x1320;
        internal const int TownOnlyNoticeStringPoolId = 0x1321;

        private const string RequestBlockedNoticeFallback = "You cannot make another morph request yet.";
        private const string TargetNotFoundNoticeFallback = "Failed to find user %s.";
        private const string TownOnlyNoticeFallback = "You can use random transform potion only in the town.";

        internal static string GetRequestBlockedNotice(bool appendFallbackSuffix = true)
        {
            return MapleStoryStringPool.GetOrFallback(
                RequestBlockedNoticeStringPoolId,
                RequestBlockedNoticeFallback,
                appendFallbackSuffix);
        }

        internal static string FormatTargetNotFoundNotice(string targetName)
        {
            string resolvedFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                TargetNotFoundNoticeStringPoolId,
                "Failed to find user {0}.",
                1,
                out _);

            try
            {
                return string.Format(resolvedFormat, targetName ?? string.Empty);
            }
            catch
            {
                return string.Format("Failed to find user {0}.", targetName ?? string.Empty);
            }
        }

        internal static string GetTownOnlyNotice(bool appendFallbackSuffix = true)
        {
            return MapleStoryStringPool.GetOrFallback(
                TownOnlyNoticeStringPoolId,
                TownOnlyNoticeFallback,
                appendFallbackSuffix);
        }
    }
}
