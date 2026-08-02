using System;
using System.Collections.Concurrent;

namespace HaCreator.MapSimulator.Managers
{
    public enum CookieHousePointInboxPayloadKind
    {
        TextPoint,
        RawContextPoint,
        OpcodeFramedRawContextPoint,
        OpcodeFramedSessionValuePoint
    }

        public sealed class CookieHousePointInboxMessage
        {
            public CookieHousePointInboxMessage(int point, string source, string rawText, CookieHousePointInboxPayloadKind payloadKind)
            {
            Point = point;
            Source = string.IsNullOrWhiteSpace(source) ? "cookiehouse-inbox" : source;
            RawText = rawText ?? string.Empty;
            PayloadKind = payloadKind;
        }

        public int Point { get; }
        public string Source { get; }
        public string RawText { get; }
        public CookieHousePointInboxPayloadKind PayloadKind { get; }
    }

    /// <summary>
    /// Adapter inbox for externally authored Cookie House point updates.
    /// Each line is encoded as either "<point>", "point <point>", "raw <hex>",
    /// or "packetraw <hex>" where <hex> is either the client-shaped little-endian
    /// CWvsContext Cookie House point dword recovered from v95 or a full decrypted
    /// Maple packet frame whose payload is that same dword.
    /// </summary>
    public sealed class CookieHousePointInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;
        public const int ClientPacketOpcodeByteLength = 2;
        public const int ClientContextPointByteLength = 4;
        public const int ClientOpcodeFramedPointByteLength = ClientPacketOpcodeByteLength + ClientContextPointByteLength;
        public const int ClientContextPointOffset = 0x4148;
        public const int ClientBitmapNumberDigitCount = 3;
        public const int ClientMaximumDisplayPoint = 999;

        private readonly ConcurrentQueue<CookieHousePointInboxMessage> _pendingMessages = new();
        private readonly RetiredMapleSocketState _socketState = new("Cookie House point inbox", DefaultPort, "Cookie House point inbox ready for role-session/local ingress.");

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public int ReceivedCount { get; private set; }
        public int ProxyIngressReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastIngressMode { get; private set; } = "none";
        public string LastStatus => _socketState.LastStatus;

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            _socketState.Stop("Cookie House point inbox ready for role-session/local ingress.");
        }

        public bool TryDequeue(out CookieHousePointInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueProxy(CookieHousePointInboxMessage message)
        {
            EnqueueMessage(message, MapSimulatorNetworkIngressMode.Proxy);
        }

        public void EnqueueLocal(CookieHousePointInboxMessage message)
        {
            EnqueueMessage(message, MapSimulatorNetworkIngressMode.Local);
        }

        public void RecordDispatchResult(string source, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message) ? "point update" : message;
            _socketState.SetStatus(success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.");
        }

        public void Dispose()
        {
        }

        public static bool TryParsePointLine(
            string text,
            out int point,
            out CookieHousePointInboxPayloadKind payloadKind,
            out string error)
        {
            point = 0;
            payloadKind = CookieHousePointInboxPayloadKind.TextPoint;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Cookie House inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && IsOpcodeFramedRawContextAlias(parts[0]))
            {
                return TryParseOpcodeFramedRawContextPoint(
                    string.Join(string.Empty, parts, 1, parts.Length - 1),
                    out point,
                    out payloadKind,
                    out error);
            }

            if (parts.Length >= 2 && IsRawContextAlias(parts[0]))
            {
                return TryParseRawContextPoint(
                    string.Join(string.Empty, parts, 1, parts.Length - 1),
                    out point,
                    out payloadKind,
                    out error);
            }

            string valueToken = parts.Length switch
            {
                1 => parts[0],
                >= 2 when string.Equals(parts[0], "point", StringComparison.OrdinalIgnoreCase) => parts[1],
                _ => null
            };

            if (string.IsNullOrWhiteSpace(valueToken) || !int.TryParse(valueToken, out point))
            {
                error = $"Invalid Cookie House point payload: {text}";
                return false;
            }

            return TryValidateClientPoint(point, out point, out error);
        }

        internal static bool TryValidateClientPoint(int point, out int normalizedPoint, out string error)
        {
            normalizedPoint = 0;
            error = null;
            if (point < 0)
            {
                error = $"Cookie House point payload decodes to an invalid negative score ({point}).";
                return false;
            }

            normalizedPoint = point;
            return true;
        }

        private static bool IsRawContextAlias(string token)
        {
            return string.Equals(token, "raw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "context", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOpcodeFramedRawContextAlias(string token)
        {
            return string.Equals(token, "packetraw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "packetrecv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "packetclientraw", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseRawContextPoint(
            string hexPayload,
            out int point,
            out CookieHousePointInboxPayloadKind payloadKind,
            out string error)
        {
            point = 0;
            payloadKind = CookieHousePointInboxPayloadKind.RawContextPoint;
            error = null;

            if (string.IsNullOrWhiteSpace(hexPayload))
            {
                error = "Cookie House raw payload is empty.";
                return false;
            }

            string normalized = hexPayload.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (normalized.Length != ClientContextPointByteLength * 2)
            {
                error = $"Cookie House raw payload must be exactly {ClientContextPointByteLength} bytes for CWvsContext+0x{ClientContextPointOffset:X}.";
                return false;
            }

            byte[] bytes = new byte[ClientContextPointByteLength];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    error = $"Invalid Cookie House raw payload: {hexPayload}";
                    return false;
                }
            }

            return TryDecodeClientContextPoint(bytes, out point, out error);
        }

        private static bool TryParseOpcodeFramedRawContextPoint(
            string hexPayload,
            out int point,
            out CookieHousePointInboxPayloadKind payloadKind,
            out string error)
        {
            point = 0;
            payloadKind = CookieHousePointInboxPayloadKind.OpcodeFramedRawContextPoint;
            error = null;

            if (!TryParseHexBytes(hexPayload, out byte[] bytes, out error))
            {
                return false;
            }

            if (bytes.Length != ClientOpcodeFramedPointByteLength)
            {
                error = $"Cookie House opcode-framed raw payload must be exactly {ClientOpcodeFramedPointByteLength} bytes (2-byte opcode + 4-byte CWvsContext+0x{ClientContextPointOffset:X} point payload).";
                return false;
            }

            byte[] payload = new byte[ClientContextPointByteLength];
            Buffer.BlockCopy(bytes, ClientPacketOpcodeByteLength, payload, 0, payload.Length);
            return TryDecodeClientContextPoint(payload, out point, out error);
        }

        internal static bool TryDecodeClientContextPoint(byte[] payload, out int point, out string error)
        {
            point = 0;
            error = null;

            if (payload == null || payload.Length != ClientContextPointByteLength)
            {
                error = $"Cookie House raw payload must be exactly {ClientContextPointByteLength} bytes for CWvsContext+0x{ClientContextPointOffset:X}.";
                return false;
            }

            int decodedPoint = BitConverter.ToInt32(payload, 0);
            return TryValidateClientPoint(decodedPoint, out point, out error);
        }

        private static bool TryParseHexBytes(string hexPayload, out byte[] bytes, out string error)
        {
            bytes = null;
            error = null;

            if (string.IsNullOrWhiteSpace(hexPayload))
            {
                error = "Cookie House raw payload is empty.";
                return false;
            }

            string normalized = hexPayload.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
            if ((normalized.Length & 1) != 0)
            {
                error = $"Invalid Cookie House raw payload: {hexPayload}";
                return false;
            }

            bytes = new byte[normalized.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    error = $"Invalid Cookie House raw payload: {hexPayload}";
                    bytes = null;
                    return false;
                }
            }

            return true;
        }

        private void EnqueueMessage(CookieHousePointInboxMessage message, string ingressMode)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastIngressMode = MapSimulatorNetworkIngressMode.IsKnown(ingressMode)
                ? ingressMode
                : MapSimulatorNetworkIngressMode.Proxy;
            switch (LastIngressMode)
            {
                case MapSimulatorNetworkIngressMode.Proxy:
                    ProxyIngressReceivedCount++;
                    break;

                case MapSimulatorNetworkIngressMode.Local:
                    LocalIngressReceivedCount++;
                    break;

            }
        }

    }
}

