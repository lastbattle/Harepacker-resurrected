using System;
using System.Globalization;
using System.Collections.Concurrent;
using System.IO;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class GuildBossPacketInboxMessage
    {
        public GuildBossPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "guildboss-transport" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback transport seam for Guild Boss packet flow.
    /// Inbound lines accept "344 <hex>", "345 <hex>", "healer <y>", "pulley <state>",
    /// or a decrypted Maple packet via "packetraw <hex>".
    /// Outbound pulley requests are emitted as "pulleyhit &lt;sequence&gt; &lt;tickCount&gt;".
    /// </summary>
    public sealed class GuildBossPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18486;
        private const int PacketTypeHealerMove = 344;
        private const int PacketTypePulleyStateChange = 345;
        private const int OutboundPulleyRequestOpcode = 259;
        private const string PulleyRequestPacketHex = "0301";

        private readonly ConcurrentQueue<GuildBossPacketInboxMessage> _pendingMessages = new();
        private readonly RetiredMapleSocketState _socketState = new("Guild boss transport", DefaultPort, "Guild boss transport inactive.");

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public bool HasConnectedClients => _socketState.HasConnectedClients;
        public int ConnectedClientCount => _socketState.ConnectedClientCount;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus => _socketState.LastStatus;

        public string DescribeStatus()
        {
            return _socketState.Describe(ReceivedCount, SentCount);
        }

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            StopInternal(clearPending: true);
            _socketState.SetStatus("GuildBoss packet transport listener already retired.");
        }

        public bool TryDequeue(out GuildBossPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendPulleyRequest(GuildBossField.PulleyPacketRequest request, out string status)
        {
            status = "Guild boss pulley requests require the role-session bridge or local packet command path; loopback transport is retired.";
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
            StopInternal(clearPending: true);
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
                message = "Guild boss transport line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                message = "Guild boss transport line is empty.";
                return false;
            }

            if (TryParseRawPacket(tokens, out packetType, out payload, out ignored, out message))
            {
                return true;
            }

            if (ignored)
            {
                return false;
            }

            if (!TryParsePacketType(tokens[0], out packetType))
            {
                message = $"Unsupported guild boss packet type: {tokens[0]}";
                return false;
            }

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!TryParseRelayPayload(tokens, 1, out payload, out message))
                {
                    packetType = 0;
                    return false;
                }

                return true;
            }

            if (packetType == OutboundPulleyRequestOpcode)
            {
                ignored = true;
                message = $"Ignored outbound guild boss pulley request opcode: {OutboundPulleyRequestOpcode}.";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            if (packetType == PacketTypeHealerMove)
            {
                if (TryParseSignedShortPayload(tokens, 1, out payload))
                {
                    return true;
                }

                message = "Guild boss healer packet requires either a signed Y value or a hex payload.";
                return false;
            }

            if (TryParseBytePayload(tokens, 1, out payload))
            {
                return true;
            }

            message = "Guild boss pulley packet requires either a state byte or a hex payload.";
            return false;
        }

        private static bool TryParseRawPacket(string[] tokens, out int packetType, out byte[] payload, out bool ignored, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            ignored = false;
            error = null;

            if (tokens.Length == 0
                || !string.Equals(tokens[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!TryParseHexPayload(tokens, 1, out byte[] rawPacket))
            {
                error = "Guild boss raw packet requires a decrypted packet hex payload.";
                return false;
            }

            if (rawPacket.Length < sizeof(short))
            {
                error = "Guild boss raw packet must include a 2-byte opcode.";
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
                error = $"Guild boss raw packet decode failed: {ex.Message}";
                return false;
            }

            if (packetType == OutboundPulleyRequestOpcode)
            {
                ignored = true;
                error = $"Ignored outbound guild boss raw pulley request opcode: {OutboundPulleyRequestOpcode}.";
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

            if (packetType != PacketTypeHealerMove && packetType != PacketTypePulleyStateChange)
            {
                error = $"Unsupported guild boss raw packet opcode: {packetType}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
            packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            return true;
        }

        private static bool TryParseRelayPayload(string[] tokens, int valueIndex, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (!TryParseHexPayload(tokens, valueIndex, out payload))
            {
                error =
                    $"Guild boss packet {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} requires a current-wrapper relay payload.";
                return false;
            }

            return TryValidateRelayPayload(payload, out error);
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

            if (wrapperPacketType != PacketTypeHealerMove && wrapperPacketType != PacketTypePulleyStateChange)
            {
                error = $"Unsupported guild boss current-wrapper relay packet opcode: {wrapperPacketType}";
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
                "344" or "healer" => AssignPacketType(PacketTypeHealerMove, out packetType),
                "345" or "pulley" => AssignPacketType(PacketTypePulleyStateChange, out packetType),
                _ => int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType)
            };
        }

        private static bool AssignPacketType(int value, out int packetType)
        {
            packetType = value;
            return true;
        }

        private static bool TryParseSignedShortPayload(string[] tokens, int valueIndex, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (tokens.Length <= valueIndex)
            {
                return false;
            }

            if (short.TryParse(tokens[valueIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out short signedValue))
            {
                payload = BitConverter.GetBytes(signedValue);
                return true;
            }

            return TryParseHexPayload(tokens, valueIndex, out payload);
        }

        private static bool TryParseBytePayload(string[] tokens, int valueIndex, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (tokens.Length <= valueIndex)
            {
                return false;
            }

            if (byte.TryParse(tokens[valueIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte stateValue))
            {
                payload = new[] { stateValue };
                return true;
            }

            return TryParseHexPayload(tokens, valueIndex, out payload);
        }

        private static bool TryParseHexPayload(string[] tokens, int valueIndex, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            string compactHex = RemoveWhitespace(string.Join(string.Empty, tokens[valueIndex..]));
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
                return false;
            }
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"current-wrapper relay ({SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                PacketTypeHealerMove => "healer (344)",
                PacketTypePulleyStateChange => "pulley (345)",
                _ => packetType.ToString(CultureInfo.InvariantCulture)
            };
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

        private void StopInternal(bool clearPending)
        {
            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }
    }
}
