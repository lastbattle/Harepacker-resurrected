namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientDirectNoticeText
    {
        internal const int PartyQuestInvitationStringPoolId = 0xCDA;
        internal const int MedalQuestNoticeStringPoolId = 0x1A8B;
        internal const int QuestTimerWarningStringPoolId = 0xD97;
        internal const int QuestTimerExpiredStringPoolId = 0xD98;
        private const string PartyQuestInvitationNotice = "A party quest invitation has arrived.";
        private const string MedalQuestNotice = "A medal quest notice has arrived.";
        private const string QuestTimerWarningNotice = "The quest timer is almost out.";
        private const string QuestTimerExpiredNotice = "The quest timer has expired.";

        public static bool TryResolve(int resultType, out string text, out int stringPoolId)
        {
            stringPoolId = resultType switch
            {
                11 => PartyQuestInvitationStringPoolId,
                13 => MedalQuestNoticeStringPoolId,
                15 => QuestTimerWarningStringPoolId,
                16 => QuestTimerExpiredStringPoolId,
                _ => -1
            };

            text = stringPoolId switch
            {
                PartyQuestInvitationStringPoolId => PartyQuestInvitationNotice,
                MedalQuestNoticeStringPoolId => MedalQuestNotice,
                QuestTimerWarningStringPoolId => QuestTimerWarningNotice,
                QuestTimerExpiredStringPoolId => QuestTimerExpiredNotice,
                _ => null
            };

            return text != null;
        }
    }
}
