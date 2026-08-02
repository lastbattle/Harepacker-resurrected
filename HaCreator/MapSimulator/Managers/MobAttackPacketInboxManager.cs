using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MobAttackPacketInboxMessage
    {
        public MobAttackPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "mobattack-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class MobAttackPacketInboxManager : IDisposable
    {
        public const int MovePacketType = 287;

        private readonly ConcurrentQueue<MobAttackPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Mob attack packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out MobAttackPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueProxy(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "mobattack-proxy" : source;
            EnqueueMessage(
                new MobAttackPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                packetSource);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "mobattack-local" : source;
            EnqueueMessage(
                new MobAttackPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                packetSource);
        }

        public void RecordDispatchResult(MobAttackPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "mobattack-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "mobattack-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out MobAttackPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Mob attack inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/mobattackpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/mobattackpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Mob attack inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported mob attack packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new MobAttackPacketInboxMessage(packetType, payload, "mobattack-inbox", text);
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
                    && packetType == MovePacketType;
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return packetType == MovePacketType;
            }

            if (trimmed.Equals("move", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("mobmove", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("onmove", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MovePacketType;
                return true;
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType == MovePacketType
                ? $"MobMove (0x{MovePacketType:X})"
                : $"packet {packetType}";
        }

        public void Dispose()
        {
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryParsePayload(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            string trimmed = token.Trim();
            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (trimmed.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeHexBytes(trimmed[payloadHexPrefix.Length..], out payload, out error);
            }

            if (trimmed.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payload = Convert.FromBase64String(trimmed[payloadBase64Prefix.Length..]);
                    return true;
                }
                catch (FormatException ex)
                {
                    error = $"Mob attack inbox base64 payload was invalid: {ex.Message}";
                    return false;
                }
            }

            return TryDecodeHexBytes(trimmed, out payload, out error);
        }

        private static bool TryDecodeHexBytes(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string normalized = text.Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            if ((normalized.Length & 1) != 0)
            {
                error = "Mob attack inbox hex payload must contain an even number of characters.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException ex)
            {
                error = $"Mob attack inbox hex payload was invalid: {ex.Message}";
                return false;
            }
        }

        private void EnqueueMessage(MobAttackPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string packetSource = string.IsNullOrWhiteSpace(sourceLabel) ? "mobattack-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {packetSource}.";
        }
    }
}

