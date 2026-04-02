namespace HaCreator.MapSimulator.Interaction
{
    internal static class AntiMacroOwnerStringPoolText
    {
        internal const int AttemptMessageStringPoolId = 6677;

        private const string AttemptMessageFallback = "Attempt {0} of 2";

        public static string GetAttemptMessageFormat(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(AttemptMessageStringPoolId, AttemptMessageFallback, appendFallbackSuffix);
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

            return $"{fallbackText} (StringPool {stringPoolId} fallback)";
        }
    }
}
