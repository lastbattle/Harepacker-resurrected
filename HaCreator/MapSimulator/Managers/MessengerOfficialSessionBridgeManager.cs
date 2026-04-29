using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MessengerOfficialSessionBridgeMessage
    {
        public MessengerOfficialSessionBridgeMessage(byte[] payload, string source, string rawText, int opcode)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "messenger-session" : source;
            RawText = rawText ?? string.Empty;
            Opcode = opcode;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int Opcode { get; }
    }

    public sealed class MessengerOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18504;
        public const ushort DefaultInboundResultOpcode = PacketOwnedSocialUtilityPacketTable.MessengerInboundOpcode;
        private const int MaxRecentOutboundPackets = 32;
        private const int MaxRecentInboundPackets = 32;
        private const int MaxPendingResultExpectations = 32;
        private const int MaxPassiveInboundOpcodeObservations = 64;
        private const string DefaultProcessName = "MapleStory";

        private readonly string _ownerName;
        private readonly int _defaultListenPort;
        private readonly ushort _defaultInboundOpcode;
        private readonly ushort _defaultOutboundOpcode;
        private readonly HashSet<ushort> _additionalInboundOpcodes;
        private readonly HashSet<ushort> _additionalOutboundOpcodes;
        private readonly ConcurrentQueue<MessengerOfficialSessionBridgeMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<MessengerOfficialSessionBridgeMessage> _pendingObservedOutboundMessages = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly object _sync = new();
        private readonly Queue<OutboundPacketTrace> _recentOutboundPackets = new();
        private readonly Queue<InboundPacketTrace> _recentInboundPackets = new();
        private readonly List<PendingResultExpectation> _pendingResultExpectations = new();
        private readonly HashSet<ushort> _observedInboundOpcodes = new();
        private readonly HashSet<byte> _observedMessengerInboundSubtypes = new();
        private readonly HashSet<byte> _observedMapleTvSendResultCodes = new();
        private readonly Dictionary<ushort, int> _passiveInboundOpcodeHitCounts = new();
        private readonly Dictionary<ushort, HashSet<byte>> _passiveInboundSubtypeObservations = new();
        private readonly Dictionary<ushort, HashSet<byte>> _passiveMapleTvResultCodeObservations = new();
        private readonly Dictionary<ushort, string> _passiveInboundOpcodeSampleRawHex = new();
        private readonly Dictionary<byte, string> _observedMessengerSubtypePayloadSamples = new();
        private readonly Dictionary<byte, string> _observedMapleTvResultCodePayloadSamples = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

        private readonly record struct PendingOutboundPacket(int Opcode, byte[] RawPacket);
        private readonly record struct PendingResultExpectation(
            int RequestOpcode,
            byte RequestSubtype,
            string Source,
            string Summary,
            int[] ExpectedInboundOpcodes,
            byte[] ExpectedInboundSubtypes,
            string ExpectationSummary,
            int[] MatchedInboundOpcodes);

        public readonly record struct OutboundPacketTrace(
            int Opcode,
            byte RequestType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            string Summary);

        public readonly record struct InboundPacketTrace(
            int Opcode,
            byte ResultType,
            int PayloadLength,
            string PayloadHex,
            string RawPacketHex,
            string Source,
            string Summary);

        private int _expectedResultRequestCount;
        private int _expectedResultMatchCount;
        private int _expectedResultMismatchCount;
        private int _expectedResultUnexpectedCount;
        private int _expectedResultEvictedCount;
        private int _unknownInboundBranchCount;
        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public ushort MessengerOpcode { get; private set; } = DefaultInboundResultOpcode;
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int PendingOutboundPacketCount => _pendingOutboundPackets.Count;
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public string LastStatus { get; private set; } = "Messenger official-session bridge inactive.";

        public MessengerOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
            : this(
                "Messenger",
                DefaultListenPort,
                PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode("Messenger", 0),
                PacketOwnedSocialUtilityPacketTable.MessengerOutboundOpcode,
                roleSessionProxyFactory: roleSessionProxyFactory)
        {
        }

        internal MessengerOfficialSessionBridgeManager(string ownerName, int defaultListenPort, ushort defaultInboundOpcode, ushort defaultOutboundOpcode = 0, Func<MapleRoleSessionProxy> roleSessionProxyFactory = null, params ushort[] additionalInboundOpcodes)
        {
            _ownerName = string.IsNullOrWhiteSpace(ownerName) ? "Messenger" : ownerName.Trim();
            _defaultListenPort = defaultListenPort <= 0 ? DefaultListenPort : defaultListenPort;
            ushort recoveredInboundOpcode = PacketOwnedSocialUtilityPacketTable.ResolveRecoveredInboundOpcode(_ownerName, defaultInboundOpcode);
            _defaultInboundOpcode = recoveredInboundOpcode == 0 ? DefaultInboundResultOpcode : recoveredInboundOpcode;

            ushort recoveredOutboundOpcode = PacketOwnedSocialUtilityPacketTable.GetRecoveredOutboundOpcodes(_ownerName).FirstOrDefault();
            _defaultOutboundOpcode = defaultOutboundOpcode != 0 ? defaultOutboundOpcode : recoveredOutboundOpcode;

            _additionalInboundOpcodes = new HashSet<ushort>(
                (additionalInboundOpcodes ?? Array.Empty<ushort>())
                .Concat(PacketOwnedSocialUtilityPacketTable.GetRecoveredInboundOpcodes(_ownerName))
                .Where(opcode => opcode != 0 && opcode != _defaultInboundOpcode));
            _additionalOutboundOpcodes = new HashSet<ushort>();
            foreach (ushort outboundOpcode in PacketOwnedSocialUtilityPacketTable.GetRecoveredOutboundOpcodes(_ownerName))
            {
                if (outboundOpcode != 0 && outboundOpcode != _defaultOutboundOpcode)
                {
                    _additionalOutboundOpcodes.Add(outboundOpcode);
                }
            }

            ListenPort = _defaultListenPort;
            MessengerOpcode = _defaultInboundOpcode;
            LastStatus = $"{_ownerName} official-session bridge inactive.";
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
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            string outboundText = DescribeOutboundOpcodeSet();
            string inboundText = DescribeInboundOpcodeSet();
            return $"{_ownerName} official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; pending={PendingOutboundPacketCount}; queued={QueuedCount}; inbound opcode {inboundText}{outboundText}; verify={DescribeRecoveredParityVerificationCompact()}.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public string DescribeRecentOutboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentOutboundPackets);
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    return $"{_ownerName} official-session bridge outbound history is empty.";
                }

                OutboundPacketTrace[] entries = _recentOutboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return $"{_ownerName} official-session bridge outbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.RequestType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string DescribeRecentInboundPackets(int maxCount = 10)
        {
            int normalizedCount = Math.Clamp(maxCount, 1, MaxRecentInboundPackets);
            lock (_sync)
            {
                if (_recentInboundPackets.Count == 0)
                {
                    return $"{_ownerName} official-session bridge inbound history is empty.";
                }

                InboundPacketTrace[] entries = _recentInboundPackets
                    .Reverse()
                    .Take(normalizedCount)
                    .ToArray();
                return $"{_ownerName} official-session bridge inbound history:"
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        entries.Select(entry =>
                            $"opcode={entry.Opcode} type={entry.ResultType} payloadLen={entry.PayloadLength} source={entry.Source} summary={entry.Summary} payloadHex={entry.PayloadHex} raw={entry.RawPacketHex}"));
            }
        }

        public string ClearRecentOutboundPackets()
        {
            lock (_sync)
            {
                _recentOutboundPackets.Clear();
            }

            LastStatus = $"{_ownerName} official-session bridge outbound history cleared.";
            return LastStatus;
        }

        public string ClearRecentInboundPackets()
        {
            lock (_sync)
            {
                _recentInboundPackets.Clear();
            }

            LastStatus = $"{_ownerName} official-session bridge inbound history cleared.";
            return LastStatus;
        }

        public string DescribeRecoveredPacketTable()
        {
            return PacketOwnedSocialUtilityPacketTable.DescribeRecoveredPacketTable(_ownerName);
        }

        public string DescribeRecoveredParityVerification()
        {
            lock (_sync)
            {
                string observedInboundOpcodes = _observedInboundOpcodes.Count == 0
                    ? "none"
                    : string.Join("/", _observedInboundOpcodes.OrderBy(opcode => opcode));
                string ownerBranchEvidence = string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase)
                    ? (_observedMapleTvSendResultCodes.Count == 0
                        ? "observed send-result codes=none"
                        : $"observed send-result codes={string.Join("/", _observedMapleTvSendResultCodes.OrderBy(code => code))}")
                    : (_observedMessengerInboundSubtypes.Count == 0
                        ? "observed subtypes=none"
                        : $"observed subtypes={string.Join("/", _observedMessengerInboundSubtypes.OrderBy(code => code))}");
                string passiveEvidence = DescribePassiveInboundEvidence();
                string outboundSamples = DescribeObservedOutboundSamples();
                string inboundSamples = DescribeObservedInboundSamples();
                return $"{_ownerName} recovered-table verification: observed inbound opcodes={observedInboundOpcodes}; {ownerBranchEvidence}; expected requests={_expectedResultRequestCount}; matched={_expectedResultMatchCount}; mismatched={_expectedResultMismatchCount}; unexpected={_expectedResultUnexpectedCount}; evicted={_expectedResultEvictedCount}; pending={_pendingResultExpectations.Count}; unknown branches={_unknownInboundBranchCount}; outbound samples {outboundSamples}; inbound samples {inboundSamples}; passive {passiveEvidence}.";
            }
        }

        public string ClearRecoveredParityVerification()
        {
            lock (_sync)
            {
                _pendingResultExpectations.Clear();
                _observedInboundOpcodes.Clear();
                _observedMessengerInboundSubtypes.Clear();
                _observedMapleTvSendResultCodes.Clear();
                _passiveInboundOpcodeHitCounts.Clear();
                _passiveInboundSubtypeObservations.Clear();
                _passiveMapleTvResultCodeObservations.Clear();
                _passiveInboundOpcodeSampleRawHex.Clear();
                _observedMessengerSubtypePayloadSamples.Clear();
                _observedMapleTvResultCodePayloadSamples.Clear();
                _expectedResultRequestCount = 0;
                _expectedResultMatchCount = 0;
                _expectedResultMismatchCount = 0;
                _expectedResultUnexpectedCount = 0;
                _expectedResultEvictedCount = 0;
                _unknownInboundBranchCount = 0;
            }

            LastStatus = $"{_ownerName} official-session bridge recovered-table verification counters cleared.";
            return LastStatus;
        }

        private string DescribeRecoveredParityVerificationCompact()
        {
            lock (_sync)
            {
                return $"observed={_observedInboundOpcodes.Count}; matched={_expectedResultMatchCount}; pending={_pendingResultExpectations.Count}; unknown={_unknownInboundBranchCount}";
            }
        }

        public bool TryReplayRecentOutboundPacket(int historyIndexFromNewest, out string status)
        {
            if (historyIndexFromNewest <= 0)
            {
                status = $"{_ownerName} replay index must be 1 or greater.";
                LastStatus = status;
                return false;
            }

            OutboundPacketTrace[] entries;
            lock (_sync)
            {
                if (_recentOutboundPackets.Count == 0)
                {
                    status = $"No captured {_ownerName} outbound packets are available to replay.";
                    LastStatus = status;
                    return false;
                }

                if (historyIndexFromNewest > _recentOutboundPackets.Count)
                {
                    status = $"{_ownerName} replay index {historyIndexFromNewest} exceeds the {_recentOutboundPackets.Count} captured outbound packet(s).";
                    LastStatus = status;
                    return false;
                }

                entries = _recentOutboundPackets.ToArray();
            }

            OutboundPacketTrace trace = entries[^historyIndexFromNewest];
            try
            {
                byte[] rawPacket = Convert.FromHexString(trace.RawPacketHex);
                return TrySendOutboundRawPacket(rawPacket, out status);
            }
            catch (FormatException ex)
            {
                status = $"Captured {_ownerName} outbound packet {historyIndexFromNewest} could not be replayed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void Start(int listenPort, string remoteHost, int remotePort, ushort messengerOpcode)
        {
            lock (_sync)
            {
                StopInternal(clearPending: false);
                ResetInboundState();

                try
                {
                    ListenPort = listenPort <= 0 ? _defaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    MessengerOpcode = messengerOpcode == 0 ? _defaultInboundOpcode : messengerOpcode;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: false);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = $"{_ownerName} official-session bridge listening on 127.0.0.1:{ListenPort}, proxying to {RemoteHost}:{RemotePort}, and filtering opcode {DescribeInboundOpcodeSet()}. {proxyStatus}";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: false);
                    LastStatus = $"{_ownerName} official-session bridge failed to start: {ex.Message}";
                }
            }
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, ushort messengerOpcode, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"{_ownerName} official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            int resolvedListenPort = listenPort <= 0 ? _defaultListenPort : listenPort;
            if (!TryResolveProcessSelector(processSelector, out int? owningProcessId, out string owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, _ownerName, out var candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            ushort resolvedOpcode = messengerOpcode == 0 ? _defaultInboundOpcode : messengerOpcode;
            if (IsRunning
                && ListenPort == resolvedListenPort
                && RemotePort == candidate.RemoteEndpoint.Port
                && MessengerOpcode == resolvedOpcode
                && string.Equals(RemoteHost, candidate.RemoteEndpoint.Address.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                status = $"{_ownerName} official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} using opcode {DescribeInboundOpcodeSet(resolvedOpcode)}.";
                LastStatus = status;
                return true;
            }

            Start(resolvedListenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, resolvedOpcode);
            status = $"{_ownerName} official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                LastStatus = $"{_ownerName} official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out MessengerOfficialSessionBridgeMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TryDequeueObservedOutbound(out MessengerOfficialSessionBridgeMessage message)
        {
            return _pendingObservedOutboundMessages.TryDequeue(out message);
        }

        internal void RecordRecoveredInboundPacketForTest(byte[] rawPacket, string source = "test-inbound")
        {
            RecordObservedInboundPacket(rawPacket, source);
        }

        internal void RecordRecoveredOutboundPacketForTest(byte[] rawPacket, string source = "test-outbound")
        {
            RecordObservedOutboundPacket(rawPacket, source);
        }

        public bool TrySendOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = $"{_ownerName} official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            if (!TryBuildRawPacket(opcode, payload, out byte[] rawPacket, out status))
            {
                LastStatus = status;
                return false;
            }

            try
            {
                if (!_roleSessionProxy.TrySendToServer(rawPacket, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = rawPacket;
                RecordObservedOutboundPacket(rawPacket, "simulator-send");
                status = $"Injected {_ownerName} outbound opcode {opcode} into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"{_ownerName} official-session outbound injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public bool TryQueueOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (!TryBuildRawPacket(opcode, payload, out byte[] rawPacket, out status))
            {
                LastStatus = status;
                return false;
            }

            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, rawPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = rawPacket;
            RecordObservedOutboundPacket(rawPacket, "simulator-queue");
            status = $"Queued {_ownerName} outbound opcode {opcode} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public bool TrySendOutboundRawPacket(byte[] rawPacket, out string status)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] _))
            {
                status = $"{_ownerName} outbound raw packet must include a 2-byte opcode.";
                LastStatus = status;
                return false;
            }

            if (!_roleSessionProxy.HasConnectedSession)
            {
                status = $"{_ownerName} official-session bridge has no connected Maple session for outbound injection.";
                LastStatus = status;
                return false;
            }

            byte[] clonedRawPacket = (byte[])rawPacket.Clone();
            try
            {
                if (!_roleSessionProxy.TrySendToServer(clonedRawPacket, out string proxyStatus))
                {
                    status = proxyStatus;
                    LastStatus = status;
                    return false;
                }

                SentCount++;
                LastSentOpcode = opcode;
                LastSentRawPacket = clonedRawPacket;
                RecordObservedOutboundPacket(clonedRawPacket, "simulator-replay");
                status = $"Injected {_ownerName} outbound opcode {opcode} raw packet into live session.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"{_ownerName} official-session outbound injection failed: {ex.Message}";
                LastStatus = status;
                return false;
            }
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "Messenger OnPacket payload" : detail;
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
                int flushed = FlushPendingOutboundPacketsViaProxy();
                LastStatus = flushed > 0
                    ? $"{_ownerName} official-session bridge initialized Maple crypto and flushed {flushed} queued request(s)."
                    : _roleSessionProxy.LastStatus;
                return;
            }

            if (!TryDecodeInboundMessengerPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", MessengerOpcode, _additionalInboundOpcodes, out MessengerOfficialSessionBridgeMessage message))
            {
                RecordPassiveInboundPacket(e.RawPacket);
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            RecordObservedInboundPacket(e.RawPacket, message.Source);
            ReceivedCount++;
            LastStatus = $"Queued {_ownerName} opcode {message.Opcode} ({message.Payload.Length} byte(s)) from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            RecordObservedOutboundPacket(e.RawPacket, $"official-session-client:{e.SourceEndpoint}");
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: clearPending);
            if (!clearPending)
            {
                return;
            }

            while (_pendingMessages.TryDequeue(out _))
            {
            }

            while (_pendingObservedOutboundMessages.TryDequeue(out _))
            {
            }

            while (_pendingOutboundPackets.TryDequeue(out _))
            {
            }

            _recentOutboundPackets.Clear();
            _recentInboundPackets.Clear();
            ResetInboundState();
            ReceivedCount = 0;
            SentCount = 0;
            QueuedCount = 0;
            LastSentOpcode = -1;
            LastSentRawPacket = Array.Empty<byte>();
            LastQueuedOpcode = -1;
            LastQueuedRawPacket = Array.Empty<byte>();
        }

        private int FlushPendingOutboundPacketsViaProxy()
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

                SentCount++;
                LastSentOpcode = packet.Opcode;
                LastSentRawPacket = packet.RawPacket;
                RecordObservedOutboundPacket(packet.RawPacket, "deferred-flush");
                flushed++;
            }

            return flushed;
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

        private static bool TryDecodeInboundMessengerPacket(
            byte[] rawPacket,
            string source,
            ushort primaryOpcode,
            IReadOnlySet<ushort> additionalOpcodes,
            out MessengerOfficialSessionBridgeMessage message)
        {
            message = null;
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return false;
            }

            if (opcode != primaryOpcode && additionalOpcodes?.Contains((ushort)opcode) != true)
            {
                return false;
            }

            message = new MessengerOfficialSessionBridgeMessage(
                payload,
                source,
                $"packetclientraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}",
                opcode);
            return true;
        }

        private void RecordObservedInboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return;
            }

            byte subtype = payload.Length > 0 ? payload[0] : (byte)0;
            bool hasKnownBranch = PacketOwnedSocialUtilityPacketTable.TryDecodeRecoveredInboundBranch(
                _ownerName,
                opcode,
                payload,
                out byte inboundSubtype,
                out byte resultCode,
                out string branchSummary);
            byte traceType = subtype;
            if (hasKnownBranch)
            {
                if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase)
                    && resultCode != byte.MaxValue)
                {
                    traceType = resultCode;
                }
                else if (inboundSubtype != byte.MaxValue)
                {
                    traceType = inboundSubtype;
                }
            }

            lock (_sync)
            {
                _observedInboundOpcodes.Add((ushort)opcode);
                if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
                {
                    if (resultCode != byte.MaxValue)
                    {
                        _observedMapleTvSendResultCodes.Add(resultCode);
                        _observedMapleTvResultCodePayloadSamples[resultCode] = Convert.ToHexString(payload);
                    }
                }
                else if (payload.Length > 0)
                {
                    _observedMessengerInboundSubtypes.Add(traceType);
                    _observedMessengerSubtypePayloadSamples[traceType] = Convert.ToHexString(payload);
                }

                string verificationSummary = ResolveInboundExpectationSummary(opcode, traceType, hasKnownBranch, branchSummary);
                _recentInboundPackets.Enqueue(new InboundPacketTrace(
                    opcode,
                    traceType,
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                    source,
                    verificationSummary));
                while (_recentInboundPackets.Count > MaxRecentInboundPackets)
                {
                    _recentInboundPackets.Dequeue();
                }
            }
        }

        private void RecordObservedOutboundPacket(byte[] rawPacket, string source)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return;
            }

            byte requestType = payload.Length > 0 ? payload[0] : (byte)0;
            lock (_sync)
            {
                string summary = IsTrackedOutboundOpcode(opcode) ? "tracked" : "observed";
                if (PacketOwnedSocialUtilityPacketTable.TryBuildRecoveredResultExpectation(
                        _ownerName,
                        opcode,
                        payload,
                        out int[] expectedInboundOpcodes,
                        out byte[] expectedInboundSubtypes,
                        out string expectationSummary))
                {
                    AddPendingResultExpectation(new PendingResultExpectation(
                        opcode,
                        requestType,
                        source,
                        Convert.ToHexString(payload),
                        expectedInboundOpcodes,
                        expectedInboundSubtypes,
                        expectationSummary,
                        Array.Empty<int>()));
                    summary = $"{summary}; {expectationSummary}";
                }

                _recentOutboundPackets.Enqueue(new OutboundPacketTrace(
                    opcode,
                    requestType,
                    payload.Length,
                    Convert.ToHexString(payload),
                    Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                    source,
                    summary));
                while (_recentOutboundPackets.Count > MaxRecentOutboundPackets)
                {
                    _recentOutboundPackets.Dequeue();
                }
            }

            if (ShouldQueueObservedOutboundForOwner(opcode, source))
            {
                _pendingObservedOutboundMessages.Enqueue(new MessengerOfficialSessionBridgeMessage(
                    payload,
                    source,
                    $"packetclientraw {Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}",
                    opcode));
            }
        }

        private bool ShouldQueueObservedOutboundForOwner(int opcode, string source)
        {
            if (string.IsNullOrWhiteSpace(source)
                || !source.StartsWith("official-session-client:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
            {
                return opcode == PacketOwnedSocialUtilityPacketTable.MapleTvOutboundConsumeCashItemOpcode;
            }

            return string.Equals(_ownerName, "Messenger", StringComparison.OrdinalIgnoreCase)
                && (opcode == PacketOwnedSocialUtilityPacketTable.MessengerOutboundOpcode
                    || opcode == PacketOwnedSocialUtilityPacketTable.MessengerClaimRequestOpcode);
        }

        private bool TryBuildRawPacket(int opcode, IReadOnlyList<byte> payload, out byte[] rawPacket, out string status)
        {
            rawPacket = Array.Empty<byte>();
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"{_ownerName} outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                return false;
            }

            byte[] safePayload = payload == null
                ? Array.Empty<byte>()
                : payload as byte[] ?? payload.ToArray();
            using PacketWriter writer = new();
            writer.Write((ushort)opcode);
            writer.WriteBytes(safePayload);
            rawPacket = writer.ToArray();

            status = null;
            return true;
        }

        private string DescribeInboundOpcodeSet()
        {
            return DescribeInboundOpcodeSet(MessengerOpcode);
        }

        private string DescribeOutboundOpcodeSet()
        {
            var outboundOpcodes = new List<ushort>();
            if (_defaultOutboundOpcode > 0)
            {
                outboundOpcodes.Add(_defaultOutboundOpcode);
            }

            outboundOpcodes.AddRange(_additionalOutboundOpcodes.Where(opcode => opcode > 0));
            ushort[] deduped = outboundOpcodes.Distinct().OrderBy(opcode => opcode).ToArray();
            if (deduped.Length == 0)
            {
                return string.Empty;
            }

            return deduped.Length == 1
                ? $"; outbound opcode={deduped[0]}"
                : $"; outbound opcodes={string.Join("/", deduped)}";
        }

        private bool IsTrackedOutboundOpcode(int opcode)
        {
            return _defaultOutboundOpcode == 0 && _additionalOutboundOpcodes.Count == 0
                || _defaultOutboundOpcode > 0 && opcode == _defaultOutboundOpcode
                || _additionalOutboundOpcodes.Contains((ushort)opcode);
        }

        private static int FindMatchingExpectationIndex(
            IReadOnlyList<PendingResultExpectation> expectations,
            int inboundOpcode,
            byte inboundSubtype,
            bool hasKnownSubtype)
        {
            for (int i = 0; i < expectations.Count; i++)
            {
                PendingResultExpectation expectation = expectations[i];
                if ((expectation.ExpectedInboundOpcodes?.Length ?? 0) > 0
                    && !expectation.ExpectedInboundOpcodes.Contains(inboundOpcode))
                {
                    continue;
                }

                if ((expectation.ExpectedInboundSubtypes?.Length ?? 0) == 0)
                {
                    return i;
                }

                if (hasKnownSubtype && expectation.ExpectedInboundSubtypes.Contains(inboundSubtype))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RecordPassiveInboundPacket(byte[] rawPacket)
        {
            if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload))
            {
                return;
            }

            if (!PacketOwnedSocialUtilityPacketTable.TryDecodeRecoveredInboundBranch(
                    _ownerName,
                    opcode,
                    payload,
                    out byte inboundSubtype,
                    out byte resultCode,
                    out _))
            {
                return;
            }

            lock (_sync)
            {
                if (_passiveInboundOpcodeHitCounts.Count >= MaxPassiveInboundOpcodeObservations
                    && !_passiveInboundOpcodeHitCounts.ContainsKey((ushort)opcode))
                {
                    return;
                }

                ushort opcodeKey = (ushort)opcode;
                _passiveInboundOpcodeHitCounts[opcodeKey] = _passiveInboundOpcodeHitCounts.TryGetValue(opcodeKey, out int count)
                    ? count + 1
                    : 1;
                _passiveInboundOpcodeSampleRawHex[opcodeKey] = Convert.ToHexString(rawPacket ?? Array.Empty<byte>());
                if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
                {
                    if (resultCode != byte.MaxValue)
                    {
                        if (!_passiveMapleTvResultCodeObservations.TryGetValue(opcodeKey, out HashSet<byte> resultCodes))
                        {
                            resultCodes = new HashSet<byte>();
                            _passiveMapleTvResultCodeObservations[opcodeKey] = resultCodes;
                        }

                        resultCodes.Add(resultCode);
                    }
                }
                else if (inboundSubtype != byte.MaxValue)
                {
                    if (!_passiveInboundSubtypeObservations.TryGetValue(opcodeKey, out HashSet<byte> subtypes))
                    {
                        subtypes = new HashSet<byte>();
                        _passiveInboundSubtypeObservations[opcodeKey] = subtypes;
                    }

                    subtypes.Add(inboundSubtype);
                }
            }
        }

        private void AddPendingResultExpectation(PendingResultExpectation expectation)
        {
            _pendingResultExpectations.Add(expectation);
            _expectedResultRequestCount++;
            while (_pendingResultExpectations.Count > MaxPendingResultExpectations)
            {
                _pendingResultExpectations.RemoveAt(0);
                _expectedResultEvictedCount++;
            }
        }

        private string ResolveInboundExpectationSummary(int opcode, byte inboundSubtype, bool hasKnownBranch, string branchSummary)
        {
            string branchText = string.IsNullOrWhiteSpace(branchSummary)
                ? "unknown recovered branch"
                : branchSummary;
            if (!hasKnownBranch)
            {
                _unknownInboundBranchCount++;
            }

            int matchIndex = FindMatchingExpectationIndex(_pendingResultExpectations, opcode, inboundSubtype, hasKnownBranch);
            if (matchIndex >= 0)
            {
                PendingResultExpectation expectation = _pendingResultExpectations[matchIndex];
                if (TryRecordMultiOpcodeExpectationMatch(expectation, opcode, out PendingResultExpectation updatedExpectation, out string partialSummary))
                {
                    _pendingResultExpectations[matchIndex] = updatedExpectation;
                    return $"{branchText}; {partialSummary}";
                }

                _pendingResultExpectations.RemoveAt(matchIndex);
                _expectedResultMatchCount++;
                return $"{branchText}; matched {expectation.ExpectationSummary} from {expectation.Source}";
            }

            int mismatchIndex = FindMismatchedExpectationIndex(_pendingResultExpectations, opcode);
            if (mismatchIndex >= 0)
            {
                PendingResultExpectation expectation = _pendingResultExpectations[mismatchIndex];
                _pendingResultExpectations.RemoveAt(mismatchIndex);
                _expectedResultMismatchCount++;
                return $"{branchText}; mismatched pending {expectation.ExpectationSummary} from {expectation.Source}";
            }

            _expectedResultUnexpectedCount++;
            return $"{branchText}; no pending recovered-table request expectation";
        }

        private static bool TryRecordMultiOpcodeExpectationMatch(
            PendingResultExpectation expectation,
            int inboundOpcode,
            out PendingResultExpectation updatedExpectation,
            out string summary)
        {
            updatedExpectation = expectation;
            summary = string.Empty;

            int[] expectedOpcodes = expectation.ExpectedInboundOpcodes?
                .Where(opcode => opcode > 0)
                .Distinct()
                .OrderBy(opcode => opcode)
                .ToArray()
                ?? Array.Empty<int>();
            if (expectedOpcodes.Length <= 1 || (expectation.ExpectedInboundSubtypes?.Length ?? 0) > 0)
            {
                return false;
            }

            int[] matchedOpcodes = expectation.MatchedInboundOpcodes?
                .Where(opcode => opcode > 0)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();
            if (!matchedOpcodes.Contains(inboundOpcode))
            {
                matchedOpcodes = matchedOpcodes.Concat(new[] { inboundOpcode }).Distinct().ToArray();
            }

            int[] remainingOpcodes = expectedOpcodes
                .Where(opcode => !matchedOpcodes.Contains(opcode))
                .ToArray();
            if (remainingOpcodes.Length == 0)
            {
                return false;
            }

            updatedExpectation = expectation with { MatchedInboundOpcodes = matchedOpcodes };
            summary = $"observed partial {expectation.ExpectationSummary} from {expectation.Source}; still waiting for opcode(s) {string.Join("/", remainingOpcodes)}";
            return true;
        }

        private string DescribePassiveInboundEvidence()
        {
            if (_passiveInboundOpcodeHitCounts.Count == 0)
            {
                return "recovered opcode candidates=none";
            }

            return string.Join(
                "; ",
                _passiveInboundOpcodeHitCounts
                    .OrderBy(entry => entry.Key)
                    .Select(entry =>
                    {
                        string branchEvidence;
                        if (string.Equals(_ownerName, "MapleTV", StringComparison.OrdinalIgnoreCase))
                        {
                            branchEvidence = _passiveMapleTvResultCodeObservations.TryGetValue(entry.Key, out HashSet<byte> resultCodes) && resultCodes.Count > 0
                                ? $"resultCodes={string.Join("/", resultCodes.OrderBy(code => code))}"
                                : "resultCodes=none";
                        }
                        else
                        {
                            branchEvidence = _passiveInboundSubtypeObservations.TryGetValue(entry.Key, out HashSet<byte> subtypes) && subtypes.Count > 0
                                ? $"subtypes={string.Join("/", subtypes.OrderBy(code => code))}"
                                : "subtypes=none";
                        }

                        string sample = _passiveInboundOpcodeSampleRawHex.TryGetValue(entry.Key, out string rawHex)
                            ? $", sample={rawHex}"
                            : string.Empty;
                        return $"opcode {entry.Key} hits={entry.Value} {branchEvidence}{sample}";
                    }));
        }

        private string DescribeObservedOutboundSamples()
        {
            if (_recentOutboundPackets.Count == 0)
            {
                return "none";
            }

            return string.Join(
                "; ",
                _recentOutboundPackets
                    .Reverse()
                    .GroupBy(entry => new { entry.Opcode, entry.RequestType })
                    .Take(6)
                    .Select(group =>
                    {
                        OutboundPacketTrace entry = group.First();
                        return $"opcode {entry.Opcode} type {entry.RequestType} payload={entry.PayloadHex} raw={entry.RawPacketHex}";
                    }));
        }

        private string DescribeObservedInboundSamples()
        {
            if (_recentInboundPackets.Count == 0)
            {
                return "none";
            }

            return string.Join(
                "; ",
                _recentInboundPackets
                    .Reverse()
                    .GroupBy(entry => new { entry.Opcode, entry.ResultType })
                    .Take(6)
                    .Select(group =>
                    {
                        InboundPacketTrace entry = group.First();
                        return $"opcode {entry.Opcode} type {entry.ResultType} payload={entry.PayloadHex} raw={entry.RawPacketHex}";
                    }));
        }

        private static int FindMismatchedExpectationIndex(IReadOnlyList<PendingResultExpectation> expectations, int inboundOpcode)
        {
            for (int i = 0; i < expectations.Count; i++)
            {
                PendingResultExpectation expectation = expectations[i];
                if ((expectation.ExpectedInboundOpcodes?.Length ?? 0) > 0
                    && expectation.ExpectedInboundOpcodes.Contains(inboundOpcode))
                {
                    return i;
                }
            }

            return -1;
        }

        private string DescribeInboundOpcodeSet(ushort primaryOpcode)
        {
            if (_additionalInboundOpcodes.Count == 0)
            {
                return primaryOpcode.ToString();
            }

            return string.Join(",", new[] { primaryOpcode }.Concat(_additionalInboundOpcodes).Distinct().OrderBy(opcode => opcode));
        }

        private void ResetInboundState()
        {
            _observedInboundOpcodes.Clear();
            _observedMessengerInboundSubtypes.Clear();
            _observedMapleTvSendResultCodes.Clear();
            _passiveInboundOpcodeHitCounts.Clear();
            _passiveInboundSubtypeObservations.Clear();
            _passiveMapleTvResultCodeObservations.Clear();
            _passiveInboundOpcodeSampleRawHex.Clear();
            _observedMessengerSubtypePayloadSamples.Clear();
            _observedMapleTvResultCodePayloadSamples.Clear();
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

            string normalized = selector.Trim();
            owningProcessName = normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^4]
                : normalized;
            return owningProcessName.Length > 0;
        }

        private static bool TryResolveDiscoveryCandidate(
            System.Collections.Generic.IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            string ownerName,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            var filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"{DescribeDiscoveryOwner(ownerName)} official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(match =>
                    $"{match.ProcessName}({match.ProcessId}) local {match.LocalEndpoint.Port} -> remote {match.RemoteEndpoint.Port}"));
                status = $"{DescribeDiscoveryOwner(ownerName)} official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Add a localPort filter.";
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

        private static string DescribeDiscoveryOwner(string ownerName)
        {
            return string.IsNullOrWhiteSpace(ownerName) ? "Messenger" : ownerName.Trim();
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string processScope = owningProcessId.HasValue
                ? $"process {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName)
                    ? DefaultProcessName
                    : owningProcessName;
            string localScope = localPort.HasValue ? $" and local port {localPort.Value}" : string.Empty;
            return $"{processScope} on remote port {remotePort}{localScope}";
        }
    }
}
