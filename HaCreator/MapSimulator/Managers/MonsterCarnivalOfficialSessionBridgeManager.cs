using System;
using System.Buffers.Binary;
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
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Monster Carnival transport bridge that proxies a live Maple
    /// session and feeds inbound Monster Carnival packets into the existing
    /// packet-owned runtime seam.
    /// </summary>
    public sealed class MonsterCarnivalOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18488;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int FirstCarnivalOpcode = 346;
        private const int LastCarnivalOpcode = 353;
        internal const int OutboundRequestOpcode = 262;
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<MonsterCarnivalPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingRequest> _pendingOutboundRequests = new();
        private readonly ConcurrentQueue<ObservedOutboundRequest> _observedOutboundRequests = new();
        private readonly ConcurrentDictionary<int, int> _opcodeMappings = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

        private sealed record PendingRequest(MonsterCarnivalTab Tab, int EntryIndex, byte[] RawPacket);

        public readonly record struct ObservedOutboundRequest(
            MonsterCarnivalTab Tab,
            int EntryIndex,
            string Source);

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && !_roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int PendingPacketCount => _pendingOutboundRequests.Count;
        public int ObservedOutboundRequestCount => _observedOutboundRequests.Count;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public string LastStatus { get; private set; } = "Monster Carnival official-session bridge inactive.";

        public MonsterCarnivalOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
                : HasPassiveEstablishedSocketPair
                    ? DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)
                : "no active Maple session";
            return $"Monster Carnival official-session bridge {lifecycle}; {session}; attachMode=proxy+passive-observe; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; mappings={DescribePacketMappings()}; recent={DescribeRecentPackets()}. {LastStatus}";
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
                int requestedListenPort = autoSelectListenPort ? 0 : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                    {
                        status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Monster Carnival official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                SessionDiscoveryCandidate? passiveSessionToPreserve = ResolvePassiveSessionToPreserve(resolvedRemoteHost, remotePort);
                bool preservePassiveHandoff = passiveSessionToPreserve.HasValue;
                StopInternal(clearPending: !preservePassiveHandoff);

                try
                {
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    ListenPort = requestedListenPort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: !preservePassiveHandoff);
                        if (preservePassiveHandoff)
                        {
                            _passiveEstablishedSession = passiveSessionToPreserve;
                        }

                        LastStatus = proxyStatus;
                        status = LastStatus;
                        return false;
                    }

                    ListenPort = _roleSessionProxy.ListenPort;
                    _passiveEstablishedSession = null;
                    LastStatus = proxyStatus;
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: !preservePassiveHandoff);
                    if (preservePassiveHandoff)
                    {
                        _passiveEstablishedSession = passiveSessionToPreserve;
                    }

                    LastStatus = $"Monster Carnival official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
                    return false;
                }
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            TryStart(listenPort, remoteHost, remotePort, out _);
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
                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
            {
                status = $"Monster Carnival official-session bridge already listens on 127.0.0.1:{ListenPort} and remains armed for discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Monster Carnival official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Monster Carnival official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
            LastStatus = status;
            return true;
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
                status = "Monster Carnival official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: false);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus = $"Observed already-established Monster Carnival Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. This passive attach can keep the CField_MonsterCarnival session target visible, but it cannot decrypt inbound 346-353 traffic or inject outbound opcode {OutboundRequestOpcode} after the Maple handshake; reconnect through the localhost proxy for live Monster Carnival packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Monster Carnival official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Monster Carnival official-session proxy attach listen port must be 0 or a valid TCP port.";
                LastStatus = status;
                return false;
            }

            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? DefaultListenPort : listenPort;

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    if (MatchesDiscoveredTargetConfiguration(
                            ListenPort,
                            RemoteHost,
                            RemotePort,
                            requestedListenPort,
                            candidate.RemoteEndpoint,
                            autoSelectListenPort))
                    {
                        status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Monster Carnival official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesDiscoveredTargetConfiguration(
                        ListenPort,
                        RemoteHost,
                        RemotePort,
                        requestedListenPort,
                        candidate.RemoteEndpoint,
                        autoSelectListenPort))
                {
                    _passiveEstablishedSession = candidate;
                    status =
                        $"Monster Carnival official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}; keeping existing proxy listener on 127.0.0.1:{ListenPort}.";
                    LastStatus = status;
                    return true;
                }

                bool preservePendingForReconnectHandoff = !HasAttachedClient && _pendingOutboundRequests.Count > 0;
                StopInternal(clearPending: !preservePendingForReconnectHandoff);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
                {
                    _passiveEstablishedSession = candidate;
                    LastStatus = $"Observed already-established Monster Carnival Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                LastStatus =
                    $"Observed already-established Monster Carnival Maple socket pair {DescribeEstablishedSession(candidate)}. " +
                    $"Armed localhost proxy on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; reconnect Maple through this proxy to recover decrypt/inject ownership for inbound 346-353 and outbound opcode {OutboundRequestOpcode}. " +
                    $"Opcode {OutboundRequestOpcode} requests will queue until the proxied handshake initializes." +
                    (_pendingOutboundRequests.IsEmpty
                        ? string.Empty
                        : $" Preserved {_pendingOutboundRequests.Count} previously queued request(s) from passive attach.");
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

        public bool TryConfigurePacketMapping(int opcode, int packetType, out string status)
        {
            if (opcode <= 0)
            {
                status = "Monster Carnival opcode mappings require a positive opcode.";
                return false;
            }

            if (packetType < FirstCarnivalOpcode || packetType > LastCarnivalOpcode)
            {
                status = $"Monster Carnival packet mappings only accept raw packet types {FirstCarnivalOpcode}-{LastCarnivalOpcode}.";
                return false;
            }

            _opcodeMappings[opcode] = packetType;
            status = $"Mapped Monster Carnival opcode {opcode} to {DescribePacketType(packetType)}.";
            LastStatus = status;
            return true;
        }

        public bool RemovePacketMapping(int opcode, out string status)
        {
            if (_opcodeMappings.TryRemove(opcode, out int packetType))
            {
                status = $"Removed Monster Carnival opcode {opcode} mapping for {DescribePacketType(packetType)}.";
                LastStatus = status;
                return true;
            }

            status = $"Monster Carnival opcode {opcode} is not currently mapped.";
            return false;
        }

        public void ClearPacketMappings()
        {
            _opcodeMappings.Clear();
            LastStatus = "Cleared Monster Carnival official-session opcode mappings.";
        }

        public string DescribePacketMappings()
        {
            if (_opcodeMappings.IsEmpty)
            {
                return "none";
            }

            return string.Join(
                ", ",
                _opcodeMappings
                    .OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key}->{DescribePacketType(entry.Value)}"));
        }

        public string DescribeRecentPackets()
        {
            lock (_sync)
            {
                if (_recentPackets.Count == 0)
                {
                    return "none";
                }

                return string.Join(" | ", _recentPackets);
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Monster Carnival official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MonsterCarnivalPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TryDequeueObservedOutboundRequest(out ObservedOutboundRequest request)
        {
            return _observedOutboundRequests.TryDequeue(out request);
        }

        public bool TrySendRequest(MonsterCarnivalTab tab, int entryIndex, out string status)
        {
            if (entryIndex < 0)
            {
                status = $"Monster Carnival request index must be non-negative, got {entryIndex}.";
                LastStatus = status;
                return false;
            }

            if (HasPassiveEstablishedSocketPair)
            {
                status = $"Monster Carnival official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. It cannot inject opcode {OutboundRequestOpcode} into an already-established Maple socket pair after the handshake; reconnect through the localhost proxy first.";
                LastStatus = status;
                return false;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Monster Carnival official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] rawPacket = BuildRequestPacket(tab, entryIndex);
                if (!_roleSessionProxy.TrySendToServer(rawPacket, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                SentCount++;
                RecordRecentPacket(OutboundRequestOpcode, rawPacket, OutboundRequestOpcode, $"inject-request tab={(int)tab} index={entryIndex}");
                status = $"Injected Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Monster Carnival official-session injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public bool TrySendOrQueueRequest(MonsterCarnivalTab tab, int entryIndex, out bool queued, out string status)
        {
            queued = false;
            if (HasConnectedSession)
            {
                return TrySendRequest(tab, entryIndex, out status);
            }

            if (!TryQueueRequest(tab, entryIndex, out status))
            {
                return false;
            }

            queued = true;
            return true;
        }

        public bool TryQueueRequest(MonsterCarnivalTab tab, int entryIndex, out string status)
        {
            if (entryIndex < 0)
            {
                status = $"Monster Carnival request index must be non-negative, got {entryIndex}.";
                LastStatus = status;
                return false;
            }

            if (HasPassiveEstablishedSocketPair && !IsRunning)
            {
                status =
                    $"Monster Carnival official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. " +
                    $"It cannot inject opcode {OutboundRequestOpcode} into an already-established Maple socket pair after the handshake; " +
                    "run /mcarnival session attachproxy <listenPort|0> <remotePort> ... to arm reconnect before queueing requests.";
                LastStatus = status;
                return false;
            }

            if (!IsRunning && !HasAttachedClient)
            {
                status = "Monster Carnival official-session bridge is not armed for deferred live-session injection.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = BuildRequestPacket(tab, entryIndex);
            _pendingOutboundRequests.Enqueue(new PendingRequest(tab, entryIndex, rawPacket));
            QueuedCount++;
            RecordRecentPacket(OutboundRequestOpcode, rawPacket, OutboundRequestOpcode, $"queue-request tab={(int)tab} index={entryIndex}");
            status = HasPassiveEstablishedSocketPair
                ? $"Queued Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) for the proxied reconnect handshake."
                : $"Queued Monster Carnival opcode {OutboundRequestOpcode} (tab={(int)tab}, index={entryIndex}) for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType is >= FirstCarnivalOpcode and <= LastCarnivalOpcode
                ? $"Monster Carnival opcode {packetType}"
                : packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode
                    ? $"CField::OnPacket relay {packetType}"
                : $"Monster Carnival packet {packetType}";
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);
            _passiveEstablishedSession = null;
            if (!clearPending)
            {
                return;
            }

            while (_pendingMessages.TryDequeue(out _))
            {
            }

            while (_pendingOutboundRequests.TryDequeue(out _))
            {
            }

            while (_observedOutboundRequests.TryDequeue(out _))
            {
            }

            _recentPackets.Clear();
            ReceivedCount = 0;
            SentCount = 0;
            QueuedCount = 0;
        }

        private bool TryStartProxyListener(int listenPort, string remoteHost, int remotePort, out string status)
        {
            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? 0 : listenPort;

            try
            {
                RemoteHost = NormalizeRemoteHost(remoteHost);
                RemotePort = remotePort;
                ListenPort = requestedListenPort;
                if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                {
                    StopInternal(clearPending: false);
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                ListenPort = _roleSessionProxy.ListenPort;
                status = $"Monster Carnival official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                StopInternal(clearPending: false);
                status = $"Monster Carnival official-session bridge failed to start: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        private SessionDiscoveryCandidate? ResolvePassiveSessionToPreserve(string remoteHost, int remotePort)
        {
            if (!_passiveEstablishedSession.HasValue)
            {
                return null;
            }

            SessionDiscoveryCandidate candidate = _passiveEstablishedSession.Value;
            if (candidate.RemoteEndpoint == null
                || candidate.RemoteEndpoint.Port != remotePort
                || !string.Equals(
                    NormalizeRemoteHost(candidate.RemoteEndpoint.Address.ToString()),
                    NormalizeRemoteHost(remoteHost),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return candidate;
        }

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.IsInit)
            {
                int flushed = FlushQueuedRequestsViaProxy();
                LastStatus = flushed > 0
                    ? $"Monster Carnival official-session bridge initialized Maple crypto and flushed {flushed} queued request(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryMapInboundPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", out MonsterCarnivalPacketInboxMessage message)
                || message == null)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued Monster Carnival opcode {message.PacketType} ({DescribePacketType(message.PacketType)}) from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (TryDecodeOutboundRequestPacket(e.RawPacket, out int tab, out int entryIndex))
            {
                RecordRecentPacket(OutboundRequestOpcode, e.RawPacket, OutboundRequestOpcode, $"outbound-request tab={tab} index={entryIndex}");
                if (TryNormalizeObservedRequestTab(tab, out MonsterCarnivalTab requestTab) && entryIndex >= 0)
                {
                    _observedOutboundRequests.Enqueue(new ObservedOutboundRequest(requestTab, entryIndex, e.SourceEndpoint));
                    LastStatus = $"Forwarded live Monster Carnival request opcode {OutboundRequestOpcode} (tab={tab}, index={entryIndex}) from {e.SourceEndpoint} and queued it as a pending local ownership token.";
                    return;
                }

                LastStatus = $"Forwarded live Monster Carnival request opcode {OutboundRequestOpcode} (tab={tab}, index={entryIndex}) from {e.SourceEndpoint}; tab/index could not be used as a pending local ownership token.";
                return;
            }

            LastStatus = _roleSessionProxy.LastStatus;
        }

        public static bool TryDecodeInboundCarnivalPacket(byte[] rawPacket, string source, out MonsterCarnivalPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            int opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)));
            byte[] payload = rawPacket.Length > sizeof(ushort)
                ? rawPacket[sizeof(ushort)..]
                : Array.Empty<byte>();

            if (opcode == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode
                && TryDecodeCarnivalPacketFromRelayPrefixChain(
                    SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                    payload,
                    out int relayedPacketType,
                    out byte[] relayedPayload))
            {
                message = new MonsterCarnivalPacketInboxMessage(
                    opcode,
                    SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(relayedPacketType, relayedPayload),
                    source,
                    $"packetclientraw {Convert.ToHexString(rawPacket)}",
                    relayedPacketType);
                return true;
            }

            if (opcode < FirstCarnivalOpcode || opcode > LastCarnivalOpcode)
            {
                return false;
            }

            message = new MonsterCarnivalPacketInboxMessage(
                opcode,
                SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload),
                source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}",
                opcode);
            return true;
        }

        internal static bool TryDecodeCarnivalPacketFromRelayPrefixChain(
            int firstPacketType,
            byte[] firstPayload,
            out int carnivalPacketType,
            out byte[] carnivalPayload)
        {
            carnivalPacketType = 0;
            carnivalPayload = Array.Empty<byte>();
            firstPayload ??= Array.Empty<byte>();

            int relayPacketType = firstPacketType;
            byte[] relayPayload = firstPayload;
            for (int depth = 0; depth < SpecialFieldRuntimeCoordinator.RelayPrefixChainMaxDepth; depth++)
            {
                if (relayPacketType >= FirstCarnivalOpcode && relayPacketType <= LastCarnivalOpcode)
                {
                    carnivalPacketType = relayPacketType;
                    carnivalPayload = relayPayload;
                    return true;
                }

                if (relayPacketType != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
                {
                    return false;
                }

                if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                        relayPayload,
                        out relayPacketType,
                        out relayPayload,
                        out _))
                {
                    return false;
                }
            }

            return false;
        }

        private bool TryMapInboundPacket(byte[] rawPacket, string source, out MonsterCarnivalPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            int opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)));
            if (_opcodeMappings.TryGetValue(opcode, out int mappedPacketType))
            {
                byte[] payload = rawPacket.Length > sizeof(ushort)
                    ? rawPacket[sizeof(ushort)..]
                    : Array.Empty<byte>();
                message = new MonsterCarnivalPacketInboxMessage(
                    SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                    SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(mappedPacketType, payload),
                    source,
                    $"packetclientraw {Convert.ToHexString(rawPacket)}",
                    mappedPacketType);
                RecordRecentPacket(opcode, rawPacket, mappedPacketType, "mapped");
                return true;
            }

            bool decoded = TryDecodeInboundCarnivalPacket(rawPacket, source, out message);
            if (decoded)
            {
                RecordRecentPacket(opcode, rawPacket, message.OwnerPacketType, "direct");
            }

            return decoded;
        }

        private int FlushQueuedRequestsViaProxy()
        {
            int flushed = 0;
            while (_pendingOutboundRequests.TryDequeue(out PendingRequest request))
            {
                if (!_roleSessionProxy.TrySendToServer(request.RawPacket, out string status))
                {
                    _pendingOutboundRequests.Enqueue(request);
                    LastStatus = status;
                    break;
                }

                SentCount++;
                LastSentRecord(request.RawPacket, request.Tab, request.EntryIndex);
                flushed++;
            }

            return flushed;
        }

        private void LastSentRecord(byte[] rawPacket, MonsterCarnivalTab tab, int entryIndex)
        {
            RecordRecentPacket(OutboundRequestOpcode, rawPacket, OutboundRequestOpcode, $"flush-request tab={(int)tab} index={entryIndex}");
        }

        private static bool TryDecodeOutboundRequestPacket(byte[] rawPacket, out int tab, out int entryIndex)
        {
            tab = 0;
            entryIndex = 0;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte) + sizeof(int))
            {
                return false;
            }

            int opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)));
            if (opcode != OutboundRequestOpcode)
            {
                return false;
            }

            tab = rawPacket[sizeof(ushort)];
            entryIndex = BitConverter.ToInt32(rawPacket, sizeof(ushort) + sizeof(byte));
            return true;
        }

        private static bool TryNormalizeObservedRequestTab(int rawTab, out MonsterCarnivalTab tab)
        {
            tab = rawTab switch
            {
                0 => MonsterCarnivalTab.Mob,
                1 => MonsterCarnivalTab.Skill,
                2 => MonsterCarnivalTab.Guardian,
                _ => MonsterCarnivalTab.Mob
            };

            return rawTab is >= 0 and <= 2;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode => $"CField::OnPacket relay ({SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode})",
                346 => "enter (346)",
                347 => "personalcp (347)",
                348 => "teamcp (348)",
                349 => "requestresult (349)",
                350 => "requestfailure (350)",
                351 => "death (351)",
                352 => "memberout (352)",
                353 => "gameresult (353)",
                _ => packetType.ToString()
            };
        }

        private void RecordRecentPacket(int opcode, byte[] rawPacket, int packetType, string source)
        {
            lock (_sync)
            {
                _recentPackets.Enqueue($"opcode={opcode} type={DescribePacketType(packetType)} source={source} raw={Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}");
                while (_recentPackets.Count > RecentPacketCapacity)
                {
                    _recentPackets.Dequeue();
                }
            }
        }

        private static byte[] BuildRequestPacket(MonsterCarnivalTab tab, int entryIndex)
        {
            using PacketWriter writer = new();
            writer.Write((ushort)OutboundRequestOpcode);
            writer.WriteByte((byte)tab);
            writer.WriteInt(entryIndex);
            return writer.ToArray();
        }
        private static IEnumerable<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int bufferSize = 0;
            int result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != ErrorInsufficientBuffer || bufferSize <= 0)
            {
                yield break;
            }

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(buffer, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
                if (result != 0)
                {
                    yield break;
                }

                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int i = 0; i < rowCount; i++)
                {
                    yield return Marshal.PtrToStructure<TcpRowOwnerPid>(rowPtr);
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static IPAddress DecodeAddress(uint address)
        {
            return new IPAddress(BitConverter.GetBytes(address));
        }

        private static int DecodePort(byte[] portBytes)
        {
            return portBytes == null || portBytes.Length < 2
                ? 0
                : (portBytes[0] << 8) | portBytes[1];
        }

        private static bool TryResolveProcess(int pid, out string processName)
        {
            processName = null;
            try
            {
                processName = Process.GetProcessById(pid).ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
            }
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
                error = "Monster Carnival official-session discovery requires a process name or pid when a selector is provided.";
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

        private static string NormalizeRemoteHost(string remoteHost)
        {
            string trimmed = string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
            return IPAddress.TryParse(trimmed, out IPAddress parsedAddress)
                ? parsedAddress.ToString()
                : trimmed;
        }

        private static bool MatchesTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            string requestedRemoteHost,
            int requestedRemotePort)
        {
            return currentListenPort == requestedListenPort
                && currentRemotePort == requestedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(requestedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesRequestedTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            string requestedRemoteHost,
            int requestedRemotePort,
            bool ignoreListenPort)
        {
            return (ignoreListenPort || currentListenPort == requestedListenPort)
                && currentRemotePort == requestedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(requestedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint candidateRemoteEndpoint,
            bool ignoreListenPort = false)
        {
            if (candidateRemoteEndpoint == null)
            {
                return false;
            }

            return MatchesRequestedTargetConfiguration(
                currentListenPort,
                currentRemoteHost,
                currentRemotePort,
                requestedListenPort,
                candidateRemoteEndpoint.Address.ToString(),
                candidateRemoteEndpoint.Port,
                ignoreListenPort);
        }

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            return owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? "the selected process"
                    : $"process '{owningProcessName}'";
        }

        internal static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Monster Carnival official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Monster Carnival official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /mcarnival session discover to inspect them, or add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
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
                return $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            string matches = string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
            return
                $"{matches}{Environment.NewLine}" +
                "Use /mcarnival session attach ... for passive status-only observation of an already-established socket pair. " +
                "Use /mcarnival session attachproxy ... (or start/startauto) for live inbound 346-353 decrypt and outbound opcode 262 inject ownership after reconnect through 127.0.0.1.";
        }

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {DescribeEstablishedSession(candidate)}; proxy reconnect required for decrypt/inject";
        }

        private static IReadOnlyList<SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (!localPort.HasValue)
            {
                return candidates ?? Array.Empty<SessionDiscoveryCandidate>();
            }

            return (candidates ?? Array.Empty<SessionDiscoveryCandidate>())
                .Where(candidate => candidate.LocalEndpoint.Port == localPort.Value)
                .ToArray();
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
