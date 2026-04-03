using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketFieldUtilityAdminResultStringPoolText
    {
        internal const int Mode4StringPoolId = 0xA4;
        internal const int Mode5StringPoolId = 0xA6;
        internal const int MapleAdminNoticeDisabledStringPoolId = 0xA9;
        internal const int MapleAdminNoticeEnabledStringPoolId = 0xAA;
        internal const int ClearAdminNoticeStringPoolId = 0xAC;
        internal const int ClaimUnavailableStringPoolId = 0xAD;
        internal const int MoveTargetFormattedStringPoolId = 0xB0;
        internal const int MoveChannelFailureStringPoolId = 0xB1;
        internal const int AdminBlockDisabledStringPoolId = 0xBDF;
        internal const int AdminBlockEnabledStringPoolId = 0xBE0;
        internal const int MapNameFallbackStringPoolId = 0x6EC;

        private const string Mode4Fallback = "Applied admin mode subtype 4.";
        private const string Mode5Fallback = "Applied admin mode subtype 5.";
        private const string MapleAdminNoticeDisabledFallback = "Disabled Maple Admin notice mode.";
        private const string MapleAdminNoticeEnabledFallback = "Enabled Maple Admin notice mode.";
        private const string ClearAdminNoticeFallback = "Cleared the current admin notice.";
        private const string ClaimUnavailableFallback = "Admin claim is unavailable.";
        private const string MoveTargetFormattedFallback = "Moved to {0}.";
        private const string MoveChannelFailureFallback = "Unable to move to the requested channel.";
        private const string AdminBlockDisabledFallback = "Admin block is disabled.";
        private const string AdminBlockEnabledFallback = "Admin block is enabled.";

        public static string GetModeNotice(byte subtype)
        {
            return subtype switch
            {
                4 => GetResolvedOrFallback(Mode4StringPoolId, Mode4Fallback),
                5 => GetResolvedOrFallback(Mode5StringPoolId, Mode5Fallback),
                _ => string.Format(CultureInfo.InvariantCulture, "Applied admin mode subtype {0}.", subtype)
            };
        }

        public static string GetMapleAdminNoticeModeNotice(bool enabled)
        {
            return enabled
                ? GetResolvedOrFallback(MapleAdminNoticeEnabledStringPoolId, MapleAdminNoticeEnabledFallback)
                : GetResolvedOrFallback(MapleAdminNoticeDisabledStringPoolId, MapleAdminNoticeDisabledFallback);
        }

        public static string GetClearAdminNoticeText()
        {
            return GetResolvedOrFallback(ClearAdminNoticeStringPoolId, ClearAdminNoticeFallback);
        }

        public static string GetClaimUnavailableText()
        {
            return GetResolvedOrFallback(ClaimUnavailableStringPoolId, ClaimUnavailableFallback);
        }

        public static string FormatMoveTargetText(string target)
        {
            return FormatResolvedOrFallback(MoveTargetFormattedStringPoolId, MoveTargetFormattedFallback, target);
        }

        public static string GetMoveChannelFailureText()
        {
            return GetResolvedOrFallback(MoveChannelFailureStringPoolId, MoveChannelFailureFallback);
        }

        public static string GetAdminBlockText(bool enabled)
        {
            return enabled
                ? GetResolvedOrFallback(AdminBlockEnabledStringPoolId, AdminBlockEnabledFallback)
                : GetResolvedOrFallback(AdminBlockDisabledStringPoolId, AdminBlockDisabledFallback);
        }

        public static string GetMapNameFallback(int mapId)
        {
            return string.Format(CultureInfo.InvariantCulture, "Map {0}", mapId);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
                ClearAdminNoticeStringPoolId => ClearAdminNoticeFallback,
                ClaimUnavailableStringPoolId => ClaimUnavailableFallback,
                MoveChannelFailureStringPoolId => MoveChannelFailureFallback,
                AdminBlockDisabledStringPoolId => AdminBlockDisabledFallback,
                AdminBlockEnabledStringPoolId => AdminBlockEnabledFallback,
                _ => null,
            };

            return text != null;
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText)
        {
            return TryResolve(stringPoolId, out string resolvedText)
                ? resolvedText
                : $"{fallbackText} (StringPool 0x{stringPoolId:X} fallback)";
        }

        private static string FormatResolvedOrFallback(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string format = TryResolve(stringPoolId, out string resolvedFormat)
                ? resolvedFormat
                : fallbackFormat;
            string text = string.Format(CultureInfo.InvariantCulture, format, args);
            return TryResolve(stringPoolId, out _)
                ? text
                : $"{text} (StringPool 0x{stringPoolId:X} fallback)";
        }
    }
}
