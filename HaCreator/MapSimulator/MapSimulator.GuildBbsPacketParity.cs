using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        // IDA evidence: CWvsContext::OnPacket case 59 dispatches CWvsContext::OnGuildBBSPacket.
        private const ushort GuildBbsInboundResultOpcode = 59;
        // Client request opcode mirrored by GuildBbsRuntime client-preview payloads.
        private const ushort GuildBbsOutboundRequestOpcode = 179;
        private const int GuildBbsOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;

        private readonly MessengerOfficialSessionBridgeManager _guildBbsOfficialSessionBridge =
            new("Guild BBS", 18516, GuildBbsInboundResultOpcode, GuildBbsOutboundRequestOpcode);
        private bool _guildBbsOfficialSessionBridgeEnabled;
        private bool _guildBbsOfficialSessionBridgeUseDiscovery;
        private int _guildBbsOfficialSessionBridgeConfiguredListenPort = 18516;
        private string _guildBbsOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _guildBbsOfficialSessionBridgeConfiguredRemotePort;
        private ushort _guildBbsOfficialSessionBridgeConfiguredInboundOpcode = GuildBbsInboundResultOpcode;
        private string _guildBbsOfficialSessionBridgeConfiguredProcessSelector;
        private int? _guildBbsOfficialSessionBridgeConfiguredLocalPort;
        private int _nextGuildBbsOfficialSessionBridgeDiscoveryRefreshAt;

        private string DescribeGuildBbsOfficialSessionBridgeStatus()
        {
            string enabledText = _guildBbsOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _guildBbsOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _guildBbsOfficialSessionBridgeUseDiscovery
                ? _guildBbsOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_guildBbsOfficialSessionBridgeConfiguredRemotePort} with local port {_guildBbsOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_guildBbsOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_guildBbsOfficialSessionBridgeConfiguredRemoteHost}:{_guildBbsOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_guildBbsOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_guildBbsOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _guildBbsOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_guildBbsOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_guildBbsOfficialSessionBridgeConfiguredListenPort}";
            return $"Guild BBS session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, inbound opcode {_guildBbsOfficialSessionBridgeConfiguredInboundOpcode}. {_guildBbsOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureGuildBbsOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_guildBbsOfficialSessionBridgeEnabled)
            {
                if (_guildBbsOfficialSessionBridge.IsRunning)
                {
                    _guildBbsOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_guildBbsOfficialSessionBridgeConfiguredListenPort <= 0
                || _guildBbsOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue
                || _guildBbsOfficialSessionBridgeConfiguredInboundOpcode == 0)
            {
                if (_guildBbsOfficialSessionBridge.IsRunning)
                {
                    _guildBbsOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_guildBbsOfficialSessionBridgeUseDiscovery)
            {
                if (_guildBbsOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _guildBbsOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    if (_guildBbsOfficialSessionBridge.IsRunning)
                    {
                        _guildBbsOfficialSessionBridge.Stop();
                    }

                    return;
                }

                _guildBbsOfficialSessionBridge.TryRefreshFromDiscovery(
                    _guildBbsOfficialSessionBridgeConfiguredListenPort,
                    _guildBbsOfficialSessionBridgeConfiguredRemotePort,
                    _guildBbsOfficialSessionBridgeConfiguredInboundOpcode,
                    _guildBbsOfficialSessionBridgeConfiguredProcessSelector,
                    _guildBbsOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_guildBbsOfficialSessionBridgeConfiguredRemotePort <= 0
                || _guildBbsOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_guildBbsOfficialSessionBridgeConfiguredRemoteHost))
            {
                if (_guildBbsOfficialSessionBridge.IsRunning)
                {
                    _guildBbsOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_guildBbsOfficialSessionBridge.IsRunning
                && _guildBbsOfficialSessionBridge.ListenPort == _guildBbsOfficialSessionBridgeConfiguredListenPort
                && _guildBbsOfficialSessionBridge.RemotePort == _guildBbsOfficialSessionBridgeConfiguredRemotePort
                && _guildBbsOfficialSessionBridge.MessengerOpcode == _guildBbsOfficialSessionBridgeConfiguredInboundOpcode
                && string.Equals(_guildBbsOfficialSessionBridge.RemoteHost, _guildBbsOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_guildBbsOfficialSessionBridge.IsRunning)
            {
                _guildBbsOfficialSessionBridge.Stop();
            }

            _guildBbsOfficialSessionBridge.Start(
                _guildBbsOfficialSessionBridgeConfiguredListenPort,
                _guildBbsOfficialSessionBridgeConfiguredRemoteHost,
                _guildBbsOfficialSessionBridgeConfiguredRemotePort,
                _guildBbsOfficialSessionBridgeConfiguredInboundOpcode);
        }

        private void RefreshGuildBbsOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_guildBbsOfficialSessionBridgeEnabled
                || !_guildBbsOfficialSessionBridgeUseDiscovery
                || _guildBbsOfficialSessionBridgeConfiguredRemotePort <= 0
                || _guildBbsOfficialSessionBridgeConfiguredInboundOpcode == 0
                || _guildBbsOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextGuildBbsOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextGuildBbsOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + GuildBbsOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _guildBbsOfficialSessionBridge.TryRefreshFromDiscovery(
                _guildBbsOfficialSessionBridgeConfiguredListenPort,
                _guildBbsOfficialSessionBridgeConfiguredRemotePort,
                _guildBbsOfficialSessionBridgeConfiguredInboundOpcode,
                _guildBbsOfficialSessionBridgeConfiguredProcessSelector,
                _guildBbsOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainGuildBbsOfficialSessionBridge()
        {
            while (_guildBbsOfficialSessionBridge.TryDequeue(out MessengerOfficialSessionBridgeMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                string detail = _guildBbsRuntime.ApplyBoardPacket(message.Payload);
                bool applied = detail.StartsWith("Decoded Guild BBS board packet", StringComparison.OrdinalIgnoreCase);
                _guildBbsOfficialSessionBridge.RecordDispatchResult(message.Source, applied, $"CUIGuildBBS::OnGuildBBSPacket {message.Opcode}: {detail}");
                if (applied)
                {
                    WireGuildBbsWindowData();
                    _chat?.AddSystemMessage(detail, currTickCount);
                }
                else
                {
                    _chat?.AddErrorMessage(detail, currTickCount);
                }
            }
        }

        private ChatCommandHandler.CommandResult HandleGuildBbsSessionCommand(string[] args)
        {
            const string usage = "Usage: /guildbbs packet session [status|discover <remotePort> [inboundOpcode] [processName|pid] [localPort]|historyin [count]|clearhistoryin|historyout [count]|clearhistoryout|replay <historyIndex>|sendraw <hex>|send <register|comment|list|view|delete|deleteseq|replydelete [visibleIndex]|replydeleteseq [visibleIndex]|submit|reply>|queue <register|comment|list|view|delete|deleteseq|replydelete [visibleIndex]|replydeleteseq [visibleIndex]|submit|reply>|start <listenPort> <serverHost> <serverPort> [inboundOpcode]|startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]|stop]";
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeGuildBbsOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "discover", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int discoverRemotePort)
                    || discoverRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session discover <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                }

                int argumentIndex = 2;
                ushort inboundOpcode = GuildBbsInboundResultOpcode;
                if (args.Length >= 3 && ushort.TryParse(args[2], out ushort parsedInboundOpcode) && parsedInboundOpcode != 0)
                {
                    inboundOpcode = parsedInboundOpcode;
                    argumentIndex = 3;
                }

                string processSelector = args.Length > argumentIndex ? args[argumentIndex] : null;
                int? localPortFilter = null;
                if (args.Length > argumentIndex + 1)
                {
                    if (!int.TryParse(args[argumentIndex + 1], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session discover <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                return ChatCommandHandler.CommandResult.Info(
                    _guildBbsOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
            }

            if (string.Equals(args[0], "historyin", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session historyin [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_guildBbsOfficialSessionBridge.DescribeRecentInboundPackets(count));
            }

            if (string.Equals(args[0], "clearhistoryin", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_guildBbsOfficialSessionBridge.ClearRecentInboundPackets());
            }

            if (string.Equals(args[0], "historyout", StringComparison.OrdinalIgnoreCase))
            {
                int count = 10;
                if (args.Length >= 2 && (!int.TryParse(args[1], out count) || count <= 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session historyout [count]");
                }

                return ChatCommandHandler.CommandResult.Info(_guildBbsOfficialSessionBridge.DescribeRecentOutboundPackets(count));
            }

            if (string.Equals(args[0], "clearhistoryout", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Ok(_guildBbsOfficialSessionBridge.ClearRecentOutboundPackets());
            }

            if (string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2
                    || !int.TryParse(args[1], out int replayIndex)
                    || replayIndex <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session replay <historyIndex>");
                }

                return _guildBbsOfficialSessionBridge.TryReplayRecentOutboundPacket(replayIndex, out string replayStatus)
                    ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                    : ChatCommandHandler.CommandResult.Error(replayStatus);
            }

            if (string.Equals(args[0], "sendraw", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacket))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session sendraw <hex>");
                }

                return _guildBbsOfficialSessionBridge.TrySendOutboundRawPacket(rawPacket, out string sendRawStatus)
                    ? ChatCommandHandler.CommandResult.Ok(sendRawStatus)
                    : ChatCommandHandler.CommandResult.Error(sendRawStatus);
            }

            if (string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase))
            {
                bool queueOnly = string.Equals(args[0], "queue", StringComparison.OrdinalIgnoreCase);
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error(
                        $"Usage: /guildbbs packet session {args[0]} <register|comment|list|view|delete|deleteseq|replydelete [visibleIndex]|replydeleteseq [visibleIndex]|submit|reply>");
                }

                if (!TryResolveGuildBbsOutboundRequestPayloads(args, 1, out IReadOnlyList<byte[]> payloads, out string resolveStatus))
                {
                    return ChatCommandHandler.CommandResult.Error(resolveStatus);
                }

                if (payloads.Count == 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Guild BBS request preview did not produce any payloadhex fields.");
                }

                var statusBuilder = new StringBuilder();
                bool allSucceeded = true;
                for (int i = 0; i < payloads.Count; i++)
                {
                    byte[] payload = payloads[i] ?? Array.Empty<byte>();
                    bool dispatched;
                    string dispatchStatus;
                    if (queueOnly)
                    {
                        dispatched = _guildBbsOfficialSessionBridge.TryQueueOutboundPacket(
                            GuildBbsOutboundRequestOpcode,
                            payload,
                            out dispatchStatus);
                    }
                    else
                    {
                        dispatched = _guildBbsOfficialSessionBridge.TrySendOutboundPacket(
                            GuildBbsOutboundRequestOpcode,
                            payload,
                            out dispatchStatus);
                    }
                    if (!dispatched)
                    {
                        allSucceeded = false;
                    }

                    if (statusBuilder.Length > 0)
                    {
                        statusBuilder.Append(' ');
                    }

                    string modeLabel = queueOnly ? "queue" : "send";
                    statusBuilder.Append($"[{modeLabel} {i + 1}/{payloads.Count}] {dispatchStatus}");
                }

                return allSucceeded
                    ? ChatCommandHandler.CommandResult.Ok(statusBuilder.ToString())
                    : ChatCommandHandler.CommandResult.Error(statusBuilder.ToString());
            }

            if (string.Equals(args[0], "start", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], out int listenPort)
                    || listenPort <= 0
                    || !int.TryParse(args[3], out int remotePort)
                    || remotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session start <listenPort> <serverHost> <serverPort> [inboundOpcode]");
                }

                ushort inboundOpcode = GuildBbsInboundResultOpcode;
                if (args.Length >= 5 && (!ushort.TryParse(args[4], out inboundOpcode) || inboundOpcode == 0))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session start <listenPort> <serverHost> <serverPort> [inboundOpcode]");
                }

                _guildBbsOfficialSessionBridgeEnabled = true;
                _guildBbsOfficialSessionBridgeUseDiscovery = false;
                _guildBbsOfficialSessionBridgeConfiguredListenPort = listenPort;
                _guildBbsOfficialSessionBridgeConfiguredRemoteHost = args[2];
                _guildBbsOfficialSessionBridgeConfiguredRemotePort = remotePort;
                _guildBbsOfficialSessionBridgeConfiguredInboundOpcode = inboundOpcode;
                _guildBbsOfficialSessionBridgeConfiguredProcessSelector = null;
                _guildBbsOfficialSessionBridgeConfiguredLocalPort = null;
                EnsureGuildBbsOfficialSessionBridgeState(shouldRun: true);
                return ChatCommandHandler.CommandResult.Ok(DescribeGuildBbsOfficialSessionBridgeStatus());
            }

            if (string.Equals(args[0], "startauto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3
                    || !int.TryParse(args[1], out int autoListenPort)
                    || autoListenPort <= 0
                    || !int.TryParse(args[2], out int autoRemotePort)
                    || autoRemotePort <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                }

                int argumentIndex = 3;
                ushort autoInboundOpcode = GuildBbsInboundResultOpcode;
                if (args.Length >= 4 && ushort.TryParse(args[3], out ushort parsedInboundOpcode) && parsedInboundOpcode != 0)
                {
                    autoInboundOpcode = parsedInboundOpcode;
                    argumentIndex = 4;
                }

                string processSelector = args.Length > argumentIndex ? args[argumentIndex] : null;
                int? localPortFilter = null;
                if (args.Length > argumentIndex + 1)
                {
                    if (!int.TryParse(args[argumentIndex + 1], out int parsedLocalPort) || parsedLocalPort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet session startauto <listenPort> <remotePort> [inboundOpcode] [processName|pid] [localPort]");
                    }

                    localPortFilter = parsedLocalPort;
                }

                _guildBbsOfficialSessionBridgeEnabled = true;
                _guildBbsOfficialSessionBridgeUseDiscovery = true;
                _guildBbsOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                _guildBbsOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                _guildBbsOfficialSessionBridgeConfiguredInboundOpcode = autoInboundOpcode;
                _guildBbsOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _guildBbsOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                _guildBbsOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                _nextGuildBbsOfficialSessionBridgeDiscoveryRefreshAt = 0;
                return _guildBbsOfficialSessionBridge.TryRefreshFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        autoInboundOpcode,
                        processSelector,
                        localPortFilter,
                        out string startStatus)
                    ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeGuildBbsOfficialSessionBridgeStatus()}")
                    : ChatCommandHandler.CommandResult.Error(startStatus);
            }

            if (string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                _guildBbsOfficialSessionBridgeEnabled = false;
                _guildBbsOfficialSessionBridgeUseDiscovery = false;
                _guildBbsOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                _guildBbsOfficialSessionBridgeConfiguredRemotePort = 0;
                _guildBbsOfficialSessionBridgeConfiguredInboundOpcode = GuildBbsInboundResultOpcode;
                _guildBbsOfficialSessionBridgeConfiguredProcessSelector = null;
                _guildBbsOfficialSessionBridgeConfiguredLocalPort = null;
                _guildBbsOfficialSessionBridge.Stop();
                return ChatCommandHandler.CommandResult.Ok(DescribeGuildBbsOfficialSessionBridgeStatus());
            }

            return ChatCommandHandler.CommandResult.Error(usage);
        }

        private bool TryResolveGuildBbsOutboundRequestPayloads(
            string[] args,
            int commandStartIndex,
            out IReadOnlyList<byte[]> payloads,
            out string status)
        {
            payloads = Array.Empty<byte[]>();
            status = null;
            if (args == null || args.Length <= commandStartIndex)
            {
                status = "Guild BBS outbound request kind is required.";
                return false;
            }

            string requestKind = args[commandStartIndex]?.Trim() ?? string.Empty;
            string preview = requestKind.ToLowerInvariant() switch
            {
                "register" => _guildBbsRuntime.BuildClientRegisterRequestPreview(),
                "comment" => _guildBbsRuntime.BuildClientCommentRequestPreview(),
                "list" => _guildBbsRuntime.BuildClientLoadListRequestPreview(),
                "view" => _guildBbsRuntime.BuildClientViewEntryRequestPreview(),
                "delete" => _guildBbsRuntime.BuildClientDeleteRequestPreview(),
                "deleteseq" => _guildBbsRuntime.BuildClientDeleteSequencePreview(),
                "replydelete" => TryBuildGuildBbsReplyDeletePreview(args, commandStartIndex + 1, sequence: false, out string replyDeletePreview)
                    ? replyDeletePreview
                    : null,
                "replydeleteseq" => TryBuildGuildBbsReplyDeletePreview(args, commandStartIndex + 1, sequence: true, out string replyDeleteSequencePreview)
                    ? replyDeleteSequencePreview
                    : null,
                "submit" => _guildBbsRuntime.BuildClientSubmitSequencePreview(),
                "reply" => _guildBbsRuntime.BuildClientReplySequencePreview(),
                _ => null
            };

            if (preview == null)
            {
                status = "Usage: /guildbbs packet session send|queue <register|comment|list|view|delete|deleteseq|replydelete [visibleIndex]|replydeleteseq [visibleIndex]|submit|reply>";
                return false;
            }

            if (!preview.StartsWith("CUIGuildBBS ", StringComparison.Ordinal))
            {
                status = preview;
                return false;
            }

            if (!TryExtractGuildBbsPayloadHexSegments(preview, out List<byte[]> parsedPayloads))
            {
                status = $"Guild BBS preview did not expose decodable payloadhex data: {preview}";
                return false;
            }

            payloads = parsedPayloads;
            return true;
        }

        private bool TryBuildGuildBbsReplyDeletePreview(
            string[] args,
            int visibleIndexArgIndex,
            bool sequence,
            out string preview)
        {
            preview = null;
            if (args != null
                && args.Length > visibleIndexArgIndex
                && int.TryParse(args[visibleIndexArgIndex], out int visibleIndex))
            {
                preview = sequence
                    ? _guildBbsRuntime.BuildClientCommentDeleteSequencePreview(visibleIndex)
                    : _guildBbsRuntime.BuildClientCommentDeleteRequestPreview(visibleIndex);
                return true;
            }

            preview = sequence
                ? _guildBbsRuntime.BuildClientCommentDeleteSequencePreview()
                : _guildBbsRuntime.BuildClientCommentDeleteRequestPreview();
            return true;
        }

        private static bool TryExtractGuildBbsPayloadHexSegments(string preview, out List<byte[]> payloads)
        {
            payloads = new List<byte[]>();
            if (string.IsNullOrWhiteSpace(preview))
            {
                return false;
            }

            const string token = "payloadhex=";
            int searchIndex = 0;
            while (searchIndex < preview.Length)
            {
                int tokenIndex = preview.IndexOf(token, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (tokenIndex < 0)
                {
                    break;
                }

                int hexStart = tokenIndex + token.Length;
                int hexEnd = hexStart;
                while (hexEnd < preview.Length && IsHexCharacter(preview[hexEnd]))
                {
                    hexEnd++;
                }

                if (hexEnd > hexStart)
                {
                    string hex = preview.Substring(hexStart, hexEnd - hexStart);
                    try
                    {
                        payloads.Add(Convert.FromHexString(hex));
                    }
                    catch (FormatException)
                    {
                        // Keep scanning; malformed segments are ignored.
                    }
                }

                searchIndex = hexEnd;
            }

            return payloads.Count > 0;
        }

        private static bool IsHexCharacter(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }

        private ChatCommandHandler.CommandResult HandleGuildBbsClientRawPacketCommand(string[] args)
        {
            if (args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packetclientraw [authority|cash|board] <opcode-framed-hex>");
            }

            string target = null;
            int payloadStart = 0;
            if (string.Equals(args[0], "authority", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "cash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "board", StringComparison.OrdinalIgnoreCase))
            {
                target = args[0];
                payloadStart = 1;
            }

            if (args.Length <= payloadStart || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(payloadStart)), out byte[] rawPacket))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packetclientraw [authority|cash|board] <opcode-framed-hex>");
            }

            if (!AdminShopPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out string decodeError))
            {
                return ChatCommandHandler.CommandResult.Error(decodeError ?? "Guild BBS packetclientraw payload could not be decoded.");
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                if (packetType == _guildBbsOfficialSessionBridgeConfiguredInboundOpcode || packetType == GuildBbsInboundResultOpcode)
                {
                    target = "board";
                }
                else
                {
                    return ChatCommandHandler.CommandResult.Error(
                        $"Guild BBS packetclientraw opcode {packetType} is not mapped automatically. Use /guildbbs packetclientraw <authority|cash|board> <hex>, or send opcode {GuildBbsInboundResultOpcode} for CWvsContext::OnGuildBBSPacket.");
                }
            }

            string applyDetail = target.ToLowerInvariant() switch
            {
                "authority" => _guildBbsRuntime.ApplyPermissionPacket(payload),
                "cash" => _guildBbsRuntime.ApplyCashOwnershipPacket(payload),
                "board" => _guildBbsRuntime.ApplyBoardPacket(payload),
                _ => "Unsupported Guild BBS packet target."
            };

            bool applied = applyDetail.StartsWith("Decoded ", StringComparison.OrdinalIgnoreCase);
            return applied
                ? ChatCommandHandler.CommandResult.Ok($"Decoded client packet opcode {packetType} and applied Guild BBS {target} payload. {applyDetail}")
                : ChatCommandHandler.CommandResult.Error(applyDetail);
        }
    }
}
