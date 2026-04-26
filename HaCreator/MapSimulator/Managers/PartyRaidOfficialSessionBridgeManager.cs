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
using HaCreator.MapSimulator.Fields;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Built-in Party Raid transport bridge that proxies a live Maple session
    /// and routes the recovered Party Raid opcode family into the existing raw-packet seam.
    /// </summary>
    public sealed class PartyRaidOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18496;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;

        private readonly ConcurrentQueue<PartyRaidPacketInboxMessage> _pendingMessages = new();
        private readonly object _sync = new();
        private readonly MapleRoleSessionProxy _roleSessionProxy;

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
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public string LastStatus { get; private set; } = "Party Raid official-session bridge inactive.";

        public PartyRaidOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
                : "no live Maple session";
            return $"Party Raid official-session bridge {lifecycle}; {session}; received={ReceivedCount}. {LastStatus}";
        }

        public static bool TryDecodePartyRaidOpcode(byte[] rawPacket, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(short))
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (!IsSupportedPartyRaidOpcode(opcode))
            {
                opcode = 0;
                return false;
            }

            payload = rawPacket.Skip(sizeof(short)).ToArray();
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
                int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                string resolvedRemoteHost = NormalizeRemoteHost(remoteHost);
                if (HasAttachedClient)
                {
                    if (MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                    {
                        status = $"Party Raid official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"Party Raid official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                    LastStatus = status;
                    return false;
                }

                if (IsRunning
                    && MatchesTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, resolvedRemoteHost, remotePort))
                {
                    status = $"Party Raid official-session bridge already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }

                StopInternal(clearPending: true);

                try
                {
                    ListenPort = resolvedListenPort;
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
                        LastStatus = proxyStatus;
                        status = LastStatus;
                        return false;
                    }

                    LastStatus = proxyStatus;
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Party Raid official-session bridge failed to start: {ex.Message}";
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

            int resolvedListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
            if (HasAttachedClient)
            {
                if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint))
                {
                    status = $"Party Raid official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                    LastStatus = status;
                    return true;
                }

                status = $"Party Raid official-session bridge is already attached to {RemoteHost}:{RemotePort}; stop it before starting a different proxy target.";
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(ListenPort, RemoteHost, RemotePort, resolvedListenPort, candidate.RemoteEndpoint) && IsRunning)
            {
                status = $"Party Raid official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            if (!TryStart(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port, out string startStatus))
            {
                status = $"Party Raid official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}, but startup failed. {startStatus}";
                LastStatus = status;
                return false;
            }

            status = $"Party Raid official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {startStatus}";
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

            IReadOnlyList<SessionDiscoveryCandidate> candidates = DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Party Raid official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out PartyRaidPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = $"Party Raid packet {packetType}";
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

        private void OnRoleSessionServerPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                return;
            }

            if (!TryDecodePartyRaidOpcode(e.RawPacket, out int opcode, out byte[] payload))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            if (opcode != SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(opcode, payload);
                opcode = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            }

            _pendingMessages.Enqueue(new PartyRaidPacketInboxMessage(opcode, payload, $"official-session:{e.SourceEndpoint}", $"packetraw {Convert.ToHexString(e.RawPacket)}"));
            LastStatus = $"Queued Party Raid opcode {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} relay from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void StopInternal(bool clearPending)
        {
            _roleSessionProxy.Stop(resetCounters: true);

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }

        private static bool IsSupportedPartyRaidOpcode(int opcode)
        {
            return opcode == PartyRaidField.ClientSessionValuePacketType
                || opcode == PartyRaidField.ClientPartyValuePacketType
                || opcode == PartyRaidField.ClientFieldSetVariablePacketType
                || opcode == 149
                || opcode == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
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

                int entryCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, sizeof(int));
                int rowSize = Marshal.SizeOf<TcpRowOwnerPid>();
                for (int i = 0; i < entryCount; i++)
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

        private static bool TryResolveProcess(int processId, out string processName)
        {
            processName = null;
            try
            {
                processName = Process.GetProcessById(processId).ProcessName;
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch
            {
                return false;
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
            return new IPAddress(BitConverter.GetBytes(address));
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
                error = "Party Raid official-session discovery requires a process name or pid when a selector is provided.";
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

        private static bool MatchesTargetConfiguration(int listenPort, string remoteHost, int remotePort, int candidateListenPort, string candidateRemoteHost, int candidateRemotePort)
        {
            return listenPort == candidateListenPort
                && string.Equals(remoteHost, candidateRemoteHost, StringComparison.OrdinalIgnoreCase)
                && remotePort == candidateRemotePort;
        }

        private static bool MatchesDiscoveredTargetConfiguration(int listenPort, string remoteHost, int remotePort, int candidateListenPort, IPEndPoint candidateRemoteEndpoint)
        {
            return candidateRemoteEndpoint != null
                && MatchesTargetConfiguration(listenPort, remoteHost, remotePort, candidateListenPort, candidateRemoteEndpoint.Address.ToString(), candidateRemoteEndpoint.Port);
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
                status = $"Party Raid official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Party Raid official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /partyraid session discover to inspect them, or add a localPort filter.";
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
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
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
            string selectorLabel = owningProcessId.HasValue
                ? $"pid {owningProcessId.Value}"
                : string.IsNullOrWhiteSpace(owningProcessName) ? "MapleStory" : owningProcessName;
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
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            TcpTableClass tblClass,
            int reserved);
    }
}
