using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed partial class SocialRoomRuntime
    {
        public bool HasPendingTradingRoomAutoCrcResponse =>
            Kind == SocialRoomKind.TradingRoom && _tradeAutoCrcReplyPending;

        public bool TryBuildTradingRoomCrcResponseRawPacket(out byte[] rawPacket, out string message)
        {
            rawPacket = Array.Empty<byte>();
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Only trading-room runtimes can build subtype-20 CRC packets.";
                return false;
            }

            List<TradeVerificationEntry> entries = _tradeLocalVerificationEntries.Count > 0 || _tradeLocalVerificationReady
                ? new List<TradeVerificationEntry>(_tradeLocalVerificationEntries)
                : BuildTradeVerificationEntries(isLocalParty: false);

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((ushort)144);
            writer.Write((byte)TradingRoomItemCrcPacketType);
            writer.Write((byte)Math.Min(byte.MaxValue, entries.Count));
            foreach (TradeVerificationEntry entry in entries)
            {
                writer.Write(entry.ItemId);
                writer.Write(unchecked((int)entry.Checksum));
            }

            rawPacket = stream.ToArray();
            message = $"Built trading-room subtype {TradingRoomItemCrcPacketType} CRC reply with {entries.Count} peer-side entr{(entries.Count == 1 ? "y" : "ies")} for outbound opcode 144, matching the client OnTrade scan over m_aaItem[1] even when the row count is zero.";
            return true;
        }

        public void MarkTradingRoomAutoCrcResponseSent()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            bool hadPendingReply = _tradeAutoCrcReplyPending;
            _tradeAutoCrcReplyPending = false;
            if (!hadPendingReply)
            {
                return;
            }

            if (_tradeRemoteLocked && _tradeLocalVerificationReady)
            {
                _tradeVerificationPending = false;
                _tradeRemoteAccepted = true;
                RoomState = _tradeLocalAccepted ? "Awaiting settlement" : "Locked";
                StatusMessage = $"Trading-room subtype {TradingRoomItemCrcPacketType} CRC reply was sent; the CTradingRoomDlg::OnTrade remote acceptance is now waiting on local final acceptance.";
            }
            else
            {
                StatusMessage = $"Trading-room subtype {TradingRoomItemCrcPacketType} CRC reply was sent, but the runtime is still waiting for a locked remote trade handoff.";
            }

            RefreshTradeOccupantsAndRows();
            PersistState();
        }
    }
}
