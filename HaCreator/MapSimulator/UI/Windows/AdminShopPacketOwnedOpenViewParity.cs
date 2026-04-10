namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct AdminShopPacketOwnedOpenViewState(
        bool PreserveView,
        int ActivePaneIndex,
        int BrowseModeIndex,
        int CategoryIndex);

    internal static class AdminShopPacketOwnedOpenViewParity
    {
        internal const int NpcPaneIndex = 0;
        internal const int UserPaneIndex = 1;
        internal const int AllBrowseModeIndex = 0;
        internal const int RebuyBrowseModeIndex = 4;
        internal const int AllCategoryIndex = 0;
        internal const int ButtonCategoryIndex = 10;

        public static AdminShopPacketOwnedOpenViewState CaptureForSetAdminShopDlg(
            bool hadActivePacketOwnedSession,
            int activePaneIndex,
            int browseModeIndex,
            int categoryIndex)
        {
            return hadActivePacketOwnedSession
                ? new AdminShopPacketOwnedOpenViewState(
                    true,
                    ClampPaneIndex(activePaneIndex),
                    ClampBrowseModeIndex(browseModeIndex),
                    ClampCategoryIndex(categoryIndex))
                : default;
        }

        public static int ClampPaneIndex(int activePaneIndex)
        {
            return activePaneIndex == UserPaneIndex
                ? UserPaneIndex
                : NpcPaneIndex;
        }

        public static int ClampBrowseModeIndex(int browseModeIndex)
        {
            return browseModeIndex < AllBrowseModeIndex || browseModeIndex > RebuyBrowseModeIndex
                ? AllBrowseModeIndex
                : browseModeIndex;
        }

        public static int ClampCategoryIndex(int categoryIndex)
        {
            return categoryIndex < AllCategoryIndex || categoryIndex > ButtonCategoryIndex
                ? AllCategoryIndex
                : categoryIndex;
        }
    }
}
