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
    /// Dedicated outbox for client-authored reactor touch requests that need a
    /// packetoutraw transport when no live official-session bridge is attached.
    /// </summary>
    public sealed class ReactorTouchPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18500;

        private sealed class ConnectedClient : IDisposable
        {
            public ConnectedClient(int id, TcpClient client, string endpoint)
            {
                Id = id;
                Client = client;
                Endpoint = endpoint;
                Writer = new StreamWriter(client.GetStream())
                {
                    AutoFlush = true
                };
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

        private sealed class PendingTouchRequest
        {
            public PendingTouchRequest(int objectId, bool isTouching, byte[] rawPacket, int sourceTick)
            {
                ObjectId = objectId;
                IsTouching = isTouching;
                RawPacket = rawPacket ?? Array.Empty<byte>();
                SourceTick = sourceTick;
            }

            public int ObjectId { get; }
            public bool IsTouching { get; }
            public byte[] RawPacket { get; }
            public int SourceTick { get; }
        }

        private readonly object _listenerLock = new();
        private readonly object _queueLock = new();
        private readonly ConcurrentDictionary<int, ConnectedClient> _clients = new();
        private readonly Queue<PendingTouchRequest> _pendingOutboundPackets = new();
        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private int _nextClientId;
        private int _nextDeferredTouchFlushTick = int.MinValue;
        private bool _deferredTouchFlushTickInitialized;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ConnectedClientCount => _clients.Count;
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int PendingPacketCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _pendingOutboundPackets.Count;
                }
            }
        }
        public int? LastSentObjectId { get; private set; }
        public bool? LastSentTouchFlag { get; private set; }
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int? LastQueuedObjectId { get; private set; }
        public bool? LastQueuedTouchFlag { get; private set; }
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Reactor touch outbox inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{Port}"
                : "inactive";
            string clients = ConnectedClientCount == 1
                ? "1 client connected"
                : $"{ConnectedClientCount} clients connected";
            string lastSent = LastSentRawPacket.Length > 0 && LastSentObjectId.HasValue && LastSentTouchFlag.HasValue
                ? $" Last sent={LastSentObjectId.Value}:{(LastSentTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedRawPacket.Length > 0 && LastQueuedObjectId.HasValue && LastQueuedTouchFlag.HasValue
                ? $" Last queued={LastQueuedObjectId.Value}:{(LastQueuedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Reactor touch outbox {lifecycle}; {clients}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}.{lastSent}{lastQueued} {LastStatus}";
        }

        public void Start(int port)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Reactor touch outbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Reactor touch outbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearQueuedPackets: false);
                    LastStatus = $"Reactor touch outbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearQueuedPackets: false);
                LastStatus = "Reactor touch outbox stopped.";
            }
        }

        public bool TrySendTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Reactor touch outbox has no connected clients.";
                LastStatus = status;
                return false;
            }

            if (objectId <= 0)
            {
                status = "Reactor touch outbox requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            lock (_queueLock)
            {
                FlushQueuedOutboundPacketsUnsafe(clients, currentTick);
                if (_pendingOutboundPackets.Count > 0)
                {
                    byte[] deferredPacket = ReactorPoolOfficialSessionBridgeManager.BuildTouchRequestPacket(objectId, isTouching);
                    int resolvedTick = ResolveCurrentTick(currentTick);
                    bool queued = EnqueueOrCoalesceDuplicateTouchRequestUnsafe(
                        new PendingTouchRequest(objectId, isTouching, deferredPacket, resolvedTick));
                    if (queued)
                    {
                        QueuedCount++;
                        LastQueuedObjectId = objectId;
                        LastQueuedTouchFlag = isTouching;
                        LastQueuedRawPacket = deferredPacket;
                        status = $"Queued packetoutraw {Convert.ToHexString(deferredPacket)} behind deferred reactor touch replay cadence.";
                    }
                    else
                    {
                        status = $"packetoutraw {Convert.ToHexString(deferredPacket)} is already the latest deferred reactor touch ownership state.";
                    }

                    LastStatus = status;
                    return true;
                }
            }

            byte[] rawPacket = ReactorPoolOfficialSessionBridgeManager.BuildTouchRequestPacket(objectId, isTouching);
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
                    RemoveClient(client.Id, $"Reactor touch outbox send failed for {client.Endpoint}: {ex.Message}");
                }
            }

            if (sent == 0)
            {
                status = "Reactor touch outbox could not deliver the outbound packet.";
                LastStatus = status;
                return false;
            }

            SentCount++;
            LastSentObjectId = objectId;
            LastSentTouchFlag = isTouching;
            LastSentRawPacket = rawPacket;
            status = $"Sent packetoutraw {rawPacketHex} to {sent} reactor touch outbox client(s).";
            LastStatus = status;
            return true;
        }

        public bool TryQueueTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch outbox requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = ReactorPoolOfficialSessionBridgeManager.BuildTouchRequestPacket(objectId, isTouching);
            int resolvedTick = ResolveCurrentTick(currentTick);
            bool queued;
            lock (_queueLock)
            {
                queued = EnqueueOrCoalesceDuplicateTouchRequestUnsafe(new PendingTouchRequest(objectId, isTouching, rawPacket, resolvedTick));
            }

            if (queued)
            {
                QueuedCount++;
                LastQueuedObjectId = objectId;
                LastQueuedTouchFlag = isTouching;
                LastQueuedRawPacket = rawPacket;
                status = $"Queued packetoutraw {Convert.ToHexString(rawPacket)} for deferred reactor touch delivery.";
            }
            else
            {
                status = $"packetoutraw {Convert.ToHexString(rawPacket)} is already the latest deferred reactor touch ownership state.";
            }

            LastStatus = status;
            return true;
        }

        public bool HasQueuedTouchRequest(int objectId, bool isTouching)
        {
            lock (_queueLock)
            {
                return _pendingOutboundPackets.Any(packet => packet.ObjectId == objectId && packet.IsTouching == isTouching);
            }
        }

        internal IReadOnlyList<(int ObjectId, bool IsTouching)> GetQueuedTouchRequestSnapshot()
        {
            lock (_queueLock)
            {
                return _pendingOutboundPackets
                    .Select(packet => (packet.ObjectId, packet.IsTouching))
                    .ToArray();
            }
        }

        public bool TryRemoveQueuedTouchRequests(int objectId, out string status)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch outbox queue removal requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            int removedCount;
            lock (_queueLock)
            {
                removedCount = RemoveQueuedTouchRequestsUnsafe(objectId);
            }

            status = removedCount > 0
                ? $"Removed {removedCount} queued packetoutraw reactor touch request(s) for object {objectId}."
                : $"No queued packetoutraw reactor touch requests were pending for object {objectId}.";
            LastStatus = status;
            return removedCount > 0;
        }

        public int ClearQueuedTouchRequests()
        {
            lock (_queueLock)
            {
                int removedCount = _pendingOutboundPackets.Count;
                _pendingOutboundPackets.Clear();
                ResetDeferredTouchFlushScheduleUnsafe();
                if (removedCount > 0)
                {
                    LastStatus = $"Cleared {removedCount} queued packetoutraw reactor touch request(s).";
                }

                return removedCount;
            }
        }

        public bool WasLastSentTouchRequest(int objectId, bool isTouching)
        {
            return LastSentObjectId == objectId && LastSentTouchFlag == isTouching;
        }

        public bool TryFlushDeferredTouchRequests(int currentTick, out string status)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            if (clients.Length == 0)
            {
                status = "Reactor touch outbox has no connected clients for deferred replay.";
                LastStatus = status;
                return false;
            }

            int flushed;
            lock (_queueLock)
            {
                flushed = FlushQueuedOutboundPacketsUnsafe(clients, currentTick);
            }

            status = flushed > 0
                ? $"Flushed {flushed} deferred packetoutraw reactor touch request(s)."
                : "No deferred packetoutraw reactor touch requests were due for replay yet.";
            LastStatus = status;
            return flushed > 0;
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearQueuedPackets: true);
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
                    string endpoint = client.Client?.RemoteEndPoint?.ToString() ?? $"reactor-touch-outbox-{clientId}";
                    var connectedClient = new ConnectedClient(clientId, client, endpoint);
                    _clients[clientId] = connectedClient;
                    int flushed = FlushQueuedOutboundPackets(Environment.TickCount);
                    LastStatus = flushed > 0
                        ? $"Reactor touch outbox client connected: {endpoint}. Flushed {flushed} queued packet(s)."
                        : $"Reactor touch outbox client connected: {endpoint}.";
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
                LastStatus = $"Reactor touch outbox error: {ex.Message}";
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

                        LastStatus = $"Ignored reactor touch outbox inbound line from {client.Endpoint}: {line}";
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
                LastStatus = $"Reactor touch outbox client error: {ex.Message}";
            }
            finally
            {
                RemoveClient(client.Id, $"Reactor touch outbox client disconnected: {client.Endpoint}.");
            }
        }

        private int FlushQueuedOutboundPackets(int currentTick)
        {
            ConnectedClient[] clients = _clients.Values.ToArray();
            lock (_queueLock)
            {
                return FlushQueuedOutboundPacketsUnsafe(clients, currentTick);
            }
        }

        private int FlushQueuedOutboundPacketsUnsafe(ConnectedClient[] clients, int currentTick)
        {
            if (clients.Length == 0)
            {
                return 0;
            }

            if (_pendingOutboundPackets.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
                return 0;
            }

            int flushed = 0;
            int resolvedCurrentTick = ResolveCurrentTick(currentTick);
            while (_pendingOutboundPackets.Count > 0
                && ShouldFlushDeferredTouchAtTick(resolvedCurrentTick, _nextDeferredTouchFlushTick, _deferredTouchFlushTickInitialized))
            {
                int replayTick = ResolveDeferredTouchReplayTick(
                    resolvedCurrentTick,
                    _nextDeferredTouchFlushTick,
                    _deferredTouchFlushTickInitialized);
                PendingTouchRequest packet = _pendingOutboundPackets.Peek();
                string line = $"packetoutraw {Convert.ToHexString(packet.RawPacket)}";
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
                        RemoveClient(client.Id, $"Reactor touch outbox send failed for {client.Endpoint}: {ex.Message}");
                    }
                }

                if (sent == 0)
                {
                    break;
                }

                PendingTouchRequest dequeued = _pendingOutboundPackets.Dequeue();

                SentCount++;
                flushed++;
                LastSentObjectId = dequeued.ObjectId;
                LastSentTouchFlag = dequeued.IsTouching;
                LastSentRawPacket = dequeued.RawPacket;
                UpdateDeferredTouchFlushScheduleAfterSendUnsafe(dequeued, replayTick);
            }

            if (_pendingOutboundPackets.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
            }

            return flushed;
        }

        private int RemoveQueuedTouchRequestsUnsafe(int objectId)
        {
            if (objectId <= 0 || _pendingOutboundPackets.Count == 0)
            {
                return 0;
            }

            int removedCount = 0;
            int pendingCount = _pendingOutboundPackets.Count;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingTouchRequest pending = _pendingOutboundPackets.Dequeue();
                if (pending.ObjectId != objectId)
                {
                    _pendingOutboundPackets.Enqueue(pending);
                }
                else
                {
                    removedCount++;
                }
            }

            if (_pendingOutboundPackets.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
            }

            return removedCount;
        }

        private bool EnqueueOrCoalesceDuplicateTouchRequestUnsafe(PendingTouchRequest next)
        {
            bool? latestQueuedStateForObject = null;
            foreach (PendingTouchRequest pending in _pendingOutboundPackets)
            {
                if (pending.ObjectId == next.ObjectId)
                {
                    latestQueuedStateForObject = pending.IsTouching;
                }
            }

            if (latestQueuedStateForObject.HasValue && latestQueuedStateForObject.Value == next.IsTouching)
            {
                return false;
            }

            _pendingOutboundPackets.Enqueue(next);
            return true;
        }

        private static int ResolveCurrentTick(int currentTick)
        {
            return currentTick == int.MinValue
                ? Environment.TickCount
                : currentTick;
        }

        internal static bool ShouldFlushDeferredTouchAtTick(int currentTick, int nextFlushTick, bool hasSchedule)
        {
            return !hasSchedule || unchecked(currentTick - nextFlushTick) >= 0;
        }

        internal static int ResolveDeferredTouchReplayTick(int currentTick, int nextFlushTick, bool hasSchedule)
        {
            if (!hasSchedule)
            {
                return currentTick;
            }

            return ShouldFlushDeferredTouchAtTick(currentTick, nextFlushTick, hasSchedule)
                ? nextFlushTick
                : currentTick;
        }

        internal static int ComputeDeferredTouchReplayDelayMs(int previousSourceTick, int nextSourceTick)
        {
            int delta = unchecked(nextSourceTick - previousSourceTick);
            if (delta < 0)
            {
                return 0;
            }

            return delta;
        }

        private void UpdateDeferredTouchFlushScheduleAfterSendUnsafe(PendingTouchRequest sent, int replayTick)
        {
            if (_pendingOutboundPackets.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
                return;
            }

            PendingTouchRequest next = _pendingOutboundPackets.Peek();
            int replayDelay = ComputeDeferredTouchReplayDelayMs(sent.SourceTick, next.SourceTick);
            _nextDeferredTouchFlushTick = unchecked(replayTick + replayDelay);
            _deferredTouchFlushTickInitialized = true;
        }

        private void ResetDeferredTouchFlushScheduleUnsafe()
        {
            _nextDeferredTouchFlushTick = int.MinValue;
            _deferredTouchFlushTickInitialized = false;
        }

        private void RemoveClient(int clientId, string status)
        {
            if (_clients.TryRemove(clientId, out ConnectedClient existing))
            {
                LastStatus = status;
                existing.Dispose();
            }
        }

        private void StopInternal(bool clearQueuedPackets)
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

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            foreach (ConnectedClient client in _clients.Values.ToArray())
            {
                RemoveClient(client.Id, $"Reactor touch outbox client disconnected: {client.Endpoint}.");
            }

            if (clearQueuedPackets)
            {
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }
                ResetDeferredTouchFlushScheduleUnsafe();

                SentCount = 0;
                QueuedCount = 0;
                LastSentObjectId = null;
                LastSentTouchFlag = null;
                LastSentRawPacket = Array.Empty<byte>();
                LastQueuedObjectId = null;
                LastQueuedTouchFlag = null;
                LastQueuedRawPacket = Array.Empty<byte>();
            }
        }
    }
}
