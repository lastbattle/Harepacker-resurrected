namespace HaCreator.MapSimulator.Interaction
{
    internal static class EngagementProposalDialogText
    {
        internal const int WaitForResponseStringPoolId = 0x1093;
        internal const int IncomingRequestPromptStringPoolId = 0x109B;
        internal const int AcceptedStringPoolId = 0x109D;
        internal const int AcceptedMarriageStringPoolId = 0x109E;
        internal const int DeclinedRequestStringPoolId = 0x109F;
        internal const int BrokenEngagementStringPoolId = 0x10A0;
        internal const int NoLongerMarriedStringPoolId = 0x10A1;
        internal const int WrongCharacterNameStringPoolId = 0x10A2;
        internal const int PartnerSameMapStringPoolId = 0x10A3;
        internal const int PartnerEtcSlotFullStringPoolId = 0x10A4;
        internal const int SameGenderStringPoolId = 0x10A5;
        internal const int AlreadyEngagedStringPoolId = 0x10A6;
        internal const int AlreadyMarriedStringPoolId = 0x10A7;
        internal const int PartnerAlreadyEngagedStringPoolId = 0x10A8;
        internal const int PartnerAlreadyMarriedStringPoolId = 0x10A9;
        internal const int RequesterBusyStringPoolId = 0x10AA;
        internal const int PartnerBusyStringPoolId = 0x10AB;
        internal const int WithdrawnRequestStringPoolId = 0x10AC;
        internal const int ReservationLockedBreakStringPoolId = 0x10AD;
        internal const int ReservationCanceledStringPoolId = 0x10AE;
        internal const int InvitationInvalidStringPoolId = 0x10AF;
        internal const int ReservationSuccessStringPoolId = 0x10B0;
        internal const int EtcSlotFullStringPoolId = 0x10B1;
        internal const int EnterPartnerNameStringPoolId = 0x10B2;
        internal const byte ResultSubtypeEngaged = 11;
        internal const byte ResultSubtypeMarried = 12;
        internal const byte ResultSubtypeEngagementBroken = 13;
        internal const byte ResultSubtypeNoLongerMarried = 14;
        internal const byte ResultSubtypeReservationSuccess = 16;
        internal const byte ResultSubtypeWrongCharacterName = 18;
        internal const byte ResultSubtypePartnerSameMap = 19;
        internal const byte ResultSubtypeRequesterEtcSlotFull = 20;
        internal const byte ResultSubtypePartnerEtcSlotFull = 21;
        internal const byte ResultSubtypeSameGender = 22;
        internal const byte ResultSubtypeAlreadyEngaged = 23;
        internal const byte ResultSubtypePartnerAlreadyEngaged = 24;
        internal const byte ResultSubtypeAlreadyMarried = 25;
        internal const byte ResultSubtypePartnerAlreadyMarried = 26;
        internal const byte ResultSubtypeRequesterBusy = 27;
        internal const byte ResultSubtypePartnerBusy = 28;
        internal const byte ResultSubtypeWithdrawnRequest = 29;
        internal const byte ResultSubtypeDeclined = 30;
        internal const byte ResultSubtypeReservationCanceled = 31;
        internal const byte ResultSubtypeReservationLockedBreak = 32;
        internal const byte ResultSubtypeInvitationInvalid = 34;

        private const string WaitForResponseFallback = "Waiting for her response...";
        private const string IncomingRequestPromptFallback = "%s has requested engagement.\r\nWill you accept this proposal?";
        private const string AcceptedFallback = "You are now engaged.";
        private const string AcceptedMarriageFallback = "You are now married!";
        private const string DeclinedRequestFallback = "She has politely declined your engagement request.";
        private const string BrokenEngagementFallback = "Your engagement has been broken.";
        private const string NoLongerMarriedFallback = "You are no longer married.";
        private const string WrongCharacterNameFallback = "You have entered the wrong character name.";
        private const string PartnerSameMapFallback = "Your partner has to be in the same map.";
        private const string PartnerEtcSlotFullFallback = "Your partner's ETC slots are full.";
        private const string SameGenderFallback = "You cannot be engaged to the same gender.";
        private const string AlreadyEngagedFallback = "You are already engaged.";
        private const string AlreadyMarriedFallback = "You are already married.";
        private const string PartnerAlreadyEngagedFallback = "She is already engaged.";
        private const string PartnerAlreadyMarriedFallback = "This person is already married.";
        private const string RequesterBusyFallback = "You're already in middle or proposing a person.";
        private const string PartnerBusyFallback = "She is currently being asked by another suitor.";
        private const string WithdrawnRequestFallback = "Unfortunately, the man who proposed to you has withdrawn his request for an engagement.";
        private const string ReservationLockedBreakFallback = "You can't break the engagement after making reservations.";
        private const string ReservationCanceledFallback = "The reservation has been canceled. Please try again later.";
        private const string InvitationInvalidFallback = "This invitation is not valid.";
        private const string ReservationSuccessFallback = "Congratulations!\r\nYour reservation was successfully made!";
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

        internal static string GetAcceptedMarriageText()
        {
            return MapleStoryStringPool.GetOrFallback(AcceptedMarriageStringPoolId, AcceptedMarriageFallback);
        }

        internal static string GetDeclinedRequestText()
        {
            return MapleStoryStringPool.GetOrFallback(DeclinedRequestStringPoolId, DeclinedRequestFallback);
        }

        internal static string GetBrokenEngagementText()
        {
            return MapleStoryStringPool.GetOrFallback(BrokenEngagementStringPoolId, BrokenEngagementFallback);
        }

        internal static string GetNoLongerMarriedText()
        {
            return MapleStoryStringPool.GetOrFallback(NoLongerMarriedStringPoolId, NoLongerMarriedFallback);
        }

        internal static string GetWrongCharacterNameText()
        {
            return MapleStoryStringPool.GetOrFallback(WrongCharacterNameStringPoolId, WrongCharacterNameFallback);
        }

        internal static string GetPartnerSameMapText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerSameMapStringPoolId, PartnerSameMapFallback);
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

        internal static string GetAlreadyMarriedText()
        {
            return MapleStoryStringPool.GetOrFallback(AlreadyMarriedStringPoolId, AlreadyMarriedFallback);
        }

        internal static string GetPartnerAlreadyEngagedText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerAlreadyEngagedStringPoolId, PartnerAlreadyEngagedFallback);
        }

        internal static string GetPartnerAlreadyMarriedText()
        {
            return MapleStoryStringPool.GetOrFallback(PartnerAlreadyMarriedStringPoolId, PartnerAlreadyMarriedFallback);
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

        internal static string GetReservationLockedBreakText()
        {
            return MapleStoryStringPool.GetOrFallback(ReservationLockedBreakStringPoolId, ReservationLockedBreakFallback);
        }

        internal static string GetReservationCanceledText()
        {
            return MapleStoryStringPool.GetOrFallback(ReservationCanceledStringPoolId, ReservationCanceledFallback);
        }

        internal static string GetInvitationInvalidText()
        {
            return MapleStoryStringPool.GetOrFallback(InvitationInvalidStringPoolId, InvitationInvalidFallback);
        }

        internal static string GetReservationSuccessText()
        {
            return MapleStoryStringPool.GetOrFallback(ReservationSuccessStringPoolId, ReservationSuccessFallback);
        }

        internal static string GetEnterPartnerNameText()
        {
            return MapleStoryStringPool.GetOrFallback(EnterPartnerNameStringPoolId, EnterPartnerNameFallback);
        }

        internal static bool TryGetMarriageResultNotice(byte subtype, string serverText, out string notice)
        {
            switch (subtype)
            {
                case ResultSubtypeEngaged:
                    notice = GetAcceptedText();
                    return true;
                case ResultSubtypeMarried:
                    notice = GetAcceptedMarriageText();
                    return true;
                case ResultSubtypeEngagementBroken:
                    notice = GetBrokenEngagementText();
                    return true;
                case ResultSubtypeNoLongerMarried:
                    notice = GetNoLongerMarriedText();
                    return true;
                case ResultSubtypeReservationSuccess:
                    notice = GetReservationSuccessText();
                    return true;
                case ResultSubtypeWrongCharacterName:
                    notice = GetWrongCharacterNameText();
                    return true;
                case ResultSubtypePartnerSameMap:
                    notice = GetPartnerSameMapText();
                    return true;
                case ResultSubtypeRequesterEtcSlotFull:
                    notice = GetEtcSlotFullText();
                    return true;
                case ResultSubtypePartnerEtcSlotFull:
                    notice = GetPartnerEtcSlotFullText();
                    return true;
                case ResultSubtypeSameGender:
                    notice = GetSameGenderText();
                    return true;
                case ResultSubtypeAlreadyEngaged:
                    notice = GetAlreadyEngagedText();
                    return true;
                case ResultSubtypePartnerAlreadyEngaged:
                    notice = GetPartnerAlreadyEngagedText();
                    return true;
                case ResultSubtypeAlreadyMarried:
                    notice = GetAlreadyMarriedText();
                    return true;
                case ResultSubtypePartnerAlreadyMarried:
                    notice = GetPartnerAlreadyMarriedText();
                    return true;
                case ResultSubtypeRequesterBusy:
                    notice = GetRequesterBusyText();
                    return true;
                case ResultSubtypePartnerBusy:
                    notice = GetPartnerBusyText();
                    return true;
                case ResultSubtypeWithdrawnRequest:
                    notice = GetWithdrawnRequestText();
                    return true;
                case ResultSubtypeDeclined:
                    notice = GetDeclinedRequestText();
                    return true;
                case ResultSubtypeReservationCanceled:
                    notice = GetReservationCanceledText();
                    return true;
                case ResultSubtypeReservationLockedBreak:
                    notice = GetReservationLockedBreakText();
                    return true;
                case ResultSubtypeInvitationInvalid:
                    notice = GetInvitationInvalidText();
                    return true;
                case 36:
                    notice = string.IsNullOrWhiteSpace(serverText) ? string.Empty : serverText.Trim();
                    return !string.IsNullOrWhiteSpace(notice);
                default:
                    notice = null;
                    return false;
            }
        }
    }
}
