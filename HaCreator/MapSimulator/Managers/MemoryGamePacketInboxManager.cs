using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MemoryGamePacketInboxMessage
    {
        public MemoryGamePacketInboxMessage(byte[] payload, string source, string rawText)
        {
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "memorygame-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for live MiniRoom Match Cards payloads.
    /// Each line accepts a raw payload, a full opcode-wrapped packet, a
    /// client opcode-wrapped request packet, or the
    /// command-shaped "/memorygame packetraw <hex payload>" and
    /// "/memorygame packetrecv <opcode> <hex payload>" and
    /// "/memorygame packetclientraw <hex packet>" forms.
    /// </summary>
    public sealed class MemoryGamePacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<MemoryGamePacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Memory Game packet inbox ready for role-session/local ingress.";

        public void EnqueueLocal(byte[] payload, string source)
        {
            EnqueueMessage(
                new MemoryGamePacketInboxMessage(payload, source, "packetraw"),
                source);
        }

        public void EnqueueProxy(MemoryGamePacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public bool TryDequeue(out MemoryGamePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message) ? "MiniRoom payload" : message;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Memory Game inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/memorygame", StringComparison.OrdinalIgnoreCase))
            {
                string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                {
                    error = "Only /memorygame packetraw <hex>, /memorygame packetrecv <opcode> <hex>, and /memorygame packetclientraw <hex> are accepted by the inbox.";
                    return false;
                }

                if (tokens[1].Equals("packetraw", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = string.Join(string.Empty, tokens, 2, tokens.Length - 2);
                }
                else if (tokens[1].Equals("packetclientraw", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseHexPayload(string.Join(string.Empty, tokens, 2, tokens.Length - 2), out byte[] rawClientPacket, out error))
                    {
                        return false;
                    }

                    return MemoryGameOfficialSessionBridgeManager.TryDecodeClientOpcodePacket(rawClientPacket, out payload, out error);
                }
                else if (tokens[1].Equals("packetrecv", StringComparison.OrdinalIgnoreCase)
                    || tokens[1].Equals("recv", StringComparison.OrdinalIgnoreCase))
                {
                    if (tokens.Length < 4 || !TryParseOpcode(tokens[2], out ushort opcode))
                    {
                        error = "Memory Game recv lines require '/memorygame packetrecv <opcode> <hex>'.";
                        return false;
                    }

                    if (!TryParseHexPayload(string.Join(string.Empty, tokens, 3, tokens.Length - 3), out byte[] recvPayload, out error))
                    {
                        return false;
                    }

                    payload = BuildOpcodeWrappedPacket(opcode, recvPayload);
                    return true;
                }
                else
                {
                    error = "Only /memorygame packetraw <hex>, /memorygame packetrecv <opcode> <hex>, and /memorygame packetclientraw <hex> are accepted by the inbox.";
                    return false;
                }
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParseHexPayload(rawHex, out byte[] rawClientPacket, out error))
                {
                    return false;
                }

                return MemoryGameOfficialSessionBridgeManager.TryDecodeClientOpcodePacket(rawClientPacket, out payload, out error);
            }

            if (trimmed.StartsWith("packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseHexPayload(trimmed["packetraw".Length..].Trim(), out payload, out error);
            }

            if (trimmed.StartsWith("raw=", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseHexPayload(trimmed["raw=".Length..].Trim(), out payload, out error);
            }

            string[] recvTokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (recvTokens.Length >= 3
                && (recvTokens[0].Equals("recv", StringComparison.OrdinalIgnoreCase)
                    || recvTokens[0].Equals("packetrecv", StringComparison.OrdinalIgnoreCase)
                    || TryParseOpcode(recvTokens[0], out _)))
            {
                int opcodeTokenIndex = recvTokens[0].Equals("recv", StringComparison.OrdinalIgnoreCase)
                    || recvTokens[0].Equals("packetrecv", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;
                int payloadTokenIndex = opcodeTokenIndex + 1;
                if (recvTokens.Length <= payloadTokenIndex || !TryParseOpcode(recvTokens[opcodeTokenIndex], out ushort opcode))
                {
                    error = "Memory Game recv lines require '<opcode> <hex>' or 'recv <opcode> <hex>'.";
                    return false;
                }

                if (!TryParseHexPayload(string.Join(string.Empty, recvTokens, payloadTokenIndex, recvTokens.Length - payloadTokenIndex), out byte[] recvPayload, out error))
                {
                    return false;
                }

                payload = BuildOpcodeWrappedPacket(opcode, recvPayload);
                return true;
            }

            return TryParseHexPayload(trimmed, out payload, out error);
        }

        private static string RemoveWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            char[] buffer = new char[text.Length];
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!char.IsWhiteSpace(ch))
                {
                    buffer[count++] = ch;
                }
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }

        private static bool TryParseOpcode(string text, out ushort opcode)
        {
            opcode = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim().TrimEnd(':');
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            return ushort.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out opcode)
                || ushort.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out opcode);
        }

        private static bool TryParseHexPayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            string compactHex = RemoveWhitespace(text)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(":", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Replace("|", string.Empty, StringComparison.Ordinal);
            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "MiniRoom payload requires hex bytes.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                error = null;
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid MiniRoom hex payload: {text}";
                return false;
            }
        }

        private static byte[] BuildOpcodeWrappedPacket(ushort opcode, byte[] payload)
        {
            byte[] packet = new byte[(payload?.Length ?? 0) + sizeof(ushort)];
            packet[0] = (byte)(opcode & 0xFF);
            packet[1] = (byte)((opcode >> 8) & 0xFF);
            if (payload != null && payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, packet, sizeof(ushort), payload.Length);
            }

            return packet;
        }

        private void EnqueueMessage(MemoryGamePacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string source = string.IsNullOrWhiteSpace(sourceLabel) ? message.Source : sourceLabel;
            LastStatus = $"Queued MiniRoom payload from {source}.";
        }
    }
}
