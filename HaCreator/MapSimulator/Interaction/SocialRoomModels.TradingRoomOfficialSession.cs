using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed partial class SocialRoomRuntime
    {
        public bool HasPendingTradingRoomAutoCrcResponse =>
            Kind == SocialRoomKind.TradingRoom && _tradeAutoCrcReplyPending;

        public bool TryBuildTradingRoomTradeRequestRawPacket(out byte[] rawPacket, out string message)
        {
            rawPacket = Array.Empty<byte>();
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Only trading-room runtimes can build subtype-17 trade request packets.";
                return false;
            }

            List<TradeVerificationEntry> entries = BuildTradingRoomTradeRequestVerificationEntries();
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((ushort)144);
            writer.Write((byte)TradingRoomTradePacketType);
            writer.Write((byte)Math.Min(byte.MaxValue, entries.Count));
            foreach (TradeVerificationEntry entry in entries)
            {
                writer.Write(entry.ItemId);
                writer.Write(unchecked((int)entry.Checksum));
            }

            rawPacket = stream.ToArray();
            message = $"Built trading-room subtype {TradingRoomTradePacketType} trade request with {entries.Count} checksum entr{(entries.Count == 1 ? "y" : "ies")} for outbound opcode 144, matching CTradingRoomDlg::Trade after the client locks the local trade button.";
            return true;
        }

        public void MarkTradingRoomTradeRequestSent()
        {
            if (Kind != SocialRoomKind.TradingRoom)
            {
                return;
            }

            _tradeLocalLocked = true;
            _tradeLocalAccepted = false;
            _tradeRemoteAccepted = false;
            _tradeLocalVerificationEntries.Clear();
            _tradeLocalVerificationEntries.AddRange(BuildTradeVerificationEntries(isLocalParty: true));
            _tradeLocalVerificationReady = _tradeLocalVerificationEntries.Count == 0 || _tradeLocalLocked;
            _tradeVerificationPending = _tradeRemoteLocked && !_tradeRemoteVerificationReady;
            RoomState = _tradeRemoteLocked ? "CRC verification" : "Locked";
            StatusMessage = $"Trading-room subtype {TradingRoomTradePacketType} trade request was sent through opcode 144; the local side now mirrors CTradingRoomDlg::Trade's locked button state.";
            RefreshTradeOccupantsAndRows();
            PersistState();
        }

        public bool TryApplyTradingRoomLocalTradeRequest(out byte[] rawPacket, out string message)
        {
            rawPacket = Array.Empty<byte>();
            message = null;
            if (!TryBuildTradingRoomTradeRequestRawPacket(out rawPacket, out string buildMessage))
            {
                message = buildMessage;
                return false;
            }

            MarkTradingRoomTradeRequestSent();
            message = $"{buildMessage} Applied the same local lock state that CTradingRoomDlg::OnTrade sets after sending opcode 144 subtype {TradingRoomTradePacketType}.";
            return true;
        }

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

        private List<TradeVerificationEntry> BuildTradingRoomTradeRequestVerificationEntries()
        {
            List<TradeVerificationEntry> entries = new List<TradeVerificationEntry>();
            entries.AddRange(BuildTradeVerificationEntries(isLocalParty: false));
            entries.AddRange(BuildTradeVerificationEntries(isLocalParty: true));
            return entries;
        }
    }
}
