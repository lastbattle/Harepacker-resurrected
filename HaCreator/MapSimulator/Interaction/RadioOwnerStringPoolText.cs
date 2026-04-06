using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class RadioOwnerStringPoolText
    {
        internal const int StartNoticeStringPoolId = 0x14CF;
        internal const int CompleteNoticeStringPoolId = 0x14D0;
        internal const int TrackPathTemplateStringPoolId = 0x1501;
        internal const int AudioPathTemplateStringPoolId = 0x1502;

        public static string FormatNotice(int stringPoolId, string trackName, bool appendFallbackSuffix = false)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, null, 1, out bool hasResolvedText);
            string displayName = string.IsNullOrWhiteSpace(trackName) ? "radio track" : trackName.Trim();
            if (hasResolvedText)
            {
                return string.Format(format, displayName);
            }

            string placeholder = $"{FormatStringPoolId(stringPoolId)} {Quote(displayName)}";
            return appendFallbackSuffix
                ? $"{placeholder} (localized client text unresolved)"
                : placeholder;
        }

        public static string FormatPathTemplateResolution(
            int stringPoolId,
            string authoredTrack,
            string resolvedDescriptor,
            bool appendFallbackSuffix = false)
        {
            string authored = string.IsNullOrWhiteSpace(authoredTrack) ? "radio track" : authoredTrack.Trim();
            string resolved = string.IsNullOrWhiteSpace(resolvedDescriptor) ? "unresolved" : resolvedDescriptor.Trim();
            bool hasResolvedText = false;
            string line;
            if (MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, null, 1, out hasResolvedText) is string format && hasResolvedText)
            {
                line = string.Format(format, authored);
                line = $"{FormatStringPoolId(stringPoolId)}: {Quote(authored)} => {Quote(line)}";
            }
            else
            {
                line = stringPoolId switch
                {
                    TrackPathTemplateStringPoolId =>
                        $"Track path ({FormatStringPoolId(stringPoolId)}): {Quote(authored)} => {Quote(resolved)}",
                    AudioPathTemplateStringPoolId =>
                        $"Audio path ({FormatStringPoolId(stringPoolId)}): {Quote(authored)} => {Quote(resolved)}",
                    _ =>
                        $"{FormatStringPoolId(stringPoolId)}: {Quote(authored)} => {Quote(resolved)}",
                };
            }

            return appendFallbackSuffix && !hasResolvedText
                ? $"{line} (localized client format unresolved)"
                : line;
        }

        private static string FormatStringPoolId(int stringPoolId)
        {
            return MapleStoryStringPool.FormatFallbackLabel(stringPoolId);
        }

        private static string Quote(string value)
        {
            return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }
    }
}
