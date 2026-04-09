using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Interaction
{
    public sealed partial class SocialRoomRuntime
    {
        public bool TryBuildTradingRoomCrcResponseRawPacket(out byte[] rawPacket, out string message)
        {
            rawPacket = Array.Empty<byte>();
            message = null;
            if (Kind != SocialRoomKind.TradingRoom)
            {
                message = "Only trading-room runtimes can build subtype-20 CRC packets.";
                return false;
            }

            List<TradeVerificationEntry> entries = BuildTradeVerificationEntries(isLocalParty: true);
            if (entries.Count == 0)
            {
                message = "Trading-room CRC reply requires at least one local offer item with a valid item id.";
                return false;
            }

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
            message = $"Built trading-room subtype {TradingRoomItemCrcPacketType} CRC reply with {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")} for outbound opcode 144.";
            return true;
        }
    }
}
