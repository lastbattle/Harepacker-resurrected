using HaCreator.MapSimulator.Interaction;
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
    /// Loopback transport seam for packet-authored script replies.
    /// Outbound replies are emitted as a metadata line plus "packetoutraw &lt;hex&gt;".
    /// </summary>
    internal sealed class PacketScriptReplyTransportManager : IDisposable
    {
        public const int DefaultPort = 18497;

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
        public int SentCount { get; private set; }
        public string LastStatus { get; private set; } = "Packet-script reply transport inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Packet-script reply transport already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Packet-script reply transport listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal();
                    LastStatus = $"Packet-script reply transport failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Packet-script reply transport stopped.";
            }
        }

        internal bool TrySendResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket, out string status)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (responsePacket?.RawPacket == null || responsePacket.RawPacket.Length == 0)
            {
                status = "Packet-script reply transport did not receive an outbound payload.";
                LastStatus = status;
                return false;
            }

            if (clients.Length == 0)
            {
                status = "Packet-script reply transport has no connected clients.";
                LastStatus = status;
                return false;
            }

            string metadataLine =
                $"scriptreply {responsePacket.MessageType} template={responsePacket.SpeakerTemplateId} " +
                $"npc={responsePacket.SpeakerNpcId} value={QuoteValue(responsePacket.SubmittedValue)}";
            string rawPacketLine = $"packetoutraw {Convert.ToHexString(responsePacket.RawPacket)}";
            int sent = 0;

            foreach (ConnectedClient client in clients)
            {
                try
                {
                    lock (client.WriteLock)
                    {
                        client.Writer.WriteLine(metadataLine);
                        client.Writer.WriteLine(rawPacketLine);
                    }

                    sent++;
                }
                catch (Exception ex)
                {
                    RemoveClient(client.Id, $"Packet-script reply transport send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Packet-script reply transport could not deliver the outbound reply.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            status = $"Dispatched packet-authored script reply to {sent} transport client(s) as opcode 65.";
            LastStatus = status;
            return true;
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    int id = Interlocked.Increment(ref _nextClientId);
                    string endpoint = client.Client.RemoteEndPoint?.ToString() ?? $"client-{id}";
                    ConnectedClient connection = new ConnectedClient(id, client, endpoint);
                    _clients[id] = connection;
                    LastStatus = $"Packet-script reply transport connected: {endpoint}.";
                    _ = Task.Run(() => MonitorClientAsync(connection, cancellationToken), cancellationToken);
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
                LastStatus = $"Packet-script reply transport stopped unexpectedly: {ex.Message}";
            }
        }

        private async Task MonitorClientAsync(ConnectedClient client, CancellationToken cancellationToken)
        {
            try
            {
                using StreamReader reader = new StreamReader(client.Client.GetStream(), leaveOpen: true);
                while (!cancellationToken.IsCancellationRequested && client.Client.Connected)
                {
                    string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    if (line.StartsWith("scriptreply", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("packetoutraw", StringComparison.OrdinalIgnoreCase))
                    {
                        LastStatus = $"Ignored outbound echo from {client.Endpoint}: {line}";
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
                LastStatus = $"Packet-script reply transport monitor failed for {client.Endpoint}: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Packet-script reply transport disconnected: {client.Endpoint}.");
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

        private void StopInternal()
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
                _listenerTask?.Wait(250);
            }
            catch
            {
            }

            foreach (ConnectedClient client in _clients.Values.ToArray())
            {
                client.Dispose();
            }

            _clients.Clear();
            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }

        private static string QuoteValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "\"\"" : $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }
    }
}
