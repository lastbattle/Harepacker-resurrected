namespace HaCreator.MapSimulator.Interaction
{
    using System;
    using System.Globalization;
    using System.Collections.Generic;

    internal static class AntiMacroOwnerStringPoolText
    {
        private readonly record struct StringPoolEntryEvidence(
            int Id,
            byte Seed,
            string RawHex,
            string DecodedText,
            string ClientSource);

        internal const int AttemptMessageStringPoolId = 6677;
        internal const int EditControlFontStringPoolId = 0x1A25;
        internal const int NoticeUserNotFoundStringPoolId = 0xC84;
        internal const int NoticeTargetNotAttackingStringPoolId = 0xC85;
        internal const int NoticeAlreadyTestedStringPoolId = 0xC86;
        internal const int NoticeTargetAlreadyTestingStringPoolId = 0xC87;
        internal const int NoticeRewardThanksStringPoolId = 0xC88;
        internal const int NoticeFailureRestrictionStringPoolId = 0xC89;
        internal const int ChatAdminLaunchStringPoolId = 0xC8D;
        internal const int ChatScreenshotReportStringPoolId = 0xC8E;
        internal const int ChatAdminActivateStringPoolId = 0xC8F;
        internal const int ChatAdminPassedStringPoolId = 0xC90;
        internal const int ChatAdminScreenshotSavedStringPoolId = 0xC91;
        internal const int NoticeUserFailedRewardStringPoolId = 0xC98;
        internal const int NoticePassedThanksStringPoolId = 0xC99;
        internal const int NoticeMacroSanctionStringPoolId = 0xC9A;
        internal const int NoticeAdminThanksStringPoolId = 0x1A65;

        // Recovered from MapleStory.exe v95 StringPool::ms_aString together with the
        // `StringPool::GetString` decode path (`ms_aString` at 0xC5A878, `ms_aKey` at 0xB98830).
        private static readonly IReadOnlyDictionary<int, StringPoolEntryEvidence> RecoveredEntries = new Dictionary<int, StringPoolEntryEvidence>
        {
            [NoticeUserNotFoundStringPoolId] = new(NoticeUserNotFoundStringPoolId, 0xBB, "BB DB 2F 56 FE EC EA E2 4B 56 D5 92 C2 5C 5C 51 3B ED 22 13 B8 F6 EC E9 5D 58", "The user cannot be found.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeTargetNotAttackingStringPoolId] = new(NoticeTargetNotAttackingStringPoolId, 0xBC, "BC 47 E1 12 9D 50 52 60 1C 82 19 C7 2D 17 03 6A 5E 6A AE 08 D3 13 52 2E 07 9E 08 95 78 10 0E 2B 43 3E E7 14 D3 14 47 2E 1B 83 4D 93 30 01 46 27 5E 7A EA 0B D8 13 5C 68 52 8C 19 93 39 07 0D 64", "You cannot use it on a user that isn't in the middle of attack.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeAlreadyTestedStringPoolId] = new(NoticeAlreadyTestedStringPoolId, 0xBD, "BD 69 74 A6 09 46 13 6F 80 A8 FB A6 D1 BB EC F5 02 4F 79 AE 1E 1F 46 7E 80 BF B5 EE C4 AD BF E0 0B 59 3C AD 1F 66 09 6E 80 F4", "This user has already been tested before.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeTargetAlreadyTestingStringPoolId] = new(NoticeTargetAlreadyTestingStringPoolId, 0xBE, "BE 2E 51 F7 87 EC B9 4A AE C7 97 F4 12 B1 FA 5D AE 08 5C F0 80 A0 B5 19 AC DA DE F3 06 B1 ED 40 AE 15 4C F9 9C EC B8 51 AE 95 FB F4 04 B1 DD 4D A8 1F 5A EA 9B BE EC 6D AE C6 C3 B3", "This user is currently going through the Lie Detector Test.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeRewardThanksStringPoolId] = new(NoticeRewardThanksStringPoolId, 0xBF, "BF A0 1B 5C 87 F2 B8 0A F8 1E 4F 5C AC 51 12 32 D7 9B 03 58 9B F8 EC 1A F9 0C 4F 4D AA 57 5A 71 CC 9C 16 1D A5 F0 FD 53 D3 0E 1B 5F A0 57 5D 23 98 A0 16 4E 9D B7 B8 2A F8 1E 48 56 AF 03 50 34 98 86 16 4A 88 EB FC 16 F3 4B 5A 0A F3 13 12 3C DD 87 1C 4E C9 FF F7 01 B7 05 6F 4E E3 41 5D 25 CC 9D 1D 5A C7", "Thank you for cooperating with the Lie Detector Test. You'll be rewarded 5000 mesos for not botting.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeFailureRestrictionStringPoolId] = new(NoticeFailureRestrictionStringPoolId, 0xC0, "C0 BC 8E 1E F3 7F 59 82 0E 92 BB 01 E3 25 10 CC 03 C8 B2 1E A0 47 10 84 41 B8 B8 1C F4 2B 17 83 05 80 87 0F F3 4A 5F 92 0E BE BF 03 E3 66 06 C6 14 86 C6 19 BC 47 44 8E 40 B1 F0 55 D4 23 14 C6 10 9C 83 1F F3 55 51 8E 42 A3 AC 10 A6 29 02 83 05 80 83 5B A7 56 43 93 0E A1 B7 19 EA 66 16 C6 02 9D 8A 0F F3 5A 5E C7 49 B7 B3 10 A6 34 01 D0 05 9A 8F 18 A7 5A 5F 89 5D F8", "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [ChatAdminLaunchStringPoolId] = new(ChatAdminLaunchStringPoolId, 0xC4, "C4 AB 14 9D 5B 52 78 17 CD 18 94 3D 64 46 3E 5F 7B AE 2B D4 56 13 4A 17 99 08 84 2C 0B 14 6A 63 7B FD 13 93", "%s have used the Lie Detector Test.", "CWvsContext::OnAntiMacroResult mode 5 / MapleStory.exe v95 StringPool::GetString"),
            [ChatScreenshotReportStringPoolId] = new(ChatScreenshotReportStringPoolId, 0xC5, "C5 39 BC 25 32 0E 79 C5 A9 B8 BC D5 AD A2 E7 06 52 68 EF 12 07 15 3C 87 BF BE A0 90 BB AD E2 0B 59 32 EF 23 09 13 3C 8D BB AD AB 90 AA A9 F1 6E 1D 72 A0 0E 0F 66 75 80 BE FB A1 D6 E8 A1 F5 0D 4F 73 E2 1B 15 15 75 96 AE BE AA 90 B8 BE FB 09 4F 7D A2 5A 0B 09 72 8C AE B4 BC D9 A6 AB BA", "%s_The screenshot has been saved. You have been notified of macro-assisted program monitoring.", "CWvsContext::OnAntiMacroResult mode 4 / MapleStory.exe v95 StringPool::GetString"),
            [ChatAdminActivateStringPoolId] = new(ChatAdminActivateStringPoolId, 0xC6, "C6 1C ED AB 98 A4 5C EB C6 D4 EF 04 F4 F7 5B B4 15 4D BE 9C AD BF 19 A9 D0 D2 F3 41 E2 F8 5E B9 1E 17 BE A0 A4 A9 19 87 DC D2 BD 25 F4 ED 4D BF 0E 56 EC D4 A4 AD 4A EB D7 D2 F8 0F B1 F8 4B A8 13 4F FF 80 A9 A8 17", "%s_The screenshot has been saved. The Lie Detector has been activated.", "CWvsContext::OnAntiMacroResult mode 5 / MapleStory.exe v95 StringPool::GetString"),
            [ChatAdminPassedStringPoolId] = new(ChatAdminPassedStringPoolId, 0xC7, "C7 56 4E B6 C0 F7 06 B7 03 0E 4C A6 03 42 30 CB 87 16 59 C9 ED F0 16 B7 27 06 5F E3 67 57 25 DD 97 07 52 9B B9 CC 16 E4 1F 41", "%s_You have passed the Lie Detector Test.", "CWvsContext::OnAntiMacroResult mode 10 / MapleStory.exe v95 StringPool::GetString"),
            [ChatAdminScreenshotSavedStringPoolId] = new(ChatAdminScreenshotSavedStringPoolId, 0xC8, "C8 C3 08 8C 67 58 82 0E A5 BD 07 E3 23 0A D0 19 87 92 5B BB 52 43 C7 4C B3 BB 1B A6 35 05 D5 14 8C C8 5B 9A 47 10 86 5E A6 BB 14 F4 35 44 D7 19 89 92 5B AA 5C 45 C7 43 B7 A7 55 E4 23 44 D6 02 81 88 1C F3 52 10 8A 4F B5 AC 1A AB 27 17 D0 18 9B 92 1E B7 13 40 95 41 B1 AC 14 EB 68", "%s_The screenshot has been saved. It appears that you may be using a macro-assisted program.", "CWvsContext::OnAntiMacroResult mode 8 / MapleStory.exe v95 StringPool::GetString"),
            [NoticeUserFailedRewardStringPoolId] = new(NoticeUserFailedRewardStringPoolId, 0xCF, "CF 69 81 FC B8 06 E4 0E 1D 1A AB 42 41 71 DE 95 1A 51 8C FD B8 07 FF 0E 4F 76 AA 46 12 15 DD 80 16 5E 9D F6 EA 53 C3 0E 1C 4E ED 03 6B 3E CD D3 1F 51 C9 FB FD 53 E5 0E 18 5B B1 47 57 35 98 C3 43 0D D9 B9 F5 16 E4 04 1C 1A A5 51 5D 3C 98 80 1B 58 C9 EC EB 16 E5 45", "The user has failed the Lie Detector Test. You'll be rewarded 7000 mesos from the user.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticePassedThanksStringPoolId] = new(NoticePassedThanksStringPoolId, 0xD0, "D0 22 BC 46 10 8F 4F A0 BB 55 F5 33 07 C0 14 9B 80 0E BF 5F 49 C7 5E B7 AD 06 E3 22 44 D7 19 8D C6 37 BA 56 10 A3 4B A2 BB 16 F2 29 16 83 25 8D 95 0F FD 13 64 8F 4F B8 B5 55 FF 29 11 83 17 87 94 5B A3 52 42 93 47 B5 B7 05 E7 32 0D CD 16 C9", "You have succesfully passed the Lie Detector Test. Thank you for participating!", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeMacroSanctionStringPoolId] = new(NoticeMacroSanctionStringPoolId, 0xD1, "D1 AE C9 13 41 B9 34 C1 D0 CB 6E E9 E9 35 82 BF AF 83 CF 09 0F AB 39 8D DA 84 7E AC BC 35 8A BF AB D7 C7 46 0C AF 3E DF D3 C6 6D FF BA 2F 90 A5 A9 93 86 16 13 A1 3A DF DD 86 22", "You will be sanctioned for using a macro-assisted program.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
            [NoticeAdminThanksStringPoolId] = new(NoticeAdminThanksStringPoolId, 0x9B, "9B 66 5B 44 75 E4 67 4A B1 EC B9 E1 56 04 96 8A C3 47 41 05 78 E0 28 43 BB EB F8 F3 50 19 D8 DD", "Thank you for your cooperation.", "CWvsContext::ShowAntiMacroNotice / MapleStory.exe v95 StringPool::GetString"),
        };

        // `CUIAntiMacro::Draw` formats StringPool 0x1A15 (6677) with a single integer argument,
        // but the localized entry recovered in this workspace is only `%d`, so the owner still
        // keeps the established simulator fallback wording around the decoded attempt count.
        private const string AttemptMessageFallback = "Attempt %d of 2";

        public static string GetAttemptMessageFormat(bool appendFallbackSuffix = true)
        {
            return GetResolvedOrFallback(AttemptMessageStringPoolId, AttemptMessageFallback, appendFallbackSuffix);
        }

        public static string FormatAttemptMessageFromClientCounter(int answerCount, bool appendFallbackSuffix = false)
        {
            // Client draw path emits `!m_bRetry + 1`. In packet payload terms this maps to
            // first-try counters (>1) => attempt 1, retry counters (<=1) => attempt 2.
            int attemptNumber = answerCount > 1 ? 1 : 2;
            return FormatAttemptMessage(attemptNumber, appendFallbackSuffix);
        }

        public static string FormatAttemptMessageFromClientCounter(int answerCount, string format)
        {
            int attemptNumber = answerCount > 1 ? 1 : 2;
            return FormatAttemptMessage(format, attemptNumber);
        }

        public static string FormatAttemptMessage(int attemptNumber, bool appendFallbackSuffix = false)
        {
            int resolvedAttempt = Math.Clamp(attemptNumber, 1, 2);
            string format = GetAttemptMessageFormat(appendFallbackSuffix);
            return FormatAttemptMessage(format, resolvedAttempt);
        }

        public static string FormatAttemptMessage(string format, int attemptNumber)
        {
            int resolvedAttempt = Math.Clamp(attemptNumber, 1, 2);
            format ??= AttemptMessageFallback;

            // Client format strings are `%d`-style. Keep compatibility with existing `{0}` fallback
            // and other accidental variants so the window remains stable across data sets.
            if (format.Contains("%d", StringComparison.Ordinal))
            {
                return format.Replace("%d", resolvedAttempt.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            if (format.Contains("%s", StringComparison.Ordinal))
            {
                return format.Replace("%s", resolvedAttempt.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, resolvedAttempt);
            }
            catch
            {
                return string.Format(CultureInfo.InvariantCulture, AttemptMessageFallback.Replace("%d", "{0}", StringComparison.Ordinal), resolvedAttempt);
            }
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = AttemptMessageStringPoolId == stringPoolId
                ? AttemptMessageFallback
                : RecoveredEntries.TryGetValue(stringPoolId, out StringPoolEntryEvidence evidence)
                    ? evidence.DecodedText
                    : null;

            return text != null;
        }

        public static bool TryGetEvidence(int stringPoolId, out string rawHex, out byte seed, out string clientSource)
        {
            if (RecoveredEntries.TryGetValue(stringPoolId, out StringPoolEntryEvidence evidence))
            {
                rawHex = evidence.RawHex;
                seed = evidence.Seed;
                clientSource = evidence.ClientSource;
                return true;
            }

            rawHex = null;
            seed = 0;
            clientSource = null;
            return false;
        }

        public static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix = false)
        {
            if (TryResolve(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} (StringPool 0x{stringPoolId:X} fallback)";
        }

    }
}
