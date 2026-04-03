using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Loopback outbox for packet-owned local utility requests that need to
    /// leave the simulator without a live Maple socket bridge.
    /// </summary>
    public sealed class LocalUtilityPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18487;

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
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Local utility packet outbox inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{Port}"
                : $"configured for 127.0.0.1:{Port}";
            string clients = HasConnectedClients
                ? $"{ConnectedClientCount} client(s) connected"
                : "no connected clients";
            string lastPacket = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            return $"Local utility packet outbox {lifecycle}; {clients}; sent={SentCount}.{lastPacket} {LastStatus}";
        }

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning && Port == (port <= 0 ? DefaultPort : port))
                {
                    LastStatus = $"Local utility packet outbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal(clearState: true);

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Local utility packet outbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearState: true);
                    LastStatus = $"Local utility packet outbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearState: true);
                LastStatus = "Local utility packet outbox stopped.";
            }
        }

        public bool TrySendOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Local utility packet outbox has no connected clients.";
                LastStatus = status;
                return false;
            }

            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"Local utility outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket((ushort)opcode, payload);
            string rawPacketHex = Convert.ToHexString(rawPacket);
            string line = $"packetoutraw {rawPacketHex}";
            int sent = 0;

            foreach (ConnectedClient client in clients)
            {
                try
                {
                    lock (client.WriteLock)
                    {
                        client.Writer.WriteLine(line);
                    }

                    sent++;
                }
                catch (Exception ex)
                {
                    RemoveClient(client.Id, $"Local utility packet outbox send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Local utility packet outbox could not deliver the outbound packet.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            LastSentOpcode = opcode;
            LastSentRawPacket = rawPacket;
            status = $"Sent packetoutraw {rawPacketHex} to {sent} local utility packet outbox client(s).";
            LastStatus = status;
            return true;
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearState: true);
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    string endpoint = client.Client?.RemoteEndPoint?.ToString() ?? $"local-utility-outbox-{clientId}";
                    var connectedClient = new ConnectedClient(clientId, client, endpoint);
                    _clients[clientId] = connectedClient;
                    LastStatus = $"Local utility packet outbox client connected: {endpoint}.";
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
                LastStatus = $"Local utility packet outbox error: {ex.Message}";
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

                        LastStatus = $"Ignored local utility packet outbox inbound line from {client.Endpoint}: {line}";
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
                LastStatus = $"Local utility packet outbox client error: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Local utility packet outbox client disconnected: {client.Endpoint}.");
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

        private void StopInternal(bool clearState)
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

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            foreach (ConnectedClient client in _clients.Values.ToArray())
            {
                RemoveClient(client.Id, LastStatus);
            }

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            if (clearState)
            {
                SentCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
            }
        }

        private static byte[] BuildRawPacket(ushort opcode, IReadOnlyList<byte> payload)
        {
            int payloadLength = payload?.Count ?? 0;
            byte[] raw = new byte[sizeof(ushort) + payloadLength];
            BitConverter.GetBytes(opcode).CopyTo(raw, 0);
            for (int i = 0; i < payloadLength; i++)
            {
                raw[sizeof(ushort) + i] = payload[i];
            }

            return raw;
        }
    }
}
