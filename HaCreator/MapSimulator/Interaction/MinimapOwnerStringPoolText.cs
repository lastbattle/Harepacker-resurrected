namespace HaCreator.MapSimulator.Interaction
{
    internal static class MinimapOwnerStringPoolText
    {
        internal const int CreateWorldMapFailureStringPoolId = 0x118;
        internal const int CreateWorldMapGuestFailureStringPoolId = 0x11A;

        private const string CreateWorldMapFailureFallback = "The world map is unavailable for this field.";
        private const string CreateWorldMapFailureResolved = "You are currently at a place where\r\nthe world map is not available.";
        private const string CreateWorldMapGuestFailureResolved = "The World Map is unavailable to Guest ID players.\r\nPlease download the full client at\r\nmaplestory.nexon.net \r\nfor access to this feature.";

        public static string GetCreateWorldMapFailureNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(CreateWorldMapFailureStringPoolId, CreateWorldMapFailureFallback, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
                CreateWorldMapFailureStringPoolId => CreateWorldMapFailureResolved,
                CreateWorldMapGuestFailureStringPoolId => CreateWorldMapGuestFailureResolved,
                _ => null,
            };

            return text != null;
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            if (TryResolve(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} (StringPool 0x{stringPoolId:X3} fallback)";
        }
    }
}
