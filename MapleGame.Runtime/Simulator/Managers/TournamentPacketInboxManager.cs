using System;
using System.Collections.Concurrent;
using System.Linq;
using HaCreator.MapSimulator.Fields;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class TournamentPacketInboxMessage
    {
        public TournamentPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "tournament-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for the tournament field wrapper.
    /// Each line is encoded as "<type> <hex-payload>", where type can be the numeric packet id
    /// or aliases such as "notice", "matchtable", "prize", "uew", or "noop".
    /// </summary>
    public sealed class TournamentPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<TournamentPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Tournament packet inbox ready for role-session/local ingress.";

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            if (packetType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
                packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            }

            _pendingMessages.Enqueue(new TournamentPacketInboxMessage(packetType, payload, source, $"{packetType}"));
            LastStatus = $"Queued {DescribePacketType(packetType)} from {source}.";
        }

        public void EnqueueProxy(TournamentPacketInboxMessage message)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {message.Source}.";
        }

        public bool TryDequeue(out TournamentPacketInboxMessage message)
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
                error = "Tournament inbox line is empty.";
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
                error = $"Unsupported Tournament packet type: {typeToken}";
                return false;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                payload = Array.Empty<byte>();
                return true;
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
                error = $"Invalid Tournament packet hex payload: {payloadToken}";
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
                error = "Tournament packetraw requires an opcode-wrapped hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(compactHex);
                if (rawPacket.Length < sizeof(ushort))
                {
                    error = "Tournament packetraw payload does not contain a supported opcode 374-378 packet.";
                    return false;
                }

                int opcode = BitConverter.ToUInt16(rawPacket, 0);
                if (opcode < (int)TournamentPacketType.Tournament || opcode > (int)TournamentPacketType.NoOp)
                {
                    error = "Tournament packetraw payload does not contain a supported opcode 374-378 packet.";
                    return false;
                }

                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(
                    opcode,
                    rawPacket.Skip(sizeof(ushort)).ToArray());
                packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Tournament packetraw hex payload: {payloadToken}";
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
                "374" or "tournament" or "notice" or "ontournament" => AssignPacketType(374, out packetType),
                "375" or "matchtable" or "match-table" or "bracket" or "ontournamentmatchtable" or "cmatchtabledlg" => AssignPacketType(375, out packetType),
                "376" or "setprize" or "set-prize" or "prize" or "ontournamentsetprize" => AssignPacketType(376, out packetType),
                "377" or "uew" or "ontournamentuew" => AssignPacketType(377, out packetType),
                "378" or "noop" => AssignPacketType(378, out packetType),
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
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"relay (CField::OnPacket {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                374 => "notice (374)",
                375 => "matchtable (375)",
                376 => "setprize (376)",
                377 => "uew (377)",
                378 => "noop (378)",
                _ => packetType.ToString()
            };
        }

        private static string RemoveWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            char[] buffer = new char[text.Length];
            int count = 0;
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                {
                    buffer[count++] = c;
                }
            }

            return new string(buffer, 0, count);
        }

    }
}
