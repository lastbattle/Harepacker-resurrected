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
