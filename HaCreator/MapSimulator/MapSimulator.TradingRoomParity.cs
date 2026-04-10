using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly TradingRoomOfficialSessionBridgeManager _tradingRoomOfficialSessionBridge = new TradingRoomOfficialSessionBridgeManager();

        private string DescribeTradingRoomOfficialSessionBridgeStatus()
        {
            return _tradingRoomOfficialSessionBridge.DescribeStatus();
        }

        private void DrainTradingRoomOfficialSessionBridge(int currentTickCount)
        {
            while (_tradingRoomOfficialSessionBridge.TryDequeue(out TradingRoomPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                if (!TryGetSocialRoomRuntime(SocialRoomKind.TradingRoom, out SocialRoomRuntime runtime))
                {
                    _tradingRoomOfficialSessionBridge.RecordDispatchResult(message.Source, success: false, "trading-room runtime inactive");
                    continue;
                }

                bool applied = runtime.TryDispatchPacketBytes(message.Payload, currentTickCount, out string resultMessage);
                _tradingRoomOfficialSessionBridge.RecordDispatchResult(
                    message.Source,
                    applied,
                    applied ? $"{runtime.DescribePacketOwnerStatus()} | {runtime.DescribeStatus()}" : resultMessage);

                if (applied)
                {
                    if (runtime.HasPendingTradingRoomAutoCrcResponse
                        && runtime.TryBuildTradingRoomCrcResponseRawPacket(out byte[] crcReplyPacket, out string crcReplyStatus)
                        && _tradingRoomOfficialSessionBridge.TrySendOutboundRawPacket(crcReplyPacket, out string injectedStatus))
                    {
                        runtime.MarkTradingRoomAutoCrcResponseSent();
                        _tradingRoomOfficialSessionBridge.RecordDispatchResult(
                            message.Source,
                            success: true,
                            $"{runtime.DescribePacketOwnerStatus()} | {runtime.DescribeStatus()} | {crcReplyStatus} | {injectedStatus}");
                    }

                    ShowSocialRoomWindow(SocialRoomKind.TradingRoom);
                }
            }
        }
    }
}
