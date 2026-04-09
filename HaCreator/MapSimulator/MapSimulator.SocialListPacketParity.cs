using HaCreator.MapSimulator.Interaction;
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
        private ushort _socialListOfficialSessionBridgeConfiguredGuildResultOpcode;
        private const int SocialListOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt;

        private const string SocialListPacketPayloadUsage =
            "Usage: /sociallist packet [status|session [status|discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n]|start <listenPort> <remoteHost> <remotePort> <opcode>|stop]|<friend|party|guild|alliance|blacklist> <payloadhex=..|payloadb64=..>|guildresult <payloadhex=..|payloadb64=..>|allianceresult <payloadhex=..|payloadb64=..>|owner <tab> <local|packet> [summary]|seed <tab>|clear <tab>|remove <tab> <name>|select <tab> <name>|summary <tab> <summary>|resolve <tab> <approve|reject> [summary]|upsert <tab> <name>|<primary>|<secondary>|<location>|<channel>|<online>|<leader>|<blocked>|<local>|guildauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<admission>|<notice>>|allianceauth <clear|payloadhex=..|payloadb64=..|<role>|<rank>|<notice>>|guildui <clear|payloadhex=..|payloadb64=..|<member>|<guildName>|<guildLevel>>|guilddialog <status|balance [mesos]|approve [summary]|reject [summary]>]";
        private const string SocialListPacketRawUsage =
            "Usage: /sociallist packetraw <friend|party|guild|alliance|blacklist|guildauth|allianceauth|guildui|guildresult|allianceresult> <hex>";

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

            if (string.Equals(packetAction, "guildresult", StringComparison.OrdinalIgnoreCase)
                && TryParseSocialListPacketPayloadArgument(args, 1, out byte[] guildResultPayload, out _))
            {
                return ChatCommandHandler.CommandResult.Ok(
                    $"CWvsContext::OnGuildResult: {_socialListRuntime.ApplyClientGuildResultPayload(guildResultPayload)}");
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

            if (string.Equals(target, "guildresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyClientGuildResultPayload(payload));
            }

            if (string.Equals(target, "allianceresult", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ApplyClientAllianceResultPayload(payload));
            }

            return ChatCommandHandler.CommandResult.Error(SocialListPacketRawUsage);
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
            string opcodeText = _socialListOfficialSessionBridgeConfiguredGuildResultOpcode > 0
                ? _socialListOfficialSessionBridgeConfiguredGuildResultOpcode.ToString()
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
            return $"Social-list session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, guild-result opcode {opcodeText}. {_socialListOfficialSessionBridge.DescribeStatus()}";
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
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode,
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
                _socialListOfficialSessionBridgeConfiguredGuildResultOpcode);
        }

        private void RefreshSocialListOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_socialListOfficialSessionBridgeEnabled
                || !_socialListOfficialSessionBridgeUseDiscovery
                || _socialListOfficialSessionBridgeConfiguredRemotePort <= 0
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
                _socialListOfficialSessionBridgeConfiguredGuildResultOpcode,
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

                string detail = _socialListRuntime.ApplyClientGuildResultPayload(message.Payload);
                bool applied = !detail.StartsWith("Unsupported", StringComparison.OrdinalIgnoreCase)
                    && !detail.Contains("could not be decoded", StringComparison.OrdinalIgnoreCase)
                    && !detail.Contains("missing", StringComparison.OrdinalIgnoreCase);
                _socialListOfficialSessionBridge.RecordDispatchResult(message.Source, applied, detail);
                WireSocialListWindowData();

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

        private ChatCommandHandler.CommandResult HandleSocialListSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeSocialListOfficialSessionBridgeStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "discover":
                    if (args.Length < 3
                        || !int.TryParse(args[1], out int discoverRemotePort)
                        || discoverRemotePort <= 0
                        || !TryParseSocialListOpcode(args[2], out ushort discoverOpcode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n]");
                    }

                    int discoverListenPort = SocialListOfficialSessionBridgeManager.DefaultListenPort;
                    if (args.Length >= 4 && int.TryParse(args[3], out int parsedDiscoverListenPort))
                    {
                        discoverListenPort = parsedDiscoverListenPort;
                    }

                    string discoverProcessSelector = null;
                    int? discoverLocalPort = null;
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
                    }

                    _socialListOfficialSessionBridgeEnabled = true;
                    _socialListOfficialSessionBridgeUseDiscovery = true;
                    _socialListOfficialSessionBridgeConfiguredListenPort = discoverListenPort;
                    _socialListOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                    _socialListOfficialSessionBridgeConfiguredRemotePort = discoverRemotePort;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = discoverProcessSelector;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = discoverLocalPort;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = discoverOpcode;
                    _nextSocialListOfficialSessionBridgeDiscoveryRefreshAt = 0;
                    return _socialListOfficialSessionBridge.TryRefreshFromDiscovery(
                        discoverListenPort,
                        discoverRemotePort,
                        discoverOpcode,
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
                        || !TryParseSocialListOpcode(args[4], out ushort startOpcode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session start <listenPort> <remoteHost> <remotePort> <opcode>");
                    }

                    _socialListOfficialSessionBridgeEnabled = true;
                    _socialListOfficialSessionBridgeUseDiscovery = false;
                    _socialListOfficialSessionBridgeConfiguredListenPort = listenPort;
                    _socialListOfficialSessionBridgeConfiguredRemoteHost = args[2];
                    _socialListOfficialSessionBridgeConfiguredRemotePort = remotePort;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = null;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = null;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = startOpcode;
                    EnsureSocialListOfficialSessionBridgeState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(DescribeSocialListOfficialSessionBridgeStatus());

                case "stop":
                    _socialListOfficialSessionBridgeEnabled = false;
                    _socialListOfficialSessionBridgeUseDiscovery = false;
                    _socialListOfficialSessionBridgeConfiguredRemotePort = 0;
                    _socialListOfficialSessionBridgeConfiguredProcessSelector = null;
                    _socialListOfficialSessionBridgeConfiguredLocalPort = null;
                    _socialListOfficialSessionBridgeConfiguredGuildResultOpcode = 0;
                    _socialListOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(DescribeSocialListOfficialSessionBridgeStatus());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet session [status|discover <remotePort> <opcode> [listenPort] [process=selector] [localPort=n]|start <listenPort> <remoteHost> <remotePort> <opcode>|stop]");
            }
        }
    }
}
