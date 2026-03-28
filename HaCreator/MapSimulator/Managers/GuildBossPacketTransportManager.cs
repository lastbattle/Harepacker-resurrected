using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Effects;
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
        private const string PulleyRequestPacketHex = "0301";

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

        private readonly ConcurrentQueue<GuildBossPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<int, ConnectedClient> _clients = new();
        private readonly object _listenerLock = new();
        private int _nextClientId;

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasConnectedClients => !_clients.IsEmpty;
        public int ConnectedClientCount => _clients.Count;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public string LastStatus { get; private set; } = "Guild boss transport inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Guild boss transport already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Guild boss transport listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Guild boss transport failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Guild boss transport stopped.";
            }
        }

        public bool TryDequeue(out GuildBossPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendPulleyRequest(GuildBossField.PulleyPacketRequest request, out string status)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Guild boss transport has no connected clients.";
                LastStatus = status;
                return false;
            }

            string legacyLine = $"pulleyhit {request.Sequence} {request.TickCount}";
            string rawPacketLine = $"packetoutraw {PulleyRequestPacketHex}";
            int sent = 0;

            foreach (ConnectedClient client in clients)
            {
                try
                {
                    lock (client.WriteLock)
                    {
                        client.Writer.WriteLine(legacyLine);
                        client.Writer.WriteLine(rawPacketLine);
                    }

                    sent++;
                }
                catch (Exception ex)
                {
                    RemoveClient(client.Id, $"Guild boss transport send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Guild boss transport could not deliver the pulley request.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            status = $"Sent pulleyhit #{request.Sequence} and packetoutraw {PulleyRequestPacketHex} to {sent} guild boss transport client(s).";
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
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Guild boss transport line is empty.";
                return false;
            }

            string[] tokens = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Guild boss transport line is empty.";
                return false;
            }

            if (TryParseRawPacket(tokens, out packetType, out payload, out error))
            {
                return true;
            }

            if (!TryParsePacketType(tokens[0], out packetType))
            {
                error = $"Unsupported guild boss packet type: {tokens[0]}";
                return false;
            }

            if (packetType == PacketTypeHealerMove)
            {
                if (TryParseSignedShortPayload(tokens, 1, out payload))
                {
                    return true;
                }

                error = "Guild boss healer packet requires either a signed Y value or a hex payload.";
                return false;
            }

            if (TryParseBytePayload(tokens, 1, out payload))
            {
                return true;
            }

            error = "Guild boss pulley packet requires either a state byte or a hex payload.";
            return false;
        }

        private static bool TryParseRawPacket(string[] tokens, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
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

            if (packetType != PacketTypeHealerMove && packetType != PacketTypePulleyStateChange)
            {
                error = $"Unsupported guild boss raw packet opcode: {packetType}";
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
                    string endpoint = client.Client?.RemoteEndPoint?.ToString() ?? $"guildboss-client-{clientId}";
                    var connectedClient = new ConnectedClient(clientId, client, endpoint);
                    _clients[clientId] = connectedClient;
                    LastStatus = $"Guild boss transport client connected: {endpoint}.";
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
                LastStatus = $"Guild boss transport error: {ex.Message}";
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

                        if (line.StartsWith("pulleyhit", StringComparison.OrdinalIgnoreCase))
                        {
                            LastStatus = $"Ignored outbound echo from {client.Endpoint}: {line}";
                            continue;
                        }

                        if (!TryParsePacketLine(line, out int packetType, out byte[] payload, out string error))
                        {
                            LastStatus = $"Ignored guild boss transport line from {client.Endpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new GuildBossPacketInboxMessage(packetType, payload, client.Endpoint, line));
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
                LastStatus = $"Guild boss transport client error: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Guild boss transport client disconnected: {client.Endpoint}.");
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

        private void RemoveClient(int clientId, string status)
        {
            if (_clients.TryRemove(clientId, out ConnectedClient client))
            {
                client.Dispose();
            }

            LastStatus = status;
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

            foreach (var pair in _clients)
            {
                RemoveClient(pair.Key, "Guild boss transport client disconnected.");
            }

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }
    }
}
