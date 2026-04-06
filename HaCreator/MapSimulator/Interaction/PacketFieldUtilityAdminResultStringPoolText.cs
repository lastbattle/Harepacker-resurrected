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

        // Recovered from MapleStory.exe v95 StringPool::ms_aString with StringPool::GetString:
        // - 0x0A4 seed 0xDB raw "DB C0 F6 F2 19 1E D7 85 C9 12 40 50 78 EC 22 40 AD FF EC EB 55 0F 96 91 C0 5D 50 4E 7E EB 67 52 BD FA FC F4 4A 58"
        // - 0x0A6 seed 0xDD raw "DD 32 0E 79 C5 AF B5 AC DC A7 AF FF 07 53 7B EF 12 07 15 3C 87 BF BE A0 90 BB B9 F7 0D 58 6F BC 1C 13 0A"
        // - 0x0A9 seed 0xE0 raw "E0 6A 5F 92 0E BE BF 03 E3 66 17 D6 12 8B 83 08 A0 55 45 8B 42 AF FE 07 E3 2B 0B D5 14 8C C6 0F BB 56 10 89 4F BB BB 55 E0 34 0B CE 51 9C 8E 1E F3 41 51 89 45 A5 F0"
        // - 0x0AA seed 0xE1 raw "E1 3F 0E BB 7D C5 DD 9D 69 AC AC 28 97 B4 BE 92 C2 46 61 A0 7D C4 D2 9D 6D E0 A0 22 C3 B2 A4 96 D4 07 02 BA 38 DF 9C 85 6D E1 AC 68"
        // - 0x0AC seed 0xE3 raw "E3 C0 E8 4C 56 DE 92 DA 57 13 40 72 FB 2F 56 AC B9 FC E9 4D 13 C4 96 C8 12 52 05 6C FD 28 5D B9 B9 D7 D7 7A 56 D8 92 C1 57 13 4A 69"
        // - 0x0AD seed 0xE4 raw "E4 6A 61 07 9F 4D 95 3D 15 13 2F 44 6A AE 01 DC 5A 5F 6B 16 C3"
        // - 0x0B0 seed 0xE7 raw "E7 D0 1A E5 0E 0B 1A 8E 46 40 32 D0 95 1D 49 C9 F5 F7 10 F6 1F 0A 5E E3 42 46 71 82 D4 56 4E"
        // - 0x0B1 seed 0xE8 raw "E8 65 89 4F B4 B2 10 A6 32 0B 83 17 81 88 1F F3 47 58 82 0E BE B7 07 E3 22 44 CE 14 9A 85 13 B2 5D 44 C9"
        // - 0xBDF seed 0x16 raw "16 34 FF F8 4A B0 1F 19 EA 9B EC BF 5C A5 D1 97 E9 09 F4 B9 45 B9 09 4A FF 93 A9 E2 19 9B D9 D2 FC 12 F4 B9 4D B2 0E 5C EC D4 B8 A4 5C EB C0 C4 F8 13 B6 EA 08 B2 1B 54 FB D4 AE A9 5F A4 C7 D2 BD 16 F0 EB 46 B5 14 5E B0"
        // - 0xBE0 seed 0x17 raw "17 9A 4C 47 23 98 83 12 4F 87 F0 F6 14 B7 03 0E 49 E3 41 57 34 D6 D4 73 48 8A FA FD 73 E4 0D 1A 56 AF 5A 12 22 DD 9A 07 13"
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
            text = stringPoolId switch
            {
                BlockAccessSuccessStringPoolId => BlockAccessSuccessResolved,
                UnblockAccessSuccessStringPoolId => UnblockAccessSuccessResolved,
                RemoveNameFromRanksStringPoolId => RemoveNameFromRanksResolved,
                InvalidCharacterNameStringPoolId => InvalidCharacterNameResolved,
                WrongNpcNameStringPoolId => WrongNpcNameResolved,
                RequestFailedStringPoolId => RequestFailedResolved,
                HiredMerchantLocatedFormatStringPoolId => HiredMerchantLocatedFormatResolved,
                HiredMerchantNotFoundStringPoolId => HiredMerchantNotFoundResolved,
                WarningMessageNotEnteredStringPoolId => WarningMessageNotEnteredResolved,
                WarningSentStringPoolId => WarningSentResolved,
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
