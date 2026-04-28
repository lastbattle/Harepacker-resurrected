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
        internal const int AllCategoryIndex = 0;
        internal const int ButtonCategoryIndex = 10;

        public static AdminShopPacketOwnedOpenViewState ResolveDefaultForSetAdminShopDlg()
        {
            return new AdminShopPacketOwnedOpenViewState(
                true,
                NpcPaneIndex,
                AllBrowseModeIndex,
                AllCategoryIndex);
        }

        public static bool ShouldClearPendingRequestOnSetAdminShopDlg()
        {
            // CAdminShopDlg::SetAdminShopDlg clears m_bShopRequestSent before rebuilding packet-owned rows.
            return true;
        }

        public static AdminShopPacketOwnedOpenViewState ResolveUserSellRefocus(int categoryIndex)
        {
            return new AdminShopPacketOwnedOpenViewState(
                true,
                UserPaneIndex,
                AllBrowseModeIndex,
                ClampCategoryIndex(categoryIndex));
        }

        public static int ClampPaneIndex(int activePaneIndex)
        {
            return activePaneIndex == UserPaneIndex
                ? UserPaneIndex
                : NpcPaneIndex;
        }

        public static int ClampBrowseModeIndex(int browseModeIndex)
        {
            return AllBrowseModeIndex;
        }

        public static int ClampCategoryIndex(int categoryIndex)
        {
            return categoryIndex < AllCategoryIndex || categoryIndex > ButtonCategoryIndex
                ? AllCategoryIndex
                : categoryIndex;
        }
    }
}
