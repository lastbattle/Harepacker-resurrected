namespace HaCreator.MapSimulator.Interaction
{
    internal static class NpcInteractionPresentationProfile
    {
        internal static bool ShouldDrawEntryList(NpcInteractionPresentationStyle style, int entryCount)
        {
            return style switch
            {
                NpcInteractionPresentationStyle.PacketScriptUtilDialog => entryCount > 1,
                NpcInteractionPresentationStyle.PacketQuestResultUtilDialog => false,
                _ => true
            };
        }

        internal static bool ShouldDrawCloseButton(NpcInteractionPresentationStyle style)
        {
            return style != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog;
        }

        internal static bool ShouldCloseWhenClickingOutside(NpcInteractionPresentationStyle style)
        {
            return style != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog;
        }

        internal static bool ShouldDrawEntryHeader(NpcInteractionPresentationStyle style)
        {
            return style != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog;
        }

        internal static bool ShouldDrawPageIndicator(NpcInteractionPresentationStyle style)
        {
            return style != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog;
        }

        internal static bool ShouldDrawPrimaryButton(
            NpcInteractionPresentationStyle style,
            string primaryButtonText)
        {
            return style != NpcInteractionPresentationStyle.PacketQuestResultUtilDialog
                && !string.IsNullOrEmpty(primaryButtonText);
        }
    }
}
