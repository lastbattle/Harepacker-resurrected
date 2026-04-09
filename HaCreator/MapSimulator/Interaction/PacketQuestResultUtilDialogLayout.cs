namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketQuestResultUtilDialogButtonVisualState
    {
        Normal = 0,
        MouseOver = 1,
        Pressed = 2,
        Disabled = 3,
        KeyFocused = 4
    }

    internal static class PacketQuestResultUtilDialogLayout
    {
        // UIWindow2.img/UtilDlgEx exposes the v95-era quest-result util dialog shell
        // as top/center/bottom slices sized 519x28, 519x13, and 519x44.
        internal const int DefaultWindowWidth = 519;
        internal const int DefaultTopHeight = 28;
        internal const int DefaultCenterHeight = 13;
        internal const int DefaultBottomHeight = 44;
        internal const int DefaultCenterRepeatCount = 10;
        internal const int DefaultWindowHeight =
            DefaultTopHeight + (DefaultCenterHeight * DefaultCenterRepeatCount) + DefaultBottomHeight;

        internal static string ResolveNextButtonText(bool hasNextPage)
        {
            return hasNextPage ? "Next" : "OK";
        }

        internal static PacketQuestResultUtilDialogButtonVisualState ResolveButtonVisualState(
            bool enabled,
            bool isPressed,
            bool isHovered,
            bool isKeyFocused)
        {
            if (!enabled)
            {
                return PacketQuestResultUtilDialogButtonVisualState.Disabled;
            }

            if (isPressed)
            {
                return PacketQuestResultUtilDialogButtonVisualState.Pressed;
            }

            if (isHovered)
            {
                return PacketQuestResultUtilDialogButtonVisualState.MouseOver;
            }

            return isKeyFocused
                ? PacketQuestResultUtilDialogButtonVisualState.KeyFocused
                : PacketQuestResultUtilDialogButtonVisualState.Normal;
        }
    }
}
