using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class SummonedPacketInboxMessage
    {
        public SummonedPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "summoned-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class SummonedPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<SummonedPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Summoned packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out SummonedPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "summoned-local" : source;
            EnqueueMessage(
                new SummonedPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                packetSource);
        }

        public void EnqueueProxy(SummonedPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            EnqueueMessage(message, message.Source);
        }

        public void RecordDispatchResult(SummonedPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "summoned-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "summoned-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out SummonedPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Summoned inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/summonedpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/summonedpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Summoned inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported summoned packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new SummonedPacketInboxMessage(packetType, payload, "summoned-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string trimmed = token.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packetType)
                    && Enum.IsDefined(typeof(SummonedPacketType), packetType);
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return Enum.IsDefined(typeof(SummonedPacketType), packetType);
            }

            if (trimmed.Equals("create", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("created", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Created;
                return true;
            }

            if (trimmed.Equals("remove", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("removed", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Removed;
                return true;
            }

            if (trimmed.Equals("move", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Move;
                return true;
            }

            if (trimmed.Equals("attack", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Attack;
                return true;
            }

            if (trimmed.Equals("skill", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Skill;
                return true;
            }

            if (trimmed.Equals("hit", StringComparison.OrdinalIgnoreCase))
            {
                packetType = (int)SummonedPacketType.Hit;
                return true;
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return Enum.IsDefined(typeof(SummonedPacketType), packetType)
                ? $"{(SummonedPacketType)packetType} (0x{packetType:X})"
                : $"packet {packetType}";
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = "Summoned raw packet is missing.";
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            packetType = rawPacket[0] | (rawPacket[1] << 8);
            if (!Enum.IsDefined(typeof(SummonedPacketType), packetType))
            {
                error = $"Unsupported summoned packet opcode {packetType}.";
                return false;
            }

            payload = new byte[rawPacket.Length - sizeof(ushort)];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(rawPacket, sizeof(ushort), payload, 0, payload.Length);
            }

            error = null;
            return true;
        }

        public void Dispose()
        {
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = text[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "Summoned inbox hex payload must contain an even number of characters.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "Summoned inbox hex payload is invalid.";
                    return false;
                }
            }

            if (text.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = text[payloadBase64Prefix.Length..].Trim();
                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "Summoned inbox Base64 payload is invalid.";
                    return false;
                }
            }

            error = "Summoned inbox payload must use payloadhex=.. or payloadb64=..";
            return false;
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnqueueMessage(SummonedPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string packetSource = string.IsNullOrWhiteSpace(sourceLabel) ? "summoned-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {packetSource}.";
        }
    }
}

