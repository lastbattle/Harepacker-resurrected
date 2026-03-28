using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
    /// Loopback inbox for Massacre packet and context ownership seams.
    /// </summary>
    public sealed class MassacrePacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;

        private readonly ConcurrentQueue<MassacrePacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Massacre packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Massacre packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal(clearPending: true);

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Massacre packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Massacre packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Massacre packet inbox stopped.";
            }
        }

        public bool TryDequeue(out MassacrePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
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
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
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

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Massacre packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParsePacketLine(line, out MassacrePacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored Massacre inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new MassacrePacketInboxMessage(
                            message.Kind,
                            remoteEndpoint,
                            line,
                            message.Value1,
                            message.Value2,
                            message.Value3,
                            message.Value4,
                            message.PacketType,
                            message.Payload,
                            message.ClearResult,
                            message.HasScoreOverride,
                            message.HasRankOverride,
                            message.Rank));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribeMessage(message)} from {remoteEndpoint}.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Massacre packet inbox client error: {ex.Message}";
            }
        }

        private void StopInternal(bool clearPending)
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
            }
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
