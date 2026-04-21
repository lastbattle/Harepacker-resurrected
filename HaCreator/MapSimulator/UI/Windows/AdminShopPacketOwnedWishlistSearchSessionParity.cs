namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedWishlistSearchSessionParity
    {
        internal static int ResolveEffectiveSearchSessionId(int searchSessionId, int localSearchRequestId)
        {
            return searchSessionId >= 0
                ? searchSessionId
                : localSearchRequestId;
        }

        internal static bool IsSnapshotCompatibleWithActiveSession(
            int activeServiceSessionId,
            int activeSearchSessionId,
            int pendingSearchRequestId,
            AdminShopPacketOwnedWishlistSearchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            bool serviceSessionCompatible = snapshot.ServiceSessionId < 0
                || activeServiceSessionId < 0
                || snapshot.ServiceSessionId == activeServiceSessionId;
            int snapshotSearchSessionId = ResolveEffectiveSearchSessionId(
                snapshot.SearchSessionId,
                snapshot.LocalSearchRequestId);
            int activeOrPendingSearchSessionId = activeSearchSessionId >= 0
                ? activeSearchSessionId
                : pendingSearchRequestId;
            bool searchSessionCompatible = snapshotSearchSessionId < 0
                || activeOrPendingSearchSessionId < 0
                || snapshotSearchSessionId == activeOrPendingSearchSessionId;
            return serviceSessionCompatible && searchSessionCompatible;
        }
    }
}
