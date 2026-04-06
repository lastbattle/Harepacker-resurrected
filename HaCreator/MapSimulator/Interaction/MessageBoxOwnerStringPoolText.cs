namespace HaCreator.MapSimulator.Interaction
{
    internal static class MessageBoxOwnerStringPoolText
    {
        internal const int CreateFailedNoticeStringPoolId = 0x1EA;

        private const string CreateFailedNoticeFallback = "You cannot open a chalkboard here.";
        private const string CreateFailedNoticeResolved = "You can't open the chalkboard here.";

        public static string GetCreateFailedNotice(bool appendFallbackSuffix = true)
        {
            if (TryResolve(CreateFailedNoticeStringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            return appendFallbackSuffix
                ? $"{CreateFailedNoticeFallback} (StringPool 0x{CreateFailedNoticeStringPoolId:X3} fallback)"
                : CreateFailedNoticeFallback;
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId == CreateFailedNoticeStringPoolId
                ? CreateFailedNoticeResolved
                : null;
            return text != null;
        }
    }
}
