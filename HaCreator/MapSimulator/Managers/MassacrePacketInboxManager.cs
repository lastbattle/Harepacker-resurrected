using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using HaCreator.MapSimulator.Fields;

namespace HaCreator.MapSimulator.Managers
{
    public enum MassacrePacketInboxMessageKind
    {
        Clock,
        ClockPayload,
        Info,
        InfoPayload,
        IncGauge,
        Stage,
        Bonus,
        Result,
        Packet
    }

    public sealed class MassacrePacketInboxMessage
    {
        public MassacrePacketInboxMessage(
            MassacrePacketInboxMessageKind kind,
            string source,
            string rawText,
            int value1 = 0,
            int value2 = 0,
            int value3 = 0,
            int value4 = 0,
            int packetType = -1,
            byte[] payload = null,
            bool clearResult = false,
            bool hasScoreOverride = false,
            bool hasRankOverride = false,
            char rank = 'D')
        {
            Kind = kind;
            Source = string.IsNullOrWhiteSpace(source) ? "massacre-inbox" : source;
            RawText = rawText ?? string.Empty;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            Value4 = value4;
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            ClearResult = clearResult;
            HasScoreOverride = hasScoreOverride;
            HasRankOverride = hasRankOverride;
            Rank = rank;
        }

        public MassacrePacketInboxMessageKind Kind { get; }
        public string Source { get; }
        public string RawText { get; }
        public int Value1 { get; }
        public int Value2 { get; }
        public int Value3 { get; }
        public int Value4 { get; }
        public int PacketType { get; }
        public byte[] Payload { get; }
        public bool ClearResult { get; }
        public bool HasScoreOverride { get; }
        public bool HasRankOverride { get; }
        public char Rank { get; }
    }

    /// <summary>
    /// Adapter inbox for Massacre packet and context ownership seams.
    /// </summary>
    public sealed class MassacrePacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<MassacrePacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Massacre packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out MassacrePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(MassacrePacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void EnqueueProxy(MassacrePacketInboxMessage message)
        {
            EnqueueMessage(message, message?.Source);
        }

        public void RecordDispatchResult(string source, MassacrePacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result)
                ? DescribeMessage(message)
                : $"{DescribeMessage(message)}: {result}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out MassacrePacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Massacre inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string[] parts = trimmed.Split((char[])null, 5, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Massacre inbox line is empty.";
                return false;
            }

            if (TryParseRawPacket(trimmed, parts, out message, out error))
            {
                return true;
            }

            switch (parts[0].Trim().ToLowerInvariant())
            {
                case "clock":
                case "timer":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int seconds) || seconds < 0)
                    {
                        error = "Massacre clock payload must be a non-negative number of seconds.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Clock, "massacre-inbox", trimmed, value1: seconds);
                    return true;

                case "clockraw":
                case "timerraw":
                    if (parts.Length < 2 || !TryParseHexPayload(parts[1], out byte[] clockPayload))
                    {
                        error = "Massacre raw clock payload must be valid hex.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.ClockPayload, "massacre-inbox", trimmed, payload: clockPayload);
                    return true;

                case "info":
                case "context":
                    if (parts.Length < 4
                        || !int.TryParse(parts[1], out int hit)
                        || !int.TryParse(parts[2], out int miss)
                        || !int.TryParse(parts[3], out int cool)
                        || hit < 0
                        || miss < 0
                        || cool < 0)
                    {
                        error = "Massacre info payload must be 'info <hit> <miss> <cool> [skill]'.";
                        return false;
                    }

                    int skill = 0;
                    if (parts.Length >= 5 && (!int.TryParse(parts[4], out skill) || skill < 0))
                    {
                        error = "Massacre info payload must be 'info <hit> <miss> <cool> [skill]'.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Info, "massacre-inbox", trimmed, hit, miss, cool, skill);
                    return true;

                case "inforaw":
                case "contextraw":
                    if (parts.Length < 2 || !TryParseHexPayload(parts[1], out byte[] infoPayload))
                    {
                        error = "Massacre raw info payload must be valid hex.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.InfoPayload, "massacre-inbox", trimmed, payload: infoPayload);
                    return true;

                case "inc":
                case "gauge":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int incGauge) || incGauge < 0)
                    {
                        error = "Massacre inc payload must be a non-negative integer.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.IncGauge, "massacre-inbox", trimmed, value1: incGauge);
                    return true;

                case "stage":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int stage) || stage <= 0)
                    {
                        error = "Massacre stage payload must be a positive integer.";
                        return false;
                    }

                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Stage, "massacre-inbox", trimmed, value1: stage);
                    return true;

                case "bonus":
                    message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Bonus, "massacre-inbox", trimmed);
                    return true;

                case "result":
                    if (parts.Length < 2)
                    {
                        error = "Massacre result payload must be 'result <clear|fail> [score] [rank]'.";
                        return false;
                    }

                    bool clearResult;
                    if (string.Equals(parts[1], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        clearResult = true;
                    }
                    else if (string.Equals(parts[1], "fail", StringComparison.OrdinalIgnoreCase))
                    {
                        clearResult = false;
                    }
                    else
                    {
                        error = "Massacre result mode must be clear or fail.";
                        return false;
                    }

                    bool hasScoreOverride = false;
                    int scoreOverride = 0;
                    if (parts.Length >= 3)
                    {
                        if (!int.TryParse(parts[2], out scoreOverride) || scoreOverride < 0)
                        {
                            error = "Massacre result score must be a non-negative integer.";
                            return false;
                        }

                        hasScoreOverride = true;
                    }

                    bool hasRankOverride = false;
                    char rank = 'D';
                    if (parts.Length >= 4)
                    {
                        if (parts[3].Length != 1)
                        {
                            error = "Massacre result rank must be a single letter.";
                            return false;
                        }

                        rank = parts[3][0];
                        hasRankOverride = true;
                    }

                    message = new MassacrePacketInboxMessage(
                        MassacrePacketInboxMessageKind.Result,
                        "massacre-inbox",
                        trimmed,
                        value1: scoreOverride,
                        clearResult: clearResult,
                        hasScoreOverride: hasScoreOverride,
                        hasRankOverride: hasRankOverride,
                        rank: rank);
                    return true;

                default:
                    error = $"Unsupported Massacre inbox action: {parts[0]}";
                    return false;
            }
        }

        private void EnqueueMessage(MassacrePacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string source = string.IsNullOrWhiteSpace(sourceLabel) ? message.Source : sourceLabel;
            LastStatus = $"Queued {DescribeMessage(message)} from {source}.";
        }

        private static bool TryParseRawPacket(string trimmed, string[] parts, out MassacrePacketInboxMessage message, out string error)
        {
            message = null;
            error = null;
            string action = parts[0].Trim().ToLowerInvariant();

            if (action == "packetraw")
            {
                if (parts.Length < 2 || !TryParseHexPayload(parts[1], out byte[] rawPacket) || rawPacket.Length < sizeof(short))
                {
                    error = "Massacre opcode-wrapped packet must include a valid hex payload with a 2-byte opcode.";
                    return false;
                }

                int packetType = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket);
                byte[] payload = rawPacket.Length == sizeof(short) ? Array.Empty<byte>() : rawPacket[sizeof(short)..];
                if (packetType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
                {
                    payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
                    packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
                }

                message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Packet, "massacre-inbox", trimmed, packetType: packetType, payload: payload);
                return true;
            }

            if (action == "raw")
            {
                if (parts.Length < 3 || !int.TryParse(parts[1], out int packetType) || packetType < 0 || !TryParseHexPayload(parts[2], out byte[] payload))
                {
                    error = "Massacre raw packet lines must be 'raw <packetType> <hex-payload>'.";
                    return false;
                }

                if (packetType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
                {
                    payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
                    packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
                }

                message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Packet, "massacre-inbox", trimmed, packetType: packetType, payload: payload);
                return true;
            }

            if (!int.TryParse(parts[0], out int barePacketType) || barePacketType < 0)
            {
                return false;
            }

            if (parts.Length < 2 || !TryParseHexPayload(parts[1], out byte[] barePayload))
            {
                error = "Massacre raw packet lines must be '<packetType> <hex-payload>'.";
                return false;
            }

            if (barePacketType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                barePayload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(barePacketType, barePayload);
                barePacketType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            }

            message = new MassacrePacketInboxMessage(MassacrePacketInboxMessageKind.Packet, "massacre-inbox", trimmed, packetType: barePacketType, payload: barePayload);
            return true;
        }

        private static bool TryParseHexPayload(string text, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            string compactHex = string.Concat((text ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
            if (string.IsNullOrWhiteSpace(compactHex) || (compactHex.Length % 2) != 0)
            {
                return false;
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                return true;
            }
            catch
            {
                payload = Array.Empty<byte>();
                return false;
            }
        }

        private static string DescribeMessage(MassacrePacketInboxMessage message)
        {
            if (message == null)
            {
                return "Massacre inbox message";
            }

            return message.Kind switch
            {
                MassacrePacketInboxMessageKind.Clock => $"Massacre clock {message.Value1}s",
                MassacrePacketInboxMessageKind.ClockPayload => "Massacre raw clock payload",
                MassacrePacketInboxMessageKind.Info => $"Massacre info {message.Value1}/{message.Value3}/{message.Value2}/{message.Value4}",
                MassacrePacketInboxMessageKind.InfoPayload => "Massacre raw info payload",
                MassacrePacketInboxMessageKind.IncGauge => $"Massacre inc gauge {message.Value1}",
                MassacrePacketInboxMessageKind.Stage => $"Massacre stage {message.Value1}",
                MassacrePacketInboxMessageKind.Bonus => "Massacre bonus presentation",
                MassacrePacketInboxMessageKind.Result => $"Massacre result {(message.ClearResult ? "clear" : "fail")}",
                MassacrePacketInboxMessageKind.Packet => $"Massacre packet {message.PacketType}",
                _ => "Massacre inbox message"
            };
        }
    }
}
