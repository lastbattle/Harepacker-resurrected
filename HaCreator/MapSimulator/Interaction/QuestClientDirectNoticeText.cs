namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientDirectNoticeText
    {
        internal const int PartyQuestInvitationStringPoolId = 0xCDA;
        internal const int MedalQuestNoticeStringPoolId = 0x1A8B;
        internal const int RewardBlockedByEquippedItemStringPoolId = 0xD97;
        internal const int RewardBlockedByUniqueItemStringPoolId = 0xD98;
        private const string PartyQuestInvitationNotice = "The quest has ended\r\ndue to an unknown error.";
        private const string MedalQuestNotice = "You do not have enough mesos.";
        private const string RewardBlockedByEquippedItemNotice = "Unable to retrieve it due to the equipment\r\n currently being worn by the character.";
        private const string RewardBlockedByUniqueItemNotice = "You may not possess more than \r\none of this item.";

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

            text = stringPoolId switch
            {
                PartyQuestInvitationStringPoolId => PartyQuestInvitationNotice,
                MedalQuestNoticeStringPoolId => MedalQuestNotice,
                RewardBlockedByEquippedItemStringPoolId => RewardBlockedByEquippedItemNotice,
                RewardBlockedByUniqueItemStringPoolId => RewardBlockedByUniqueItemNotice,
                _ => null
            };

            return text != null;
        }
    }
}
