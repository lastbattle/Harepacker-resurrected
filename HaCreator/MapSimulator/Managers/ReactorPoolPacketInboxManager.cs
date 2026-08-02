using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ReactorPoolPacketInboxMessage
    {
        public ReactorPoolPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "reactorpacket-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class ReactorPoolPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<ReactorPoolPacketInboxMessage> _pendingMessages = new();
        public int ReceivedCount { get; private set; }
        public int ProxyIngressReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastIngressMode { get; private set; } = "none";
        public string LastStatus { get; private set; } = "Reactor packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out ReactorPoolPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "reactorpacket-local" : source;
            EnqueueMessage(
                new ReactorPoolPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                MapSimulatorNetworkIngressMode.Local,
                packetSource);
        }

        public void EnqueueProxy(ReactorPoolPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            EnqueueMessage(message, MapSimulatorNetworkIngressMode.Proxy, message.Source);
        }

        public void RecordDispatchResult(ReactorPoolPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "reactorpacket-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "reactorpacket-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out ReactorPoolPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Reactor inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/reactorpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/reactorpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Reactor inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported reactor packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new ReactorPoolPacketInboxMessage(packetType, payload, "reactorpacket-inbox", text);
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
                    && Enum.IsDefined(typeof(PacketReactorPoolPacketKind), (PacketReactorPoolPacketKind)packetType);
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return Enum.IsDefined(typeof(PacketReactorPoolPacketKind), (PacketReactorPoolPacketKind)packetType);
            }

            packetType = trimmed.ToLowerInvariant() switch
            {
                "changestate" or "change" => (int)PacketReactorPoolPacketKind.ChangeState,
                "move" => (int)PacketReactorPoolPacketKind.Move,
                "enter" or "enterfield" => (int)PacketReactorPoolPacketKind.EnterField,
                "leave" or "leavefield" => (int)PacketReactorPoolPacketKind.LeaveField,
                _ => 0
            };
            return packetType != 0;
        }

        public static string DescribePacketType(int packetType)
        {
            return Enum.IsDefined(typeof(PacketReactorPoolPacketKind), (PacketReactorPoolPacketKind)packetType)
                ? $"{(PacketReactorPoolPacketKind)packetType} (0x{packetType:X})"
                : $"packet {packetType}";
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Reactor client packet must include a 2-byte opcode.";
                return false;
            }

            packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (!Enum.IsDefined(typeof(PacketReactorPoolPacketKind), (PacketReactorPoolPacketKind)packetType))
            {
                error = $"Unsupported reactor client opcode {packetType}.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
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
                return TryDecodeHexBytes(hex, out payload, out error);
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
                    error = "Reactor inbox Base64 payload is invalid.";
                    return false;
                }
            }

            error = "Reactor inbox payload must use payloadhex=.. or payloadb64=..";
            return false;
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
                error = "Reactor inbox hex payload must contain an even number of characters.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                error = "Reactor inbox hex payload is invalid.";
                return false;
            }
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

        private void EnqueueMessage(ReactorPoolPacketInboxMessage message, string ingressMode, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastIngressMode = string.IsNullOrWhiteSpace(ingressMode) ? "unknown" : ingressMode;
            switch (LastIngressMode)
            {
                case MapSimulatorNetworkIngressMode.Proxy:
                    ProxyIngressReceivedCount++;
                    break;
                case MapSimulatorNetworkIngressMode.Local:
                    LocalIngressReceivedCount++;
                    break;
            }

            string packetSource = string.IsNullOrWhiteSpace(sourceLabel) ? "reactorpacket-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {packetSource} via {LastIngressMode}.";
        }
    }
}

