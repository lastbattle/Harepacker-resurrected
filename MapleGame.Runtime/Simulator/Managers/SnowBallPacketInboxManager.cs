using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class SnowBallPacketInboxMessage
    {
        public SnowBallPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "snowball-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for SnowBall minigame packets.
    /// Inbound lines accept "<type> <hex-payload>" or "packetraw <hex>" where type can be
    /// the numeric packet id or the aliases "state" (338), "hit" (339), "msg" (340), and "touch" (341).
    /// Outbound local touch requests are emitted as "touch <team> <requestedAtTick> <sequence>"
    /// plus "packetoutraw <hex>" for the client opcode 256.
    /// </summary>
    public sealed class SnowBallPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18488;

        private const int PacketTypeState = SnowBallField.PacketTypeState;
        private const int PacketTypeHit = SnowBallField.PacketTypeHit;
        private const int PacketTypeMessage = SnowBallField.PacketTypeMessage;
        private const int PacketTypeTouch = SnowBallField.PacketTypeTouch;
        private const int OutboundTouchOpcode = SnowBallField.OutboundTouchOpcode;

        private readonly ConcurrentQueue<SnowBallPacketInboxMessage> _pendingMessages = new();
        private readonly RetiredMapleSocketState _socketState = new("SnowBall packet inbox", DefaultPort, "SnowBall packet inbox ready for role-session/local ingress.");

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public bool HasConnectedClients => _socketState.HasConnectedClients;
        public int ConnectedClientCount => _socketState.ConnectedClientCount;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus => _socketState.LastStatus;

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            _socketState.Stop("SnowBall packet inbox ready for role-session/local ingress.");
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            _pendingMessages.Enqueue(new SnowBallPacketInboxMessage(packetType, payload, source, $"{packetType}"));
        }

        public bool TryDequeue(out SnowBallPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendTouchRequest(SnowBallField.TouchPacketRequest request, out string status)
        {
            status = "SnowBall touch requests require the role-session bridge or local packet command path; loopback transport is retired.";
            _socketState.SetStatus(status);
            return false;
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = DescribePacketType(packetType);
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            _socketState.SetStatus(success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.");
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            bool parsed = TryParsePacketLine(text, out packetType, out payload, out bool ignored, out string message);
            error = parsed ? null : message;
            return parsed && !ignored;
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out bool ignored, out string message)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            ignored = false;
            message = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                message = "SnowBall inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (TryParseRawPacket(trimmed, out packetType, out payload, out ignored, out message))
            {
                return true;
            }

            if (ignored)
            {
                return false;
            }

            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                message = "SnowBall inbox line is empty.";
                return false;
            }

            if (tokens[0].Equals("touch", StringComparison.OrdinalIgnoreCase) && tokens.Length > 1)
            {
                ignored = true;
                message = "Ignored outbound SnowBall touch request metadata.";
                return false;
            }

            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(typeToken, out packetType))
            {
                message = $"Unsupported SnowBall packet type: {typeToken}";
                return false;
            }

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!TryParseRelayPayload(payloadToken, out payload, out message))
                {
                    packetType = 0;
                    return false;
                }

                return true;
            }

            if (packetType == PacketTypeTouch)
            {
                payload = Array.Empty<byte>();
                return true;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                message = "SnowBall packet requires a hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                return true;
            }
            catch (FormatException)
            {
                message = $"Invalid SnowBall packet hex payload: {payloadToken}";
                return false;
            }
        }

        private static bool TryParseRawPacket(string text, out int packetType, out byte[] payload, out bool ignored, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            ignored = false;
            error = null;

            string[] tokens = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || !tokens[0].Equals("packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TryParseHexPayload(tokens, 1, out byte[] rawPacket))
            {
                error = "SnowBall raw packet requires a decrypted packet hex payload.";
                return false;
            }

            if (rawPacket.Length < sizeof(short))
            {
                error = "SnowBall raw packet must include a 2-byte opcode.";
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
                error = $"SnowBall raw packet decode failed: {ex.Message}";
                return false;
            }

            if (packetType == OutboundTouchOpcode)
            {
                ignored = true;
                error = $"Ignored outbound SnowBall raw touch packet opcode: {OutboundTouchOpcode}.";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!TryValidateRelayPayload(payload, out error))
                {
                    packetType = 0;
                    payload = Array.Empty<byte>();
                    return false;
                }

                return true;
            }

            if (packetType != PacketTypeState
                && packetType != PacketTypeHit
                && packetType != PacketTypeMessage
                && packetType != PacketTypeTouch)
            {
                error = $"Unsupported SnowBall raw packet opcode: {packetType}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
            packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            return true;
        }

        private static bool TryParseRelayPayload(string payloadToken, out byte[] relayPayload, out string error)
        {
            relayPayload = Array.Empty<byte>();
            error = null;

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error =
                    $"SnowBall packet {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} requires a current-wrapper relay payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                relayPayload = Convert.FromHexString(compactHex);
            }
            catch (FormatException)
            {
                error = $"Invalid SnowBall relay packet hex payload: {payloadToken}";
                return false;
            }

            return TryValidateRelayPayload(relayPayload, out error);
        }

        private static bool TryValidateRelayPayload(byte[] relayPayload, out string error)
        {
            if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                    relayPayload,
                    out int wrapperPacketType,
                    out _,
                    out error))
            {
                return false;
            }

            if (wrapperPacketType != PacketTypeState
                && wrapperPacketType != PacketTypeHit
                && wrapperPacketType != PacketTypeMessage
                && wrapperPacketType != PacketTypeTouch)
            {
                error = $"Unsupported SnowBall current-wrapper relay packet opcode: {wrapperPacketType}";
                return false;
            }

            return true;
        }

        private static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToLowerInvariant();
            return normalized switch
            {
                "338" or "state" => AssignPacketType(PacketTypeState, out packetType),
                "339" or "hit" => AssignPacketType(PacketTypeHit, out packetType),
                "340" or "msg" or "message" => AssignPacketType(PacketTypeMessage, out packetType),
                "341" or "touch" => AssignPacketType(PacketTypeTouch, out packetType),
                _ => int.TryParse(normalized, out packetType)
            };
        }

        private static bool AssignPacketType(int value, out int packetType)
        {
            packetType = value;
            return true;
        }

        private static bool TryParseHexPayload(string[] tokens, int startIndex, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (tokens.Length <= startIndex)
            {
                return false;
            }

            string compactHex = RemoveWhitespace(string.Join(string.Empty, tokens.Skip(startIndex)));
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                return true;
            }
            catch (FormatException)
            {
                payload = Array.Empty<byte>();
                return false;
            }
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"CField::OnPacket relay ({SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                PacketTypeState => "SnowBall state packet (338)",
                PacketTypeHit => "SnowBall hit packet (339)",
                PacketTypeMessage => "SnowBall message packet (340)",
                PacketTypeTouch => "SnowBall touch packet (341)",
                _ => $"SnowBall packet {packetType}"
            };
        }

        private static string RemoveWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return string.Concat(value.Where(ch => !char.IsWhiteSpace(ch)));
        }

    }
}
