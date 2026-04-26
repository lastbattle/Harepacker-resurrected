using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace HaCreator.MapSimulator.Managers
{
    public enum SocialListOfficialSessionBridgePayloadKind
    {
        FriendResult,
        PartyResult,
        GuildResult,
        AllianceResult
    }

    public sealed class SocialListOfficialSessionBridgeMessage
    {
        public SocialListOfficialSessionBridgeMessage(byte[] payload, string source, int opcode, SocialListOfficialSessionBridgePayloadKind kind)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "sociallist-session" : source;
            Opcode = opcode;
            Kind = kind;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public int Opcode { get; }
        public SocialListOfficialSessionBridgePayloadKind Kind { get; }
        public string ResultLabel => Kind switch
        {
            SocialListOfficialSessionBridgePayloadKind.FriendResult => "friend-result",
            SocialListOfficialSessionBridgePayloadKind.PartyResult => "party-result",
            SocialListOfficialSessionBridgePayloadKind.AllianceResult => "alliance-result",
            _ => "guild-result"
        };
    }

    /// <summary>
    /// Proxies a live Maple session and forwards the configured inbound
    /// CWvsContext::OnGuildResult and CWvsContext::OnAllianceResult opcodes into
    /// the existing social-list packet seams.
    /// </summary>
    public sealed class SocialListOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18504;
        public const ushort ClientPartyResultOpcode = 62;
        public const ushort ClientFriendResultOpcode = 65;
        public const ushort ClientGuildResultOpcode = 67;
        public const ushort ClientAllianceResultOpcode = 68;
        private const int MaxRecentPackets = 32;
        private const string DefaultProcessName = "MapleStory";

        private readonly ConcurrentQueue<SocialListOfficialSessionBridgeMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly Queue<SessionPacketTrace> _recentPackets = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        public readonly record struct SessionPacketTrace(
            string Direction,
            int Opcode,
            string ResultLabel,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source);

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public ushort FriendResultOpcode { get; private set; }
        public ushort PartyResultOpcode { get; private set; }
        public ushort GuildResultOpcode { get; private set; }
        public ushort AllianceResultOpcode { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public int ForwardedOutboundCount { get; private set; }
        public int InjectedOutboundCount { get; private set; }
        public int RecentPacketCount
        {
            get
            {
                lock (_sync)
                {
                    return _recentPackets.Count;
                }
            }
        }

        public int LastInjectedOutboundOpcode { get; private set; }
        public byte[] LastInjectedOutboundRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Social-list official-session bridge inactive.";

        public SocialListOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            string injectedText = InjectedOutboundCount > 0
                ? $"; injectedOutbound={InjectedOutboundCount}, lastInjectedOpcode={LastInjectedOutboundOpcode}"
                : $"; injectedOutbound={InjectedOutboundCount}";
            return $"Social-list official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}; history={RecentPacketCount}{injectedText}; {DescribeConfiguredOpcodes(FriendResultOpcode, PartyResultOpcode, GuildResultOpcode, AllianceResultOpcode)}. {LastStatus}";
        }

        public string DescribeRecentPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentPackets);
            lock (_sync)
            {
                if (_recentPackets.Count == 0)
                {
                    return "Social-list official-session bridge packet history is empty.";
                }

                SessionPacketTrace[] entries = _recentPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return "Social-list official-session bridge packet history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select((entry, index) =>
                            $"#{index + 1} {entry.Direction} opcode={entry.Opcode} kind={entry.ResultLabel} payloadLen={entry.PayloadLength} source={entry.Source} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentPackets()
        {
            lock (_sync)
            {
                _recentPackets.Clear();
            }

            LastStatus = "Social-list official-session bridge packet history cleared.";
            return LastStatus;
        }

        public bool TryReplayRecentPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = "Social-list replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            SessionPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentPackets.Count == 0)
                {
                    status = "No captured social-list packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentPackets.Count)
                {
                    status = $"Social-list replay index {historyIndexFromNewest} exceeds the {_recentPackets.Count} captured packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentPackets.ToArray();
            }

            SessionPacketTrace trace = entries[^historyIndexFromNewest];
            try
            {
                byte[] rawPacket = Convert.FromHexString(trace.RawPacketHex);
                if (string.Equals(trace.Direction, "out", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trace.Direction, "inject", StringComparison.OrdinalIgnoreCase))
                {
                    return TrySendOutboundRawPacket(rawPacket, out status);
                }

                if (!TryDecodeInboundResultPacket(
                        rawPacket,
                        $"history-replay:{trace.Source}",
                        FriendResultOpcode,
                        PartyResultOpcode,
                        GuildResultOpcode,
                        AllianceResultOpcode,
                        out SocialListOfficialSessionBridgeMessage message))
                {
                    status = $"Captured social-list packet {historyIndexFromNewest} is not a configured inbound result packet.";
                    LastStatus = status;
                    return false;
                }

                _pendingMessages.Enqueue(message);
                status = $"Queued captured {message.ResultLabel} opcode {message.Opcode} from social-list history entry {historyIndexFromNewest}.";
                LastStatus = status;
                return true;
            }
            catch (FormatException ex)
            {
                status = $"Captured social-list packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(
            int listenPort,
            string remoteHost,
            int remotePort,
            ushort friendResultOpcode,
            ushort partyResultOpcode,
            ushort guildResultOpcode,
            ushort allianceResultOpcode)
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
                ResetInboundState();

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    FriendResultOpcode = friendResultOpcode;
                    PartyResultOpcode = partyResultOpcode;
                    GuildResultOpcode = guildResultOpcode;
                    AllianceResultOpcode = allianceResultOpcode;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = $"Social-list official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering {DescribeConfiguredOpcodes(friendResultOpcode, partyResultOpcode, guildResultOpcode, allianceResultOpcode)}. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"Social-list official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(
            int listenPort,
            int remotePort,
            ushort friendResultOpcode,
            ushort partyResultOpcode,
            ushort guildResultOpcode,
            ushort allianceResultOpcode,
            string processSelector,
            int? localPort,
            out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Social-list official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
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

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                GuildBossOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(
                    IsRunning,
                    ListenPort,
                    RemoteHost,
                    RemotePort,
                    FriendResultOpcode,
                    PartyResultOpcode,
                    GuildResultOpcode,
                    AllianceResultOpcode,
                    resolvedListenPort,
                    candidate.RemoteEndpoint,
                    friendResultOpcode,
                    partyResultOpcode,
                    guildResultOpcode,
                    allianceResultOpcode))
            {
                status = $"Social-list official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} using {DescribeConfiguredOpcodes(friendResultOpcode, partyResultOpcode, guildResultOpcode, allianceResultOpcode)}.";
                LastStatus = status;
                return true;
            }

            Start(
                resolvedListenPort,
                candidate.RemoteEndpoint.Address.ToString(),
                candidate.RemoteEndpoint.Port,
                friendResultOpcode,
                partyResultOpcode,
                guildResultOpcode,
                allianceResultOpcode);
            status = $"Social-list official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                GuildBossOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
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
                LastStatus = "Social-list official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out SocialListOfficialSessionBridgeMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "social-list result payload" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode))
            {
                status = "Social-list outbound raw packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = "Social-list official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            if (!_roleSessionProxy.TrySendToServer(clonedRawPacket, out string proxyStatus))
            {
                status = proxyStatus;
                LastStatus = status;
                return false;
            }

            InjectedOutboundCount++;
            LastInjectedOutboundOpcode = opcode;
            LastInjectedOutboundRawPacket = clonedRawPacket;
            RecordRecentPacket("inject", opcode, "client-request", clonedRawPacket, clonedRawPacket, "manual-inject");
            status = $"Injected social-list outbound opcode {opcode} into live session.";
            LastStatus = status;
            return true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        private static bool TryDecodeInboundResultPacket(
            byte[] rawPacket,
            string source,
            ushort friendResultOpcode,
            ushort partyResultOpcode,
            ushort guildResultOpcode,
            ushort allianceResultOpcode,
            out SocialListOfficialSessionBridgeMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            SocialListOfficialSessionBridgePayloadKind kind;
            if (friendResultOpcode > 0 && opcode == friendResultOpcode)
            {
                kind = SocialListOfficialSessionBridgePayloadKind.FriendResult;
            }
            else if (partyResultOpcode > 0 && opcode == partyResultOpcode)
            {
                kind = SocialListOfficialSessionBridgePayloadKind.PartyResult;
            }
            else if (guildResultOpcode > 0 && opcode == guildResultOpcode)
            {
                kind = SocialListOfficialSessionBridgePayloadKind.GuildResult;
            }
            else if (allianceResultOpcode > 0 && opcode == allianceResultOpcode)
            {
                kind = SocialListOfficialSessionBridgePayloadKind.AllianceResult;
            }
            else
            {
                return false;
            }

            byte[] payload = rawPacket.Skip(sizeof(ushort)).ToArray();
            message = new SocialListOfficialSessionBridgeMessage(payload, source, opcode, kind);
            return true;
        }

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeInboundResultPacket(
                    e.RawPacket,
                    $"official-session:{e.SourceEndpoint}",
                    FriendResultOpcode,
                    PartyResultOpcode,
                    GuildResultOpcode,
                    AllianceResultOpcode,
                    out SocialListOfficialSessionBridgeMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            RecordRecentPacket("in", message.Opcode, message.ResultLabel, message.Payload, e.RawPacket, message.Source);
            LastStatus = $"Queued {message.ResultLabel} opcode {message.Opcode} ({message.Payload.Length} byte(s)) from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            ForwardedOutboundCount++;
            int opcode = e.RawPacket.Length >= sizeof(ushort) ? BitConverter.ToUInt16(e.RawPacket, 0) : 0;
            RecordRecentPacket("out", opcode, "client-request", e.RawPacket.Length > sizeof(ushort) ? e.RawPacket[sizeof(ushort)..] : Array.Empty<byte>(), e.RawPacket, $"official-session:{e.SourceEndpoint}");
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

                ResetInboundState();
            }
        }

        private void ResetInboundState()
        {
            ForwardedOutboundCount = 0;
            InjectedOutboundCount = 0;
            LastInjectedOutboundOpcode = 0;
            LastInjectedOutboundRawPacket = Array.Empty<byte>();
            lock (_sync)
            {
                _recentPackets.Clear();
            }
        }

        private void RecordRecentPacket(string direction, int opcode, string resultLabel, byte[] payload, byte[] rawPacket, string source)
        {
            SessionPacketTrace trace = new(
                direction,
                opcode,
                string.IsNullOrWhiteSpace(resultLabel) ? "unknown" : resultLabel.Trim(),
                payload?.Length ?? 0,
                Convert.ToHexString(payload ?? Array.Empty<byte>()),
                Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                string.IsNullOrWhiteSpace(source) ? "sociallist-session" : source.Trim());

            lock (_sync)
            {
                _recentPackets.Enqueue(trace);
                while (_recentPackets.Count > MaxRecentPackets)
                {
                    _recentPackets.Dequeue();
                }
            }
        }

        private static bool TryDecodeOpcode(byte[] rawPacket, out int opcode)
        {
            opcode = 0;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            return true;
        }

        private static bool TryResolveProcessSelector(string selector, out int? owningProcessId, out string owningProcessName, out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;

            string trimmed = selector?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                owningProcessName = DefaultProcessName;
                return true;
            }

            if (int.TryParse(trimmed, out int processId) && processId > 0)
            {
                owningProcessId = processId;
                return true;
            }

            try
            {
                Process.GetProcessesByName(trimmed).FirstOrDefault();
                owningProcessName = trimmed;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Social-list official-session discovery could not inspect process selector '{trimmed}': {ex.Message}";
                return false;
            }
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int listenPort,
            string remoteHost,
            int remotePort,
            ushort friendOpcode,
            ushort partyOpcode,
            ushort opcode,
            ushort allianceOpcode,
            int desiredListenPort,
            IPEndPoint desiredRemoteEndpoint,
            ushort desiredFriendOpcode,
            ushort desiredPartyOpcode,
            ushort desiredOpcode,
            ushort desiredAllianceOpcode)
        {
            if (!isRunning || desiredRemoteEndpoint == null)
            {
                return false;
            }

            return listenPort == desiredListenPort
                && remotePort == desiredRemoteEndpoint.Port
                && friendOpcode == desiredFriendOpcode
                && partyOpcode == desiredPartyOpcode
                && opcode == desiredOpcode
                && allianceOpcode == desiredAllianceOpcode
                && string.Equals(remoteHost, desiredRemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeConfiguredOpcodes(
            ushort friendResultOpcode,
            ushort partyResultOpcode,
            ushort guildResultOpcode,
            ushort allianceResultOpcode)
        {
            string friendOpcode = friendResultOpcode > 0 ? friendResultOpcode.ToString() : "unset";
            string partyOpcode = partyResultOpcode > 0 ? partyResultOpcode.ToString() : "unset";
            string guildOpcode = guildResultOpcode > 0 ? guildResultOpcode.ToString() : "unset";
            string allianceOpcode = allianceResultOpcode > 0 ? allianceResultOpcode.ToString() : "unset";
            return $"friend-result opcode {friendOpcode}, party-result opcode {partyOpcode}, guild-result opcode {guildOpcode}, and alliance-result opcode {allianceOpcode}";
        }

        private static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            candidate = default;
            status = null;

            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Social-list official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(
                    ", ",
                    filteredCandidates.Select(entry => $"{entry.ProcessName}({entry.ProcessId}) local {entry.LocalEndpoint.Port} -> remote {entry.RemoteEndpoint.Port}"));
                status = $"Social-list official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
                return false;
            }

            candidate = filteredCandidates[0];
            return true;
        }

        private static IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int? localPort)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return Array.Empty<GuildBossOfficialSessionBridgeManager.SessionDiscoveryCandidate>();
            }

            return localPort.HasValue
                ? candidates.Where(candidate => candidate.LocalEndpoint.Port == localPort.Value).ToArray()
                : candidates;
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"process {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            string remoteScope = remotePort > 0 ? $"remote port {remotePort}" : "any remote port";
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on {remoteScope}{localScope}";
        }
    }
}
