using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class AdminShopDialogClientParityText
    {
        internal const int OpenRejectedStringPoolId = 0x1238;
        internal const int WishlistClosePromptStringPoolId = 0x1237;
        internal const int SharedRetryStringPoolId = 0x1239;
        internal const int RequestRejectedStringPoolId = 0x123A;
        internal const int ListingUnavailableStringPoolId = 0x123B;
        internal const int ListingChangedStringPoolId = 0x123C;
        internal const int ListingRemovedStringPoolId = 0x123D;
        internal const int TradeRetryStringPoolId = 0x123E;
        internal const int BlockedRequestStringPoolId = 0x123F;
        internal const int BusyRequestStringPoolId = 0x1240;
        internal const int CommonNoticeStringPoolId = 0x16ED;
        internal const int BuyTreatSinglyConfirmStringPoolId = 0x361;
        internal const int BuyAskItemCountStringPoolId = 0x362;
        internal const int SellTreatSinglyConfirmStringPoolId = 0x363;
        internal const int SellAskItemCountStringPoolId = 0x364;
        internal const int InvalidSourceSlotStringPoolId = 3448;
        internal const int MissingSourceItemStringPoolId = 4673;
        internal const int NotEnoughMesoStringPoolId = 0x1A8B;

        internal static string GetOpenRejectedNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                OpenRejectedStringPoolId,
                "Admin-shop open was rejected (StringPool 0x1238).",
                appendFallbackSuffix: true);
        }

        internal static string GetWishlistClosePrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                WishlistClosePromptStringPoolId,
                "Would you like to open the admin-shop wish list before closing? (StringPool 0x1237).",
                appendFallbackSuffix: true);
        }

        internal static string GetBuyTreatSinglyConfirmPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                BuyTreatSinglyConfirmStringPoolId,
                "Confirm this single-item Cash Shop request? (StringPool 0x361).",
                appendFallbackSuffix: true);
        }

        internal static string GetBuyAskItemCountPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                BuyAskItemCountStringPoolId,
                "Choose the request count for this Cash Shop row. (StringPool 0x362).",
                appendFallbackSuffix: true);
        }

        internal static string GetSellTreatSinglyConfirmPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                SellTreatSinglyConfirmStringPoolId,
                "Confirm this single-source admin-shop trade? (StringPool 0x363).",
                appendFallbackSuffix: true);
        }

        internal static string GetSellAskItemCountPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                SellAskItemCountStringPoolId,
                "Choose the source-item count for this admin-shop trade. (StringPool 0x364).",
                appendFallbackSuffix: true);
        }

        internal static string GetInvalidSourceSlotNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                InvalidSourceSlotStringPoolId,
                "This source slot can no longer be traded in the admin shop. (StringPool 3448).",
                appendFallbackSuffix: true);
        }

        internal static string GetMissingSourceItemNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                MissingSourceItemStringPoolId,
                "You do not have the required source item for this admin-shop trade. (StringPool 4673).",
                appendFallbackSuffix: true);
        }

        internal static string GetNotEnoughMesoNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                NotEnoughMesoStringPoolId,
                "You do not have enough mesos. (StringPool 0x1A8B).",
                appendFallbackSuffix: true);
        }

        internal static bool HandlesResultSubtype(byte subtype)
        {
            return subtype == 4;
        }

        internal static bool IsNoticeOnlyResult(byte resultCode)
        {
            return resultCode == 10;
        }

        internal static bool IsModeledResultCode(byte resultCode)
        {
            return resultCode <= 11;
        }

        internal static bool TryGetResultNotice(byte resultCode, out string notice, out bool reopensDialog)
        {
            reopensDialog = false;
            int stringPoolId = resultCode switch
            {
                1 or 2 or 3 => SharedRetryStringPoolId,
                4 => RequestRejectedStringPoolId,
                5 => ListingUnavailableStringPoolId,
                6 => ListingChangedStringPoolId,
                7 => ListingRemovedStringPoolId,
                8 => TradeRetryStringPoolId,
                9 => BlockedRequestStringPoolId,
                10 => CommonNoticeStringPoolId,
                11 => BusyRequestStringPoolId,
                _ => 0
            };

            reopensDialog = resultCode is 1 or 2 or 3 or 6 or 7 or 8 or 11;
            if (stringPoolId <= 0)
            {
                notice = string.Empty;
                return false;
            }

            string fallback = $"Admin-shop result notice (StringPool 0x{stringPoolId:X}).";
            notice = MapleStoryStringPool.GetOrFallback(stringPoolId, fallback, appendFallbackSuffix: true) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(notice);
        }

        internal static string BuildUnsupportedResultMessage(byte subtype, byte resultCode)
        {
            return !HandlesResultSubtype(subtype)
                ? $"CAdminShopDlg::OnPacket ignored subtype {subtype}; the v95 client only handles subtype 4 in packet 366."
                : $"CAdminShopDlg::OnPacket received unmodeled result code {resultCode}.";
        }

        internal static string BuildResultStateLabel(byte resultCode)
        {
            return resultCode switch
            {
                0 => "Success",
                1 or 2 or 3 or 6 or 8 => "Retry",
                4 => "Rejected",
                5 => "Unavailable",
                7 => "Removed",
                9 => "Blocked",
                10 => "Notice",
                11 => "Busy",
                _ => "Rejected"
            };
        }
    }
}
