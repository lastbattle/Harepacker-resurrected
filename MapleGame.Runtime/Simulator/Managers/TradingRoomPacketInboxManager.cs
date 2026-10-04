using System;
using System.Collections.Concurrent;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class TradingRoomPacketInboxMessage
    {
        public TradingRoomPacketInboxMessage(byte[] payload, string source, string rawText)
        {
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "trading-room-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for packet-owned trading-room payloads owned by
    /// CTradingRoomDlg::OnPacket.
    /// </summary>
    public sealed class TradingRoomPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<TradingRoomPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Trading-room packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out TradingRoomPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(byte[] payload, string source)
        {
            EnqueueMessage(new TradingRoomPacketInboxMessage(payload, source, "local"));
        }

        public void EnqueueProxy(TradingRoomPacketInboxMessage message)
        {
            EnqueueMessage(message);
        }

        public void RecordDispatchResult(TradingRoomPacketInboxMessage message, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "trading-room payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "trading-room-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "trading-room-inbox"}";
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
                error = "Trading-room inbox line is empty.";
                return false;
            }

            string[] tokens = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Trading-room inbox line is empty.";
                return false;
            }

            int index = 0;
            if (tokens[0].Equals("/socialroom", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            if (tokens.Length > index && IsTradingRoomToken(tokens[index]))
            {
                index++;
            }

            if (tokens.Length > index && tokens[index].Equals("packet", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            if (tokens.Length <= index)
            {
                error = "Trading-room inbox line is missing packet data.";
                return false;
            }

            string head = tokens[index];
            if (head.Equals("packetclientraw", StringComparison.OrdinalIgnoreCase) || head.Equals("clientraw", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Trading-room packetclientraw lines require opcode-framed hex bytes.";
                    return false;
                }

                return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
            }

            if (head.Equals("packetraw", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Trading-room packetraw lines require hex bytes.";
                    return false;
                }

                return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
            }

            if (head.Equals("packetrecv", StringComparison.OrdinalIgnoreCase) || head.Equals("recv", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (tokens.Length <= index || !TryParseOpcode(tokens[index], out ushort opcode))
                {
                    error = "Trading-room recv lines require '<opcode> <hex>' or 'recv <opcode> <hex>'.";
                    return false;
                }

                index++;
                if (tokens.Length <= index)
                {
                    error = "Trading-room recv lines require a payload after the opcode.";
                    return false;
                }

                if (!TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out byte[] recvPayload, out error))
                {
                    return false;
                }

                payload = BuildOpcodeWrappedPacket(opcode, recvPayload);
                return true;
            }

            if (TryParseOpcode(head, out ushort inferredOpcode))
            {
                index++;
                if (tokens.Length <= index)
                {
                    error = "Trading-room recv lines require a payload after the opcode.";
                    return false;
                }

                if (!TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out byte[] recvPayload, out error))
                {
                    return false;
                }

                payload = BuildOpcodeWrappedPacket(inferredOpcode, recvPayload);
                return true;
            }

            return TryParseHexPayload(string.Join(string.Empty, tokens, index, tokens.Length - index), out payload, out error);
        }

        private static bool IsTradingRoomToken(string text)
        {
            string normalized = text?.Trim().ToLowerInvariant();
            return normalized == "tradingroom" || normalized == "trade";
        }

        private static bool TryParseHexPayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            string normalized = string.Concat((text ?? string.Empty)
                .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
            {
                error = "Trading-room inbox payload is empty.";
                return false;
            }

            if ((normalized.Length & 1) != 0)
            {
                error = "Trading-room inbox payload must contain an even number of hex digits.";
                return false;
            }

            payload = new byte[normalized.Length / 2];
            for (int i = 0; i < payload.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out payload[i]))
                {
                    error = $"Trading-room inbox payload contained invalid hex at byte {i}.";
                    payload = Array.Empty<byte>();
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseOpcode(string text, out ushort opcode)
        {
            string normalized = text?.Trim() ?? string.Empty;
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
                return ushort.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out opcode);
            }

            return ushort.TryParse(normalized, out opcode);
        }

        private static byte[] BuildOpcodeWrappedPacket(ushort opcode, byte[] payload)
        {
            byte[] normalizedPayload = payload ?? Array.Empty<byte>();
            byte[] result = new byte[2 + normalizedPayload.Length];
            result[0] = (byte)(opcode & 0xFF);
            result[1] = (byte)((opcode >> 8) & 0xFF);
            if (normalizedPayload.Length > 0)
            {
                Buffer.BlockCopy(normalizedPayload, 0, result, 2, normalizedPayload.Length);
            }

            return result;
        }

        private void EnqueueMessage(TradingRoomPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued trading-room payload from {message.Source}.";
        }
    }
}
