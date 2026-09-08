namespace HaCreator.MapSimulator.Interaction
{
    internal static class MessageBoxOwnerStringPoolText
    {
        internal const int CreateFailedNoticeStringPoolId = 0x1EA;

        private const string CreateFailedNoticeFallback = "You cannot use the chalkboard here.";

        public static string GetCreateFailedNotice(bool appendFallbackSuffix = true)
        {
            return MapleStoryStringPool.GetOrFallback(
                CreateFailedNoticeStringPoolId,
                CreateFailedNoticeFallback,
                appendFallbackSuffix,
                minimumHexWidth: 3);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }
    }
}
