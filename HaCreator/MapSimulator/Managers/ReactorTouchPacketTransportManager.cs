using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Dedicated outbox for client-authored reactor touch requests that need a
    /// packetoutraw transport when no live official-session bridge is attached.
    /// </summary>
    public sealed class ReactorTouchPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18500;

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
        private readonly object _queueLock = new();
        private readonly RetiredMapleSocketState _socketState = new("Reactor touch outbox", DefaultPort, "Reactor touch outbox inactive.");
        private readonly Queue<PendingTouchRequest> _pendingOutboundPackets = new();
        private int _nextDeferredTouchFlushTick = int.MinValue;
        private bool _deferredTouchFlushTickInitialized;

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public int ConnectedClientCount => _socketState.ConnectedClientCount;
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
        public string LastStatus => _socketState.LastStatus;

        public string DescribeStatus()
        {
            string lastSent = LastSentRawPacket.Length > 0 && LastSentObjectId.HasValue && LastSentTouchFlag.HasValue
                ? $" Last sent={LastSentObjectId.Value}:{(LastSentTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedRawPacket.Length > 0 && LastQueuedObjectId.HasValue && LastQueuedTouchFlag.HasValue
                ? $" Last queued={LastQueuedObjectId.Value}:{(LastQueuedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return _socketState.Describe(sentCount: SentCount, pendingCount: PendingPacketCount, queuedCount: QueuedCount, detail: $"{lastSent}{lastQueued}".Trim());
        }

        public void Start(int port)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            StopInternal(clearQueuedPackets: false);
            _socketState.SetStatus("Reactor touch outbox listener already retired.");
        }

        public bool TrySendTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch outbox requires a positive reactor object id.";
                _socketState.SetStatus(status);
                return false;
            }

            byte[] rawPacket = ReactorPoolOfficialSessionBridgeManager.BuildTouchRequestPacket(objectId, isTouching);
            int resolvedTick = ResolveCurrentTick(currentTick);
            lock (_queueLock)
            {
                FlushQueuedOutboundPacketsUnsafe(resolvedTick);
                if (_pendingOutboundPackets.Count > 0)
                {
                    bool queued = EnqueueOrCoalesceDuplicateTouchRequestUnsafe(
                        new PendingTouchRequest(objectId, isTouching, rawPacket, resolvedTick));
                    if (queued)
                    {
                        QueuedCount++;
                        LastQueuedObjectId = objectId;
                        LastQueuedTouchFlag = isTouching;
                        LastQueuedRawPacket = rawPacket;
                        status = $"Queued packetoutraw {Convert.ToHexString(rawPacket)} behind deferred reactor touch replay cadence.";
                    }
                    else
                    {
                        status = $"packetoutraw {Convert.ToHexString(rawPacket)} is already the latest deferred reactor touch ownership state.";
                    }

                    _socketState.SetStatus(status);
                    return true;
                }

                SentCount++;
                LastSentObjectId = objectId;
                LastSentTouchFlag = isTouching;
                LastSentRawPacket = rawPacket;
            }

            status = $"Recorded packetoutraw {Convert.ToHexString(rawPacket)} for reactor touch delivery.";
            _socketState.SetStatus(status);
            return true;
        }

        public bool TryQueueTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch outbox requires a positive reactor object id.";
                _socketState.SetStatus(status);
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

            _socketState.SetStatus(status);
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
                _socketState.SetStatus(status);
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
            _socketState.SetStatus(status);
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
                    _socketState.SetStatus($"Cleared {removedCount} queued packetoutraw reactor touch request(s).");
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
            int flushed = FlushQueuedOutboundPackets(currentTick);
            status = flushed > 0
                ? $"Flushed {flushed} deferred packetoutraw reactor touch request(s)."
                : "No deferred packetoutraw reactor touch requests were due for replay yet.";
            _socketState.SetStatus(status);
            return flushed > 0;
        }

        public void Dispose()
        {
            StopInternal(clearQueuedPackets: true);
        }


        private int FlushQueuedOutboundPackets(int currentTick)
        {
            lock (_queueLock)
            {
                return FlushQueuedOutboundPacketsUnsafe(currentTick);
            }
        }

        private int FlushQueuedOutboundPacketsUnsafe(int currentTick)
        {
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

            PendingTouchRequest previousHead = _pendingOutboundPackets.Peek();
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
            else
            {
                PendingTouchRequest nextHead = _pendingOutboundPackets.Peek();
                if (_deferredTouchFlushTickInitialized
                    && removedCount > 0
                    && !ReferenceEquals(previousHead, nextHead))
                {
                    int replayDelay = ComputeDeferredTouchReplayDelayMs(previousHead.SourceTick, nextHead.SourceTick);
                    _nextDeferredTouchFlushTick = unchecked(_nextDeferredTouchFlushTick + replayDelay);
                }
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

        private void StopInternal(bool clearQueuedPackets)
        {
            if (clearQueuedPackets)
            {
                _pendingOutboundPackets.Clear();
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
