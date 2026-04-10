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
    /// <summary>
    /// Loopback outbox for client-authored CRPSGameDlg opcode 160 packets when
    /// no live Maple socket bridge is attached.
    /// </summary>
    public sealed class RockPaperScissorsClientPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18492;

        private sealed record PendingOutboundPacket(RockPaperScissorsClientPacket Packet, byte[] RawPacket);

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
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
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
        public RockPaperScissorsClientPacket LastSentPacket { get; private set; }
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int QueuedCount { get; private set; }
        public RockPaperScissorsClientPacket LastQueuedPacket { get; private set; }
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Rock-Paper-Scissors client outbox inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{Port}"
                : $"configured for 127.0.0.1:{Port}";
            string clients = HasConnectedClients
                ? $"{ConnectedClientCount} client(s) connected"
                : "no connected clients";
            string lastPacket = LastSentPacket != null
                ? $" lastOut={LastSentPacket.Summary}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string queuedPacket = LastQueuedPacket != null
                ? $" lastQueued={LastQueuedPacket.Summary}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Rock-Paper-Scissors client outbox {lifecycle}; {clients}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}.{lastPacket}{queuedPacket} {LastStatus}";
        }

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning && Port == (port <= 0 ? DefaultPort : port))
                {
                    LastStatus = $"Rock-Paper-Scissors client outbox already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Rock-Paper-Scissors client outbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearState: true);
                    LastStatus = $"Rock-Paper-Scissors client outbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearState: true);
                LastStatus = "Rock-Paper-Scissors client outbox stopped.";
            }
        }

        public bool TrySendClientPacket(RockPaperScissorsClientPacket packet, out string status)
        {
            FlushQueuedOutboundPackets();
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Rock-Paper-Scissors client outbox has no connected clients.";
                LastStatus = status;
                return false;
            }

            if (packet == null)
            {
                status = "Rock-Paper-Scissors client outbox requires a client packet.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket(packet);
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
                    RemoveClient(client.Id, $"Rock-Paper-Scissors client outbox send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Rock-Paper-Scissors client outbox could not deliver the outbound packet.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            LastSentPacket = packet;
            LastSentRawPacket = rawPacket;
            status = $"Sent packetoutraw {rawPacketHex} to {sent} Rock-Paper-Scissors client outbox client(s).";
            LastStatus = status;
            return true;
        }

        public bool TryQueueClientPacket(RockPaperScissorsClientPacket packet, out string status)
        {
            if (packet == null)
            {
                status = "Rock-Paper-Scissors client outbox requires a client packet.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRawPacket(packet);
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(packet, rawPacket));
            QueuedCount++;
            LastQueuedPacket = packet;
            LastQueuedRawPacket = rawPacket;
            status = $"Queued packetoutraw {Convert.ToHexString(rawPacket)} for deferred Rock-Paper-Scissors client delivery.";
            LastStatus = status;
            return true;
        }

        public bool HasQueuedClientPacket(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return false;
            }

            byte[] target = BuildRawPacket(packet);
            PendingOutboundPacket[] pending = _pendingOutboundPackets.ToArray();
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].RawPacket.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        public bool WasLastSentClientPacket(RockPaperScissorsClientPacket packet)
        {
            if (packet == null || LastSentPacket == null)
            {
                return false;
            }

            byte[] target = BuildRawPacket(packet);
            return LastSentRawPacket.AsSpan().SequenceEqual(target);
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearState: true);
            }
        }

        internal static byte[] BuildRawPacket(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return Array.Empty<byte>();
            }

            byte[] payload = BuildClientPayload(packet);
            byte[] raw = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes((ushort)packet.Opcode).CopyTo(raw, 0);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, raw, sizeof(ushort), payload.Length);
            }

            return raw;
        }

        public static byte[] BuildClientPayload(RockPaperScissorsClientPacket packet)
        {
            if (packet == null)
            {
                return Array.Empty<byte>();
            }

            int payloadLength = packet.Payload?.Length ?? 0;
            byte[] payload = new byte[sizeof(byte) + payloadLength];
            payload[0] = (byte)packet.RequestType;
            if (payloadLength > 0)
            {
                Buffer.BlockCopy(packet.Payload, 0, payload, sizeof(byte), payloadLength);
            }

            return payload;
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    string endpoint = client.Client?.RemoteEndPoint?.ToString() ?? $"rps-client-outbox-{clientId}";
                    var connectedClient = new ConnectedClient(clientId, client, endpoint);
                    _clients[clientId] = connectedClient;
                    int flushed = FlushQueuedOutboundPackets();
                    LastStatus = flushed > 0
                        ? $"Rock-Paper-Scissors client outbox client connected: {endpoint}. Flushed {flushed} queued packet(s)."
                        : $"Rock-Paper-Scissors client outbox client connected: {endpoint}.";
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
                LastStatus = $"Rock-Paper-Scissors client outbox error: {ex.Message}";
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

                        LastStatus = $"Ignored Rock-Paper-Scissors client outbox inbound line from {client.Endpoint}: {line}";
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
                LastStatus = $"Rock-Paper-Scissors client outbox client error: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Rock-Paper-Scissors client outbox client disconnected: {client.Endpoint}.");
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
                LastSentPacket = null;
                LastSentRawPacket = Array.Empty<byte>();
                QueuedCount = 0;
                LastQueuedPacket = null;
                LastQueuedRawPacket = Array.Empty<byte>();
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }
            }
        }

        private int FlushQueuedOutboundPackets()
        {
            int flushed = 0;
            while (_pendingOutboundPackets.TryPeek(out PendingOutboundPacket packet))
            {
                ConnectedClient[] clients = _clients.Values.ToArray();
                if (clients.Length == 0)
                {
                    break;
                }

                string line = $"packetoutraw {Convert.ToHexString(packet.RawPacket)}";
                int delivered = 0;
                foreach (ConnectedClient client in clients)
                {
                    try
                    {
                        lock (client.WriteLock)
                        {
                            client.Writer.WriteLine(line);
                        }

                        delivered++;
                    }
                    catch (Exception ex)
                    {
                        RemoveClient(client.Id, $"Rock-Paper-Scissors client outbox send failed for {client.Endpoint}: {ex.Message}");
                    }
                }

                if (delivered == 0)
                {
                    break;
                }

                if (!_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket dequeuedPacket))
                {
                    break;
                }

                SentCount++;
                LastSentPacket = dequeuedPacket.Packet;
                LastSentRawPacket = dequeuedPacket.RawPacket;
                flushed++;
            }

            return flushed;
        }
    }
}
