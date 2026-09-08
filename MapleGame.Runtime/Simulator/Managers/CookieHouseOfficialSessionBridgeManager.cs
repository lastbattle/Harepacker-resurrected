using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Cookie House transport bridge that proxies a live Maple session
    /// and feeds a configured inbound point opcode into the existing context-owned
    /// Cookie House score seam.
    /// </summary>
    public sealed class CookieHouseOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18496;
        public const ushort DefaultInboundSessionValueOpcode = 93;
        private const string DefaultProcessName = "MapleStory";
        internal const int ClientSessionPointKeyStringPoolId = 0x11D9;
        private const int RecentPacketCapacity = 8;
        private const int InferencePacketCapacity = 24;
        private const int MinimumInferenceObservations = 2;
        private const int MinimumInferenceDistinctPointValues = 2;

        private readonly ConcurrentQueue<CookieHousePointInboxMessage> _pendingMessages = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly Queue<byte[]> _recentInferencePackets = new();
        private readonly HashSet<int> _mappedInboundPointOpcodes = new();
        private readonly HashSet<int> _inferredInboundPointOpcodes = new();
        private readonly HashSet<int> _suppressedDefaultInboundPointOpcodes = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;
        public CookieHouseOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateChannel()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Cookie House official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session" : "no live Maple session";
            return $"Cookie House official-session bridge {lifecycle}; {session}; received={ReceivedCount}; mapped point opcodes={DescribeMappedInboundPointOpcodes()}; inference={DescribeInferenceStatus()}; recent={DescribeRecentPackets()}. {LastStatus}";
        }

        public string DescribeMappedInboundPointOpcodes()
        {
            lock (_sync)
            {
                return DescribeMappedInboundPointOpcodesNoLock();
            }
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

        public string DescribeInferenceStatus()
        {
            lock (_sync)
            {
                return DescribeInferenceStatusNoLock();
            }
        }

        public void SetMappedInboundPointOpcodes(IEnumerable<int> opcodes)
        {
            lock (_sync)
            {
                _mappedInboundPointOpcodes.Clear();
                _inferredInboundPointOpcodes.Clear();
                _suppressedDefaultInboundPointOpcodes.Clear();
                foreach (int opcode in opcodes ?? Enumerable.Empty<int>())
                {
                    if (opcode > 0 && opcode <= ushort.MaxValue)
                    {
                        _mappedInboundPointOpcodes.Add(opcode);
                    }
                }

                LastStatus = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
            }
        }

        public bool TryAddMappedInboundPointOpcode(int opcode, out string status)
        {
            lock (_sync)
            {
                if (opcode <= 0 || opcode > ushort.MaxValue)
                {
                    status = $"Cookie House official-session bridge requires a positive 16-bit inbound opcode, got {opcode}.";
                    LastStatus = status;
                    return false;
                }

                _suppressedDefaultInboundPointOpcodes.Remove(opcode);
                if (opcode == DefaultInboundSessionValueOpcode)
                {
                    status = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
                    LastStatus = status;
                    return true;
                }

                if (!_mappedInboundPointOpcodes.Add(opcode))
                {
                    status = $"Cookie House official-session bridge already maps inbound point opcode {opcode}/0x{opcode:X}.";
                    LastStatus = status;
                    return true;
                }

                _inferredInboundPointOpcodes.Remove(opcode);
                status = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
                LastStatus = status;
                return true;
            }
        }

        public bool TryRemoveMappedInboundPointOpcode(int opcode, out string status)
        {
            lock (_sync)
            {
                if (opcode <= 0 || opcode > ushort.MaxValue)
                {
                    status = $"Cookie House official-session bridge requires a positive 16-bit inbound opcode, got {opcode}.";
                    LastStatus = status;
                    return false;
                }

                if (opcode == DefaultInboundSessionValueOpcode)
                {
                    _suppressedDefaultInboundPointOpcodes.Add(opcode);
                    _mappedInboundPointOpcodes.Remove(opcode);
                    _inferredInboundPointOpcodes.Remove(opcode);
                    status = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
                    LastStatus = status;
                    return true;
                }

                if (!_mappedInboundPointOpcodes.Remove(opcode))
                {
                    status = $"Cookie House official-session bridge has no mapped inbound point opcode {opcode}/0x{opcode:X}.";
                    LastStatus = status;
                    return false;
                }

                _inferredInboundPointOpcodes.Remove(opcode);
                status = $"Cookie House official-session bridge mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
                LastStatus = status;
                return true;
            }
        }

        public void ClearMappedInboundPointOpcodes()
        {
            lock (_sync)
            {
                _mappedInboundPointOpcodes.Clear();
                _inferredInboundPointOpcodes.Clear();
                _suppressedDefaultInboundPointOpcodes.Add(DefaultInboundSessionValueOpcode);
                LastStatus = "Cookie House official-session bridge cleared mapped point opcodes.";
            }
        }

        public void ClearInference()
        {
            lock (_sync)
            {
                foreach (int opcode in _inferredInboundPointOpcodes)
                {
                    _mappedInboundPointOpcodes.Remove(opcode);
                }

                _inferredInboundPointOpcodes.Clear();
                _recentInferencePackets.Clear();
                LastStatus = "Cookie House official-session bridge cleared inferred point-opcode candidates.";
            }
        }

        public bool TryPromoteInferredInboundPointOpcode(out string status)
        {
            lock (_sync)
            {
                if (TryPromoteInferredInboundPointOpcodeNoLock(out int opcode, out status))
                {
                    status = $"Cookie House official-session bridge inferred inbound point opcode {opcode}/0x{opcode:X}. mapped point opcodes: {DescribeMappedInboundPointOpcodesNoLock()}.";
                    LastStatus = status;
                    return true;
                }

                LastStatus = status;
                return false;
            }
        }

        internal bool TryObserveInboundPacketForInference(
            byte[] rawPacket,
            out int opcode,
            out bool mapped,
            out string status)
        {
            opcode = 0;
            mapped = false;
            status = null;

            if (!TryDecodeOpcode(rawPacket, out opcode, out byte[] payload))
            {
                status = "Cookie House live packet must contain a 2-byte opcode prefix.";
                LastStatus = status;
                return false;
            }

            lock (_sync)
            {
                mapped = IsMappedInboundPointOpcodeNoLock(opcode);
                if (!mapped)
                {
                    RecordInferencePacketNoLock(rawPacket);
                    if (TryPromoteInferredInboundPointOpcodeNoLock(out int inferredOpcode, out string inferenceStatus))
                    {
                        mapped = IsMappedInboundPointOpcodeNoLock(opcode);
                        LastStatus = $"Cookie House official-session bridge inferred inbound point opcode {inferredOpcode}/0x{inferredOpcode:X}. {inferenceStatus}";
                    }
                    else if (!string.IsNullOrWhiteSpace(inferenceStatus))
                    {
                        LastStatus = inferenceStatus;
                    }
                }

                RecordRecentPacketNoLock(opcode, payload.Length, mapped, rawPacket);
                status = LastStatus;
                return true;
            }
        }

        public void ClearRecentPackets()
        {
            lock (_sync)
            {
                _recentPackets.Clear();
                LastStatus = "Cookie House official-session bridge cleared recent packet history.";
            }
        }

        public bool TryStart(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                int resolvedListenPort = listenPort < 0 ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                    {
                        status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Cookie House official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    ListenPort = resolvedListenPort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
                        LastStatus = proxyStatus;
                        status = LastStatus;
                        return false;
                    }

                    LastStatus = $"Cookie House official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}. {proxyStatus}";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Cookie House official-session bridge failed to start: {ex.Message}";
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

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!CoconutOfficialSessionBridgeManager.TryResolveDiscoveryCandidate(candidates, remotePort, owningProcessId, owningProcessName, localPort, out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate, out status))
            {
                LastStatus = status;
                return false;
            }

            int resolvedListenPort = listenPort < 0 ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
                {
                    status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Cookie House official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (IsRunning
                && MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
            {
                status = $"Cookie House official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Cookie House official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Cookie House official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
            LastStatus = status;
            return true;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                return selectorError;
            }

            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates =
                CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return CoconutOfficialSessionBridgeManager.DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Cookie House official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out CookieHousePointInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string detail)
        {
            string summary = string.IsNullOrWhiteSpace(detail) ? "point update" : detail;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public static bool TryBuildInboundPointMessageFromRawPacket(
            byte[] rawPacket,
            int expectedOpcode,
            string source,
            out CookieHousePointInboxMessage message,
            out string error)
        {
            HashSet<int> mappedOpcodes = new() { expectedOpcode };
            return TryBuildInboundPointMessageFromRawPacket(rawPacket, mappedOpcodes, source, out message, out _, out error);
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
if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                _recentInferencePackets.Clear();
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

            if (!TryObserveInboundPacketForInference(e.RawPacket, out int opcode, out bool mapped, out _))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (!mapped)
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            HashSet<int> resolvedMappedOpcodes;
            lock (_sync)
            {
                resolvedMappedOpcodes = BuildResolvedMappedOpcodeSetNoLock();
            }

            if (!TryBuildInboundPointMessageFromRawPacket(e.RawPacket, resolvedMappedOpcodes, $"official-session:{e.SourceEndpoint}", out CookieHousePointInboxMessage message, out int resolvedOpcode, out string error))
            {
                LastStatus = $"Ignored Cookie House live packet opcode {opcode}: {error}";
                return;
            }

            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LastStatus = $"Queued Cookie House point {message.Point} from live session {e.SourceEndpoint} via opcode {resolvedOpcode}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            LastStatus = _roleSessionProxy.LastStatus;
        }

        internal static bool TryBuildInboundPointMessageFromRawPacket(
            byte[] rawPacket,
            ISet<int> mappedOpcodes,
            string source,
            out CookieHousePointInboxMessage message,
            out int opcode,
            out string error)
        {
            message = null;
            error = null;
            opcode = 0;

            if (!TryDecodeOpcode(rawPacket, out opcode, out byte[] payload))
            {
                error = "Cookie House live packet must contain a 2-byte opcode prefix.";
                return false;
            }

            if (mappedOpcodes == null || mappedOpcodes.Count == 0)
            {
                error = "Cookie House live packet bridge has no mapped inbound point opcode.";
                return false;
            }

            if (!mappedOpcodes.Contains(opcode))
            {
                error = $"opcode {opcode} is not mapped as a Cookie House point carrier.";
                return false;
            }

            if (opcode == DefaultInboundSessionValueOpcode)
            {
                if (!TryDecodeSessionValuePointPayload(payload, out int sessionPoint, out string sessionKey, out error))
                {
                    return false;
                }

                message = new CookieHousePointInboxMessage(
                    sessionPoint,
                    string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                    $"packetclientraw {Convert.ToHexString(rawPacket)}",
                    CookieHousePointInboxPayloadKind.OpcodeFramedSessionValuePoint);
                error = $"decoded opcode {opcode} Cookie House session value key {sessionKey}";
                return true;
            }

            if (payload.Length != CookieHousePointInboxManager.ClientContextPointByteLength)
            {
                error = $"Cookie House live opcode {opcode} payload must be exactly {CookieHousePointInboxManager.ClientContextPointByteLength} bytes for CWvsContext+0x{CookieHousePointInboxManager.ClientContextPointOffset:X}.";
                return false;
            }

            if (!CookieHousePointInboxManager.TryDecodeClientContextPoint(payload, out int point, out error))
            {
                return false;
            }

            message = new CookieHousePointInboxMessage(
                point,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetclientraw {Convert.ToHexString(rawPacket)}",
                CookieHousePointInboxPayloadKind.OpcodeFramedRawContextPoint);
            return true;
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

        private void RecordRecentPacketNoLock(int opcode, int payloadLength, bool mapped, byte[] rawPacket)
        {
            string rawHex = rawPacket == null || rawPacket.Length == 0 ? string.Empty : Convert.ToHexString(rawPacket);
            if (rawHex.Length > 48)
            {
                rawHex = rawHex[..48] + "...";
            }

            string mappingLabel = mapped
                ? _inferredInboundPointOpcodes.Contains(opcode) ? "mapped-inferred" : "mapped"
                : "seen";
            _recentPackets.Enqueue($"{opcode}/0x{opcode:X} len={payloadLength} {mappingLabel} raw={rawHex}");
            while (_recentPackets.Count > RecentPacketCapacity)
            {
                _recentPackets.Dequeue();
            }
        }

        private string DescribeMappedInboundPointOpcodesNoLock()
        {
            if (_mappedInboundPointOpcodes.Count == 0)
            {
                if (_suppressedDefaultInboundPointOpcodes.Contains(DefaultInboundSessionValueOpcode))
                {
                    return "none";
                }

                return $"{DefaultInboundSessionValueOpcode}/0x{DefaultInboundSessionValueOpcode:X} (default session value)";
            }

            IEnumerable<int> resolvedOpcodes = BuildResolvedMappedOpcodeSetNoLock().OrderBy(opcode => opcode);
            return string.Join(", ", resolvedOpcodes.Select(opcode =>
            {
                if (_inferredInboundPointOpcodes.Contains(opcode))
                {
                    return $"{opcode}/0x{opcode:X} (inferred)";
                }

                if (opcode == DefaultInboundSessionValueOpcode)
                {
                    return $"{opcode}/0x{opcode:X} (default session value)";
                }

                return $"{opcode}/0x{opcode:X}";
            }));
        }

        private bool IsMappedInboundPointOpcodeNoLock(int opcode)
        {
            return BuildResolvedMappedOpcodeSetNoLock().Contains(opcode);
        }

        private HashSet<int> BuildResolvedMappedOpcodeSetNoLock()
        {
            HashSet<int> resolved = new(_mappedInboundPointOpcodes);
            if (!_suppressedDefaultInboundPointOpcodes.Contains(DefaultInboundSessionValueOpcode))
            {
                resolved.Add(DefaultInboundSessionValueOpcode);
            }

            return resolved;
        }

        private string DescribeInferenceStatusNoLock()
        {
            List<InboundPointOpcodeCandidateSummary> candidates = SummarizeInferenceCandidates(_recentInferencePackets, _mappedInboundPointOpcodes);
            if (_inferredInboundPointOpcodes.Count > 0)
            {
                string inferred = string.Join(", ", _inferredInboundPointOpcodes
                    .OrderBy(opcode => opcode)
                    .Select(opcode => $"{opcode}/0x{opcode:X}"));
                return candidates.Count == 0
                    ? $"auto-mapped {inferred}"
                    : $"auto-mapped {inferred}; candidates={FormatCandidateList(candidates)}";
            }

            return candidates.Count == 0
                ? "no eligible 4-byte point candidates observed"
                : $"candidates={FormatCandidateList(candidates)}";
        }

        private void RecordInferencePacketNoLock(byte[] rawPacket)
        {
            if (rawPacket == null || rawPacket.Length < CookieHousePointInboxManager.ClientOpcodeFramedPointByteLength)
            {
                return;
            }

            if (TryDecodeOpcode(rawPacket, out int opcode, out _)
                && opcode == DefaultInboundSessionValueOpcode)
            {
                return;
            }

            _recentInferencePackets.Enqueue((byte[])rawPacket.Clone());
            while (_recentInferencePackets.Count > InferencePacketCapacity)
            {
                _recentInferencePackets.Dequeue();
            }
        }

        private bool TryPromoteInferredInboundPointOpcodeNoLock(out int opcode, out string status)
        {
            if (!TryInferBestInboundPointOpcode(_recentInferencePackets, _mappedInboundPointOpcodes, out opcode, out status))
            {
                return false;
            }

            if (_mappedInboundPointOpcodes.Add(opcode))
            {
                _inferredInboundPointOpcodes.Add(opcode);
            }

            return true;
        }

        internal static bool TryInferBestInboundPointOpcode(
            IEnumerable<byte[]> rawPackets,
            ISet<int> mappedOpcodes,
            out int opcode,
            out string status)
        {
            opcode = 0;
            List<InboundPointOpcodeCandidateSummary> candidates = SummarizeInferenceCandidates(rawPackets, mappedOpcodes);
            List<InboundPointOpcodeCandidateSummary> eligibleCandidates = candidates
                .Where(candidate =>
                    candidate.ObservationCount >= MinimumInferenceObservations
                    && candidate.DistinctPointValueCount >= MinimumInferenceDistinctPointValues)
                .OrderByDescending(candidate => candidate.ObservationCount)
                .ThenByDescending(candidate => candidate.DistinctPointValueCount)
                .ThenByDescending(candidate => candidate.TransitionCount)
                .ThenByDescending(candidate => candidate.GradeBucketCount)
                .ThenByDescending(candidate => candidate.MaximumPoint - candidate.MinimumPoint)
                .ThenBy(candidate => candidate.Opcode)
                .ToList();

            if (eligibleCandidates.Count == 0)
            {
                status = candidates.Count == 0
                    ? "Cookie House official-session bridge has not observed any eligible 4-byte point candidates yet."
                    : $"Cookie House official-session bridge is still observing point-opcode candidates: {FormatCandidateList(candidates)}.";
                return false;
            }

            InboundPointOpcodeCandidateSummary bestCandidate = eligibleCandidates[0];
            List<InboundPointOpcodeCandidateSummary> topRankedCandidates = eligibleCandidates
                .Where(candidate => HaveEquivalentInferenceRanking(candidate, bestCandidate))
                .ToList();

            if (topRankedCandidates.Count > 1)
            {
                status = $"Cookie House official-session bridge observed multiple equally ranked point-opcode candidates and will not auto-map ambiguously: {FormatCandidateList(topRankedCandidates)}.";
                return false;
            }

            opcode = bestCandidate.Opcode;
            status = $"Cookie House official-session bridge promoted the best-ranked point-opcode candidate after {bestCandidate.ObservationCount} observations, {bestCandidate.DistinctPointValueCount} distinct point values, and {bestCandidate.TransitionCount} score transitions.";
            return true;
        }

        private static List<InboundPointOpcodeCandidateSummary> SummarizeInferenceCandidates(IEnumerable<byte[]> rawPackets, ISet<int> mappedOpcodes)
        {
            Dictionary<int, InboundPointOpcodeCandidateAccumulator> candidates = new();
            foreach (byte[] rawPacket in rawPackets ?? Enumerable.Empty<byte[]>())
            {
                if (!TryDecodeOpcode(rawPacket, out int opcode, out byte[] payload)
                    || payload.Length != CookieHousePointInboxManager.ClientContextPointByteLength
                    || (mappedOpcodes?.Contains(opcode) ?? false))
                {
                    continue;
                }

                if (!CookieHousePointInboxManager.TryDecodeClientContextPoint(payload, out int point, out _)
                    || point > CookieHousePointInboxManager.ClientMaximumDisplayPoint)
                {
                    continue;
                }

                if (!candidates.TryGetValue(opcode, out InboundPointOpcodeCandidateAccumulator candidate))
                {
                    candidate = new InboundPointOpcodeCandidateAccumulator(opcode);
                    candidates.Add(opcode, candidate);
                }

                candidate.AddPoint(point);
            }

            return candidates.Values
                .OrderByDescending(candidate => candidate.ObservationCount)
                .ThenByDescending(candidate => candidate.DistinctPointValueCount)
                .ThenByDescending(candidate => candidate.TransitionCount)
                .ThenBy(candidate => candidate.Opcode)
                .Select(candidate => candidate.ToSummary())
                .ToList();
        }

        private static string FormatCandidateList(IEnumerable<InboundPointOpcodeCandidateSummary> candidates)
        {
            return string.Join(", ", candidates.Select(candidate =>
                $"{candidate.Opcode}/0x{candidate.Opcode:X} obs={candidate.ObservationCount} distinct={candidate.DistinctPointValueCount} changes={candidate.TransitionCount} grades={candidate.GradeBucketCount} range={candidate.MinimumPoint}-{candidate.MaximumPoint} lastPoint={candidate.LastPoint}"));
        }

        private static bool HaveEquivalentInferenceRanking(
            InboundPointOpcodeCandidateSummary left,
            InboundPointOpcodeCandidateSummary right)
        {
            return left.ObservationCount == right.ObservationCount
                && left.DistinctPointValueCount == right.DistinctPointValueCount
                && left.TransitionCount == right.TransitionCount
                && left.GradeBucketCount == right.GradeBucketCount
                && (left.MaximumPoint - left.MinimumPoint) == (right.MaximumPoint - right.MinimumPoint);
        }

        internal static bool TryDecodeSessionValuePointPayload(
            byte[] payload,
            out int point,
            out string sessionKey,
            out string error)
        {
            point = 0;
            sessionKey = string.Empty;
            error = null;

            try
            {
                PacketReader reader = new(payload ?? Array.Empty<byte>());
                string key = reader.ReadMapleString();
                string value = reader.ReadMapleString();
                if (reader.Remaining != 0)
                {
                    error = "Cookie House opcode 93 payload had trailing bytes after the session value pair.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Cookie House opcode 93 session key was empty.";
                    return false;
                }

                if (!TryDecodeSessionValuePoint(key, value, out point, out error))
                {
                    return false;
                }

                sessionKey = key;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is EndOfStreamException || ex is IOException)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static bool TryDecodeSessionValuePoint(
            string key,
            string value,
            out int point,
            out string error)
        {
            point = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Cookie House session value key was empty.";
                return false;
            }

            bool hasRecoveredClientKey = MapleStoryStringPool.TryGet(ClientSessionPointKeyStringPoolId, out string recoveredClientKey)
                && IsPlausibleSessionValueKey(recoveredClientKey);
            if (hasRecoveredClientKey
                && !string.Equals(key, recoveredClientKey, StringComparison.Ordinal))
            {
                error = $"Cookie House session value key {key} did not match the recovered client key {recoveredClientKey}.";
                return false;
            }

            if (!hasRecoveredClientKey && !IsPlausibleSessionValueKey(key))
            {
                error = $"Cookie House session value key {key} did not resemble a client session-value token.";
                return false;
            }

            if (!int.TryParse(value, out int parsedPoint))
            {
                error = $"Cookie House session value payload {value} was not a signed integer.";
                return false;
            }

            return CookieHousePointInboxManager.TryValidateClientPoint(parsedPoint, out point, out error)
                && point <= CookieHousePointInboxManager.ClientMaximumDisplayPoint
                ? true
                : FailSessionValuePointRange(out error, point);
        }

        private static bool IsPlausibleSessionValueKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.IndexOfAny(new[] { '/', '\\', '%' }) < 0
                && key.IndexOf(".img", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool FailSessionValuePointRange(out string error, int point)
        {
            error = $"Cookie House session value point {point} exceeds the client-owned three-digit display range.";
            return false;
        }

        private sealed class InboundPointOpcodeCandidateAccumulator
        {
            private readonly HashSet<int> _distinctPoints = new();
            private bool _hasLastObservedPoint;

            public InboundPointOpcodeCandidateAccumulator(int opcode)
            {
                Opcode = opcode;
            }

            public int Opcode { get; }
            public int ObservationCount { get; private set; }
            public int DistinctPointValueCount => _distinctPoints.Count;
            public int TransitionCount { get; private set; }
            public int GradeBucketCount => _gradeBuckets.Count;
            public int MinimumPoint { get; private set; } = int.MaxValue;
            public int MaximumPoint { get; private set; } = int.MinValue;
            public int LastPoint { get; private set; }
            private readonly HashSet<int> _gradeBuckets = new();

            public void AddPoint(int point)
            {
                ObservationCount++;
                if (_hasLastObservedPoint && LastPoint != point)
                {
                    TransitionCount++;
                }

                LastPoint = point;
                _hasLastObservedPoint = true;
                _distinctPoints.Add(point);
                _gradeBuckets.Add(CookieHouseField.FindGradeForTesting(point));
                MinimumPoint = Math.Min(MinimumPoint, point);
                MaximumPoint = Math.Max(MaximumPoint, point);
            }

            public InboundPointOpcodeCandidateSummary ToSummary()
            {
                return new InboundPointOpcodeCandidateSummary(
                    Opcode,
                    ObservationCount,
                    DistinctPointValueCount,
                    TransitionCount,
                    GradeBucketCount,
                    MinimumPoint == int.MaxValue ? 0 : MinimumPoint,
                    MaximumPoint == int.MinValue ? 0 : MaximumPoint,
                    LastPoint);
            }
        }

        private readonly record struct InboundPointOpcodeCandidateSummary(
            int Opcode,
            int ObservationCount,
            int DistinctPointValueCount,
            int TransitionCount,
            int GradeBucketCount,
            int MinimumPoint,
            int MaximumPoint,
            int LastPoint);
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
                error = "Cookie House official-session discovery requires a process name or pid when a selector is provided.";
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
            return string.IsNullOrWhiteSpace(remoteHost)
                ? IPAddress.Loopback.ToString()
                : remoteHost.Trim();
        }

        private static bool MatchesTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            string requestedRemoteHost,
            int requestedRemotePort)
        {
            return (requestedListenPort == 0 || currentListenPort == requestedListenPort)
                && currentRemotePort == requestedRemotePort
                && string.Equals(
                    NormalizeRemoteHost(currentRemoteHost),
                    NormalizeRemoteHost(requestedRemoteHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesDiscoveredTargetConfiguration(
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int requestedListenPort,
            IPEndPoint discoveredRemoteEndpoint)
        {
            if (discoveredRemoteEndpoint == null)
            {
                return false;
            }

            return MatchesTargetConfiguration(
                currentListenPort,
                currentRemoteHost,
                currentRemotePort,
                requestedListenPort,
                discoveredRemoteEndpoint.Address.ToString(),
                discoveredRemoteEndpoint.Port);
        }
    }
}
