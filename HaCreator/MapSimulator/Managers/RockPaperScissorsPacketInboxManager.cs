using HaCreator.MapSimulator.Fields;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class RockPaperScissorsPacketInboxMessage
    {
        public RockPaperScissorsPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "rps-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Optional loopback inbox for CRPSGameDlg ownership packets.
    /// Each line is encoded as "<subtype> <hex-payload>" or
    /// "packetraw <opcode-wrapped-hex>".
    /// </summary>
    public sealed class RockPaperScissorsPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18491;

        private readonly ConcurrentQueue<RockPaperScissorsPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Rock-Paper-Scissors packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Rock-Paper-Scissors packet inbox already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Rock-Paper-Scissors packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Rock-Paper-Scissors packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Rock-Paper-Scissors packet inbox stopped.";
            }
        }

        public bool TryDequeue(out RockPaperScissorsPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message)
                ? DescribePacketType(packetType)
                : $"{DescribePacketType(packetType)}: {message}";
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

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Rock-Paper-Scissors inbox line is empty.";
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

            if (!RockPaperScissorsField.TryParsePacketType(typeToken, out packetType))
            {
                error = $"Unsupported Rock-Paper-Scissors packet subtype: {typeToken}";
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
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Rock-Paper-Scissors payload: {payloadToken}";
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
                error = "Rock-Paper-Scissors packetraw requires an opcode-wrapped hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                byte[] rawPacket = Convert.FromHexString(compactHex);
                if (rawPacket.Length < sizeof(ushort) + sizeof(byte))
                {
                    error = "Rock-Paper-Scissors packetraw payload is too short.";
                    return false;
                }

                ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
                if (opcode != RockPaperScissorsField.OwnerOpcode)
                {
                    error = $"Rock-Paper-Scissors packetraw opcode must be {RockPaperScissorsField.OwnerOpcode}.";
                    return false;
                }

                packetType = rawPacket[sizeof(ushort)];
                if (!RockPaperScissorsField.TryParsePacketType(packetType.ToString(), out _))
                {
                    error = $"Unsupported Rock-Paper-Scissors packet subtype: {packetType}";
                    return false;
                }

                payload = new byte[rawPacket.Length - sizeof(ushort) - sizeof(byte)];
                if (payload.Length > 0)
                {
                    Buffer.BlockCopy(rawPacket, sizeof(ushort) + sizeof(byte), payload, 0, payload.Length);
                }

                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Rock-Paper-Scissors packetraw payload: {payloadToken}";
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
                LastStatus = $"Rock-Paper-Scissors packet inbox error: {ex.Message}";
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

                        if (!TryParsePacketLine(line, out int packetType, out byte[] payload, out string error))
                        {
                            LastStatus = $"Ignored Rock-Paper-Scissors inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new RockPaperScissorsPacketInboxMessage(packetType, payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(packetType)} from {remoteEndpoint}.";
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
                LastStatus = $"Rock-Paper-Scissors inbox client error from {remoteEndpoint}: {ex.Message}";
            }
        }

        private void StopInternal(bool clearPending)
        {
            _listenerCancellation?.Cancel();

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
            }
        }

        private static string RemoveWhitespace(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                6 => "RPS reset-win (6)",
                7 => "RPS reset-lose (7)",
                8 => "RPS open (8)",
                9 => "RPS start (9)",
                10 => "RPS force-result (10)",
                11 => "RPS result-payload (11)",
                12 => "RPS continue (12)",
                13 => "RPS destroy (13)",
                14 => "RPS reset (14)",
                _ => $"RPS subtype {packetType}"
            };
        }
    }
}
