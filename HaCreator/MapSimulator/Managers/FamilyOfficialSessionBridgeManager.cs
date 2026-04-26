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
    public sealed class FamilyOfficialSessionBridgeMessage
    {
        public FamilyOfficialSessionBridgeMessage(ushort opcode, byte[] payload, string source, string rawText)
        {
            Opcode = opcode;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
            RawText = rawText ?? string.Empty;
        }

        public ushort Opcode { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Proxies a live Maple session and peels family result opcodes into the
    /// existing family runtime packet seam.
    /// </summary>
    public sealed class FamilyOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18513;
        public const ushort FamilyLocalChartOpcode = 98;
        public const ushort FamilyInfoOpcode = 99;
        public const ushort FamilyResultOpcode = 100;
        public const ushort FamilyPrivilegeListOpcode = 104;
        public const ushort FamilySetPrivilegeOpcode = 107;
        private const string DefaultProcessName = "MapleStory";
        private const int AddressFamilyInet = 2;
        private const int ErrorInsufficientBuffer = 122;

        private readonly ConcurrentQueue<FamilyOfficialSessionBridgeMessage> _pendingMessages = new();
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
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public int ForwardedOutboundCount { get; private set; }
        public string LastStatus { get; private set; } = "Family official-session bridge inactive.";

        public FamilyOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
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
            return $"Family official-session bridge {lifecycle}; {session}; received={ReceivedCount}; forwardedOutbound={ForwardedOutboundCount}. {LastStatus}";
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

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string proxyStatus))
                    {
                        StopInternal(clearPending: true);
                        LastStatus = proxyStatus;
                        return;
                    }

                    LastStatus = proxyStatus;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Family official-session bridge failed to start: {ex.Message}";
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

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Family official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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
                LastStatus = "Family official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out FamilyOfficialSessionBridgeMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message) ? "family payload" : message;
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

            if (!TryDecodeOpcode(e.RawPacket, out int opcode, out byte[] payload)
                || !IsFamilyOpcode(opcode))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(new FamilyOfficialSessionBridgeMessage(
                (ushort)opcode,
                payload,
                $"official-session:{e.SourceEndpoint}",
                $"packetrecv {opcode} {Convert.ToHexString(payload)}"));
            LastStatus = $"Queued family opcode {opcode} from live session {e.SourceEndpoint}.";
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            if (e == null || e.IsInit)
            {
                return;
            }

            ForwardedOutboundCount++;
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

            ForwardedOutboundCount = 0;
        }

        private static bool IsFamilyOpcode(int opcode)
        {
            return opcode == FamilyLocalChartOpcode
                || opcode == FamilyInfoOpcode
                || opcode == FamilyResultOpcode
                || opcode == FamilyPrivilegeListOpcode
                || opcode == FamilySetPrivilegeOpcode;
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
                return $"No established Maple sessions found for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
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
