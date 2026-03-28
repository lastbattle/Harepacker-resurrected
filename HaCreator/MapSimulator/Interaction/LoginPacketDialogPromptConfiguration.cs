using HaCreator.MapSimulator.UI;

namespace HaCreator.MapSimulator
{
    internal enum LoginPacketDialogOwner
    {
        LoginUtilityDialog = 0,
        ConnectionNotice = 1,
    }

    internal sealed class LoginPacketDialogPromptConfiguration
    {
        public LoginPacketDialogOwner Owner { get; init; } = LoginPacketDialogOwner.LoginUtilityDialog;
        public string Title { get; init; }
        public string Body { get; init; }
        public int? NoticeTextIndex { get; init; }
        public ConnectionNoticeWindowVariant? NoticeVariant { get; init; }
        public LoginUtilityDialogButtonLayout? ButtonLayout { get; init; }
        public LoginUtilityDialogAction? Action { get; init; }
        public string PrimaryLabel { get; init; }
        public string SecondaryLabel { get; init; }
        public string InputLabel { get; init; }
        public string InputPlaceholder { get; init; }
        public bool InputMasked { get; init; }
        public int InputMaxLength { get; init; }
        public SoftKeyboardKeyboardType SoftKeyboardType { get; init; } = SoftKeyboardKeyboardType.AlphaNumeric;
        public int DurationMs { get; init; } = 2400;
    }
}
