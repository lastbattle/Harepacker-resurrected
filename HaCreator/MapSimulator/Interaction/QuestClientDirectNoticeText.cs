namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientDirectNoticeText
    {
        internal const int PartyQuestInvitationStringPoolId = 0xCDA;
        internal const int MedalQuestNoticeStringPoolId = 0x1A8B;
        internal const int RewardBlockedByEquippedItemStringPoolId = 0xD97;
        internal const int RewardBlockedByUniqueItemStringPoolId = 0xD98;
        public static bool TryResolve(int resultType, out string text, out int stringPoolId)
        {
            stringPoolId = resultType switch
            {
                11 => PartyQuestInvitationStringPoolId,
                13 => MedalQuestNoticeStringPoolId,
                15 => RewardBlockedByEquippedItemStringPoolId,
                16 => RewardBlockedByUniqueItemStringPoolId,
                _ => -1
            };

            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }
    }
}
