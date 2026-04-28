namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedOwnerGateParity
    {
        internal static bool ShouldIgnoreOpenAtOwnerGate(bool hasBlockingUniqueModelessOwner, int commodityCount)
        {
            return commodityCount > 0 && hasBlockingUniqueModelessOwner;
        }

        internal static bool ShouldIgnoreResultAtOwnerGate(
            bool hasBlockingUniqueModelessOwner,
            bool acceptsResultAtOwnerGate)
        {
            return hasBlockingUniqueModelessOwner || !acceptsResultAtOwnerGate;
        }

        internal static bool ShouldStageDeferredResultAtOwnerGate(
            bool keepSessionActive,
            bool hasPendingRequestState,
            AdminShopPacketOwnedOwnerVisibilityState ownerVisibilityState)
        {
            if (!keepSessionActive)
            {
                return false;
            }

            return hasPendingRequestState
                || ownerVisibilityState == AdminShopPacketOwnedOwnerVisibilityState.HiddenByCashShopFamily;
        }

        internal static bool HasPendingResultRequestState(
            bool hasPendingTradeRequest,
            bool hasPendingWishlistRegister,
            bool hasPendingWishlistSearch,
            bool isWaitingForResult,
            bool hasPendingPacketOwnedResult)
        {
            return hasPendingTradeRequest
                || hasPendingWishlistRegister
                || hasPendingWishlistSearch
                || isWaitingForResult
                || hasPendingPacketOwnedResult;
        }
    }
}
