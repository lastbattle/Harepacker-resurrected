namespace HaCreator.MapSimulator.Interaction
{
    using System;
    using System.Globalization;

    internal static class AntiMacroOwnerStringPoolText
    {
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

        // `CUIAntiMacro::Draw` formats StringPool 0x1A15 (6677) with a single integer argument.
        private const string AttemptMessageFallback = "Attempt %d of 2";
        private const string NoticeUserNotFoundFallback = "The user cannot be found.";
        private const string NoticeTargetNotAttackingFallback = "You cannot use it on a user that isn't in the middle of attack.";
        private const string NoticeAlreadyTestedFallback = "This user has already been tested before.";
        private const string NoticeTargetAlreadyTestingFallback = "This user is currently going through the Lie Detector Test.";
        private const string NoticeRewardThanksFallback = "Thank you for cooperating with the Lie Detector Test. You'll be rewarded 5000 mesos for not botting.";
        private const string NoticeFailureRestrictionFallback = "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.";
        private const string ChatAdminLaunchFallback = "%s have used the Lie Detector Test.";
        private const string ChatScreenshotReportFallback = "%s_The screenshot has been saved. You have been notified of macro-assisted program monitoring.";
        private const string ChatAdminActivateFallback = "%s_The screenshot has been saved. The Lie Detector has been activated.";
        private const string ChatAdminPassedFallback = "%s_You have passed the Lie Detector Test.";
        private const string ChatAdminScreenshotSavedFallback = "%s_The screenshot has been saved. It appears that you may be using a macro-assisted program.";
        private const string NoticeUserFailedRewardFallback = "The user has failed the Lie Detector Test. You'll be rewarded 7000 mesos from the user.";
        private const string NoticePassedThanksFallback = "You have succesfully passed the Lie Detector Test. Thank you for participating!";
        private const string NoticeMacroSanctionFallback = "You will be sanctioned for using a macro-assisted program.";
        private const string NoticeAdminThanksFallback = "Thank you for your cooperation.";

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

        public static string FormatAttemptMessage(int attemptNumber, bool appendFallbackSuffix = false)
        {
            int resolvedAttempt = Math.Clamp(attemptNumber, 1, 2);
            string format = GetAttemptMessageFormat(appendFallbackSuffix);

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
            text = stringPoolId switch
            {
                NoticeUserNotFoundStringPoolId => NoticeUserNotFoundFallback,
                NoticeTargetNotAttackingStringPoolId => NoticeTargetNotAttackingFallback,
                NoticeAlreadyTestedStringPoolId => NoticeAlreadyTestedFallback,
                NoticeTargetAlreadyTestingStringPoolId => NoticeTargetAlreadyTestingFallback,
                NoticeRewardThanksStringPoolId => NoticeRewardThanksFallback,
                NoticeFailureRestrictionStringPoolId => NoticeFailureRestrictionFallback,
                ChatAdminLaunchStringPoolId => ChatAdminLaunchFallback,
                ChatScreenshotReportStringPoolId => ChatScreenshotReportFallback,
                ChatAdminActivateStringPoolId => ChatAdminActivateFallback,
                ChatAdminPassedStringPoolId => ChatAdminPassedFallback,
                ChatAdminScreenshotSavedStringPoolId => ChatAdminScreenshotSavedFallback,
                NoticeUserFailedRewardStringPoolId => NoticeUserFailedRewardFallback,
                NoticePassedThanksStringPoolId => NoticePassedThanksFallback,
                NoticeMacroSanctionStringPoolId => NoticeMacroSanctionFallback,
                NoticeAdminThanksStringPoolId => NoticeAdminThanksFallback,
                AttemptMessageStringPoolId => AttemptMessageFallback,
                _ => null,
            };

            return text != null;
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
