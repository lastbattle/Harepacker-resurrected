namespace HaCreator.MapSimulator.Interaction
{
    internal static class EngagementProposalDialogText
    {
        internal const int WaitForResponseStringPoolId = 0x1093;
        internal const int IncomingRequestPromptStringPoolId = 0x109B;
        internal const int AcceptedStringPoolId = 0x109D;
        internal const int PartnerEtcSlotFullStringPoolId = 0x10A4;
        internal const int SameGenderStringPoolId = 0x10A5;
        internal const int AlreadyEngagedStringPoolId = 0x10A6;
        internal const int PartnerAlreadyEngagedStringPoolId = 0x10A8;
        internal const int RequesterBusyStringPoolId = 0x10AA;
        internal const int PartnerBusyStringPoolId = 0x10AB;
        internal const int WithdrawnRequestStringPoolId = 0x10AC;
        internal const int EtcSlotFullStringPoolId = 0x10B1;
        internal const int EnterPartnerNameStringPoolId = 0x10B2;

        private const string WaitForResponseFallback = "Waiting for her response...";
        private const string IncomingRequestPromptFallback = "%s has requested engagement.\r\nWill you accept this proposal?";
        private const string AcceptedFallback = "You are now engaged.";
        private const string PartnerEtcSlotFullFallback = "Your partner's ETC slots are full.";
        private const string SameGenderFallback = "You cannot be engaged to the same gender.";
        private const string AlreadyEngagedFallback = "You are already engaged.";
        private const string PartnerAlreadyEngagedFallback = "She is already engaged.";
        private const string RequesterBusyFallback = "You're already in middle or proposing a person.";
        private const string PartnerBusyFallback = "She is currently being asked by another suitor.";
        private const string WithdrawnRequestFallback = "Unfortunately, the man who proposed to you has withdrawn his request for an engagement.";
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

        internal static string GetAcceptedText()
        {
            return MapleStoryStringPool.GetOrFallback(AcceptedStringPoolId, AcceptedFallback);
        }

        internal static string GetPartnerEtcSlotFullText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerEtcSlotFullStringPoolId, PartnerEtcSlotFullFallback);
        }

        internal static string GetSameGenderText()
        {
            return MapleStoryStringPool.GetOrFallback(SameGenderStringPoolId, SameGenderFallback);
        }

        internal static string GetAlreadyEngagedText()
        {
            return MapleStoryStringPool.GetOrFallback(AlreadyEngagedStringPoolId, AlreadyEngagedFallback);
        }

        internal static string GetPartnerAlreadyEngagedText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerAlreadyEngagedStringPoolId, PartnerAlreadyEngagedFallback);
        }

        internal static string GetRequesterBusyText()
        {
            return MapleStoryStringPool.GetOrFallback(RequesterBusyStringPoolId, RequesterBusyFallback);
        }

        internal static string GetPartnerBusyText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerBusyStringPoolId, PartnerBusyFallback);
        }

        internal static string GetWithdrawnRequestText()
        {
            return MapleStoryStringPool.GetOrFallback(WithdrawnRequestStringPoolId, WithdrawnRequestFallback);
        }

        internal static string GetEnterPartnerNameText()
        {
            return MapleStoryStringPool.GetOrFallback(EnterPartnerNameStringPoolId, EnterPartnerNameFallback);
        }
    }
}
