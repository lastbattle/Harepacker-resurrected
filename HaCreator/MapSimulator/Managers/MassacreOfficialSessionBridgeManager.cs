using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Massacre transport bridge that proxies a live Maple session
    /// and feeds inbound Massacre packets into the existing packet-owned seam.
    /// </summary>
    public sealed class MassacreOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18489;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;
        private const int CurrentWrapperRelayOpcode = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
        private const int DefaultInboundSessionValueOpcode = 93;
        private const int PacketTypeIncGauge = 173;
        private const int PacketTypeResult = 174;
        private const int MassacreCoolSessionKeyStringPoolId = 0x14F1;
        private const int MassacreHitSessionKeyStringPoolId = 0x14F2;
        private const int MassacreStageSessionKeyStringPoolId = 0x14F3;
        private const int MassacreMissSessionKeyStringPoolId = 0x14F4;
        private const int MassacrePartySessionKeyStringPoolId = 0x14F5;
        private const int MassacreSkillSessionKeyStringPoolId = 0x14F6;
        private const string MassacreCoolSessionKeyFallback = "massacre_cool";
        private const string MassacreHitSessionKeyFallback = "massacre_hit";
        private const string MassacreStageSessionKeyFallback = "massacre_laststage";
        private const string MassacreMissSessionKeyFallback = "massacre_miss";
        private const string MassacrePartySessionKeyFallback = "massacre_party";
        private const string MassacreSkillSessionKeyFallback = "massacre_skill";
        private const int MaxNestedRelayDepth = 8;
        private const int MaxRecentInboundPackets = 32;
        private const string DiscoverCommandUsage = "/massacre session discover <remotePort> [processName|pid] [localPort]";
        private const string AttachCommandUsage = "/massacre session attach <remotePort> [processName|pid] [localPort]";
        private const string AttachProxyCommandUsage = "/massacre session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        private const string StartAutoCommandUsage = "/massacre session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        private readonly ConcurrentQueue<MassacrePacketInboxMessage> _pendingMessages = new();
        private readonly Dictionary<int, MassacrePacketInboxMessageKind> _mappedInboundOpcodes = new();
        private readonly Queue<InboundPacketTrace> _recentInboundPackets = new();
        private readonly object _sync = new();
        private readonly MassacreSessionValueInfoState _sessionValueInfoState = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private SessionDiscoveryCandidate? _passiveEstablishedSession;

        public MassacreOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public readonly record struct SessionDiscoveryCandidate(
            int ProcessId,
            string ProcessName,
            IPEndPoint LocalEndpoint,
            IPEndPoint RemoteEndpoint);
        public readonly record struct InboundPacketTrace(
            int Opcode,
            int PacketType,
            MassacrePacketInboxMessageKind Kind,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source);

        internal sealed class MassacreSessionValueInfoState
        {
            public bool HasHit { get; private set; }
            public bool HasMiss { get; private set; }
            public bool HasCool { get; private set; }
            public int Hit { get; private set; }
            public int Miss { get; private set; }
            public int Cool { get; private set; }
            public int Skill { get; private set; }

            public bool TryApply(string key, int value, out bool hasInfoBaseline)
            {
                hasInfoBaseline = false;
                if (!TryMatchSessionValueKey(key, MassacreHitSessionKeyStringPoolId, MassacreHitSessionKeyFallback))
                {
                    if (!TryMatchSessionValueKey(key, MassacreMissSessionKeyStringPoolId, MassacreMissSessionKeyFallback))
                    {
                        if (!TryMatchSessionValueKey(key, MassacreCoolSessionKeyStringPoolId, MassacreCoolSessionKeyFallback))
                        {
                            if (!TryMatchSessionValueKey(key, MassacreSkillSessionKeyStringPoolId, MassacreSkillSessionKeyFallback))
                            {
                                return false;
                            }

                            Skill = value;
                            hasInfoBaseline = HasHit && HasMiss && HasCool;
                            return true;
                        }

                        Cool = value;
                        HasCool = true;
                        hasInfoBaseline = HasHit && HasMiss;
                        return true;
                    }

                    Miss = value;
                    HasMiss = true;
                    hasInfoBaseline = HasHit && HasCool;
                    return true;
                }

                Hit = value;
                HasHit = true;
                hasInfoBaseline = HasMiss && HasCool;
                return true;
            }

            public void Clear()
            {
                HasHit = false;
                HasMiss = false;
                HasCool = false;
                Hit = 0;
                Miss = 0;
                Cool = 0;
                Skill = 0;
            }
        }
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasPassiveEstablishedSocketPair => _passiveEstablishedSession.HasValue && !_roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Massacre official-session bridge inactive.";

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
            return $"Massacre official-session bridge {lifecycle}; {session}; received={ReceivedCount}; mapped inbound opcodes={DescribeMappedInboundOpcodes()}. {LastStatus}";
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
                        status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesRequestedTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, resolvedRemoteHost, remotePort, autoSelectListenPort))
                {
                    status = $"Massacre official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = requestedListenPort;
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
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
                    StopInternal(clearPending: true);
                    LastStatus = $"Massacre official-session bridge failed to start: {ex.Message}";
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
                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, requestedListenPort, candidate.RemoteEndpoint, autoSelectListenPort))
            {
                status = $"Massacre official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Massacre official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Massacre official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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
                status = "Massacre official-session attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                if (HasAttachedClient)
                {
                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before observing an already-established socket pair.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;
                RemoteHost = candidate.RemoteEndpoint.Address.ToString();
                RemotePort = candidate.RemoteEndpoint.Port;
                LastStatus = $"Observed already-established Massacre Maple socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}. This path cannot decrypt post-handshake Massacre clock/context/result traffic or inject wrapper packets after the Maple handshake; reconnect through the localhost proxy for live packet ownership.";
                status = LastStatus;
                return true;
            }
        }

        public bool TryAttachEstablishedSessionAndStartProxy(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
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
                status = "Massacre official-session proxy attach requires an established Maple client socket pair.";
                LastStatus = status;
                return false;
            }

            if (listenPort < 0 || listenPort > ushort.MaxValue)
            {
                status = "Massacre official-session proxy attach listen port must be 0 or a valid TCP port.";
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
                        status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Massacre official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before preparing an already-established socket pair for reconnect.";
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
                        $"Massacre official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}; keeping existing proxy listener on 127.0.0.1:{ListenPort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);
                _passiveEstablishedSession = candidate;

                if (!TryStartProxyListener(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
                {
                    _passiveEstablishedSession = candidate;
                    LastStatus = $"Observed already-established Massacre Maple socket pair {DescribeEstablishedSession(candidate)}, but reconnect proxy startup failed. {startStatus}";
                    status = LastStatus;
                    return false;
                }

                LastStatus =
                    $"Observed already-established Massacre Maple socket pair {DescribeEstablishedSession(candidate)}. " +
                    $"Armed localhost proxy on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; reconnect Maple through this proxy to recover Massacre decrypt/inject ownership for clock/context/result traffic through the existing bridge seam.";
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

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Massacre official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MassacrePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public string DescribeRecentInboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentInboundPackets);
            lock (_sync)
            {
                if (_recentInboundPackets.Count == 0)
                {
                    return "Massacre official-session bridge inbound history is empty.";
                }

                InboundPacketTrace[] entries = _recentInboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Massacre official-session bridge inbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode=0x{entry.Opcode:X4} kind={entry.Kind} packetType={entry.PacketType} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentInboundPackets()
        {
            lock (_sync)
            {
                _recentInboundPackets.Clear();
            }

            LastStatus = "Massacre official-session bridge inbound history cleared.";
            return LastStatus;
        }

        public bool TryMapInboundOpcode(int opcode, MassacrePacketInboxMessageKind kind, out string status)
        {
            if (opcode < 0 || opcode > ushort.MaxValue)
            {
                status = "Massacre inbound opcode must fit in an unsigned 16-bit packet id.";
                return false;
            }

            if (!IsSupportedMappedKind(kind))
            {
                status = $"Massacre official-session bridge cannot map inbound opcode {opcode} to {kind}.";
                return false;
            }

            lock (_sync)
            {
                _mappedInboundOpcodes[opcode] = kind;
                status = $"Massacre official-session bridge maps inbound opcode 0x{opcode:X4} to {DescribeMappedKind(kind)}.";
                LastStatus = status;
                return true;
            }
        }

        public bool TryRemoveMappedInboundOpcode(int opcode, out string status)
        {
            lock (_sync)
            {
                if (_mappedInboundOpcodes.Remove(opcode))
                {
                    status = $"Massacre official-session bridge removed inbound opcode 0x{opcode:X4}.";
                    LastStatus = status;
                    return true;
                }
            }

            status = $"Massacre inbound opcode 0x{opcode:X4} is not currently mapped.";
            return false;
        }

        public void ClearMappedInboundOpcodes()
        {
            lock (_sync)
            {
                _mappedInboundOpcodes.Clear();
                LastStatus = "Massacre official-session bridge cleared mapped inbound opcodes.";
            }
        }

        public string DescribeMappedInboundOpcodes()
        {
            lock (_sync)
            {
                if (_mappedInboundOpcodes.Count == 0)
                {
                    return "none";
                }

                return string.Join(
                    ", ",
                    _mappedInboundOpcodes
                        .OrderBy(pair => pair.Key)
                        .Select(pair => $"0x{pair.Key:X4}->{DescribeMappedKind(pair.Value)}"));
            }
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = packetType switch
            {
                DefaultInboundSessionValueOpcode => "CWvsContext::OnSessionValue(93) Massacre payload",
                CurrentWrapperRelayOpcode => $"CField::OnPacket relay {CurrentWrapperRelayOpcode}",
                PacketTypeIncGauge => "Massacre inc gauge",
                PacketTypeResult => "Massacre result",
                _ => $"Massacre packet {packetType}"
            };
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void RecordDispatchResult(string source, MassacrePacketInboxMessage message, bool success, string result)
        {
            string packetLabel = message?.Kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => "Massacre clock payload",
                MassacrePacketInboxMessageKind.Info => "Massacre context payload",
                MassacrePacketInboxMessageKind.InfoPayload => "Massacre context payload",
                MassacrePacketInboxMessageKind.Stage => "Massacre stage payload",
                MassacrePacketInboxMessageKind.Bonus => "Massacre bonus payload",
                MassacrePacketInboxMessageKind.Packet => message.PacketType switch
                {
                    DefaultInboundSessionValueOpcode => "CWvsContext::OnSessionValue(93) Massacre payload",
                    CurrentWrapperRelayOpcode => $"CField::OnPacket relay {CurrentWrapperRelayOpcode}",
                    PacketTypeIncGauge => "Massacre inc gauge",
                    PacketTypeResult => "Massacre result",
                    _ => $"Massacre packet {message.PacketType}"
                },
                _ => "Massacre official-session payload"
            };
            string summary = string.IsNullOrWhiteSpace(result) ? packetLabel : $"{packetLabel}: {result}";
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
            bool autoSelectListenPort = listenPort <= 0;
            int requestedListenPort = autoSelectListenPort ? 0 : listenPort;
            string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);

            try
            {
                ListenPort = requestedListenPort;
                RemoteHost = resolvedRemoteHost;
                RemotePort = remotePort;
                if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                {
                    StopInternal(clearPending: false);
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                ListenPort = _roleSessionProxy.ListenPort;
                status = proxyStatus;
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                StopInternal(clearPending: false);
                status = $"Massacre official-session bridge failed to start: {ex.Message}";
                LastStatus = status;
                return false;
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

            if (!TryDecodeInboundMassacrePacket(
                    e.RawPacket,
                    $"official-session:{e.SourceEndpoint}",
                    GetMappedInboundOpcodesSnapshot(),
                    _sessionValueInfoState,
                    out MassacrePacketInboxMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            RecordInboundPacket(e.RawPacket, message, $"official-session:{e.SourceEndpoint}");
            ReceivedCount++;
            LastStatus = $"Queued {DescribeMessage(message)} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            LastStatus = _roleSessionProxy.LastStatus;
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

            _recentInboundPackets.Clear();
            _sessionValueInfoState.Clear();
            ReceivedCount = 0;
        }

        private Dictionary<int, MassacrePacketInboxMessageKind> GetMappedInboundOpcodesSnapshot()
        {
            lock (_sync)
            {
                return new Dictionary<int, MassacrePacketInboxMessageKind>(_mappedInboundOpcodes);
            }
        }

        private static bool IsSupportedMappedKind(MassacrePacketInboxMessageKind kind)
        {
            return kind == MassacrePacketInboxMessageKind.ClockPayload
                || kind == MassacrePacketInboxMessageKind.InfoPayload
                || kind == MassacrePacketInboxMessageKind.IncGauge
                || kind == MassacrePacketInboxMessageKind.Stage
                || kind == MassacrePacketInboxMessageKind.Bonus
                || kind == MassacrePacketInboxMessageKind.Result
                || kind == MassacrePacketInboxMessageKind.Packet;
        }

        private static string DescribeMappedKind(MassacrePacketInboxMessageKind kind)
        {
            return kind switch
            {
                MassacrePacketInboxMessageKind.ClockPayload => "clock payload",
                MassacrePacketInboxMessageKind.InfoPayload => "context payload",
                MassacrePacketInboxMessageKind.IncGauge => "inc gauge",
                MassacrePacketInboxMessageKind.Stage => "stage",
                MassacrePacketInboxMessageKind.Bonus => "bonus",
                MassacrePacketInboxMessageKind.Result => "result",
                MassacrePacketInboxMessageKind.Packet => "packet",
                _ => kind.ToString()
            };
        }

        internal static bool TryDecodeInboundMassacrePacket(
            byte[] rawPacket,
            string source,
            IReadOnlyDictionary<int, MassacrePacketInboxMessageKind> mappedOpcodes,
            MassacreSessionValueInfoState sessionValueInfoState,
            out MassacrePacketInboxMessage message)
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

            if (mappedOpcodes != null && mappedOpcodes.TryGetValue(opcode, out MassacrePacketInboxMessageKind mappedKind))
            {
                return TryDecodeMappedInboundPacket(
                    mappedKind,
                    opcode,
                    payload,
                    source,
                    sessionValueInfoState,
                    rawPacket,
                    out message);
            }

            if (opcode == CurrentWrapperRelayOpcode
                && SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(payload, out int relayedPacketType, out byte[] relayedPayload, out _))
            {
                if (relayedPacketType == DefaultInboundSessionValueOpcode)
                {
                    return TryDecodeSessionValueMessage(relayedPayload, source, sessionValueInfoState, rawPacket, out message);
                }

                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Packet,
                    source,
                    $"packetclientraw {Convert.ToHexString(rawPacket)}",
                    packetType: relayedPacketType,
                    payload: relayedPayload);
                return relayedPacketType == DefaultInboundSessionValueOpcode
                    || relayedPacketType == PacketTypeIncGauge
                    || relayedPacketType == PacketTypeResult;
            }

            if (opcode == DefaultInboundSessionValueOpcode)
            {
                return TryDecodeSessionValueMessage(payload, source, sessionValueInfoState, rawPacket, out message);
            }

            if (opcode == PacketTypeIncGauge
                || opcode == PacketTypeResult)
            {
                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Packet,
                    source,
                    $"packetclientraw {Convert.ToHexString(rawPacket)}",
                    packetType: opcode,
                    payload: payload);
                return true;
            }

            return false;
        }

        private static bool TryDecodeMappedInboundPacket(
            MassacrePacketInboxMessageKind mappedKind,
            int opcode,
            byte[] payload,
            string source,
            MassacreSessionValueInfoState sessionValueInfoState,
            byte[] rawPacket,
            out MassacrePacketInboxMessage message)
        {
            message = null;
            string rawText = $"packetclientraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}";

            if (IsSessionValueMappedKind(mappedKind)
                && TryDecodeSessionValueMessage(payload, source, sessionValueInfoState, rawPacket, out MassacrePacketInboxMessage sessionMessage)
                && IsMappedSessionValueKindMatch(mappedKind, sessionMessage.Kind))
            {
                message = new MassacrePacketInboxMessage(
                    sessionMessage.Kind,
                    source,
                    rawText,
                    sessionMessage.Value1,
                    sessionMessage.Value2,
                    sessionMessage.Value3,
                    sessionMessage.Value4,
                    packetType: opcode,
                    payload: payload,
                    clearResult: sessionMessage.ClearResult,
                    hasScoreOverride: sessionMessage.HasScoreOverride,
                    hasRankOverride: sessionMessage.HasRankOverride,
                    rank: sessionMessage.Rank);
                return true;
            }

            if (mappedKind == MassacrePacketInboxMessageKind.Stage
                && TryDecodeMappedInt32Payload(payload, out int stage)
                && stage > 0)
            {
                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Stage,
                    source,
                    rawText,
                    value1: stage,
                    packetType: opcode,
                    payload: payload);
                return true;
            }

            if (mappedKind == MassacrePacketInboxMessageKind.Bonus
                && TryDecodeMappedInt32Payload(payload, out int bonusValue)
                && bonusValue >= 0)
            {
                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Bonus,
                    source,
                    rawText,
                    value1: bonusValue,
                    packetType: opcode,
                    payload: payload);
                return true;
            }

            if (mappedKind == MassacrePacketInboxMessageKind.IncGauge
                && TryDecodeMappedInt32Payload(payload, out int incGauge)
                && incGauge >= 0)
            {
                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.IncGauge,
                    source,
                    rawText,
                    value1: incGauge,
                    packetType: opcode,
                    payload: payload);
                return true;
            }

            message = new MassacrePacketInboxMessage(
                mappedKind,
                source,
                rawText,
                packetType: opcode,
                payload: payload);
            return true;
        }

        private static bool IsSessionValueMappedKind(MassacrePacketInboxMessageKind kind)
        {
            return kind == MassacrePacketInboxMessageKind.Stage
                || kind == MassacrePacketInboxMessageKind.Bonus
                || kind == MassacrePacketInboxMessageKind.Info
                || kind == MassacrePacketInboxMessageKind.InfoPayload;
        }

        private static bool IsMappedSessionValueKindMatch(MassacrePacketInboxMessageKind mappedKind, MassacrePacketInboxMessageKind decodedKind)
        {
            return mappedKind == decodedKind
                || (mappedKind == MassacrePacketInboxMessageKind.InfoPayload && decodedKind == MassacrePacketInboxMessageKind.Info);
        }

        private static bool TryDecodeMappedInt32Payload(byte[] payload, out int value)
        {
            value = 0;
            if (payload == null || payload.Length < sizeof(int))
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int)));
            return true;
        }

        private static bool TryDecodeSessionValueMessage(
            byte[] payload,
            string source,
            MassacreSessionValueInfoState sessionValueInfoState,
            byte[] rawPacket,
            out MassacrePacketInboxMessage message)
        {
            message = null;
            if (!TryDecodeSessionValuePair(payload, out string key, out string value))
            {
                return false;
            }

            if (!TryParseClientAtoi(value, out int parsedValue))
            {
                parsedValue = 0;
            }

            string rawText = $"packetclientraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}";
            if (TryMatchSessionValueKey(key, MassacreStageSessionKeyStringPoolId, MassacreStageSessionKeyFallback))
            {
                if (parsedValue <= 0)
                {
                    return false;
                }

                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Stage,
                    source,
                    rawText,
                    value1: parsedValue,
                    packetType: DefaultInboundSessionValueOpcode,
                    payload: payload);
                return true;
            }

            if (TryMatchSessionValueKey(key, MassacrePartySessionKeyStringPoolId, MassacrePartySessionKeyFallback))
            {
                if (parsedValue < 0)
                {
                    return false;
                }

                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Bonus,
                    source,
                    rawText,
                    value1: parsedValue,
                    packetType: DefaultInboundSessionValueOpcode,
                    payload: payload);
                return true;
            }

            if (parsedValue < 0)
            {
                return false;
            }

            if (sessionValueInfoState != null
                && sessionValueInfoState.TryApply(key, parsedValue, out bool hasInfoBaseline)
                && hasInfoBaseline)
            {
                message = new MassacrePacketInboxMessage(
                    MassacrePacketInboxMessageKind.Info,
                    source,
                    rawText,
                    sessionValueInfoState.Hit,
                    sessionValueInfoState.Miss,
                    sessionValueInfoState.Cool,
                    sessionValueInfoState.Skill,
                    packetType: DefaultInboundSessionValueOpcode,
                    payload: payload);
                return true;
            }

            return false;
        }

        private static bool TryDecodeSessionValuePair(byte[] payload, out string key, out string value)
        {
            key = null;
            value = null;
            payload ??= Array.Empty<byte>();

            int offset = 0;
            return TryReadMapleString(payload, ref offset, out key)
                && TryReadMapleString(payload, ref offset, out value)
                && offset == payload.Length
                && !string.IsNullOrWhiteSpace(key);
        }

        private static bool TryReadMapleString(byte[] payload, ref int offset, out string value)
        {
            value = null;
            if (payload == null || offset < 0 || offset + sizeof(ushort) > payload.Length)
            {
                return false;
            }

            int length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (length < 0 || offset + length > payload.Length)
            {
                return false;
            }

            value = Encoding.Default.GetString(payload, offset, length);
            offset += length;
            return true;
        }

        internal static bool TryParseClientAtoi(string text, out int value)
        {
            value = 0;
            if (text == null)
            {
                return false;
            }

            int index = 0;
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            int sign = 1;
            if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            {
                sign = text[index] == '-' ? -1 : 1;
                index++;
            }

            if (index >= text.Length || !char.IsDigit(text[index]))
            {
                return false;
            }

            long parsed = 0;
            while (index < text.Length && char.IsDigit(text[index]))
            {
                parsed = (parsed * 10) + (text[index] - '0');
                long signed = parsed * sign;
                if (signed > int.MaxValue)
                {
                    value = int.MaxValue;
                    return true;
                }

                if (signed < int.MinValue)
                {
                    value = int.MinValue;
                    return true;
                }

                index++;
            }

            value = (int)(parsed * sign);
            return true;
        }

        private void RecordInboundPacket(byte[] rawPacket, MassacrePacketInboxMessage message, string source)
        {
            lock (_sync)
            {
                _recentInboundPackets.Enqueue(new InboundPacketTrace(
                    rawPacket != null && rawPacket.Length >= sizeof(ushort)
                        ? BinaryPrimitives.ReadUInt16LittleEndian(rawPacket.AsSpan(0, sizeof(ushort)))
                        : -1,
                    message?.PacketType ?? -1,
                    message?.Kind ?? MassacrePacketInboxMessageKind.Packet,
                    message?.Payload?.Length ?? 0,
                    Convert.ToHexString(message?.Payload ?? Array.Empty<byte>()),
                    Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                    source));
                while (_recentInboundPackets.Count > MaxRecentInboundPackets)
                {
                    _recentInboundPackets.Dequeue();
                }
            }
        }

        private static string DescribeMessage(MassacrePacketInboxMessage message)
        {
            if (message == null)
            {
                return "Massacre packet";
            }

            return message.Kind switch
            {
                MassacrePacketInboxMessageKind.Clock => "Massacre clock",
                MassacrePacketInboxMessageKind.ClockPayload => "Massacre clock payload",
                MassacrePacketInboxMessageKind.Info => "Massacre info",
                MassacrePacketInboxMessageKind.InfoPayload => "Massacre info payload",
                MassacrePacketInboxMessageKind.IncGauge => "Massacre inc gauge",
                MassacrePacketInboxMessageKind.Stage => "Massacre stage",
                MassacrePacketInboxMessageKind.Bonus => "Massacre bonus",
                MassacrePacketInboxMessageKind.Result => "Massacre result",
                _ => $"Massacre packet {message.PacketType}"
            };
        }

        private static bool TryMatchSessionValueKey(string key, int stringPoolId, string fallback)
        {
            string expected = MapleStoryStringPool.GetOrFallback(stringPoolId, fallback);
            return string.Equals(key, expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, fallback, StringComparison.OrdinalIgnoreCase);
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
                error = "Massacre official-session discovery requires a process name or pid when a selector is provided.";
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

        private static string DescribeEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}";
        }

        private static string DescribePassiveEstablishedSession(SessionDiscoveryCandidate candidate)
        {
            return $"observing established socket pair {candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}; proxy reconnect required for decrypt/inject";
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
                status = $"Massacre official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Massacre official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use {DiscoverCommandUsage} to inspect them, then narrow with a localPort filter before using {AttachCommandUsage} for passive observation, {AttachProxyCommandUsage} to arm a reconnect proxy for an already-established socket pair, or {StartAutoCommandUsage} for reconnect proxy ownership.";
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

            return string.Join(
                Environment.NewLine,
                filteredCandidates
                    .Select(candidate =>
                        $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}")
                    .Concat(new[]
                    {
                        $"Use {AttachCommandUsage} for passive observation of an already-established Maple socket pair.",
                        $"Use {AttachProxyCommandUsage} to arm a reconnect proxy for an already-established Maple socket pair.",
                        $"Use {StartAutoCommandUsage} when you need a reconnect through the localhost proxy for decryptable traffic and outbound injection."
                    }));
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
