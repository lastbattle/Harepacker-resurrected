using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
    }

    /// <summary>
    /// Loopback transport seam for Coconut minigame packets.
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
        private readonly ConcurrentDictionary<int, ConnectedClient> _clients = new();
        private readonly object _listenerLock = new();
        private int _nextClientId;

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        private sealed class ConnectedClient : IDisposable
        {
            public ConnectedClient(int id, TcpClient client, string endpoint)
            {
                Id = id;
                Client = client;
                Endpoint = endpoint;
                Writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            }

            public int Id { get; }
            public TcpClient Client { get; }
            public string Endpoint { get; }
            public StreamWriter Writer { get; }
            public object WriteLock { get; } = new();

            public void Dispose()
            {
                try
                {
                    Writer.Dispose();
                }
                catch
                {
                }

                try
                {
                    Client.Dispose();
                }
                catch
                {
                }
            }
        }

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasConnectedClients => !_clients.IsEmpty;
        public int ConnectedClientCount => _clients.Count;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus { get; private set; } = "Coconut transport inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Coconut transport already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Coconut transport listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Coconut transport failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Coconut transport stopped.";
            }
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            _pendingMessages.Enqueue(new CoconutPacketInboxMessage(packetType, payload, source, $"{packetType}"));
        }

        public bool TryDequeue(out CoconutPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendAttackRequest(CoconutField.AttackPacketRequest request, out string status)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Coconut transport has no connected clients.";
                LastStatus = status;
                return false;
            }

            string line = $"attack {request.TargetId} {request.DelayMs} {request.RequestedAtTick}";
            string rawPacketLine = $"packetoutraw {BuildOutboundAttackPacketHex(request)}";
            int sent = 0;

            foreach (ConnectedClient client in clients)
            {
                try
                {
                    lock (client.WriteLock)
                    {
                        client.Writer.WriteLine(line);
                        client.Writer.WriteLine(rawPacketLine);
                    }

                    sent++;
                }
                catch (Exception ex)
                {
                    RemoveClient(client.Id, $"Coconut transport send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Coconut transport could not deliver the attack request.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            status = $"Sent Coconut attack request for target {request.TargetId} to {sent} transport client(s), including packetoutraw opcode {OutboundNormalAttackOpcode}.";
            LastStatus = status;
            return true;
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
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
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

            if (packetType != PacketTypeHit && packetType != PacketTypeScore)
            {
                error = $"Unsupported Coconut raw packet opcode: {packetType}";
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
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    string endpoint = client.Client?.RemoteEndPoint?.ToString() ?? $"coconut-client-{clientId}";
                    var connectedClient = new ConnectedClient(clientId, client, endpoint);
                    _clients[clientId] = connectedClient;
                    LastStatus = $"Coconut transport client connected: {endpoint}.";
                    _ = Task.Run(() => HandleClientAsync(connectedClient, cancellationToken), cancellationToken);
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
                LastStatus = $"Coconut transport error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(ConnectedClient client, CancellationToken cancellationToken)
        {
            try
            {
                using (client)
                using (StreamReader reader = new StreamReader(client.Client.GetStream()))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (line.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                            || line.StartsWith("packetoutraw", StringComparison.OrdinalIgnoreCase))
                        {
                            LastStatus = $"Ignored outbound echo from {client.Endpoint}: {line}";
                            continue;
                        }

                        if (!TryParsePacketLine(line, out int packetType, out byte[] payload, out bool ignored, out string message))
                        {
                            LastStatus = ignored
                                ? $"Ignored outbound echo from {client.Endpoint}: {message}"
                                : $"Ignored Coconut transport line from {client.Endpoint}: {message}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new CoconutPacketInboxMessage(packetType, payload, client.Endpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(packetType)} from {client.Endpoint}.";
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
                LastStatus = $"Coconut transport client error: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Coconut transport client disconnected: {client.Endpoint}.");
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

        private static string BuildOutboundAttackPacketHex(CoconutField.AttackPacketRequest request)
        {
            PacketWriter packetWriter = new PacketWriter();
            packetWriter.WriteShort(OutboundNormalAttackOpcode);
            packetWriter.WriteShort(request.TargetId);
            packetWriter.WriteShort(request.DelayMs);
            return Convert.ToHexString(packetWriter.ToArray()).ToLowerInvariant();
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
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

        private void StopInternal(bool clearPending)
        {
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch (SocketException)
            {
            }

            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;
            foreach (ConnectedClient client in _clients.Values)
            {
                client.Dispose();
            }

            _clients.Clear();

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                SentCount = 0;
            }
        }

        private void RemoveClient(int clientId, string status)
        {
            if (_clients.TryRemove(clientId, out ConnectedClient client))
            {
                client.Dispose();
            }

            LastStatus = status;
        }
    }
}
