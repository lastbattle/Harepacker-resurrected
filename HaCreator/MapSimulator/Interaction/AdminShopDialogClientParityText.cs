using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class AdminShopDialogClientParityText
    {
        internal const int OpenRejectedStringPoolId = 0x1238;
        internal const int SharedRetryStringPoolId = 0x1239;
        internal const int RequestRejectedStringPoolId = 0x123A;
        internal const int ListingUnavailableStringPoolId = 0x123B;
        internal const int ListingChangedStringPoolId = 0x123C;
        internal const int ListingRemovedStringPoolId = 0x123D;
        internal const int TradeRetryStringPoolId = 0x123E;
        internal const int BlockedRequestStringPoolId = 0x123F;
        internal const int BusyRequestStringPoolId = 0x1240;
        internal const int CommonNoticeStringPoolId = 0x16ED;

        internal static string GetOpenRejectedNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                OpenRejectedStringPoolId,
                "Admin-shop open was rejected (StringPool 0x1238).",
                appendFallbackSuffix: true);
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
            return subtype != 4
                ? $"CAdminShopDlg::OnPacket ignored subtype {subtype}; the v95 client only handles subtype 4 in packet 366."
                : $"CAdminShopDlg::OnPacket received unmodeled result code {resultCode}.";
        }

        internal static string BuildResultStateLabel(byte resultCode)
        {
            return resultCode switch
            {
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
