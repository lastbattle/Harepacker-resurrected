using System;
using System.Collections.Concurrent;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ExpeditionIntermediaryPacketInboxMessage
    {
        public ExpeditionIntermediaryPacketInboxMessage(byte[] payload, string source, string rawText, int opcode = -1)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "expedition-inbox" : source;
            RawText = rawText ?? string.Empty;
            Opcode = opcode;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int Opcode { get; }
        public bool HasOpcodeFrame => Opcode >= 0;
    }

    /// <summary>
    /// Adapter inbox for decoded ExpeditionIntermediary payloads. Each line is
    /// either a direct retCode-prefixed payload, or an opcode-framed client
    /// packet line starting with packetclientraw.
    /// </summary>
    public sealed class ExpeditionIntermediaryPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<ExpeditionIntermediaryPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Expedition intermediary packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out ExpeditionIntermediaryPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueProxy(ExpeditionIntermediaryPacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void EnqueueLocal(ExpeditionIntermediaryPacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void RecordDispatchResult(ExpeditionIntermediaryPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribeMessage(message);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "expedition-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "expedition-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out ExpeditionIntermediaryPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Expedition intermediary inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/expeditionpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/expeditionpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Expedition intermediary inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!TryDecodeOpcodeFramedPacket(rawPacket, out int opcode, out byte[] payload, out error))
                {
                    return false;
                }

                message = new ExpeditionIntermediaryPacketInboxMessage(payload, "expedition-inbox", text, opcode);
                return true;
            }

            if (!TryParsePayload(trimmed, out byte[] directPayload, out error))
            {
                return false;
            }

            message = new ExpeditionIntermediaryPacketInboxMessage(directPayload, "expedition-inbox", text);
            return true;
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int opcode, out byte[] payload, out string error)
        {
            opcode = -1;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Expedition intermediary client packet must include a 2-byte opcode.";
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        public static string DescribeMessage(ExpeditionIntermediaryPacketInboxMessage message)
        {
            if (message == null)
            {
                return "expedition payload";
            }

            return message.HasOpcodeFrame
                ? $"ExpeditionResult opcode {message.Opcode} ({message.Payload.Length} byte(s))"
                : $"expedition payload ({message.Payload.Length} byte(s))";
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
                error = "Expedition intermediary payload is missing.";
                return false;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = text[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (text.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = text[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            string normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.Length == 0 || (normalized.Length % 2) != 0)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private void EnqueueMessage(ExpeditionIntermediaryPacketInboxMessage message, string source)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {DescribeMessage(message)} from {source ?? message.Source}.";
        }
    }
}
