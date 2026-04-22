using HaCreator.MapSimulator.Fields;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session for CRPSGameDlg packet ownership.
    /// </summary>
    public sealed class RockPaperScissorsOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18493;
        private const string DefaultProcessName = "MapleStory";
        private const int MaxRecentOutboundPackets = 20;
        private const int MaxRecentInboundPackets = 20;
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;

        private readonly ConcurrentQueue<RockPaperScissorsPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<RockPaperScissorsClientPacket> _pendingClientPackets = new();
        private readonly List<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly List<InboundPacketTrace> _recentInboundPackets = new();
        private readonly object _sync = new();
        private bool _hasObservedLiveOutboundOpcode160;
        private bool _hasObservedLiveInboundOpcode371;

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);

        internal readonly record struct OutboundPacketTrace(
            int Opcode,
            RockPaperScissorsClientRequestType RequestType,
            RockPaperScissorsChoice Choice,
            int PayloadLength,
            string Source,
            string Summary,
            string PayloadHex,
            string RawPacketHex);

        internal readonly record struct InboundPacketTrace(
            int Opcode,
            int PacketType,
            int PayloadLength,
            string Source,
            string Summary,
            string PayloadHex,
            string RawPacketHex);

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
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && _activePair == null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public bool HoldsLiveSessionOwnership => IsRunning || HasAttachedClient || HasPassiveEstablishedSocketPair;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int RecentOutboundPacketCount
        {
            get
            {
                lock (_sync)
                {
                    return _recentOutboundPackets.Count;
                }
            }
        }
        public int PendingPacketCount => _pendingClientPackets.Count;
        public int RecentInboundPacketCount
        {
            get
            {
                lock (_sync)
                {
                    return _recentInboundPackets.Count;
                }
            }
        }
        public string LastStatus { get; private set; } = "Rock-Paper-Scissors official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : HasPassiveEstablishedSocketPair
                    ? DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)
                : "no active Maple session";
            string outboundHistory = RecentOutboundPacketCount == 0
                ? "no captured client opcode 160 history"
                : $"{RecentOutboundPacketCount} captured client opcode 160 packet(s)";
            string inboundHistory = RecentInboundPacketCount == 0
                ? "no captured server opcode 371 history"
                : $"{RecentInboundPacketCount} captured server opcode 371 packet(s)";
            string guidance = DescribeSessionControlGuidance();
            string verification = DescribeLiveOwnershipVerificationStatus(
                HasConnectedSession,
                HasPassiveEstablishedSocketPair,
                IsRunning,
                _hasObservedLiveOutboundOpcode160,
                _hasObservedLiveInboundOpcode371);
            return $"Rock-Paper-Scissors official-session bridge {lifecycle}; {session}; received={ReceivedCount}; injected={SentCount}; forwarded={ForwardedOutboundCount}; pending={PendingPacketCount}; queued={QueuedCount}; {outboundHistory}; {inboundHistory}. {verification} {LastStatus} {guidance}";
        }

        public static IReadOnlyList<SessionDiscoveryCandidate> DiscoverEstablishedSessions(
            int remotePort,
            int? owningProcessId = null,
            string owningProcessName = null)
        {
            if (remotePort <= 0)
            {
                return Array.Empty<SessionDiscoveryCandidate>();
            }

            List<SessionDiscoveryCandidate> candidates = new();
            foreach (TcpRowOwnerPid row in EnumerateTcpRows())
            {
                if (row.state != (uint)TcpState.Established)
                {
                    continue;
                }

                int localPort = DecodePort(row.localPort);
                int resolvedRemotePort = DecodePort(row.remotePort);
                if (localPort <= 0 || resolvedRemotePort != remotePort)
                {
                    continue;
                }

                if (!TryResolveProcess(row.owningPid, out string processName))
                {
                    continue;
                }

                if (owningProcessId.HasValue && row.owningPid != owningProcessId.Value)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(owningProcessName)
                    && !string.Equals(processName, NormalizeProcessSelector(owningProcessName), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IPAddress localAddress = DecodeAddress(row.localAddr);
                IPAddress remoteAddress = DecodeAddress(row.remoteAddr);
                if (IPAddress.Any.Equals(remoteAddress) || IPAddress.None.Equals(remoteAddress))
                {
                    continue;
                }

                candidates.Add(new SessionDiscoveryCandidate(
                    row.owningPid,
                    processName,
                    new IPEndPoint(localAddress, localPort),
                    new IPEndPoint(remoteAddress, resolvedRemotePort)));
            }

            return candidates
                .OrderBy(candidate => candidate.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.ProcessId)
                .ThenBy(candidate => candidate.LocalEndpoint.Port)
                .ToArray();
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                bool autoSelectListenPort = listenPort <= 0;
                int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                    {
                        status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Rock-Paper-Scissors official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, autoSelectListenPort ? 0 : requestedListenPort);
                    _listener.Start();
                    ListenPort = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? requestedListenPort;
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Rock-Paper-Scissors official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Rock-Paper-Scissors official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
                    return false;
                }
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
                {
                    status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort) && IsRunning)
            {
                status = $"Rock-Paper-Scissors official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Rock-Paper-Scissors official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status =
                $"Rock-Paper-Scissors official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. " +
                $"{startStatus} {BuildDiscoveryAttachmentRequirementMessage(ListenPort)}";
            LastStatus = status;
            return true;
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            return TryStartFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
        }

        public bool TryAttachEstablishedSession(int remotePort, string processSelector, int? localPort, out string status)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            return TryAttachEstablishedSession(candidate, out status);
        }

        public bool TryAttachEstablishedSessionAndStartProxy(
            int listenPort,
            int remotePort,
            string processSelector,
            int? localPort,
            out string status)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            return TryAttachEstablishedSessionAndStartProxy(listenPort, candidate, out status);
        }

        public bool TryAttachEstablishedSession(SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Rock-Paper-Scissors official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus =
                    $"Observed already-established Rock-Paper-Scissors Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. " +
                    $"This passive attach keeps the live socket pair visible to the CRPSGameDlg ownership seam, but it still cannot decrypt inbound opcode {RockPaperScissorsField.OwnerOpcode} traffic or inject opcode {RockPaperScissorsField.ClientOpcode} after the Maple handshake; reconnect through the localhost proxy for live packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Rock-Paper-Scissors official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Rock-Paper-Scissors official-session proxy attach listen port must be 0 or a valid TCP port.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Rock-Paper-Scissors official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
                {
                    LastStatus = $"Observed already-established Rock-Paper-Scissors Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                LastStatus =
                    $"Observed already-established Rock-Paper-Scissors Maple socket pair {DescribeEstablishedSession(candidate)}. " +
                    $"Armed localhost proxy on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; reconnect Maple through this proxy to recover CRPSGameDlg decrypt/inject ownership. " +
                    $"Opcode {RockPaperScissorsField.ClientOpcode} requests will queue until the proxied handshake initializes.";
                status = LastStatus;
                return true;
            }
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Rock-Paper-Scissors official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse<OutboundPacketTrace>()
                    .Take(normalizedCount)
                    .ToArray();
                return "Rock-Paper-Scissors official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.RequestType} choice={entry.Choice} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Rock-Paper-Scissors official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public string DescribeRecentInboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentInboundPackets);
            lock (_sync)
            {
                if (_recentInboundPackets.Count == 0)
                {
                    return "Rock-Paper-Scissors official-session bridge inbound history is empty.";
                }

                InboundPacketTrace[] entries = _recentInboundPackets
                    .Reverse<InboundPacketTrace>()
                    .Take(normalizedCount)
                    .ToArray();
                return "Rock-Paper-Scissors official-session bridge inbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} subtype={entry.PacketType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentInboundPackets()
        {
            lock (_sync)
            {
                _recentInboundPackets.Clear();
            }

            LastStatus = "Rock-Paper-Scissors official-session bridge inbound history cleared.";
            return LastStatus;
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Rock-Paper-Scissors official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out RockPaperScissorsPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendOrQueueClientPacket(RockPaperScissorsClientPacket packet, out bool queued, out string status)
        {
            queued = false;
            if (packet == null)
            {
                status = "Rock-Paper-Scissors official-session bridge requires a client packet.";
                LastStatus = status;
                return false;
            }

            if (HasConnectedSession)
            {
                return TrySendClientPacket(packet, out status);
            }

            if (HasPassiveEstablishedSocketPair && !IsRunning)
            {
                status = $"Rock-Paper-Scissors official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. It cannot inject opcode {RockPaperScissorsField.ClientOpcode} into an already-established Maple socket pair after the handshake; reconnect through the localhost proxy first.";
                LastStatus = status;
                return false;
            }

            if (!IsRunning)
            {
                status = "Rock-Paper-Scissors official-session bridge is not running.";
                LastStatus = status;
                return false;
            }

            _pendingClientPackets.Enqueue(packet);
            QueuedCount++;
            queued = true;
            status = HasPassiveEstablishedSocketPair
                ? $"Queued Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)packet.RequestType} for deferred live-session injection after Maple reconnects through 127.0.0.1:{ListenPort}."
                : $"Queued Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)packet.RequestType} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public bool TrySendClientPacket(RockPaperScissorsClientPacket packet, out string status)
        {
            status = null;
            BridgePair pair = _activePair;
            if (pair == null || !pair.InitCompleted)
            {
                status = "Rock-Paper-Scissors official-session bridge has no initialized Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] rawPacket = RockPaperScissorsClientPacketTransportManager.BuildRawPacket(packet);
                pair.ServerSession.SendPacket(rawPacket);
                if (TryBuildOutboundTrace(rawPacket, "simulator-inject", out OutboundPacketTrace trace))
                {
                    RecordOutboundTrace(trace);
                }

                SentCount++;
                status = $"Injected Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)packet.RequestType} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session bridge client-packet injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
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
                LastStatus = $"Rock-Paper-Scissors official-session bridge error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;

            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        LastStatus = "Rejected Rock-Paper-Scissors official-session client because a live Maple session is already attached.";
                        client.Close();
                        return;
                    }
                }

                TcpClient server = new();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Rock-Paper-Scissors official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Rock-Paper-Scissors official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _passiveEstablishedSession = null;
                    _activePair = pair;
                }

                LastStatus = $"Rock-Paper-Scissors official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Rock-Paper-Scissors official-session bridge connect failed: {ex.Message}";
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
                    int flushed = FlushQueuedClientPackets(pair);
                    LastStatus = flushed > 0
                        ? $"Rock-Paper-Scissors official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued client packet(s)."
                        : $"Rock-Paper-Scissors official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryBuildInboundMessage(raw, $"official-session:{pair.RemoteEndpoint}", out RockPaperScissorsPacketInboxMessage message))
                {
                    return;
                }

                if (TryBuildInboundTrace(raw, $"official-session:{pair.RemoteEndpoint}", out InboundPacketTrace inboundTrace))
                {
                    RecordInboundTrace(inboundTrace);
                    _hasObservedLiveInboundOpcode371 = true;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued Rock-Paper-Scissors subtype {message.PacketType} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session server handling failed: {ex.Message}");
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
                pair.ServerSession.SendPacket(raw);
                if (TryBuildOutboundTrace(raw, $"official-session:{pair.ClientEndpoint}", out OutboundPacketTrace trace))
                {
                    RecordOutboundTrace(trace);
                    _hasObservedLiveOutboundOpcode160 = true;
                    ForwardedOutboundCount++;
                    LastStatus = $"Forwarded live Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)trace.RequestType} from {pair.ClientEndpoint}.";
                }
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session client handling failed: {ex.Message}");
            }
        }

        private int FlushQueuedClientPackets(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingClientPackets.TryDequeue(out RockPaperScissorsClientPacket packet))
            {
                byte[] rawPacket = RockPaperScissorsClientPacketTransportManager.BuildRawPacket(packet);
                pair.ServerSession.SendPacket(rawPacket);
                if (TryBuildOutboundTrace(rawPacket, "simulator-queued", out OutboundPacketTrace trace))
                {
                    RecordOutboundTrace(trace);
                }

                SentCount++;
                flushed++;
            }

            return flushed;
        }

        private bool TryStartProxyListener(int listenPort, string remoteHost, int remotePort, out string status)
        {
            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;

            try
            {
                RemoteHost = NormalizeRemoteHost(remoteHost);
                RemotePort = remotePort;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, autoSelectListenPort ? 0 : requestedListenPort);
                _listener.Start();
                ListenPort = (_listener.LocalEndpoint as IPEndPoint)?.Port ?? requestedListenPort;
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                status = $"Rock-Paper-Scissors official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                StopInternal(clearPending: false);
                status = $"Rock-Paper-Scissors official-session bridge failed to start: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        internal static bool TryBuildInboundMessage(byte[] rawPacket, string source, out RockPaperScissorsPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != RockPaperScissorsField.OwnerOpcode)
            {
                return false;
            }

            int packetType = rawPacket[sizeof(ushort)];
            if (!RockPaperScissorsField.TryParsePacketType(packetType.ToString(), out _))
            {
                return false;
            }

            int payloadLength = rawPacket.Length - sizeof(ushort) - sizeof(byte);
            if (!RockPaperScissorsField.HasValidOwnerPacketPayloadShape(packetType, payloadLength))
            {
                return false;
            }

            byte[] payload = new byte[payloadLength];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(rawPacket, sizeof(ushort) + sizeof(byte), payload, 0, payload.Length);
            }

            message = new RockPaperScissorsPacketInboxMessage(
                packetType,
                payload,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetraw {Convert.ToHexString(rawPacket)}");
            return true;
        }

        internal static bool MatchesTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            string expectedRemoteHost,
            int expectedRemotePort)
        {
            return currentListenPort == expectedListenPort
                && string.Equals(NormalizeRemoteHost(currentRemoteHost), NormalizeRemoteHost(expectedRemoteHost), StringComparison.OrdinalIgnoreCase)
                && currentRemotePort == expectedRemotePort;
        }

        internal static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint remoteEndpoint,
            bool autoSelectListenPort)
        {
            if (remoteEndpoint == null)
            {
                return false;
            }

            if (currentRemotePort != remoteEndpoint.Port
                || !string.Equals(NormalizeRemoteHost(currentRemoteHost), NormalizeRemoteHost(remoteEndpoint.Address.ToString()), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return autoSelectListenPort || currentListenPort == requestedListenPort;
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
                _passiveEstablishedSession = null;
            }

            pair?.Close();
            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                while (_pendingClientPackets.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                SentCount = 0;
                ForwardedOutboundCount = 0;
                QueuedCount = 0;
                _recentOutboundPackets.Clear();
                _recentInboundPackets.Clear();
                _hasObservedLiveOutboundOpcode160 = false;
                _hasObservedLiveInboundOpcode371 = false;
            }
        }

        internal static string DescribeLiveOwnershipVerificationStatus(
            bool hasConnectedSession,
            bool hasPassiveEstablishedSocketPair,
            bool isRunning,
            bool hasObservedLiveOutboundOpcode160,
            bool hasObservedLiveInboundOpcode371)
        {
            if (hasObservedLiveOutboundOpcode160 && hasObservedLiveInboundOpcode371)
            {
                return $"Live ownership verification complete: captured proxied Maple opcode {RockPaperScissorsField.ClientOpcode} outbound plus opcode {RockPaperScissorsField.OwnerOpcode} inbound.";
            }

            if (hasPassiveEstablishedSocketPair && !isRunning)
            {
                return $"Live ownership verification pending reconnect: passive attach cannot capture opcode {RockPaperScissorsField.ClientOpcode}/{RockPaperScissorsField.OwnerOpcode} after handshake.";
            }

            if (isRunning && !hasConnectedSession)
            {
                return $"Live ownership verification pending reconnect: waiting for Maple to reconnect through localhost before opcode {RockPaperScissorsField.ClientOpcode}/{RockPaperScissorsField.OwnerOpcode} capture can start.";
            }

            if (hasConnectedSession)
            {
                if (!hasObservedLiveOutboundOpcode160 && !hasObservedLiveInboundOpcode371)
                {
                    return $"Live ownership verification in progress: waiting for both proxied opcode {RockPaperScissorsField.ClientOpcode} outbound and opcode {RockPaperScissorsField.OwnerOpcode} inbound.";
                }

                if (!hasObservedLiveOutboundOpcode160)
                {
                    return $"Live ownership verification in progress: opcode {RockPaperScissorsField.OwnerOpcode} inbound captured, waiting for proxied opcode {RockPaperScissorsField.ClientOpcode} outbound.";
                }

                return $"Live ownership verification in progress: opcode {RockPaperScissorsField.ClientOpcode} outbound captured, waiting for proxied opcode {RockPaperScissorsField.OwnerOpcode} inbound.";
            }

            return $"Live ownership verification idle: start an RPS session bridge and capture opcode {RockPaperScissorsField.ClientOpcode}/{RockPaperScissorsField.OwnerOpcode} traffic.";
        }

        internal static bool TryBuildInboundTrace(byte[] rawPacket, string source, out InboundPacketTrace trace)
        {
            trace = default;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != RockPaperScissorsField.OwnerOpcode)
            {
                return false;
            }

            int packetType = rawPacket[sizeof(ushort)];
            if (!RockPaperScissorsField.TryParsePacketType(packetType.ToString(), out _))
            {
                return false;
            }

            int payloadOffset = sizeof(ushort) + sizeof(byte);
            int payloadLength = rawPacket.Length - payloadOffset;
            if (!RockPaperScissorsField.HasValidOwnerPacketPayloadShape(packetType, payloadLength))
            {
                return false;
            }

            byte[] payload = payloadLength == 0 ? Array.Empty<byte>() : rawPacket[payloadOffset..];
            string summary = $"opcode={RockPaperScissorsField.OwnerOpcode} subtype={packetType}";
            trace = new InboundPacketTrace(
                opcode,
                packetType,
                payloadLength,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                summary,
                Convert.ToHexString(payload),
                Convert.ToHexString(rawPacket));
            return true;
        }

        internal static bool TryBuildOutboundTrace(byte[] rawPacket, string source, out OutboundPacketTrace trace)
        {
            trace = default;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != RockPaperScissorsField.ClientOpcode)
            {
                return false;
            }

            byte requestTypeValue = rawPacket[sizeof(ushort)];
            if (!Enum.IsDefined(typeof(RockPaperScissorsClientRequestType), (int)requestTypeValue))
            {
                return false;
            }

            RockPaperScissorsClientRequestType requestType = (RockPaperScissorsClientRequestType)requestTypeValue;
            int payloadOffset = sizeof(ushort) + sizeof(byte);
            int payloadLength = rawPacket.Length - payloadOffset;
            RockPaperScissorsChoice choice = RockPaperScissorsChoice.None;
            if (requestType == RockPaperScissorsClientRequestType.Select)
            {
                if (payloadLength != sizeof(byte))
                {
                    return false;
                }

                choice = (RockPaperScissorsChoice)rawPacket[payloadOffset];
                if (choice is < RockPaperScissorsChoice.Rock or > RockPaperScissorsChoice.Scissor)
                {
                    return false;
                }
            }
            else if (payloadLength != 0)
            {
                return false;
            }

            byte[] payload = payloadLength == 0 ? Array.Empty<byte>() : rawPacket[payloadOffset..];
            string summary = requestType == RockPaperScissorsClientRequestType.Select
                ? $"opcode={RockPaperScissorsField.ClientOpcode} subtype={(int)requestType} choice={choice}"
                : $"opcode={RockPaperScissorsField.ClientOpcode} subtype={(int)requestType}";
            trace = new OutboundPacketTrace(
                opcode,
                requestType,
                choice,
                payloadLength,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-client" : source,
                summary,
                Convert.ToHexString(payload),
                Convert.ToHexString(rawPacket));
            return true;
        }

        private void RecordOutboundTrace(OutboundPacketTrace trace)
        {
            lock (_sync)
            {
                _recentOutboundPackets.Add(trace);
                while (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.RemoveAt(0);
                }
            }
        }

        private void RecordInboundTrace(InboundPacketTrace trace)
        {
            lock (_sync)
            {
                _recentInboundPackets.Add(trace);
                while (_recentInboundPackets.Count > MaxRecentInboundPackets)
                {
                    _recentInboundPackets.RemoveAt(0);
                }
            }
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }

        private static string NormalizeRemoteHost(string remoteHost)
        {
            return string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established session {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static bool MatchesRequestedTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            string expectedRemoteHost,
            int expectedRemotePort,
            bool autoSelectListenPort)
        {
            if (currentRemotePort != expectedRemotePort
                || !string.Equals(NormalizeRemoteHost(currentRemoteHost), NormalizeRemoteHost(expectedRemoteHost), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return autoSelectListenPort || currentListenPort == expectedListenPort;
        }

        private string DescribeSessionControlGuidance()
        {
            if (HasConnectedSession)
            {
                return $"Live opcode {RockPaperScissorsField.OwnerOpcode}/{RockPaperScissorsField.ClientOpcode} ownership is active through the proxied Maple session.";
            }

            if (HasPassiveEstablishedSocketPair && !IsRunning)
            {
                return "Use `/rps session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]` or `/rps session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]` and reconnect Maple through localhost to recover live decrypt/inject ownership.";
            }

            if (IsRunning && !HasConnectedSession)
            {
                return $"Reconnect Maple through 127.0.0.1:{ListenPort} to recover opcode {RockPaperScissorsField.OwnerOpcode}/{RockPaperScissorsField.ClientOpcode} decrypt/inject ownership.";
            }

            return "Use `/rps session attach <remotePort> [processName|pid] [localPort]` for passive status-only binding to an already-established Maple socket pair, or use `/rps session start ...`, `/rps session startauto ...`, or `/rps session attachproxy ...` for live decrypt/inject ownership.";
        }

        private static IReadOnlyList<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int size = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref size, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != 0 && result != ErrorInsufficientBuffer)
            {
                return Array.Empty<TcpRowOwnerPid>();
            }

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                result = GetExtendedTcpTable(buffer, ref size, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
                if (result != 0)
                {
                    return Array.Empty<TcpRowOwnerPid>();
                }

                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                List<TcpRowOwnerPid> rows = new(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    rows.Add(Marshal.PtrToStructure<TcpRowOwnerPid>(rowPointer));
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }

                return rows;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int DecodePort(byte[] portBytes)
        {
            if (portBytes == null || portBytes.Length < 2)
            {
                return 0;
            }

            return (portBytes[0] << 8) | portBytes[1];
        }

        private static IPAddress DecodeAddress(uint address)
        {
            byte[] bytes = BitConverter.GetBytes(address);
            return new IPAddress(bytes);
        }

        private static bool TryResolveProcess(int processId, out string processName)
        {
            processName = null;
            try
            {
                Process process = Process.GetProcessById(processId);
                processName = process.ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveProcessSelector(string processSelector, out int? processId, out string processName, out string error)
        {
            processId = null;
            processName = null;
            error = null;

            if (string.IsNullOrWhiteSpace(processSelector))
            {
                processName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(processSelector, out int parsedProcessId) && parsedProcessId > 0)
            {
                processId = parsedProcessId;
                return true;
            }

            string normalized = NormalizeProcessSelector(processSelector);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                error = "Process selector must be a positive pid or process name.";
                return false;
            }

            processName = normalized;
            return true;
        }

        private static string NormalizeProcessSelector(string processSelector)
        {
            if (string.IsNullOrWhiteSpace(processSelector))
            {
                return null;
            }

            string trimmed = processSelector.Trim();
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmed[..^4]
                : trimmed;
        }

        private static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 1)
            {
                candidate = filteredCandidates[0];
                status = $"Resolved 1 established Maple session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return true;
            }

            if (filteredCandidates.Count == 0)
            {
                candidate = default;
                status = $"No established Maple sessions found for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            candidate = default;
            status = $"Found {filteredCandidates.Count} established Maple sessions for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}. Refine the process selector or local port.";
            return false;
        }

        private static IReadOnlyList<SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue || localPort.Value <= 0)
            {
                return candidates ?? Array.Empty<SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
        }

        internal static string DescribeDiscoveryCandidates(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established Maple sessions found for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return "Rock-Paper-Scissors official-session bridge discovery candidates:"
                + Environment.NewLine
                + string.Join(
                    Environment.NewLine,
                    filteredCandidates.Select(candidate =>
                        $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"))
                + Environment.NewLine
                + BuildDiscoveryAttachmentRequirementMessage();
        }

        private static string BuildDiscoveryAttachmentRequirementMessage(int? listenPort = null)
        {
            string reconnectTarget = listenPort.HasValue && listenPort.Value > 0
                ? $"127.0.0.1:{listenPort.Value}"
                : "the configured localhost listen port";
            return $"Discovery identifies established Maple sockets. Use `/rps session attach ...` to bind the simulator to the current socket pair for passive status-only observation, or reconnect Maple through {reconnectTarget} so the bridge can recover the init packet and Maple crypto for live opcode {RockPaperScissorsField.OwnerOpcode}/{RockPaperScissorsField.ClientOpcode} ownership.";
        }

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            if (owningProcessId.HasValue)
            {
                return $"pid {owningProcessId.Value}";
            }

            return string.IsNullOrWhiteSpace(owningProcessName) ? DefaultProcessName : owningProcessName;
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = DescribeSelector(owningProcessId, owningProcessName);
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRowOwnerPid
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] remotePort;
            public int owningPid;
        }

        private enum TcpTableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);
    }
}
