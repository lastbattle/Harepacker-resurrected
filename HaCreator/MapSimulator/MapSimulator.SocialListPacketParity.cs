using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using System;
using System.Linq;
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly SocialListOfficialSessionBridgeManager _socialListOfficialSessionBridge = new();
        private bool _socialListOfficialSessionBridgeEnabled;
        private bool _socialListOfficialSessionBridgeUseDiscovery;
        private int _socialListOfficialSessionBridgeConfiguredListenPort = SocialListOfficialSessionBridgeManager.DefaultListenPort;
        private string _socialListOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _socialListOfficialSessionBridgeConfiguredRemotePort;
        private string _socialListOfficialSessionBridgeConfiguredProcessSelector;
        private int? _socialListOfficialSessionBridgeConfiguredLocalPort;
        private ushort _socialListOfficialSessionBridgeConfiguredFriendResultOpcode = SocialListOfficialSessionBridgeManager.ClientFriendResultOpcode;
        private ushort _socialListOfficialSessionBridgeConfiguredPartyResultOpcode = SocialListOfficialSessionBridgeManager.ClientPartyResultOpcode;
        private ushort _socialListOfficialSessionBridgeConfiguredGuildResultOpcode;
        private ushort _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode;
        private const int SocialListOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt;

        private const string SocialListPacketPayloadUsage =
            "Usage: /sociallist packet [status|session [status|discoverstatus <remotePort> [process=selector] [localPort=n]|discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n] [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]|start <listenPort> <remoteHost> <remotePort> <opcode> [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]|history [count]|clearhistory|replay <historyIndex>|sendraw <hex>|stop]|clientraw <hex>|<friend|party|guild|alliance|blacklist> <payloadhex=..|payloadb64=..>|friendresult <payloadhex=..|payloadb64=..>|partyresult <payloadhex=..|payloadb64=..>|guildresult <payloadhex=..|payloadb64=..>|guildskillresult <payloadhex=..|payloadb64=..>|allianceresult <payloadhex=..|payloadb64=..>|owner <tab> <local|packet> [summary]|seed <tab>|clear <tab>|remove <tab> <name>|select <tab> <name>|summary <tab> <summary>|resolve <tab> <approve|reject> [level=n] [remain=m] [fund=mesos] [summary]|upsert <tab> <name>|<primary>|<secondary>|<location>|<channel>|<online>|<leader>|<blocked>|<local>|guildauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<admission>|<notice>>|allianceauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<notice>>|guildui <clear|payloadhex=..|payloadb64=..|<member>|<guildName>|<guildLevel>>|guilddialog <status|balance [mesos]|approve [summary]|reject [summary]>]";
        private const string SocialListPacketRawUsage =
            "Usage: /sociallist packetraw <friend|party|guild|alliance|blacklist|guildauth|allianceauth|guildui|friendresult|partyresult|guildresult|guildskillresult|allianceresult|clientresult> <hex>";

        private ChatCommandHandler.CommandResult HandleSocialListPacketCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{_socialListRuntime.DescribeStatus()}{Environment.NewLine}{DescribeSocialListOfficialSessionBridgeStatus()}");
            }

            if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
            {
                return HandleSocialListSessionCommand(args.Skip(1).ToArray());
            }

            string packetAction = args[0].ToLowerInvariant();
            if (string.Equals(packetAction, "clientraw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(packetAction, "clientresult", StringComparison.OrdinalIgnoreCase))
            {
                return HandleSocialListClientResultRawCommand(args.Skip(1).ToArray());
            }

            if (TryParseSocialListTabToken(packetAction, out SocialListTab packetTab))
            {
                if (!TryParseSocialListPacketPayloadArgument(args, 1, out byte[] rosterPayload, out string payloadError))
                {
                    return ChatCommandHandler.CommandResult.Error(payloadError ?? SocialListPacketPayloadUsage);
                }

                string clientFamily = packetTab switch
                {
                    SocialListTab.Friend => "CWvsContext::OnFriendResult",
                    SocialListTab.Party => "CWvsContext::OnPartyResult",
                    SocialListTab.Guild => "CWvsContext::OnGuildResult",
                    SocialListTab.Alliance => "CWvsContext::OnAllianceResult",
                    _ => "social-list roster"
                };
                return ChatCommandHandler.CommandResult.Ok(
                    $"{clientFamily}: {_socialListRuntime.ApplyPacketOwnedRosterPayload(packetTab, rosterPayload)}");
            }

            if (string.Equals(packetAction, "guildauth", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildAuthorityPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult authority: {_socialListRuntime.ApplyPacketOwnedGuildAuthorityPayload(guildAuthorityPayload)}");
            }

            if (string.Equals(packetAction, "allianceauth", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] allianceAuthorityPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnAllianceResult authority: {_socialListRuntime.ApplyPacketOwnedAllianceAuthorityPayload(allianceAuthorityPayload)}");
            }

            if (string.Equals(packetAction, "guildui", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildUiPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult UI: {_socialListRuntime.ApplyPacketOwnedGuildUiPayload(guildUiPayload)}");
            }

            if (string.Equals(packetAction, "friendresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] friendResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnFriendResult: {_socialListRuntime.ApplyClientFriendResultPayload(friendResultPayload)}");
            }

            if (string.Equals(packetAction, "partyresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] partyResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnPartyResult: {_socialListRuntime.ApplyClientPartyResultPayload(partyResultPayload)}");
            }

            if (string.Equals(packetAction, "guildresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult: {ApplyClientGuildResultPayload(guildResultPayload)}");
            }

            if (string.Equals(packetAction, "guildskillresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildSkillResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"Guild skill result: {ApplyPacketOwnedGuildSkillResultPayload(guildSkillResultPayload)}");
            }

            if (string.Equals(packetAction, "allianceresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] allianceResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnAllianceResult: {_socialListRuntime.ApplyClientAllianceResultPayload(allianceResultPayload)}");
            }

            return ChatCommandHandler.CommandResult.Error(SocialListPacketPayloadUsage);
        }

        private ChatCommandHandler.CommandResult HandleSocialListPacketRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] payload))
            {
                return ChatCommandHandler.CommandResult.Error(SocialListPacketRawUsage);
            }

            string target = args[0];
            if (TryParseSocialListTabToken(target, out SocialListTab rosterTab))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedRosterPayload(rosterTab, payload));
            }

            if (string.Equals(target, "guildauth", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedGuildAuthorityPayload(payload));
            }

            if (string.Equals(target, "allianceauth", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedAllianceAuthorityPayload(payload));
            }

            if (string.Equals(target, "guildui", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyPacketOwnedGuildUiPayload(payload));
            }

            if (string.Equals(target, "friendresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyClientFriendResultPayload(payload));
            }

            if (string.Equals(target, "partyresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyClientPartyResultPayload(payload));
            }

            if (string.Equals(target, "guildresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(ApplyClientGuildResultPayload(payload));
            }

            if (string.Equals(target, "guildskillresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(ApplyPacketOwnedGuildSkillResultPayload(payload));
            }

            if (string.Equals(target, "allianceresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyClientAllianceResultPayload(payload));
            }

            if (string.Equals(target, "clientresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(target, "clientraw", StringComparison.OrdinalIgnoreCase))
            {
                return ApplySocialListClientResultRawPacket(payload);
            }

            return ChatCommandHandler.CommandResult.Error(SocialListPacketRawUsage);
        }

        private ChatCommandHandler.CommandResult HandleSocialListClientResultRawCommand(string[] args)
        {
            if (args.Length < 1 || !TryDecodeHexBytes(string.Join(string.Empty, args), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet clientraw <opcode-framed-hex>");
            }

            return ApplySocialListClientResultRawPacket(rawPacket);
        }

        private ChatCommandHandler.CommandResult ApplySocialListClientResultRawPacket(byte[] rawPacket)
        {
            ResolveConfiguredSocialListResultOpcodes(
                out ushort friendResultOpcode,
                out ushort partyResultOpcode,
                out ushort guildResultOpcode,
                out ushort allianceResultOpcode);

            if (!SocialListPacketCodec.TryParseOpcodeFramedClientResult(
                    rawPacket,
                    friendResultOpcode,
                    partyResultOpcode,
                    guildResultOpcode,
                    allianceResultOpcode,
                    out SocialListClientResultOpcodeKind kind,
                    out byte[] payload,
                    out string error))
            {
                return ChatCommandHandler.CommandResult.Error(error ?? SocialListPacketRawUsage);
            }

            string detail = kind switch
            {
                SocialListClientResultOpcodeKind.FriendResult => _socialListRuntime.ApplyClientFriendResultPayload(payload),
                SocialListClientResultOpcodeKind.PartyResult => _socialListRuntime.ApplyClientPartyResultPayload(payload),
                SocialListClientResultOpcodeKind.AllianceResult => _socialListRuntime.ApplyClientAllianceResultPayload(payload),
                _ => ApplyClientGuildResultPayload(payload)
            };
            return ChatCommandHandler.CommandResult.Ok($"CWvsContext::{kind} opcode-framed packet: {detail}");
        }

        private static bool TryParseSocialListPacketPayloadArgument(string[] args, int payloadIndex, out byte[] payload, out string error)
        {
            payload = null;
            error = SocialListPacketPayloadUsage;
            if (args == null || args.Length <= payloadIndex)
            {
                return false;
            }

            if (!TryParseBinaryPayloadArgument(args[payloadIndex], out payload, out string parseError))
            {
                error = parseError ?? SocialListPacketPayloadUsage;
                return false;
            }

            return true;
        }

        private static bool TryParseSocialListOpcode(string token, out ushort opcode)
        {
            opcode = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string trimmed = token.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out opcode);
            }

            return ushort.TryParse(trimmed, out opcode);
        }

        private string DescribeSocialListOfficialSessionBridgeStatus()
        {
            string enabledText = _socialListOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _socialListOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string friendOpcodeText = _socialListOfficialSessionBridgeConfiguredFriendResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredFriendResultOpcode.ToString()
                : "unset";
            string partyOpcodeText = _socialListOfficialSessionBridgeConfiguredPartyResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredPartyResultOpcode.ToString()
                : "unset";
            string guildOpcodeText = _socialListOfficialSessionBridgeConfiguredGuildResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredGuildResultOpcode.ToString()
                : "unset";
            string allianceOpcodeText = _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode.ToString()
                : "unset";
            string configuredTarget = _socialListOfficialSessionBridgeUseDiscovery
                ? _socialListOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_socialListOfficialSessionBridgeConfiguredRemotePort} with local port {_socialListOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_socialListOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_socialListOfficialSessionBridgeConfiguredRemoteHost}:{_socialListOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_socialListOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_socialListOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _socialListOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_socialListOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_socialListOfficialSessionBridgeConfiguredListenPort}";
            return $"Social-list session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, friend-result opcode {friendOpcodeText}, party-result opcode {partyOpcodeText}, guild-result opcode {guildOpcodeText}, alliance-result opcode {allianceOpcodeText}. {_socialListOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureSocialListOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_socialListOfficialSessionBridgeEnabled)
            {
                if (_socialListOfficialSessionBridge.IsRunning)
                {
                    _socialListOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_socialListOfficialSessionBridgeConfiguredListenPort <= 0
                || _socialListOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue
                || _socialListOfficialSessionBridgeConfiguredFriendResultOpcode == 0
                || _socialListOfficialSessionBridgeConfiguredPartyResultOpcode == 0
                || _socialListOfficialSessionBridgeConfiguredGuildResultOpcode == 0)
            {
                if (_socialListOfficialSessionBridge.IsRunning)
                {
                    _socialListOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_socialListOfficialSessionBridgeUseDiscovery)
            {
                if (_socialListOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _socialListOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_socialListOfficialSessionBridge.IsRunning)
                    {
                        _socialListOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _socialListOfficialSessionBridge.TryRefreshFromDiscovery(
                    _socialListOfficialSessionBridgeConfiguredListenPort,
                    _socialListOfficialSessionBridgeConfiguredRemotePort,
                    _socialListOfficialSessionBridgeConfiguredFriendResultOpcode,
                    _socialListOfficialSessionBridgeConfiguredPartyResultOpcode,
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode,
                    _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode,
                    _socialListOfficialSessionBridgeConfiguredProcessSelector,
                    _socialListOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_socialListOfficialSessionBridgeConfiguredRemotePort <= 0
                || _socialListOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_socialListOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_socialListOfficialSessionBridge.IsRunning)
                {
                    _socialListOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_socialListOfficialSessionBridge.IsRunning
                && _socialListOfficialSessionBridge.ListenPort == _socialListOfficialSessionBridgeConfiguredListenPort
                && _socialListOfficialSessionBridge.RemotePort == _socialListOfficialSessionBridgeConfiguredRemotePort
                && _socialListOfficialSessionBridge.FriendResultOpcode == _socialListOfficialSessionBridgeConfiguredFriendResultOpcode
                && _socialListOfficialSessionBridge.PartyResultOpcode == _socialListOfficialSessionBridgeConfiguredPartyResultOpcode
                && _socialListOfficialSessionBridge.GuildResultOpcode == _socialListOfficialSessionBridgeConfiguredGuildResultOpcode
                && string.Equals(_socialListOfficialSessionBridge.RemoteHost, _socialListOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_socialListOfficialSessionBridge.IsRunning)
            {
                _socialListOfficialSessionBridge.Stop();
            }

            _socialListOfficialSessionBridge.Start(
                _socialListOfficialSessionBridgeConfiguredListenPort,
                _socialListOfficialSessionBridgeConfiguredRemoteHost,
                _socialListOfficialSessionBridgeConfiguredRemotePort,
                _socialListOfficialSessionBridgeConfiguredFriendResultOpcode,
                _socialListOfficialSessionBridgeConfiguredPartyResultOpcode,
                _socialListOfficialSessionBridgeConfiguredGuildResultOpcode,
                _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode);
        }

        private void RefreshSocialListOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_socialListOfficialSessionBridgeEnabled
                || !_socialListOfficialSessionBridgeUseDiscovery
                || _socialListOfficialSessionBridgeConfiguredRemotePort <= 0
                || _socialListOfficialSessionBridgeConfiguredFriendResultOpcode == 0
                || _socialListOfficialSessionBridgeConfiguredPartyResultOpcode == 0
                || _socialListOfficialSessionBridgeConfiguredGuildResultOpcode == 0
                || _socialListOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + SocialListOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _socialListOfficialSessionBridge.TryRefreshFromDiscovery(
                _socialListOfficialSessionBridgeConfiguredListenPort,
                _socialListOfficialSessionBridgeConfiguredRemotePort,
                _socialListOfficialSessionBridgeConfiguredFriendResultOpcode,
                _socialListOfficialSessionBridgeConfiguredPartyResultOpcode,
                _socialListOfficialSessionBridgeConfiguredGuildResultOpcode,
                _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode,
                _socialListOfficialSessionBridgeConfiguredProcessSelector,
                _socialListOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainSocialListOfficialSessionBridge()
        {
            while (_socialListOfficialSessionBridge.TryDequeue(out SocialListOfficialSessionBridgeMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                string detail = ApplySocialListOfficialSessionMessage(message);
                bool applied = !detail.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase)
                    && !detail.Contains("could not be decoded", StringComparison.OrdinalIgnoreCase)
                    && !detail.Contains("missing", StringComparison.OrdinalIgnoreCase);
                _socialListOfficialSessionBridge.RecordDispatchResult(message.Source, applied, $"{message.ResultLabel}: {detail}");
                WireSocialListWindowData();
                RefreshGuildSkillUiContext();
                WireGuildSkillWindowData();

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    if (applied)
                    {
                        _chat?.AddSystemMessage(detail, currTickCount);
                    }
                    else
                    {
                        _chat?.AddErrorMessage(detail, currTickCount);
                    }
                }
            }
        }

        private string ApplySocialListOfficialSessionMessage(SocialListOfficialSessionBridgeMessage message)
        {
            return message.Kind switch
            {
                SocialListOfficialSessionBridgePayloadKind.FriendResult => _socialListRuntime.ApplyClientFriendResultPayload(message.Payload),
                SocialListOfficialSessionBridgePayloadKind.PartyResult => _socialListRuntime.ApplyClientPartyResultPayload(message.Payload),
                SocialListOfficialSessionBridgePayloadKind.AllianceResult => _socialListRuntime.ApplyClientAllianceResultPayload(message.Payload),
                _ => ApplyClientGuildResultPayload(message.Payload)
            };
        }

        private string ApplyClientGuildResultPayload(byte[] payload)
        {
            string detail = _socialListRuntime.ApplyClientGuildResultPayload(payload);
            SocialListClientGuildResultPacket packet = default;
            bool parsedGuildResult = payload != null &&
                                     SocialListPacketCodec.TryParseClientGuildResult(payload, out packet, out _);
            if (!parsedGuildResult)
            {
                RefreshGuildSkillUiContext();
                WireGuildSkillWindowData();
                return detail;
            }

            if (packet.Kind == SocialListClientGuildResultKind.ResultNotice)
            {
                string pendingResolutionDetail = _guildSkillRuntime.TryResolvePendingFromClientResultNotice(
                    packet.HasExplicitNotice,
                    packet.ResultNotice);
                if (!string.IsNullOrWhiteSpace(pendingResolutionDetail))
                {
                    TryTriggerSpecialistPetSocialFeedback(pendingResolutionDetail, Environment.TickCount);
                    detail = string.IsNullOrWhiteSpace(detail)
                        ? pendingResolutionDetail
                        : $"{detail} {pendingResolutionDetail}";
                }

                RefreshGuildSkillUiContext();
                WireGuildSkillWindowData();
                return detail;
            }

            if (packet.Kind != SocialListClientGuildResultKind.SkillRecord || !packet.GuildSkillRecord.HasValue)
            {
                RefreshGuildSkillUiContext();
                WireGuildSkillWindowData();
                return detail;
            }

            if (_guildSkillRuntime.BuildSnapshot().Entries.Count == 0)
            {
                _guildSkillRuntime.SetSkills(SkillDataLoader.LoadGuildSkills(_DxDeviceManager.GraphicsDevice));
            }

            string skillRecordDetail = _guildSkillRuntime.ApplyPacketOwnedSkillRecord(packet.GuildSkillRecord.Value, packet.GuildId);
            TryTriggerSpecialistPetSocialFeedback(skillRecordDetail, Environment.TickCount);
            RefreshGuildSkillUiContext();
            WireGuildSkillWindowData();
            return string.IsNullOrWhiteSpace(detail)
                ? skillRecordDetail
                : $"{detail} {skillRecordDetail}";
        }

        private string ApplyPacketOwnedGuildSkillResultPayload(byte[] payload)
        {
            if (payload == null)
            {
                return "Packet-owned guild-skill result payload is missing.";
            }

            if (!SocialListPacketCodec.TryParseGuildSkillResult(payload, out GuildSkillResultPacket packet, out string error))
            {
                return error ?? "Packet-owned guild-skill result payload could not be decoded.";
            }

            if (_guildSkillRuntime.BuildSnapshot().Entries.Count == 0)
            {
                _guildSkillRuntime.SetSkills(SkillDataLoader.LoadGuildSkills(_DxDeviceManager.GraphicsDevice));
            }

            string detail = _guildSkillRuntime.ApplyPacketOwnedResult(packet);
            TryTriggerSpecialistPetSocialFeedback(detail, Environment.TickCount);
            RefreshGuildSkillUiContext();
            WireGuildSkillWindowData();
            return detail;
        }

        private ChatCommandHandler.CommandResult HandleSocialListSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeSocialListOfficialSessionBridgeStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "discoverstatus":
                    return HandleSocialListSessionDiscoverStatusCommand(args);

                case "discover":
                    if (args.Length < 3
                        || !int.TryParse(args[1], out int discoverRemotePort)
                        || discoverRemotePort <= 0
                        || !TryParseSocialListOpcode(args[2], out ushort discoverOpcodeDefault))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n] [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]");
                    }

                    int discoverListenPort = SocialListOfficialSessionBridgeManager.DefaultListenPort;
                    if (args.Length >= 4 && int.TryParse(args[3], out int parsedDiscoverListenPort))
                    {
                        discoverListenPort = parsedDiscoverListenPort;
                    }

                    string discoverProcessSelector = null;
                    int? discoverLocalPort = null;
                    ushort discoverFriendOpcode = SocialListOfficialSessionBridgeManager.ClientFriendResultOpcode;
                    ushort discoverPartyOpcode = SocialListOfficialSessionBridgeManager.ClientPartyResultOpcode;
                    ushort discoverGuildOpcode = discoverOpcodeDefault;
                    ushort discoverAllianceOpcode = discoverOpcodeDefault;
                    for (int i = 3; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("process=", StringComparison.OrdinalIgnoreCase))
                        {
                            discoverProcessSelector = args[i]["process=".Length..];
                        }
                        else if (args[i].StartsWith("localPort=", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(args[i]["localPort=".Length..], out int parsedLocalPort))
                        {
                            discoverLocalPort = parsedLocalPort;
                        }
                        else if (!TryApplySocialListOpcodeOverrideToken(
                                     args[i],
                                     ref discoverFriendOpcode,
                                     ref discoverPartyOpcode,
                                     ref discoverGuildOpcode,
                                     ref discoverAllianceOpcode,
                                     out string opcodeOverrideError))
                        {
                            return ChatCommandHandler.CommandResult.Error(opcodeOverrideError);
                        }
                    }

                    _socialListOfficialSessionBridgeEnabled = true;
                    _socialListOfficialSessionBridgeUseDiscovery = true;
                    _socialListOfficialSessionBridgeConfiguredListenPort = discoverListenPort;
                    _socialListOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                    _socialListOfficialSessionBridgeConfiguredRemotePort = discoverRemotePort;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = discoverProcessSelector;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = discoverLocalPort;
                    _socialListOfficialSessionBridgeConfiguredFriendResultOpcode = discoverFriendOpcode;
                    _socialListOfficialSessionBridgeConfiguredPartyResultOpcode = discoverPartyOpcode;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = discoverGuildOpcode;
                    _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode = discoverAllianceOpcode;
                    _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt = 0;
                    return _socialListOfficialSessionBridge.TryRefreshFromDiscovery(
                        discoverListenPort,
                        discoverRemotePort,
                        discoverFriendOpcode,
                        discoverPartyOpcode,
                        discoverGuildOpcode,
                        discoverAllianceOpcode,
                        discoverProcessSelector,
                        discoverLocalPort,
                        out string discoverStatus)
                        ? ChatCommandHandler.CommandResult.Ok($"{discoverStatus} {DescribeSocialListOfficialSessionBridgeStatus()}")
                        : ChatCommandHandler.CommandResult.Error(discoverStatus);

                case "start":
                    if (args.Length < 5
                        || !int.TryParse(args[1], out int listenPort)
                        || listenPort <= 0
                        || !int.TryParse(args[3], out int remotePort)
                        || remotePort <= 0
                        || !TryParseSocialListOpcode(args[4], out ushort startOpcodeDefault))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session start <listenPort> <remoteHost> <remotePort> <opcode> [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]");
                    }

                    ushort startFriendOpcode = SocialListOfficialSessionBridgeManager.ClientFriendResultOpcode;
                    ushort startPartyOpcode = SocialListOfficialSessionBridgeManager.ClientPartyResultOpcode;
                    ushort startGuildOpcode = startOpcodeDefault;
                    ushort startAllianceOpcode = startOpcodeDefault;
                    for (int i = 5; i < args.Length; i++)
                    {
                        if (!TryApplySocialListOpcodeOverrideToken(
                                args[i],
                                ref startFriendOpcode,
                                ref startPartyOpcode,
                                ref startGuildOpcode,
                                ref startAllianceOpcode,
                                out string opcodeOverrideError))
                        {
                            return ChatCommandHandler.CommandResult.Error(opcodeOverrideError);
                        }
                    }

                    _socialListOfficialSessionBridgeEnabled = true;
                    _socialListOfficialSessionBridgeUseDiscovery = false;
                    _socialListOfficialSessionBridgeConfiguredListenPort = listenPort;
                    _socialListOfficialSessionBridgeConfiguredRemoteHost = args[2];
                    _socialListOfficialSessionBridgeConfiguredRemotePort = remotePort;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = null;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = null;
                    _socialListOfficialSessionBridgeConfiguredFriendResultOpcode = startFriendOpcode;
                    _socialListOfficialSessionBridgeConfiguredPartyResultOpcode = startPartyOpcode;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = startGuildOpcode;
                    _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode = startAllianceOpcode;
                    EnsureSocialListOfficialSessionBridgeState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(DescribeSocialListOfficialSessionBridgeStatus());

                case "stop":
                    _socialListOfficialSessionBridgeEnabled = false;
                    _socialListOfficialSessionBridgeUseDiscovery = false;
                    _socialListOfficialSessionBridgeConfiguredRemotePort = 0;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = null;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = null;
                    _socialListOfficialSessionBridgeConfiguredFriendResultOpcode = SocialListOfficialSessionBridgeManager.ClientFriendResultOpcode;
                    _socialListOfficialSessionBridgeConfiguredPartyResultOpcode = SocialListOfficialSessionBridgeManager.ClientPartyResultOpcode;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = 0;
                    _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode = 0;
                    _socialListOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(DescribeSocialListOfficialSessionBridgeStatus());

                case "history":
                    return HandleSocialListSessionHistoryCommand(args);

                case "clearhistory":
                    return ChatCommandHandler.CommandResult.Ok(_socialListOfficialSessionBridge.ClearRecentPackets());

                case "replay":
                    return HandleSocialListSessionReplayCommand(args);

                case "sendraw":
                    return HandleSocialListSessionSendRawCommand(args);

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session [status|discoverstatus <remotePort> [process=selector] [localPort=n]|discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n] [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]|start <listenPort> <remoteHost> <remotePort> <opcode> [friendOpcode=n] [partyOpcode=n] [guildOpcode=n] [allianceOpcode=n]|history [count]|clearhistory|replay <historyIndex>|sendraw <hex>|stop]");
            }
        }

        private ChatCommandHandler.CommandResult HandleSocialListSessionHistoryCommand(string[] args)
        {
            int historyCount = 10;
            if (args.Length > 1
                && (!int.TryParse(args[1], out historyCount) || historyCount <= 0))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session history [count]");
            }

            return ChatCommandHandler.CommandResult.Info(_socialListOfficialSessionBridge.DescribeRecentPackets(historyCount));
        }

        private ChatCommandHandler.CommandResult HandleSocialListSessionReplayCommand(string[] args)
        {
            if (args.Length < 2
                || !int.TryParse(args[1], out int replayIndex)
                || replayIndex <= 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session replay <historyIndex>");
            }

            return _socialListOfficialSessionBridge.TryReplayRecentPacket(replayIndex, out string replayStatus)
                ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                : ChatCommandHandler.CommandResult.Error(replayStatus);
        }

        private ChatCommandHandler.CommandResult HandleSocialListSessionSendRawCommand(string[] args)
        {
            if (args.Length < 2 || !TryDecodeHexBytes(string.Concat(args[1..]), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session sendraw <hex>");
            }

            return _socialListOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string sendStatus)
                ? ChatCommandHandler.CommandResult.Ok(sendStatus)
                : ChatCommandHandler.CommandResult.Error(sendStatus);
        }

        private ChatCommandHandler.CommandResult HandleSocialListSessionDiscoverStatusCommand(string[] args)
        {
            if (args.Length < 2
                || !int.TryParse(args[1], out int remotePort)
                || remotePort <= 0
                || remotePort > ushort.MaxValue)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session discoverstatus <remotePort> [process=selector] [localPort=n]");
            }

            string processSelector = null;
            int? localPort = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].StartsWith("process=", StringComparison.OrdinalIgnoreCase))
                {
                    processSelector = args[i]["process=".Length..];
                }
                else if (args[i].StartsWith("localPort=", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i]["localPort=".Length..], out int parsedLocalPort))
                {
                    localPort = parsedLocalPort;
                }
            }

            return ChatCommandHandler.CommandResult.Info(
                _socialListOfficialSessionBridge.DescribeDiscoveredSessions(remotePort, processSelector, localPort));
        }

        private void ResolveConfiguredSocialListResultOpcodes(
            out ushort friendResultOpcode,
            out ushort partyResultOpcode,
            out ushort guildResultOpcode,
            out ushort allianceResultOpcode)
        {
            friendResultOpcode = _socialListOfficialSessionBridgeConfiguredFriendResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredFriendResultOpcode
                : SocialListOfficialSessionBridgeManager.ClientFriendResultOpcode;
            partyResultOpcode = _socialListOfficialSessionBridgeConfiguredPartyResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredPartyResultOpcode
                : SocialListOfficialSessionBridgeManager.ClientPartyResultOpcode;
            guildResultOpcode = _socialListOfficialSessionBridgeConfiguredGuildResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredGuildResultOpcode
                : SocialListOfficialSessionBridgeManager.ClientGuildResultOpcode;
            allianceResultOpcode = _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredAllianceResultOpcode
                : SocialListOfficialSessionBridgeManager.ClientAllianceResultOpcode;
        }

        private static bool TryApplySocialListOpcodeOverrideToken(
            string token,
            ref ushort friendResultOpcode,
            ref ushort partyResultOpcode,
            ref ushort guildResultOpcode,
            ref ushort allianceResultOpcode,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            if (!TrySplitSocialListOpcodeOverride(token, out string key, out string value))
            {
                return true;
            }

            if (!TryParseSocialListOpcode(value, out ushort parsedOpcode))
            {
                error = $"Invalid social-list session opcode override '{token}'.";
                return false;
            }

            switch (key.ToLowerInvariant())
            {
                case "friendopcode":
                case "friend":
                    friendResultOpcode = parsedOpcode;
                    return true;
                case "partyopcode":
                case "party":
                    partyResultOpcode = parsedOpcode;
                    return true;
                case "guildopcode":
                case "guild":
                    guildResultOpcode = parsedOpcode;
                    return true;
                case "allianceopcode":
                case "alliance":
                    allianceResultOpcode = parsedOpcode;
                    return true;
                default:
                    error = $"Unsupported social-list session opcode override key '{key}' in '{token}'.";
                    return false;
            }
        }

        private static bool TrySplitSocialListOpcodeOverride(string token, out string key, out string value)
        {
            key = null;
            value = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            int separatorIndex = token.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
            {
                return false;
            }

            key = token[..separatorIndex].Trim();
            value = token[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(key);
        }
    }
}
