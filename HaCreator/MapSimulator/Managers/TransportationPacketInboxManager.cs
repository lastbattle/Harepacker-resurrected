using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class TransportationPacketInboxMessage
    {
        public TransportationPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "transport-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for transit and voyage field wrapper packets.
    /// Supports direct aliases for the recovered CField_ContiMove handlers as well as
    /// decrypted Maple packets for opcodes 164 (OnContiMove) and 165 (OnContiState).
    /// </summary>
    public sealed class TransportationPacketInboxManager : IDisposable
    {
        public const int PacketTypeContiMove = 164;
        public const int PacketTypeContiState = 165;
        public const byte ContiMoveStartShip = 8;
        public const byte ContiMoveMoveField = 10;
        public const byte ContiMoveEndShip = 12;

        private readonly ConcurrentQueue<TransportationPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Transport packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out TransportationPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(TransportationPacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void EnqueueProxy(TransportationPacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void RecordDispatchResult(string source, TransportationPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result) ? DescribePacket(message.PacketType, message.Payload) : $"{DescribePacket(message.PacketType, message.Payload)}: {result}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Transport inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (TryParseRawPacket(trimmed, out packetType, out payload, out error))
            {
                return true;
            }

            string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Transport inbox line is empty.";
                return false;
            }

            string action = parts[0].Trim().ToLowerInvariant();
            switch (action)
            {
                case "start":
                case "onstartshipmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveStartShip, out packetType, out payload, out error);

                case "move":
                case "onmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveMoveField, out packetType, out payload, out error);

                case "end":
                case "onendshipmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveEndShip, out packetType, out payload, out error);

                case "state":
                case "contistate":
                case "oncontistate":
                    if (parts.Length < 3
                        || !byte.TryParse(parts[1], out byte state)
                        || !byte.TryParse(parts[2], out byte stateValue))
                    {
                        error = "Transport state lines must be 'state <state> <value>'.";
                        return false;
                    }

                    packetType = PacketTypeContiState;
                    payload = new[] { state, stateValue };
                    return true;

                case "contimove":
                case "oncontimove":
                    if (parts.Length < 3
                        || !byte.TryParse(parts[1], out byte moveType)
                        || !byte.TryParse(parts[2], out byte moveValue))
                    {
                        error = "Transport conti-move lines must be 'OnContiMove <subtype> <value>'.";
                        return false;
                    }

                    packetType = PacketTypeContiMove;
                    payload = new[] { moveType, moveValue };
                    return true;

                default:
                    if (!int.TryParse(parts[0], out packetType))
                    {
                        error = $"Unsupported transport packet action: {parts[0]}";
                        return false;
                    }

                    if (packetType != PacketTypeContiMove && packetType != PacketTypeContiState)
                    {
                        error = $"Unsupported transport packet opcode: {packetType}";
                        return false;
                    }

                    if (parts.Length < 2)
                    {
                        error = "Transport packet lines must include a hex payload.";
                        return false;
                    }

                    if (!TryParseHexPayload(string.Join(string.Empty, parts.Skip(1)), out payload))
                    {
                        error = "Transport packet payload must be valid hex.";
                        return false;
                    }

                    return true;
            }
        }

        private static bool TryBuildContiMoveAlias(string[] parts, byte moveType, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (parts.Length < 2 || !byte.TryParse(parts[1], out byte value))
            {
                error = $"Transport move lines must be '{parts[0]} <value>'.";
                return false;
            }

            packetType = PacketTypeContiMove;
            payload = new[] { moveType, value };
            return true;
        }

        private static bool TryParseRawPacket(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !string.Equals(parts[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (parts.Length < 2)
            {
                error = "Transport raw packet requires a decrypted packet hex payload.";
                return false;
            }

            if (!TryParseHexPayload(string.Join(string.Empty, parts.Skip(1)), out byte[] rawPacket))
            {
                error = "Transport raw packet payload must be valid hex.";
                return false;
            }

            if (rawPacket.Length < sizeof(short))
            {
                error = "Transport raw packet must include a 2-byte opcode.";
                return false;
            }

            try
            {
                PacketReader reader = new PacketReader(rawPacket);
                packetType = reader.ReadShort();
                payload = reader.ReadBytes(rawPacket.Length - sizeof(short));
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                error = $"Transport raw packet decode failed: {ex.Message}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            if (packetType != PacketTypeContiMove && packetType != PacketTypeContiState)
            {
                error = $"Unsupported transport raw packet opcode: {packetType}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            return true;
        }

        internal static string DescribePacket(int packetType, byte[] payload)
        {
            if (packetType == PacketTypeContiMove && payload?.Length >= 2)
            {
                return payload[0] switch
                {
                    ContiMoveStartShip => $"Transport OnContiMove start ({payload[1]}) [client subtype 8 -> OnStartShipMoveField]",
                    ContiMoveMoveField => $"Transport OnContiMove move ({payload[1]}) [client subtype 10 -> OnMoveField]",
                    ContiMoveEndShip => $"Transport OnContiMove end ({payload[1]}) [client subtype 12 -> OnEndShipMoveField]",
                    _ => $"Transport OnContiMove ({payload[0]}, {payload[1]})"
                };
            }

            if (packetType == PacketTypeContiState && payload?.Length >= 2)
            {
                string stateBranch = payload[0] switch
                {
                    0 or 1 or 6 => "EnterShipMove branch",
                    2 or 5 => "LeaveShipMove branch",
                    3 or 4 => "AppearShip-or-LeaveShipMove branch",
                    _ => "unhandled branch"
                };
                return $"Transport OnContiState ({payload[0]}, {payload[1]}) [{stateBranch}]";
            }

            return $"Transport packet {packetType}";
        }

        private static bool TryParseHexPayload(string text, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = NormalizeHexPayload(text);
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            if ((normalized.Length & 1) != 0)
            {
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string NormalizeHexPayload(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return string.Concat(value.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)));
        }

        private void EnqueueMessage(TransportationPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string source = string.IsNullOrWhiteSpace(sourceLabel) ? message.Source : sourceLabel;
            LastStatus = $"Queued {DescribePacket(message.PacketType, message.Payload)} from {source}.";
        }
    }
}
