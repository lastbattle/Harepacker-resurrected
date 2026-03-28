using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public enum DojoPacketMessageKind
    {
        Energy,
        Clock,
        Stage,
        Clear,
        TimeOver,
        RawPacket
    }

    public sealed class DojoPacketInboxMessage
    {
        public DojoPacketInboxMessage(DojoPacketMessageKind kind, int value, string option, string source, string rawText, int packetType = -1, byte[] payload = null)
        {
            Kind = kind;
            Value = value;
            Option = option ?? string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "dojo-inbox" : source;
            RawText = rawText ?? string.Empty;
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
        }

        public DojoPacketMessageKind Kind { get; }
        public int Value { get; }
        public string Option { get; }
        public string Source { get; }
        public string RawText { get; }
        public int PacketType { get; }
        public byte[] Payload { get; }
    }

    /// <summary>
    /// Optional loopback inbox for Mu Lung Dojo runtime updates.
    /// Supported lines:
    /// - "energy <0-10000>"
    /// - "clock <seconds>"
    /// - "stage <0-32>"
    /// - "clear [auto|none|<nextMapId>]"
    /// - "timeover [<exitMapId>]"
    /// - "raw <packetType> <hex-payload>"
    /// - "<packetType> <hex-payload>"
    /// </summary>
    public sealed class DojoPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;

        private readonly ConcurrentQueue<DojoPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Dojo packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Dojo packet inbox already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Dojo packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Dojo packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Dojo packet inbox stopped.";
            }
        }

        public bool TryDequeue(out DojoPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, DojoPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result) ? DescribeMessage(message) : $"{DescribeMessage(message)}: {result}";
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

        public static bool TryParsePacketLine(
            string text,
            out DojoPacketMessageKind kind,
            out int value,
            out string option,
            out int packetType,
            out byte[] payload,
            out string error)
        {
            kind = DojoPacketMessageKind.Energy;
            value = 0;
            option = string.Empty;
            packetType = -1;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Dojo inbox line is empty.";
                return false;
            }

            string[] parts = text.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Dojo inbox line is empty.";
                return false;
            }

            string action = parts[0].Trim().ToLowerInvariant();
            if (action == "raw" || int.TryParse(parts[0], out packetType))
            {
                int payloadTokenIndex = action == "raw" ? 2 : 1;
                if (action == "raw")
                {
                    if (parts.Length < 3 || !int.TryParse(parts[1], out packetType) || packetType < 0)
                    {
                        error = "Dojo raw packet lines must be 'raw <packetType> <hex-payload>'.";
                        return false;
                    }
                }
                else if (parts.Length < 2)
                {
                    error = "Dojo raw packet lines must be '<packetType> <hex-payload>'.";
                    return false;
                }

                string hexPayload = parts.Length > payloadTokenIndex ? parts[payloadTokenIndex] : string.Empty;
                if (!TryParseHexPayload(hexPayload, out payload))
                {
                    error = "Dojo raw packet payload must be valid hex.";
                    return false;
                }

                kind = DojoPacketMessageKind.RawPacket;
                return true;
            }

            switch (action)
            {
                case "energy":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out value) || value < 0 || value > 10000)
                    {
                        error = "Dojo energy payload must be between 0 and 10000.";
                        return false;
                    }

                    kind = DojoPacketMessageKind.Energy;
                    return true;

                case "clock":
                case "timer":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out value) || value < 0)
                    {
                        error = "Dojo clock payload must be a non-negative number of seconds.";
                        return false;
                    }

                    kind = DojoPacketMessageKind.Clock;
                    return true;

                case "stage":
                    if (parts.Length < 2 || !int.TryParse(parts[1], out value) || value < 0 || value > 32)
                    {
                        error = "Dojo stage payload must be between 0 and 32.";
                        return false;
                    }

                    kind = DojoPacketMessageKind.Stage;
                    return true;

                case "clear":
                case "next":
                case "nextfloor":
                    kind = DojoPacketMessageKind.Clear;
                    if (parts.Length >= 2)
                    {
                        option = parts[1];
                        if (!string.Equals(option, "auto", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(option, "none", StringComparison.OrdinalIgnoreCase)
                            && (!int.TryParse(option, out value) || value <= 0))
                        {
                            error = "Dojo clear payload must be 'auto', 'none', or a positive map id.";
                            return false;
                        }
                    }

                    return true;

                case "timeover":
                case "timeout":
                    kind = DojoPacketMessageKind.TimeOver;
                    if (parts.Length >= 2)
                    {
                        option = parts[1];
                        if (!int.TryParse(option, out value) || value <= 0)
                        {
                            error = "Dojo time-over payload must be a positive map id when provided.";
                            return false;
                        }
                    }

                    return true;

                default:
                    error = $"Unsupported Dojo inbox action: {parts[0]}";
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
                LastStatus = $"Dojo packet inbox error: {ex.Message}";
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

                        if (!TryParsePacketLine(line, out DojoPacketMessageKind kind, out int value, out string option, out int packetType, out byte[] payload, out string error))
                        {
                            LastStatus = $"Ignored Dojo inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new DojoPacketInboxMessage(kind, value, option, remoteEndpoint, line, packetType, payload));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribeMessage(kind, value, option, packetType, payload)} from {remoteEndpoint}.";
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
                LastStatus = $"Dojo packet inbox client error: {ex.Message}";
            }
        }

        private static string DescribeMessage(DojoPacketInboxMessage message)
        {
            return DescribeMessage(message.Kind, message.Value, message.Option, message.PacketType, message.Payload);
        }

        private static string DescribeMessage(DojoPacketMessageKind kind, int value, string option, int packetType, byte[] payload)
        {
            return kind switch
            {
                DojoPacketMessageKind.Energy => $"Dojo energy {value}",
                DojoPacketMessageKind.Clock => $"Dojo clock {value}s",
                DojoPacketMessageKind.Stage => $"Dojo stage {value}",
                DojoPacketMessageKind.Clear => string.IsNullOrWhiteSpace(option) ? "Dojo clear" : $"Dojo clear {option}",
                DojoPacketMessageKind.TimeOver => string.IsNullOrWhiteSpace(option) ? "Dojo time-over" : $"Dojo time-over {option}",
                DojoPacketMessageKind.RawPacket => $"Dojo packet {packetType} ({payload?.Length ?? 0} bytes)",
                _ => "Dojo packet"
            };
        }

        private static bool TryParseHexPayload(string text, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Trim();

            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
            }

            if ((normalized.Length & 1) != 0)
            {
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                return false;
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

            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
            }
        }
    }
}
