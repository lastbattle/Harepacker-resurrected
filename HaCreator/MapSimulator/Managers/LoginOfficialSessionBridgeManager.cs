using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public readonly record struct LoginNewCharacterRequest(
        string CharacterName,
        int Race,
        short SubJob,
        byte Gender,
        int FaceId,
        int HairStyleId,
        int SkinValue,
        int HairColorValue,
        int CoatId,
        int PantsId,
        int ShoesId,
        int WeaponId,
        bool IsCharSale = false,
        int CharSaleJob = 0,
        IReadOnlyList<int> ExtraSaleAvatarValues = null);

    /// <summary>
    /// Built-in login bridge that proxies a live Maple login session and mirrors
    /// inbound login packets into the existing login packet inbox seam.
    /// </summary>
    public sealed class LoginOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18486;
        public const short OutboundCheckDuplicateIdOpcode = 21;
        public const short OutboundNewCharacterOpcode = 22;
        public const short OutboundNewCharacterSaleOpcode = 23;
        public const short OutboundDeleteCharacterOpcode = 24;
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<LoginPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<int, LoginPacketType> _opcodeMappings = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly object _sync = new();

        private readonly MapleRoleSessionProxy _roleSessionProxy;

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public int SentCount => _roleSessionProxy.SentCount;
        public string LastStatus { get; private set; } = "Login official-session bridge inactive.";

        public LoginOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _roleSessionProxy = (roleSessionProxyFactory ?? (() => MapleRoleSessionProxyFactory.GlobalV95.CreateLogin()))();
            _roleSessionProxy.ServerPacketReceived += OnRoleSessionServerPacketReceived;
            _roleSessionProxy.ClientPacketReceived += OnRoleSessionClientPacketReceived;
        }

        public bool TryConfigurePacketMapping(int opcode, LoginPacketType packetType, out string status)
        {
            if (opcode <= 0)
            {
                status = "Login opcode mappings require a positive opcode.";
                return false;
            }

            _opcodeMappings[opcode] = packetType;
            status = $"Mapped login opcode {opcode} to {packetType}.";
            LastStatus = status;
            return true;
        }

        public bool RemovePacketMapping(int opcode, out string status)
        {
            if (_opcodeMappings.TryRemove(opcode, out LoginPacketType packetType))
            {
                status = $"Removed login opcode {opcode} mapping for {packetType}.";
                LastStatus = status;
                return true;
            }

            status = $"Login opcode {opcode} is not currently mapped.";
            return false;
        }

        public void ClearPacketMappings()
        {
            _opcodeMappings.Clear();
            LastStatus = "Cleared login official-session opcode mappings.";
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
                    .Select(entry => $"{entry.Key}->{entry.Value}"));
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

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);

                ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                RemotePort = remotePort;

                if (!_roleSessionProxy.Start(ListenPort, RemoteHost, RemotePort, out string status))
                {
                    StopInternal(clearPending: true);
                    LastStatus = status;
                    return;
                }

                LastStatus = status;
            }
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            return TryRefreshFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            if (HasAttachedClient)
            {
                status = $"Login official-session bridge is already attached to {RemoteHost}:{RemotePort}; keeping the current live Maple session.";
                LastStatus = status;
                return true;
            }

            int? owningProcessId = null;
            string owningProcessName = null;
            if (!TryResolveProcessSelector(processSelector, out owningProcessId, out owningProcessName, out string selectorError))
            {
                status = selectorError;
                LastStatus = status;
                return false;
            }

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            if (!TryResolveDiscoveryCandidate(
                    candidates,
                    remotePort,
                    owningProcessId,
                    owningProcessName,
                    localPort,
                    out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
                    out status))
            {
                LastStatus = status;
                return false;
            }

            if (MatchesDiscoveredTargetConfiguration(
                    IsRunning,
                    ListenPort,
                    RemoteHost,
                    RemotePort,
                    listenPort <= 0 ? DefaultListenPort : listenPort,
                    candidate.RemoteEndpoint))
            {
                status = $"Login official-session bridge remains armed for {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}.";
                LastStatus = status;
                return true;
            }

            Start(listenPort, candidate.RemoteEndpoint.Address.ToString(), candidate.RemoteEndpoint.Port);
            status = $"Login official-session bridge discovered {candidate.ProcessName} ({candidate.ProcessId}) at {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} from local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}. {LastStatus}";
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

            var candidates = CoconutOfficialSessionBridgeManager.DiscoverEstablishedSessions(remotePort, owningProcessId, owningProcessName);
            return DescribeDiscoveryCandidates(candidates, remotePort, owningProcessId, owningProcessName, localPort);
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Login official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out LoginPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TryMapInboundPacket(byte[] rawPacket, string source, out LoginPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            int opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (_opcodeMappings.TryGetValue(opcode, out LoginPacketType mappedPacketType))
            {
                byte[] payloadBytes = rawPacket.Length > sizeof(ushort)
                    ? rawPacket[sizeof(ushort)..]
                    : Array.Empty<byte>();
                string[] arguments = new[] { $"payloadhex={Convert.ToHexString(payloadBytes)}" };
                message = new LoginPacketInboxMessage(
                    mappedPacketType,
                    string.IsNullOrWhiteSpace(source) ? "official-session" : source,
                    $"{mappedPacketType} payloadhex={Convert.ToHexString(payloadBytes)}",
                    arguments);
                RecordRecentPacket(opcode, rawPacket, mappedPacketType, "configured");
                LastStatus = $"Queued login packet {mappedPacketType} from live session opcode {opcode}.";
                return true;
            }

            if (!LoginPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out LoginPacketType packetType, out string[] fallbackArguments))
            {
                RecordRecentPacket(opcode, rawPacket, packetType: null, "unmapped");
                LastStatus = $"Ignored unmapped login opcode {opcode}; configure /loginpacket session map <opcode> <packet> to route it.";
                return false;
            }

            message = new LoginPacketInboxMessage(
                packetType,
                string.IsNullOrWhiteSpace(source) ? "official-session" : source,
                $"{packetType} payloadhex={Convert.ToHexString(rawPacket.Length > sizeof(ushort) ? rawPacket[sizeof(ushort)..] : Array.Empty<byte>())}",
                fallbackArguments);
            RecordRecentPacket(opcode, rawPacket, packetType, "direct");
            LastStatus = $"Queued login packet {packetType} from live session opcode {opcode}.";
            return true;
        }

        public bool TrySendCheckDuplicateIdRequest(string characterName, out string status)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                status = "Login official-session duplicate-name injection requires a character name.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildCheckDuplicateIdPacket(characterName),
                $"Injected login opcode {OutboundCheckDuplicateIdOpcode} for duplicate-name check '{characterName.Trim()}' into live session",
                out status);
        }

        public bool TrySendNewCharacterRequest(LoginNewCharacterRequest request, out string status)
        {
            if (string.IsNullOrWhiteSpace(request.CharacterName))
            {
                status = "Login official-session new-character injection requires a character name.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildNewCharacterPacket(request),
                $"Injected login opcode {(request.IsCharSale ? OutboundNewCharacterSaleOpcode : OutboundNewCharacterOpcode)} for new character '{request.CharacterName.Trim()}' into live session",
                out status);
        }

        public bool TrySendDeleteCharacterRequest(string secondaryPassword, int characterId, out string status)
        {
            if (characterId <= 0)
            {
                status = "Login official-session delete-character injection requires a valid character id.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildDeleteCharacterPacket(secondaryPassword, characterId),
                $"Injected login opcode {OutboundDeleteCharacterOpcode} for character {characterId} into live session",
                out status);
        }

        public static byte[] BuildCheckDuplicateIdPacket(string characterName)
        {
            PacketWriter writer = new();
            writer.WriteShort(OutboundCheckDuplicateIdOpcode);
            writer.WriteMapleString((characterName ?? string.Empty).Trim());
            return writer.ToArray();
        }

        public static byte[] BuildNewCharacterPacket(LoginNewCharacterRequest request)
        {
            PacketWriter writer = new();
            writer.WriteShort(request.IsCharSale ? OutboundNewCharacterSaleOpcode : OutboundNewCharacterOpcode);
            writer.WriteMapleString((request.CharacterName ?? string.Empty).Trim());
            writer.WriteInt(request.Race);

            if (request.IsCharSale)
            {
                writer.WriteInt(request.CharSaleJob);
                writer.WriteInt(request.FaceId);
                writer.WriteInt(request.HairStyleId);
                writer.WriteInt(request.SkinValue);
                writer.WriteInt(request.HairColorValue);
                writer.WriteInt(request.CoatId);
                writer.WriteInt(request.PantsId);
                writer.WriteInt(request.ShoesId);
                writer.WriteInt(request.WeaponId);

                IReadOnlyList<int> extraValues = request.ExtraSaleAvatarValues ?? Array.Empty<int>();
                for (int index = 0; index < extraValues.Count; index++)
                {
                    writer.WriteInt(extraValues[index]);
                }
            }
            else
            {
                writer.WriteShort(request.SubJob);
                writer.WriteInt(request.FaceId);
                writer.WriteInt(request.HairStyleId);
                writer.WriteInt(request.SkinValue);
                writer.WriteInt(request.HairColorValue);
                writer.WriteInt(request.CoatId);
                writer.WriteInt(request.PantsId);
                writer.WriteInt(request.ShoesId);
                writer.WriteInt(request.WeaponId);
                writer.WriteByte(request.Gender);
            }

            return writer.ToArray();
        }

        public static byte[] BuildDeleteCharacterPacket(string secondaryPassword, int characterId)
        {
            PacketWriter writer = new();
            writer.WriteShort(OutboundDeleteCharacterOpcode);
            writer.WriteMapleString((secondaryPassword ?? string.Empty).Trim());
            writer.WriteInt(characterId);
            return writer.ToArray();
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

            if (!TryMapInboundPacket(e.RawPacket, $"official-session:{e.SourceEndpoint}", out LoginPacketInboxMessage message))
            {
                LastStatus = _roleSessionProxy.LastStatus;
                return;
            }

            _pendingMessages.Enqueue(message);
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private void OnRoleSessionClientPacketReceived(object sender, MapleSessionPacketEventArgs e)
        {
            LastStatus = _roleSessionProxy.LastStatus;
        }

        private bool TrySendPacket(byte[] payload, string successPrefix, out string status)
        {
            if (!_roleSessionProxy.TrySendToServer(payload, out string proxyStatus))
            {
                status = proxyStatus;
                LastStatus = status;
                return false;
            }

            status = $"{successPrefix}. {proxyStatus}";
            LastStatus = status;
            return true;
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

        private void RecordRecentPacket(int opcode, byte[] rawPacket, LoginPacketType? packetType, string detail)
        {
            string summary = packetType.HasValue
                ? $"{opcode}->{packetType.Value}[{detail}]:{Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}"
                : $"{opcode}:{detail}:{Convert.ToHexString(rawPacket ?? Array.Empty<byte>())}";

            lock (_sync)
            {
                _recentPackets.Enqueue(summary);
                while (_recentPackets.Count > RecentPacketCapacity)
                {
                    _recentPackets.Dequeue();
                }
            }
        }

        private static bool TryResolveProcessSelector(
            string processSelector,
            out int? owningProcessId,
            out string owningProcessName,
            out string error)
        {
            owningProcessId = null;
            owningProcessName = null;
            error = null;

            if (string.IsNullOrWhiteSpace(processSelector))
            {
                return true;
            }

            string trimmed = processSelector.Trim();
            if (int.TryParse(trimmed, out int processId) && processId > 0)
            {
                owningProcessId = processId;
                return true;
            }

            try
            {
                Process[] matches = Process.GetProcessesByName(trimmed);
                if (matches.Length == 1)
                {
                    owningProcessId = matches[0].Id;
                    owningProcessName = matches[0].ProcessName;
                    return true;
                }
            }
            catch
            {
            }

            owningProcessName = trimmed;
            return true;
        }

        private static string DescribeSelector(int? owningProcessId, string owningProcessName)
        {
            if (owningProcessId.HasValue)
            {
                return $"pid {owningProcessId.Value}";
            }

            return string.IsNullOrWhiteSpace(owningProcessName)
                ? "any process"
                : owningProcessName;
        }

        internal static bool TryResolveDiscoveryCandidate(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort,
            out CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate candidate,
            out string status)
        {
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                status = $"Login official-session discovery found no established TCP session for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
                candidate = default;
                return false;
            }

            if (filteredCandidates.Count > 1)
            {
                string matches = string.Join(", ", filteredCandidates.Select(candidate =>
                    $"{candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port} via {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port}"));
                status = $"Login official-session discovery found multiple candidates for {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}: {matches}. Use /loginpacket session discover to inspect them, or add a localPort filter.";
                candidate = default;
                return false;
            }

            candidate = filteredCandidates[0];
            status = null;
            return true;
        }

        internal static string DescribeDiscoveryCandidates(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
            int remotePort,
            int? owningProcessId,
            string owningProcessName,
            int? localPort)
        {
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> filteredCandidates = FilterCandidatesByLocalPort(candidates, localPort);
            if (filteredCandidates.Count == 0)
            {
                return $"No established TCP sessions matched {DescribeDiscoveryScope(owningProcessId, owningProcessName, remotePort, localPort)}.";
            }

            return string.Join(
                Environment.NewLine,
                filteredCandidates.Select(candidate =>
                    $"{candidate.ProcessName} ({candidate.ProcessId}) local {candidate.LocalEndpoint.Address}:{candidate.LocalEndpoint.Port} -> remote {candidate.RemoteEndpoint.Address}:{candidate.RemoteEndpoint.Port}"));
        }

        private static IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> FilterCandidatesByLocalPort(
            IReadOnlyList<CoconutOfficialSessionBridgeManager.SessionDiscoveryCandidate> candidates,
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

        internal static bool MatchesDiscoveredTargetConfiguration(
            bool isRunning,
            int currentListenPort,
            string currentRemoteHost,
            int currentRemotePort,
            int expectedListenPort,
            IPEndPoint discoveredRemoteEndpoint)
        {
            if (!isRunning || discoveredRemoteEndpoint == null)
            {
                return false;
            }

            return currentListenPort == expectedListenPort
                && currentRemotePort == discoveredRemoteEndpoint.Port
                && string.Equals(
                    currentRemoteHost,
                    discoveredRemoteEndpoint.Address.ToString(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string DescribeDiscoveryScope(int? owningProcessId, string owningProcessName, int remotePort, int? localPort)
        {
            string selectorLabel = DescribeSelector(owningProcessId, owningProcessName);
            return localPort.HasValue
                ? $"{selectorLabel} on remote port {remotePort} and local port {localPort.Value}"
                : $"{selectorLabel} on remote port {remotePort}";
        }
    }
}
