namespace HaCreator.MapSimulator.Interaction
{
    internal static class MinimapOwnerStringPoolText
    {
        internal const int CreateWorldMapFailureStringPoolId = 0x118;
        internal const int CreateWorldMapGuestFailureStringPoolId = 0x11A;

        private const string CreateWorldMapFailureFallback = "The world map is unavailable for this field.";

        public static string GetCreateWorldMapFailureNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(CreateWorldMapFailureStringPoolId, CreateWorldMapFailureFallback, appendFallbackSuffix);
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
