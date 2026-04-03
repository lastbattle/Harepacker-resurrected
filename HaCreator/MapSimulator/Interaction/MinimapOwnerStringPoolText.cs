namespace HaCreator.MapSimulator.Interaction
{
    internal static class MinimapOwnerStringPoolText
    {
        internal const int CreateWorldMapFailureStringPoolId = 0x118;

        private const string CreateWorldMapFailureFallback = "The world map is unavailable for this field.";

        public static string GetCreateWorldMapFailureNotice(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(CreateWorldMapFailureStringPoolId, CreateWorldMapFailureFallback, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
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
