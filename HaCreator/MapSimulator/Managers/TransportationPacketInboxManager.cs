using MapleLib.PacketLib;
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
    public sealed class TransportationPacketInboxMessage
    {
        public TransportationPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "transport-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback inbox for transit and voyage field wrapper packets.
    /// Supports direct aliases for the recovered CField_ContiMove handlers as well as
    /// decrypted Maple packets for opcodes 164 (OnContiMove) and 165 (OnContiState).
    /// </summary>
    public sealed class TransportationPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;
        public const int PacketTypeContiMove = 164;
        public const int PacketTypeContiState = 165;
        public const byte ContiMoveStartShip = 8;
        public const byte ContiMoveMoveField = 10;
        public const byte ContiMoveEndShip = 12;

        private readonly ConcurrentQueue<TransportationPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Transport packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Transport packet inbox already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Transport packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Transport packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Transport packet inbox stopped.";
            }
        }

        public bool TryDequeue(out TransportationPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, TransportationPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result) ? DescribePacket(message.PacketType, message.Payload) : $"{DescribePacket(message.PacketType, message.Payload)}: {result}";
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
                error = "Transport inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (TryParseRawPacket(trimmed, out packetType, out payload, out error))
            {
                return true;
            }

            string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Transport inbox line is empty.";
                return false;
            }

            string action = parts[0].Trim().ToLowerInvariant();
            switch (action)
            {
                case "start":
                case "onstartshipmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveStartShip, out packetType, out payload, out error);

                case "move":
                case "onmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveMoveField, out packetType, out payload, out error);

                case "end":
                case "onendshipmovefield":
                    return TryBuildContiMoveAlias(parts, ContiMoveEndShip, out packetType, out payload, out error);

                case "state":
                case "contistate":
                case "oncontistate":
                    if (parts.Length < 3
                        || !byte.TryParse(parts[1], out byte state)
                        || !byte.TryParse(parts[2], out byte stateValue))
                    {
                        error = "Transport state lines must be 'state <state> <value>'.";
                        return false;
                    }

                    packetType = PacketTypeContiState;
                    payload = new[] { state, stateValue };
                    return true;

                case "contimove":
                case "oncontimove":
                    if (parts.Length < 3
                        || !byte.TryParse(parts[1], out byte moveType)
                        || !byte.TryParse(parts[2], out byte moveValue))
                    {
                        error = "Transport conti-move lines must be 'OnContiMove <subtype> <value>'.";
                        return false;
                    }

                    packetType = PacketTypeContiMove;
                    payload = new[] { moveType, moveValue };
                    return true;

                default:
                    if (!int.TryParse(parts[0], out packetType))
                    {
                        error = $"Unsupported transport packet action: {parts[0]}";
                        return false;
                    }

                    if (packetType != PacketTypeContiMove && packetType != PacketTypeContiState)
                    {
                        error = $"Unsupported transport packet opcode: {packetType}";
                        return false;
                    }

                    if (parts.Length < 2)
                    {
                        error = "Transport packet lines must include a hex payload.";
                        return false;
                    }

                    string compactHex = RemoveWhitespace(string.Join(string.Empty, parts.Skip(1)));
                    if (!TryParseHexPayload(compactHex, out payload))
                    {
                        error = "Transport packet payload must be valid hex.";
                        return false;
                    }

                    return true;
            }
        }

        private static bool TryBuildContiMoveAlias(string[] parts, byte moveType, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (parts.Length < 2 || !byte.TryParse(parts[1], out byte value))
            {
                error = $"Transport move lines must be '{parts[0]} <value>'.";
                return false;
            }

            packetType = PacketTypeContiMove;
            payload = new[] { moveType, value };
            return true;
        }

        private static bool TryParseRawPacket(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !string.Equals(parts[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (parts.Length < 2)
            {
                error = "Transport raw packet requires a decrypted packet hex payload.";
                return false;
            }

            string compactHex = RemoveWhitespace(string.Join(string.Empty, parts.Skip(1)));
            if (!TryParseHexPayload(compactHex, out byte[] rawPacket))
            {
                error = "Transport raw packet payload must be valid hex.";
                return false;
            }

            if (rawPacket.Length < sizeof(short))
            {
                error = "Transport raw packet must include a 2-byte opcode.";
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
                error = $"Transport raw packet decode failed: {ex.Message}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            if (packetType != PacketTypeContiMove && packetType != PacketTypeContiState)
            {
                error = $"Unsupported transport raw packet opcode: {packetType}";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            return true;
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
                LastStatus = $"Transport packet inbox error: {ex.Message}";
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
                            LastStatus = $"Ignored transport inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new TransportationPacketInboxMessage(packetType, payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacket(packetType, payload)} from {remoteEndpoint}.";
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
                LastStatus = $"Transport packet inbox client error: {ex.Message}";
            }
        }

        private static string DescribePacket(int packetType, byte[] payload)
        {
            if (packetType == PacketTypeContiMove && payload?.Length >= 2)
            {
                return payload[0] switch
                {
                    ContiMoveStartShip => $"Transport OnContiMove start ({payload[1]})",
                    ContiMoveMoveField => $"Transport OnContiMove move ({payload[1]})",
                    ContiMoveEndShip => $"Transport OnContiMove end ({payload[1]})",
                    _ => $"Transport OnContiMove ({payload[0]}, {payload[1]})"
                };
            }

            if (packetType == PacketTypeContiState && payload?.Length >= 2)
            {
                return $"Transport OnContiState ({payload[0]}, {payload[1]})";
            }

            return $"Transport packet {packetType}";
        }

        private static bool TryParseHexPayload(string text, out byte[] payload)
        {
            payload = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
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

        private static string RemoveWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return string.Concat(value.Where(ch => !char.IsWhiteSpace(ch)));
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
