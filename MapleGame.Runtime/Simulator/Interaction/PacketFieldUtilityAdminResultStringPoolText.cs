using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketFieldUtilityAdminResultStringPoolText
    {
        internal const int BlockAccessSuccessStringPoolId = 0xA4;
        internal const int UnblockAccessSuccessStringPoolId = 0xA6;
        internal const int RemoveNameFromRanksStringPoolId = 0xA9;
        internal const int InvalidCharacterNameStringPoolId = 0xAA;
        internal const int WrongNpcNameStringPoolId = 0xAC;
        internal const int RequestFailedStringPoolId = 0xAD;
        internal const int HiredMerchantLocatedFormatStringPoolId = 0xB0;
        internal const int HiredMerchantNotFoundStringPoolId = 0xB1;
        internal const int WarningMessageNotEnteredStringPoolId = 0xBDF;
        internal const int WarningSentStringPoolId = 0xBE0;
        internal const int MapNameFallbackStringPoolId = 0x6EC;

        private const string BlockAccessSuccessResolved = "You have successfully blocked access.";
        private const string UnblockAccessSuccessResolved = "The unblocking has been successful";
        private const string RemoveNameFromRanksResolved = "You have successfully removed the name from the ranks.";
        private const string InvalidCharacterNameResolved = "You have entered an invalid character name.";
        private const string WrongNpcNameResolved = "You have either entered a wrong NPC name or";
        private const string RequestFailedResolved = "Your request failed.";
        private const string HiredMerchantLocatedFormatResolved = "Hired Merchant located at : {0}";
        private const string HiredMerchantNotFoundResolved = "Unable to find the hired merchant.";
        private const string WarningMessageNotEnteredResolved = "Unable to send the message. Please enter the user's name before warning.";
        private const string WarningSentResolved = "Your warning has been successfully sent.";

        public static string GetSubtype4Notice()
        {
            return GetResolvedOrFallback(BlockAccessSuccessStringPoolId, BlockAccessSuccessResolved);
        }

        public static string GetSubtype5Notice()
        {
            return GetResolvedOrFallback(UnblockAccessSuccessStringPoolId, UnblockAccessSuccessResolved);
        }

        public static string GetSubtype6Notice(bool successBranch)
        {
            return successBranch
                ? GetResolvedOrFallback(InvalidCharacterNameStringPoolId, InvalidCharacterNameResolved)
                : GetResolvedOrFallback(RemoveNameFromRanksStringPoolId, RemoveNameFromRanksResolved);
        }

        public static string GetSubtype11EmptyChannelText()
        {
            return GetResolvedOrFallback(WrongNpcNameStringPoolId, WrongNpcNameResolved);
        }

        public static string GetSubtype42FailureText()
        {
            return GetResolvedOrFallback(RequestFailedStringPoolId, RequestFailedResolved);
        }

        public static string FormatHiredMerchantLocationText(string target)
        {
            return FormatResolvedOrFallback(HiredMerchantLocatedFormatStringPoolId, HiredMerchantLocatedFormatResolved, target);
        }

        public static string GetHiredMerchantNotFoundText()
        {
            return GetResolvedOrFallback(HiredMerchantNotFoundStringPoolId, HiredMerchantNotFoundResolved);
        }

        public static string GetWarningText(bool successBranch)
        {
            return successBranch
                ? GetResolvedOrFallback(WarningSentStringPoolId, WarningSentResolved)
                : GetResolvedOrFallback(WarningMessageNotEnteredStringPoolId, WarningMessageNotEnteredResolved);
        }

        public static string GetMapNameFallback(int mapId)
        {
            return string.Format(CultureInfo.InvariantCulture, "Map {0}", mapId);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText, appendFallbackSuffix: true);
        }

        private static string FormatResolvedOrFallback(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, fallbackFormat, args.Length, out bool usedResolvedText);
            string text = string.Format(CultureInfo.InvariantCulture, format, args);
            return usedResolvedText
                ? text
                : $"{text} ({MapleStoryStringPool.FormatFallbackLabel(stringPoolId)} fallback)";
        }
    }
}
