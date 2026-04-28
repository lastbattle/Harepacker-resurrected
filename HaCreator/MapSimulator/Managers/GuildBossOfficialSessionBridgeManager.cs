using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Guild Boss transport bridge that proxies a live Maple session,
    /// decrypts guild-boss packets in-process, and can inject opcode 259 without
    /// an external line-based bridge.
    /// </summary>
    public sealed class GuildBossOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18488;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int PacketTypeHealerMove = 344;
        private const int PacketTypePulleyStateChange = 345;
        private const int OutboundPulleyRequestOpcode = 259;

        private readonly ConcurrentQueue<GuildBossPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingPulleyRequest> _pendingOutboundRequests = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);
        private sealed record PendingPulleyRequest(GuildBossField.PulleyPacketRequest Request, byte[] RawPacket);
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && !_roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public bool HoldsLiveSessionOwnership => IsRunning || HasAttachedClient || HasPassiveEstablishedSocketPair;
        public int PendingPacketCount => _pendingOutboundRequests.Count;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public string LastStatus { get; private set; } = "Guild boss official-session bridge inactive.";

        public GuildBossOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            return $"Guild boss official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}. {LastStatus}";
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
                        status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Guild boss official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
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

                    LastStatus = $"Guild boss official-session bridge failed to start: {ex.Message}";
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
                    status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort) && IsRunning)
            {
                status = $"Guild boss official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Guild boss official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status =
                $"Guild boss official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. " +
                $"{startStatus} {BuildDiscoveryAttachmentRequirementMessage(ListenPort)}";
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
                status = "Guild boss official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                if (_passiveEstablishedSession.HasValue
                    && IsSameEstablishedSession(_passiveEstablishedSession.Value, candidate))
                {
                    LastStatus =
                        $"Guild boss official-session bridge is already observing established Guild Boss Maple socket pair {DescribeEstablishedSession(candidate)}; keeping passive ownership and {PendingPacketCount} queued opcode {OutboundPulleyRequestOpcode} request(s). " +
                        $"Reconnect through the localhost proxy for live packet ownership.";
                    status = LastStatus;
                    return true;
                }

                bool preservePendingForPassiveHandoff = !HasAttachedClient && _pendingOutboundRequests.Count > 0;
                StopInternal(clearPending: !preservePendingForPassiveHandoff);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus =
                    $"Observed already-established Guild Boss Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. " +
                    $"This passive attach keeps the live socket pair visible to the guild-boss ownership seam, but it still cannot decrypt inbound {PacketTypeHealerMove}/{PacketTypePulleyStateChange} traffic or inject opcode {OutboundPulleyRequestOpcode} after the Maple handshake; reconnect through the localhost proxy for live packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Guild boss official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Guild boss official-session proxy attach listen port must be 0 or a valid TCP port.";
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
                        status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Guild boss official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
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
                        $"Guild boss official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}; keeping existing proxy listener on 127.0.0.1:{ListenPort}.";
                    LastStatus = status;
                    return true;
                }

                bool preservePendingForReconnectHandoff = !HasAttachedClient && _pendingOutboundRequests.Count > 0;
                StopInternal(clearPending: !preservePendingForReconnectHandoff);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
                {
                    // Keep passive ownership visible when reconnect proxy startup fails so retrying
                    // attachproxy/startauto does not require re-attaching discovery first.
                    _passiveEstablishedSession = candidate;
                    LastStatus = $"Observed already-established Guild Boss Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                LastStatus =
                    $"Observed already-established Guild Boss Maple socket pair {DescribeEstablishedSession(candidate)}. " +
                    $"Armed localhost proxy on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; reconnect Maple through this proxy to recover decrypt/inject ownership. " +
                    $"Opcode {OutboundPulleyRequestOpcode} requests will queue until the proxied handshake initializes.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            return TryStartFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
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

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Guild boss official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out GuildBossPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendPulleyRequest(GuildBossField.PulleyPacketRequest request, out string status)
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                if (HasPassiveEstablishedSocketPair)
                {
                    status = $"Guild boss official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. It cannot inject opcode {OutboundPulleyRequestOpcode} into an already-established Maple socket pair after the handshake; reconnect through the localhost proxy first.";
                    LastStatus = status;
                    return false;
                }

                status = "Guild boss official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                byte[] packet = BuildPulleyRequestPacket();
                if (!_roleSessionProxy.TrySendToServer(packet, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                SentCount++;
                status = $"Injected Guild Boss opcode {OutboundPulleyRequestOpcode} into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"Guild boss official-session injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public bool TrySendOrQueuePulleyRequest(GuildBossField.PulleyPacketRequest request, out bool queued, out string status)
        {
            queued = false;
            if (HasConnectedSession)
            {
                return TrySendPulleyRequest(request, out status);
            }

            if (!TryQueuePulleyRequest(request, out status))
            {
                return false;
            }

            queued = true;
            return true;
        }

        public bool TryQueuePulleyRequest(GuildBossField.PulleyPacketRequest request, out string status)
        {
            if (!IsRunning && !HasAttachedClient)
            {
                if (!HasPassiveEstablishedSocketPair)
                {
                    status = "Guild boss official-session bridge is not armed for deferred live-session injection.";
                    LastStatus = status;
                    return false;
                }
            }

            _pendingOutboundRequests.Enqueue(new PendingPulleyRequest(request, BuildPulleyRequestPacket()));
            QueuedCount++;
            status = HasPassiveEstablishedSocketPair
                ? $"Queued Guild Boss opcode {OutboundPulleyRequestOpcode} request #{request.Sequence} while observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)}. Arm `/guildboss session attachproxy ...` or `/guildboss session startauto ...` and reconnect through localhost to flush it after the proxied handshake initializes."
                : $"Queued Guild Boss opcode {OutboundPulleyRequestOpcode} request #{request.Sequence} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType switch
            {
                PacketTypeHealerMove => "Guild boss healer",
                PacketTypePulleyStateChange => "Guild boss pulley",
                _ => $"Guild boss packet {packetType}"
            };
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


        private bool TryStartProxyListener(int listenPort, string remoteHost, int remotePort, out string status)
        {
            return TryStart(listenPort, remoteHost, remotePort, out status);
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
                int flushed = FlushQueuedPulleyRequestsViaProxy();
                LastStatus = flushed > 0
                    ? $"Guild boss official-session bridge initialized Maple crypto and flushed {flushed} queued pulley request(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeOpcode(e.RawPacket, out int opcode, out byte[] payload)
                || (opcode != PacketTypeHealerMove && opcode != PacketTypePulleyStateChange))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            byte[] relayPayload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
            _pendingMessages.Enqueue(new GuildBossPacketInboxMessage(
                SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode,
                relayPayload,
                $"official-session:{e.SourceEndpoint}",
                $"packetraw {Convert.ToHexString(e.RawPacket)}"));
            ReceivedCount++;
            LastStatus =
                $"Queued CField::OnPacket opcode {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} relay for Guild Boss opcode {opcode} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                return;
            }

            if (TryDecodeOpcode(e.RawPacket, out int opcode, out _)
                && opcode == OutboundPulleyRequestOpcode)
            {
                LastStatus = $"Forwarded live Guild Boss opcode {OutboundPulleyRequestOpcode} from {e.SourceEndpoint}.";
            }
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);
            _passiveEstablishedSession = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                while (_pendingOutboundRequests.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                SentCount = 0;
                QueuedCount = 0;
            }
        }
        private int FlushQueuedPulleyRequestsViaProxy()
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingOutboundRequests.TryPeek(out PendingPulleyRequest pending))
            {
                if (!_roleSessionProxy.TrySendToServer(pending.RawPacket, out _))
                {
                    break;
                }

                if (!_pendingOutboundRequests.TryDequeue(out _))
                {
                    break;
                }

                SentCount++;
                flushed++;
            }

            return flushed;
        }

        private static byte[] BuildPulleyRequestPacket()
        {
            PacketWriter writer = new PacketWriter();
            writer.WriteShort((short)OutboundPulleyRequestOpcode);
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
                error = "Guild boss official-session discovery requires a process name or pid when a selector is provided.";
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
                status = $"Guild boss official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.RemoteEndpoint.Address}:{match.RemoteEndpoint.Port} via {match.LocalEndpoint.Address}:{match.LocalEndpoint.Port}"));
                status = $"Guild boss official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /guildboss session discover to inspect them, or add a localPort filter.";
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

            return "Guild boss official-session bridge discovery candidates:"
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
            return $"Discovery identifies established Maple sockets. Use `/guildboss session attach ...` to bind the simulator to the current socket pair for passive status-only observation, `/guildboss session attachproxy ...` to arm a reconnect proxy for a selected established socket pair, or `/guildboss session startauto ...` to arm reconnect proxy ownership from discovery and queue opcode {OutboundPulleyRequestOpcode} until Maple reconnects through {reconnectTarget}.";
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

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}; proxy reconnect required for decrypt/inject";
        }

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static bool IsSameEstablishedSession(SessionDiscoveryCandidate left, SessionDiscoveryCandidate right)
        {
            return left.ProcessId == right.ProcessId
                && string.Equals(left.ProcessName, right.ProcessName, StringComparison.OrdinalIgnoreCase)
                && Equals(left.LocalEndpoint, right.LocalEndpoint)
                && Equals(left.RemoteEndpoint, right.RemoteEndpoint);
        }

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Skip(sizeof(short)).ToArray();
            return true;
        }

        internal static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            IPEndPoint discoveredRemoteEndpoint,
            bool ignoreListenPort = false)
        {
            return discoveredRemoteEndpoint != null
                && MatchesRequestedTargetConfiguration(
                    currentListenPort,
                    currentRemoteHost,
                    currentRemotePort,
                    expectedListenPort,
                    discoveredRemoteEndpoint.Address.ToString(),
                    discoveredRemoteEndpoint.Port,
                    ignoreListenPort);
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
                && currentRemotePort == expectedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(expectedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool MatchesRequestedTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            string expectedRemoteHost,
            int expectedRemotePort,
            bool ignoreListenPort)
        {
            return (ignoreListenPort || currentListenPort == expectedListenPort)
                && currentRemotePort == expectedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(expectedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
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
            TcpTableOwnerPidAll = 5
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            TcpTableClass tblClass,
            int reserved);
    }
}
