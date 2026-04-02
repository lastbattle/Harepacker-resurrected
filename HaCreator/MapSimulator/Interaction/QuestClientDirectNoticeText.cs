namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientDirectNoticeText
    {
        internal const int PartyQuestInvitationStringPoolId = 0xCDA;
        internal const int MedalQuestNoticeStringPoolId = 0x1A8B;
        internal const int QuestTimerWarningStringPoolId = 0xD97;
        internal const int QuestTimerExpiredStringPoolId = 0xD98;

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
                PartyQuestInvitationStringPoolId => "Client quest-result notice branch (party-quest invitation text unresolved).",
                MedalQuestNoticeStringPoolId => "Client quest-result notice branch (medal quest text unresolved).",
                QuestTimerWarningStringPoolId => "Client quest-result notice branch (quest timer warning text unresolved).",
                QuestTimerExpiredStringPoolId => "Client quest-result notice branch (quest timer expiry text unresolved).",
                _ => null
            };

            return text != null;
        }
    }
}
