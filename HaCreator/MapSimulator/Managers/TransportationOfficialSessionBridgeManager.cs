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
    /// Built-in transport bridge that proxies a live Maple session and feeds
    /// decrypted CField_ContiMove packets into the existing transport dispatcher.
    /// </summary>
    public sealed class TransportationOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18491;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int MaxRecentOutboundPackets = 32;

        private readonly ConcurrentQueue<TransportationPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

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
            string Source);
        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket);
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && !_roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int ForwardedOutboundCount { get; private set; }
        public int ForwardedOutboundTransportCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus { get; private set; } = "Transport official-session bridge inactive.";

        public TransportationOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={DescribeOutboundPacket(LastSentOpcode, LastSentRawPacket)}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={DescribeOutboundPacket(LastQueuedOpcode, LastQueuedRawPacket)}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return $"Transport official-session bridge {lifecycle}; {session}; attachMode=proxy+passive-observe; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; forwardedOutbound={ForwardedOutboundCount}; forwardedOutboundTransport={ForwardedOutboundTransportCount}; inbound opcodes=164,165; outbound opcode={TransportationFieldInitRequestCodec.OutboundFieldInitOpcode}.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return "Transport official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Transport official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"{DescribeOutboundPacket(entry.Opcode, Convert.FromHexString(entry.RawPacketHex))} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = "Transport official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (!TryResolveRecentOutboundPacket(historyIndexFromNewest, "replay", out byte[] rawPacket, out string resolveStatus))
            {
                status = resolveStatus;
                LastStatus = status;
                return false;
            }

            return TrySendRawPacket(rawPacket, out status);
        }

        public bool TryQueueRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (!TryResolveRecentOutboundPacket(historyIndexFromNewest, "queue", out byte[] rawPacket, out string resolveStatus))
            {
                status = resolveStatus;
                LastStatus = status;
                return false;
            }

            return TryQueueRawPacket(rawPacket, out status);
        }

        public bool TryObserveOutboundRawPacket(byte[] rawPacket, string source, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                status = "Transport outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            byte[] clonedPacket = (byte[])rawPacket.Clone();
            RecordOutboundPacket(new OutboundPacketTrace(
                opcode,
                payload?.Length ?? 0,
                BuildPayloadPreview(payload),
                Convert.ToHexString(clonedPacket),
                string.IsNullOrWhiteSpace(source) ? "transport-observed" : source.Trim()));

            status = $"Observed outbound {DescribeOutboundPacket(opcode, clonedPacket)} into transport official-session bridge history.";
            LastStatus = status;
            return true;
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
                int resolvedListenPort = autoSelectListenPort ? 0 : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                    {
                        status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Transport official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                if (!TryStartProxyListener(autoSelectListenPort ? 0 : resolvedListenPort, resolvedRemoteHost, remotePort, out string startStatus))
                {
                    status = startStatus;
                    return false;
                }

                status = startStatus;
                return true;
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
            int resolvedListenPort = autoSelectListenPort ? 0 : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
                {
                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
            {
                status = $"Transport official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Transport official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Transport official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus} {BuildDiscoveryAttachmentRequirementMessage(ListenPort)}";
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

        public bool TryAttachEstablishedSession(SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Transport official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus = $"Observed already-established transport Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. This passive attach can keep the client-owned wrapper target visible, but it cannot decrypt inbound 164/165 traffic or inject outbound opcode {TransportationFieldInitRequestCodec.OutboundFieldInitOpcode} after the Maple handshake; reconnect through the localhost proxy for live transport packet ownership.";
                status = LastStatus;
                return true;
            }
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

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, SessionDiscoveryCandidate candidate, out string status)
        {
            if (candidate.LocalEndpoint == null || candidate.RemoteEndpoint == null || candidate.RemoteEndpoint.Port <= 0)
            {
                status = "Transport official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Transport official-session proxy attach listen port must be 0 or a valid TCP port.";
                LastStatus = status;
                return false;
            }

            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? 0 : listenPort;
            string resolvedRemoteHost = candidate.RemoteEndpoint.Address.ToString();
            int resolvedRemotePort = candidate.RemoteEndpoint.Port;

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, resolvedRemotePort, autoSelectListenPort))
                {
                    _passiveEstablishedSession = candidate;
                    status = $"Observed already-established transport Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. Transport official-session bridge remains armed on 127.0.0.1:{ListenPort}; reconnect Maple through that localhost proxy to recover transport packet ownership.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(autoSelectListenPort ? 0 : requestedListenPort, resolvedRemoteHost, resolvedRemotePort, out string startStatus))
                {
                    LastStatus = $"Observed already-established transport Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                status = $"Observed already-established transport Maple socket pair {DescribeEstablishedSession(candidate)}. {startStatus} Reconnect Maple through 127.0.0.1:{ListenPort} so the bridge can recover the init packet and Maple crypto; deferred outbound transport queueing is now armed for that reconnect path.";
                LastStatus = status;
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

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Transport official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out TransportationPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendFieldInitRequest(int fieldId, int shipKind, out string status)
        {
            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKind}.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = TransportationFieldInitRequestCodec.BuildRawFieldInitPacket(fieldId, shipKind);
            if (!TrySendRawPacket(
                rawPacket,
                out status,
                countAsTypedSend: true,
                allowDeferredQueueWhenPassive: true,
                out bool queuedForDeferredInjection))
            {
                return false;
            }

            if (queuedForDeferredInjection)
            {
                return true;
            }

            status = $"Injected {TransportationFieldInitRequestCodec.DescribeFieldInitRequest(fieldId, shipKind)} into live session {RemoteHost}:{RemotePort}.";
            LastStatus = status;
            return true;
        }

        public bool TryQueueFieldInitRequest(int fieldId, int shipKind, out string status)
        {
            if (!TransportationFieldInitRequestCodec.IsSupportedShipKind(shipKind))
            {
                status = $"Transport field-init request only supports ship kinds 0 and 1, but received {shipKind}.";
                LastStatus = status;
                return false;
            }

            byte[] rawPacket = TransportationFieldInitRequestCodec.BuildRawFieldInitPacket(fieldId, shipKind);
            return TryQueueRawPacket(rawPacket, out status);
        }

        public bool TrySendRawPacket(byte[] rawPacket, out string status)
        {
            return TrySendRawPacket(
                rawPacket,
                out status,
                countAsTypedSend: true,
                allowDeferredQueueWhenPassive: true,
                out _);
        }

        public bool TryQueueRawPacket(byte[] rawPacket, out string status)
        {
            string armStatus = null;
            if (HasPassiveEstablishedSocketPair
                && !IsRunning
                && !TryArmReconnectProxyForPassiveAttach(out armStatus))
            {
                status = armStatus;
                LastStatus = status;
                return false;
            }

            if (!TryDecodeOpcode(rawPacket, out int opcode, out _))
            {
                status = "Transport outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            byte[] clonedPacket = (byte[])rawPacket.Clone();
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, clonedPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = clonedPacket;
            string queueStatus = HasPassiveEstablishedSocketPair
                ? $"Queued outbound {DescribeOutboundPacket(opcode, clonedPacket)} for deferred live-session injection after Maple reconnects through 127.0.0.1:{ListenPort}."
                : $"Queued outbound {DescribeOutboundPacket(opcode, clonedPacket)} for deferred live-session injection.";
            status = string.IsNullOrWhiteSpace(armStatus)
                ? queueStatus
                : $"{armStatus} {queueStatus}";
            LastStatus = status;
            return true;
        }

        private bool TryArmReconnectProxyForPassiveAttach(out string status)
        {
            status = "Transport official-session bridge has no passive Maple socket pair to arm for deferred queueing.";

            SessionDiscoveryCandidate? passiveCandidate = _passiveEstablishedSession;
            if (!passiveCandidate.HasValue)
            {
                return false;
            }

            SessionDiscoveryCandidate candidate = passiveCandidate.Value;
            string resolvedRemoteHost = candidate.RemoteEndpoint.Address.ToString();
            int resolvedRemotePort = candidate.RemoteEndpoint.Port;
            int requestedListenPort = ListenPort > 0 ? ListenPort : DefaultListenPort;

            lock (_sync)
            {
                if (_roleSessionProxy.HasAttachedClient)
                {
                    status = $"Transport official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before arming a different reconnect proxy.";
                    return false;
                }

                if (IsRunning)
                {
                    status = $"Transport official-session bridge is already armed on 127.0.0.1:{ListenPort} for deferred reconnects.";
                    return true;
                }

                if (!TryStartProxyListener(requestedListenPort, resolvedRemoteHost, resolvedRemotePort, out string startStatus))
                {
                    status = $"Observed already-established transport Maple socket pair {DescribeEstablishedSession(candidate)}, but automatic reconnect-proxy arming failed. {startStatus}";
                    return false;
                }

                _passiveEstablishedSession = candidate;
                status = $"Observed already-established transport Maple socket pair {DescribeEstablishedSession(candidate)}. {startStatus} Deferred outbound queueing is now armed for reconnects through 127.0.0.1:{ListenPort}.";
                LastStatus = status;
                return true;
            }
        }

        public static bool TryParseOutboundRawPacketHex(string rawPacketHex, out byte[] rawPacket, out string status)
        {
            rawPacket = Array.Empty<byte>();
            status = null;

            if (string.IsNullOrWhiteSpace(rawPacketHex))
            {
                status = "Transport outbound raw packet hex cannot be empty.";
                return false;
            }

            if (!TryNormalizeOutboundRawPacketHex(rawPacketHex, out string normalizedHex, out status))
            {
                return false;
            }

            if (normalizedHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalizedHex = normalizedHex[2..];
            }

            if (normalizedHex.Length < sizeof(short) * 2)
            {
                status = "Transport outbound packet must include at least a 2-byte opcode.";
                return false;
            }

            if ((normalizedHex.Length & 1) != 0)
            {
                status = "Transport outbound packet hex must contain an even number of characters.";
                return false;
            }

            try
            {
                rawPacket = Convert.FromHexString(normalizedHex);
                if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
                {
                    status = "Transport outbound packet must include a 2-byte opcode.";
                    rawPacket = Array.Empty<byte>();
                    return false;
                }

                status = $"Parsed outbound {DescribeOutboundPacket(opcode, rawPacket)} from raw hex ({payload.Length} payload bytes).";
                return true;
            }
            catch (FormatException ex)
            {
                status = $"Transport outbound packet hex is invalid: {ex.Message}";
                rawPacket = Array.Empty<byte>();
                return false;
            }
        }

        private static bool TryNormalizeOutboundRawPacketHex(string rawPacketHex, out string normalizedHex, out string status)
        {
            normalizedHex = string.Empty;
            status = null;

            if (string.IsNullOrWhiteSpace(rawPacketHex))
            {
                status = "Transport outbound raw packet hex cannot be empty.";
                return false;
            }

            string trimmed = rawPacketHex.Trim();

            // Accept captured-history snippets such as:
            // "raw=0801002C..." or "... raw=08-01-00-2C ..."
            int rawEqualsIndex = trimmed.IndexOf("raw=", StringComparison.OrdinalIgnoreCase);
            if (rawEqualsIndex >= 0)
            {
                trimmed = trimmed[(rawEqualsIndex + "raw=".Length)..];
            }

            string[] recognizedPrefixes =
            {
                "packetoutraw",
                "packetraw",
                "raw",
                "hex",
                "payloadhex"
            };
            foreach (string prefix in recognizedPrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed[prefix.Length..].TrimStart(' ', '\t', ':', '=');
                    break;
                }
            }

            normalizedHex = string.Concat(trimmed.Where(ch =>
                !char.IsWhiteSpace(ch)
                && ch != '-'
                && ch != ':'
                && ch != ','
                && ch != ';'));
            if (string.IsNullOrWhiteSpace(normalizedHex))
            {
                status = "Transport outbound raw packet hex cannot be empty.";
                return false;
            }

            return true;
        }

        public void RecordDispatchResult(string source, TransportationPacketInboxMessage message, bool success, string result)
        {
            string summary = string.IsNullOrWhiteSpace(result)
                ? TransportationPacketInboxManager.DescribePacket(message?.PacketType ?? 0, message?.Payload)
                : $"{TransportationPacketInboxManager.DescribePacket(message?.PacketType ?? 0, message?.Payload)}: {result}";
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
        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.IsInit)
            {
                int flushed = FlushQueuedOutboundPacketsViaProxy();
                LastStatus = flushed > 0
                    ? $"Transport official-session bridge initialized Maple crypto and flushed {flushed} queued outbound packet(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeInboundTransportPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", out TransportationPacketInboxMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued {TransportationPacketInboxManager.DescribePacket(message.PacketType, message.Payload)} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            ForwardedOutboundCount++;

            if (TryDecodeOpcode(e.RawPacket, out int opcode, out byte[] payload))
            {
                bool isTransportOpcode = opcode == TransportationPacketInboxManager.PacketTypeContiMove
                    || opcode == TransportationPacketInboxManager.PacketTypeContiState
                    || opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode;
                if (isTransportOpcode)
                {
                    ForwardedOutboundTransportCount++;
                }

                RecordOutboundPacket(new OutboundPacketTrace(
                    opcode,
                    payload?.Length ?? 0,
                    BuildPayloadPreview(payload),
                    Convert.ToHexString(e.RawPacket),
                    e.SourceEndpoint));

                if (isTransportOpcode)
                {
                    LastStatus = $"Forwarded outbound {TransportationPacketInboxManager.DescribePacket(opcode, payload)} from {e.SourceEndpoint}.";
                    return;
                }
            }

            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void RecordSentOutboundPacket(
            int opcode,
            byte[] rawPacket,
            byte[] payload,
            string source,
            bool countAsTypedSend)
        {
            if (countAsTypedSend)
            {
                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = rawPacket;
            }

            ForwardedOutboundCount++;
            if (opcode == TransportationPacketInboxManager.PacketTypeContiMove
                || opcode == TransportationPacketInboxManager.PacketTypeContiState
                || opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode)
            {
                ForwardedOutboundTransportCount++;
            }

            RecordOutboundPacket(new OutboundPacketTrace(
                opcode,
                payload?.Length ?? 0,
                BuildPayloadPreview(payload),
                Convert.ToHexString(rawPacket),
                source));
        }

        private bool TrySendRawPacket(
            byte[] rawPacket,
            out string status,
            bool countAsTypedSend,
            bool allowDeferredQueueWhenPassive,
            out bool queuedForDeferredInjection)
        {
            queuedForDeferredInjection = false;
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                status = "Transport outbound packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            if (HasPassiveEstablishedSocketPair && !HasConnectedSession)
            {
                if (!allowDeferredQueueWhenPassive)
                {
                    status = $"Transport official-session bridge is observing {DescribePassiveEstablishedSession(_passiveEstablishedSession.Value)} and cannot inject until the client reconnects through the proxy.";
                    LastStatus = status;
                    return false;
                }

                if (!TryQueueRawPacket(rawPacket, out status))
                {
                    return false;
                }

                queuedForDeferredInjection = true;
                return true;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Transport official-session bridge has no active Maple session.";
                LastStatus = status;
                return false;
            }

            byte[] clonedPacket = (byte[])rawPacket.Clone();
            if (!_roleSessionProxy.TrySendToServer(clonedPacket, out string proxyStatus))
            {
                status = proxyStatus;
                LastStatus = status;
                return false;
            }

            RecordSentOutboundPacket(opcode, clonedPacket, payload, "simulator-send", countAsTypedSend);
            status = $"Injected outbound {DescribeOutboundPacket(opcode, clonedPacket)} into live session. {proxyStatus}";
            LastStatus = status;
            return true;
        }

        private int FlushQueuedOutboundPacketsViaProxy()
        {
            int flushed = 0;
            while (_pendingOutboundPackets.TryDequeue(out PendingOutboundPacket packet))
            {
                if (!_roleSessionProxy.TrySendToServer(packet.RawPacket, out string status))
                {
                    _pendingOutboundPackets.Enqueue(packet);
                    LastStatus = status;
                    break;
                }

                TryDecodeOpcode(packet.RawPacket, out int opcode, out byte[] payload);
                RecordSentOutboundPacket(opcode, packet.RawPacket, payload, "deferred-flush", countAsTypedSend: true);
                flushed++;
            }

            return flushed;
        }

        private bool TryStartProxyListener(int listenPort, string remoteHost, int remotePort, out string status)
        {
            int resolvedListenPort = listenPort <= 0 ? 0 : listenPort;
            try
            {
                RemoteHost = NormalizeRemoteHost(remoteHost);
                RemotePort = remotePort;
                if (!_roleSessionProxy.Start(resolvedListenPort, RemoteHost, RemotePort, out string proxyStatus))
                {
                    StopInternal(clearPending: false);
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                ListenPort = _roleSessionProxy.ListenPort;
                status = $"Transport official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                StopInternal(clearPending: false);
                status = $"Transport official-session bridge failed to start: {ex.Message}";
                LastStatus = status;
                return false;
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

            while (_pendingOutboundPackets.TryDequeue(out _))
            {
            }

            _recentOutboundPackets.Clear();
            ReceivedCount = 0;
            SentCount = 0;
            ForwardedOutboundCount = 0;
            ForwardedOutboundTransportCount = 0;
            QueuedCount = 0;
            LastSentOpcode = -1;
            LastSentRawPacket = Array.Empty<byte>();
            LastQueuedOpcode = -1;
            LastQueuedRawPacket = Array.Empty<byte>();
        }

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            opcode = rawPacket[0] | (rawPacket[1] << 8);
            payload = rawPacket.Length > sizeof(ushort)
                ? rawPacket[sizeof(ushort)..]
                : Array.Empty<byte>();
            return true;
        }

        private static bool TryDecodeInboundTransportPacket(byte[] rawPacket, string source, out TransportationPacketInboxMessage message)
        {
            message = null;
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return false;
            }

            if (opcode != TransportationPacketInboxManager.PacketTypeContiMove
                && opcode != TransportationPacketInboxManager.PacketTypeContiState)
            {
                return false;
            }

            message = new TransportationPacketInboxMessage(
                opcode,
                payload,
                source,
                $"packetclientraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}");
            return true;
        }

        private bool TryResolveRecentOutboundPacket(int historyIndexFromNewest, string action, out byte[] rawPacket, out string status)
        {
            rawPacket = Array.Empty<byte>();
            if (historyIndexFromNewest <= 0)
            {
                status = $"Transport outbound history {action} index must be 1 or greater.";
                return false;
            }

            lock (_sync)
            {
                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"Transport outbound history has {_recentOutboundPackets.Count} packet(s); cannot {action} index {historyIndexFromNewest}.";
                    return false;
                }

                OutboundPacketTrace trace = _recentOutboundPackets.ToArray()[^historyIndexFromNewest];
                rawPacket = Convert.FromHexString(trace.RawPacketHex);
                status = null;
                return true;
            }
        }

        private void RecordOutboundPacket(OutboundPacketTrace trace)
        {
            lock (_sync)
            {
                _recentOutboundPackets.Enqueue(trace);
                while (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }
            }
        }

        private static string BuildPayloadPreview(byte[] payload)
        {
            return Convert.ToHexString(payload ?? Array.Empty<byte>());
        }

        private static string DescribeOutboundPacket(int opcode, byte[] rawPacket)
        {
            return opcode == TransportationFieldInitRequestCodec.OutboundFieldInitOpcode
                ? $"field-init ({opcode})"
                : TransportationPacketInboxManager.DescribePacket(opcode, rawPacket != null && rawPacket.Length > sizeof(ushort) ? rawPacket[sizeof(ushort)..] : Array.Empty<byte>());
        }

        private static string NormalizeRemoteHost(string remoteHost)
        {
            string trimmed = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
            return IPAddress.TryParse(trimmed, out IPAddress address) ? address.ToString() : trimmed;
        }

        private static bool MatchesRequestedTargetConfiguration(
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
                && string.Equals(NormalizeRemoteHost(currentRemoteHost), NormalizeRemoteHost(requestedRemoteHost), StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint remoteEndpoint,
            bool ignoreListenPort)
        {
            return remoteEndpoint != null
                && MatchesRequestedTargetConfiguration(
                    currentListenPort,
                    currentRemoteHost,
                    currentRemotePort,
                    requestedListenPort,
                    remoteEndpoint.Address.ToString(),
                    remoteEndpoint.Port,
                    ignoreListenPort);
        }

        private static string BuildDiscoveryAttachmentRequirementMessage(int listenPort)
        {
            return $"Reconnect Maple through 127.0.0.1:{listenPort} so the role-session bridge can recover Maple crypto ownership.";
        }

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {DescribeEstablishedSession(candidate)}; proxy reconnect required for decrypt/inject";
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
                error = "Transport official-session discovery requires a process name or pid when a selector is provided.";
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
            if (filteredCandidates.Count == 0)
            {
                status = $"Transport official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match => $"{match.RemoteEndpoint.Address}:{match.RemoteEndpoint.Port} via {match.LocalEndpoint.Address}:{match.LocalEndpoint.Port}"));
                status = $"Transport official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        private static string DescribeDiscoveryCandidates(
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

            return string.Join(Environment.NewLine, filteredCandidates.Select(DescribeEstablishedSession));
        }

        private static IReadOnlyList<SessionDiscoveryCandidate> FilterCandidatesByLocalPort(IReadOnlyList<SessionDiscoveryCandidate> candidates, int? localPort)
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
            string selector = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName) ? DefaultProcessName : owningProcessName;
            return localPort.HasValue
                ? $"{selector} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selector} on remote port {remotePort}";
        }

        private static IEnumerable<TcpRowOwnerPid> EnumerateTcpRows()
        {
            int bufferSize = 0;
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, sort: true, AddressFamilyInet, TcpTableClass.TcpTableOwnerPidAll, 0);
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
                IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int index = 0; index < rowCount; index++)
                {
                    yield return Marshal.PtrToStructure<TcpRowOwnerPid>(rowPointer);
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int DecodePort(byte[] portBytes)
        {
            return portBytes == null || portBytes.Length < 2
                ? 0
                : (portBytes[0] << 8) | portBytes[1];
        }

        private static IPAddress DecodeAddress(uint address)
        {
            return new IPAddress(address);
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
            TcpTableOwnerPidAll = 5,
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);
    }
}
