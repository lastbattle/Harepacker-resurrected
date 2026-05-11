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

    public readonly record struct LoginCheckPasswordAuthMaterial(
        string NexonPassport,
        byte[] MachineId,
        int GameRoomClient,
        byte GameStartMode,
        int PartnerCode,
        string Source,
        DateTime CapturedAtUtc);

    public readonly record struct LoginSelectCharacterRequest(
        int CharacterId,
        byte LoginOpt,
        string SecondaryPassword = null,
        string MacAddress = null,
        string MacAddressWithHddSerial = null);

    public readonly record struct LoginSelectCharacterByVacRequest(
        int CharacterId,
        int WorldId,
        byte LoginOpt,
        string SecondaryPassword = null,
        string MacAddress = null,
        string MacAddressWithHddSerial = null);

    public readonly record struct LoginSelectWorldRequest(
        string NexonPassport,
        byte[] MachineId,
        int GameRoomClient,
        byte GameStartMode,
        int WorldId,
        int ChannelId,
        int LocalIpAddress = 0);

    public sealed class LoginSelectCharacterByVacRoundTripEvidence
    {
        public int? RequestOpcode { get; init; }
        public int? RequestCharacterId { get; init; }
        public int? RequestWorldId { get; init; }
        public string RequestPacketHex { get; init; }
        public DateTime? RequestSentAtUtc { get; init; }
        public string ResultSource { get; init; }
        public string ResultPacketHex { get; init; }
        public DateTime? ResultReceivedAtUtc { get; init; }
        public byte? ResultCode { get; init; }
        public byte? SecondaryCode { get; init; }
        public string EndpointText { get; init; }
        public int? ResultCharacterId { get; init; }
        public DateTime? FieldHandoffAtUtc { get; init; }
        public string FieldHandoffEndpointText { get; init; }

        public bool HasRequest => RequestOpcode.HasValue;
        public bool HasSuccessfulResult => ResultCode.HasValue && ResultCharacterId.HasValue;
        public bool HasFieldHandoff => FieldHandoffAtUtc.HasValue;
        public bool IsComplete => HasRequest && HasSuccessfulResult && HasFieldHandoff;
    }

    /// <summary>
    /// Built-in login bridge that proxies a live Maple login session and mirrors
    /// inbound login packets into the existing login packet inbox seam.
    /// </summary>
    public sealed class LoginOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18486;
        public const short OutboundCheckPasswordOpcode = 1;
        public const short OutboundSelectWorldOpcode = 5;
        public const short OutboundCheckUserLimitOpcode = 6;
        public const short OutboundReturnToTitleOpcode = 12;
        public const short OutboundViewAllCharacterOpcode = 13;
        public const short OutboundSelectCharacterByVacOpcode = 14;
        public const short OutboundSelectCharacterOpcode = 19;
        public const short OutboundCheckDuplicateIdOpcode = 21;
        public const short OutboundNewCharacterOpcode = 22;
        public const short OutboundNewCharacterSaleOpcode = 23;
        public const short OutboundDeleteCharacterOpcode = 24;
        public const short OutboundSelectCharacterLoginOpt0Opcode = 28;
        public const short OutboundSelectCharacterLoginOpt1Opcode = 29;
        public const short OutboundSelectCharacterByVacLoginOpt0Opcode = 30;
        public const short OutboundSelectCharacterByVacLoginOpt1Opcode = 31;
        public const int ClientMachineIdLength = 16;
        private const int RecentPacketCapacity = 8;

        private readonly ConcurrentQueue<LoginPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentDictionary<int, LoginPacketType> _opcodeMappings = new();
        private readonly Queue<string> _recentPackets = new();
        private readonly object _sync = new();

        private readonly MapleRoleSessionProxy _roleSessionProxy;
        private LoginCheckPasswordAuthMaterial? _capturedCheckPasswordAuth;
        private LoginSelectCharacterByVacRoundTripEvidence _selectCharacterByVacRoundTripEvidence = new();

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _roleSessionProxy.IsRunning;
        public bool HasAttachedClient => _roleSessionProxy.HasAttachedClient;
        public bool HasConnectedSession => _roleSessionProxy.HasConnectedSession;
        public int ReceivedCount => _roleSessionProxy.ReceivedCount;
        public int SentCount => _roleSessionProxy.SentCount;
        public bool HasCapturedCheckPasswordAuth => _capturedCheckPasswordAuth.HasValue;
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

        public LoginSelectCharacterByVacRoundTripEvidence GetSelectCharacterByVacRoundTripEvidence()
        {
            lock (_sync)
            {
                return _selectCharacterByVacRoundTripEvidence;
            }
        }

        public string DescribeSelectCharacterByVacRoundTripEvidence()
        {
            LoginSelectCharacterByVacRoundTripEvidence evidence = GetSelectCharacterByVacRoundTripEvidence();
            if (evidence.IsComplete)
            {
                return $"VAC round-trip complete: opcode {evidence.RequestOpcode} character {evidence.RequestCharacterId} world {evidence.RequestWorldId}, backend result {evidence.ResultCode}/{evidence.SecondaryCode} from {evidence.ResultSource} to {evidence.EndpointText}, simulator handoff {evidence.FieldHandoffEndpointText}.";
            }

            if (evidence.HasSuccessfulResult)
            {
                return $"VAC round-trip waiting for simulator field handoff: backend result {evidence.ResultCode}/{evidence.SecondaryCode} from {evidence.ResultSource} to {evidence.EndpointText}.";
            }

            if (evidence.HasRequest)
            {
                return $"VAC round-trip waiting for backend result: opcode {evidence.RequestOpcode} character {evidence.RequestCharacterId} world {evidence.RequestWorldId}.";
            }

            return "VAC round-trip evidence not captured.";
        }

        public void ClearSelectCharacterByVacRoundTripEvidence()
        {
            lock (_sync)
            {
                _selectCharacterByVacRoundTripEvidence = new LoginSelectCharacterByVacRoundTripEvidence();
            }

            LastStatus = "Cleared SelectCharacterByVAC round-trip evidence.";
        }

        public bool TryGetCapturedCheckPasswordAuth(out LoginCheckPasswordAuthMaterial authMaterial)
        {
            if (_capturedCheckPasswordAuth.HasValue)
            {
                authMaterial = _capturedCheckPasswordAuth.Value;
                authMaterial = authMaterial with { MachineId = (byte[])authMaterial.MachineId.Clone() };
                return true;
            }

            authMaterial = default;
            return false;
        }

        public void ClearCapturedCheckPasswordAuth()
        {
            _capturedCheckPasswordAuth = null;
            LastStatus = "Cleared captured login CheckPassword auth material.";
        }

        public string DescribeCapturedCheckPasswordAuth()
        {
            if (!_capturedCheckPasswordAuth.HasValue)
            {
                return "CheckPassword auth not captured";
            }

            LoginCheckPasswordAuthMaterial auth = _capturedCheckPasswordAuth.Value;
            return $"CheckPassword auth captured from {auth.Source} at {auth.CapturedAtUtc:HH:mm:ss}Z (machine id {auth.MachineId?.Length ?? 0} bytes, gameStartMode {auth.GameStartMode}, partner {auth.PartnerCode})";
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
                RecordSelectCharacterByVacResultEvidence(mappedPacketType, payloadBytes, rawPacket, source);
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
            RecordSelectCharacterByVacResultEvidence(packetType, rawPacket.Length > sizeof(ushort) ? rawPacket[sizeof(ushort)..] : Array.Empty<byte>(), rawPacket, source);
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

        public bool TrySendCheckPasswordRequest(
            string password,
            string nexonPassport,
            byte[] machineId,
            int gameRoomClient,
            byte gameStartMode,
            int partnerCode,
            out string status)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                status = "Login official-session CheckPassword injection requires the title password.";
                LastStatus = status;
                return false;
            }

            if (string.IsNullOrWhiteSpace(nexonPassport))
            {
                status = "Login official-session CheckPassword injection requires a Nexon passport captured from the client auth layer.";
                LastStatus = status;
                return false;
            }

            if (machineId == null || machineId.Length != ClientMachineIdLength)
            {
                status = $"Login official-session CheckPassword injection requires a {ClientMachineIdLength}-byte client machine id.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildCheckPasswordPacket(password, nexonPassport, machineId, gameRoomClient, gameStartMode, partnerCode),
                $"Injected login opcode {OutboundCheckPasswordOpcode} for CheckPassword through the live session",
                out status);
        }

        public bool TrySendCheckUserLimitRequest(int worldId, out string status)
        {
            if (worldId < 0 || worldId > ushort.MaxValue)
            {
                status = "Login official-session CheckUserLimit injection requires a 16-bit world id.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildCheckUserLimitPacket(worldId),
                $"Injected login opcode {OutboundCheckUserLimitOpcode} for CheckUserLimit world {worldId} into live session",
                out status);
        }

        public bool TrySendSelectWorldRequest(LoginSelectWorldRequest request, out string status)
        {
            if (string.IsNullOrWhiteSpace(request.NexonPassport))
            {
                status = "Login official-session SelectWorld injection requires a Nexon passport captured from the client auth layer.";
                LastStatus = status;
                return false;
            }

            if (request.MachineId == null || request.MachineId.Length != ClientMachineIdLength)
            {
                status = $"Login official-session SelectWorld injection requires a {ClientMachineIdLength}-byte client machine id.";
                LastStatus = status;
                return false;
            }

            if (request.WorldId < 0 || request.WorldId > byte.MaxValue)
            {
                status = "Login official-session SelectWorld injection requires an 8-bit world id.";
                LastStatus = status;
                return false;
            }

            if (request.ChannelId < 0 || request.ChannelId > byte.MaxValue)
            {
                status = "Login official-session SelectWorld injection requires an 8-bit channel id.";
                LastStatus = status;
                return false;
            }

            return TrySendPacket(
                BuildSelectWorldPacket(request),
                $"Injected login opcode {OutboundSelectWorldOpcode} for SelectWorld world {request.WorldId}, channel {request.ChannelId + 1} into live session",
                out status);
        }

        public bool TrySendViewAllCharacterRequest(bool includeTitleReturn, byte gameStartMode, out string status)
        {
            return TrySendPacket(
                BuildViewAllCharacterPacket(includeTitleReturn, gameStartMode),
                $"Injected login opcode {OutboundViewAllCharacterOpcode} for ViewAllChar through the live session",
                out status);
        }

        public bool TrySendSelectCharacterRequest(LoginSelectCharacterRequest request, out string status)
        {
            if (request.CharacterId <= 0)
            {
                status = "Login official-session SelectCharacter injection requires a valid character id.";
                LastStatus = status;
                return false;
            }

            if (request.LoginOpt is 0 or 1 &&
                string.IsNullOrWhiteSpace(request.SecondaryPassword))
            {
                status = "Login official-session SelectCharacter injection requires a secondary password for the client secondary-password branch.";
                LastStatus = status;
                return false;
            }

            short opcode = ResolveSelectCharacterOpcode(request.LoginOpt);
            return TrySendPacket(
                BuildSelectCharacterPacket(request),
                $"Injected login opcode {opcode} for SelectCharacter character {request.CharacterId} into live session",
                out status);
        }

        public bool TrySendSelectCharacterByVacRequest(LoginSelectCharacterByVacRequest request, out string status)
        {
            if (request.CharacterId <= 0)
            {
                status = "Login official-session SelectCharacterByVAC injection requires a valid character id.";
                LastStatus = status;
                return false;
            }

            if (request.WorldId < 0)
            {
                status = "Login official-session SelectCharacterByVAC injection requires a non-negative world id.";
                LastStatus = status;
                return false;
            }

            if (request.LoginOpt is 0 or 1 &&
                string.IsNullOrWhiteSpace(request.SecondaryPassword))
            {
                status = "Login official-session SelectCharacterByVAC injection requires a secondary password for the client secondary-password branch.";
                LastStatus = status;
                return false;
            }

            short opcode = ResolveSelectCharacterByVacOpcode(request.LoginOpt);
            byte[] packet = BuildSelectCharacterByVacPacket(request);
            bool sent = TrySendPacket(
                packet,
                $"Injected login opcode {opcode} for SelectCharacterByVAC character {request.CharacterId} in world {request.WorldId} into live session",
                out status);
            if (sent)
            {
                RecordSelectCharacterByVacRequestEvidence(request, opcode, packet);
            }

            return sent;
        }

        public void RecordSelectCharacterByVacFieldHandoff(LoginIssuedDirectConnect directConnect)
        {
            if (directConnect == null ||
                !string.Equals(directConnect.SourcePacket, "SelectCharacterByVACResult", StringComparison.Ordinal))
            {
                return;
            }

            lock (_sync)
            {
                LoginSelectCharacterByVacRoundTripEvidence current = _selectCharacterByVacRoundTripEvidence ?? new();
                _selectCharacterByVacRoundTripEvidence = new LoginSelectCharacterByVacRoundTripEvidence
                {
                    RequestOpcode = current.RequestOpcode,
                    RequestCharacterId = current.RequestCharacterId,
                    RequestWorldId = current.RequestWorldId,
                    RequestPacketHex = current.RequestPacketHex,
                    RequestSentAtUtc = current.RequestSentAtUtc,
                    ResultSource = current.ResultSource,
                    ResultPacketHex = current.ResultPacketHex,
                    ResultReceivedAtUtc = current.ResultReceivedAtUtc,
                    ResultCode = current.ResultCode,
                    SecondaryCode = current.SecondaryCode,
                    EndpointText = current.EndpointText,
                    ResultCharacterId = current.ResultCharacterId,
                    FieldHandoffAtUtc = DateTime.UtcNow,
                    FieldHandoffEndpointText = directConnect.EndpointText,
                };
            }

            LastStatus = "Recorded SelectCharacterByVAC simulator field handoff evidence.";
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

        public static byte[] BuildCheckPasswordPacket(
            string password,
            string nexonPassport,
            byte[] machineId,
            int gameRoomClient,
            byte gameStartMode,
            int partnerCode)
        {
            PacketWriter writer = new();
            writer.WriteShort(OutboundCheckPasswordOpcode);
            writer.WriteMapleString(password ?? string.Empty);
            writer.WriteMapleString(nexonPassport ?? string.Empty);
            writer.Write(machineId ?? Array.Empty<byte>());
            writer.WriteInt(gameRoomClient);
            writer.WriteByte(gameStartMode);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteInt(partnerCode);
            return writer.ToArray();
        }

        public static byte[] BuildCheckUserLimitPacket(int worldId)
        {
            PacketWriter writer = new();
            writer.WriteShort(OutboundCheckUserLimitOpcode);
            writer.WriteShort((short)Math.Clamp(worldId, 0, ushort.MaxValue));
            return writer.ToArray();
        }

        public static byte[] BuildSelectWorldPacket(LoginSelectWorldRequest request)
        {
            // Client evidence: CLogin::SendLoginPacket (0x5dbef0), called from
            // CUIChannelSelect::EnterChannel, writes the passport, machine id,
            // game-room client, game-start mode, world, channel, and local socket IP.
            PacketWriter writer = new();
            writer.WriteShort(OutboundSelectWorldOpcode);
            writer.WriteMapleString((request.NexonPassport ?? string.Empty).Trim());
            writer.WriteBytes(request.MachineId ?? Array.Empty<byte>());
            writer.WriteInt(request.GameRoomClient);
            writer.WriteByte(request.GameStartMode);
            writer.WriteByte((byte)Math.Clamp(request.WorldId, 0, byte.MaxValue));
            writer.WriteByte((byte)Math.Clamp(request.ChannelId, 0, byte.MaxValue));
            writer.WriteInt(request.LocalIpAddress);
            return writer.ToArray();
        }

        public static byte[] BuildViewAllCharacterPacket(bool includeTitleReturn, byte gameStartMode)
        {
            PacketWriter writer = new();
            if (includeTitleReturn)
            {
                writer.WriteShort(OutboundReturnToTitleOpcode);
            }

            writer.WriteShort(OutboundViewAllCharacterOpcode);
            writer.WriteByte(gameStartMode);
            return writer.ToArray();
        }

        public static byte[] BuildSelectCharacterPacket(LoginSelectCharacterRequest request)
        {
            byte loginOpt = NormalizeSelectCharacterLoginOpt(request.LoginOpt);
            PacketWriter writer = new();
            writer.WriteShort(ResolveSelectCharacterOpcode(loginOpt));

            string macAddress = request.MacAddress ?? string.Empty;
            string macAddressWithHddSerial = request.MacAddressWithHddSerial ?? string.Empty;
            string secondaryPassword = (request.SecondaryPassword ?? string.Empty).Trim();

            switch (loginOpt)
            {
                case 0:
                    writer.WriteByte(1);
                    writer.WriteInt(request.CharacterId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    writer.WriteMapleString(secondaryPassword);
                    break;
                case 1:
                    writer.WriteMapleString(secondaryPassword);
                    writer.WriteInt(request.CharacterId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    break;
                default:
                    writer.WriteInt(request.CharacterId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    break;
            }

            return writer.ToArray();
        }

        public static byte[] BuildSelectCharacterByVacPacket(LoginSelectCharacterByVacRequest request)
        {
            // Client evidence: CLogin::SendSelectCharPacketByVAC (0x5d7550) mirrors
            // SendSelectCharPacket but writes the selected ViewAllChar world id after the character id.
            byte loginOpt = NormalizeSelectCharacterLoginOpt(request.LoginOpt);
            PacketWriter writer = new();
            writer.WriteShort(ResolveSelectCharacterByVacOpcode(loginOpt));

            string macAddress = request.MacAddress ?? string.Empty;
            string macAddressWithHddSerial = request.MacAddressWithHddSerial ?? string.Empty;
            string secondaryPassword = (request.SecondaryPassword ?? string.Empty).Trim();
            int worldId = Math.Max(0, request.WorldId);

            switch (loginOpt)
            {
                case 0:
                    writer.WriteByte(1);
                    writer.WriteInt(request.CharacterId);
                    writer.WriteInt(worldId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    writer.WriteMapleString(secondaryPassword);
                    break;
                case 1:
                    writer.WriteMapleString(secondaryPassword);
                    writer.WriteInt(request.CharacterId);
                    writer.WriteInt(worldId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    break;
                default:
                    writer.WriteInt(request.CharacterId);
                    writer.WriteInt(worldId);
                    writer.WriteMapleString(macAddress);
                    writer.WriteMapleString(macAddressWithHddSerial);
                    break;
            }

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

        private static byte NormalizeSelectCharacterLoginOpt(byte loginOpt)
        {
            return loginOpt <= 3 ? loginOpt : (byte)2;
        }

        private static short ResolveSelectCharacterOpcode(byte loginOpt)
        {
            return NormalizeSelectCharacterLoginOpt(loginOpt) switch
            {
                0 => OutboundSelectCharacterLoginOpt0Opcode,
                1 => OutboundSelectCharacterLoginOpt1Opcode,
                _ => OutboundSelectCharacterOpcode,
            };
        }

        private static short ResolveSelectCharacterByVacOpcode(byte loginOpt)
        {
            return NormalizeSelectCharacterLoginOpt(loginOpt) switch
            {
                0 => OutboundSelectCharacterByVacLoginOpt0Opcode,
                1 => OutboundSelectCharacterByVacLoginOpt1Opcode,
                _ => OutboundSelectCharacterByVacOpcode,
            };
        }

        public static bool TryParseClientMachineId(string text, out byte[] machineId, out string error)
        {
            machineId = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = $"Client machine id requires {ClientMachineIdLength} bytes of hex data.";
                return false;
            }

            string normalized = new(text
                .Where(ch => !char.IsWhiteSpace(ch) && ch != '-' && ch != ':' && ch != ',')
                .ToArray());
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[2..];
            }

            if (normalized.Length != ClientMachineIdLength * 2 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
            {
                error = $"Client machine id requires exactly {ClientMachineIdLength} hex bytes.";
                return false;
            }

            try
            {
                machineId = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                error = "Client machine id hex data is invalid.";
                return false;
            }
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
            bool capturedAuth = e != null &&
                !e.IsInit &&
                TryCaptureCheckPasswordAuthFromClientPacket(e.RawPacket, $"official-client:{e.SourceEndpoint}");

            if (!capturedAuth)
            {
                LastStatus = _roleSessionProxy.LastStatus;
            }
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

        private void RecordSelectCharacterByVacRequestEvidence(
            LoginSelectCharacterByVacRequest request,
            short opcode,
            byte[] packet)
        {
            lock (_sync)
            {
                _selectCharacterByVacRoundTripEvidence = new LoginSelectCharacterByVacRoundTripEvidence
                {
                    RequestOpcode = opcode,
                    RequestCharacterId = request.CharacterId,
                    RequestWorldId = request.WorldId,
                    RequestPacketHex = Convert.ToHexString(packet ?? Array.Empty<byte>()),
                    RequestSentAtUtc = DateTime.UtcNow,
                };
            }
        }

        private void RecordSelectCharacterByVacResultEvidence(
            LoginPacketType packetType,
            byte[] payloadBytes,
            byte[] rawPacket,
            string source)
        {
            if (packetType != LoginPacketType.SelectCharacterByVacResult ||
                !LoginSelectCharacterByVacResultCodec.TryDecode(payloadBytes, out LoginSelectCharacterByVacResultProfile profile, out _) ||
                !profile.IsConnectSuccess)
            {
                return;
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "official-session" : source.Trim();
            lock (_sync)
            {
                LoginSelectCharacterByVacRoundTripEvidence current = _selectCharacterByVacRoundTripEvidence ?? new();
                _selectCharacterByVacRoundTripEvidence = new LoginSelectCharacterByVacRoundTripEvidence
                {
                    RequestOpcode = current.RequestOpcode,
                    RequestCharacterId = current.RequestCharacterId,
                    RequestWorldId = current.RequestWorldId,
                    RequestPacketHex = current.RequestPacketHex,
                    RequestSentAtUtc = current.RequestSentAtUtc,
                    ResultSource = normalizedSource,
                    ResultPacketHex = Convert.ToHexString(rawPacket ?? Array.Empty<byte>()),
                    ResultReceivedAtUtc = DateTime.UtcNow,
                    ResultCode = profile.ResultCode,
                    SecondaryCode = profile.SecondaryCode,
                    EndpointText = profile.EndpointText,
                    ResultCharacterId = profile.CharacterId,
                    FieldHandoffAtUtc = current.FieldHandoffAtUtc,
                    FieldHandoffEndpointText = current.FieldHandoffEndpointText,
                };
            }
        }

        private bool TryCaptureCheckPasswordAuth(byte[] rawPacket, string source)
        {
            if (!TryReadCheckPasswordAuth(rawPacket, source, out LoginCheckPasswordAuthMaterial authMaterial))
            {
                return false;
            }

            _capturedCheckPasswordAuth = authMaterial;
            LastStatus = $"Captured login CheckPassword auth material from {authMaterial.Source}.";
            return true;
        }

        internal bool TryCaptureCheckPasswordAuthFromClientPacket(byte[] rawPacket, string source)
        {
            return TryCaptureCheckPasswordAuth(rawPacket, source);
        }

        private static bool TryReadCheckPasswordAuth(
            byte[] rawPacket,
            string source,
            out LoginCheckPasswordAuthMaterial authMaterial)
        {
            authMaterial = default;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return false;
            }

            try
            {
                using PacketReader reader = new(rawPacket);
                ushort opcode = (ushort)reader.ReadShort();
                if (opcode != OutboundCheckPasswordOpcode)
                {
                    return false;
                }

                _ = reader.ReadMapleString();
                string nexonPassport = reader.ReadMapleString();
                if (reader.Remaining < ClientMachineIdLength + sizeof(int) + 3 + sizeof(int))
                {
                    return false;
                }

                byte[] machineId = reader.ReadBytes(ClientMachineIdLength);
                int gameRoomClient = reader.ReadInt();
                byte gameStartMode = reader.ReadByte();
                reader.ReadByte();
                reader.ReadByte();
                int partnerCode = reader.ReadInt();

                if (string.IsNullOrWhiteSpace(nexonPassport) || machineId.Length != ClientMachineIdLength)
                {
                    return false;
                }

                authMaterial = new LoginCheckPasswordAuthMaterial(
                    nexonPassport,
                    machineId,
                    gameRoomClient,
                    gameStartMode,
                    partnerCode,
                    string.IsNullOrWhiteSpace(source) ? "official-client" : source,
                    DateTime.UtcNow);
                return true;
            }
            catch
            {
                return false;
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
