using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.UI
{
    internal enum AdminShopPacketOwnedResultGateAction
    {
        IgnoreUnsupportedSubtype,
        StageMalformedSubtypePayload,
        ApplyTradeRequestResult,
        ApplyWishlistRegisterResult,
        ApplyWishlistSearchResult,
        DisconnectNoPendingRequest
    }

    internal static class AdminShopPacketOwnedResultGateParity
    {
        internal static AdminShopPacketOwnedResultGateAction ResolveGateAction(
            byte subtype,
            bool hasResultCode,
            bool hasPendingTradeRequest,
            bool hasPendingWishlistRegister,
            bool hasPendingWishlistSearch)
        {
            if (!AdminShopDialogClientParityText.HandlesResultSubtype(subtype))
            {
                return AdminShopPacketOwnedResultGateAction.IgnoreUnsupportedSubtype;
            }

            if (!hasResultCode)
            {
                return AdminShopPacketOwnedResultGateAction.StageMalformedSubtypePayload;
            }

            if (hasPendingTradeRequest)
            {
                return AdminShopPacketOwnedResultGateAction.ApplyTradeRequestResult;
            }

            if (hasPendingWishlistRegister)
            {
                return AdminShopPacketOwnedResultGateAction.ApplyWishlistRegisterResult;
            }

            if (hasPendingWishlistSearch)
            {
                return AdminShopPacketOwnedResultGateAction.ApplyWishlistSearchResult;
            }

            return AdminShopPacketOwnedResultGateAction.DisconnectNoPendingRequest;
        }

        internal static bool ShouldCaptureWishlistSearchSnapshot(
            bool hasPendingTradeRequest,
            bool hasPendingWishlistRegister,
            bool hasPendingWishlistSearch)
        {
            return !hasPendingTradeRequest
                && !hasPendingWishlistRegister
                && hasPendingWishlistSearch;
        }
    }
}
