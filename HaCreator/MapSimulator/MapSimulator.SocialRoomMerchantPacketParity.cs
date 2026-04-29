using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly SocialRoomMerchantPacketInboxManager _socialRoomMerchantPacketInbox = new();
        private readonly SocialRoomMerchantOfficialSessionBridgeManager _socialRoomMerchantOfficialSessionBridge;

        private string DescribeSocialRoomMerchantPacketInboxStatus(SocialRoomKind kind)
        {
            return $"{_socialRoomMerchantPacketInbox.LastStatus} Merchant inbox status for {kind}: adapter-only; listener-fallback retired.";
        }

        private string DescribeSocialRoomMerchantOfficialSessionBridgeStatus(SocialRoomKind kind)
        {
            string configuredKind = _socialRoomMerchantOfficialSessionBridge.PreferredKind.HasValue
                ? _socialRoomMerchantOfficialSessionBridge.PreferredKind.Value.ToString()
                : "none";
            string kindStatus = _socialRoomMerchantOfficialSessionBridge.PreferredKind == kind
                ? "active for this owner"
                : $"armed for {configuredKind}";
            return $"{_socialRoomMerchantOfficialSessionBridge.DescribeStatus()} Merchant bridge status for {kind}: {kindStatus}.";
        }

        private bool TrySendEntrustedShopBlacklistOutboundPacket(byte[] rawPacket, string summary)
        {
            if (!_socialRoomMerchantOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string bridgeStatus))
            {
                return false;
            }

            PushFieldRuleMessage(
                string.IsNullOrWhiteSpace(summary) ? bridgeStatus : $"{summary} {bridgeStatus}",
                Environment.TickCount,
                showOverlay: false);
            return true;
        }

        private void DrainSocialRoomMerchantPacketInbox(int currentTickCount)
        {
            while (_socialRoomMerchantPacketInbox.TryDequeue(out SocialRoomMerchantPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                if (!TryGetSocialRoomRuntime(message.Kind, out SocialRoomRuntime runtime))
                {
                    _socialRoomMerchantPacketInbox.RecordDispatchResult(message, success: false, "merchant-room runtime inactive");
                    continue;
                }

                bool applied = runtime.TryDispatchPacketBytes(message.Payload, currentTickCount, out string resultMessage);
                _socialRoomMerchantPacketInbox.RecordDispatchResult(
                    message,
                    applied,
                    applied ? $"{runtime.DescribePacketOwnerStatus()} | {runtime.DescribeStatus()}" : resultMessage);

                if (applied)
                {
                    ShowSocialRoomWindow(message.Kind);
                }
            }
        }

        private void DrainSocialRoomMerchantOfficialSessionBridge(int currentTickCount)
        {
            while (_socialRoomMerchantOfficialSessionBridge.TryDequeue(out SocialRoomMerchantPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                if (!TryGetSocialRoomRuntime(message.Kind, out SocialRoomRuntime runtime))
                {
                    _socialRoomMerchantOfficialSessionBridge.RecordDispatchResult(message.Source, success: false, "merchant-room runtime inactive");
                    continue;
                }

                bool applied = runtime.TryDispatchPacketBytes(message.Payload, currentTickCount, out string resultMessage);
                _socialRoomMerchantOfficialSessionBridge.RecordDispatchResult(
                    message.Source,
                    applied,
                    applied ? $"{runtime.DescribePacketOwnerStatus()} | {runtime.DescribeStatus()}" : resultMessage);

                if (applied)
                {
                    ShowSocialRoomWindow(message.Kind);
                }
            }
        }
    }
}

