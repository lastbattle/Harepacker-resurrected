using System;
using System.Collections.Concurrent;
using HaCreator.MapSimulator.Fields;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MonsterCarnivalPacketInboxMessage
    {
        public MonsterCarnivalPacketInboxMessage(int packetType, byte[] payload, string source, string rawText, int? relayedPacketType = null)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "mcarnival-inbox" : source;
            RawText = rawText ?? string.Empty;
            RelayedPacketType = relayedPacketType;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int? RelayedPacketType { get; }
        public int OwnerPacketType => RelayedPacketType ?? PacketType;
    }

    /// <summary>
    /// Adapter inbox for live Monster Carnival field packets.
    /// Each line is encoded as "<type> <hex-payload>", where type can be the numeric packet id
    /// or aliases such as "enter", "personalcp", "teamcp", "requestresult", "requestfail",
    /// "death", "memberout", and "result".
    /// </summary>
    public sealed class MonsterCarnivalPacketInboxManager : IDisposable
    {
        private const int PacketTypeEnter = 346;
        private const int PacketTypePersonalCp = 347;
        private const int PacketTypeTeamCp = 348;
        private const int PacketTypeRequestResult = 349;
        private const int PacketTypeRequestFailure = 350;
        private const int PacketTypeProcessForDeath = 351;
        private const int PacketTypeShowMemberOutMessage = 352;
        private const int PacketTypeGameResult = 353;

        private readonly ConcurrentQueue<MonsterCarnivalPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Monster Carnival packet inbox ready for role-session/local ingress.";

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            int ownerPacketType = packetType;
            if (packetType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
                packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            }
            else if (!TryResolveRelayedPacketType(payload, out ownerPacketType))
            {
                ownerPacketType = packetType;
            }

            _pendingMessages.Enqueue(new MonsterCarnivalPacketInboxMessage(packetType, payload, source, $"{packetType}", relayedPacketType: ownerPacketType));
        }

        public void EnqueueProxy(MonsterCarnivalPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {DescribePacketType(message.OwnerPacketType)} from {message.Source}.";
        }

        public bool TryDequeue(out MonsterCarnivalPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = DescribePacketType(packetType);
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Monster Carnival inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (typeToken.Equals("packetraw", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("wrapped", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("opcode", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseWrappedPacketLine(payloadToken, out packetType, out payload, out error);
            }

            if (!TryParsePacketType(typeToken, out packetType))
            {
                error = $"Unsupported Monster Carnival packet type: {typeToken}";
                return false;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Monster Carnival packet requires a hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
                packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Monster Carnival packet hex payload: {payloadToken}";
                return false;
            }
        }

        private static bool TryParseWrappedPacketLine(string payloadToken, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Monster Carnival packetraw requires an opcode-wrapped hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(compactHex);
                if (!MonsterCarnivalOfficialSessionBridgeManager.TryDecodeInboundCarnivalPacket(rawPacket, "mcarnival-inbox", out MonsterCarnivalPacketInboxMessage message))
                {
                    error = "Monster Carnival packetraw payload does not contain a supported opcode 346-353 packet.";
                    return false;
                }

                packetType = message.PacketType;
                payload = message.Payload;
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Monster Carnival packetraw hex payload: {payloadToken}";
                return false;
            }
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
                "346" or "enter" => AssignPacketType(PacketTypeEnter, out packetType),
                "347" or "personalcp" or "personal" => AssignPacketType(PacketTypePersonalCp, out packetType),
                "348" or "teamcp" or "team" => AssignPacketType(PacketTypeTeamCp, out packetType),
                "349" or "requestresult" or "requestok" => AssignPacketType(PacketTypeRequestResult, out packetType),
                "350" or "requestfailure" or "requestfail" or "fail" => AssignPacketType(PacketTypeRequestFailure, out packetType),
                "351" or "processfordeath" or "death" => AssignPacketType(PacketTypeProcessForDeath, out packetType),
                "352" or "showmemberoutmessage" or "memberout" => AssignPacketType(PacketTypeShowMemberOutMessage, out packetType),
                "353" or "gameresult" or "result" => AssignPacketType(PacketTypeGameResult, out packetType),
                _ => int.TryParse(normalized, out packetType)
            };
        }

        private static bool AssignPacketType(int value, out int packetType)
        {
            packetType = value;
            return true;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"CField::OnPacket relay ({SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                PacketTypeEnter => "enter (346)",
                PacketTypePersonalCp => "personalcp (347)",
                PacketTypeTeamCp => "teamcp (348)",
                PacketTypeRequestResult => "requestresult (349)",
                PacketTypeRequestFailure => "requestfailure (350)",
                PacketTypeProcessForDeath => "processfordeath (351)",
                PacketTypeShowMemberOutMessage => "memberout (352)",
                PacketTypeGameResult => "gameresult (353)",
                _ => packetType.ToString()
            };
        }

        private static bool TryResolveRelayedPacketType(byte[] relayPayload, out int packetType)
        {
            return MonsterCarnivalOfficialSessionBridgeManager.TryDecodeCarnivalPacketFromRelayPrefixChain(
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                relayPayload,
                out packetType,
                out _);
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

    }
}
