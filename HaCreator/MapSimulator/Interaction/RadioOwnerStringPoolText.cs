using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class RadioOwnerStringPoolText
    {
        internal const int StartNoticeStringPoolId = 0x14CF;
        internal const int CompleteNoticeStringPoolId = 0x14D0;
        internal const int TrackPathTemplateStringPoolId = 0x1501;
        internal const int AudioPathTemplateStringPoolId = 0x1502;

        private const string StartNoticeFallback = "Radio started: {0}";
        private const string CompleteNoticeFallback = "Radio completed: {0}";

        public static string FormatNotice(int stringPoolId, string trackName, bool appendFallbackSuffix = false)
        {
            string format = ResolveNoticeFormat(stringPoolId);
            bool hasResolvedText = !string.IsNullOrWhiteSpace(format);
            string effectiveFormat = hasResolvedText ? format : ResolveFallbackNoticeFormat(stringPoolId);
            string formatted = string.Format(
                effectiveFormat,
                string.IsNullOrWhiteSpace(trackName) ? "radio track" : trackName.Trim());
            return appendFallbackSuffix && !hasResolvedText
                ? $"{formatted} (StringPool 0x{stringPoolId:X} fallback)"
                : formatted;
        }

        public static string FormatPathTemplateResolution(
            int stringPoolId,
            string authoredTrack,
            string resolvedDescriptor,
            bool appendFallbackSuffix = false)
        {
            string authored = string.IsNullOrWhiteSpace(authoredTrack) ? "radio track" : authoredTrack.Trim();
            string resolved = string.IsNullOrWhiteSpace(resolvedDescriptor) ? "unresolved" : resolvedDescriptor.Trim();
            string line = stringPoolId switch
            {
                TrackPathTemplateStringPoolId =>
                    $"Track path ({FormatStringPoolId(stringPoolId)}): {Quote(authored)} => {Quote(resolved)}",
                AudioPathTemplateStringPoolId =>
                    $"Audio path ({FormatStringPoolId(stringPoolId)}): {Quote(authored)} => {Quote(resolved)}",
                _ =>
                    $"{FormatStringPoolId(stringPoolId)}: {Quote(authored)} => {Quote(resolved)}",
            };

            return appendFallbackSuffix
                ? $"{line} (localized client format unresolved)"
                : line;
        }

        private static string ResolveNoticeFormat(int stringPoolId)
        {
            // The radio branch already recovered ownership ids from the client
            // (`CRadioManager::Play` / `Stop`), but literal localized string
            // payloads are still not fully extracted.
            return null;
        }

        private static string ResolveFallbackNoticeFormat(int stringPoolId)
        {
            return stringPoolId switch
            {
                StartNoticeStringPoolId => StartNoticeFallback,
                CompleteNoticeStringPoolId => CompleteNoticeFallback,
                _ => "{0}",
            };
        }

        private static string FormatStringPoolId(int stringPoolId)
        {
            return $"StringPool 0x{stringPoolId:X}";
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }
    }
}
