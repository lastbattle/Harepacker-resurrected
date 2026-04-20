using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.UI
{
    internal enum AdminShopPacketOwnedResultGateAction
    {
        IgnoreUnsupportedSubtype,
        StageMalformedSubtypePayload,
        ApplyTradeRequestResult,
        ApplyWishlistRegisterResult,
        DisconnectNoPendingRequest
    }

    internal static class AdminShopPacketOwnedResultGateParity
    {
        internal static AdminShopPacketOwnedResultGateAction ResolveGateAction(
            byte subtype,
            bool hasResultCode,
            bool hasPendingTradeRequest,
            bool hasPendingWishlistRegister)
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

            return AdminShopPacketOwnedResultGateAction.DisconnectNoPendingRequest;
        }
    }
}
