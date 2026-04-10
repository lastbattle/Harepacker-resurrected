using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session and forwards CSummonedPool::OnPacket opcodes
    /// into the existing packet-owned summoned-pool seam.
    /// </summary>
    public sealed class SummonedOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18486;
        private const string DefaultProcessName = "MapleStory";
        private const string OfficialSessionTraceSourcePrefix = "official-session:";
        private const int MaxRecentOutboundPackets = 32;
        private const int MaxRecentSg88ManualAttackRequests = 16;
        private const int Sg88ManualAttackCaptureGraceMs = 120;
        private const int MaxLearnedSg88ManualAttackTemplatesPerTargetCount = 8;
        private const int MaxRecentTeslaAttackRequests = 16;
        private const int TeslaAttackCaptureGraceMs = 120;
        private const int MaxLearnedTeslaAttackTemplatesPerTargetCount = 8;
        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket, string ObservedSource);
        private sealed class LearnedSg88ManualAttackTemplate
        {
            public required Sg88ManualAttackRequestPacketTemplate Template { get; init; }
            public required int RequestedAt { get; init; }
            public required int ObservedAt { get; init; }
            public required string Source { get; init; }
            public required string Evidence { get; init; }
            public required int PrimaryTargetMobId { get; init; }
            public required int[] TargetMobIds { get; init; }
            public string ResolutionSource { get; set; }
        }

        private sealed class Sg88ManualAttackCapture
        {
            public int SummonObjectId { get; init; }
            public int RequestedAt { get; init; }
            public int PrimaryTargetMobId { get; init; }
            public int[] TargetMobIds { get; init; } = Array.Empty<int>();
            public int BaseDelayMs { get; init; }
            public int FollowUpDelayMs { get; init; }
            public int CaptureWindowEndAt { get; init; }
            public List<OutboundPacketTrace> ObservedPackets { get; } = new();
            public OutboundPacketTrace? RequestPacket { get; set; }
            public int RequestPacketScore { get; set; }
            public string RequestPacketEvidence { get; set; }
            public string ResolutionSource { get; set; }
        }

        private readonly ConcurrentQueue<SummonedPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly List<Sg88ManualAttackCapture> _pendingSg88ManualAttackCaptures = new();
        private readonly Queue<Sg88ManualAttackCapture> _recentSg88ManualAttackCaptures = new();
        private readonly Dictionary<int, List<LearnedSg88ManualAttackTemplate>> _learnedSg88ManualAttackTemplates = new();
        private readonly List<Sg88ManualAttackCapture> _pendingTeslaAttackCaptures = new();
        private readonly Queue<Sg88ManualAttackCapture> _recentTeslaAttackCaptures = new();
        private readonly Dictionary<int, List<LearnedSg88ManualAttackTemplate>> _learnedTeslaAttackTemplates = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);
        public readonly record struct OutboundPacketTrace(
            int Opcode,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            int ObservedAt,
            int? BoundSg88SummonObjectId,
            int? BoundSg88RequestedAt);
        internal readonly record struct Sg88ManualAttackRequestPacketTemplate(
            int Opcode,
            byte[] RawPacket,
            int[] SummonObjectIdOffsets,
            int[] PrimaryTargetMobIdOffsets,
            int[] TargetMobIdOffsets,
            int TargetCount);
        internal readonly record struct Sg88ManualAttackTraceBinding(
            bool IsWithinCaptureWindow,
            bool MatchedSummonObjectId,
            bool MatchedPrimaryTargetMobId,
            int MatchedTargetCount,
            int Score,
            string Evidence)
        {
            public bool HasSemanticEvidence => MatchedSummonObjectId || MatchedPrimaryTargetMobId || MatchedTargetCount > 0;
        }
        internal readonly record struct Sg88ManualAttackTemplateLanePreference(
            int LaneScore,
            int OrderedMatchScore,
            int LeadingOrderedMatchCount,
            int ExactOrderedMatchCount);

        private sealed class BridgePair
        {
            public BridgePair(TcpClient clientTcpClient, TcpClient serverTcpClient, Session clientSession, Session serverSession)
            {
                ClientTcpClient = clientTcpClient;
                ServerTcpClient = serverTcpClient;
                ClientSession = clientSession;
                ServerSession = serverSession;
                RemoteEndpoint = serverTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-remote";
                ClientEndpoint = clientTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-client";
            }

            public TcpClient ClientTcpClient { get; }
            public TcpClient ServerTcpClient { get; }
            public Session ClientSession { get; }
            public Session ServerSession { get; }
            public string RemoteEndpoint { get; }
            public string ClientEndpoint { get; }
            public short Version { get; set; }
            public bool InitCompleted { get; set; }

            public void Close()
            {
                try
                {
                    ClientTcpClient.Close();
                }
                catch
                {
                }

                try
                {
                    ServerTcpClient.Close();
                }
                catch
                {
                }
            }
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int QueuedCount { get; private set; }
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Summoned official-session bridge inactive.";

        public string DescribeStatus()
        {
            lock (_sync)
            {
                PruneExpiredSg88ManualAttackCaptures(Environment.TickCount);
                PruneExpiredTeslaAttackCaptures(Environment.TickCount);
                string lifecycle = IsRunning
                    ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                    : "inactive";
                string session = HasConnectedSession
                    ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                    : "no active Maple session";
                string lastOutbound = LastSentOpcode >= 0
                    ? $" lastOut=0x{LastSentOpcode:X}[{Convert.ToHexString(LastSentRawPacket)}]."
                    : string.Empty;
                string lastQueued = LastQueuedOpcode >= 0
                    ? $" lastQueued=0x{LastQueuedOpcode:X}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                    : string.Empty;
                return $"Summoned official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; sg88Pending={_pendingSg88ManualAttackCaptures.Count}; sg88Recent={_recentSg88ManualAttackCaptures.Count}; teslaPending={_pendingTeslaAttackCaptures.Count}; teslaRecent={_recentTeslaAttackCaptures.Count}; inbound opcodes=0x116-0x11B; outbound=raw passthrough plus live capture history.{lastOutbound}{lastQueued} {LastStatus}";
            }
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                PruneExpiredSg88ManualAttackCaptures(Environment.TickCount);
                PruneExpiredTeslaAttackCaptures(Environment.TickCount);
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Summoned official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Summoned official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode=0x{entry.Opcode:X} payloadLen={entry.PayloadLength} observedAt={entry.ObservedAt} source={entry.Source}{FormatBoundSg88Request(entry)} payloadHex={entry.PayloadHex}"));
            }
        }

        public string DescribeRecentSg88ManualAttackRequests(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentSg88ManualAttackRequests);
            lock (_sync)
            {
                PruneExpiredSg88ManualAttackCaptures(Environment.TickCount);
                IEnumerable<Sg88ManualAttackCapture> entries = _recentSg88ManualAttackCaptures
                    .Reverse()
                    .Concat(_pendingSg88ManualAttackCaptures.AsEnumerable().Reverse())
                    .Take(normalizedCount);
                if (!entries.Any())
                {
                    return "Summoned official-session bridge SG-88 request history is empty.";
                }

                return "Summoned official-session bridge SG-88 request history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"summon={entry.SummonObjectId} requestedAt={entry.RequestedAt} primaryTarget={entry.PrimaryTargetMobId} targetCount={entry.TargetMobIds.Length} baseDelay={entry.BaseDelayMs} followUpDelay={entry.FollowUpDelayMs} captureWindowEndAt={entry.CaptureWindowEndAt} resolution={entry.ResolutionSource ?? "pending"} requestPacket={FormatRequestPacket(entry)} observedPackets={FormatObservedPacketList(entry.ObservedPackets, entry.RequestPacket)}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
                _recentSg88ManualAttackCaptures.Clear();
                _pendingSg88ManualAttackCaptures.Clear();
                _learnedSg88ManualAttackTemplates.Clear();
                _recentTeslaAttackCaptures.Clear();
                _pendingTeslaAttackCaptures.Clear();
                _learnedTeslaAttackTemplates.Clear();
            }

            LastStatus = "Summoned official-session bridge outbound, SG-88/Tesla request history, and learned template cache cleared.";
            return LastStatus;
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                    {
                        status = $"Summoned official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Summoned official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Summoned official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = resolvedListenPort;
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Summoned official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Summoned official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
                    return false;
                }
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            TryStart(listenPort, remoteHost, remotePort, out _);
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Summoned official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
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
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(IsRunning, ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
            {
                status = $"Summoned official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            return TryStart(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out status);
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
                LastStatus = "Summoned official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out SummonedPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "summoned opcode" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            return TrySendOutboundRawPacket(rawPacket, "simulator:command-sendraw", out status);
        }

        internal bool TrySendOutboundRawPacket(byte[] rawPacket, string observedSource, out string status)
        {
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out string error))
            {
                status = error;
                LastStatus = status;
                return false;
            }

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
            }

            if (pair == null || !pair.InitCompleted)
            {
                status = "Summoned official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            try
            {
                pair.ServerSession.SendPacket(clonedRawPacket);
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = clonedRawPacket;
                RecordObservedOutboundPacket(clonedRawPacket, NormalizeObservedOutboundSource(observedSource, "simulator:outbound"));
                status = $"Injected summoned outbound raw opcode 0x{opcode:X} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Summoned official-session outbound injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public bool TryQueueOutboundRawPacket(byte[] rawPacket, out string status)
        {
            return TryQueueOutboundRawPacket(rawPacket, "simulator:command-queueraw", out status);
        }

        internal bool TryQueueOutboundRawPacket(byte[] rawPacket, string observedSource, out string status)
        {
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out string error))
            {
                status = error;
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(
                opcode,
                clonedRawPacket,
                NormalizeObservedOutboundSource(observedSource, "simulator:queued-outbound")));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = clonedRawPacket;
            status = $"Queued summoned outbound raw opcode 0x{opcode:X} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        internal void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeObservedOutboundPacket(rawPacket, source, out OutboundPacketTrace trace))
            {
                return;
            }

            lock (_sync)
            {
                PruneExpiredSg88ManualAttackCaptures(trace.ObservedAt);
                PruneExpiredTeslaAttackCaptures(trace.ObservedAt);
                trace = TryBindSg88ManualAttackCapture(trace);
                trace = TryBindTeslaAttackCapture(trace);
                while (_recentOutboundPackets.Count >= MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }

                _recentOutboundPackets.Enqueue(trace);
            }
        }

        internal void TrackSg88ManualAttackRequest(
            int summonObjectId,
            int requestedAt,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            int baseDelayMs,
            int followUpDelayMs)
        {
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                return;
            }

            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            int captureWindowEndAt = CalculateCaptureWindowEndAt(requestedAt, baseDelayMs, followUpDelayMs);
            lock (_sync)
            {
                PruneExpiredSg88ManualAttackCaptures(requestedAt);
                for (int i = _pendingSg88ManualAttackCaptures.Count - 1; i >= 0; i--)
                {
                    Sg88ManualAttackCapture pendingCapture = _pendingSg88ManualAttackCaptures[i];
                    if (pendingCapture.SummonObjectId != summonObjectId)
                    {
                        continue;
                    }

                    ArchiveSg88ManualAttackCapture(pendingCapture, "superseded");
                    _pendingSg88ManualAttackCaptures.RemoveAt(i);
                }

                _pendingSg88ManualAttackCaptures.Add(new Sg88ManualAttackCapture
                {
                    SummonObjectId = summonObjectId,
                    RequestedAt = requestedAt,
                    PrimaryTargetMobId = primaryTargetMobId,
                    TargetMobIds = resolvedTargetMobIds,
                    BaseDelayMs = Math.Max(0, baseDelayMs),
                    FollowUpDelayMs = Math.Max(0, followUpDelayMs),
                    CaptureWindowEndAt = captureWindowEndAt
                });
            }
        }

        internal bool TrySendLearnedSg88ManualAttackRequest(
            int summonObjectId,
            int requestedAt,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out string status)
        {
            status = null;
            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                status = "SG-88 request replay requires a positive summon object id and request tick.";
                return false;
            }

            if (primaryTargetMobId <= 0 || resolvedTargetMobIds.Length == 0)
            {
                status = "SG-88 request replay requires at least one admitted target mob id.";
                return false;
            }

            Sg88ManualAttackRequestPacketTemplate template;
            string templateStatus;
            lock (_sync)
            {
                if (!TryResolveLearnedSg88ManualAttackRequestTemplate(
                        resolvedTargetMobIds.Length,
                        primaryTargetMobId,
                        resolvedTargetMobIds,
                        out template,
                        out templateStatus))
                {
                    status = templateStatus;
                    LastStatus = status;
                    return false;
                }
            }

            if (!TryBuildSg88ManualAttackRequestRawPacket(
                    template,
                    summonObjectId,
                    primaryTargetMobId,
                    resolvedTargetMobIds,
                    out byte[] rawPacket,
                    out string buildError))
            {
                status = buildError;
                LastStatus = status;
                return false;
            }

            string replayTraceSource = $"simulator:sg88-template:{requestedAt}";
            if (!TrySendOutboundRawPacket(rawPacket, replayTraceSource, out string sendStatus))
            {
                status = sendStatus;
                return false;
            }

            status = $"{sendStatus} Learned SG-88 template targetCount={template.TargetCount} opcode=0x{template.Opcode:X}.";
            LastStatus = status;
            return true;
        }

        internal bool ResolveSg88ManualAttackRequest(int summonObjectId, int requestedAt, string resolutionSource)
        {
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                return false;
            }

            lock (_sync)
            {
                string normalizedResolutionSource = string.IsNullOrWhiteSpace(resolutionSource)
                    ? "resolved"
                    : resolutionSource.Trim();
                for (int i = _pendingSg88ManualAttackCaptures.Count - 1; i >= 0; i--)
                {
                    Sg88ManualAttackCapture pendingCapture = _pendingSg88ManualAttackCaptures[i];
                    if (pendingCapture.SummonObjectId != summonObjectId || pendingCapture.RequestedAt != requestedAt)
                    {
                        continue;
                    }

                    string preferredResolutionSource = PreferSg88TemplateResolutionSource(
                        normalizedResolutionSource,
                        pendingCapture.ResolutionSource);
                    ArchiveSg88ManualAttackCapture(pendingCapture, preferredResolutionSource);
                    _pendingSg88ManualAttackCaptures.RemoveAt(i);
                    return true;
                }

                foreach (Sg88ManualAttackCapture archivedCapture in _recentSg88ManualAttackCaptures.Reverse())
                {
                    if (archivedCapture.SummonObjectId != summonObjectId || archivedCapture.RequestedAt != requestedAt)
                    {
                        continue;
                    }

                    archivedCapture.ResolutionSource = PreferSg88TemplateResolutionSource(
                        normalizedResolutionSource,
                        archivedCapture.ResolutionSource);
                    UpdateLearnedSg88ManualAttackTemplateResolution(archivedCapture);
                    return true;
                }
            }

            return false;
        }

        internal void TrackTeslaCoilAttackRequest(
            int summonObjectId,
            int requestedAt,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            int baseDelayMs)
        {
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                return;
            }

            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            int captureWindowEndAt = CalculateTeslaCaptureWindowEndAt(requestedAt, baseDelayMs);
            lock (_sync)
            {
                PruneExpiredTeslaAttackCaptures(requestedAt);
                for (int i = _pendingTeslaAttackCaptures.Count - 1; i >= 0; i--)
                {
                    Sg88ManualAttackCapture pendingCapture = _pendingTeslaAttackCaptures[i];
                    if (pendingCapture.SummonObjectId != summonObjectId)
                    {
                        continue;
                    }

                    ArchiveTeslaAttackCapture(pendingCapture, "superseded");
                    _pendingTeslaAttackCaptures.RemoveAt(i);
                }

                _pendingTeslaAttackCaptures.Add(new Sg88ManualAttackCapture
                {
                    SummonObjectId = summonObjectId,
                    RequestedAt = requestedAt,
                    PrimaryTargetMobId = primaryTargetMobId,
                    TargetMobIds = resolvedTargetMobIds,
                    BaseDelayMs = Math.Max(0, baseDelayMs),
                    FollowUpDelayMs = 0,
                    CaptureWindowEndAt = captureWindowEndAt
                });
            }
        }

        internal bool TrySendLearnedTeslaCoilAttackRequest(
            int summonObjectId,
            int requestedAt,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out string status)
        {
            status = null;
            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                status = "Tesla request replay requires a positive summon object id and request tick.";
                return false;
            }

            if (primaryTargetMobId <= 0 || resolvedTargetMobIds.Length == 0)
            {
                status = "Tesla request replay requires at least one admitted target mob id.";
                return false;
            }

            Sg88ManualAttackRequestPacketTemplate template;
            string templateStatus;
            lock (_sync)
            {
                if (!TryResolveLearnedTeslaAttackRequestTemplate(
                        resolvedTargetMobIds.Length,
                        primaryTargetMobId,
                        resolvedTargetMobIds,
                        out template,
                        out templateStatus))
                {
                    status = templateStatus;
                    LastStatus = status;
                    return false;
                }
            }

            if (!TryBuildSg88ManualAttackRequestRawPacket(
                    template,
                    summonObjectId,
                    primaryTargetMobId,
                    resolvedTargetMobIds,
                    out byte[] rawPacket,
                    out string buildError))
            {
                status = buildError;
                LastStatus = status;
                return false;
            }

            string replayTraceSource = $"simulator:tesla-template:{requestedAt}";
            if (!TrySendOutboundRawPacket(rawPacket, replayTraceSource, out string sendStatus))
            {
                status = sendStatus;
                return false;
            }

            status = $"{sendStatus} Learned Tesla template targetCount={template.TargetCount} opcode=0x{template.Opcode:X}.";
            LastStatus = status;
            return true;
        }

        internal void ResolveTeslaCoilAttackRequest(int summonObjectId, int requestedAt, string resolutionSource)
        {
            if (summonObjectId <= 0 || requestedAt == int.MinValue)
            {
                return;
            }

            lock (_sync)
            {
                for (int i = _pendingTeslaAttackCaptures.Count - 1; i >= 0; i--)
                {
                    Sg88ManualAttackCapture pendingCapture = _pendingTeslaAttackCaptures[i];
                    if (pendingCapture.SummonObjectId != summonObjectId || pendingCapture.RequestedAt != requestedAt)
                    {
                        continue;
                    }

                    ArchiveTeslaAttackCapture(pendingCapture, string.IsNullOrWhiteSpace(resolutionSource) ? "resolved" : resolutionSource.Trim());
                    _pendingTeslaAttackCaptures.RemoveAt(i);
                    break;
                }
            }
        }

        internal static bool TryCreateBridgeMessageFromRawPacket(byte[] rawPacket, string source, out SummonedPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (!SummonedPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out error))
            {
                return false;
            }

            if (!IsBridgeOpcode(packetType))
            {
                error = $"Unsupported summoned bridge opcode {packetType}.";
                return false;
            }

            message = new SummonedPacketInboxMessage(
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

        public static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            candidate = default;
            status = null;

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = localPort.HasValue
                ? candidates.Where(candidateValue => candidateValue.LocalEndpoint.Port == localPort.Value).ToArray()
                : candidates;
            if (filteredCandidates.Count == 0)
            {
                status = $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                status = $"Found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}; add a local port filter to disambiguate.";
                return false;
            }

            CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate selected = filteredCandidates[0];
            candidate = new SessionDiscoveryCandidate(
                selected.ProcessId,
                selected.ProcessName,
                selected.LocalEndpoint,
                selected.RemoteEndpoint);
            return true;
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => AcceptClientAsync(client, cancellationToken), cancellationToken);
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
                LastStatus = $"Summoned official-session bridge error: {ex.Message}";
            }
        }

        private async Task AcceptClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;

            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        client.Close();
                        LastStatus = "Summoned official-session bridge rejected an additional client because one Maple session is already attached.";
                        return;
                    }
                }

                TcpClient server = new();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Summoned official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Summoned official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Summoned official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Summoned official-session bridge connect failed: {ex.Message}";
            }
        }

        private void HandleServerPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                byte[] raw = packet.ToArray();
                if (isInit)
                {
                    PacketReader initReader = new(raw);
                    initReader.ReadShort();
                    pair.Version = initReader.ReadShort();
                    string patchLocation = initReader.ReadMapleString();
                    byte[] clientSendIv = initReader.ReadBytes(4);
                    byte[] clientReceiveIv = initReader.ReadBytes(4);
                    byte serverType = initReader.ReadByte();

                    pair.ClientSession.SIV = CreateCrypto(clientReceiveIv, pair.Version);
                    pair.ClientSession.RIV = CreateCrypto(clientSendIv, pair.Version);
                    pair.ClientSession.SendInitialPacket(pair.Version, patchLocation, clientSendIv, clientReceiveIv, serverType);
                    pair.InitCompleted = true;
                    int flushed = FlushQueuedOutboundPackets(pair);
                    LastStatus = flushed > 0
                        ? $"Summoned official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued outbound packet(s)."
                        : $"Summoned official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryCreateBridgeMessageFromRawPacket(raw, $"official-session:{pair.RemoteEndpoint}", out SummonedPacketInboxMessage message, out _))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued {SummonedPacketInboxManager.DescribePacketType(message.PacketType)} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Summoned official-session server handling failed: {ex.Message}");
            }
        }

        private void HandleClientPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                if (isInit)
                {
                    return;
                }

                byte[] raw = packet.ToArray();
                pair.ServerSession.SendPacket((byte[])raw.Clone());
                ForwardedOutboundCount++;
                RecordObservedOutboundPacket(raw, $"official-session:{pair.ClientEndpoint}");
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Summoned official-session client handling failed: {ex.Message}");
            }
        }

        private void ClearActivePair(BridgePair pair, string status)
        {
            lock (_sync)
            {
                if (_activePair != pair)
                {
                    return;
                }

                _activePair = null;
                LastStatus = status;
            }

            pair?.Close();
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

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
                _activePair = null;
            }

            pair?.Close();
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                ForwardedOutboundCount = 0;
                _recentOutboundPackets.Clear();
                _pendingSg88ManualAttackCaptures.Clear();
                _recentSg88ManualAttackCaptures.Clear();
                _learnedSg88ManualAttackTemplates.Clear();
                while (_pendingOutboundPackets.TryDequeue(out _))
                {
                }

                SentCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
                QueuedCount = 0;
                LastQueuedOpcode = -1;
                LastQueuedRawPacket = Array.Empty<byte>();
            }
        }

        private int FlushQueuedOutboundPackets(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundPackets.TryPeek(out PendingOutboundPacket packet))
            {
                pair.ServerSession.SendPacket(packet.RawPacket);
                if (!_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket dequeuedPacket))
                {
                    break;
                }

                SentCount++;
                LastSentOpcode = dequeuedPacket.Opcode;
                LastSentRawPacket = dequeuedPacket.RawPacket;
                RecordObservedOutboundPacket(dequeuedPacket.RawPacket, dequeuedPacket.ObservedSource);
                flushed++;
            }

            return flushed;
        }

        internal static bool TryValidateOutboundRawPacket(byte[] rawPacket, out int opcode, out string error)
        {
            opcode = -1;
            error = "Summoned outbound raw packet is missing.";
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            opcode = rawPacket[0] | (rawPacket[1] << 8);
            error = null;
            return true;
        }

        private static string NormalizeObservedOutboundSource(string source, string fallbackSource)
        {
            return string.IsNullOrWhiteSpace(source)
                ? fallbackSource
                : source.Trim();
        }

        internal static bool TryDecodeObservedOutboundPacket(byte[] rawPacket, string source, out OutboundPacketTrace trace)
        {
            trace = default;
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out _))
            {
                return false;
            }

            byte[] payload = rawPacket.Length > sizeof(ushort)
                ? rawPacket[sizeof(ushort)..]
                : Array.Empty<byte>();
            trace = new OutboundPacketTrace(
                opcode,
                payload.Length,
                Convert.ToHexString(payload),
                Convert.ToHexString(rawPacket),
                string.IsNullOrWhiteSpace(source) ? "unknown-source" : source.Trim(),
                Environment.TickCount,
                null,
                null);
            return true;
        }

        private OutboundPacketTrace TryBindSg88ManualAttackCapture(OutboundPacketTrace trace)
        {
            Sg88ManualAttackCapture selectedCapture = null;
            Sg88ManualAttackTraceBinding selectedBinding = default;
            int selectedAgeMs = int.MaxValue;
            for (int i = _pendingSg88ManualAttackCaptures.Count - 1; i >= 0; i--)
            {
                Sg88ManualAttackCapture capture = _pendingSg88ManualAttackCaptures[i];
                Sg88ManualAttackTraceBinding binding = EvaluateSg88ManualAttackTraceBinding(
                    trace,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    capture.RequestedAt,
                    capture.CaptureWindowEndAt);
                if (!binding.IsWithinCaptureWindow)
                {
                    continue;
                }

                int ageMs = Math.Max(0, unchecked(trace.ObservedAt - capture.RequestedAt));
                if (selectedCapture != null
                    && (binding.Score < selectedBinding.Score
                        || (binding.Score == selectedBinding.Score && ageMs >= selectedAgeMs)))
                {
                    continue;
                }

                selectedCapture = capture;
                selectedBinding = binding;
                selectedAgeMs = ageMs;
            }

            if (selectedCapture != null)
            {
                OutboundPacketTrace boundTrace = trace with
                {
                    BoundSg88SummonObjectId = selectedCapture.SummonObjectId,
                    BoundSg88RequestedAt = selectedCapture.RequestedAt
                };
                selectedCapture.ObservedPackets.Add(boundTrace);
                TryAssignSg88ManualAttackRequestPacket(selectedCapture, boundTrace, selectedBinding);
                return boundTrace;
            }

            return trace;
        }

        private void PruneExpiredSg88ManualAttackCaptures(int currentTime)
        {
            for (int i = _pendingSg88ManualAttackCaptures.Count - 1; i >= 0; i--)
            {
                Sg88ManualAttackCapture pendingCapture = _pendingSg88ManualAttackCaptures[i];
                if (currentTime <= pendingCapture.CaptureWindowEndAt)
                {
                    continue;
                }

                ArchiveSg88ManualAttackCapture(pendingCapture, "expired");
                _pendingSg88ManualAttackCaptures.RemoveAt(i);
            }
        }

        private void ArchiveSg88ManualAttackCapture(Sg88ManualAttackCapture capture, string resolutionSource)
        {
            if (capture == null)
            {
                return;
            }

            capture.ResolutionSource = string.IsNullOrWhiteSpace(resolutionSource) ? "resolved" : resolutionSource.Trim();
            UpdateLearnedSg88ManualAttackTemplateResolution(capture);
            while (_recentSg88ManualAttackCaptures.Count >= MaxRecentSg88ManualAttackRequests)
            {
                _recentSg88ManualAttackCaptures.Dequeue();
            }

            _recentSg88ManualAttackCaptures.Enqueue(capture);
        }

        private OutboundPacketTrace TryBindTeslaAttackCapture(OutboundPacketTrace trace)
        {
            Sg88ManualAttackCapture selectedCapture = null;
            Sg88ManualAttackTraceBinding selectedBinding = default;
            int selectedAgeMs = int.MaxValue;
            for (int i = _pendingTeslaAttackCaptures.Count - 1; i >= 0; i--)
            {
                Sg88ManualAttackCapture capture = _pendingTeslaAttackCaptures[i];
                Sg88ManualAttackTraceBinding binding = EvaluateSg88ManualAttackTraceBinding(
                    trace,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    capture.RequestedAt,
                    capture.CaptureWindowEndAt);
                if (!binding.IsWithinCaptureWindow)
                {
                    continue;
                }

                int ageMs = Math.Max(0, unchecked(trace.ObservedAt - capture.RequestedAt));
                if (selectedCapture != null
                    && (binding.Score < selectedBinding.Score
                        || (binding.Score == selectedBinding.Score && ageMs >= selectedAgeMs)))
                {
                    continue;
                }

                selectedCapture = capture;
                selectedBinding = binding;
                selectedAgeMs = ageMs;
            }

            if (selectedCapture != null)
            {
                selectedCapture.ObservedPackets.Add(trace);
                TryAssignTeslaAttackRequestPacket(selectedCapture, trace, selectedBinding);
            }

            return trace;
        }

        private void PruneExpiredTeslaAttackCaptures(int currentTime)
        {
            for (int i = _pendingTeslaAttackCaptures.Count - 1; i >= 0; i--)
            {
                Sg88ManualAttackCapture pendingCapture = _pendingTeslaAttackCaptures[i];
                if (currentTime <= pendingCapture.CaptureWindowEndAt)
                {
                    continue;
                }

                ArchiveTeslaAttackCapture(pendingCapture, "expired");
                _pendingTeslaAttackCaptures.RemoveAt(i);
            }
        }

        private void ArchiveTeslaAttackCapture(Sg88ManualAttackCapture capture, string resolutionSource)
        {
            if (capture == null)
            {
                return;
            }

            capture.ResolutionSource = string.IsNullOrWhiteSpace(resolutionSource) ? "resolved" : resolutionSource.Trim();
            UpdateLearnedTeslaAttackTemplateResolution(capture);
            while (_recentTeslaAttackCaptures.Count >= MaxRecentTeslaAttackRequests)
            {
                _recentTeslaAttackCaptures.Dequeue();
            }

            _recentTeslaAttackCaptures.Enqueue(capture);
        }

        private void UpdateLearnedSg88ManualAttackTemplateResolution(Sg88ManualAttackCapture capture)
        {
            if (capture?.RequestPacket is not OutboundPacketTrace requestPacket
                || capture.TargetMobIds.Length <= 0
                || !_learnedSg88ManualAttackTemplates.TryGetValue(capture.TargetMobIds.Length, out List<LearnedSg88ManualAttackTemplate> templates))
            {
                return;
            }

            foreach (LearnedSg88ManualAttackTemplate template in templates)
            {
                if (template.RequestedAt == capture.RequestedAt
                    && template.ObservedAt == requestPacket.ObservedAt
                    && template.PrimaryTargetMobId == capture.PrimaryTargetMobId
                    && template.TargetMobIds.SequenceEqual(capture.TargetMobIds))
                {
                    template.ResolutionSource = capture.ResolutionSource;
                }
            }
        }

        private void UpdateLearnedTeslaAttackTemplateResolution(Sg88ManualAttackCapture capture)
        {
            if (capture?.RequestPacket is not OutboundPacketTrace requestPacket
                || capture.TargetMobIds.Length <= 0
                || !_learnedTeslaAttackTemplates.TryGetValue(capture.TargetMobIds.Length, out List<LearnedSg88ManualAttackTemplate> templates))
            {
                return;
            }

            foreach (LearnedSg88ManualAttackTemplate template in templates)
            {
                if (template.RequestedAt == capture.RequestedAt
                    && template.ObservedAt == requestPacket.ObservedAt
                    && template.PrimaryTargetMobId == capture.PrimaryTargetMobId
                    && template.TargetMobIds.SequenceEqual(capture.TargetMobIds))
                {
                    template.ResolutionSource = capture.ResolutionSource;
                }
            }
        }

        internal static Sg88ManualAttackTraceBinding EvaluateSg88ManualAttackTraceBinding(
            OutboundPacketTrace trace,
            int summonObjectId,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            int requestedAt,
            int captureWindowEndAt)
        {
            if (trace.ObservedAt < requestedAt || trace.ObservedAt > captureWindowEndAt)
            {
                return default;
            }

            byte[] payload = TryDecodeObservedPayloadHex(trace.PayloadHex);
            bool matchedSummonObjectId = summonObjectId > 0 && PayloadContainsInt32(payload, summonObjectId);
            bool matchedPrimaryTargetMobId = primaryTargetMobId > 0 && PayloadContainsInt32(payload, primaryTargetMobId);
            int matchedTargetCount = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .Count(mobId => PayloadContainsInt32(payload, mobId)) ?? 0;
            int score = 1;
            if (matchedSummonObjectId)
            {
                score += 8;
            }

            if (matchedPrimaryTargetMobId)
            {
                score += 4;
            }

            score += Math.Min(4, matchedTargetCount);
            string evidence = BuildTraceBindingEvidence(
                matchedSummonObjectId,
                matchedPrimaryTargetMobId,
                matchedTargetCount);
            return new Sg88ManualAttackTraceBinding(
                true,
                matchedSummonObjectId,
                matchedPrimaryTargetMobId,
                matchedTargetCount,
                score,
                evidence);
        }

        private static int CalculateCaptureWindowEndAt(int requestedAt, int baseDelayMs, int followUpDelayMs)
        {
            long totalDelay = (long)Math.Max(0, baseDelayMs) + Math.Max(0, followUpDelayMs) + Sg88ManualAttackCaptureGraceMs;
            long captureWindowEndAt = (long)requestedAt + totalDelay;
            if (captureWindowEndAt > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (captureWindowEndAt < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)captureWindowEndAt;
        }

        private static int CalculateTeslaCaptureWindowEndAt(int requestedAt, int baseDelayMs)
        {
            long totalDelay = (long)Math.Max(0, baseDelayMs) + TeslaAttackCaptureGraceMs;
            long captureWindowEndAt = (long)requestedAt + totalDelay;
            if (captureWindowEndAt > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (captureWindowEndAt < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)captureWindowEndAt;
        }

        private static string FormatBoundSg88Request(OutboundPacketTrace trace)
        {
            return trace.BoundSg88SummonObjectId.HasValue && trace.BoundSg88RequestedAt.HasValue
                ? $" sg88Summon={trace.BoundSg88SummonObjectId.Value} sg88RequestedAt={trace.BoundSg88RequestedAt.Value}"
                : string.Empty;
        }

        private static string FormatObservedPacketList(IReadOnlyList<OutboundPacketTrace> observedPackets, OutboundPacketTrace? requestPacket)
        {
            if (observedPackets == null || observedPackets.Count == 0)
            {
                return "none";
            }

            return string.Join(
                ",",
                observedPackets.Select(packet =>
                {
                    bool isRequestPacket = requestPacket.HasValue
                        && packet.Opcode == requestPacket.Value.Opcode
                        && packet.ObservedAt == requestPacket.Value.ObservedAt
                        && string.Equals(packet.PayloadHex, requestPacket.Value.PayloadHex, StringComparison.Ordinal);
                    return $"{(isRequestPacket ? "*" : string.Empty)}0x{packet.Opcode:X}@{packet.ObservedAt}[{packet.PayloadLength}]";
                }));
        }

        private static string FormatRequestPacket(Sg88ManualAttackCapture capture)
        {
            if (capture?.RequestPacket is not OutboundPacketTrace requestPacket)
            {
                return "unresolved";
            }

            string evidence = string.IsNullOrWhiteSpace(capture.RequestPacketEvidence)
                ? "window-only"
                : capture.RequestPacketEvidence;
            return $"0x{requestPacket.Opcode:X}@{requestPacket.ObservedAt}[{requestPacket.PayloadLength}] score={capture.RequestPacketScore} evidence={evidence} source={requestPacket.Source}";
        }

        private void TryAssignSg88ManualAttackRequestPacket(
            Sg88ManualAttackCapture capture,
            OutboundPacketTrace trace,
            Sg88ManualAttackTraceBinding binding)
        {
            if (capture == null || !binding.HasSemanticEvidence)
            {
                return;
            }

            if (!IsEligibleSg88TemplateEvidenceSource(trace.Source))
            {
                return;
            }

            // Only promote traces that can actually be turned back into a reusable request template.
            if (!TryCreateSg88ManualAttackRequestTemplate(
                    trace,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    out _,
                    out _))
            {
                return;
            }

            int candidateAgeMs = Math.Max(0, unchecked(trace.ObservedAt - capture.RequestedAt));
            int existingAgeMs = capture.RequestPacket.HasValue
                ? Math.Max(0, unchecked(capture.RequestPacket.Value.ObservedAt - capture.RequestedAt))
                : int.MaxValue;
            if (capture.RequestPacket.HasValue
                && (binding.Score < capture.RequestPacketScore
                    || (binding.Score == capture.RequestPacketScore && candidateAgeMs >= existingAgeMs)))
            {
                return;
            }

            capture.RequestPacket = trace;
            capture.RequestPacketScore = binding.Score;
            capture.RequestPacketEvidence = binding.Evidence;
            PromoteLearnedSg88ManualAttackTemplate(capture, trace, binding);
        }

        private void TryAssignTeslaAttackRequestPacket(
            Sg88ManualAttackCapture capture,
            OutboundPacketTrace trace,
            Sg88ManualAttackTraceBinding binding)
        {
            if (capture == null || !binding.HasSemanticEvidence)
            {
                return;
            }

            if (capture.RequestPacket is OutboundPacketTrace existingRequestPacket
                && capture.RequestPacketScore > binding.Score)
            {
                return;
            }

            if (capture.RequestPacket is OutboundPacketTrace tiedRequestPacket
                && capture.RequestPacketScore == binding.Score
                && tiedRequestPacket.ObservedAt > trace.ObservedAt)
            {
                return;
            }

            if (!TryCreateSg88ManualAttackRequestTemplate(
                    trace,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    out _,
                    out _))
            {
                return;
            }

            capture.RequestPacket = trace;
            capture.RequestPacketScore = binding.Score;
            capture.RequestPacketEvidence = binding.Evidence;
            PromoteLearnedTeslaAttackTemplate(capture, trace, binding);
        }

        private static string BuildTraceBindingEvidence(
            bool matchedSummonObjectId,
            bool matchedPrimaryTargetMobId,
            int matchedTargetCount)
        {
            List<string> evidence = new();
            if (matchedSummonObjectId)
            {
                evidence.Add("summon");
            }

            if (matchedPrimaryTargetMobId)
            {
                evidence.Add("primary");
            }

            if (matchedTargetCount > 0)
            {
                evidence.Add($"targets:{matchedTargetCount}");
            }

            return evidence.Count == 0
                ? "window"
                : string.Join("+", evidence);
        }

        private static byte[] TryDecodeObservedPayloadHex(string payloadHex)
        {
            if (string.IsNullOrWhiteSpace(payloadHex))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return Convert.FromHexString(payloadHex);
            }
            catch (FormatException)
            {
                return Array.Empty<byte>();
            }
        }

        private static byte[] TryDecodeObservedRawPacketHex(string rawPacketHex)
        {
            if (string.IsNullOrWhiteSpace(rawPacketHex))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return Convert.FromHexString(rawPacketHex);
            }
            catch (FormatException)
            {
                return Array.Empty<byte>();
            }
        }

        internal static bool TryCreateSg88ManualAttackRequestTemplate(
            OutboundPacketTrace requestPacket,
            int summonObjectId,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out Sg88ManualAttackRequestPacketTemplate template,
            out string error)
        {
            template = default;
            error = "SG-88 request packet template is missing.";
            byte[] rawPacket = TryDecodeObservedRawPacketHex(requestPacket.RawPacketHex);
            if (!TryValidateOutboundRawPacket(rawPacket, out int opcode, out _))
            {
                return false;
            }

            byte[] payload = rawPacket.Length > sizeof(ushort)
                ? rawPacket[sizeof(ushort)..]
                : Array.Empty<byte>();
            if (payload.Length < sizeof(int))
            {
                error = "SG-88 request packet template payload is too short.";
                return false;
            }

            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (resolvedTargetMobIds.Length == 0)
            {
                error = "SG-88 request packet template requires at least one target mob id.";
                return false;
            }

            int[] summonPayloadOffsets = FindAllInt32Offsets(payload, summonObjectId);
            if (summonObjectId <= 0 || summonPayloadOffsets.Length == 0)
            {
                error = "SG-88 request packet template could not locate the summon object id in the captured payload.";
                return false;
            }

            int[] primaryPayloadOffsets = FindAllInt32Offsets(payload, primaryTargetMobId);
            if (primaryTargetMobId <= 0 || primaryPayloadOffsets.Length == 0)
            {
                error = "SG-88 request packet template could not locate the primary target mob id in the captured payload.";
                return false;
            }

            if (!TryMapTargetPayloadOffsets(payload, resolvedTargetMobIds, summonPayloadOffsets, out int[] targetPayloadOffsets))
            {
                error = "SG-88 request packet template could not map every admitted target mob id onto the captured payload.";
                return false;
            }

            primaryPayloadOffsets = ResolvePrimaryPayloadOffsets(
                payload,
                primaryTargetMobId,
                summonPayloadOffsets,
                targetPayloadOffsets);
            if (primaryPayloadOffsets.Length == 0)
            {
                error = "SG-88 request packet template could not isolate a reusable primary target mob id field.";
                return false;
            }

            template = new Sg88ManualAttackRequestPacketTemplate(
                opcode,
                (byte[])rawPacket.Clone(),
                summonPayloadOffsets.Select(static offset => offset + sizeof(ushort)).ToArray(),
                primaryPayloadOffsets.Select(static offset => offset + sizeof(ushort)).ToArray(),
                targetPayloadOffsets.Select(static offset => offset + sizeof(ushort)).ToArray(),
                resolvedTargetMobIds.Length);
            error = null;
            return true;
        }

        internal static bool TryBuildSg88ManualAttackRequestRawPacket(
            Sg88ManualAttackRequestPacketTemplate template,
            int summonObjectId,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out byte[] rawPacket,
            out string error)
        {
            rawPacket = Array.Empty<byte>();
            error = "SG-88 request packet template is missing.";
            if (template.RawPacket == null || template.RawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            int[] resolvedTargetMobIds = targetMobIds?
                .Where(static mobId => mobId > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();
            if (summonObjectId <= 0 || primaryTargetMobId <= 0 || resolvedTargetMobIds.Length == 0)
            {
                error = "SG-88 request packet replay requires summon, primary target, and admitted target ids.";
                return false;
            }

            if (resolvedTargetMobIds.Length != template.TargetMobIdOffsets.Length)
            {
                error = $"SG-88 request packet replay requires {template.TargetMobIdOffsets.Length} target id slot(s), but the active request has {resolvedTargetMobIds.Length}.";
                return false;
            }

            rawPacket = (byte[])template.RawPacket.Clone();
            foreach (int offset in template.SummonObjectIdOffsets)
            {
                if (!TryWriteInt32LittleEndian(rawPacket, offset, summonObjectId))
                {
                    error = "SG-88 request packet replay could not rewrite the summon object id.";
                    rawPacket = Array.Empty<byte>();
                    return false;
                }
            }

            foreach (int offset in template.PrimaryTargetMobIdOffsets)
            {
                if (!TryWriteInt32LittleEndian(rawPacket, offset, primaryTargetMobId))
                {
                    error = "SG-88 request packet replay could not rewrite the primary target mob id.";
                    rawPacket = Array.Empty<byte>();
                    return false;
                }
            }

            for (int i = 0; i < template.TargetMobIdOffsets.Length; i++)
            {
                if (!TryWriteInt32LittleEndian(rawPacket, template.TargetMobIdOffsets[i], resolvedTargetMobIds[i]))
                {
                    error = $"SG-88 request packet replay could not rewrite target mob slot {i}.";
                    rawPacket = Array.Empty<byte>();
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal bool TryResolveLearnedSg88ManualAttackRequestTemplate(
            int targetCount,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out Sg88ManualAttackRequestPacketTemplate template,
            out string status)
        {
            template = default;
            status = "No learned SG-88 request packet template is available yet.";
            if (targetCount <= 0)
            {
                return false;
            }

            PruneExpiredSg88ManualAttackCaptures(Environment.TickCount);
            int[] resolvedTargetMobIds = NormalizeTargetMobIds(targetMobIds);

            Sg88ManualAttackCapture selectedCapture = null;
            bool selectedCaptureIsPending = false;
            int selectedObservedAt = int.MinValue;
            Sg88ManualAttackTemplateLanePreference selectedLanePreference = default;
            Sg88ManualAttackRequestPacketTemplate selectedTemplate = default;

            bool TryConsiderCapture(Sg88ManualAttackCapture capture, bool isPendingCapture)
            {
                if (capture?.RequestPacket is not OutboundPacketTrace requestPacket
                    || capture.TargetMobIds.Length != targetCount
                    || !IsEligibleSg88TemplateEvidenceSource(requestPacket.Source))
                {
                    return false;
                }

                if (!TryCreateSg88ManualAttackRequestTemplate(
                        requestPacket,
                        capture.SummonObjectId,
                        capture.PrimaryTargetMobId,
                        capture.TargetMobIds,
                        out Sg88ManualAttackRequestPacketTemplate candidateTemplate,
                        out _))
                {
                    return false;
                }

                Sg88ManualAttackTemplateLanePreference lanePreference = EvaluateSg88TemplateLanePreference(
                    primaryTargetMobId,
                    resolvedTargetMobIds,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds);
                if (selectedCapture != null
                    && !ShouldPreferSg88CaptureTemplateCandidate(
                        lanePreference,
                        capture.ResolutionSource,
                        requestPacket.ObservedAt,
                        selectedLanePreference,
                        selectedCapture.ResolutionSource,
                        selectedObservedAt))
                {
                    return false;
                }

                selectedTemplate = candidateTemplate;
                selectedCapture = capture;
                selectedCaptureIsPending = isPendingCapture;
                selectedObservedAt = requestPacket.ObservedAt;
                selectedLanePreference = lanePreference;
                return true;
            }

            foreach (Sg88ManualAttackCapture capture in _pendingSg88ManualAttackCaptures.AsEnumerable().Reverse())
            {
                TryConsiderCapture(capture, isPendingCapture: true);
            }

            foreach (Sg88ManualAttackCapture capture in _recentSg88ManualAttackCaptures.Reverse())
            {
                TryConsiderCapture(capture, isPendingCapture: false);
            }

            if (selectedCapture?.RequestPacket is OutboundPacketTrace selectedRequestPacket)
            {
                template = selectedTemplate;
                string captureState = selectedCaptureIsPending
                    ? "live official capture"
                    : $"archived official capture ({selectedCapture.ResolutionSource ?? "resolved"})";
                status =
                    $"Using learned SG-88 request template from opcode 0x{template.Opcode:X} captured at tick {selectedCapture.RequestedAt} via {captureState} laneScore={selectedLanePreference.LaneScore}.";
                return true;
            }

            if (_learnedSg88ManualAttackTemplates.TryGetValue(targetCount, out List<LearnedSg88ManualAttackTemplate> learnedTemplates)
                && learnedTemplates.Count > 0)
            {
                LearnedSg88ManualAttackTemplate learnedTemplate = null;
                Sg88ManualAttackTemplateLanePreference lanePreference = default;
                int learnedObservedAt = int.MinValue;
                foreach (LearnedSg88ManualAttackTemplate candidate in learnedTemplates)
                {
                    Sg88ManualAttackTemplateLanePreference candidatePreference = EvaluateSg88TemplateLanePreference(
                        primaryTargetMobId,
                        resolvedTargetMobIds,
                        candidate.PrimaryTargetMobId,
                        candidate.TargetMobIds);
                    if (learnedTemplate != null
                        && !ShouldPreferSg88LearnedTemplateCandidate(
                            candidatePreference,
                            candidate.ResolutionSource,
                            candidate.ObservedAt,
                            lanePreference,
                            learnedTemplate.ResolutionSource,
                            learnedObservedAt))
                    {
                        continue;
                    }

                    learnedTemplate = candidate;
                    lanePreference = candidatePreference;
                    learnedObservedAt = candidate.ObservedAt;
                }

                if (learnedTemplate == null)
                {
                    status = $"No learned SG-88 request packet template matched targetCount={targetCount}.";
                    return false;
                }

                template = learnedTemplate.Template;
                string resolution = string.IsNullOrWhiteSpace(learnedTemplate.ResolutionSource)
                    ? "cached official capture"
                    : $"cached official capture ({learnedTemplate.ResolutionSource})";
                status =
                    $"Using cached learned SG-88 request template from opcode 0x{template.Opcode:X} captured at tick {learnedTemplate.RequestedAt} via {resolution} laneScore={lanePreference.LaneScore}.";
                return true;
            }

            status = $"No learned SG-88 request packet template matched targetCount={targetCount}.";
            return false;
        }

        internal bool TryResolveLearnedTeslaAttackRequestTemplate(
            int targetCount,
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            out Sg88ManualAttackRequestPacketTemplate template,
            out string status)
        {
            template = default;
            status = "No learned Tesla request packet template is available yet.";
            if (targetCount <= 0)
            {
                return false;
            }

            PruneExpiredTeslaAttackCaptures(Environment.TickCount);
            int[] resolvedTargetMobIds = NormalizeTargetMobIds(targetMobIds);

            Sg88ManualAttackCapture selectedCapture = null;
            bool selectedCaptureIsPending = false;
            int selectedObservedAt = int.MinValue;
            Sg88ManualAttackTemplateLanePreference selectedLanePreference = default;
            Sg88ManualAttackRequestPacketTemplate selectedTemplate = default;

            bool TryConsiderCapture(Sg88ManualAttackCapture capture, bool isPendingCapture)
            {
                if (capture?.RequestPacket is not OutboundPacketTrace requestPacket
                    || capture.TargetMobIds.Length != targetCount
                    || !IsEligibleSg88TemplateEvidenceSource(requestPacket.Source))
                {
                    return false;
                }

                if (!TryCreateSg88ManualAttackRequestTemplate(
                        requestPacket,
                        capture.SummonObjectId,
                        capture.PrimaryTargetMobId,
                        capture.TargetMobIds,
                        out Sg88ManualAttackRequestPacketTemplate candidateTemplate,
                        out _))
                {
                    return false;
                }

                Sg88ManualAttackTemplateLanePreference candidatePreference = EvaluateSg88TemplateLanePreference(
                    primaryTargetMobId,
                    resolvedTargetMobIds,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds);
                if (selectedCapture != null
                    && !ShouldPreferSg88CaptureTemplateCandidate(
                        candidatePreference,
                        capture.ResolutionSource,
                        requestPacket.ObservedAt,
                        selectedLanePreference,
                        selectedCapture.ResolutionSource,
                        selectedObservedAt))
                {
                    return false;
                }

                selectedCapture = capture;
                selectedCaptureIsPending = isPendingCapture;
                selectedObservedAt = requestPacket.ObservedAt;
                selectedLanePreference = candidatePreference;
                selectedTemplate = candidateTemplate;
                return true;
            }

            foreach (Sg88ManualAttackCapture capture in _pendingTeslaAttackCaptures)
            {
                TryConsiderCapture(capture, isPendingCapture: true);
            }

            foreach (Sg88ManualAttackCapture capture in _recentTeslaAttackCaptures)
            {
                TryConsiderCapture(capture, isPendingCapture: false);
            }

            if (selectedCapture?.RequestPacket is OutboundPacketTrace)
            {
                template = selectedTemplate;
                string captureState = selectedCaptureIsPending
                    ? "live official capture"
                    : $"archived official capture ({selectedCapture.ResolutionSource ?? "resolved"})";
                status =
                    $"Using learned Tesla request template from opcode 0x{template.Opcode:X} captured at tick {selectedCapture.RequestedAt} via {captureState} laneScore={selectedLanePreference.LaneScore}.";
                return true;
            }

            if (_learnedTeslaAttackTemplates.TryGetValue(targetCount, out List<LearnedSg88ManualAttackTemplate> learnedTemplates)
                && learnedTemplates.Count > 0)
            {
                LearnedSg88ManualAttackTemplate learnedTemplate = null;
                Sg88ManualAttackTemplateLanePreference lanePreference = default;
                int learnedObservedAt = int.MinValue;
                foreach (LearnedSg88ManualAttackTemplate candidate in learnedTemplates)
                {
                    Sg88ManualAttackTemplateLanePreference candidatePreference = EvaluateSg88TemplateLanePreference(
                        primaryTargetMobId,
                        resolvedTargetMobIds,
                        candidate.PrimaryTargetMobId,
                        candidate.TargetMobIds);
                    if (learnedTemplate != null
                        && !ShouldPreferSg88LearnedTemplateCandidate(
                            candidatePreference,
                            candidate.ResolutionSource,
                            candidate.ObservedAt,
                            lanePreference,
                            learnedTemplate.ResolutionSource,
                            learnedObservedAt))
                    {
                        continue;
                    }

                    learnedTemplate = candidate;
                    lanePreference = candidatePreference;
                    learnedObservedAt = candidate.ObservedAt;
                }

                if (learnedTemplate == null)
                {
                    status = $"No learned Tesla request packet template matched targetCount={targetCount}.";
                    return false;
                }

                template = learnedTemplate.Template;
                string resolution = string.IsNullOrWhiteSpace(learnedTemplate.ResolutionSource)
                    ? "cached official capture"
                    : $"cached official capture ({learnedTemplate.ResolutionSource})";
                status =
                    $"Using cached learned Tesla request template from opcode 0x{template.Opcode:X} captured at tick {learnedTemplate.RequestedAt} via {resolution} laneScore={lanePreference.LaneScore}.";
                return true;
            }

            status = $"No learned Tesla request packet template matched targetCount={targetCount}.";
            return false;
        }

        private static bool IsEligibleSg88TemplateEvidenceSource(string source)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.StartsWith(OfficialSessionTraceSourcePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetSg88TemplateResolutionRank(string resolutionSource)
        {
            if (string.IsNullOrWhiteSpace(resolutionSource))
            {
                return 0;
            }

            return resolutionSource.Trim() switch
            {
                "1021-confirm" => 4,
                "0x119-target-cover" => 3,
                "expired" => 2,
                "superseded" => 1,
                _ => 1
            };
        }

        private static string PreferSg88TemplateResolutionSource(string candidateResolutionSource, string existingResolutionSource)
        {
            int candidateRank = GetSg88TemplateResolutionRank(candidateResolutionSource);
            int existingRank = GetSg88TemplateResolutionRank(existingResolutionSource);
            if (candidateRank != existingRank)
            {
                return candidateRank >= existingRank
                    ? candidateResolutionSource
                    : existingResolutionSource;
            }

            return string.IsNullOrWhiteSpace(candidateResolutionSource)
                ? existingResolutionSource
                : candidateResolutionSource;
        }

        private static int GetTeslaTemplateResolutionRank(string resolutionSource)
        {
            if (string.IsNullOrWhiteSpace(resolutionSource))
            {
                return 0;
            }

            return resolutionSource.Trim() switch
            {
                "0x119-target-cover" => 3,
                "expired" => 2,
                "superseded" => 1,
                _ => 1
            };
        }

        private static string PreferTeslaTemplateResolutionSource(string candidateResolutionSource, string existingResolutionSource)
        {
            int candidateRank = GetTeslaTemplateResolutionRank(candidateResolutionSource);
            int existingRank = GetTeslaTemplateResolutionRank(existingResolutionSource);
            if (candidateRank != existingRank)
            {
                return candidateRank >= existingRank
                    ? candidateResolutionSource
                    : existingResolutionSource;
            }

            return string.IsNullOrWhiteSpace(candidateResolutionSource)
                ? existingResolutionSource
                : candidateResolutionSource;
        }

        private void PromoteLearnedSg88ManualAttackTemplate(
            Sg88ManualAttackCapture capture,
            OutboundPacketTrace trace,
            Sg88ManualAttackTraceBinding binding)
        {
            if (capture?.RequestPacket is not OutboundPacketTrace requestPacket
                || !IsEligibleSg88TemplateEvidenceSource(trace.Source))
            {
                return;
            }

            if (!TryCreateSg88ManualAttackRequestTemplate(
                    requestPacket,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    out Sg88ManualAttackRequestPacketTemplate template,
                    out _))
            {
                return;
            }

            int targetCount = capture.TargetMobIds.Length;
            if (targetCount <= 0)
            {
                return;
            }

            if (!_learnedSg88ManualAttackTemplates.TryGetValue(targetCount, out List<LearnedSg88ManualAttackTemplate> templates))
            {
                templates = new List<LearnedSg88ManualAttackTemplate>();
                _learnedSg88ManualAttackTemplates[targetCount] = templates;
            }

            string preferredResolutionSource = capture.ResolutionSource;
            int existingIndex = templates.FindIndex(candidate =>
                candidate.PrimaryTargetMobId == capture.PrimaryTargetMobId
                && candidate.TargetMobIds.SequenceEqual(capture.TargetMobIds));
            if (existingIndex >= 0)
            {
                LearnedSg88ManualAttackTemplate existingTemplate = templates[existingIndex];
                preferredResolutionSource = PreferSg88TemplateResolutionSource(
                    capture.ResolutionSource,
                    existingTemplate.ResolutionSource);
                existingTemplate.ResolutionSource = preferredResolutionSource;
                if (existingTemplate.ObservedAt > trace.ObservedAt
                    || (existingTemplate.ObservedAt == trace.ObservedAt
                        && string.CompareOrdinal(existingTemplate.Evidence, binding.Evidence) >= 0))
                {
                    return;
                }

                templates.RemoveAt(existingIndex);
            }

            templates.Add(new LearnedSg88ManualAttackTemplate
            {
                Template = template,
                RequestedAt = capture.RequestedAt,
                ObservedAt = trace.ObservedAt,
                Source = trace.Source,
                Evidence = binding.Evidence,
                PrimaryTargetMobId = capture.PrimaryTargetMobId,
                TargetMobIds = (int[])capture.TargetMobIds.Clone(),
                ResolutionSource = preferredResolutionSource
            });
            templates.Sort((left, right) =>
            {
                int observedCompare = right.ObservedAt.CompareTo(left.ObservedAt);
                if (observedCompare != 0)
                {
                    return observedCompare;
                }

                int resolutionCompare = GetSg88TemplateResolutionRank(right.ResolutionSource)
                    .CompareTo(GetSg88TemplateResolutionRank(left.ResolutionSource));
                if (resolutionCompare != 0)
                {
                    return resolutionCompare;
                }

                return string.CompareOrdinal(right.Evidence, left.Evidence);
            });

            while (templates.Count > MaxLearnedSg88ManualAttackTemplatesPerTargetCount)
            {
                templates.RemoveAt(templates.Count - 1);
            }
        }

        private void PromoteLearnedTeslaAttackTemplate(
            Sg88ManualAttackCapture capture,
            OutboundPacketTrace trace,
            Sg88ManualAttackTraceBinding binding)
        {
            string error = null;
            if (capture?.TargetMobIds == null
                || capture.TargetMobIds.Length == 0
                || !TryCreateSg88ManualAttackRequestTemplate(
                    trace,
                    capture.SummonObjectId,
                    capture.PrimaryTargetMobId,
                    capture.TargetMobIds,
                    out Sg88ManualAttackRequestPacketTemplate template,
                    out error))
            {
                LastStatus = string.IsNullOrWhiteSpace(error)
                    ? LastStatus
                    : error;
                return;
            }

            if (!_learnedTeslaAttackTemplates.TryGetValue(capture.TargetMobIds.Length, out List<LearnedSg88ManualAttackTemplate> templates))
            {
                templates = new List<LearnedSg88ManualAttackTemplate>();
                _learnedTeslaAttackTemplates[capture.TargetMobIds.Length] = templates;
            }

            string preferredResolutionSource = capture.ResolutionSource;
            LearnedSg88ManualAttackTemplate existingTemplate = templates.FirstOrDefault(candidate =>
                candidate.PrimaryTargetMobId == capture.PrimaryTargetMobId
                && candidate.TargetMobIds.SequenceEqual(capture.TargetMobIds));
            if (existingTemplate != null)
            {
                preferredResolutionSource = PreferTeslaTemplateResolutionSource(
                    capture.ResolutionSource,
                    existingTemplate.ResolutionSource);
                if (existingTemplate.ObservedAt > trace.ObservedAt)
                {
                    existingTemplate.ResolutionSource = preferredResolutionSource;
                    return;
                }

                templates.Remove(existingTemplate);
            }

            templates.Add(new LearnedSg88ManualAttackTemplate
            {
                Template = template,
                RequestedAt = capture.RequestedAt,
                ObservedAt = trace.ObservedAt,
                Source = trace.Source,
                Evidence = binding.Evidence,
                PrimaryTargetMobId = capture.PrimaryTargetMobId,
                TargetMobIds = capture.TargetMobIds.ToArray(),
                ResolutionSource = preferredResolutionSource
            });
            templates.Sort((left, right) =>
            {
                int observedCompare = right.ObservedAt.CompareTo(left.ObservedAt);
                if (observedCompare != 0)
                {
                    return observedCompare;
                }

                int resolutionCompare = GetTeslaTemplateResolutionRank(right.ResolutionSource)
                    .CompareTo(GetTeslaTemplateResolutionRank(left.ResolutionSource));
                if (resolutionCompare != 0)
                {
                    return resolutionCompare;
                }

                return string.CompareOrdinal(right.Evidence, left.Evidence);
            });

            while (templates.Count > MaxLearnedTeslaAttackTemplatesPerTargetCount)
            {
                templates.RemoveAt(templates.Count - 1);
            }
        }

        private static int[] NormalizeTargetMobIds(IReadOnlyList<int> targetMobIds)
        {
            if (targetMobIds == null || targetMobIds.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> normalized = new(targetMobIds.Count);
            HashSet<int> seen = new();
            foreach (int mobId in targetMobIds)
            {
                if (mobId <= 0 || !seen.Add(mobId))
                {
                    continue;
                }

                normalized.Add(mobId);
            }

            return normalized.ToArray();
        }

        internal static Sg88ManualAttackTemplateLanePreference EvaluateSg88TemplateLanePreference(
            int primaryTargetMobId,
            IReadOnlyList<int> targetMobIds,
            int candidatePrimaryTargetMobId,
            IReadOnlyList<int> candidateTargetMobIds)
        {
            int[] normalizedTargets = NormalizeTargetMobIds(targetMobIds);
            int[] normalizedCandidateTargets = NormalizeTargetMobIds(candidateTargetMobIds);
            bool samePrimary = primaryTargetMobId > 0
                && candidatePrimaryTargetMobId > 0
                && primaryTargetMobId == candidatePrimaryTargetMobId;
            bool sameOrderedTargets = normalizedTargets.SequenceEqual(normalizedCandidateTargets);
            bool sameTargetSet = normalizedTargets.Length == normalizedCandidateTargets.Length
                && normalizedTargets.OrderBy(static mobId => mobId)
                    .SequenceEqual(normalizedCandidateTargets.OrderBy(static mobId => mobId));
            int exactOrderedMatchCount = 0;
            int leadingOrderedMatchCount = 0;
            bool leadingMismatchSeen = false;
            for (int i = 0; i < Math.Min(normalizedTargets.Length, normalizedCandidateTargets.Length); i++)
            {
                if (normalizedTargets[i] != normalizedCandidateTargets[i])
                {
                    leadingMismatchSeen = true;
                    continue;
                }

                exactOrderedMatchCount++;
                if (!leadingMismatchSeen)
                {
                    leadingOrderedMatchCount++;
                }
            }

            int orderedMatchScore = (leadingOrderedMatchCount * 8) + (exactOrderedMatchCount * 2);
            if (samePrimary && sameOrderedTargets)
            {
                return new Sg88ManualAttackTemplateLanePreference(4, orderedMatchScore, leadingOrderedMatchCount, exactOrderedMatchCount);
            }

            if (samePrimary && sameTargetSet)
            {
                return new Sg88ManualAttackTemplateLanePreference(3, orderedMatchScore, leadingOrderedMatchCount, exactOrderedMatchCount);
            }

            if (samePrimary)
            {
                return new Sg88ManualAttackTemplateLanePreference(2, orderedMatchScore, leadingOrderedMatchCount, exactOrderedMatchCount);
            }

            if (sameTargetSet)
            {
                return new Sg88ManualAttackTemplateLanePreference(1, orderedMatchScore, leadingOrderedMatchCount, exactOrderedMatchCount);
            }

            return new Sg88ManualAttackTemplateLanePreference(0, orderedMatchScore, leadingOrderedMatchCount, exactOrderedMatchCount);
        }

        internal static bool ShouldPreferSg88CaptureTemplateCandidate(
            Sg88ManualAttackTemplateLanePreference candidate,
            string candidateResolutionSource,
            int candidateObservedAt,
            Sg88ManualAttackTemplateLanePreference selected,
            string selectedResolutionSource,
            int selectedObservedAt)
        {
            if (candidate.LaneScore != selected.LaneScore)
            {
                return candidate.LaneScore > selected.LaneScore;
            }

            if (candidate.LaneScore > 0
                && candidate.LeadingOrderedMatchCount != selected.LeadingOrderedMatchCount)
            {
                return candidate.LeadingOrderedMatchCount > selected.LeadingOrderedMatchCount;
            }

            if (candidate.LaneScore > 0
                && candidate.ExactOrderedMatchCount != selected.ExactOrderedMatchCount)
            {
                return candidate.ExactOrderedMatchCount > selected.ExactOrderedMatchCount;
            }

            if (candidate.LaneScore > 0
                && candidate.OrderedMatchScore != selected.OrderedMatchScore)
            {
                return candidate.OrderedMatchScore > selected.OrderedMatchScore;
            }

            if (candidate.LaneScore > 0)
            {
                int candidateResolutionRank = GetSg88TemplateResolutionRank(candidateResolutionSource);
                int selectedResolutionRank = GetSg88TemplateResolutionRank(selectedResolutionSource);
                if (candidateResolutionRank != selectedResolutionRank)
                {
                    return candidateResolutionRank > selectedResolutionRank;
                }
            }

            return candidateObservedAt >= selectedObservedAt;
        }

        internal static bool ShouldPreferSg88LearnedTemplateCandidate(
            Sg88ManualAttackTemplateLanePreference candidate,
            string candidateResolutionSource,
            int candidateObservedAt,
            Sg88ManualAttackTemplateLanePreference selected,
            string selectedResolutionSource,
            int selectedObservedAt)
        {
            if (candidate.LaneScore != selected.LaneScore)
            {
                return candidate.LaneScore > selected.LaneScore;
            }

            if (candidate.LaneScore > 0
                && candidate.LeadingOrderedMatchCount != selected.LeadingOrderedMatchCount)
            {
                return candidate.LeadingOrderedMatchCount > selected.LeadingOrderedMatchCount;
            }

            if (candidate.LaneScore > 0
                && candidate.ExactOrderedMatchCount != selected.ExactOrderedMatchCount)
            {
                return candidate.ExactOrderedMatchCount > selected.ExactOrderedMatchCount;
            }

            if (candidate.LaneScore > 0
                && candidate.OrderedMatchScore != selected.OrderedMatchScore)
            {
                return candidate.OrderedMatchScore > selected.OrderedMatchScore;
            }

            if (candidate.LaneScore > 0)
            {
                int candidateResolutionRank = GetSg88TemplateResolutionRank(candidateResolutionSource);
                int selectedResolutionRank = GetSg88TemplateResolutionRank(selectedResolutionSource);
                if (candidateResolutionRank != selectedResolutionRank)
                {
                    return candidateResolutionRank > selectedResolutionRank;
                }
            }

            return candidateObservedAt >= selectedObservedAt;
        }

        private static int[] FindAllInt32Offsets(byte[] payload, int value)
        {
            if (payload == null || payload.Length < sizeof(int))
            {
                return Array.Empty<int>();
            }

            List<int> offsets = new();
            for (int offset = 0; offset <= payload.Length - sizeof(int); offset++)
            {
                if (BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int))) == value)
                {
                    offsets.Add(offset);
                }
            }

            return offsets.ToArray();
        }

        private static bool TryMapTargetPayloadOffsets(
            byte[] payload,
            IReadOnlyList<int> targetMobIds,
            IReadOnlyList<int> reservedPayloadOffsets,
            out int[] offsets)
        {
            offsets = Array.Empty<int>();
            if (payload == null || payload.Length < sizeof(int) || targetMobIds == null || targetMobIds.Count == 0)
            {
                return false;
            }

            HashSet<int> reservedOffsets = reservedPayloadOffsets != null
                ? new HashSet<int>(reservedPayloadOffsets)
                : new HashSet<int>();
            int[][] candidateOffsetSets = new int[targetMobIds.Count][];
            for (int i = 0; i < targetMobIds.Count; i++)
            {
                int[] candidateOffsets = FindAllInt32Offsets(payload, targetMobIds[i])
                    .Where(candidateOffset => !reservedOffsets.Contains(candidateOffset))
                    .Distinct()
                    .OrderBy(candidateOffset => candidateOffset)
                    .ToArray();
                if (candidateOffsets.Length == 0)
                {
                    return false;
                }

                candidateOffsetSets[i] = candidateOffsets;
            }

            int[] currentOffsets = new int[targetMobIds.Count];
            int[] bestOffsets = null;
            int bestSpan = int.MaxValue;
            int bestGap = int.MaxValue;
            SearchOrderedTargetPayloadOffsets(
                candidateOffsetSets,
                index: 0,
                previousOffset: -1,
                currentOffsets,
                ref bestOffsets,
                ref bestSpan,
                ref bestGap);
            offsets = bestOffsets ?? Array.Empty<int>();
            return offsets.Length == targetMobIds.Count;
        }

        private static void SearchOrderedTargetPayloadOffsets(
            IReadOnlyList<int[]> candidateOffsetSets,
            int index,
            int previousOffset,
            int[] currentOffsets,
            ref int[] bestOffsets,
            ref int bestSpan,
            ref int bestGap)
        {
            if (candidateOffsetSets == null || index < 0)
            {
                return;
            }

            if (index >= candidateOffsetSets.Count)
            {
                int span = currentOffsets[^1] - currentOffsets[0];
                int gap = 0;
                for (int i = 1; i < currentOffsets.Length; i++)
                {
                    gap += currentOffsets[i] - currentOffsets[i - 1];
                }

                if (bestOffsets == null
                    || span < bestSpan
                    || (span == bestSpan && gap < bestGap))
                {
                    bestOffsets = (int[])currentOffsets.Clone();
                    bestSpan = span;
                    bestGap = gap;
                }

                return;
            }

            int[] candidateOffsets = candidateOffsetSets[index];
            for (int i = 0; i < candidateOffsets.Length; i++)
            {
                int candidateOffset = candidateOffsets[i];
                if (candidateOffset <= previousOffset)
                {
                    continue;
                }

                currentOffsets[index] = candidateOffset;
                SearchOrderedTargetPayloadOffsets(
                    candidateOffsetSets,
                    index + 1,
                    candidateOffset,
                    currentOffsets,
                    ref bestOffsets,
                    ref bestSpan,
                    ref bestGap);
            }
        }

        private static int[] ResolvePrimaryPayloadOffsets(
            byte[] payload,
            int primaryTargetMobId,
            IReadOnlyList<int> summonPayloadOffsets,
            IReadOnlyList<int> targetPayloadOffsets)
        {
            if (payload == null || payload.Length < sizeof(int) || primaryTargetMobId <= 0)
            {
                return Array.Empty<int>();
            }

            HashSet<int> summonOffsets = summonPayloadOffsets != null
                ? new HashSet<int>(summonPayloadOffsets)
                : new HashSet<int>();
            HashSet<int> targetOffsets = targetPayloadOffsets != null
                ? new HashSet<int>(targetPayloadOffsets)
                : new HashSet<int>();
            int[] candidateOffsets = FindAllInt32Offsets(payload, primaryTargetMobId)
                .Where(candidateOffset => !summonOffsets.Contains(candidateOffset))
                .Distinct()
                .OrderBy(candidateOffset => candidateOffset)
                .ToArray();
            if (candidateOffsets.Length == 0)
            {
                return Array.Empty<int>();
            }

            int firstTargetOffset = targetPayloadOffsets != null && targetPayloadOffsets.Count > 0
                ? targetPayloadOffsets[0]
                : int.MaxValue;
            int dedicatedPrimaryOffset = candidateOffsets
                .Where(candidateOffset => candidateOffset < firstTargetOffset)
                .DefaultIfEmpty(int.MinValue)
                .Max();
            List<int> resolvedOffsets = new();
            if (dedicatedPrimaryOffset != int.MinValue)
            {
                resolvedOffsets.Add(dedicatedPrimaryOffset);
            }

            foreach (int candidateOffset in candidateOffsets)
            {
                if (targetOffsets.Contains(candidateOffset))
                {
                    resolvedOffsets.Add(candidateOffset);
                }
            }

            if (resolvedOffsets.Count == 0)
            {
                resolvedOffsets.Add(candidateOffsets[0]);
            }

            return resolvedOffsets
                .Distinct()
                .OrderBy(candidateOffset => candidateOffset)
                .ToArray();
        }

        private static bool TryWriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            if (buffer == null || offset < 0 || offset > buffer.Length - sizeof(int))
            {
                return false;
            }

            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, sizeof(int)), value);
            return true;
        }

        private static bool PayloadContainsInt32(byte[] payload, int value)
        {
            if (payload == null || payload.Length < sizeof(int))
            {
                return false;
            }

            Span<byte> needle = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(needle, value);
            for (int offset = 0; offset <= payload.Length - needle.Length; offset++)
            {
                if (payload[offset] == needle[0]
                    && payload[offset + 1] == needle[1]
                    && payload[offset + 2] == needle[2]
                    && payload[offset + 3] == needle[3])
                {
                    return true;
                }
            }

            return false;
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        private static string NormalizeRemoteHost(string remoteHost)
        {
            return string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
        }

        private static bool MatchesTargetConfiguration(
            int listenPort,
            string remoteHost,
            int remotePort,
            int desiredListenPort,
            string desiredRemoteHost,
            int desiredRemotePort)
        {
            return listenPort == desiredListenPort
                   && remotePort == desiredRemotePort
                   && string.Equals(remoteHost, desiredRemoteHost, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int listenPort,
            string remoteHost,
            int remotePort,
            int desiredListenPort,
            IPEndPoint discoveredRemoteEndpoint)
        {
            return isRunning
                   && discoveredRemoteEndpoint != null
                   && listenPort == desiredListenPort
                   && remotePort == discoveredRemoteEndpoint.Port
                   && string.Equals(remoteHost, discoveredRemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveProcessSelector(string processSelector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = DefaultProcessName;
            error = null;

            if (string.IsNullOrWhiteSpace(processSelector))
            {
                return true;
            }

            string trimmedSelector = processSelector.Trim();
            if (int.TryParse(trimmedSelector, out int parsedProcessId) && parsedProcessId > 0)
            {
                owningProcessId = parsedProcessId;
                owningProcessName = null;
                return true;
            }

            owningProcessName = trimmedSelector.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmedSelector[..^4]
                : trimmedSelector;
            if (string.IsNullOrWhiteSpace(owningProcessName))
            {
                error = "Summoned official-session bridge process selector is invalid.";
                return false;
            }

            return true;
        }

        private static IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue)
            {
                return candidates ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return candidates?
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray()
                ?? Array.Empty<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string ownerText = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the MapleStory process"
                    : owningProcessName;
            string localPortText = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{ownerText} on remote port {remotePort}{localPortText}";
        }

        private static bool IsBridgeOpcode(int packetType)
        {
            return Enum.IsDefined(typeof(SummonedPacketType), packetType);
        }
    }
}
