using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class MessengerClientParityText
    {
        internal const int InviteSentStringPoolId = 0x318;
        internal const int ContactNotFoundStringPoolId = 0x319;
        internal const int NotAcceptingChatStringPoolId = 0x31A;
        internal const int InviteDeniedStringPoolId = 0x31B;
        internal const int IncomingInviteNoticeStringPoolId = 0x31C;
        internal const int IncomingInvitePromptStringPoolId = 0x31D;
        internal const int HelpTitleStringPoolId = 0x330;
        internal const int HelpInviteStringPoolId = 0x331;
        internal const int HelpEndStringPoolId = 0x332;
        internal const int EnteredStringPoolId = 0x333;
        internal const int UnableToJoinInvitedRoomStringPoolId = 0x334;
        internal const int LeftRoomStringPoolId = 0x335;
        internal const int PromptUserNameStringPoolId = 0x336;
        internal const int InvalidCharacterNameStringPoolId = 0x337;
        internal const int TypingSuffixStringPoolId = 0x338;
        internal const int ExitChatRoomPromptStringPoolId = 0xE21;
        internal const int InvitePromptTitleStringPoolId = 0x538;
        internal const int ClaimUnderCoverStringPoolId = 0xD55;
        internal const int ClaimSucceededWithRemainingStringPoolId = 0xD56;
        internal const int ClaimSucceededNoRemainingStringPoolId = 0xD57;
        internal const int ClaimFailedStringPoolId = 0xD58;
        internal const int ClaimRejectedStringPoolId = 0xD59;
        internal const int ClaimBlockedStringPoolId = 0x1A5E;
        internal const int ClaimServerUnavailableStringPoolId = 0xD5B;
        internal const int ClaimTargetRejectedStringPoolId = 0xD5C;
        internal const int ClaimCategoryRejectedStringPoolId = 0xD5D;
        internal const int ClaimCooldownStringPoolId = 0xD5E;
        internal const int ClaimAvailableTimeStringPoolId = 0xD63;
        internal const int ClaimServerStateRejectedStringPoolId = 0xD65;

        private const string InviteSentFallback = "- You have sent the invite to '{0}'.";
        private const string ContactNotFoundFallback = "- '{0}' can't be found.";
        private const string NotAcceptingChatFallback = "'{0}' is currently not accepting chat.";
        private const string InviteDeniedFallback = "'{0}' denied the request.";
        private const string IncomingInviteNoticeFallback = "- '{0}' have requested you to join the chat.";
        private const string IncomingInvitePromptFallback = "'{0}' have requested you to \r\njoin the chat room.\r\nWill you accept it?";
        private const string HelpTitleFallback = "[ Maple Messenger Help ]";
        private const string HelpInviteFallback = "Invite : /invite character-name";
        private const string HelpEndFallback = "End : /q";
        private const string EnteredFallback = "- '{0}' has entered.";
        private const string UnableToJoinInvitedRoomFallback = "- You have been unable to join the invited chat room.";
        private const string LeftRoomFallback = "- '{0}' have left the room.";
        private const string PromptUserNameFallback = "- Please enter the user's name.";
        private const string InvalidCharacterNameFallback = "- The character name is invalid.";
        private const string TypingSuffixFallback = " is typing.";
        private const string ExitChatRoomPromptFallback = "Will you exit this chat room?";
        private const string InvitePromptTitleFallback = "Messenger";
        private const string ClaimUnderCoverFallback = "You cannot submit a claim while under cover.";
        private const string ClaimSucceededWithRemainingFallback = "Your report has been submitted. You may submit {0} more report(s) today.";
        private const string ClaimSucceededNoRemainingFallback = "Your report has been submitted. You cannot submit another report today.";
        private const string ClaimFailedFallback = "The report could not be submitted.";
        private const string ClaimRejectedFallback = "The report was rejected.";
        private const string ClaimBlockedFallback = "The report could not be submitted because the claim server blocked the request.";
        private const string ClaimServerUnavailableFallback = "The report could not be submitted because the claim server is unavailable.";
        private const string ClaimTargetRejectedFallback = "The report could not be submitted for the selected character.";
        private const string ClaimCategoryRejectedFallback = "The report could not be submitted for the selected report category.";
        private const string ClaimCooldownFallback = "The report could not be submitted yet. Please try again later.";
        private const string ClaimAvailableTimeFallback = "Reports can only be submitted between {0}:00 and {1}:00.";
        private const string ClaimServerStateRejectedFallback = "The report could not be submitted because the claim server is not accepting requests.";

        public static string FormatInviteSent(string name) => FormatSingleArgument(InviteSentStringPoolId, InviteSentFallback, name);

        public static string FormatContactNotFound(string name) => FormatSingleArgument(ContactNotFoundStringPoolId, ContactNotFoundFallback, name);

        public static string FormatNotAcceptingChat(string name) => FormatSingleArgument(NotAcceptingChatStringPoolId, NotAcceptingChatFallback, name);

        public static string FormatInviteDenied(string name) => FormatSingleArgument(InviteDeniedStringPoolId, InviteDeniedFallback, name);

        public static string FormatIncomingInviteNotice(string name) => FormatSingleArgument(IncomingInviteNoticeStringPoolId, IncomingInviteNoticeFallback, name);

        public static string FormatIncomingInvitePrompt(string name) => FormatSingleArgument(IncomingInvitePromptStringPoolId, IncomingInvitePromptFallback, name);

        public static string FormatEntered(string name) => FormatSingleArgument(EnteredStringPoolId, EnteredFallback, name);

        public static string FormatLeftRoom(string name) => FormatSingleArgument(LeftRoomStringPoolId, LeftRoomFallback, name);

        public static string FormatTyping(string name)
        {
            string suffix = MapleStoryStringPool.GetOrFallback(TypingSuffixStringPoolId, TypingSuffixFallback);
            return $"{name ?? string.Empty}{suffix}";
        }

        public static string GetHelpText()
        {
            return string.Join(
                Environment.NewLine,
                MapleStoryStringPool.GetOrFallback(HelpTitleStringPoolId, HelpTitleFallback),
                MapleStoryStringPool.GetOrFallback(HelpInviteStringPoolId, HelpInviteFallback),
                MapleStoryStringPool.GetOrFallback(HelpEndStringPoolId, HelpEndFallback));
        }

        public static string GetUnableToJoinInvitedRoom()
        {
            return MapleStoryStringPool.GetOrFallback(
                UnableToJoinInvitedRoomStringPoolId,
                UnableToJoinInvitedRoomFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 3);
        }

        public static string GetPromptUserName()
        {
            return MapleStoryStringPool.GetOrFallback(
                PromptUserNameStringPoolId,
                PromptUserNameFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 3);
        }

        public static string GetInvalidCharacterName()
        {
            return MapleStoryStringPool.GetOrFallback(
                InvalidCharacterNameStringPoolId,
                InvalidCharacterNameFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 3);
        }

        public static string GetExitChatRoomPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                ExitChatRoomPromptStringPoolId,
                ExitChatRoomPromptFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 3);
        }

        public static string GetInvitePromptTitle()
        {
            return MapleStoryStringPool.GetOrFallback(
                InvitePromptTitleStringPoolId,
                InvitePromptTitleFallback,
                appendFallbackSuffix: false,
                minimumHexWidth: 3);
        }

        public static string FormatOfficialClaimResult(byte resultCode, bool succeeded, int remainingClaimCount, byte openTime, byte closeTime)
        {
            switch (resultCode)
            {
                case 2:
                    if (!succeeded)
                    {
                        return MapleStoryStringPool.GetOrFallback(ClaimFailedStringPoolId, ClaimFailedFallback);
                    }

                    if (remainingClaimCount > 0)
                    {
                        string successFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                            ClaimSucceededWithRemainingStringPoolId,
                            ClaimSucceededWithRemainingFallback,
                            maxPlaceholderCount: 1,
                            out _);
                        return string.Format(successFormat, remainingClaimCount);
                    }

                    return MapleStoryStringPool.GetOrFallback(ClaimSucceededNoRemainingStringPoolId, ClaimSucceededNoRemainingFallback);

                case 3:
                    return MapleStoryStringPool.GetOrFallback(ClaimRejectedStringPoolId, ClaimRejectedFallback);
                case 65:
                    return MapleStoryStringPool.GetOrFallback(ClaimBlockedStringPoolId, ClaimBlockedFallback);
                case 66:
                    return MapleStoryStringPool.GetOrFallback(ClaimServerUnavailableStringPoolId, ClaimServerUnavailableFallback);
                case 67:
                    return MapleStoryStringPool.GetOrFallback(ClaimTargetRejectedStringPoolId, ClaimTargetRejectedFallback);
                case 68:
                    return MapleStoryStringPool.GetOrFallback(ClaimCategoryRejectedStringPoolId, ClaimCategoryRejectedFallback);
                case 69:
                    return MapleStoryStringPool.GetOrFallback(ClaimCooldownStringPoolId, ClaimCooldownFallback);
                case 71:
                    string availableTimeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                        ClaimAvailableTimeStringPoolId,
                        ClaimAvailableTimeFallback,
                        maxPlaceholderCount: 2,
                        out _);
                    return string.Format(availableTimeFormat, openTime, closeTime);
                case 72:
                    return MapleStoryStringPool.GetOrFallback(ClaimServerStateRejectedStringPoolId, ClaimServerStateRejectedFallback);
                default:
                    return $"Claim result code {resultCode}.";
            }
        }

        private static string FormatSingleArgument(int stringPoolId, string fallbackFormat, string value)
        {
            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(compositeFormat, value ?? string.Empty);
        }
    }
}
