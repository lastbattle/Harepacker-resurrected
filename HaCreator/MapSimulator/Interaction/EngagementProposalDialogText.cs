namespace HaCreator.MapSimulator.Interaction
{
    internal static class EngagementProposalDialogText
    {
        internal const int WaitForResponseStringPoolId = 0x1093;
        internal const int IncomingRequestPromptStringPoolId = 0x109B;
        internal const int EtcSlotFullStringPoolId = 0x10B1;
        internal const int EnterPartnerNameStringPoolId = 0x10B2;

        private const string WaitForResponseFallback = "Waiting for her response...";
        private const string IncomingRequestPromptFallback = "%s has requested engagement.\r\nWill you accept this proposal?";
        private const string EtcSlotFullFallback = "Your ETC slot is full.\r\nPlease remove some items.";
        private const string EnterPartnerNameFallback = "Please enter your partner's name.";

        internal static string GetWaitForResponseText()
        {
            return MapleStoryStringPool.GetOrFallback(WaitForResponseStringPoolId, WaitForResponseFallback);
        }

        internal static string FormatIncomingRequestPrompt(string proposerName)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                IncomingRequestPromptStringPoolId,
                IncomingRequestPromptFallback,
                1,
                out _);
            return string.Format(format, string.IsNullOrWhiteSpace(proposerName) ? "Someone" : proposerName.Trim());
        }

        internal static string GetEtcSlotFullText()
        {
            return MapleStoryStringPool.GetOrFallback(EtcSlotFullStringPoolId, EtcSlotFullFallback);
        }

        internal static string GetEnterPartnerNameText()
        {
            return MapleStoryStringPool.GetOrFallback(EnterPartnerNameStringPoolId, EnterPartnerNameFallback);
        }
    }
}
