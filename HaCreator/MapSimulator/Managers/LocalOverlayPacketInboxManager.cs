using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LocalOverlayPacketInboxMessage
    {
        public LocalOverlayPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "local-overlay-packet-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class LocalOverlayPacketInboxManager : IDisposable
    {
        public const int FieldFadeInOutClientPacketType = 240;
        public const int FieldFadeOutForceClientPacketType = 241;
        public const int BalloonMsgClientPacketType = 245;

        private readonly ConcurrentQueue<LocalOverlayPacketInboxMessage> _pendingMessages = new();
        public int ReceivedCount { get; private set; }
        public int ProxyIngressReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastIngressMode { get; private set; } = "none";
        public string LastStatus { get; private set; } = "Local overlay packet inbox ready for role-session/local ingress.";

        public void EnqueueProxy(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "local-overlay-proxy" : source;
            EnqueueMessage(
                new LocalOverlayPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                MapSimulatorNetworkIngressMode.Proxy,
                packetSource);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "local-overlay-ui" : source;
            EnqueueMessage(
                new LocalOverlayPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                MapSimulatorNetworkIngressMode.Local,
                packetSource);
        }

        public bool TryDequeue(out LocalOverlayPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(LocalOverlayPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "local-overlay-packet-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "local-overlay-packet-inbox"}: {detail}";
        }

        public void Dispose()
        {
        }

        public static bool TryParseLine(string text, out LocalOverlayPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Local overlay inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/localoverlaypacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/localoverlaypacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Local overlay inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out error)
                    || !IsSupportedPacketType(packetType))
                {
                    error ??= $"Unsupported local overlay client opcode {packetType}.";
                    return false;
                }

                message = new LocalOverlayPacketInboxMessage(packetType, payload, "local-overlay-packet-inbox", text);
                return true;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int parsedPacketType))
            {
                error = $"Unsupported local overlay packet '{packetToken}'.";
                return false;
            }

            byte[] parsedPayload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out parsedPayload, out error))
            {
                return false;
            }

            message = new LocalOverlayPacketInboxMessage(parsedPacketType, parsedPayload, "local-overlay-packet-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim();
            if (string.Equals(normalized, "fade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfadeinout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onfieldfadeinout", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FieldFadeInOutClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "fadeoutforce", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfadeoutforce", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onfieldfadeoutforce", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FieldFadeOutForceClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "balloon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "balloonmsg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onballoonmsg", StringComparison.OrdinalIgnoreCase))
            {
                packetType = BalloonMsgClientPacketType;
                return true;
            }

            if ((normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(normalized[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packetType))
                || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return IsSupportedPacketType(packetType);
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                FieldFadeInOutClientPacketType => $"OnFieldFadeInOut (0x{packetType:X})",
                FieldFadeOutForceClientPacketType => $"OnFieldFadeOutForce (0x{packetType:X})",
                BalloonMsgClientPacketType => $"OnBalloonMsg (0x{packetType:X})",
                _ => $"0x{packetType:X}"
            };
        }

        public static bool IsSupportedPacketType(int packetType)
        {
            return packetType == FieldFadeInOutClientPacketType
                || packetType == FieldFadeOutForceClientPacketType
                || packetType == BalloonMsgClientPacketType;
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

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

            try
            {
                payload = Convert.FromHexString(text.Replace(" ", string.Empty, StringComparison.Ordinal));
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnqueueMessage(LocalOverlayPacketInboxMessage message, string ingressMode, string statusSource)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastIngressMode = string.IsNullOrWhiteSpace(ingressMode)
                ? "unknown"
                : ingressMode;
            switch (LastIngressMode)
            {
                case MapSimulatorNetworkIngressMode.Proxy:
                    ProxyIngressReceivedCount++;
                    break;
                case MapSimulatorNetworkIngressMode.Local:
                    LocalIngressReceivedCount++;
                    break;
            }

            string source = string.IsNullOrWhiteSpace(statusSource)
                ? message.Source
                : statusSource;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {source}.";
        }
    }
}

