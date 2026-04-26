using HaCreator.MapSimulator.Fields;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class RockPaperScissorsPacketInboxMessage
    {
        public RockPaperScissorsPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "rps-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for CRPSGameDlg ownership packets.
    /// Each line is encoded as "<subtype> <hex-payload>" or
    /// "packetraw <opcode-wrapped-hex>".
    /// </summary>
    public sealed class RockPaperScissorsPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<RockPaperScissorsPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Rock-Paper-Scissors packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out RockPaperScissorsPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            EnqueueMessage(new RockPaperScissorsPacketInboxMessage(packetType, payload, source, $"{packetType}"));
        }

        public void EnqueueProxy(RockPaperScissorsPacketInboxMessage message)
        {
            EnqueueMessage(message);
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message)
                ? DescribePacketType(packetType)
                : $"{DescribePacketType(packetType)}: {message}";
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
                error = "Rock-Paper-Scissors inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (typeToken.Equals("packetraw", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("wrapped", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("opcode", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseWrappedPacketLine(payloadToken, out packetType, out payload, out error);
            }

            if (!RockPaperScissorsField.TryParsePacketType(typeToken, out packetType))
            {
                error = $"Unsupported Rock-Paper-Scissors packet subtype: {typeToken}";
                return false;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                payload = Array.Empty<byte>();
                return true;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                if (!RockPaperScissorsField.HasValidOwnerPacketPayloadShape(packetType, payload.Length))
                {
                    error = $"Invalid Rock-Paper-Scissors payload length for subtype {packetType}.";
                    return false;
                }

                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Rock-Paper-Scissors payload: {payloadToken}";
                return false;
            }
        }

        private static bool TryParseWrappedPacketLine(string payloadToken, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Rock-Paper-Scissors packetraw requires an opcode-wrapped hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(compactHex);
                if (rawPacket.Length < sizeof(ushort) + sizeof(byte))
                {
                    error = "Rock-Paper-Scissors packetraw payload is too short.";
                    return false;
                }

                ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
                if (opcode != RockPaperScissorsField.OwnerOpcode)
                {
                    error = $"Rock-Paper-Scissors packetraw opcode must be {RockPaperScissorsField.OwnerOpcode}.";
                    return false;
                }

                packetType = rawPacket[sizeof(ushort)];
                if (!RockPaperScissorsField.TryParsePacketType(packetType.ToString(), out _))
                {
                    error = $"Unsupported Rock-Paper-Scissors packet subtype: {packetType}";
                    return false;
                }

                payload = new byte[rawPacket.Length - sizeof(ushort) - sizeof(byte)];
                if (payload.Length > 0)
                {
                    Buffer.BlockCopy(rawPacket, sizeof(ushort) + sizeof(byte), payload, 0, payload.Length);
                }

                if (!RockPaperScissorsField.HasValidOwnerPacketPayloadShape(packetType, payload.Length))
                {
                    error = $"Invalid Rock-Paper-Scissors packetraw payload length for subtype {packetType}.";
                    return false;
                }

                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Rock-Paper-Scissors packetraw payload: {payloadToken}";
                return false;
            }
        }

        private static string RemoveWhitespace(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                6 => "RPS reset-win (6)",
                7 => "RPS reset-lose (7)",
                8 => "RPS open (8)",
                9 => "RPS start (9)",
                10 => "RPS force-result (10)",
                11 => "RPS result-payload (11)",
                12 => "RPS continue (12)",
                13 => "RPS destroy (13)",
                14 => "RPS reset (14)",
                _ => $"RPS subtype {packetType}"
            };
        }

        private void EnqueueMessage(RockPaperScissorsPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {message.Source}.";
        }
    }
}
