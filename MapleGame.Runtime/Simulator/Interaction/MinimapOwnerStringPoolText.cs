namespace HaCreator.MapSimulator.Interaction
{
    internal static class MinimapOwnerStringPoolText
    {
        internal const int CreateWorldMapFailureStringPoolId = 0x118;
        internal const int CreateWorldMapGuestFailureStringPoolId = 0x11A;

        private const string CreateWorldMapFailureFallback = "The world map is unavailable for this field.";
        private const string CreateWorldMapGuestFailureFallback = "The World Map is unavailable to Guest ID players.";

        public static string GetCreateWorldMapFailureNotice(bool appendFallbackSuffix = true)
        {
            return GetCreateWorldMapFailureNotice(isGuestContext: false, appendFallbackSuffix);
        }

        public static string GetCreateWorldMapFailureNotice(bool isGuestContext, bool appendFallbackSuffix = true)
        {
            return isGuestContext
                ? GetResolvedOrFallback(CreateWorldMapGuestFailureStringPoolId, CreateWorldMapGuestFailureFallback, appendFallbackSuffix)
                : GetResolvedOrFallback(CreateWorldMapFailureStringPoolId, CreateWorldMapFailureFallback, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText, appendFallbackSuffix, minimumHexWidth: 3);
        }
    }
}
