using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal static partial class MapleStoryStringPool
    {
        private static readonly IReadOnlyDictionary<int, string> OverrideEntries = new Dictionary<int, string>
        {
            [0x1A15] = "%d",
            // Recovered from MapleStory.exe v95 StringPool::GetString. The generated table
            // in this workspace uses the decoded storage order, not the client key lookup,
            // so direct index resolution for these MapleTV result ids is incorrect.
            [0x0F9E] = "The message was successfully sent.",
            [0x0F9F] = "The waiting line is longer than an hour. \r\nPlease try using it at a later time.",
            [0x0FA0] = "You've entered the wrong user name.",
            // Recovered from MapleStory.exe v95 StringPool::ms_aString via StringPool::GetString
            // using ms_aKey (0xB98830). These anti-macro ids are still null in the generated
            // table for this workspace, but `CWvsContext::OnAntiMacroResult` and
            // `CWvsContext::ShowAntiMacroNotice` use them directly for the packet-owned
            // anti-macro controller and notice owner.
            [0x0C84] = "The user cannot be found.",
            [0x0C85] = "You cannot use it on a user that isn't in the middle of attack.",
            [0x0C86] = "This user has already been tested before.",
            [0x0C87] = "This user is currently going through the Lie Detector Test.",
            [0x0C88] = "Thank you for cooperating with the Lie Detector Test. You'll be rewarded 5000 mesos for not botting.",
            [0x0C89] = "The Lie Detector Test confirms that you have been botting. Repeated failure of the test will result in game restrictions.",
            [0x0C8D] = "%s have used the Lie Detector Test.",
            [0x0C8E] = "%s_The screenshot has been saved. You have been notified of macro-assisted program monitoring.",
            [0x0C8F] = "%s_The screenshot has been saved. The Lie Detector has been activated.",
            [0x0C90] = "%s_You have passed the Lie Detector Test.",
            [0x0C91] = "%s_The screenshot has been saved. It appears that you may be using a macro-assisted program.",
            [0x0C98] = "The user has failed the Lie Detector Test. You'll be rewarded 7000 mesos from the user.",
            [0x0C99] = "You have succesfully passed the Lie Detector Test. Thank you for participating!",
            [0x0C9A] = "You will be sanctioned for using a macro-assisted program.",
            [0x1A65] = "Thank you for your cooperation.",
            // Recovered from MapleStory.exe v95 StringPool::ms_aString via StringPool::GetString
            // using ms_aKey (0xB98830). These ids are radio-owner literals that were still null
            // in the generated table for this workspace, but the simulator now needs the exact
            // client text and path templates for CRadioManager parity.
            [0x14CF] = "[%s]'s broadcasting will begin. Please turn up the volume.",
            [0x14D0] = "[%s]'s broadcasting has ended.",
            [0x1501] = "Sound/Radio.img/%s",
            [0x1502] = "Sound/Radio.img/%s/track",
            // Recovered from MapleStory.exe v95 dragon-layer owners. `CDragon::CreateEffect`
            // resolves these ids through StringPool before loading the layer from Effect/BasicEff.
            [0x0B6B] = "Effect/BasicEff.img/dragonBlink",
            [0x15DA] = "Effect/BasicEff.img/dragonFury",
            // Recovered from MapleStory.exe v95 `CSetGuildMarkDlg::OnCreate`. The guild-mark
            // combo uses these client string ids rather than the raw WZ family node names.
            [0x0D14] = "Animal",
            [0x0D15] = "Plant",
            [0x0D16] = "Pattern",
            [0x0D17] = "Letter",
            [0x0D18] = "Etc",
            // Recovered from MapleStory.exe v95 `CUIFamily::Draw` and
            // `CUIFamilyChart::Draw` / `_DrawChartItem`. Keep these family ids
            // explicit here so the simulator follows the client wording even if
            // regenerated string-pool data drifts.
            [0x11FD] = "(You do not have family members.)",
            [0x1200] = "(You do not have a family yet.)",
            [0x1201] = "[Please add a Junior.]",
            [0x1202] = "%s Family",
            [0x1203] = "Senior(%d ppl.)",
            [0x1204] = "Junior(%d ppl.)",
            // Recovered from MapleStory.exe v95 `CUIQuestAlarm::Draw` and
            // `CUIQuestAlarm::OnButtonClicked`. Keep these quest-alarm ids explicit so the
            // owner retains the exact client title and notice strings even if regenerated
            // string-pool order drifts again.
            [0x0E4C] = "Quest Helper (%d/5)",
            [0x106F] = "[%s] It has been excluded from the auto alarm and it will not be automatically reigstered until you re log-on",
            [0x18EC] = "There are no quests in the quest helper.",
        };

        public static int Count => Entries.Length;

        public static bool Contains(int stringPoolId)
        {
            return (uint)stringPoolId < (uint)Entries.Length;
        }

        public static bool TryGet(int stringPoolId, out string text)
        {
            if (OverrideEntries.TryGetValue(stringPoolId, out text))
            {
                return true;
            }

            if (Contains(stringPoolId))
            {
                text = Entries[stringPoolId];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }
            }

            text = null;
            return false;
        }

        public static string GetOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix = false, int minimumHexWidth = 0)
        {
            if (TryGet(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} ({FormatFallbackLabel(stringPoolId, minimumHexWidth)} fallback)";
        }

        public static string GetCompositeFormatOrFallback(
            int stringPoolId,
            string fallbackFormat,
            int maxPlaceholderCount,
            out bool usedResolvedText)
        {
            if (TryGet(stringPoolId, out string resolvedFormat))
            {
                usedResolvedText = true;
                return ConvertPrintfFormatToCompositeFormat(resolvedFormat, maxPlaceholderCount);
            }

            usedResolvedText = false;
            return fallbackFormat;
        }

        public static string FormatFallbackLabel(int stringPoolId, int minimumHexWidth = 0)
        {
            string format = minimumHexWidth > 0 ? $"X{minimumHexWidth}" : "X";
            return $"StringPool 0x{stringPoolId.ToString(format)}";
        }

        private static string ConvertPrintfFormatToCompositeFormat(string format, int maxPlaceholderCount)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            int tokenIndex = 0;
            int searchStart = 0;
            while (tokenIndex < maxPlaceholderCount)
            {
                int markerIndex = FindNextPrintfPlaceholder(format, searchStart);
                if (markerIndex < 0)
                {
                    break;
                }

                string replacement = $"{{{tokenIndex}}}";
                format = format.Remove(markerIndex, 2).Insert(markerIndex, replacement);
                searchStart = markerIndex + replacement.Length;
                tokenIndex++;
            }

            return format;
        }

        private static int FindNextPrintfPlaceholder(string format, int searchStart)
        {
            int stringIndex = format.IndexOf("%s", searchStart, StringComparison.Ordinal);
            int digitIndex = format.IndexOf("%d", searchStart, StringComparison.Ordinal);

            if (stringIndex < 0)
            {
                return digitIndex;
            }

            if (digitIndex < 0)
            {
                return stringIndex;
            }

            return Math.Min(stringIndex, digitIndex);
        }
    }
}
