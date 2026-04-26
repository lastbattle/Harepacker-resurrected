using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class CoconutPacketInboxMessage
    {
        public CoconutPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "coconut-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int? LocalTeam { get; init; }
        public bool IsLocalTeamUpdate => LocalTeam.HasValue;

        public static CoconutPacketInboxMessage CreateLocalTeamUpdate(int localTeam, string source, string rawText)
        {
            return new CoconutPacketInboxMessage(0, Array.Empty<byte>(), source, rawText)
            {
                LocalTeam = localTeam == 1 ? 1 : 0
            };
        }
    }

    /// <summary>
    /// Adapter inbox for Coconut minigame packets.
    /// Inbound lines accept "<type> <hex-payload>", where type can be the numeric packet id
    /// or the aliases "hit" (342) and "score" (343).
    /// Outbound normal-attack requests are emitted as "attack <targetId> <delayMs> <requestedAtTick>".
    /// </summary>
    public sealed class CoconutPacketInboxManager : IDisposable
    {
        private const int OutboundNormalAttackOpcode = 257;
        public const int DefaultPort = 18486;
        private const int PacketTypeHit = 342;
        private const int PacketTypeScore = 343;

        private readonly ConcurrentQueue<CoconutPacketInboxMessage> _pendingMessages = new();
        private readonly RetiredMapleSocketState _socketState = new("Coconut packet inbox", DefaultPort, "Coconut packet inbox ready for role-session/local ingress.");
        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public bool HasConnectedClients => _socketState.HasConnectedClients;
        public int ConnectedClientCount => _socketState.ConnectedClientCount;
        public int ReceivedCount { get; private set; }
        public int ProxyIngressReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastIngressMode { get; private set; } = "none";
        public int SentCount { get; private set; }
        public string LastStatus => _socketState.LastStatus;

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            _socketState.Stop("Coconut packet inbox ready for role-session/local ingress.");
        }

        public void EnqueueProxy(CoconutPacketInboxMessage message)
        {
            EnqueueMessage(
                message,
                MapSimulatorNetworkIngressMode.Proxy,
                message?.Source);
        }

        public void EnqueueProxy(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "coconut-proxy" : source;
            EnqueueMessage(
                new CoconutPacketInboxMessage(packetType, payload, packetSource, $"{packetType}"),
                MapSimulatorNetworkIngressMode.Proxy,
                packetSource);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "coconut-local" : source;
            EnqueueMessage(
                new CoconutPacketInboxMessage(packetType, payload, packetSource, $"{packetType}"),
                MapSimulatorNetworkIngressMode.Local,
                packetSource);
        }

        public void EnqueueLocalTeamUpdate(int localTeam, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "coconut-local" : source;
            EnqueueMessage(
                CoconutPacketInboxMessage.CreateLocalTeamUpdate(localTeam, packetSource, $"team {localTeam}"),
                MapSimulatorNetworkIngressMode.Local,
                packetSource);
        }

        public bool TryDequeue(out CoconutPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendAttackRequest(CoconutField.AttackPacketRequest request, out string status)
        {
            status = "Coconut attack requests require the role-session bridge or local packet command path; loopback transport is retired.";
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
            bool parsed = TryParsePacketLine(text, out packetType, out payload, out _, out string message);
            error = parsed ? null : message;
            return parsed;
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out bool ignored, out string message)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            ignored = false;
            message = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                message = "Coconut inbox line is empty.";
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

            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(typeToken, out packetType))
            {
                message = $"Unsupported Coconut packet type: {typeToken}";
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

            if (packetType == OutboundNormalAttackOpcode)
            {
                ignored = true;
                message = $"Ignored outbound Coconut attack packet opcode: {OutboundNormalAttackOpcode}.";
                return false;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                message = "Coconut packet requires a hex payload.";
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
                message = $"Invalid Coconut packet hex payload: {payloadToken}";
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
            if (tokens.Length == 0 || !string.Equals(tokens[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TryParseHexPayload(tokens, 1, out byte[] rawPacket))
            {
                error = "Coconut raw packet requires a decrypted packet hex payload.";
                return false;
            }

            if (rawPacket.Length < sizeof(short))
            {
                error = "Coconut raw packet must include a 2-byte opcode.";
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
                error = $"Coconut raw packet decode failed: {ex.Message}";
                return false;
            }

            if (packetType == OutboundNormalAttackOpcode)
            {
                ignored = true;
                error = $"Ignored outbound Coconut raw attack packet opcode: {OutboundNormalAttackOpcode}.";
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

            if (packetType != PacketTypeHit && packetType != PacketTypeScore)
            {
                error = $"Unsupported Coconut raw packet opcode: {packetType}";
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
                    $"Coconut packet {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} requires a current-wrapper relay payload.";
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
                error = $"Invalid Coconut relay packet hex payload: {payloadToken}";
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

            if (wrapperPacketType != PacketTypeHit && wrapperPacketType != PacketTypeScore)
            {
                error = $"Unsupported Coconut current-wrapper relay packet opcode: {wrapperPacketType}";
                return false;
            }

            return true;
        }

        public static bool TryParseTransportLine(string text, string source, out CoconutPacketInboxMessage message, out bool ignored, out string error)
        {
            message = null;
            ignored = false;
            error = null;

            if (TryParseTeamLine(text, out int localTeam, out error))
            {
                message = CoconutPacketInboxMessage.CreateLocalTeamUpdate(localTeam, source, text);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            if (!TryParsePacketLine(text, out int packetType, out byte[] payload, out ignored, out error))
            {
                return false;
            }

            message = new CoconutPacketInboxMessage(packetType, payload, source, text);
            return true;
        }

        private static bool TryParseTeamLine(string text, out int localTeam, out string error)
        {
            localTeam = 0;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] tokens = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || !string.Equals(tokens[0], "team", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (tokens.Length != 2)
            {
                error = "Coconut team line usage: team <maple|story|0|1>.";
                return false;
            }

            string teamToken = tokens[1].Trim().ToLowerInvariant();
            if (teamToken == "maple" || teamToken == "0")
            {
                localTeam = 0;
                return true;
            }

            if (teamToken == "story" || teamToken == "1")
            {
                localTeam = 1;
                return true;
            }

            error = "Coconut team line usage: team <maple|story|0|1>.";
            return false;
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
                "342" or "hit" => AssignPacketType(PacketTypeHit, out packetType),
                "343" or "score" => AssignPacketType(PacketTypeScore, out packetType),
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
                0 => "Coconut local-team update",
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"CField::OnPacket relay ({SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                PacketTypeHit => "Coconut hit packet (342)",
                PacketTypeScore => "Coconut score packet (343)",
                _ => $"Coconut packet {packetType}"
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

        private void EnqueueMessage(CoconutPacketInboxMessage message, string ingressMode, string statusSource)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastIngressMode = ingressMode;
            if (!MapSimulatorNetworkIngressMode.IsKnown(LastIngressMode))
            {
                LastIngressMode = MapSimulatorNetworkIngressMode.Proxy;
            }

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
            _socketState.SetStatus(message.IsLocalTeamUpdate
                ? $"Queued Coconut local team {(message.LocalTeam == 1 ? "Story" : "Maple")} from {source}."
                : $"Queued {DescribePacketType(message.PacketType)} from {source}.");
        }
    }
}
