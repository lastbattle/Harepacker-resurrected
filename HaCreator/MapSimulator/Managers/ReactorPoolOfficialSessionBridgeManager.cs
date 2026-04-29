using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards CReactorPool::OnPacket reactor
    /// opcodes into the existing packet-owned reactor runtime seam.
    /// </summary>
    public sealed class ReactorPoolOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18499;
        public const short OutboundTouchReactorOpcode = 250;
        private const string DefaultProcessName = "MapleStory";

        private readonly ConcurrentQueue<ReactorPoolPacketInboxMessage> _pendingMessages = new();
        private readonly Queue<PendingTouchRequest> _pendingTouchRequests = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private int _nextDeferredTouchFlushTick = int.MinValue;
        private bool _deferredTouchFlushTickInitialized;

        private sealed class PendingTouchRequest
        {
            public PendingTouchRequest(int objectId, bool isTouching, byte[] packet, int sourceTick)
            {
                ObjectId = objectId;
                IsTouching = isTouching;
                Packet = packet ?? Array.Empty<byte>();
                SourceTick = sourceTick;
            }

            public int ObjectId { get; }
            public bool IsTouching { get; }
            public byte[] Packet { get; }
            public int SourceTick { get; }
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int InjectedTouchRequestCount { get; private set; }
        public int QueuedTouchRequestCount
        {
            get
            {
                lock (_sync)
                {
                    return _pendingTouchRequests.Count;
                }
            }
        }
        public int? LastInjectedTouchObjectId { get; private set; }
        public bool? LastInjectedTouchFlag { get; private set; }
        public byte[] LastInjectedTouchPacket { get; private set; } = Array.Empty<byte>();
        public int? LastQueuedTouchObjectId { get; private set; }
        public bool? LastQueuedTouchFlag { get; private set; }
        public byte[] LastQueuedTouchPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Reactor official-session bridge inactive.";

        public ReactorPoolOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session"
                : "no active Maple session";
            string lastInjected = LastInjectedTouchPacket.Length > 0 && LastInjectedTouchObjectId.HasValue && LastInjectedTouchFlag.HasValue
                ? $" Last injected={LastInjectedTouchObjectId.Value}:{(LastInjectedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastInjectedTouchPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedTouchPacket.Length > 0 && LastQueuedTouchObjectId.HasValue && LastQueuedTouchFlag.HasValue
                ? $" Last queued={LastQueuedTouchObjectId.Value}:{(LastQueuedTouchFlag.Value ? "enter" : "leave")} [{Convert.ToHexString(LastQueuedTouchPacket)}]."
                : string.Empty;
            return $"Reactor official-session bridge {lifecycle}; {session}; received={ReceivedCount}; touch injected={InjectedTouchRequestCount}; touch queued={QueuedTouchRequestCount}; inbound opcodes=334,335,336,337.{lastInjected}{lastQueued} {LastStatus}";
        }

        public bool TrySendTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch injection requires a positive reactor object id.";
                return false;
            }

            lock (_sync)
            {
                if (!_roleSessionProxy.HasConnectedSession)
                {
                    status = "Reactor official-session bridge has no connected Maple session for touch injection.";
                    LastStatus = status;
                    return false;
                }

                FlushQueuedTouchRequestsViaProxyUnsafe(currentTick);
                if (_pendingTouchRequests.Count > 0)
                {
                    byte[] deferredPacket = BuildTouchRequestPacket(objectId, isTouching);
                    int resolvedTick = ResolveCurrentTick(currentTick);
                    bool queued = EnqueueOrCoalesceDuplicateTouchRequestUnsafe(
                        new PendingTouchRequest(objectId, isTouching, deferredPacket, resolvedTick));
                    if (queued)
                    {
                        LastQueuedTouchObjectId = objectId;
                        LastQueuedTouchFlag = isTouching;
                        LastQueuedTouchPacket = deferredPacket;
                        status = $"Queued reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) behind deferred replay cadence.";
                    }
                    else
                    {
                        status = $"Reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) is already the latest deferred ownership state.";
                    }

                    LastStatus = status;
                    return true;
                }

                byte[] packet = BuildTouchRequestPacket(objectId, isTouching);
                if (!_roleSessionProxy.TrySendToServer(packet, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                InjectedTouchRequestCount++;
                LastInjectedTouchObjectId = objectId;
                LastInjectedTouchFlag = isTouching;
                LastInjectedTouchPacket = packet;
                LastStatus = $"Injected reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) into live session.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryQueueTouchRequest(int objectId, bool isTouching, out string status, int currentTick = int.MinValue)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch queue requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            byte[] packet = BuildTouchRequestPacket(objectId, isTouching);
            int resolvedTick = ResolveCurrentTick(currentTick);
            bool queued;
            lock (_sync)
            {
                queued = EnqueueOrCoalesceDuplicateTouchRequestUnsafe(new PendingTouchRequest(objectId, isTouching, packet, resolvedTick));
            }

            if (queued)
            {
                LastQueuedTouchObjectId = objectId;
                LastQueuedTouchFlag = isTouching;
                LastQueuedTouchPacket = packet;
                status = $"Queued reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) until a live Maple session is attached.";
            }
            else
            {
                status = $"Reactor touch opcode {OutboundTouchReactorOpcode} for object {objectId} ({(isTouching ? "enter" : "leave")}) is already the latest deferred ownership state.";
            }

            LastStatus = status;
            return true;
        }

        public bool HasQueuedTouchRequest(int objectId, bool isTouching)
        {
            lock (_sync)
            {
                return _pendingTouchRequests.Any(packet => packet.ObjectId == objectId && packet.IsTouching == isTouching);
            }
        }

        internal IReadOnlyList<(int ObjectId, bool IsTouching)> GetQueuedTouchRequestSnapshot()
        {
            lock (_sync)
            {
                return _pendingTouchRequests
                    .Select(packet => (packet.ObjectId, packet.IsTouching))
                    .ToArray();
            }
        }

        public bool TryRemoveQueuedTouchRequests(int objectId, out string status)
        {
            if (objectId <= 0)
            {
                status = "Reactor touch queue removal requires a positive reactor object id.";
                LastStatus = status;
                return false;
            }

            int removedCount;
            lock (_sync)
            {
                removedCount = RemoveQueuedTouchRequestsUnsafe(objectId);
            }

            status = removedCount > 0
                ? $"Removed {removedCount} queued reactor touch opcode {OutboundTouchReactorOpcode} request(s) for object {objectId}."
                : $"No queued reactor touch opcode {OutboundTouchReactorOpcode} requests were pending for object {objectId}.";
            LastStatus = status;
            return removedCount > 0;
        }

        public int ClearQueuedTouchRequests()
        {
            lock (_sync)
            {
                int removedCount = _pendingTouchRequests.Count;
                _pendingTouchRequests.Clear();
                ResetDeferredTouchFlushScheduleUnsafe();
                if (removedCount > 0)
                {
                    LastStatus = $"Cleared {removedCount} queued reactor touch opcode {OutboundTouchReactorOpcode} request(s).";
                }

                return removedCount;
            }
        }

        public bool WasLastInjectedTouchRequest(int objectId, bool isTouching)
        {
            return LastInjectedTouchObjectId == objectId && LastInjectedTouchFlag == isTouching;
        }

        public bool TryFlushDeferredTouchRequests(int currentTick, out string status)
        {
            lock (_sync)
            {
                if (!_roleSessionProxy.HasConnectedSession)
                {
                    status = "Reactor official-session bridge has no connected Maple session for deferred touch replay.";
                    LastStatus = status;
                    return false;
                }

                int flushed = FlushQueuedTouchRequestsViaProxyUnsafe(currentTick);
                status = flushed > 0
                    ? $"Flushed {flushed} deferred reactor touch request(s) into live session."
                    : "No deferred reactor touch requests were due for replay yet.";
                LastStatus = status;
                return flushed > 0;
            }
        }

        internal static byte[] BuildTouchRequestPacket(int objectId, bool isTouching)
        {
            using PacketWriter writer = new(sizeof(ushort) + sizeof(int) + sizeof(byte));
            writer.Write(OutboundTouchReactorOpcode);
            writer.WriteInt(objectId);
            writer.WriteByte(isTouching ? 1 : 0);
            return writer.ToArray();
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            lock (_sync)
            {
                StopForStartPreservingDeferredTouchRequestsUnsafe();

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = $"Reactor official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Reactor official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Reactor official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out var candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(
                    IsRunning,
                    ListenPort,
                    RemoteHost,
                    RemotePort,
                    resolvedListenPort,
                    candidate.RemoteEndpoint))
            {
                status = $"Reactor official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Reactor official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Reactor official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out ReactorPoolPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "reactor opcode" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        internal static bool TryCreateBridgeMessageFromRawPacket(byte[] rawPacket, string source, out ReactorPoolPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (!ReactorPoolPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out error))
            {
                return false;
            }

            if (!IsBridgeOpcode(packetType))
            {
                error = $"Unsupported reactor bridge opcode {packetType}.";
                return false;
            }

            message = new ReactorPoolPacketInboxMessage(
                packetType,
                payload,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}");
            return true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryCreateBridgeMessageFromRawPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", out ReactorPoolPacketInboxMessage message, out _))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued {ReactorPoolPacketInboxManager.DescribePacketType(message.PacketType)} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                _pendingTouchRequests.Clear();
                ResetDeferredTouchFlushScheduleUnsafe();
                ReceivedCount = 0;
                LastQueuedTouchObjectId = null;
                LastQueuedTouchFlag = null;
                LastQueuedTouchPacket = Array.Empty<byte>();
            }
        }

        private void StopForStartPreservingDeferredTouchRequestsUnsafe()
        {
            _roleSessionProxy.Stop(resetCounters: true);

            while (_pendingMessages.TryDequeue(out _))
            {
            }

            ReceivedCount = 0;
        }

        private int FlushQueuedTouchRequestsViaProxyUnsafe(int currentTick)
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                return 0;
            }

            if (_pendingTouchRequests.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
                return 0;
            }

            int flushed = 0;
            int resolvedCurrentTick = ResolveCurrentTick(currentTick);
            while (_pendingTouchRequests.Count > 0
                && ShouldFlushDeferredTouchAtTick(resolvedCurrentTick, _nextDeferredTouchFlushTick, _deferredTouchFlushTickInitialized))
            {
                int replayTick = ResolveDeferredTouchReplayTick(
                    resolvedCurrentTick,
                    _nextDeferredTouchFlushTick,
                    _deferredTouchFlushTickInitialized);
                PendingTouchRequest pending = _pendingTouchRequests.Peek();
                if (!_roleSessionProxy.TrySendToServer(pending.Packet, out _))
                {
                    break;
                }

                PendingTouchRequest dequeued = _pendingTouchRequests.Dequeue();
                InjectedTouchRequestCount++;
                flushed++;
                LastInjectedTouchObjectId = dequeued.ObjectId;
                LastInjectedTouchFlag = dequeued.IsTouching;
                LastInjectedTouchPacket = dequeued.Packet;
                UpdateDeferredTouchFlushScheduleAfterSendUnsafe(dequeued, replayTick);
            }

            if (_pendingTouchRequests.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
            }

            if (flushed > 0)
            {
                LastStatus = $"Flushed {flushed} queued reactor touch request(s) into live session.";
            }

            return flushed;
        }

        private int RemoveQueuedTouchRequestsUnsafe(int objectId)
        {
            if (objectId <= 0 || _pendingTouchRequests.Count == 0)
            {
                return 0;
            }

            PendingTouchRequest previousHead = _pendingTouchRequests.Peek();
            int removedCount = 0;
            int pendingCount = _pendingTouchRequests.Count;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingTouchRequest pending = _pendingTouchRequests.Dequeue();
                if (pending.ObjectId != objectId)
                {
                    _pendingTouchRequests.Enqueue(pending);
                }
                else
                {
                    removedCount++;
                }
            }

            if (_pendingTouchRequests.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
            }
            else
            {
                PendingTouchRequest nextHead = _pendingTouchRequests.Peek();
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
            foreach (PendingTouchRequest pending in _pendingTouchRequests)
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

            _pendingTouchRequests.Enqueue(next);
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
            if (_pendingTouchRequests.Count == 0)
            {
                ResetDeferredTouchFlushScheduleUnsafe();
                return;
            }

            PendingTouchRequest next = _pendingTouchRequests.Peek();
            int replayDelay = ComputeDeferredTouchReplayDelayMs(sent.SourceTick, next.SourceTick);
            _nextDeferredTouchFlushTick = unchecked(replayTick + replayDelay);
            _deferredTouchFlushTickInitialized = true;
        }

        private void ResetDeferredTouchFlushScheduleUnsafe()
        {
            _nextDeferredTouchFlushTick = int.MinValue;
            _deferredTouchFlushTickInitialized = false;
        }

        private static bool IsBridgeOpcode(int packetType)
        {
            return packetType == (int)PacketReactorPoolPacketKind.ChangeState
                || packetType == (int)PacketReactorPoolPacketKind.Move
                || packetType == (int)PacketReactorPoolPacketKind.EnterField
                || packetType == (int)PacketReactorPoolPacketKind.LeaveField;
        }

        private static bool TryResolveProcessSelector(string selector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;
            if (string.IsNullOrWhiteSpace(selector))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(selector, out int pid) && pid > 0)
            {
                owningProcessId = pid;
                return true;
            }

            string normalized = NormalizeProcessSelector(selector);
            if (normalized.Length == 0)
            {
                error = "Reactor official-session discovery requires a process name or pid when a selector is provided.";
                return false;
            }

            owningProcessName = normalized;
            return true;
        }

        private static string NormalizeProcessSelector(string selector)
        {
            string trimmed = selector?.Trim() ?? string.Empty;
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmed[..^4]
                : trimmed;
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            IPEndPoint expectedRemoteEndpoint)
        {
            if (!isRunning || expectedRemoteEndpoint == null)
            {
                return false;
            }

            if (currentListenPort != expectedListenPort || currentRemotePort != expectedRemoteEndpoint.Port)
            {
                return false;
            }

            return IPAddress.TryParse(currentRemoteHost, out IPAddress currentRemoteAddress)
                && currentRemoteAddress.Equals(expectedRemoteEndpoint.Address);
        }

        private static bool TryResolveDiscoveryCandidate(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Reactor official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.RemoteEndpoint.Address}:{match.RemoteEndpoint.Port} via {match.LocalEndpoint.Address}:{match.LocalEndpoint.Port}"));
                status = $"Reactor official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /reactorpacket session discover to inspect them, or add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        private static System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue)
            {
                return candidates ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the selected process"
                    : $"process '{owningProcessName}'";
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
        }
    }
}
