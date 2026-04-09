using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Wz;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using MobItem = HaCreator.MapSimulator.Entities.MobItem;
using HaRepacker.Utils;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Combat;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;


namespace HaCreator.MapSimulator

{

    public partial class MapSimulator : Microsoft.Xna.Framework.Game

    {
        private ChatCommandHandler.CommandResult HandleSocialRoomEmployeeCommand(
            SocialRoomRuntime runtime,
            SocialRoomKind kind,
            string[] args,
            int actionIndex)
        {
            if (runtime == null)
            {
                return ChatCommandHandler.CommandResult.Error("Social-room runtime is unavailable.");
            }

            string employeeUsage = kind == SocialRoomKind.EntrustedShop
                ? "Usage: /socialroom entrustedshop [packet] employee <status|template <itemId|clear>|offset <x> <y>|world <x> <y>|facing <left|right|random>|packetraw <hex bytes>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|reset>"
                : "Usage: /socialroom personalshop [packet] employee <status|template <itemId|clear>|offset <x> <y>|world <x> <y>|facing <left|right|random>|packetraw <hex bytes>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|reset>";
            string employeeAction = args.Length > actionIndex + 1 ? args[actionIndex + 1] : "status";

            switch (employeeAction.ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info($"{runtime.DescribeStatus()}{Environment.NewLine}{_packetOwnedEmployeePoolDispatcher.DescribeStatus()}");

                case "template":
                    if (args.Length <= actionIndex + 2)
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    string templateToken = args[actionIndex + 2];
                    if (string.Equals(templateToken, "clear", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(templateToken, "reset", StringComparison.OrdinalIgnoreCase))
                    {
                        return runtime.TrySetEmployeeTemplate(0, out string clearMessage)
                            ? ChatCommandHandler.CommandResult.Ok(clearMessage)
                            : ChatCommandHandler.CommandResult.Error(clearMessage);
                    }

                    if (!int.TryParse(templateToken, out int templateId))
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    return runtime.TrySetEmployeeTemplate(templateId, out string templateMessage)
                        ? ChatCommandHandler.CommandResult.Ok(templateMessage)
                        : ChatCommandHandler.CommandResult.Error(templateMessage);

                case "offset":
                    if (args.Length <= actionIndex + 3
                        || !int.TryParse(args[actionIndex + 2], out int offsetX)
                        || !int.TryParse(args[actionIndex + 3], out int offsetY))
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    return runtime.TrySetEmployeeAnchorOffset(offsetX, offsetY, out string offsetMessage)
                        ? ChatCommandHandler.CommandResult.Ok(offsetMessage)
                        : ChatCommandHandler.CommandResult.Error(offsetMessage);

                case "world":
                    if (args.Length <= actionIndex + 3
                        || !int.TryParse(args[actionIndex + 2], out int worldX)
                        || !int.TryParse(args[actionIndex + 3], out int worldY))
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    return runtime.TrySetEmployeeWorldPosition(worldX, worldY, out string worldMessage)
                        ? ChatCommandHandler.CommandResult.Ok(worldMessage)
                        : ChatCommandHandler.CommandResult.Error(worldMessage);

                case "facing":
                    if (args.Length <= actionIndex + 2)
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    bool? flip = args[actionIndex + 2].ToLowerInvariant() switch
                    {
                        "left" => true,
                        "right" => false,
                        "random" => null,
                        _ => null
                    };

                    if (!string.Equals(args[actionIndex + 2], "left", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(args[actionIndex + 2], "right", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(args[actionIndex + 2], "random", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    return runtime.TrySetEmployeeFlip(flip, out string facingMessage)
                        ? ChatCommandHandler.CommandResult.Ok(facingMessage)
                        : ChatCommandHandler.CommandResult.Error(facingMessage);

                case "packetraw":
                    if (args.Length <= actionIndex + 2)
                    {
                        return ChatCommandHandler.CommandResult.Error(employeeUsage);
                    }

                    if (!MemoryGameField.TryParseMiniRoomPacketHex(string.Join(' ', args, actionIndex + 2, args.Length - actionIndex - 2), out byte[] packetBytes, out string packetParseError))
                    {
                        return ChatCommandHandler.CommandResult.Error(packetParseError);
                    }

                    bool packetApplied = _packetOwnedEmployeePoolDispatcher.TryApplyPacket(
                        SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeEnterFieldOpcode,
                        packetBytes,
                        kind,
                        currTickCount,
                        out string packetMessage);
                    if (packetApplied)
                    {
                        _packetOwnedEmployeePoolDispatcher.SyncRuntime(runtime, packetMessage, persistState: true);
                    }

                    return packetApplied
                        ? ChatCommandHandler.CommandResult.Ok(packetMessage)
                        : ChatCommandHandler.CommandResult.Error(packetMessage);

                case "session":
                    return HandleSocialRoomEmployeeSessionCommand(runtime, kind, args, actionIndex, employeeUsage);

                case "reset":
                    runtime.ResetEmployeePlacement();
                    return ChatCommandHandler.CommandResult.Ok(runtime.StatusMessage);

                default:
                    return ChatCommandHandler.CommandResult.Error(employeeUsage);
            }
        }

        private ChatCommandHandler.CommandResult HandleSocialRoomEmployeeSessionCommand(
            SocialRoomRuntime runtime,
            SocialRoomKind kind,
            string[] args,
            int actionIndex,
            string employeeUsage)
        {
            if (runtime == null)
            {
                return ChatCommandHandler.CommandResult.Error("Social-room runtime is unavailable.");
            }

            string sessionUsage = kind == SocialRoomKind.EntrustedShop
                ? "Usage: /socialroom entrustedshop [packet] employee session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]"
                : "Usage: /socialroom personalshop [packet] employee session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]";
            string sessionAction = args.Length > actionIndex + 2 ? args[actionIndex + 2] : "status";

            switch (sessionAction.ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info(
                        $"{runtime.DescribeStatus()}{Environment.NewLine}{_packetOwnedEmployeePoolDispatcher.DescribeStatus()}{Environment.NewLine}{_socialRoomEmployeeOfficialSessionBridge.LastStatus}");

                case "discover":
                    if (args.Length <= actionIndex + 3
                        || !int.TryParse(args[actionIndex + 3], out int discoverRemotePort)
                        || discoverRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    string processSelector = args.Length > actionIndex + 4 ? args[actionIndex + 4] : null;
                    int? localPortFilter = null;
                    if (args.Length > actionIndex + 5)
                    {
                        if (!int.TryParse(args[actionIndex + 5], out int parsedLocalPort) || parsedLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error(sessionUsage);
                        }

                        localPortFilter = parsedLocalPort;
                    }

                    return ChatCommandHandler.CommandResult.Info(
                        _socialRoomEmployeeOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));

                case "start":
                    if (args.Length <= actionIndex + 5
                        || !int.TryParse(args[actionIndex + 3], out int listenPort)
                        || listenPort <= 0
                        || !int.TryParse(args[actionIndex + 5], out int remotePort)
                        || remotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    _socialRoomEmployeeOfficialSessionBridge.Start(listenPort, args[actionIndex + 4], remotePort, kind);
                    return ChatCommandHandler.CommandResult.Ok(_socialRoomEmployeeOfficialSessionBridge.LastStatus);

                case "startauto":
                    if (args.Length <= actionIndex + 4
                        || !int.TryParse(args[actionIndex + 3], out int autoListenPort)
                        || autoListenPort <= 0
                        || !int.TryParse(args[actionIndex + 4], out int autoRemotePort)
                        || autoRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    string autoProcessSelector = args.Length > actionIndex + 5 ? args[actionIndex + 5] : null;
                    int? autoLocalPortFilter = null;
                    if (args.Length > actionIndex + 6)
                    {
                        if (!int.TryParse(args[actionIndex + 6], out int parsedAutoLocalPort) || parsedAutoLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error(sessionUsage);
                        }

                        autoLocalPortFilter = parsedAutoLocalPort;
                    }

                    return _socialRoomEmployeeOfficialSessionBridge.TryStartFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        autoProcessSelector,
                        autoLocalPortFilter,
                        preferredKind: kind,
                        out string startStatus)
                        ? ChatCommandHandler.CommandResult.Ok(startStatus)
                        : ChatCommandHandler.CommandResult.Error(startStatus);

                case "stop":
                    _socialRoomEmployeeOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(_socialRoomEmployeeOfficialSessionBridge.LastStatus);

                default:
                    return ChatCommandHandler.CommandResult.Error(string.IsNullOrWhiteSpace(sessionUsage) ? employeeUsage : sessionUsage);
            }
        }

        private ChatCommandHandler.CommandResult HandleTradingRoomInboxCommand(string[] args, int actionIndex)
        {
            const string inboxUsage = "Usage: /socialroom tradingroom [packet] inbox [status|start [port]|stop]";
            string inboxAction = args.Length > actionIndex + 1 ? args[actionIndex + 1] : "status";
            switch (inboxAction.ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info(DescribeTradingRoomPacketInboxStatus());

                case "start":
                {
                    int configuredPort = TradingRoomPacketInboxManager.DefaultPort;
                    if (args.Length > actionIndex + 2
                        && (!int.TryParse(args[actionIndex + 2], out configuredPort)
                            || configuredPort <= 0
                            || configuredPort > ushort.MaxValue))
                    {
                        return ChatCommandHandler.CommandResult.Error(inboxUsage);
                    }

                    _tradingRoomPacketInboxEnabled = true;
                    _tradingRoomPacketInboxConfiguredPort = configuredPort;
                    EnsureTradingRoomPacketInboxState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(DescribeTradingRoomPacketInboxStatus());
                }

                case "stop":
                    _tradingRoomPacketInboxEnabled = false;
                    EnsureTradingRoomPacketInboxState(shouldRun: false);
                    return ChatCommandHandler.CommandResult.Ok(DescribeTradingRoomPacketInboxStatus());

                default:
                    return ChatCommandHandler.CommandResult.Error(inboxUsage);
            }
        }

        private ChatCommandHandler.CommandResult HandleTransportSessionCommand(string[] args)
        {
            const string sessionUsage = "Usage: /transport session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|replay <historyIndex>|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]";
            string sessionAction = args.Length > 1 ? args[1] : "status";

            switch (sessionAction.ToLowerInvariant())
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info(
                        $"{_transportField.DescribeStatus()}{Environment.NewLine}{_transportPacketInbox.LastStatus}{Environment.NewLine}{_transportOfficialSessionBridge.DescribeStatus()}");

                case "discover":
                    if (args.Length < 3
                        || !int.TryParse(args[2], out int discoverRemotePort)
                        || discoverRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    string processSelector = args.Length >= 4 ? args[3] : null;
                    int? localPortFilter = null;
                    if (args.Length >= 5)
                    {
                        if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error(sessionUsage);
                        }

                        localPortFilter = parsedLocalPort;
                    }

                    return ChatCommandHandler.CommandResult.Info(
                        _transportOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));

                case "history":
                    int historyCount = 10;
                    if (args.Length >= 3
                        && (!int.TryParse(args[2], out historyCount) || historyCount <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    return ChatCommandHandler.CommandResult.Info(
                        _transportOfficialSessionBridge.DescribeRecentOutboundPackets(historyCount));

                case "clearhistory":
                    return ChatCommandHandler.CommandResult.Ok(
                        _transportOfficialSessionBridge.ClearRecentOutboundPackets());

                case "replay":
                    if (args.Length < 3
                        || !int.TryParse(args[2], out int replayIndex)
                        || replayIndex <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    return _transportOfficialSessionBridge.TryReplayRecentOutboundPacket(replayIndex, out string replayStatus)
                        ? ChatCommandHandler.CommandResult.Ok(replayStatus)
                        : ChatCommandHandler.CommandResult.Error(replayStatus);

                case "start":
                    if (args.Length < 5
                        || !int.TryParse(args[2], out int listenPort)
                        || listenPort <= 0
                        || !int.TryParse(args[4], out int remotePort)
                        || remotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    return _transportOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startStatus)
                        ? ChatCommandHandler.CommandResult.Ok(startStatus)
                        : ChatCommandHandler.CommandResult.Error(startStatus);

                case "startauto":
                    if (args.Length < 4
                        || !int.TryParse(args[2], out int autoListenPort)
                        || autoListenPort <= 0
                        || !int.TryParse(args[3], out int autoRemotePort)
                        || autoRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(sessionUsage);
                    }

                    string autoProcessSelector = args.Length >= 5 ? args[4] : null;
                    int? autoLocalPortFilter = null;
                    if (args.Length >= 6)
                    {
                        if (!int.TryParse(args[5], out int parsedAutoLocalPort) || parsedAutoLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error(sessionUsage);
                        }

                        autoLocalPortFilter = parsedAutoLocalPort;
                    }

                    return _transportOfficialSessionBridge.TryStartFromDiscovery(
                        autoListenPort,
                        autoRemotePort,
                        autoProcessSelector,
                        autoLocalPortFilter,
                        out string autoStartStatus)
                        ? ChatCommandHandler.CommandResult.Ok(autoStartStatus)
                        : ChatCommandHandler.CommandResult.Error(autoStartStatus);

                case "stop":
                    _transportOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(_transportOfficialSessionBridge.LastStatus);

                default:
                    return ChatCommandHandler.CommandResult.Error(sessionUsage);
            }
        }

        private static bool TryParseActorPlacement(string[] commandArgs, int xIndex, int yIndex, out Vector2 position, out string error)
        {
            position = Vector2.Zero;
            error = null;
            if (commandArgs == null
                || commandArgs.Length <= yIndex
                || !float.TryParse(commandArgs[xIndex], out float x)
                || !float.TryParse(commandArgs[yIndex], out float y))
            {
                error = "Ariant actor position requires numeric <x> <y> world coordinates.";
                return false;
            }

            position = new Vector2(x, y);
            return true;
        }

        private static bool TryParseActorFacingAndAction(string[] commandArgs, int startIndex, out string actionName, out bool? facingRight, out string error)
        {
            actionName = null;
            facingRight = null;
            error = null;

            for (int i = startIndex; i < (commandArgs?.Length ?? 0); i++)
            {
                string token = commandArgs[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (string.Equals(token, "left", StringComparison.OrdinalIgnoreCase))
                {
                    facingRight = false;
                    continue;
                }

                if (string.Equals(token, "right", StringComparison.OrdinalIgnoreCase))
                {
                    facingRight = true;
                    continue;
                }

                if (actionName == null)
                {
                    actionName = token;
                    continue;
                }

                error = $"Unexpected Ariant actor token '{token}'.";
                return false;
            }

            return true;
        }
        private bool TryConfigureLoginPacketPayload(
            LoginPacketType packetType,
            string[] args,
            out string error,
            out string summary)
        {
            error = null;
            summary = null;
            args = LoginPacketPayloadArgumentNormalizer.Normalize(packetType, args);


            switch (packetType)
            {
                case LoginPacketType.WorldInformation:
                    if (args.Length == 0)
                    {
                        _loginWorldInfoPacketProfiles.Clear();
                        summary = "Using generated WorldInformation metadata.";
                        return true;
                    }


                    if (args.Length == 1 &&
                        (args[0].Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                         args[0].Equals("reset", StringComparison.OrdinalIgnoreCase)))
                    {
                        _loginWorldInfoPacketProfiles.Clear();
                        summary = "Cleared packet-authored WorldInformation metadata.";
                        return true;
                    }


                    bool appendedPacketWorld = false;
                    bool receivedPacketTerminator = false;
                    foreach (string token in args)
                    {
                        if (token.Equals("append", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }


                        if (token.Equals("reset", StringComparison.OrdinalIgnoreCase))
                        {
                            _loginWorldInfoPacketProfiles.Clear();
                            continue;
                        }


                        if (TryParseWorldInfoPacketPayloadArgument(token, out LoginWorldInfoPacketProfile packetProfile, out bool isWorldInfoTerminator, out string worldInfoPayloadError))
                        {
                            if (isWorldInfoTerminator)
                            {
                                receivedPacketTerminator = true;
                                continue;
                            }


                            _loginWorldInfoPacketProfiles[packetProfile.WorldId] = packetProfile;
                            appendedPacketWorld = true;
                            continue;
                        }


                        if (worldInfoPayloadError != null)
                        {
                            error = worldInfoPayloadError;
                            return false;
                        }


                        if (!TryParseWorldInfoPacketProfile(token, out LoginWorldInfoPacketProfile profile))
                        {
                            error = "Usage: /loginpacket worldinfo [reset] <worldId:visibleChannels:occupancyPercent[:adult] ... | payloadhex=<hex> | payloadb64=<base64> | end>";
                            return false;
                        }


                        _loginWorldInfoPacketProfiles[profile.WorldId] = profile;

                    }



                    string loadedWorlds = _loginWorldInfoPacketProfiles.Count == 0
                        ? "none"
                        : string.Join(", ", _loginWorldInfoPacketProfiles.Keys.OrderBy(id => id));
                    summary = appendedPacketWorld || receivedPacketTerminator
                        ? receivedPacketTerminator
                            ? $"Applied streamed WorldInformation updates for {loadedWorlds} and received the client terminator."
                            : $"Applied streamed WorldInformation updates for {loadedWorlds}."
                        : $"Loaded packet-authored WorldInformation for {loadedWorlds}.";
                    return true;


                case LoginPacketType.RecommendWorldMessage:
                    _loginPacketRecommendedWorldIds.Clear();
                    _loginPacketRecommendedWorldMessages.Clear();
                    _loginPacketRecommendedWorldOrder.Clear();
                    if (args.Length == 0)
                    {
                        summary = "Using generated RecommendWorldMessage ordering.";
                        return true;
                    }


                    if (args.Length == 1 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        summary = "Cleared packet-authored recommended worlds.";
                        return true;
                    }


                    for (int i = 0; i < args.Length;)

                    {

                        if (TryParseRecommendWorldMessagePayloadArgument(
                                args[i],
                                out IReadOnlyList<LoginRecommendWorldMessageEntry> decodedEntries,
                                out bool clearPayload,
                                out string recommendPayloadError))
                        {
                            if (!clearPayload)
                            {
                                foreach (LoginRecommendWorldMessageEntry entry in decodedEntries)
                                {
                                    if (_loginPacketRecommendedWorldIds.Add(entry.WorldId))
                                    {
                                        _loginPacketRecommendedWorldOrder.Add(entry.WorldId);
                                    }

                                    if (!string.IsNullOrWhiteSpace(entry.Message))
                                    {
                                        _loginPacketRecommendedWorldMessages[entry.WorldId] = entry.Message.Replace("\\n", "\r\n", StringComparison.Ordinal);
                                    }
                                }
                            }

                            i++;

                            continue;
                        }

                        if (recommendPayloadError != null)
                        {
                            error = recommendPayloadError;

                            return false;
                        }

                        if (!TryParseRecommendWorldMessageEntry(args, ref i, out int worldId, out string message))
                        {

                            error = "Usage: /loginpacket recommendworld <worldId[=message] ... | payloadhex=<hex> | payloadb64=<base64> | clearpayload>";
                            return false;

                        }



                        if (_loginPacketRecommendedWorldIds.Add(worldId))
                        {
                            _loginPacketRecommendedWorldOrder.Add(worldId);
                        }
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            _loginPacketRecommendedWorldMessages[worldId] = message.Replace("\\n", "\r\n", StringComparison.Ordinal);
                        }
                    }


                    summary = $"Packet-authored recommendations: {string.Join(", ", _loginPacketRecommendedWorldOrder)}.";

                    return true;



                case LoginPacketType.LatestConnectedWorld:
                    if (args.Length == 0)
                    {
                        _loginPacketLatestConnectedWorldId = null;
                        summary = "Using generated LatestConnectedWorld focus.";
                        return true;
                    }


                    if (args.Length == 1 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _loginPacketLatestConnectedWorldId = null;
                        summary = "Cleared packet-authored latest connected world.";
                        return true;
                    }


                    string latestPayloadError = null;
                    if (args.Length == 1 &&
                        TryParseLatestConnectedWorldPayloadArgument(
                            args[0],
                            out int decodedLatestWorldId,
                            out bool clearLatestPayload,
                            out latestPayloadError))
                    {

                        _loginPacketLatestConnectedWorldId = clearLatestPayload ? null : decodedLatestWorldId;

                        summary = clearLatestPayload
                            ? "Cleared packet-authored latest connected world."
                            : $"Packet-authored latest connected world: {decodedLatestWorldId}.";

                        return true;
                    }

                    if (args.Length == 1 && latestPayloadError != null)
                    {

                        error = latestPayloadError;

                        return false;
                    }

                    if (!int.TryParse(args[0], out int latestWorldId) || latestWorldId < 0)
                    {

                        error = "Usage: /loginpacket latestworld <worldId | payloadhex=<hex> | payloadb64=<base64> | clearpayload>";
                        return false;

                    }



                    _loginPacketLatestConnectedWorldId = latestWorldId;

                    summary = $"Packet-authored latest connected world: {latestWorldId}.";

                    return true;



                case LoginPacketType.CheckUserLimitResult:

                    byte checkUserLimitResultCode = 0;

                    byte? populationLevel = null;
                    string checkUserLimitPayloadError = null;
                    if (args.Length == 1 &&
                        TryParseCheckUserLimitPayloadArgument(
                            args[0],
                            out byte decodedResultCode,
                            out byte? decodedPopulationLevel,
                            out bool clearCheckUserLimitPayload,
                            out checkUserLimitPayloadError))
                    {

                        _loginPacketCheckUserLimitResultCode = clearCheckUserLimitPayload ? null : decodedResultCode;

                        _loginPacketCheckUserLimitPopulationLevel = clearCheckUserLimitPayload ? null : decodedPopulationLevel;

                        summary = clearCheckUserLimitPayload
                            ? "Using generated CheckUserLimitResult behavior."
                            : $"Configured CheckUserLimitResult code {_loginPacketCheckUserLimitResultCode}"
                              + (_loginPacketCheckUserLimitPopulationLevel.HasValue ? $" with population level {_loginPacketCheckUserLimitPopulationLevel.Value}." : ".");

                        return true;
                    }

                    if (args.Length == 1 && checkUserLimitPayloadError != null)
                    {

                        error = checkUserLimitPayloadError;

                        return false;
                    }

                    if (args.Length > 0 && !byte.TryParse(args[0], out checkUserLimitResultCode))
                    {

                        error = "Usage: /loginpacket checkuserlimit [resultCode] [populationLevel] | [payloadhex=<hex> | payloadb64=<base64> | clearpayload]";
                        return false;

                    }



                    byte parsedPopulationLevel = 0;
                    if (args.Length > 1 && !byte.TryParse(args[1], out parsedPopulationLevel))
                    {

                        error = "Usage: /loginpacket checkuserlimit [resultCode] [populationLevel] | [payloadhex=<hex> | payloadb64=<base64> | clearpayload]";
                        return false;

                    }



                    if (args.Length > 1)
                    {

                        populationLevel = parsedPopulationLevel;
                    }

                    _loginPacketCheckUserLimitResultCode = args.Length > 0 ? checkUserLimitResultCode : null;
                    _loginPacketCheckUserLimitPopulationLevel = populationLevel;
                    summary = args.Length == 0
                        ? "Using generated CheckUserLimitResult behavior."
                        : $"Configured CheckUserLimitResult code {_loginPacketCheckUserLimitResultCode}"
                          + (_loginPacketCheckUserLimitPopulationLevel.HasValue ? $" with population level {_loginPacketCheckUserLimitPopulationLevel.Value}." : ".");
                    return true;
                case LoginPacketType.CheckPasswordResult:
                    return TryConfigureCheckPasswordPacketPayload(args, out error, out summary);
                case LoginPacketType.GuestIdLoginResult:
                    return TryConfigureGuestIdLoginPacketPayload(args, out error, out summary);
                case LoginPacketType.AccountInfoResult:
                case LoginPacketType.SetAccountResult:
                case LoginPacketType.ConfirmEulaResult:
                case LoginPacketType.CheckPinCodeResult:
                case LoginPacketType.UpdatePinCodeResult:
                case LoginPacketType.EnableSpwResult:
                case LoginPacketType.CheckSpwResult:
                case LoginPacketType.CheckDuplicatedIdResult:
                case LoginPacketType.CreateNewCharacterResult:
                    return packetType == LoginPacketType.CreateNewCharacterResult
                        ? TryConfigureCreateNewCharacterPacketPayload(args, out error, out summary)
                        : TryConfigureLoginAccountDialogPacketPayload(packetType, args, out error, out summary);
                case LoginPacketType.DeleteCharacterResult:

                    return TryConfigureLoginAccountDialogPacketPayload(packetType, args, out error, out summary);


                case LoginPacketType.SelectWorldResult:
                    return TryConfigureSelectWorldPacketPayload(args, out error, out summary);
                case LoginPacketType.SelectCharacterResult:
                    return TryConfigureSelectCharacterResultPacketPayload(args, out error, out summary);
                case LoginPacketType.SelectCharacterByVacResult:
                    return TryConfigureSelectCharacterByVacResultPacketPayload(args, out error, out summary);
                case LoginPacketType.ViewAllCharResult:
                    return TryConfigureViewAllCharPacketPayload(args, out error, out summary);


                case LoginPacketType.ExtraCharInfoResult:

                    return TryConfigureExtraCharInfoPacketPayload(args, out error, out summary);

            }



            return true;

        }





        private bool TryConfigureCheckPasswordPacketPayload(
            string[] args,
            out string error,
            out string summary)
        {
            error = null;
            summary = null;
            bool clearPayload = false;
            LoginCheckPasswordResultProfile decodedProfile = null;
            foreach (string arg in args)
            {
                if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))
                {
                    clearPayload = true;
                    continue;
                }


                if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string payloadError))
                {
                    if (!LoginCheckPasswordResultCodec.TryDecode(payloadBytes, out decodedProfile, out string decodeError))
                    {
                        error = decodeError ?? "CheckPasswordResult payload could not be decoded.";
                        return false;
                    }


                    continue;

                }



                if (payloadError != null &&
                    (arg.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))
                {
                    error = payloadError;
                    return false;
                }


                error = "Usage: /loginpacket checkpassword [payloadhex=<hex> | payloadb64=<base64> | clearpayload]";

                return false;

            }



            if (clearPayload)
            {
                _loginPacketCheckPasswordResultProfile = null;
                summary = "Using generated CheckPasswordResult behavior.";
            }


            if (decodedProfile != null)
            {
                _loginPacketCheckPasswordResultProfile = decodedProfile;
                summary = BuildConfiguredCheckPasswordPayloadSummary(decodedProfile);
            }


            if (decodedProfile != null || clearPayload)
            {
                return true;
            }


            summary = "Using generated CheckPasswordResult behavior.";

            return true;

        }



        private bool TryConfigureGuestIdLoginPacketPayload(
            string[] args,
            out string error,
            out string summary)
        {
            error = null;
            summary = null;
            bool clearPayload = false;
            LoginGuestIdLoginResultProfile decodedProfile = null;
            foreach (string arg in args)
            {
                if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))
                {
                    clearPayload = true;
                    continue;
                }


                if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string payloadError))
                {
                    if (!LoginGuestIdLoginResultCodec.TryDecode(payloadBytes, out decodedProfile, out string decodeError))
                    {
                        error = decodeError ?? "GuestIdLoginResult payload could not be decoded.";
                        return false;
                    }


                    continue;

                }



                if (payloadError != null &&
                    (arg.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))
                {
                    error = payloadError;
                    return false;
                }


                error = "Usage: /loginpacket guestlogin [payloadhex=<hex> | payloadb64=<base64> | clearpayload]";

                return false;

            }



            if (clearPayload)
            {
                _loginPacketGuestIdLoginResultProfile = null;
                summary = "Using generated GuestIdLoginResult behavior.";
            }


            if (decodedProfile != null)
            {
                _loginPacketGuestIdLoginResultProfile = decodedProfile;
                summary = BuildConfiguredGuestIdLoginPayloadSummary(decodedProfile);
            }


            if (decodedProfile != null || clearPayload)
            {
                return true;
            }


            summary = "Using generated GuestIdLoginResult behavior.";

            return true;

        }



        private static string BuildConfiguredCheckPasswordPayloadSummary(LoginCheckPasswordResultProfile profile)
        {
            if (profile == null)
            {
                return "Configured packet-authored CheckPasswordResult payload.";
            }


            string detail = string.Join(
                " | ",
                new[]
                {
                    $"Result code: {profile.ResultCode}",
                    $"Bootstrap mode: {profile.AccountBootstrapMode}",
                    profile.AccountId.HasValue ? $"Account id: {profile.AccountId.Value}" : null,
                    profile.CharacterCount.HasValue ? $"Character count: {profile.CharacterCount.Value}" : null,
                    profile.ClientKey.Length > 0 ? $"Client key: {Convert.ToHexString(profile.ClientKey)}" : null,
                }.Where(text => !string.IsNullOrWhiteSpace(text)));


            return string.IsNullOrWhiteSpace(detail)
                ? "Configured packet-authored CheckPasswordResult payload."
                : $"Configured packet-authored CheckPasswordResult payload ({detail}).";
        }


        private static string BuildConfiguredGuestIdLoginPayloadSummary(LoginGuestIdLoginResultProfile profile)
        {
            if (profile == null)
            {
                return "Configured packet-authored GuestIdLoginResult payload.";
            }


            string detail = string.Join(
                " | ",
                new[]
                {
                    $"Result code: {profile.ResultCode}",
                    $"Registration status: {profile.RegistrationStatusId}",
                    profile.AccountId.HasValue ? $"Account id: {profile.AccountId.Value}" : null,
                    profile.CharacterCount.HasValue ? $"Character count: {profile.CharacterCount.Value}" : null,
                    string.IsNullOrWhiteSpace(profile.GuestRegistrationUrl) ? null : "Guest registration URL present",
                }.Where(text => !string.IsNullOrWhiteSpace(text)));


            return string.IsNullOrWhiteSpace(detail)
                ? "Configured packet-authored GuestIdLoginResult payload."
                : $"Configured packet-authored GuestIdLoginResult payload ({detail}).";
        }


        private bool TryConfigureLoginAccountDialogPacketPayload(
            LoginPacketType packetType,
            string[] args,
            out string error,
            out string summary)
        {
            error = null;
            summary = null;
            List<string> promptArgs = new();
            bool clearPayload = false;
            LoginAccountDialogPacketProfile decodedProfile = null;
            foreach (string arg in args)
            {
                if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))
                {
                    clearPayload = true;
                    continue;
                }


                if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string payloadError))
                {
                    if (!LoginAccountDialogPacketCodec.TryDecode(packetType, payloadBytes, out decodedProfile, out string decodeError))
                    {
                        error = decodeError ?? $"{packetType} payload could not be decoded.";
                        return false;
                    }


                    continue;

                }



                if (payloadError != null &&
                    (arg.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase) ||
                     arg.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))
                {
                    error = payloadError;
                    return false;
                }


                promptArgs.Add(arg);

            }



            if (clearPayload)
            {
                _loginPacketAccountDialogProfiles.Remove(packetType);
                summary = $"Cleared packet-authored {packetType} account-dialog payload.";
            }


            if (decodedProfile != null)
            {
                _loginPacketAccountDialogProfiles[packetType] = WithLoginAccountDialogPacketSource(decodedProfile, "loginpacket-command");
                summary = BuildConfiguredLoginAccountDialogPayloadSummary(decodedProfile);
            }


            if (promptArgs.Count == 0)
            {
                if (decodedProfile != null || clearPayload)
                {
                    return true;
                }


                _loginPacketDialogPrompts.Remove(packetType);
                summary = $"Using the default {packetType} dialog behavior.";
                return true;
            }


            if (!TryConfigureLoginPacketDialogPrompt(packetType, promptArgs.ToArray(), out error, out string promptSummary))
            {
                return false;
            }


            summary = string.IsNullOrWhiteSpace(summary)
                ? promptSummary
                : string.IsNullOrWhiteSpace(promptSummary)
                    ? summary
                    : $"{summary} {promptSummary}";
            return true;
        }


        private static string BuildConfiguredLoginAccountDialogPayloadSummary(LoginAccountDialogPacketProfile profile)
        {
            if (profile == null)
            {
                return "Configured packet-authored login account dialog payload.";
            }


            string inlineSummary = LoginAccountDialogPacketProfileFormatter.BuildInlineSummary(profile);
            if (!string.IsNullOrWhiteSpace(profile.TextValue))
            {
                inlineSummary = string.IsNullOrWhiteSpace(inlineSummary)
                    ? "text override present"
                    : inlineSummary + " | Text override present";
            }


            return string.IsNullOrWhiteSpace(inlineSummary)
                ? $"Configured packet-authored {profile.PacketType} payload."
                : $"Configured packet-authored {profile.PacketType} payload ({inlineSummary}).";
        }


        private static bool TryParseLoginPacketDialogPrompt(
            string[] args,
            out LoginPacketDialogPromptConfiguration prompt,
            out string error)
        {
            prompt = null;
            error = null;


            LoginPacketDialogOwner owner = LoginPacketDialogOwner.LoginUtilityDialog;
            bool hasExplicitOwner = false;
            string title = null;
            string body = null;
            int? noticeTextIndex = null;
            ConnectionNoticeWindowVariant? noticeVariant = null;
            LoginUtilityDialogButtonLayout? buttonLayout = null;
            string primaryLabel = null;
            string secondaryLabel = null;
            string inputLabel = null;
            string inputPlaceholder = null;
            bool inputMasked = false;
            int inputMaxLength = 0;
            SoftKeyboardKeyboardType softKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric;
            LoginUtilityDialogVisualStyle visualStyle = LoginUtilityDialogVisualStyle.Default;
            Rectangle? inputBoundsOverride = null;
            int durationMs = 2400;


            for (int i = 0; i < args.Length; i++)
            {
                if (!TrySplitLoginPacketPromptArgument(args[i], out string key, out string initialValue))
                {
                    error = "Usage: /loginpacket <packet> [payloadhex=<hex>|payloadb64=<base64>|clearpayload] [mode=utility|notice] [title=<text>] [body=<text>] [notice=<index>] [variant=notice|noticecog|loading|loadingsinglegauge] [buttons=ok|yesno|accept|nowlater|restartexit|nexon|enabledisablespw] [visualstyle=default|securityyesno|secondarypasswordchoice] [primary=<label>] [secondary=<label>] [inputlabel=<text>] [placeholder=<text>] [masked=true|false] [maxlength=<count>] [keyboardtype=alphanumeric|alphaedges|numeric|numericalt] [inputbounds=<x,y,w,h>] [duration=<ms>]";
                    return false;

                }



                string value = CollectLoginPacketPromptValue(args, ref i, key, initialValue);
                switch (key)
                {
                    case "mode":
                        if (value.Equals("notice", StringComparison.OrdinalIgnoreCase))
                        {
                            owner = LoginPacketDialogOwner.ConnectionNotice;
                            hasExplicitOwner = true;
                        }
                        else if (value.Equals("utility", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("dialog", StringComparison.OrdinalIgnoreCase))
                        {
                            owner = LoginPacketDialogOwner.LoginUtilityDialog;
                            hasExplicitOwner = true;
                        }
                        else
                        {
                            error = "mode must be utility or notice.";
                            return false;
                        }
                        break;


                    case "title":

                        title = DecodeLoginPacketPromptText(value);

                        break;



                    case "body":

                        body = DecodeLoginPacketPromptText(value);

                        break;



                    case "notice":
                        if (!int.TryParse(value, out int parsedNotice) || parsedNotice < 0)
                        {
                            error = "notice must be a non-negative Login.img/Notice/text index.";
                            return false;
                        }


                        noticeTextIndex = parsedNotice;

                        break;



                    case "variant":
                        if (!LoginEntryTryParseConnectionNoticeVariant(value, out ConnectionNoticeWindowVariant parsedVariant))
                        {
                            error = "variant must be notice, noticecog, loading, or loadingsinglegauge.";
                            return false;
                        }


                        noticeVariant = parsedVariant;

                        break;



                    case "buttons":

                        if (!TryParseLoginUtilityButtonLayout(value, out LoginUtilityDialogButtonLayout parsedButtonLayout))

                        {

                            error = "buttons must be ok, yesno, accept, nowlater, restartexit, nexon, or enabledisablespw.";
                            return false;

                        }



                        buttonLayout = parsedButtonLayout;

                        break;

                    case "visualstyle":
                    case "style":
                        if (!TryParseLoginUtilityDialogVisualStyle(value, out LoginUtilityDialogVisualStyle parsedVisualStyle))
                        {
                            error = "visualstyle must be default, securityyesno, or secondarypasswordchoice.";
                            return false;
                        }

                        visualStyle = parsedVisualStyle;
                        break;



                    case "primary":

                        primaryLabel = DecodeLoginPacketPromptText(value);

                        break;



                    case "secondary":

                        secondaryLabel = DecodeLoginPacketPromptText(value);

                        break;



                    case "inputlabel":
                    case "input":
                        inputLabel = DecodeLoginPacketPromptText(value);
                        break;


                    case "placeholder":
                    case "hint":
                        inputPlaceholder = DecodeLoginPacketPromptText(value);
                        break;


                    case "masked":
                        if (!bool.TryParse(value, out inputMasked))
                        {
                            error = "masked must be true or false.";
                            return false;
                        }
                        break;


                    case "maxlength":
                    case "maxlen":
                        if (!int.TryParse(value, out inputMaxLength) || inputMaxLength < 0)
                        {
                            error = "maxlength must be a non-negative integer.";
                            return false;
                        }
                        break;


                    case "keyboardtype":
                        if (!TryParseSoftKeyboardKeyboardType(value, out softKeyboardType))
                        {
                            error = "keyboardtype must be alphanumeric, alphaedges, numeric, or numericalt.";
                            return false;
                        }
                        break;

                    case "inputbounds":
                    case "bounds":
                        if (!TryParseLoginPacketPromptRectangle(value, out Rectangle parsedBounds))
                        {
                            error = "inputbounds must be x,y,width,height.";
                            return false;
                        }

                        inputBoundsOverride = parsedBounds;
                        break;


                    case "duration":
                        if (!int.TryParse(value, out durationMs) || durationMs < 0)
                        {
                            error = "duration must be a non-negative millisecond value.";
                            return false;
                        }
                        break;


                    default:
                        error = $"Unsupported dialog override option '{key}'.";
                        return false;
                }
            }


            prompt = new LoginPacketDialogPromptConfiguration
            {
                Owner = owner,
                HasExplicitOwner = hasExplicitOwner,
                Title = title,
                Body = body,
                NoticeTextIndex = noticeTextIndex,
                NoticeVariant = noticeVariant,
                ButtonLayout = buttonLayout,
                VisualStyle = visualStyle,
                PrimaryLabel = primaryLabel,
                SecondaryLabel = secondaryLabel,
                InputLabel = inputLabel,
                InputPlaceholder = inputPlaceholder,
                InputMasked = inputMasked,
                InputMaxLength = inputMaxLength,
                SoftKeyboardType = softKeyboardType,
                InputBoundsOverride = inputBoundsOverride,
                DurationMs = durationMs,
            };
            return true;
        }

        private static bool TryParseLoginUtilityDialogVisualStyle(string value, out LoginUtilityDialogVisualStyle visualStyle)
        {
            visualStyle = LoginUtilityDialogVisualStyle.Default;
            string normalized = (value ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "default" => Assign(LoginUtilityDialogVisualStyle.Default, out visualStyle),
                "securityyesno" => Assign(LoginUtilityDialogVisualStyle.SecurityYesNo, out visualStyle),
                "secondarypasswordchoice" or "secondarypassword" or "spwchoice" => Assign(LoginUtilityDialogVisualStyle.SecondaryPasswordChoice, out visualStyle),
                _ => Enum.TryParse(value, true, out visualStyle),
            };
        }

        private static bool Assign<T>(T value, out T result)
        {
            result = value;
            return true;
        }

        private static bool TryParseLoginPacketPromptRectangle(string value, out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;
            string[] parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], out int x) ||
                !int.TryParse(parts[1], out int y) ||
                !int.TryParse(parts[2], out int width) ||
                !int.TryParse(parts[3], out int height) ||
                width <= 0 ||
                height <= 0)
            {
                return false;
            }

            rectangle = new Rectangle(x, y, width, height);
            return true;
        }
        private static bool TryParseSoftKeyboardKeyboardType(string value, out SoftKeyboardKeyboardType keyboardType)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "alphanumeric":
                case "type0":
                case "0":
                    keyboardType = SoftKeyboardKeyboardType.AlphaNumeric;
                    return true;
                case "alphaedges":
                case "alphaedge":
                case "type1":
                case "1":
                    keyboardType = SoftKeyboardKeyboardType.AlphaNumericWithAlphaEdges;
                    return true;
                case "numeric":
                case "type2":
                case "2":
                    keyboardType = SoftKeyboardKeyboardType.NumericOnly;
                    return true;
                case "numericalt":
                case "numeric-alt":
                case "type3":
                case "3":
                    keyboardType = SoftKeyboardKeyboardType.NumericOnlyAlt;
                    return true;
                default:
                    keyboardType = SoftKeyboardKeyboardType.AlphaNumeric;
                    return false;
            }
        }




        /// <summary>
        /// Registers all chat commands
        /// </summary>
        private void RegisterChatCommands()
        {
            _chat.CommandHandler.RegisterCommand(
                "login",
                "Show the login bootstrap runtime state",
                "/login",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }


                    return ChatCommandHandler.CommandResult.Info(
                        _loginRuntime.DescribeStatus()
                        + Environment.NewLine
                        + $"Adult access: {(_loginAccountIsAdult ? "enabled" : "disabled")}");
                });


            _chat.CommandHandler.RegisterCommand(
                "loginstep",
                "Force the login runtime to a specific step",
                "/loginstep <title|world|char|newchar|avatar|vac|enter>",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }


                    if (args.Length == 0 || !LoginRuntimeManager.TryParseStep(args[0], out LoginStep step))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginstep <title|world|char|newchar|avatar|vac|enter>");
                    }


                    _loginRuntime.ForceStep(step, "Manual login step override");
                    SyncLoginTitleWindow();
                    RefreshWorldChannelSelectorWindows();
                    SyncLoginCharacterSelectWindow();
                    return ChatCommandHandler.CommandResult.Ok(_loginRuntime.DescribeStatus());
                });


            _chat.CommandHandler.RegisterCommand(
                "loginadult",
                "Toggle simulated adult-channel access for the login selectors",
                "/loginadult <on|off>",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"Adult access is currently {(_loginAccountIsAdult ? "enabled" : "disabled")}.");
                    }


                    string normalized = args[0].Trim().ToLowerInvariant();
                    if (normalized != "on" && normalized != "off")
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginadult <on|off>");
                    }


                    _loginAccountIsAdult = normalized == "on";
                    _selectorLastResultCode = SelectorRequestResultCode.None;
                    _selectorLastResultMessage = null;
                    RefreshWorldChannelSelectorWindows();
                    SyncRecommendWorldWindow();
                    return ChatCommandHandler.CommandResult.Ok($"Adult access {(_loginAccountIsAdult ? "enabled" : "disabled")}.");
                });


            _chat.CommandHandler.RegisterCommand(

                "loginpacket",

                "Dispatch or configure a login bootstrap packet into the runtime",

                "/loginpacket <inbox|session|stream|checkpassword|guestlogin|accountinfo|checkuserlimit|setaccount|eula|checkpin|updatepin|worldinfo|selectworld|selectchar|checkduplicatedid|newcharresult|deletechar|enablespw|vac|recommendworld|latestworld|extracharinfo|checkspw> [...]",
                args =>
                {
                    if (args.Length > 0 && string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(DescribeLoginPacketInboxStatus());
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int configuredPort = LoginPacketInboxManager.DefaultPort;
                            if (args.Length > 2 && (!int.TryParse(args[2], out configuredPort) || configuredPort <= 0 || configuredPort > ushort.MaxValue))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket inbox start [port]");
                            }


                            _loginPacketInboxEnabled = true;
                            _loginPacketInboxConfiguredPort = configuredPort;
                            EnsureLoginPacketInboxState(true);
                            return ChatCommandHandler.CommandResult.Ok(DescribeLoginPacketInboxStatus());
                        }


                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _loginPacketInboxEnabled = false;
                            EnsureLoginPacketInboxState(false);
                            return ChatCommandHandler.CommandResult.Ok(DescribeLoginPacketInboxStatus());
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket inbox [status|start [port]|stop]");

                    }

                    if (args.Length > 0 && string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(DescribeLoginPacketTransportStatus());
                        }

                        if (string.Equals(args[1], "map", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int opcode)
                                || opcode <= 0
                                || !LoginRuntimeManager.TryParsePacketType(args[3], out LoginPacketType mappedPacketType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session map <opcode> <checkpassword|guestlogin|accountinfo|checkuserlimit|setaccount|eula|checkpin|updatepin|viewallchar|vac|worldinfo|selectworld|selectchar|checkduplicatedid|newcharresult|deletechar|enablespw|latestworld|recommendworld|extracharinfo|checkspw|setfield|setitc|setcashshop|setbackeffect|setmapobjectvisible|clearbackeffect>");
                            }

                            return _loginOfficialSessionBridge.TryConfigurePacketMapping(opcode, mappedPacketType, out string mapStatus)
                                ? ChatCommandHandler.CommandResult.Ok(mapStatus)
                                : ChatCommandHandler.CommandResult.Error(mapStatus);
                        }

                        if (string.Equals(args[1], "unmap", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int opcode)
                                || opcode <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session unmap <opcode>");
                            }

                            return _loginOfficialSessionBridge.RemovePacketMapping(opcode, out string unmapStatus)
                                ? ChatCommandHandler.CommandResult.Ok(unmapStatus)
                                : ChatCommandHandler.CommandResult.Error(unmapStatus);
                        }

                        if (string.Equals(args[1], "clearmap", StringComparison.OrdinalIgnoreCase))
                        {
                            _loginOfficialSessionBridge.ClearPacketMappings();
                            return ChatCommandHandler.CommandResult.Ok(DescribeLoginOfficialSessionBridgeStatus());
                        }

                        if (string.Equals(args[1], "recent", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"Login session recent packets: {_loginOfficialSessionBridge.DescribeRecentPackets()}");
                        }

                        if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int discoverRemotePort)
                                || discoverRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session discover <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 4 ? args[3] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 5)
                            {
                                if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session discover <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return ChatCommandHandler.CommandResult.Info(
                                _loginOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                        }

                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 5
                                || !int.TryParse(args[2], out int listenPort)
                                || listenPort <= 0
                                || !int.TryParse(args[4], out int remotePort)
                                || remotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session start <listenPort> <serverHost> <serverPort>");
                            }

                            _loginOfficialSessionBridgeEnabled = true;
                            _loginOfficialSessionBridgeUseDiscovery = false;
                            _loginOfficialSessionBridgeConfiguredListenPort = listenPort;
                            _loginOfficialSessionBridgeConfiguredRemoteHost = args[3];
                            _loginOfficialSessionBridgeConfiguredRemotePort = remotePort;
                            _loginOfficialSessionBridgeConfiguredProcessSelector = null;
                            _loginOfficialSessionBridgeConfiguredLocalPort = null;
                            EnsureLoginPacketInboxState(IsLoginRuntimeSceneActive);
                            return ChatCommandHandler.CommandResult.Ok(DescribeLoginOfficialSessionBridgeStatus());
                        }

                        if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int autoListenPort)
                                || autoListenPort <= 0
                                || !int.TryParse(args[3], out int autoRemotePort)
                                || autoRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 5 ? args[4] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 6)
                            {
                                if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            _loginOfficialSessionBridgeEnabled = true;
                            _loginOfficialSessionBridgeUseDiscovery = true;
                            _loginOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                            _loginOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                            _loginOfficialSessionBridgeConfiguredProcessSelector = processSelector;
                            _loginOfficialSessionBridgeConfiguredLocalPort = localPortFilter;
                            EnsureLoginPacketInboxState(IsLoginRuntimeSceneActive);

                            if (!IsLoginRuntimeSceneActive)
                            {
                                return ChatCommandHandler.CommandResult.Ok(DescribeLoginOfficialSessionBridgeStatus());
                            }

                            return _loginOfficialSessionBridge.TryStartFromDiscovery(
                                autoListenPort,
                                autoRemotePort,
                                processSelector,
                                localPortFilter,
                                out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }

                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _loginOfficialSessionBridgeEnabled = false;
                            _loginOfficialSessionBridgeConfiguredProcessSelector = null;
                            _loginOfficialSessionBridgeConfiguredLocalPort = null;
                            EnsureLoginPacketInboxState(IsLoginRuntimeSceneActive);
                            return ChatCommandHandler.CommandResult.Ok(DescribeLoginOfficialSessionBridgeStatus());
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket session [status|map <opcode> <packet>|unmap <opcode>|clearmap|recent|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
                    }



                    if (!IsLoginRuntimeSceneActive)
                    {

                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");

                    }

                    if (args.Length > 0 &&
                        (string.Equals(args[0], "stream", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(args[0], "script", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(args[0], "sequence", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!LoginPacketScriptCodec.TryDecodeArguments(
                                args.Skip(1).ToArray(),
                                "login-ui",
                                out IReadOnlyList<LoginPacketInboxMessage> scriptMessages,
                                out string scriptError))
                        {
                            return ChatCommandHandler.CommandResult.Error(scriptError);
                        }

                        if (scriptMessages.Count == 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("The login packet script did not contain any dispatchable packets.");
                        }

                        List<string> dispatchedPackets = new(scriptMessages.Count);
                        foreach (LoginPacketInboxMessage scriptMessage in scriptMessages)
                        {
                            if (!TryConfigureLoginPacketPayload(scriptMessage.PacketType, scriptMessage.Arguments, out string scriptPayloadError, out _))
                            {
                                return ChatCommandHandler.CommandResult.Error(scriptPayloadError ?? $"Failed to configure {scriptMessage.PacketType} from the login packet script.");
                            }

                            DispatchLoginRuntimePacket(scriptMessage.PacketType, out _);
                            dispatchedPackets.Add(scriptMessage.PacketType.ToString());
                        }

                        string packetSummary = string.Join(", ", dispatchedPackets);
                        return ChatCommandHandler.CommandResult.Ok($"Dispatched {scriptMessages.Count} login packet(s) from the scripted stream: {packetSummary}.");
                    }



                    if (args.Length == 0 || !LoginRuntimeManager.TryParsePacketType(args[0], out LoginPacketType packetType))

                    {

                        return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket <inbox|session|stream|checkpassword|guestlogin|accountinfo|checkuserlimit|setaccount|eula|checkpin|updatepin|worldinfo|selectworld|selectchar|checkduplicatedid|newcharresult|deletechar|enablespw|vac|recommendworld|latestworld|extracharinfo|checkspw> [...]");
                    }



                    if (!TryConfigureLoginPacketPayload(packetType, args.Skip(1).ToArray(), out string payloadError, out string payloadSummary))
                    {
                        return ChatCommandHandler.CommandResult.Error(payloadError);
                    }


                    DispatchLoginRuntimePacket(packetType, out string message);
                    return ChatCommandHandler.CommandResult.Ok(string.IsNullOrWhiteSpace(payloadSummary)
                        ? message
                        : $"{message} {payloadSummary}");
                });


            // /map <id> - Change to a different map
            _chat.CommandHandler.RegisterCommand(
                "map",
                "Teleport to a map by ID",
                "/map <mapId>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /map <mapId>");
                    }


                    if (!int.TryParse(args[0], out int mapId))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid map ID: {args[0]}");
                    }


                    if (_loadMapCallback == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Map loading not available");
                    }


                    if (!QueueMapTransfer(mapId, null))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Unable to queue map change to {mapId}.");
                    }


                    return ChatCommandHandler.CommandResult.Ok($"Loading map {mapId}...");

                });

            _chat.CommandHandler.RegisterCommand(
                "maptransfer",
                "Inspect or drive the map-transfer packet/session bridge",
                "/maptransfer [status|session ...|packet result <resultCode> <regular|continent> [mapIds...]]",
                HandleMapTransferCommand);



            // /job <jobid> - Change the active player job and refocus skill UI
            _chat.CommandHandler.RegisterCommand(
                "job",
                "Change the player's job ID",
                "/job <jobId>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /job <jobId>");
                    }


                    if (!int.TryParse(args[0], out int jobId))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid job ID: {args[0]}");
                    }


                    if (jobId < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Job ID must be non-negative");
                    }


                    if (!TrySetPlayerJob(jobId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Player not available");
                    }


                    string jobName = _playerManager?.Player?.Build?.JobName ?? SkillDataLoader.GetJobName(jobId);

                    return ChatCommandHandler.CommandResult.Ok($"Changed job to {jobName} ({jobId})");

                });



            // /pos - Show current camera position
            _chat.CommandHandler.RegisterCommand(
                "pos",
                "Show current camera position",
                "/pos",
                args =>
                {
                    return ChatCommandHandler.CommandResult.Info($"Camera: X={mapShiftX}, Y={mapShiftY}");
                });


            _chat.CommandHandler.RegisterCommand(

                "wedding",

                "Inspect or drive the wedding ceremony runtime",

                "/wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y> [action] [left|right]|actor avatar <groom|bride> <x> <y> <avatarLookHex> [action] [left|right]|guest <add|avatar|move|remove|clear|status> ...|inbox [status|start [port]|stop]|end]",
                args =>
                {
                    WeddingField wedding = _specialFieldRuntime.SpecialEffects.Wedding;
                    if (!wedding.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Wedding runtime is only active on wedding maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(wedding.DescribeStatus());
                    }


                    if (string.Equals(args[0], "progress", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[1], out int step)
                            || !int.TryParse(args[2], out int groomId)
                            || !int.TryParse(args[3], out int brideId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding progress <step> <groomId> <brideId>");
                        }


                        wedding.OnWeddingProgress(step, groomId, brideId, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());

                    }



                    if (string.Equals(args[0], "respond", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding respond <yes|no>");
                        }


                        bool accepted;
                        if (string.Equals(args[1], "yes", StringComparison.OrdinalIgnoreCase))
                        {
                            accepted = true;
                        }
                        else if (string.Equals(args[1], "no", StringComparison.OrdinalIgnoreCase))
                        {
                            accepted = false;
                        }
                        else
                        {
                            return ChatCommandHandler.CommandResult.Error("Wedding response must be yes or no");
                        }


                        WeddingPacketResponse? response = wedding.RespondToCurrentDialog(accepted, currTickCount);
                        return response.HasValue
                            ? ChatCommandHandler.CommandResult.Ok($"{wedding.DescribeStatus()} Sent packet {response.Value}.")
                            : ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }
                    if (string.Equals(args[0], "actor", StringComparison.OrdinalIgnoreCase)
                        && args.Length >= 2
                        && string.Equals(args[1], "avatar", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 6)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding actor avatar <groom|bride> <x> <y> <avatarLookHex> [action] [left|right]");
                        }

                        if (!TryParseActorPlacement(args, 3, 4, out Vector2 avatarPosition, out string avatarPlacementError))
                        {
                            return ChatCommandHandler.CommandResult.Error(avatarPlacementError);
                        }

                        if (!TryParseActorFacingAndAction(args, 6, out string avatarActionName, out bool? avatarFacingRight, out string avatarParseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(avatarParseError);
                        }

                        byte[] avatarPayload;
                        try
                        {
                            avatarPayload = ByteUtils.HexToBytes(args[5]);
                        }
                        catch (Exception ex)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid AvatarLook hex payload: {ex.Message}");
                        }

                        if (!TryCreateWeddingAvatarBuild(args[2], avatarPayload, out CharacterBuild avatarBuild, out string avatarBuildError))
                        {
                            return ChatCommandHandler.CommandResult.Error(avatarBuildError);
                        }

                        if (!TryResolveWeddingParticipantId(wedding, args[2], out int avatarParticipantId, out string avatarIdError))
                        {
                            return ChatCommandHandler.CommandResult.Error(avatarIdError);
                        }

                        if (!wedding.TryConfigureParticipantActor(
                            avatarParticipantId,
                            avatarPosition,
                            avatarBuild,
                            avatarFacingRight,
                            avatarActionName,
                            out string avatarConfigureError))
                        {
                            return ChatCommandHandler.CommandResult.Error(avatarConfigureError);
                        }

                        SyncWeddingRemoteActorsToSharedPool(wedding);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    if (string.Equals(args[0], "actor", StringComparison.OrdinalIgnoreCase) && args.Length >= 5)
                    {
                        if (!TryParseActorPlacement(args, 2, 3, out Vector2 actorPosition, out string actorPlacementError))
                        {
                            return ChatCommandHandler.CommandResult.Error(actorPlacementError);
                        }

                        if (!TryParseActorFacingAndAction(args, 4, out string actorActionName, out bool? actorFacingRight, out string actorParseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(actorParseError);
                        }

                        if (!TryResolveWeddingParticipantId(wedding, args[1], out int actorParticipantId, out string actorIdError))
                        {
                            return ChatCommandHandler.CommandResult.Error(actorIdError);
                        }

                        if (!wedding.TryConfigureParticipantActor(
                            actorParticipantId,
                            actorPosition,
                            build: null,
                            actorFacingRight,
                            actorActionName,
                            out string actorConfigureError))
                        {
                            return ChatCommandHandler.CommandResult.Error(actorConfigureError);
                        }

                        SyncWeddingRemoteActorsToSharedPool(wedding);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    if (string.Equals(args[0], "actor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[2], out int actorX)
                            || !int.TryParse(args[3], out int actorY))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding actor <groom|bride> <x> <y>");
                        }


                        if (string.Equals(args[1], "groom", StringComparison.OrdinalIgnoreCase))
                        {
                            if (wedding.GroomId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding groom ID is not set yet. Use /wedding progress first.");
                            }


                            wedding.SetParticipantPosition(wedding.GroomId, new Vector2(actorX, actorY));
                            SyncWeddingRemoteActorsToSharedPool(wedding);

                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());

                        }



                        if (string.Equals(args[1], "bride", StringComparison.OrdinalIgnoreCase))
                        {
                            if (wedding.BrideId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding bride ID is not set yet. Use /wedding progress first.");
                            }


                            wedding.SetParticipantPosition(wedding.BrideId, new Vector2(actorX, actorY));
                            SyncWeddingRemoteActorsToSharedPool(wedding);

                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());

                        }



                        return ChatCommandHandler.CommandResult.Error("Wedding actor must be groom or bride");

                    }



                    if (string.Equals(args[0], "guest", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(wedding.DescribeStatus());
                        }

                        if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                        {
                            wedding.ClearAudienceParticipants();
                            SyncWeddingRemoteActorsToSharedPool(wedding);
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (string.Equals(args[1], "remove", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /wedding guest remove <name>");
                            }

                            if (!wedding.RemoveAudienceParticipant(args[2]))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Wedding guest '{args[2]}' does not exist.");
                            }

                            SyncWeddingRemoteActorsToSharedPool(wedding);
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (args.Length < 5)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding guest <add|avatar|move> <name> <x> <y> [...]");
                        }

                        if (!TryParseActorPlacement(args, 3, 4, out Vector2 guestPosition, out string guestPlacementError))
                        {
                            return ChatCommandHandler.CommandResult.Error(guestPlacementError);
                        }

                        if (string.Equals(args[1], "avatar", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 6)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /wedding guest avatar <name> <x> <y> <avatarLookHex> [action] [left|right]");
                            }

                            if (!TryParseActorFacingAndAction(args, 6, out string guestAvatarAction, out bool? guestAvatarFacing, out string guestAvatarParseError))
                            {
                                return ChatCommandHandler.CommandResult.Error(guestAvatarParseError);
                            }

                            byte[] guestAvatarPayload;
                            try
                            {
                                guestAvatarPayload = ByteUtils.HexToBytes(args[5]);
                            }
                            catch (Exception ex)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid AvatarLook hex payload: {ex.Message}");
                            }

                            if (!TryCreateWeddingAvatarBuild(args[2], guestAvatarPayload, out CharacterBuild guestAvatarBuild, out string guestAvatarBuildError))
                            {
                                return ChatCommandHandler.CommandResult.Error(guestAvatarBuildError);
                            }

                            wedding.UpsertAudienceParticipant(guestAvatarBuild, guestPosition, guestAvatarFacing ?? true, guestAvatarAction);
                            SyncWeddingRemoteActorsToSharedPool(wedding);
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (!TryParseActorFacingAndAction(args, 5, out string guestActionName, out bool? guestFacingRight, out string guestParseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(guestParseError);
                        }

                        if (string.Equals(args[1], "add", StringComparison.OrdinalIgnoreCase))
                        {
                            CharacterBuild guestBuild = CreateWeddingAudienceClone(args[2]);
                            if (guestBuild == null)
                            {
                                return ChatCommandHandler.CommandResult.Error("No local player build is available to clone for the remote wedding guest.");
                            }

                            wedding.UpsertAudienceParticipant(guestBuild, guestPosition, guestFacingRight ?? true, guestActionName);
                            SyncWeddingRemoteActorsToSharedPool(wedding);
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (string.Equals(args[1], "move", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!wedding.TryMoveAudienceParticipant(args[2], guestPosition, guestFacingRight, guestActionName, out string guestMoveError))
                            {
                                return ChatCommandHandler.CommandResult.Error(guestMoveError);
                            }

                            SyncWeddingRemoteActorsToSharedPool(wedding);
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /wedding guest <add|avatar|move|remove|clear|status> ...");
                    }

                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{wedding.DescribeStatus()}{Environment.NewLine}{_weddingPacketInbox.LastStatus}");
                        }

                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = WeddingPacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /wedding inbox start [port]");
                            }

                            _weddingPacketInbox.Start(port);
                            return ChatCommandHandler.CommandResult.Ok(_weddingPacketInbox.LastStatus);
                        }

                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _weddingPacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_weddingPacketInbox.LastStatus);
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /wedding inbox [status|start [port]|stop]");
                    }

                    if (string.Equals(args[0], "end", StringComparison.OrdinalIgnoreCase))
                    {
                        wedding.OnWeddingCeremonyEnd(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }


                    return ChatCommandHandler.CommandResult.Error("Usage: /wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y> [action] [left|right]|actor avatar <groom|bride> <x> <y> <avatarLookHex> [action] [left|right]|guest <add|avatar|move|remove|clear|status> ...|inbox [status|start [port]|stop]|end]");
                });
            _chat.CommandHandler.RegisterCommand(
                "engage",
                "Inspect or drive the dedicated engagement proposal dialog seam",
                "/engage [open <partnerName> [ringItemId] [message...]|open <proposerName> <partnerName> [ringItemId] [message...]|incoming <proposerName> [ringItemId] [sealItemId] [message...]|incomingrequest <proposerName> [sealItemId] [message...]|decision <payloadhex=..|payloadb64=..>|inbox [status|start [port]|stop]|accept|withdraw|dismiss|invitation [neat|sweet|premium]|wishlist [receive|give|input] [groom|bride]|clear|status]",
                args =>
                {
                    _engagementProposalController.UpdateLocalContext(_playerManager?.Player?.Build);
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_engagementProposalController.DescribeStatus());
                    }
                    switch (args[0].ToLowerInvariant())
                    {
                        case "open":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage open <partnerName> [ringItemId] [message...]");
                            }

                            string localProposalOwner = _playerManager?.Player?.Build?.Name;
                            bool hasExplicitProposer = args.Length >= 3
                                && !TryParseOptionalPositiveInt(args, 2, out _);
                            string outgoingProposerName = hasExplicitProposer ? args[1] : localProposalOwner;
                            string outgoingPartnerName = hasExplicitProposer ? args[2] : args[1];
                            int ringItemArgumentIndex = hasExplicitProposer ? 3 : 2;
                            int messageArgumentIndex = hasExplicitProposer ? 4 : 3;

                            string openMessage = _engagementProposalController.OpenOutgoingProposal(
                                outgoingProposerName,
                                outgoingPartnerName,
                                TryParseOptionalPositiveInt(args, ringItemArgumentIndex, out int openRingItemId) ? openRingItemId : EngagementProposalRuntime.DefaultRingItemId,
                                args.Length > messageArgumentIndex ? string.Join(" ", args, messageArgumentIndex, args.Length - messageArgumentIndex) : null,
                                !string.Equals(outgoingProposerName, localProposalOwner, StringComparison.OrdinalIgnoreCase),
                                uiWindowManager?.InventoryWindow as IInventoryRuntime,
                                uiWindowManager,
                                _playerManager?.Player?.Build,
                                _fontChat,
                                ShowUtilityFeedbackMessage,
                                () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.EngagementProposal),
                                out bool outgoingOpened);
                            if (!outgoingOpened)
                            {
                                return ChatCommandHandler.CommandResult.Error(openMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                openMessage + TryAutoDispatchOutgoingEngagementProposalRequest());

                        case "incoming":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage incoming <proposerName> [ringItemId] [sealItemId] [message...]");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _engagementProposalController.OpenIncomingProposal(
                                    args[1],
                                    _playerManager?.Player?.Build?.Name,
                                    TryParseOptionalPositiveInt(args, 2, out int incomingRingItemId) ? incomingRingItemId : EngagementProposalRuntime.DefaultRingItemId,
                                    TryParseOptionalPositiveInt(args, 3, out int incomingSealItemId) ? incomingSealItemId : EngagementProposalRuntime.DefaultSealItemId,
                                    null,
                                    args.Length > 4 ? string.Join(" ", args, 4, args.Length - 4) : null,
                                    uiWindowManager,
                                    _playerManager?.Player?.Build,
                                    _fontChat,
                                    ShowUtilityFeedbackMessage,
                                    () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.EngagementProposal)));

                        case "incomingrequest":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage incomingrequest <proposerName> [sealItemId] [message...]");
                            }

                            if (!_engagementProposalController.TryOpenIncomingProposalFromLastRequestPayload(
                                    args[1],
                                    _playerManager?.Player?.Build?.Name,
                                    TryParseOptionalPositiveInt(args, 2, out int payloadSealItemId) ? payloadSealItemId : EngagementProposalRuntime.DefaultSealItemId,
                                    args.Length > 3 ? string.Join(" ", args, 3, args.Length - 3) : null,
                                    uiWindowManager,
                                    _playerManager?.Player?.Build,
                                    _fontChat,
                                    ShowUtilityFeedbackMessage,
                                    () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.EngagementProposal),
                                    out string incomingPayloadMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(incomingPayloadMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(incomingPayloadMessage);

                        case "decision":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage decision <payloadhex=..|payloadb64=..>");
                            }

                            if (!EngagementProposalInboxManager.TryParsePayloadToken(args[1], out byte[] decisionPayload, out string decisionPayloadError))
                            {
                                return ChatCommandHandler.CommandResult.Error(decisionPayloadError);
                            }

                            if (!_engagementProposalController.TryApplyDecisionPayload(decisionPayload, uiWindowManager, out string decisionMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(decisionMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(decisionMessage);

                        case "inbox":
                            return HandleEngagementProposalInboxCommand(args.Skip(1).ToArray());

                        case "accept":
                            string acceptMessage = _engagementProposalController.Accept(uiWindowManager);
                            if (string.Equals(acceptMessage, "No engagement proposal is active.", StringComparison.Ordinal)
                                || acceptMessage.Contains("does not accept the proposal", StringComparison.Ordinal))
                            {
                                return ChatCommandHandler.CommandResult.Error(acceptMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(acceptMessage);

                        case "withdraw":
                            string withdrawMessage = _engagementProposalController.Withdraw(uiWindowManager);
                            if (string.Equals(withdrawMessage, "No engagement proposal is active.", StringComparison.Ordinal)
                                || withdrawMessage.Contains("Only the requester-owned engagement wait dialog", StringComparison.Ordinal))
                            {
                                return ChatCommandHandler.CommandResult.Error(withdrawMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(withdrawMessage);

                        case "dismiss":
                            return ChatCommandHandler.CommandResult.Ok(_engagementProposalController.Dismiss(uiWindowManager));

                        case "invitation":
                            WeddingInvitationStyle invitationStyle = WeddingInvitationStyle.Neat;
                            if (args.Length >= 2 && !Enum.TryParse(args[1], true, out invitationStyle))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage invitation [neat|sweet|premium] [1|2]");
                            }

                            int invitationDialogType = WeddingInvitationRuntime.DefaultClientDialogType;
                            if (args.Length >= 3 && !TryParseOptionalPositiveInt(args, 2, out invitationDialogType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage invitation [neat|sweet|premium] [1|2]");
                            }

                            if (invitationDialogType != WeddingInvitationRuntime.DefaultClientDialogType
                                && invitationDialogType != WeddingInvitationRuntime.AlternateClientDialogType)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding invitation dialog type must be 1 or 2.");
                            }

                            if (!_engagementProposalController.TryOpenWeddingInvitationFromAcceptedProposal(
                                _weddingInvitationController,
                                uiWindowManager,
                                _playerManager?.Player?.Build,
                                _fontChat,
                                ShowUtilityFeedbackMessage,
                                invitationStyle,
                                invitationDialogType,
                                () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.WeddingInvitation),
                                out string invitationMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(invitationMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(invitationMessage);

                        case "wishlist":
                            WeddingWishListDialogMode wishListMode = WeddingWishListDialogMode.Input;
                            if (args.Length >= 2 && !Enum.TryParse(args[1], true, out wishListMode))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /engage wishlist [receive|give|input] [groom|bride]");
                            }

                            WeddingWishListRole? wishListRole = null;
                            if (args.Length >= 3)
                            {
                                if (!Enum.TryParse(args[2], true, out WeddingWishListRole parsedRole))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /engage wishlist [receive|give|input] [groom|bride]");
                                }

                                wishListRole = parsedRole;
                            }

                            if (_weddingInvitationController.TryOpenWeddingWishListFromAcceptedInvitation(
                                    _weddingWishListController,
                                    uiWindowManager,
                                    _playerManager?.Player?.Build,
                                    uiWindowManager?.InventoryWindow as IInventoryRuntime,
                                    _fontChat,
                                    ShowUtilityFeedbackMessage,
                                    wishListMode,
                                    wishListRole,
                                    () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.WeddingWishList),
                                    out string wishListMessage)
                                || _engagementProposalController.TryOpenWeddingWishListFromAcceptedProposal(
                                    _weddingWishListController,
                                    uiWindowManager,
                                    _playerManager?.Player?.Build,
                                    uiWindowManager?.InventoryWindow as IInventoryRuntime,
                                    _fontChat,
                                    ShowUtilityFeedbackMessage,
                                    wishListMode,
                                    wishListRole,
                                    () => ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.WeddingWishList),
                                    out wishListMessage))
                            {
                                return ChatCommandHandler.CommandResult.Ok(wishListMessage);
                            }

                            return ChatCommandHandler.CommandResult.Error(wishListMessage);

                        case "clear":
                            return ChatCommandHandler.CommandResult.Ok(_engagementProposalController.Clear(uiWindowManager));

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /engage [open <partnerName> [ringItemId] [message...]|open <proposerName> <partnerName> [ringItemId] [message...]|incoming <proposerName> [ringItemId] [sealItemId] [message...]|incomingrequest <proposerName> [sealItemId] [message...]|decision <payloadhex=..|payloadb64=..>|inbox [status|start [port]|stop]|accept|withdraw|dismiss|invitation [neat|sweet|premium]|wishlist [receive|give|input] [groom|bride]|clear|status]");
                    }
                });



            _chat.CommandHandler.RegisterCommand(
                "guildboss",
                "Inspect or update guild boss healer and pulley state",
                "/guildboss [status|transport [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|healer <y>|pulley <state>|packet <344|345> <value>|packetraw <hex>]",
                args =>
                {
                    GuildBossField guildBoss = _specialFieldRuntime.SpecialEffects.GuildBoss;
                    if (!guildBoss.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Guild boss runtime is only active on guild boss maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{guildBoss.DescribeStatus()}{Environment.NewLine}{_guildBossTransport.DescribeStatus()}{Environment.NewLine}{_guildBossOfficialSessionBridge.DescribeStatus()}");
                    }


                    if (string.Equals(args[0], "transport", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{guildBoss.DescribeStatus()}{Environment.NewLine}{_guildBossTransport.DescribeStatus()}{Environment.NewLine}{_guildBossOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = GuildBossPacketTransportManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildboss transport start [port]");
                            }


                            _guildBossTransport.Start(port);

                            return ChatCommandHandler.CommandResult.Ok(_guildBossTransport.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _guildBossTransport.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_guildBossTransport.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /guildboss transport [status|start [port]|stop]");

                    }



                    if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{guildBoss.DescribeStatus()}{Environment.NewLine}{_guildBossOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int discoverRemotePort)
                                || discoverRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session discover <remotePort> [processName|pid] [localPort]");
                            }


                            string processSelector = args.Length >= 4 ? args[3] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 5)
                            {
                                if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session discover <remotePort> [processName|pid] [localPort]");
                                }


                                localPortFilter = parsedLocalPort;

                            }



                            return ChatCommandHandler.CommandResult.Info(

                                _guildBossOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));

                        }



                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 5
                                || !int.TryParse(args[2], out int listenPort)
                                || listenPort <= 0
                                || !int.TryParse(args[4], out int remotePort)
                                || remotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session start <listenPort> <serverHost> <serverPort>");
                            }


                            return _guildBossOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startMessage)
                                ? ChatCommandHandler.CommandResult.Ok(startMessage)
                                : ChatCommandHandler.CommandResult.Error(startMessage);

                        }



                        if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int autoListenPort)
                                || autoListenPort <= 0
                                || !int.TryParse(args[3], out int autoRemotePort)
                                || autoRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                            }


                            string processSelector = args.Length >= 5 ? args[4] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 6)
                            {
                                if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }


                                localPortFilter = parsedLocalPort;

                            }



                            return _guildBossOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }


                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _guildBossOfficialSessionBridge.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_guildBossOfficialSessionBridge.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /guildboss session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");

                    }



                    if (string.Equals(args[0], "healer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int healerY))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildboss healer <y>");
                        }


                        guildBoss.OnHealerMove(healerY, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(guildBoss.DescribeStatus());

                    }



                    if (string.Equals(args[0], "pulley", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int pulleyState))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildboss pulley <state>");
                        }


                        guildBoss.OnPulleyStateChange(pulleyState, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(guildBoss.DescribeStatus());

                    }



                    if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int packetType) || !int.TryParse(args[2], out int packetValue))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildboss packet <344|345> <value>");
                        }


                        byte[] payload = packetType switch
                        {
                            344 => BitConverter.GetBytes(checked((short)packetValue)),
                            345 => new[] { unchecked((byte)packetValue) },
                            _ => null
                        };


                        if (payload == null)
                        {
                            return ChatCommandHandler.CommandResult.Error("Guild boss packet must be 344 or 345");
                        }


                        if (!guildBoss.TryApplyPacket(packetType, payload, currTickCount, out string error))
                        {
                            return ChatCommandHandler.CommandResult.Error(error);
                        }


                        return ChatCommandHandler.CommandResult.Ok($"{guildBoss.DescribeStatus()} Applied packet {packetType}.");

                    }



                    if (string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!GuildBossPacketTransportManager.TryParsePacketLine(
                                string.Join(' ', args),
                                out int rawPacketType,
                                out byte[] rawPayload,
                                out string rawError))
                        {
                            return ChatCommandHandler.CommandResult.Error(rawError ?? "Unable to parse guild boss raw packet.");
                        }


                        if (!guildBoss.TryApplyPacket(rawPacketType, rawPayload, currTickCount, out string applyError))
                        {
                            return ChatCommandHandler.CommandResult.Error(applyError);
                        }


                        return ChatCommandHandler.CommandResult.Ok($"{guildBoss.DescribeStatus()} Applied raw packet {rawPacketType}.");

                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /guildboss [status|transport [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|healer <y>|pulley <state>|packet <344|345> <value>|packetraw <hex>]");

                });
            _chat.CommandHandler.RegisterCommand(
                "guildui",
                "Inspect or open the dedicated guild ranking, mark, and create-agreement dialogs",
                "/guildui [status|rank open|mark open|agree open <masterName> <guildName>]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_guildRankController.DescribeStatus()}{Environment.NewLine}{_guildMarkController.DescribeStatus()}{Environment.NewLine}{_guildCreateAgreementController.DescribeStatus()}");
                    }


                    if (string.Equals(args[0], "rank", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildui rank open");
                        }


                        return ChatCommandHandler.CommandResult.Ok(OpenGuildRankWindow());
                    }


                    if (string.Equals(args[0], "mark", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildui mark open");
                        }


                        return ChatCommandHandler.CommandResult.Ok(OpenGuildMarkWindow());
                    }


                    if (string.Equals(args[0], "agree", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4 || !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildui agree open <masterName> <guildName>");
                        }


                        return ChatCommandHandler.CommandResult.Ok(OpenGuildCreateAgreementWindow(args[2], string.Join(" ", args.Skip(3))));
                    }


                    return ChatCommandHandler.CommandResult.Error("Usage: /guildui [status|rank open|mark open|agree open <masterName> <guildName>]");
                });
            _chat.CommandHandler.RegisterCommand(
                "sociallist",
                "Inspect or drive the Social List, guild-manage, alliance-editor, and packet-owned roster-authority seams",
                "/sociallist [status|open [friend|party|guild|alliance|blacklist]|search [party|partymember|expedition]|guildsearch open|guildmanage open [position|admission|change]|alliance open [rank|notice]|packet ...|packetraw ...]",
                args =>
                {
                    const string usage = "/sociallist [status|open [friend|party|guild|alliance|blacklist]|search [party|partymember|expedition]|guildsearch open|guildmanage open [position|admission|change]|alliance open [rank|notice]|packet [session ...|...]|packetraw ...]";
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info($"{_socialListRuntime.DescribeStatus()}{Environment.NewLine}{DescribeSocialListOfficialSessionBridgeStatus()}");
                    }

                    if (string.Equals(args[0], "open", StringComparison.OrdinalIgnoreCase))
                    {
                        SocialListTab targetTab = _socialListRuntime.CurrentTab;
                        if (args.Length >= 2 && !TryParseSocialListTabToken(args[1], out targetTab))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist open [friend|party|guild|alliance|blacklist]");
                        }

                        _socialListRuntime.SelectTab(targetTab);
                        WireSocialListWindowData();
                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
                        return ChatCommandHandler.CommandResult.Ok($"Opened Social List on the {DescribePacketOwnedSocialListTab(targetTab)} tab.");
                    }

                    if (string.Equals(args[0], "search", StringComparison.OrdinalIgnoreCase))
                    {
                        SocialSearchTab searchTab = SocialSearchTab.Party;
                        if (args.Length >= 2 && !TryParseSocialSearchTabToken(args[1], out searchTab))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist search [party|partymember|expedition]");
                        }

                        _socialListRuntime.OpenSearchWindow(searchTab);
                        WireSocialSearchWindowData();
                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialSearch);
                        return ChatCommandHandler.CommandResult.Ok($"Opened Social Search on the {searchTab} tab.");
                    }

                    if (string.Equals(args[0], "guildsearch", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length >= 2 && !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist guildsearch open");
                        }

                        _socialListRuntime.OpenGuildSearchWindow();
                        WireGuildSearchWindowData();
                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildSearch);
                        return ChatCommandHandler.CommandResult.Ok("Opened the dedicated guild-search owner.");
                    }

                    if (string.Equals(args[0], "guildmanage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist guildmanage open [position|admission|change]");
                        }

                        GuildManageTab manageTab = GuildManageTab.Position;
                        if (args.Length >= 3 && !TryParseGuildManageTabToken(args[2], out manageTab))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist guildmanage open [position|admission|change]");
                        }

                        _socialListRuntime.OpenGuildManageWindow(manageTab);
                        WireGuildManageWindowData();
                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildManage);
                        return ChatCommandHandler.CommandResult.Ok($"Opened Guild Manage on the {manageTab} tab.");
                    }

                    if (string.Equals(args[0], "alliance", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !string.Equals(args[1], "open", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist alliance open [rank|notice]");
                        }

                        AllianceEditorFocus focus = AllianceEditorFocus.RankTitle;
                        if (args.Length >= 3 && !TryParseAllianceEditorFocusToken(args[2], out focus))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist alliance open [rank|notice]");
                        }

                        _socialListRuntime.OpenAllianceEditor(focus);
                        WireAllianceEditorWindowData();
                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.AllianceEditor);
                        return ChatCommandHandler.CommandResult.Ok(
                            focus == AllianceEditorFocus.Notice
                                ? "Opened the alliance editor on the notice branch."
                                : "Opened the alliance editor on the rank-title branch.");
                    }

                    if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info($"{_socialListRuntime.DescribeStatus()}{Environment.NewLine}{DescribeSocialListOfficialSessionBridgeStatus()}");
                        }

                        if (string.Equals(args[1], "session", StringComparison.OrdinalIgnoreCase))
                        {
                            return HandleSocialListSessionCommand(args.Skip(2).ToArray());
                        }

                        string packetAction = args[1].ToLowerInvariant();
                        if (TryParseSocialListTabToken(packetAction, out _)
                            || string.Equals(packetAction, "guildauth", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(packetAction, "allianceauth", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(packetAction, "guildui", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length >= 3
                                && (args[2].StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase)
                                    || args[2].StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))
                            {
                                return HandleSocialListPacketCommand(args.Skip(1).ToArray());
                            }
                        }

                        if (string.Equals(packetAction, "owner", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab ownerTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet owner <friend|party|guild|alliance|blacklist> <local|packet> [summary]");
                            }

                            bool packetOwned = args[3].ToLowerInvariant() switch
                            {
                                "packet" => true,
                                "local" => false,
                                _ => throw new ArgumentException("Roster owner must be local or packet.")
                            };

                            string summary = args.Length > 4 ? string.Join(' ', args.Skip(4)) : null;
                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.SetPacketRosterOwnership(ownerTab, packetOwned, summary));
                        }

                        if (string.Equals(packetAction, "seed", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3 || !TryParseSocialListTabToken(args[2], out SocialListTab seedTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet seed <friend|party|guild|alliance|blacklist>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.SeedPacketRoster(seedTab));
                        }

                        if (string.Equals(packetAction, "clear", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3 || !TryParseSocialListTabToken(args[2], out SocialListTab clearTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet clear <friend|party|guild|alliance|blacklist>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ClearPacketRoster(clearTab));
                        }

                        if (string.Equals(packetAction, "remove", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab removeTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet remove <friend|party|guild|alliance|blacklist> <name>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.RemovePacketEntry(removeTab, string.Join(' ', args.Skip(3))));
                        }

                        if (string.Equals(packetAction, "select", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab selectTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet select <friend|party|guild|alliance|blacklist> <name>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.SelectEntryByName(selectTab, string.Join(' ', args.Skip(3))));
                        }

                        if (string.Equals(packetAction, "summary", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab summaryTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet summary <friend|party|guild|alliance|blacklist> <summary>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.SetPacketSyncSummary(summaryTab, string.Join(' ', args.Skip(3))));
                        }

                        if (string.Equals(packetAction, "resolve", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab resolveTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet resolve <friend|party|guild|alliance|blacklist> <approve|reject> [summary]");
                            }

                            bool approved = args[3].ToLowerInvariant() switch
                            {
                                "approve" or "approved" or "accept" => true,
                                "reject" or "rejected" or "deny" => false,
                                _ => throw new ArgumentException("Resolve action must be approve or reject.")
                            };

                            string summary = args.Length > 4 ? string.Join(' ', args.Skip(4)) : null;
                            return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ResolvePacketOwnedRequest(resolveTab, approved, summary));
                        }

                        if (string.Equals(packetAction, "upsert", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4 || !TryParseSocialListTabToken(args[2], out SocialListTab upsertTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet upsert <friend|party|guild|alliance|blacklist> <name>|<primary>|<secondary>|<location>|<channel>|<online>|<leader>|<blocked>|<local>");
                            }

                            string[] fields = string.Join(' ', args.Skip(3)).Split('|', StringSplitOptions.TrimEntries);
                            if (fields.Length != 9
                                || !int.TryParse(fields[4], out int channel)
                                || !TryParsePacketOwnedBooleanToken(fields[5], out bool isOnline)
                                || !TryParsePacketOwnedBooleanToken(fields[6], out bool isLeader)
                                || !TryParsePacketOwnedBooleanToken(fields[7], out bool isBlocked)
                                || !TryParsePacketOwnedBooleanToken(fields[8], out bool isLocalPlayer))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet upsert <friend|party|guild|alliance|blacklist> <name>|<primary>|<secondary>|<location>|<channel>|<online>|<leader>|<blocked>|<local>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _socialListRuntime.UpsertPacketEntry(
                                    upsertTab,
                                    fields[0],
                                    fields[1],
                                    fields[2],
                                    fields[3],
                                    channel,
                                    isOnline,
                                    isLeader,
                                    isBlocked,
                                    isLocalPlayer));
                        }

                        if (string.Equals(packetAction, "guildauth", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guildauth <clear|<role>|<role>|<rank>|<admission>|<notice>>");
                            }

                            if (string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "local", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ClearPacketGuildAuthority());
                            }

                            string[] fields = string.Join(' ', args.Skip(2)).Split('|', StringSplitOptions.TrimEntries);
                            if (fields.Length != 4
                                || !TryParsePacketOwnedBooleanToken(fields[1], out bool canManageRanks)
                                || !TryParsePacketOwnedBooleanToken(fields[2], out bool canToggleAdmission)
                                || !TryParsePacketOwnedBooleanToken(fields[3], out bool canEditNotice))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guildauth <clear|<role>|<rank>|<admission>|<notice>>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _socialListRuntime.SetPacketGuildAuthority(fields[0], canManageRanks, canToggleAdmission, canEditNotice));
                        }

                        if (string.Equals(packetAction, "allianceauth", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet allianceauth <clear|<role>|<rank>|<notice>>");
                            }

                            if (string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "local", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ClearPacketAllianceAuthority());
                            }

                            string[] fields = string.Join(' ', args.Skip(2)).Split('|', StringSplitOptions.TrimEntries);
                            if (fields.Length != 3
                                || !TryParsePacketOwnedBooleanToken(fields[1], out bool canEditRanks)
                                || !TryParsePacketOwnedBooleanToken(fields[2], out bool canEditAllianceNotice))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet allianceauth <clear|<role>|<rank>|<notice>>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _socialListRuntime.SetPacketAllianceAuthority(fields[0], canEditRanks, canEditAllianceNotice));
                        }

                        if (string.Equals(packetAction, "guildui", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guildui <clear|<member>|<guildName>|<guildLevel>>");
                            }

                            if (string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "local", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ClearPacketGuildUiContext());
                            }

                            string[] fields = string.Join(' ', args.Skip(2)).Split('|', StringSplitOptions.TrimEntries);
                            if (fields.Length != 3
                                || !TryParsePacketOwnedBooleanToken(fields[0], out bool hasGuildMembership)
                                || !int.TryParse(fields[2], out int guildLevel))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guildui <clear|<member>|<guildName>|<guildLevel>>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _socialListRuntime.SetPacketGuildUiContext(hasGuildMembership, fields[1], guildLevel));
                        }

                        if (string.Equals(packetAction, "guilddialog", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3 || string.Equals(args[2], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(_socialListRuntime.DescribeGuildDialogRequestStatus());
                            }

                            if (string.Equals(args[2], "balance", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length == 3)
                                {
                                    return ChatCommandHandler.CommandResult.Info(_socialListRuntime.DescribeGuildDialogRequestStatus());
                                }

                                if (!int.TryParse(args[3], out int mesos))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guilddialog balance [mesos]");
                                }

                                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.SetGuildDialogMesoBalance(mesos));
                            }

                            if (string.Equals(args[2], "approve", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "accept", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "reject", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(args[2], "deny", StringComparison.OrdinalIgnoreCase))
                            {
                                bool approved = string.Equals(args[2], "approve", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(args[2], "accept", StringComparison.OrdinalIgnoreCase);
                                string summary = args.Length > 3 ? string.Join(' ', args.Skip(3)) : null;
                                return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ResolvePendingGuildDialogRequest(approved, summary));
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /sociallist packet guilddialog <status|balance [mesos]|approve [summary]|reject [summary]>");
                        }

                        return ChatCommandHandler.CommandResult.Error(
                            SocialListPacketPayloadUsage);
                    }

                    if (string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleSocialListPacketRawCommand(args.Skip(1).ToArray());
                    }

                    return ChatCommandHandler.CommandResult.Error(usage);
                });



            _chat.CommandHandler.RegisterCommand(
                "transport",
                "Inspect or drive the transit/voyage transport packet inbox and official-session bridge",
                        "/transport [status|packet [start <value>|move <value>|end <value>|state <state> <value>]|packetraw <hex>|raw <164|165> <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|replay <historyIndex>|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]",
                args =>
                {
                    bool transportActive = IsTransitVoyageWrapperMap(_mapBoard?.MapInfo) && _transportField.HasRouteConfiguration;
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_transportField.DescribeStatus()}{Environment.NewLine}{_transportPacketInbox.LastStatus}{Environment.NewLine}{_transportOfficialSessionBridge.DescribeStatus()}");
                    }


                    if (!transportActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Transport packet controls are only active on transit or voyage wrapper maps.");
                    }


                    if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /transport packet [start <value>|move <value>|end <value>|state <state> <value>]");
                        }


                        string packetLine = args[1].ToLowerInvariant() switch
                        {
                            "start" when args.Length >= 3 => $"start {args[2]}",
                            "move" when args.Length >= 3 => $"move {args[2]}",
                            "end" when args.Length >= 3 => $"end {args[2]}",
                            "state" when args.Length >= 4 => $"state {args[2]} {args[3]}",
                            _ => null
                        };
                        string parseError = null;
                        if (packetLine == null
                            || !TransportationPacketInboxManager.TryParsePacketLine(packetLine, out int packetType, out byte[] payload, out parseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(parseError ?? "Usage: /transport packet [start <value>|move <value>|end <value>|state <state> <value>]");
                        }


                        if (!TryApplyTransportInboxMessage(new TransportationPacketInboxMessage(packetType, payload, "transport-command", packetLine), out string packetResult))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetResult);
                        }


                        return ChatCommandHandler.CommandResult.Ok(_transportField.DescribeStatus());

                    }



                    if (string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))

                    {

                        string rawError = null;
                        if (!TransportationPacketInboxManager.TryParsePacketLine(
                                string.Join(' ', args),
                                out int rawPacketType,
                                out byte[] rawPayload,
                                out rawError))
                        {
                            return ChatCommandHandler.CommandResult.Error(rawError ?? "Unable to parse transport raw packet.");
                        }


                        if (!TryApplyTransportInboxMessage(new TransportationPacketInboxMessage(rawPacketType, rawPayload, "transport-command", string.Join(' ', args)), out string packetResult))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetResult);
                        }


                        return ChatCommandHandler.CommandResult.Ok(_transportField.DescribeStatus());

                    }



                    if (string.Equals(args[0], "raw", StringComparison.OrdinalIgnoreCase))

                    {
                        string rawError = null;

                        if (args.Length < 3
                            || !TransportationPacketInboxManager.TryParsePacketLine(
                                string.Join(' ', args.Skip(1)),
                                out int rawPacketType,
                                out byte[] rawPayload,
                                out rawError))
                        {
                            return ChatCommandHandler.CommandResult.Error(rawError ?? "Usage: /transport raw <164|165> <hex>");
                        }


                        if (!TryApplyTransportInboxMessage(new TransportationPacketInboxMessage(rawPacketType, rawPayload, "transport-command", string.Join(' ', args)), out string packetResult))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetResult);
                        }


                        return ChatCommandHandler.CommandResult.Ok(_transportField.DescribeStatus());

                    }



                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_transportField.DescribeStatus()}{Environment.NewLine}{_transportPacketInbox.LastStatus}{Environment.NewLine}{_transportOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))

                        {

                            int port = TransportationPacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /transport inbox start [port]");
                            }


                            _transportPacketInbox.Start(port);
                            return ChatCommandHandler.CommandResult.Ok(_transportPacketInbox.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))

                        {

                            _transportPacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_transportPacketInbox.LastStatus);

                        }



                        return ChatCommandHandler.CommandResult.Error("Usage: /transport inbox [status|start [port]|stop]");

                    }

                    if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleTransportSessionCommand(args);
                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /transport [status|packet [start <value>|move <value>|end <value>|state <state> <value>]|packetraw <hex>|raw <164|165> <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|replay <historyIndex>|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]");

                });



            _chat.CommandHandler.RegisterCommand(
                "witchscore",
                "Inspect or update the Witchtower scoreboard score",
                "/witchscore [score]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Witchtower.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Witchtower scoreboard is only active on Witchtower maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Witchtower.DescribeStatus());
                    }


                    if (!int.TryParse(args[0], out int score))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Witchtower score: {args[0]}");
                    }


                    _specialFieldRuntime.SpecialEffects.Witchtower.OnScoreUpdate(score, currTickCount);

                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Witchtower.DescribeStatus());

                });



            _chat.CommandHandler.RegisterCommand(
                "partyraid",
                "Inspect or drive the Party Raid runtime shell",
                "/partyraid [status|stage <n>|point <n>|team <red|blue>|damage <red|blue> <n>|gaugecap <n>|clock <seconds|clear>|inbox [status|start [port]|stop]|key <field|party|session> <key> <value>|result <point> <bonus> <total> [win|lose|clear]|outcome <win|lose|clear>]",
                args =>
                {
                    PartyRaidField partyRaid = _specialFieldRuntime.PartyRaid;
                    if (!partyRaid.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Party Raid runtime is only active on Party Raid field, boss, or result maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(partyRaid.DescribeStatus());
                    }


                    if (string.Equals(args[0], "stage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int stage))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid stage <n>");
                        }


                        partyRaid.OnFieldSetVariable("stage", stage.ToString());

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "point", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int point))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid point <n>");
                        }


                        partyRaid.OnPartyValue("point", point.ToString());

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "team", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid team <red|blue>");
                        }


                        if (!partyRaid.OnFieldSetVariable("team", args[1]))
                        {
                            return ChatCommandHandler.CommandResult.Error("Team must be red or blue");
                        }


                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "damage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[2], out int damage))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid damage <red|blue> <n>");
                        }


                        if (string.Equals(args[1], "red", StringComparison.OrdinalIgnoreCase))
                        {
                            partyRaid.OnFieldSetVariable("redDamage", damage.ToString());
                            return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                        }


                        if (string.Equals(args[1], "blue", StringComparison.OrdinalIgnoreCase))
                        {
                            partyRaid.OnFieldSetVariable("blueDamage", damage.ToString());
                            return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                        }


                        return ChatCommandHandler.CommandResult.Error("Damage side must be red or blue");

                    }



                    if (string.Equals(args[0], "gaugecap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int gaugeCap))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid gaugecap <n>");
                        }


                        partyRaid.OnFieldSetVariable("gaugeCap", gaugeCap.ToString());

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "clock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid clock <seconds|clear>");
                        }


                        if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                        {
                            partyRaid.ClearClock();
                            return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                        }


                        if (!int.TryParse(args[1], out int seconds))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid clock <seconds|clear>");
                        }


                        partyRaid.OnClock(2, seconds, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }












                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{partyRaid.DescribeStatus()}{Environment.NewLine}{_partyRaidPacketInbox.LastStatus}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = PartyRaidPacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /partyraid inbox start [port]");
                            }


                            _partyRaidPacketInbox.Start(port);

                            return ChatCommandHandler.CommandResult.Ok(_partyRaidPacketInbox.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _partyRaidPacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_partyRaidPacketInbox.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /partyraid inbox [status|start [port]|stop]");

                    }



                    if (string.Equals(args[0], "key", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid key <field|party|session> <key> <value>");
                        }


                        bool applied = false;
                        if (string.Equals(args[1], "field", StringComparison.OrdinalIgnoreCase))
                        {
                            applied = partyRaid.OnFieldSetVariable(args[2], args[3]);
                        }
                        else if (string.Equals(args[1], "party", StringComparison.OrdinalIgnoreCase))
                        {
                            applied = partyRaid.OnPartyValue(args[2], args[3]);
                        }
                        else if (string.Equals(args[1], "session", StringComparison.OrdinalIgnoreCase))
                        {
                            applied = partyRaid.OnSessionValue(args[2], args[3]);
                        }
                        else
                        {
                            return ChatCommandHandler.CommandResult.Error("Scope must be field, party, or session");
                        }


                        if (!applied)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Party Raid key was not accepted: {args[1]} {args[2]}={args[3]}");
                        }


                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[1], out int resultPoint)
                            || !int.TryParse(args[2], out int resultBonus)
                            || !int.TryParse(args[3], out int resultTotal))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid result <point> <bonus> <total> [win|lose|clear]");
                        }


                        partyRaid.OnSessionValue("point", resultPoint.ToString());

                        partyRaid.OnSessionValue("bonus", resultBonus.ToString());

                        partyRaid.OnSessionValue("total", resultTotal.ToString());



                        if (args.Length >= 5)
                        {
                            if (!TryParsePartyRaidOutcome(args[4], out PartyRaidResultOutcome outcome))
                            {
                                return ChatCommandHandler.CommandResult.Error("Outcome must be win, lose, or clear");
                            }


                            partyRaid.SetResultOutcome(outcome);

                        }



                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    if (string.Equals(args[0], "outcome", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !TryParsePartyRaidOutcome(args[1], out PartyRaidResultOutcome outcome))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid outcome <win|lose|clear>");
                        }


                        partyRaid.SetResultOutcome(outcome);

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());

                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /partyraid [status|stage <n>|point <n>|team <red|blue>|damage <red|blue> <n>|gaugecap <n>|clock <seconds|clear>|inbox [status|start [port]|stop]|key <field|party|session> <key> <value>|result <point> <bonus> <total> [win|lose|clear]|outcome <win|lose|clear>]");

                });



            _chat.CommandHandler.RegisterCommand(
                "tournament",
                "Inspect or drive the Tournament field wrapper",
                "/tournament [status|dialog [status|close|scroll <up|down|0..2>]|raw <374|375|376|377|378> <hex>|packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]",
                args =>
                {
                    TournamentField field = _specialFieldRuntime.Minigames.Tournament;
                    if (!field.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Tournament runtime is only active on tournament maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{field.DescribeStatus()}{Environment.NewLine}{_tournamentPacketInbox.LastStatus}{Environment.NewLine}{_tournamentOfficialSessionBridge.DescribeStatus()}");
                    }

                    if (string.Equals(args[0], "dialog", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(field.DescribeMatchTableDialog());
                        }

                        if (string.Equals(args[1], "close", StringComparison.OrdinalIgnoreCase))
                        {
                            field.CloseMatchTableDialog();
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeMatchTableDialog());
                        }

                        if (string.Equals(args[1], "scroll", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /tournament dialog scroll <up|down|0..2>");
                            }

                            int delta = args[2].ToLowerInvariant() switch
                            {
                                "up" => -1,
                                "down" => 1,
                                _ => int.MinValue
                            };

                            if (delta == int.MinValue)
                            {
                                if (!int.TryParse(args[2], out int absoluteScroll) || absoluteScroll < 0 || absoluteScroll > 2)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /tournament dialog scroll <up|down|0..2>");
                                }

                                string beforeStatus = field.DescribeMatchTableDialog();
                                if (beforeStatus.Contains("closed.", StringComparison.OrdinalIgnoreCase))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Tournament match-table dialog is not open.");
                                }

                                if (!field.TryScrollMatchTableDialog(absoluteScroll - field.MatchTableDialog.Scroll, out string absoluteMessage))
                                {
                                    return ChatCommandHandler.CommandResult.Error(absoluteMessage);
                                }

                                return ChatCommandHandler.CommandResult.Ok(absoluteMessage);
                            }

                            return field.TryScrollMatchTableDialog(delta, out string scrollMessage)
                                ? ChatCommandHandler.CommandResult.Ok(scrollMessage)
                                : ChatCommandHandler.CommandResult.Error(scrollMessage);
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /tournament dialog [status|close|scroll <up|down|0..2>]");
                    }


                    if (string.Equals(args[0], "raw", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int packetType))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /tournament raw <374|375|376|377|378> <hex>");
                        }


                        if (!TryDecodeHexBytes(args[2], out byte[] payload))
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid Tournament hex payload: {args[2]}");
                        }


                        return field.TryApplyRawPacket(packetType, payload, currTickCount, out string errorMessage)
                            ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                            : ChatCommandHandler.CommandResult.Error(errorMessage);
                    }


                    if (string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !TryDecodeHexBytes(args[1], out byte[] packetBytes) || packetBytes.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /tournament packetraw <hex>");
                        }


                        int packetType = BitConverter.ToUInt16(packetBytes, 0);
                        byte[] payload = packetBytes.Length > 2 ? packetBytes[2..] : Array.Empty<byte>();
                        return field.TryApplyRawPacket(packetType, payload, currTickCount, out string packetError)
                            ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())
                            : ChatCommandHandler.CommandResult.Error(packetError);
                    }


                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{field.DescribeStatus()}{Environment.NewLine}{_tournamentPacketInbox.LastStatus}{Environment.NewLine}{_tournamentOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = TournamentPacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && !int.TryParse(args[2], out port))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /tournament inbox start [port]");
                            }


                            _tournamentPacketInbox.Start(port);
                            return ChatCommandHandler.CommandResult.Ok(_tournamentPacketInbox.LastStatus);
                        }


                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _tournamentPacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_tournamentPacketInbox.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /tournament inbox [status|start [port]|stop]");
                    }

                    if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{field.DescribeStatus()}{Environment.NewLine}{_tournamentOfficialSessionBridge.DescribeStatus()}");
                        }

                        if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int discoverRemotePort)
                                || discoverRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /tournament session discover <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 4 ? args[3] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 5)
                            {
                                if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /tournament session discover <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return ChatCommandHandler.CommandResult.Info(
                                _tournamentOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                        }

                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 5
                                || !int.TryParse(args[2], out int listenPort)
                                || listenPort <= 0
                                || !int.TryParse(args[4], out int remotePort)
                                || remotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /tournament session start <listenPort> <serverHost> <serverPort>");
                            }

                            return _tournamentOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startMessage)
                                ? ChatCommandHandler.CommandResult.Ok(startMessage)
                                : ChatCommandHandler.CommandResult.Error(startMessage);
                        }

                        if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int autoListenPort)
                                || autoListenPort <= 0
                                || !int.TryParse(args[3], out int autoRemotePort)
                                || autoRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /tournament session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 5 ? args[4] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 6)
                            {
                                if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /tournament session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return _tournamentOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }

                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _tournamentOfficialSessionBridge.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_tournamentOfficialSessionBridge.LastStatus);
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /tournament session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
                    }


                    return ChatCommandHandler.CommandResult.Error("Usage: /tournament [status|dialog [status|close|scroll <up|down|0..2>]|raw <374|375|376|377|378> <hex>|packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]");
                });


            _chat.CommandHandler.RegisterCommand(
                "battlefield",
                "Inspect or drive the Battlefield timerboard and team score flow",
                "/battlefield [clock [seconds]|score <wolves> <sheep>|team <wolves|sheep|0|1|2|clear> [characterId]|result [wolves|sheep|draw|auto]]",
                args =>
                {
                    BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;
                    if (!battlefield.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Battlefield runtime is only active on Battlefield maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(battlefield.DescribeStatus());
                    }


                    if (string.Equals(args[0], "clock", StringComparison.OrdinalIgnoreCase))
                    {
                        int seconds = battlefield.DefaultDurationSeconds;
                        if (args.Length >= 2 && !int.TryParse(args[1], out seconds))
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid Battlefield clock seconds: {args[1]}");
                        }


                        battlefield.OnClock(2, seconds, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());

                    }



                    if (string.Equals(args[0], "score", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3
                            || !int.TryParse(args[1], out int wolves)
                            || !int.TryParse(args[2], out int sheep))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield score <wolves> <sheep>");
                        }


                        battlefield.OnScoreUpdate(wolves, sheep, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());

                    }



                    if (string.Equals(args[0], "team", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !TryParseBattlefieldTeam(args[1], out int? teamId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield team <wolves|sheep|0|1|2|clear> [characterId]");
                        }


                        if (args.Length >= 3)
                        {
                            if (!int.TryParse(args[2], out int characterId) || characterId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Battlefield character id: {args[2]}");
                            }


                            if (!teamId.HasValue)
                            {
                                return ChatCommandHandler.CommandResult.Error("Remote Battlefield team changes require an explicit team id");
                            }


                            battlefield.OnTeamChanged(characterId, teamId.Value, currTickCount);
                            _remoteUserPool.TrySetBattlefieldTeam(characterId, teamId.Value, out _);
                            return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());

                        }



                        battlefield.SetLocalTeam(teamId, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());

                    }



                    if (string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
                    {
                        BattlefieldField.BattlefieldWinner winner = BattlefieldField.BattlefieldWinner.None;
                        if (args.Length >= 2 && !TryParseBattlefieldWinner(args[1], out winner))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield result [wolves|sheep|draw|auto]");
                        }


                        if (winner == BattlefieldField.BattlefieldWinner.None)
                        {
                            winner = battlefield.WolvesScore == battlefield.SheepScore
                                ? BattlefieldField.BattlefieldWinner.Draw
                                : battlefield.WolvesScore > battlefield.SheepScore
                                    ? BattlefieldField.BattlefieldWinner.Wolves
                                    : BattlefieldField.BattlefieldWinner.Sheep;
                        }


                        battlefield.ResolveResult(winner, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());

                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /battlefield [clock [seconds]|score <wolves> <sheep>|team <wolves|sheep|0|1|2|clear> [characterId]|result [wolves|sheep|draw|auto]]");

                });



            _chat.CommandHandler.RegisterCommand(

                "coconut",

                "Inspect or drive the Coconut minigame packet and result flow",

                "/coconut [status|clock <seconds>|hit <target|-1> <delay> <state>|score <maple> <story>|raw <type> <hex>|raw packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|request [peek|clear]]",
                args =>
                {
                    CoconutField field = _specialFieldRuntime.Minigames.Coconut;
                    if (!field.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Coconut runtime is only active on Coconut maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{field.DescribeStatus()}{Environment.NewLine}{_coconutPacketInbox.LastStatus}{Environment.NewLine}{_coconutOfficialSessionBridge.LastStatus}");
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "clock":
                            if (args.Length < 2 || !int.TryParse(args[1], out int seconds) || seconds < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /coconut clock <seconds>");
                            }


                            field.OnClock(seconds, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "hit":
                            if (args.Length < 4
                                || !int.TryParse(args[1], out int targetId)
                                || !int.TryParse(args[2], out int delay)
                                || !int.TryParse(args[3], out int newState))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /coconut hit <target|-1> <delay> <state>");
                            }


                            field.OnCoconutHit(targetId, delay, newState, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "score":
                            if (args.Length < 3
                                || !int.TryParse(args[1], out int mapleScore)
                                || !int.TryParse(args[2], out int storyScore))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /coconut score <maple> <story>");
                            }


                            field.OnCoconutScore(mapleScore, storyScore, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "raw":
                            if (args.Length < 3)
                            {

                                return ChatCommandHandler.CommandResult.Error("Usage: /coconut raw <type> <hex> | /coconut raw packetraw <hex>");
                            }



                            if (!CoconutPacketInboxManager.TryParsePacketLine(
                                string.Join(' ', args.Skip(1)),
                                out int packetType,
                                out byte[] payload,
                                out bool ignoredPacket,
                                out string parseMessage))
                            {
                                return ignoredPacket
                                    ? ChatCommandHandler.CommandResult.Info(parseMessage ?? "Ignored Coconut outbound echo packet.")
                                    : ChatCommandHandler.CommandResult.Error(parseMessage ?? "Usage: /coconut raw <type> <hex> | /coconut raw packetraw <hex>");
                            }
                            if (!field.TryApplyPacket(packetType, payload, currTickCount, out string packetError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetError ?? "Failed to apply Coconut packet.");
                            }


                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_coconutPacketInbox.LastStatus}{Environment.NewLine}{_coconutOfficialSessionBridge.LastStatus}");
                            }


                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = CoconutPacketInboxManager.DefaultPort;
                                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /coconut inbox start [port]");
                                }


                                _coconutPacketInbox.Start(port);

                                return ChatCommandHandler.CommandResult.Ok(_coconutPacketInbox.LastStatus);

                            }



                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _coconutPacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_coconutPacketInbox.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /coconut inbox [status|start [port]|stop]");



                        case "session":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_coconutOfficialSessionBridge.DescribeStatus()}");
                            }
                            if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3
                                    || !int.TryParse(args[2], out int discoverRemotePort)
                                    || discoverRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /coconut session discover <remotePort> [processName|pid] [localPort]");
                                }
                                string processSelector = args.Length >= 4 ? args[3] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 5)
                                {
                                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /coconut session discover <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }
                                return ChatCommandHandler.CommandResult.Info(
                                    _coconutOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                            }


                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 5
                                    || !int.TryParse(args[2], out int listenPort)
                                    || listenPort <= 0
                                    || !int.TryParse(args[4], out int remotePort)
                                    || remotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /coconut session start <listenPort> <serverHost> <serverPort>");
                                }


                                return _coconutOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }
                            if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 4
                                    || !int.TryParse(args[2], out int autoListenPort)
                                    || autoListenPort <= 0
                                    || !int.TryParse(args[3], out int autoRemotePort)
                                    || autoRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /coconut session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }
                                string processSelector = args.Length >= 5 ? args[4] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 6)
                                {
                                    if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /coconut session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }
                                return _coconutOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }


                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _coconutOfficialSessionBridge.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_coconutOfficialSessionBridge.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /coconut session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");



                        case "request":
                            if (args.Length == 1 || string.Equals(args[1], "peek", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!field.TryPeekAttackPacketRequest(out CoconutField.AttackPacketRequest request))
                                {
                                    return ChatCommandHandler.CommandResult.Info("No pending Coconut attack request.");
                                }


                                return ChatCommandHandler.CommandResult.Info(

                                    $"Pending Coconut attack request: target={request.TargetId}, delay={request.DelayMs}, requestedAt={request.RequestedAtTick}");

                            }



                            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                            {
                                field.ClearPendingAttackPacketRequests();
                                return ChatCommandHandler.CommandResult.Ok("Cleared pending Coconut attack requests.");
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /coconut request [peek|clear]");




                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /coconut [status|clock <seconds>|hit <target|-1> <delay> <state>|score <maple> <story>|raw <type> <hex>|raw packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|request [peek|clear]]");
                    }

                });



            _chat.CommandHandler.RegisterCommand(
                "snowball",
                "Inspect or drive the SnowBall minigame packet, chat, and touch flow",
                "/snowball [status|state <state> <team0Hp> <team1Hp> <team0Pos> <team0Speed> <team1Pos> <team1Speed> [damageSnowBall damageSnowMan0 damageSnowMan1]|hit <target> <damage> <delay>|msg <team> <type>|touch|raw <type> <hex>|raw packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|request [peek|clear]]",
                args =>
                {
                    SnowBallField field = _specialFieldRuntime.Minigames.SnowBall;
                    if (!field.IsActive && field.State == SnowBallField.GameState.NotStarted)
                    {
                        return ChatCommandHandler.CommandResult.Error("SnowBall runtime is only active on SnowBall maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{field.DescribeStatus()}{Environment.NewLine}{_snowBallPacketInbox.LastStatus}{Environment.NewLine}{_snowBallOfficialSessionBridge.DescribeStatus()}");
                    }

                    switch (args[0].ToLowerInvariant())
                    {
                        case "state":
                            if (args.Length < 8
                                || !int.TryParse(args[1], out int newState)
                                || !int.TryParse(args[2], out int team0SnowManHp)
                                || !int.TryParse(args[3], out int team1SnowManHp)
                                || !int.TryParse(args[4], out int team0Pos)
                                || !int.TryParse(args[5], out int team0SpeedDegree)
                                || !int.TryParse(args[6], out int team1Pos)
                                || !int.TryParse(args[7], out int team1SpeedDegree))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /snowball state <state> <team0Hp> <team1Hp> <team0Pos> <team0Speed> <team1Pos> <team1Speed> [damageSnowBall damageSnowMan0 damageSnowMan1]");
                            }

                            int? damageSnowBall = null;
                            int? damageSnowMan0 = null;
                            int? damageSnowMan1 = null;
                            if (args.Length >= 11)
                            {
                                if (!int.TryParse(args[8], out int parsedDamageSnowBall)
                                    || !int.TryParse(args[9], out int parsedDamageSnowMan0)
                                    || !int.TryParse(args[10], out int parsedDamageSnowMan1))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /snowball state <state> <team0Hp> <team1Hp> <team0Pos> <team0Speed> <team1Pos> <team1Speed> [damageSnowBall damageSnowMan0 damageSnowMan1]");
                                }

                                damageSnowBall = parsedDamageSnowBall;
                                damageSnowMan0 = parsedDamageSnowMan0;
                                damageSnowMan1 = parsedDamageSnowMan1;
                            }

                            field.OnSnowBallState(
                                newState,
                                team0SnowManHp,
                                team1SnowManHp,
                                team0Pos,
                                team0SpeedDegree,
                                team1Pos,
                                team1SpeedDegree,
                                damageSnowBall,
                                damageSnowMan0,
                                damageSnowMan1);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "hit":
                            if (args.Length < 4
                                || !int.TryParse(args[1], out int hitTarget)
                                || !int.TryParse(args[2], out int hitDamage)
                                || !int.TryParse(args[3], out int hitDelay))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /snowball hit <target> <damage> <delay>");
                            }

                            field.OnSnowBallHit(hitTarget, hitDamage, hitDelay);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "msg":
                            if (args.Length < 3
                                || !int.TryParse(args[1], out int messageTeam)
                                || !int.TryParse(args[2], out int messageType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /snowball msg <team> <type>");
                            }

                            field.OnSnowBallMsg(messageTeam, messageType);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "touch":
                            field.OnSnowBallTouch();
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "raw":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /snowball raw <type> <hex> | /snowball raw packetraw <hex>");
                            }

                            if (!SnowBallPacketInboxManager.TryParsePacketLine(
                                    string.Join(' ', args.Skip(1)),
                                    out int packetType,
                                    out byte[] payload,
                                    out bool ignoredPacket,
                                    out string parseMessage))
                            {
                                return ignoredPacket
                                    ? ChatCommandHandler.CommandResult.Info(parseMessage ?? "Ignored SnowBall outbound echo packet.")
                                    : ChatCommandHandler.CommandResult.Error(parseMessage ?? "Usage: /snowball raw <type> <hex> | /snowball raw packetraw <hex>");
                            }

                            if (!field.TryApplyPacket(packetType, payload, currTickCount, out string packetError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetError ?? "Failed to apply SnowBall packet.");
                            }

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_snowBallPacketInbox.LastStatus}{Environment.NewLine}{_snowBallOfficialSessionBridge.DescribeStatus()}");
                            }

                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = SnowBallPacketInboxManager.DefaultPort;
                                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /snowball inbox start [port]");
                                }

                                _snowBallPacketInbox.Start(port);
                                return ChatCommandHandler.CommandResult.Ok(_snowBallPacketInbox.LastStatus);
                            }

                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _snowBallPacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_snowBallPacketInbox.LastStatus);
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /snowball inbox [status|start [port]|stop]");

                        case "session":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_snowBallOfficialSessionBridge.DescribeStatus()}");
                            }

                            if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3
                                    || !int.TryParse(args[2], out int discoverRemotePort)
                                    || discoverRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /snowball session discover <remotePort> [processName|pid] [localPort]");
                                }

                                string processSelector = args.Length >= 4 ? args[3] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 5)
                                {
                                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /snowball session discover <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }

                                return ChatCommandHandler.CommandResult.Info(
                                    _snowBallOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                            }

                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 5
                                    || !int.TryParse(args[2], out int listenPort)
                                    || listenPort <= 0
                                    || !int.TryParse(args[4], out int remotePort)
                                    || remotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /snowball session start <listenPort> <serverHost> <serverPort>");
                                }

                                return _snowBallOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }

                            if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 4
                                    || !int.TryParse(args[2], out int autoListenPort)
                                    || autoListenPort <= 0
                                    || !int.TryParse(args[3], out int autoRemotePort)
                                    || autoRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /snowball session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                string processSelector = args.Length >= 5 ? args[4] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 6)
                                {
                                    if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /snowball session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }

                                return _snowBallOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }

                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _snowBallOfficialSessionBridge.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_snowBallOfficialSessionBridge.LastStatus);
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /snowball session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");

                        case "request":
                            if (args.Length == 1 || string.Equals(args[1], "peek", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!field.TryPeekTouchPacketRequest(out SnowBallField.TouchPacketRequest request))
                                {
                                    return ChatCommandHandler.CommandResult.Info("No pending SnowBall touch request.");
                                }

                                return ChatCommandHandler.CommandResult.Info(
                                    $"Pending SnowBall touch request: team={request.Team}, requestedAt={request.TickCount}, sequence={request.Sequence}");
                            }

                            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                            {
                                field.ClearPendingTouchPacketRequests();
                                return ChatCommandHandler.CommandResult.Ok("Cleared pending SnowBall touch requests.");
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /snowball request [peek|clear]");

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /snowball [status|state <state> <team0Hp> <team1Hp> <team0Pos> <team0Speed> <team1Pos> <team1Speed> [damageSnowBall damageSnowMan0 damageSnowMan1]|hit <target> <damage> <delay>|msg <team> <type>|touch|raw <type> <hex>|raw packetraw <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|request [peek|clear]]");
                    }
                });



            _chat.CommandHandler.RegisterCommand(
                "ariantarena",
                "Inspect or drive the Ariant Arena ranking, result HUD, and remote actor overlay",
                "/ariantarena [score <name> <score>|packet <name> <score> [<name> <score> ...]|raw <type> <hex>|actor <add|avatar|move|remove|clear|status> ...|inbox [status|start [port]|stop]|remove <name>|result|clear]",
                args =>
                {
                    AriantArenaField field = _specialFieldRuntime.Minigames.AriantArena;
                    if (!field.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Ariant Arena runtime is only active on Ariant Arena maps");
                    }


                    static bool TryParseActorPlacement(string[] commandArgs, int xIndex, int yIndex, out Vector2 position, out string error)
                    {
                        position = Vector2.Zero;
                        error = null;
                        if (commandArgs.Length <= yIndex
                            || !float.TryParse(commandArgs[xIndex], out float x)
                            || !float.TryParse(commandArgs[yIndex], out float y))
                        {
                            error = "Ariant actor position requires numeric <x> <y> world coordinates.";
                            return false;
                        }


                        position = new Vector2(x, y);

                        return true;

                    }



                    static bool TryParseActorFacingAndAction(string[] commandArgs, int startIndex, out string actionName, out bool? facingRight, out string error)
                    {
                        actionName = null;
                        facingRight = null;
                        error = null;


                        for (int i = startIndex; i < commandArgs.Length; i++)
                        {
                            string token = commandArgs[i];
                            if (string.IsNullOrWhiteSpace(token))
                            {
                                continue;
                            }


                            if (string.Equals(token, "left", StringComparison.OrdinalIgnoreCase))
                            {
                                facingRight = false;
                                continue;
                            }


                            if (string.Equals(token, "right", StringComparison.OrdinalIgnoreCase))
                            {
                                facingRight = true;
                                continue;
                            }


                            if (actionName == null)
                            {
                                actionName = token;
                                continue;
                            }


                            error = $"Unexpected Ariant actor token '{token}'.";

                            return false;

                        }



                        return true;

                    }



                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "score":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena score <name> <score>");
                            }


                            if (!int.TryParse(args[2], out int score))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Ariant Arena score: {args[2]}");
                            }


                            field.OnUserScore(args[1], score);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "packet":
                            if (args.Length < 3 || args.Length % 2 == 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena packet <name> <score> [<name> <score> ...]");
                            }


                            var updates = new List<AriantArenaScoreUpdate>();
                            for (int i = 1; i < args.Length; i += 2)
                            {
                                if (!int.TryParse(args[i + 1], out int packetScore))
                                {
                                    return ChatCommandHandler.CommandResult.Error($"Invalid Ariant Arena score: {args[i + 1]}");
                                }


                                updates.Add(new AriantArenaScoreUpdate(args[i], packetScore));

                            }



                            field.ApplyUserScoreBatch(updates);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "actor":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
                            }


                            switch (args[1].ToLowerInvariant())
                            {
                                case "add":
                                    if (args.Length < 5)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena actor add <name> <x> <y> [action] [left|right]");
                                    }


                                    if (!TryParseActorPlacement(args, 3, 4, out Vector2 addPosition, out string addPlacementError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(addPlacementError);
                                    }


                                    if (!TryParseActorFacingAndAction(args, 5, out string addActionName, out bool? addFacingRight, out string addParseError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(addParseError);
                                    }


                                    CharacterBuild addTemplate = _playerManager?.Player?.Build?.Clone();
                                    if (addTemplate == null)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("No local player build is available to clone for the remote Ariant actor.");
                                    }


                                    addTemplate.Name = args[2];

                                    field.UpsertRemoteParticipant(addTemplate, addPosition, addFacingRight ?? true, addActionName);
                                    return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                                case "avatar":
                                    if (args.Length < 6)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena actor avatar <name> <x> <y> <avatarLookHex> [action] [left|right]");
                                    }


                                    if (!TryParseActorPlacement(args, 3, 4, out Vector2 avatarPosition, out string avatarPlacementError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(avatarPlacementError);
                                    }


                                    if (!TryParseActorFacingAndAction(args, 6, out string avatarActionName, out bool? avatarFacingRight, out string avatarParseError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(avatarParseError);
                                    }


                                    if (_playerManager?.Loader == null)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Character loader is not available for Ariant avatar actor decoding.");
                                    }


                                    byte[] avatarPayload;
                                    try
                                    {
                                        avatarPayload = ByteUtils.HexToBytes(args[5]);
                                    }
                                    catch (Exception ex)
                                    {
                                        return ChatCommandHandler.CommandResult.Error($"Invalid AvatarLook hex payload: {ex.Message}");
                                    }


                                    if (!LoginAvatarLookCodec.TryDecode(avatarPayload, out LoginAvatarLook avatarLook, out string avatarDecodeError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(avatarDecodeError ?? "AvatarLook payload could not be decoded.");
                                    }


                                    CharacterBuild avatarTemplate = _playerManager?.Player?.Build?.Clone();

                                    CharacterBuild avatarBuild = _playerManager.Loader.LoadFromAvatarLook(avatarLook, avatarTemplate);

                                    avatarBuild.Name = args[2];

                                    field.UpsertRemoteParticipant(avatarBuild, avatarPosition, avatarFacingRight ?? true, avatarActionName);
                                    return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());


                                case "move":
                                    if (args.Length < 5)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena actor move <name> <x> <y> [action] [left|right]");
                                    }


                                    if (!TryParseActorPlacement(args, 3, 4, out Vector2 movePosition, out string movePlacementError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(movePlacementError);
                                    }


                                    if (!TryParseActorFacingAndAction(args, 5, out string moveActionName, out bool? moveFacingRight, out string moveParseError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(moveParseError);
                                    }


                                    if (!field.TryMoveRemoteParticipant(args[2], movePosition, moveFacingRight, moveActionName, out string moveMessage))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(moveMessage);
                                    }


                                    return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());


                                case "remove":
                                    if (args.Length < 3)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena actor remove <name>");
                                    }


                                    return field.RemoveRemoteParticipant(args[2])
                                        ? ChatCommandHandler.CommandResult.Ok(field.DescribeStatus())

                                        : ChatCommandHandler.CommandResult.Error($"Remote Ariant actor '{args[2]}' does not exist.");



                                case "clear":

                                    field.ClearRemoteParticipants();
                                    return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                                default:

                                    return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena actor <add|avatar|move|remove|clear|status> ...");

                            }



                        case "raw":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena raw <type> <hex>");
                            }


                            if (!int.TryParse(args[1], out int packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Ariant packet type: {args[1]}");
                            }


                            byte[] payload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                            if (!field.TryApplyPacket(packetType, payload, currTickCount, out string packetError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetError ?? "Failed to apply Ariant packet.");
                            }


                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_ariantArenaPacketInbox.LastStatus}");
                            }


                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = AriantArenaPacketInboxManager.DefaultPort;
                                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena inbox start [port]");
                                }


                                _ariantArenaPacketInbox.Start(port);

                                return ChatCommandHandler.CommandResult.Ok(_ariantArenaPacketInbox.LastStatus);

                            }



                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _ariantArenaPacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_ariantArenaPacketInbox.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena inbox [status|start [port]|stop]");



                        case "remove":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena remove <name>");
                            }


                            field.OnUserScore(args[1], -1);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "result":

                            field.OnShowResult(currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "clear":

                            field.ClearScores();

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena [score <name> <score>|packet <name> <score> [<name> <score> ...]|raw <type> <hex>|actor <add|avatar|move|remove|clear|status> ...|inbox [status|start [port]|stop]|remove <name>|result|clear]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(

                "mcarnival",

                "Inspect or drive the Monster Carnival HUD state",

                "/mcarnival [status|tab <mob|skill|guardian>|enter <team> <personalCP> <personalTotal> <myCP> <myTotal> <enemyCP> <enemyTotal>|cp <personalCP> <personalTotal> <team0CP> <team0Total> <team1CP> <team1Total>|cpdelta <personalDelta> <personalTotalDelta> <team0Delta> <team0TotalDelta> <team1Delta> <team1TotalDelta>|request <index> [message]|requestok <mob|skill|guardian> <index> [message]|requestfail <reason>|result <code>|death <team> <name> <remainingRevives>|spells <mobIndex> <count>|raw <type> <hex>|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <enter|personalcp|teamcp|requestresult|requestfailure|processfordeath|memberout|gameresult>|unmap <opcode>|clearmap|recent|stop]]",
                args =>
                {
                    static bool TryParseMonsterCarnivalSessionPacketType(string text, out int packetType)
                    {
                        packetType = text?.Trim().ToLowerInvariant() switch
                        {
                            "enter" => 346,
                            "personalcp" or "personal" => 347,
                            "teamcp" or "team" => 348,
                            "requestresult" or "requestok" => 349,
                            "requestfailure" or "requestfail" or "fail" => 350,
                            "processfordeath" or "death" => 351,
                            "memberout" or "showmemberoutmessage" => 352,
                            "gameresult" or "result" => 353,
                            _ => -1
                        };

                        return packetType >= 346;
                    }

                    MonsterCarnivalField field = _specialFieldRuntime.Minigames.MonsterCarnival;
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{field.DescribeStatus()}{Environment.NewLine}{_monsterCarnivalPacketInbox.LastStatus}{Environment.NewLine}{_monsterCarnivalOfficialSessionBridge.DescribeStatus()}");
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "tab":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival tab <mob|skill|guardian>");
                            }


                            return field.TrySetActiveTab(args[1], out string tabMessage)

                                ? ChatCommandHandler.CommandResult.Ok(tabMessage)

                                : ChatCommandHandler.CommandResult.Error(tabMessage);



                        case "enter":
                            if (args.Length < 8)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival enter <team 0|1> <personalCP> <personalTotal> <myCP> <myTotal> <enemyCP> <enemyTotal>");
                            }


                            if (!int.TryParse(args[1], out int teamValue) || (teamValue != 0 && teamValue != 1))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival team must be 0 or 1.");
                            }


                            if (!int.TryParse(args[2], out int personalCp)
                                || !int.TryParse(args[3], out int personalTotalCp)
                                || !int.TryParse(args[4], out int myCp)
                                || !int.TryParse(args[5], out int myTotalCp)
                                || !int.TryParse(args[6], out int enemyCp)
                                || !int.TryParse(args[7], out int enemyTotalCp))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival enter arguments must be integers.");
                            }


                            field.OnEnter((MonsterCarnivalTeam)teamValue, personalCp, personalTotalCp, myCp, myTotalCp, enemyCp, enemyTotalCp);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "cp":
                            if (args.Length < 7)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival cp <personalCP> <personalTotal> <team0CP> <team0Total> <team1CP> <team1Total>");
                            }


                            if (!int.TryParse(args[1], out int updatedPersonalCp)
                                || !int.TryParse(args[2], out int updatedPersonalTotalCp)
                                || !int.TryParse(args[3], out int team0Cp)
                                || !int.TryParse(args[4], out int team0TotalCp)
                                || !int.TryParse(args[5], out int team1Cp)
                                || !int.TryParse(args[6], out int team1TotalCp))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival CP arguments must be integers.");
                            }


                            field.UpdateTeamCp(updatedPersonalCp, updatedPersonalTotalCp, team0Cp, team0TotalCp, team1Cp, team1TotalCp);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "cpdelta":
                            if (args.Length < 7)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival cpdelta <personalDelta> <personalTotalDelta> <team0Delta> <team0TotalDelta> <team1Delta> <team1TotalDelta>");
                            }


                            if (!int.TryParse(args[1], out int personalCpDelta)
                                || !int.TryParse(args[2], out int personalTotalCpDelta)
                                || !int.TryParse(args[3], out int team0CpDelta)
                                || !int.TryParse(args[4], out int team0TotalCpDelta)
                                || !int.TryParse(args[5], out int team1CpDelta)
                                || !int.TryParse(args[6], out int team1TotalCpDelta))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival CP delta arguments must be integers.");
                            }


                            field.ApplyTeamCpDelta(personalCpDelta, personalTotalCpDelta, team0CpDelta, team0TotalCpDelta, team1CpDelta, team1TotalCpDelta, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "request":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival request <index> [message]");
                            }


                            if (!int.TryParse(args[1], out int entryIndex) || entryIndex < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Monster Carnival entry index: {args[1]}");
                            }


                            string requestMessage = args.Length > 2 ? string.Join(" ", args.Skip(2)) : null;
                            return field.TryRequestActiveEntry(entryIndex, requestMessage, currTickCount, out string requestResult)
                                ? ChatCommandHandler.CommandResult.Ok(requestResult)
                                : ChatCommandHandler.CommandResult.Error(requestResult);


                        case "requestfail":
                            if (args.Length < 2 || !int.TryParse(args[1], out int reasonCode))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival requestfail <reason>");
                            }


                            field.OnRequestFailure(reasonCode, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "requestok":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival requestok <mob|skill|guardian> <index> [message]");
                            }


                            byte requestTab = args[1].ToLowerInvariant() switch
                            {
                                "mob" or "mobs" => 0,
                                "skill" or "skills" => 1,
                                "guardian" or "guardians" => 2,
                                _ => byte.MaxValue
                            };


                            if (requestTab == byte.MaxValue)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Unknown Monster Carnival tab: {args[1]}");
                            }


                            if (!int.TryParse(args[2], out int requestIndex) || requestIndex < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Monster Carnival entry index: {args[2]}");
                            }


                            field.OnRequestResult(requestTab, requestIndex, args.Length > 3 ? string.Join(" ", args.Skip(3)) : null, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "result":
                            if (args.Length < 2 || !int.TryParse(args[1], out int resultCode))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival result <code>");
                            }


                            field.OnShowGameResult(resultCode, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "death":
                            if (args.Length < 4)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival death <team 0|1> <name> <remainingRevives>");
                            }


                            if (!int.TryParse(args[1], out int deathTeamValue) || (deathTeamValue != 0 && deathTeamValue != 1))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival death team must be 0 or 1.");
                            }


                            if (!int.TryParse(args[^1], out int remainingRevives) || remainingRevives < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Monster Carnival revive count: {args[^1]}");
                            }


                            string characterName = string.Join(" ", args.Skip(2).Take(args.Length - 3));

                            field.OnProcessForDeath((MonsterCarnivalTeam)deathTeamValue, characterName, remainingRevives, currTickCount);

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "spells":
                            if (args.Length < 3
                                || !int.TryParse(args[1], out int mobIndex)
                                || !int.TryParse(args[2], out int spellCount))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival spells <mobIndex> <count>");
                            }


                            return field.TrySetMobSpellCount(mobIndex, spellCount, out string spellMessage)

                                ? ChatCommandHandler.CommandResult.Ok(spellMessage)

                                : ChatCommandHandler.CommandResult.Error(spellMessage);



                        case "raw":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival raw <type> <hex>");
                            }


                            if (!int.TryParse(args[1], out int rawPacketType))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Monster Carnival packet type: {args[1]}");
                            }


                            byte[] rawPayload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                            if (!field.TryApplyRawPacket(rawPacketType, rawPayload, currTickCount, out string rawPacketError))
                            {
                                return ChatCommandHandler.CommandResult.Error(rawPacketError ?? "Failed to apply Monster Carnival packet.");
                            }


                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());



                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_monsterCarnivalPacketInbox.LastStatus}{Environment.NewLine}{_monsterCarnivalOfficialSessionBridge.DescribeStatus()}");
                            }


                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = MonsterCarnivalPacketInboxManager.DefaultPort;
                                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival inbox start [port]");
                                }


                                _monsterCarnivalPacketInbox.Start(port);

                                return ChatCommandHandler.CommandResult.Ok(_monsterCarnivalPacketInbox.LastStatus);

                            }



                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _monsterCarnivalPacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_monsterCarnivalPacketInbox.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival inbox [status|start [port]|stop]");

                        case "session":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_monsterCarnivalOfficialSessionBridge.DescribeStatus()}");
                            }

                            if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3
                                    || !int.TryParse(args[2], out int discoverRemotePort)
                                    || discoverRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session discover <remotePort> [processName|pid] [localPort]");
                                }

                                string processSelector = args.Length >= 4 ? args[3] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 5)
                                {
                                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session discover <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }

                                return ChatCommandHandler.CommandResult.Info(
                                    _monsterCarnivalOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                            }

                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 5
                                    || !int.TryParse(args[2], out int listenPort)
                                    || listenPort <= 0
                                    || !int.TryParse(args[4], out int remotePort)
                                    || remotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session start <listenPort> <serverHost> <serverPort>");
                                }

                                return _monsterCarnivalOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startMessage)
                                    ? ChatCommandHandler.CommandResult.Ok(startMessage)
                                    : ChatCommandHandler.CommandResult.Error(startMessage);
                            }

                            if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 4
                                    || !int.TryParse(args[2], out int autoListenPort)
                                    || autoListenPort <= 0
                                    || !int.TryParse(args[3], out int autoRemotePort)
                                    || autoRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                string processSelector = args.Length >= 5 ? args[4] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 6)
                                {
                                    if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }

                                return _monsterCarnivalOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }

                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _monsterCarnivalOfficialSessionBridge.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_monsterCarnivalOfficialSessionBridge.LastStatus);
                            }

                            if (string.Equals(args[1], "map", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 4
                                    || !int.TryParse(args[2], out int opcode)
                                    || opcode <= 0
                                    || !TryParseMonsterCarnivalSessionPacketType(args[3], out int packetType))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session map <opcode> <enter|personalcp|teamcp|requestresult|requestfailure|processfordeath|memberout|gameresult>");
                                }

                                return _monsterCarnivalOfficialSessionBridge.TryConfigurePacketMapping(opcode, packetType, out string mapStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(mapStatus)
                                    : ChatCommandHandler.CommandResult.Error(mapStatus);
                            }

                            if (string.Equals(args[1], "unmap", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3
                                    || !int.TryParse(args[2], out int opcode)
                                    || opcode <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session unmap <opcode>");
                                }

                                return _monsterCarnivalOfficialSessionBridge.RemovePacketMapping(opcode, out string unmapStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(unmapStatus)
                                    : ChatCommandHandler.CommandResult.Error(unmapStatus);
                            }

                            if (string.Equals(args[1], "clearmap", StringComparison.OrdinalIgnoreCase))
                            {
                                _monsterCarnivalOfficialSessionBridge.ClearPacketMappings();
                                return ChatCommandHandler.CommandResult.Ok(_monsterCarnivalOfficialSessionBridge.LastStatus);
                            }

                            if (string.Equals(args[1], "recent", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(_monsterCarnivalOfficialSessionBridge.DescribeRecentPackets());
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <enter|personalcp|teamcp|requestresult|requestfailure|processfordeath|memberout|gameresult>|unmap <opcode>|clearmap|recent|stop]");



                        default:

                            return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival [status|tab|enter|cp|cpdelta|request|requestok|requestfail|result|death|spells|raw|inbox|session] [...]");
                    }

                });



            _chat.CommandHandler.RegisterCommand(

                "dojo",

                "Inspect the Mu Lung Dojo HUD state, loopback inbox, or official-session bridge",
                "/dojo [status|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <clock|stage|clear|timeover>|unmap <opcode>|clearmap|recent|stop]]",
                args =>
                {
                    static bool TryParseDojoSessionPacketType(string text, out int packetType)
                    {
                        packetType = text?.Trim().ToLowerInvariant() switch
                        {
                            "clock" => DojoField.PacketTypeClock,
                            "stage" => DojoField.PacketTypeStage,
                            "clear" => DojoField.PacketTypeClear,
                            "timeover" => DojoField.PacketTypeTimeOver,
                            _ => -1
                        };

                        return packetType >= DojoField.PacketTypeClock;
                    }

                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoPacketInbox.LastStatus}{Environment.NewLine}{_dojoOfficialSessionBridge.DescribeStatus()}");
                    }


                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoPacketInbox.LastStatus}{Environment.NewLine}{_dojoOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = DojoPacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo inbox start [port]");
                            }


                            _dojoPacketInbox.Start(port);

                            return ChatCommandHandler.CommandResult.Ok(_dojoPacketInbox.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _dojoPacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_dojoPacketInbox.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /dojo inbox [status|start [port]|stop]");

                    }

                    if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoOfficialSessionBridge.DescribeStatus()}");
                        }

                        if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int discoverRemotePort)
                                || discoverRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo session discover <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 4 ? args[3] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 5)
                            {
                                if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /dojo session discover <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return ChatCommandHandler.CommandResult.Info(
                                _dojoOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                        }

                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 5
                                || !int.TryParse(args[2], out int listenPort)
                                || listenPort <= 0
                                || !int.TryParse(args[4], out int remotePort)
                                || remotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo session start <listenPort> <serverHost> <serverPort>");
                            }

                            return _dojoOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }

                        if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int autoListenPort)
                                || autoListenPort <= 0
                                || !int.TryParse(args[3], out int autoRemotePort)
                                || autoRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 5 ? args[4] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 6)
                            {
                                if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /dojo session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return _dojoOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }

                        if (string.Equals(args[1], "map", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int opcode)
                                || opcode <= 0
                                || !TryParseDojoSessionPacketType(args[3], out int packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo session map <opcode> <clock|stage|clear|timeover>");
                            }

                            return _dojoOfficialSessionBridge.TryConfigurePacketMapping(opcode, packetType, out string mapStatus)
                                ? ChatCommandHandler.CommandResult.Ok(mapStatus)
                                : ChatCommandHandler.CommandResult.Error(mapStatus);
                        }

                        if (string.Equals(args[1], "unmap", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int opcode)
                                || opcode <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /dojo session unmap <opcode>");
                            }

                            return _dojoOfficialSessionBridge.RemovePacketMapping(opcode, out string unmapStatus)
                                ? ChatCommandHandler.CommandResult.Ok(unmapStatus)
                                : ChatCommandHandler.CommandResult.Error(unmapStatus);
                        }

                        if (string.Equals(args[1], "clearmap", StringComparison.OrdinalIgnoreCase))
                        {
                            _dojoOfficialSessionBridge.ClearPacketMappings();
                            return ChatCommandHandler.CommandResult.Ok(_dojoOfficialSessionBridge.LastStatus);
                        }

                        if (string.Equals(args[1], "recent", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoOfficialSessionBridge.DescribeStatus()}");
                        }

                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _dojoOfficialSessionBridge.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_dojoOfficialSessionBridge.LastStatus);
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /dojo session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <clock|stage|clear|timeover>|unmap <opcode>|clearmap|recent|stop]");
                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /dojo [status|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <clock|stage|clear|timeover>|unmap <opcode>|clearmap|recent|stop]]");
                });


            _chat.CommandHandler.RegisterCommand(
                "dojoclock",
                "Inspect or update the Mu Lung Dojo timer",
                "/dojoclock [seconds]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }


                    if (!int.TryParse(args[0], out int seconds) || seconds < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Dojo timer: {args[0]}");
                    }


                    _specialFieldRuntime.SpecialEffects.Dojo.OnClock(2, seconds, currTickCount);

                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());

                });



            _chat.CommandHandler.RegisterCommand(
                "spacegaga",
                "Inspect or update the SpaceGAGA timerboard",
                "/spacegaga [seconds]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.SpaceGaga.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("SpaceGAGA timerboard is only active on SpaceGAGA maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.SpaceGaga.DescribeStatus());
                    }


                    if (!int.TryParse(args[0], out int seconds))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid SpaceGAGA timer: {args[0]}");
                    }


                    _specialFieldRuntime.SpecialEffects.SpaceGaga.OnClock(2, seconds, currTickCount);

                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.SpaceGaga.DescribeStatus());

                });



            _chat.CommandHandler.RegisterCommand(

                "massacre",

                "Inspect or drive the Massacre timerboard and gauge flow",

                "/massacre [status|clock <seconds>|kill [gauge]|inc <value>|info <hit> <miss> <cool> [skill]|stage <index>|params <maxGauge> <decayPerSec>|bonus|result <clear|fail> [score] [rank]|reset|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]",
                args =>
                {
                    MassacreField massacre = _specialFieldRuntime.SpecialEffects.Massacre;
                    if (!massacre.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Massacre HUD is only active on Massacre maps");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{massacre.DescribeStatus()}{Environment.NewLine}{_massacrePacketInbox.LastStatus}{Environment.NewLine}{_massacreOfficialSessionBridge.DescribeStatus()}");
                    }


                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{massacre.DescribeStatus()}{Environment.NewLine}{_massacrePacketInbox.LastStatus}{Environment.NewLine}{_massacreOfficialSessionBridge.DescribeStatus()}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = MassacrePacketInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre inbox start [port]");
                            }


                            _massacrePacketInbox.Start(port);

                            return ChatCommandHandler.CommandResult.Ok(_massacrePacketInbox.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _massacrePacketInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_massacrePacketInbox.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /massacre inbox [status|start [port]|stop]");

                    }

                    if (string.Equals(args[0], "session", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{massacre.DescribeStatus()}{Environment.NewLine}{_massacreOfficialSessionBridge.DescribeStatus()}");
                        }

                        if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 3
                                || !int.TryParse(args[2], out int discoverRemotePort)
                                || discoverRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre session discover <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 4 ? args[3] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 5)
                            {
                                if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /massacre session discover <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return ChatCommandHandler.CommandResult.Info(
                                _massacreOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                        }

                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 5
                                || !int.TryParse(args[2], out int listenPort)
                                || listenPort <= 0
                                || !int.TryParse(args[4], out int remotePort)
                                || remotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre session start <listenPort> <serverHost> <serverPort>");
                            }

                            return _massacreOfficialSessionBridge.TryStart(listenPort, args[3], remotePort, out string startMessage)
                                ? ChatCommandHandler.CommandResult.Ok(startMessage)
                                : ChatCommandHandler.CommandResult.Error(startMessage);
                        }

                        if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length < 4
                                || !int.TryParse(args[2], out int autoListenPort)
                                || autoListenPort <= 0
                                || !int.TryParse(args[3], out int autoRemotePort)
                                || autoRemotePort <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                            }

                            string processSelector = args.Length >= 5 ? args[4] : null;
                            int? localPortFilter = null;
                            if (args.Length >= 6)
                            {
                                if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /massacre session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }

                                localPortFilter = parsedLocalPort;
                            }

                            return _massacreOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                : ChatCommandHandler.CommandResult.Error(startStatus);
                        }

                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _massacreOfficialSessionBridge.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_massacreOfficialSessionBridge.LastStatus);
                        }

                        return ChatCommandHandler.CommandResult.Error("Usage: /massacre session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
                    }



                    if (string.Equals(args[0], "clock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int seconds) || seconds < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre clock <seconds>");
                        }


                        massacre.OnClock(2, seconds, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "kill", StringComparison.OrdinalIgnoreCase))
                    {
                        int gaugeAmount = massacre.DefaultGaugeIncrease;
                        if (args.Length >= 2 && (!int.TryParse(args[1], out gaugeAmount) || gaugeAmount < 0))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre kill [gauge]");
                        }


                        massacre.AddKill(gaugeAmount, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "inc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int incGauge) || incGauge < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre inc <value>");
                        }


                        massacre.OnMassacreIncGauge(incGauge, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "info", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[1], out int hit)
                            || !int.TryParse(args[2], out int miss)
                            || !int.TryParse(args[3], out int cool)
                            || hit < 0
                            || miss < 0
                            || cool < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre info <hit> <miss> <cool> [skill]");
                        }


                        int skill = massacre.SkillCount;
                        if (args.Length >= 5 && (!int.TryParse(args[4], out skill) || skill < 0))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre info <hit> <miss> <cool> [skill]");
                        }


                        massacre.SetMassacreInfo(hit, miss, cool, skill, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "stage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int stage) || stage <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre stage <index>");
                        }


                        massacre.ShowCountEffectPresentation(stage, currTickCount);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "params", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3
                            || !int.TryParse(args[1], out int maxGauge)
                            || !int.TryParse(args[2], out int decayPerSec)
                            || maxGauge <= 0
                            || decayPerSec < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre params <maxGauge> <decayPerSec>");
                        }


                        massacre.SetGaugeParameters(maxGauge, decayPerSec);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    if (string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
                    {
                        massacre.ResetRoundState();
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }


                    if (string.Equals(args[0], "bonus", StringComparison.OrdinalIgnoreCase))
                    {
                        massacre.ShowBonusPresentation(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }


                    if (string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre result <clear|fail> [score] [rank]");
                        }


                        bool clear;
                        if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                        {
                            clear = true;
                        }
                        else if (string.Equals(args[1], "fail", StringComparison.OrdinalIgnoreCase))
                        {
                            clear = false;
                        }
                        else
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre result <clear|fail> [score] [rank]");
                        }


                        int? scoreOverride = null;
                        if (args.Length >= 3)
                        {
                            if (!int.TryParse(args[2], out int score))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre result <clear|fail> [score] [rank]");
                            }


                            scoreOverride = score;

                        }



                        char? rankOverride = null;
                        if (args.Length >= 4)
                        {
                            if (args[3].Length != 1)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /massacre result <clear|fail> [score] [rank]");
                            }


                            rankOverride = args[3][0];

                        }



                        massacre.ShowResultPresentation(clear, currTickCount, scoreOverride, rankOverride);

                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());

                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /massacre [status|clock <seconds>|kill [gauge]|inc <value>|info <hit> <miss> <cool> [skill]|stage <index>|params <maxGauge> <decayPerSec>|bonus|result <clear|fail> [score] [rank]|reset|inbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]");
                });



            _chat.CommandHandler.RegisterCommand(
                "dojoenergy",
                "Inspect or update the Mu Lung Dojo energy gauge",
                "/dojoenergy [0-10000]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }


                    if (!int.TryParse(args[0], out int energy) || energy < 0 || energy > 10000)
                    {
                        return ChatCommandHandler.CommandResult.Error("Energy must be between 0 and 10000");
                    }


                    _specialFieldRuntime.SpecialEffects.Dojo.SetEnergy(energy);

                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());

                });



            _chat.CommandHandler.RegisterCommand(
                "dojostage",
                "Inspect or update the Mu Lung Dojo floor banner",
                "/dojostage [0-32]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }


                    if (!int.TryParse(args[0], out int stage) || stage < 0 || stage > 32)
                    {
                        return ChatCommandHandler.CommandResult.Error("Stage must be between 0 and 32");
                    }


                    _specialFieldRuntime.SpecialEffects.Dojo.SetStage(stage, currTickCount);

                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());

                });



            _chat.CommandHandler.RegisterCommand(
                "dojoresult",
                "Trigger Mu Lung Dojo clear or time-over presentation",
                "/dojoresult <clear|timeover> [auto|none|mapId]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }


                    if (args.Length < 1 || args.Length > 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover> [auto|none|mapId]");
                    }


                    if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "none", StringComparison.OrdinalIgnoreCase))
                        {
                            _specialFieldRuntime.SpecialEffects.Dojo.ShowClearResult(currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                        }


                        if (string.Equals(args[1], "auto", StringComparison.OrdinalIgnoreCase))
                        {
                            _specialFieldRuntime.SpecialEffects.Dojo.ShowClearResultForNextFloor(currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                        }


                        if (!int.TryParse(args[1], out int nextMapId) || nextMapId <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover> [auto|none|mapId]");
                        }


                        _specialFieldRuntime.SpecialEffects.Dojo.ShowClearResult(currTickCount, nextMapId);

                        return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());

                    }



                    if (string.Equals(args[0], "timeover", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1)
                        {
                            _specialFieldRuntime.SpecialEffects.Dojo.ShowTimeOverResult(currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                        }


                        if (!int.TryParse(args[1], out int exitMapId) || exitMapId <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover> [auto|none|mapId]");
                        }


                        _specialFieldRuntime.SpecialEffects.Dojo.ShowTimeOverResult(currTickCount, exitMapId);

                        return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());

                    }



                    return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover> [auto|none|mapId]");

                });



            _chat.CommandHandler.RegisterCommand(
                "cookiepoint",
                "Inspect or update the Cookie House event score or its loopback inbox",
                "/cookiepoint [score]|inbox [status|start [port]|stop]",
                args =>
                {
                    if (!_specialFieldRuntime.CookieHouse.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Cookie House HUD is only active on Cookie House maps");
                    }


                    if (args.Length == 0)
                    {
                        _specialFieldRuntime.CookieHouse.Update();
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.CookieHouse.DescribeStatus());
                    }


                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_specialFieldRuntime.CookieHouse.DescribeStatus()}{Environment.NewLine}{_cookieHousePointInbox.LastStatus}");
                        }


                        if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                        {
                            int port = CookieHousePointInboxManager.DefaultPort;
                            if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /cookiepoint inbox start [port]");
                            }


                            _cookieHousePointInbox.Start(port);

                            return ChatCommandHandler.CommandResult.Ok(_cookieHousePointInbox.LastStatus);

                        }



                        if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                        {
                            _cookieHousePointInbox.Stop();
                            return ChatCommandHandler.CommandResult.Ok(_cookieHousePointInbox.LastStatus);
                        }


                        return ChatCommandHandler.CommandResult.Error("Usage: /cookiepoint inbox [status|start [port]|stop]");

                    }



                    if (!int.TryParse(args[0], out int point))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Cookie House score: {args[0]}");
                    }


                    SetCookieHouseContextPoint(point);
                    _specialFieldRuntime.CookieHouse.Update();
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.CookieHouse.DescribeStatus());
                });


            _chat.CommandHandler.RegisterCommand(

                "guildbbs",

                "Inspect or drive the Guild BBS runtime",

                "/guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>|title <text>|body <text>|replytext <text>|threadpage <prev|next>|commentpage <prev|next>|packet <authority|cash> <payloadhex=..|payloadb64=..>|packetraw <authority|cash> <hex>|packet clear <authority|cash|all>]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_guildBbsRuntime.DescribeStatus());
                    }


                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok("Guild BBS window opened.");
                        case "write":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.BeginWrite());
                        case "edit":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.BeginEditSelected());
                        case "register":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.SubmitCompose());
                        case "cancel":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.CancelCompose());
                        case "notice":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ToggleNotice());
                        case "reply":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.AddReply());
                        case "title":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs title <text>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.SetComposeTitle(string.Join(" ", args.Skip(1))));
                        case "body":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs body <text>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.SetComposeBody(string.Join(" ", args.Skip(1))));
                        case "replytext":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs replytext <text>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.SetReplyDraft(string.Join(" ", args.Skip(1))));
                        case "threadpage":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs threadpage <prev|next>");
                            }


                            if (string.Equals(args[1], "prev", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.MoveThreadPage(-1));
                            }


                            if (string.Equals(args[1], "next", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.MoveThreadPage(1));
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs threadpage <prev|next>");
                        case "commentpage":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs commentpage <prev|next>");
                            }


                            if (string.Equals(args[1], "prev", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.MoveCommentPage(-1));
                            }


                            if (string.Equals(args[1], "next", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.MoveCommentPage(1));
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs commentpage <prev|next>");
                        case "replydelete":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.DeleteLatestReply());
                        case "delete":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.DeleteSelectedThread());
                        case "select":
                            if (args.Length < 2 || !int.TryParse(args[1], out int threadId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs select <threadId>");
                            }


                            _guildBbsRuntime.SelectThread(threadId);
                            return ChatCommandHandler.CommandResult.Ok($"Selected Guild BBS thread #{threadId}.");
                        case "packet":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet <authority|cash|clear> [...]");
                            }

                            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet clear <authority|cash|all>");
                                }

                                if (string.Equals(args[2], "authority", StringComparison.OrdinalIgnoreCase))
                                {
                                    return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ClearPermissionMaskOverride());
                                }

                                if (string.Equals(args[2], "cash", StringComparison.OrdinalIgnoreCase))
                                {
                                    return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ClearCashOwnershipPacket());
                                }

                                if (string.Equals(args[2], "all", StringComparison.OrdinalIgnoreCase))
                                {
                                    string authorityMessage = _guildBbsRuntime.ClearPermissionMaskOverride();
                                    string cashMessage = _guildBbsRuntime.ClearCashOwnershipPacket();
                                    return ChatCommandHandler.CommandResult.Ok($"{authorityMessage} {cashMessage}");
                                }

                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet clear <authority|cash|all>");

                            }


                            string guildBbsPacketError = null;
                            if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out byte[] guildBbsPacketPayload, out guildBbsPacketError))
                            {
                                return ChatCommandHandler.CommandResult.Error(guildBbsPacketError ?? "Usage: /guildbbs packet <authority|cash> <payloadhex=..|payloadb64=..>");
                            }

                            if (string.Equals(args[1], "authority", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ApplyPermissionPacket(guildBbsPacketPayload));
                            }

                            if (string.Equals(args[1], "cash", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ApplyCashOwnershipPacket(guildBbsPacketPayload));
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packet <authority|cash> <payloadhex=..|payloadb64=..>");
                        case "packetraw":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packetraw <authority|cash> <hex bytes>");
                            }

                            if (!TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] guildBbsRawPayload))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packetraw <authority|cash> <hex bytes>");
                            }

                            if (string.Equals(args[1], "authority", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ApplyPermissionPacket(guildBbsRawPayload));
                            }

                            if (string.Equals(args[1], "cash", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ApplyCashOwnershipPacket(guildBbsRawPayload));
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs packetraw <authority|cash> <hex bytes>");

                        default:

                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>|title <text>|body <text>|replytext <text>|threadpage <prev|next>|commentpage <prev|next>|packet <authority|cash> <payloadhex=..|payloadb64=..>|packetraw <authority|cash> <hex>|packet clear <authority|cash|all>]");
                    }

                });



            _chat.CommandHandler.RegisterCommand(
                "memorygame",
                "Drive the MiniRoom Match Cards runtime",
                "/memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetclientraw|packetrecv|remote|inbox|session> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetclientraw|packetrecv|remote|inbox|session> [...]");
                    }


                    MemoryGameField field = _specialFieldRuntime.Minigames.MemoryGame;
                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                        {
                            string playerOne = args.Length >= 2 ? args[1] : "Player";
                            string playerTwo = args.Length >= 3 ? args[2] : "Opponent";
                            int rows = args.Length >= 4 && int.TryParse(args[3], out int parsedRows) ? parsedRows : 4;
                            int columns = args.Length >= 5 && int.TryParse(args[4], out int parsedColumns) ? parsedColumns : 4;
                            field.OpenRoom(playerOneName: playerOne, playerTwoName: playerTwo, rows: rows, columns: columns);
                            if (!TryShowMiniRoomWindow(out string restrictionMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(restrictionMessage ?? "Mini-room interactions are blocked in this map.");
                            }
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());
                        }
                        case "ready":
                        {
                            int playerIndex = args.Length >= 2 && int.TryParse(args[1], out int parsedPlayer) ? parsedPlayer : 0;
                            bool isReady = args.Length < 3 || !string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase);
                            if (!field.TrySetReady(playerIndex, isReady, out string readyMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(readyMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(readyMessage);
                        }
                        case "start":
                        {
                            if (!field.TryStartGame(currTickCount, out string startMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(startMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(startMessage);
                        }
                        case "flip":
                        {
                            if (args.Length < 2 || !int.TryParse(args[1], out int cardIndex))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame flip <cardIndex>");
                            }


                            if (!field.TryRevealCard(cardIndex, currTickCount, out string flipMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(flipMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(flipMessage);
                        }
                        case "tie":
                        {
                            if (!field.TryClaimTie(out string tieMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(tieMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(tieMessage);
                        }
                        case "giveup":
                        {
                            int playerIndex = args.Length >= 2 && int.TryParse(args[1], out int parsedPlayer) ? parsedPlayer : 0;
                            if (!field.TryGiveUp(playerIndex, out string giveUpMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(giveUpMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(giveUpMessage);
                        }
                        case "end":
                        {
                            int playerIndex = args.Length >= 2 && int.TryParse(args[1], out int parsedPlayer) ? parsedPlayer : field.LocalPlayerIndex;
                            if (!field.TryRequestRoomExit(playerIndex, out string endMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(endMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(endMessage);
                        }
                        case "status":
                            return ChatCommandHandler.CommandResult.Info($"{field.DescribeStatus()} | inbox={_memoryGamePacketInbox.LastStatus} | session={_memoryGameOfficialSessionBridge.LastStatus}");
                        case "packet":
                        {
                            if (args.Length < 2 || !MemoryGameField.TryParsePacketType(args[1], out MemoryGamePacketType packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packet <open|ready|unready|start|flip|tie|giveup|end|mode> [...]");
                            }


                            int playerIndex = 0;

                            int cardIndex = -1;

                            bool readyState = true;



                            if ((packetType == MemoryGamePacketType.SetReady || packetType == MemoryGamePacketType.GiveUp)
                                && args.Length >= 3
                                && int.TryParse(args[2], out int parsedPacketPlayer))
                            {
                                playerIndex = parsedPacketPlayer;
                            }


                            if (packetType == MemoryGamePacketType.SetReady)
                            {
                                string readyArg = args.Length >= 4
                                    ? args[3]
                                    : args.Length >= 3 && !int.TryParse(args[2], out _)
                                        ? args[2]
                                        : "on";
                                readyState = !string.Equals(readyArg, "off", StringComparison.OrdinalIgnoreCase)
                                    && !string.Equals(readyArg, "unready", StringComparison.OrdinalIgnoreCase);
                            }


                            if (packetType == MemoryGamePacketType.RevealCard)
                            {
                                cardIndex = args.Length >= 3 && int.TryParse(args[2], out int parsedPacketCard) ? parsedPacketCard : -1;
                                playerIndex = args.Length >= 4 && int.TryParse(args[3], out int parsedRevealPlayer) ? parsedRevealPlayer : 0;
                            }


                            string playerOne = args.Length >= 3 && packetType == MemoryGamePacketType.OpenRoom ? args[2] : "Player";
                            string playerTwo = args.Length >= 4 && packetType == MemoryGamePacketType.OpenRoom ? args[3] : "Opponent";
                            int rows = args.Length >= 5 && packetType == MemoryGamePacketType.OpenRoom && int.TryParse(args[4], out int parsedRows) ? parsedRows : 4;
                            int columns = args.Length >= 6 && packetType == MemoryGamePacketType.OpenRoom && int.TryParse(args[5], out int parsedColumns) ? parsedColumns : 4;


                            if (!field.TryDispatchPacket(packetType, currTickCount, out string packetMessage, playerIndex, cardIndex, readyState, playerOne, playerTwo, rows, columns))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetMessage);
                            }


                            if (packetType == MemoryGamePacketType.OpenRoom || packetType == MemoryGamePacketType.SelectMatchCardsMode)
                            {
                                ShowMiniRoomWindow();
                            }


                            return ChatCommandHandler.CommandResult.Ok(packetMessage);
                        }
                        case "packetraw":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packetraw <hex bytes>");
                            }


                            if (!MemoryGameField.TryParseMiniRoomPacketHex(string.Join(' ', args, 1, args.Length - 1), out byte[] packetBytes, out string packetParseError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetParseError);
                            }


                            if (!field.TryDispatchMiniRoomPacket(packetBytes, currTickCount, out string packetMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetMessage);
                            }


                            ShowMiniRoomWindow();
                            return ChatCommandHandler.CommandResult.Ok(packetMessage);
                        }
                        case "packetclientraw":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packetclientraw <hex bytes>");
                            }


                            if (!TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawClientPacket))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packetclientraw <hex bytes>");
                            }

                            if (!MemoryGameOfficialSessionBridgeManager.TryDecodeClientOpcodePacket(rawClientPacket, out byte[] clientPayload, out string clientPacketError))
                            {
                                return ChatCommandHandler.CommandResult.Error(clientPacketError ?? "Usage: /memorygame packetclientraw <hex bytes>");
                            }

                            if (!field.TryDispatchOfficialClientPacket(clientPayload, currTickCount, out string clientPacketMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(clientPacketMessage);
                            }


                            ShowMiniRoomWindow();
                            return ChatCommandHandler.CommandResult.Ok(clientPacketMessage);
                        }
                        case "packetrecv":
                        {
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packetrecv <opcode> <hex bytes>");
                            }


                            if (!ushort.TryParse(args[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[1][2..] : args[1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort recvOpcode)
                                && !ushort.TryParse(args[1], out recvOpcode))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Memory Game opcode: {args[1]}");
                            }


                            if (!MemoryGameField.TryParseMiniRoomPacketHex(string.Join(' ', args, 2, args.Length - 2), out byte[] recvPayload, out string recvParseError))
                            {
                                return ChatCommandHandler.CommandResult.Error(recvParseError);
                            }


                            byte[] recvPacket = new byte[recvPayload.Length + sizeof(ushort)];
                            recvPacket[0] = (byte)(recvOpcode & 0xFF);
                            recvPacket[1] = (byte)((recvOpcode >> 8) & 0xFF);
                            Buffer.BlockCopy(recvPayload, 0, recvPacket, sizeof(ushort), recvPayload.Length);


                            if (!field.TryDispatchMiniRoomPacket(recvPacket, currTickCount, out string recvMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(recvMessage);
                            }


                            ShowMiniRoomWindow();
                            return ChatCommandHandler.CommandResult.Ok(recvMessage);
                        }
                        case "inbox":
                        {
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(_memoryGamePacketInbox.LastStatus);
                            }


                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = MemoryGamePacketInboxManager.DefaultPort;
                                if (args.Length >= 3 && !int.TryParse(args[2], out port))
                                {
                                    return ChatCommandHandler.CommandResult.Error($"Invalid Memory Game inbox port: {args[2]}");
                                }


                                _memoryGamePacketInbox.Start(port);

                                return ChatCommandHandler.CommandResult.Ok(_memoryGamePacketInbox.LastStatus);

                            }



                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _memoryGamePacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_memoryGamePacketInbox.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /memorygame inbox [status|start [port]|stop]");
                        }
                        case "session":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_memoryGameOfficialSessionBridge.LastStatus}");
                            }
                            if (string.Equals(args[1], "discover", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 3
                                    || !int.TryParse(args[2], out int discoverRemotePort)
                                    || discoverRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session discover <remotePort> [processName|pid] [localPort]");
                                }
                                string processSelector = args.Length >= 4 ? args[3] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 5)
                                {
                                    if (!int.TryParse(args[4], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session discover <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }
                                return ChatCommandHandler.CommandResult.Info(
                                    _memoryGameOfficialSessionBridge.DescribeDiscoveredSessions(discoverRemotePort, processSelector, localPortFilter));
                            }
                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 5
                                    || !int.TryParse(args[2], out int listenPort)
                                    || listenPort <= 0
                                    || !int.TryParse(args[4], out int remotePort)
                                    || remotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session start <listenPort> <serverHost> <serverPort>");
                                }


                                _memoryGameOfficialSessionBridge.Start(listenPort, args[3], remotePort);
                                return ChatCommandHandler.CommandResult.Ok(_memoryGameOfficialSessionBridge.LastStatus);
                            }
                            if (string.Equals(args[1], "startauto", StringComparison.OrdinalIgnoreCase))
                            {
                                if (args.Length < 4
                                    || !int.TryParse(args[2], out int autoListenPort)
                                    || autoListenPort <= 0
                                    || !int.TryParse(args[3], out int autoRemotePort)
                                    || autoRemotePort <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                }
                                string processSelector = args.Length >= 5 ? args[4] : null;
                                int? localPortFilter = null;
                                if (args.Length >= 6)
                                {
                                    if (!int.TryParse(args[5], out int parsedLocalPort) || parsedLocalPort <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                                    }

                                    localPortFilter = parsedLocalPort;
                                }
                                return _memoryGameOfficialSessionBridge.TryStartFromDiscovery(autoListenPort, autoRemotePort, processSelector, localPortFilter, out string startStatus)
                                    ? ChatCommandHandler.CommandResult.Ok(startStatus)
                                    : ChatCommandHandler.CommandResult.Error(startStatus);
                            }
                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _memoryGameOfficialSessionBridge.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_memoryGameOfficialSessionBridge.LastStatus);
                            }


                            return ChatCommandHandler.CommandResult.Error("Usage: /memorygame session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]");
                        case "remote":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame remote <ready|unready|start|flip|tie|giveup|end> [...]");
                            }


                            string remoteAction = args[1].ToLowerInvariant();
                            int cardIndex = args.Length >= 3 && int.TryParse(args[2], out int parsedCardIndex) ? parsedCardIndex : -1;
                            int delayMs = args.Length >= 4 && int.TryParse(args[3], out int parsedDelayMs) ? parsedDelayMs : 600;
                            if (!field.TryQueueRemoteAction(remoteAction, currTickCount, out string remoteMessage, cardIndex, delayMs))
                            {
                                return ChatCommandHandler.CommandResult.Error(remoteMessage);
                            }


                            return ChatCommandHandler.CommandResult.Ok(remoteMessage);
                        }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetclientraw|packetrecv|remote|inbox|session> [...]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(
                "socialroom",
                "Drive mini-room, personal-shop, entrusted-shop, and trading-room parity",
                "/socialroom <miniroom|personalshop|entrustedshop|tradingroom> [packet] <action> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom <miniroom|personalshop|entrustedshop|tradingroom> [packet] <action> [...]");
                    }


                    SocialRoomKind kind = args[0].ToLowerInvariant() switch
                    {
                        "miniroom" or "mini" => SocialRoomKind.MiniRoom,
                        "personalshop" or "pshop" or "shop" => SocialRoomKind.PersonalShop,
                        "entrustedshop" or "eshop" or "membershop" => SocialRoomKind.EntrustedShop,
                        "tradingroom" or "trade" => SocialRoomKind.TradingRoom,
                        _ => (SocialRoomKind)(-1)
                    };
                    if ((int)kind < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Room must be miniroom, personalshop, entrustedshop, or tradingroom.");
                    }


                    if (!TryGetSocialRoomRuntime(kind, out SocialRoomRuntime runtime))
                    {
                        return ChatCommandHandler.CommandResult.Error("The requested social-room runtime is not available.");
                    }


                    int actionIndex = args.Length >= 2 && string.Equals(args[1], "packet", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
                    if (args.Length <= actionIndex)
                    {
                        return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
                    }


                    string action = args[actionIndex].ToLowerInvariant();
                    if (string.Equals(action, "packetraw", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length <= actionIndex + 1)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /socialroom <miniroom|personalshop|entrustedshop|tradingroom> [packet] packetraw <hex bytes>");
                        }


                        if (!MemoryGameField.TryParseMiniRoomPacketHex(string.Join(' ', args, actionIndex + 1, args.Length - actionIndex - 1), out byte[] packetBytes, out string packetParseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetParseError);
                        }


                        if (!runtime.TryDispatchPacketBytes(packetBytes, currTickCount, out string packetMessage))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetMessage);
                        }


                        if (!TryShowSocialRoomWindow(kind, out string packetRoomRestriction))
                        {
                            return ChatCommandHandler.CommandResult.Error(packetRoomRestriction ?? "This social-room interaction is blocked in this map.");
                        }

                        return ChatCommandHandler.CommandResult.Ok(packetMessage);

                    }



                    if (string.Equals(action, "packetrecv", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length <= actionIndex + 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /socialroom <miniroom|personalshop|entrustedshop|tradingroom> [packet] packetrecv <opcode> <hex bytes>");
                        }


                        if (!ushort.TryParse(args[actionIndex + 1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[actionIndex + 1][2..] : args[actionIndex + 1], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort recvOpcode)
                            && !ushort.TryParse(args[actionIndex + 1], out recvOpcode))
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid social-room opcode: {args[actionIndex + 1]}");
                        }


                        if (!MemoryGameField.TryParseMiniRoomPacketHex(string.Join(' ', args, actionIndex + 2, args.Length - actionIndex - 2), out byte[] recvPayload, out string recvParseError))
                        {
                            return ChatCommandHandler.CommandResult.Error(recvParseError);
                        }


                        byte[] recvPacket = new byte[sizeof(ushort) + recvPayload.Length];

                        BitConverter.GetBytes(recvOpcode).CopyTo(recvPacket, 0);

                        recvPayload.CopyTo(recvPacket, sizeof(ushort));

                        if ((kind == SocialRoomKind.PersonalShop || kind == SocialRoomKind.EntrustedShop)
                            && (recvOpcode == SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeEnterFieldOpcode
                                || recvOpcode == SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeLeaveFieldOpcode
                                || recvOpcode == SocialRoomEmployeeOfficialSessionBridgeManager.EmployeeMiniRoomBalloonOpcode))
                        {
                            if (!TryDispatchPacketOwnedEmployeePoolOpcode(runtime, kind, recvOpcode, recvPayload, currTickCount, out string employeePoolMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(employeePoolMessage);
                            }

                            if (!TryShowSocialRoomWindow(kind, out string employeePoolRoomRestriction))
                            {
                                return ChatCommandHandler.CommandResult.Error(employeePoolRoomRestriction ?? "This social-room interaction is blocked in this map.");
                            }

                            return ChatCommandHandler.CommandResult.Ok(employeePoolMessage);
                        }

                        if (!runtime.TryDispatchPacketBytes(recvPacket, currTickCount, out string recvMessage))
                        {
                            return ChatCommandHandler.CommandResult.Error(recvMessage);
                        }


                        if (!TryShowSocialRoomWindow(kind, out string recvRoomRestriction))
                        {
                            return ChatCommandHandler.CommandResult.Error(recvRoomRestriction ?? "This social-room interaction is blocked in this map.");
                        }
                        return ChatCommandHandler.CommandResult.Ok(recvMessage);
                    }
                    bool Dispatch(SocialRoomPacketType packetType, out string packetMessage, int itemId = 0, int quantity = 1, int meso = 0, int itemIndex = -1, string actorName = null)
                    {
                        return runtime.TryDispatchPacket(packetType, out packetMessage, itemId, quantity, meso, itemIndex, actorName);
                    }


                    switch (kind)
                    {
                        case SocialRoomKind.MiniRoom:
                            switch (action)
                            {
                                case "open":
                                    if (!TryShowSocialRoomWindow(kind, out string miniRoomRestriction))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(miniRoomRestriction ?? "Mini-room interactions are blocked in this map.");
                                    }
                                    return ChatCommandHandler.CommandResult.Ok("Mini-room window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
                                case "ready":
                                    return Dispatch(SocialRoomPacketType.ToggleReady, out string readyMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(readyMessage)
                                        : ChatCommandHandler.CommandResult.Error(readyMessage);
                                case "start":
                                    return Dispatch(SocialRoomPacketType.StartSession, out string startMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(startMessage)
                                        : ChatCommandHandler.CommandResult.Error(startMessage);
                                case "mode":
                                    return Dispatch(SocialRoomPacketType.CycleMode, out string modeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(modeMessage)
                                        : ChatCommandHandler.CommandResult.Error(modeMessage);
                                case "wager":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int miniWager))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom miniroom [packet] wager <meso>");
                                    }


                                    return Dispatch(SocialRoomPacketType.SetWager, out string wagerMessage, meso: miniWager)
                                        ? ChatCommandHandler.CommandResult.Ok(wagerMessage)
                                        : ChatCommandHandler.CommandResult.Error(wagerMessage);
                                case "settle":
                                    string outcome = args.Length > actionIndex + 1 ? args[actionIndex + 1] : "owner";
                                    return Dispatch(SocialRoomPacketType.SettleWager, out string settleMessage, actorName: outcome)
                                        ? ChatCommandHandler.CommandResult.Ok(settleMessage)
                                        : ChatCommandHandler.CommandResult.Error(settleMessage);
                                case "visit":
                                    string miniRoomVisitor = args.Length > actionIndex + 1 ? args[actionIndex + 1] : null;
                                    return runtime.AddMiniRoomVisitor(miniRoomVisitor, out string miniRoomVisitMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(miniRoomVisitMessage)
                                        : ChatCommandHandler.CommandResult.Error(miniRoomVisitMessage);
                                case "leave":
                                    string leavingOccupant = args.Length > actionIndex + 1 ? args[actionIndex + 1] : null;
                                    return runtime.RemoveMiniRoomOccupant(leavingOccupant, out string miniRoomLeaveMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(miniRoomLeaveMessage)
                                        : ChatCommandHandler.CommandResult.Error(miniRoomLeaveMessage);
                                case "place":
                                case "move":
                                    if (args.Length <= actionIndex + 2
                                        || !int.TryParse(args[actionIndex + 1], out int omokX)
                                        || !int.TryParse(args[actionIndex + 2], out int omokY))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom miniroom [packet] place <x> <y>");
                                    }


                                    return runtime.TryPlaceOmokStone(omokX, omokY, out string omokMoveMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(omokMoveMessage)
                                        : ChatCommandHandler.CommandResult.Error(omokMoveMessage);
                                case "tie":
                                    return runtime.TryRequestMiniRoomTie(out string omokTieMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(omokTieMessage)
                                        : ChatCommandHandler.CommandResult.Error(omokTieMessage);
                                case "forfeit":
                                    string forfeitingSeat = args.Length > actionIndex + 1 ? args[actionIndex + 1] : "guest";
                                    return runtime.TryForfeitMiniRoom(forfeitingSeat, out string omokForfeitMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(omokForfeitMessage)
                                        : ChatCommandHandler.CommandResult.Error(omokForfeitMessage);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom miniroom [packet] <open|status|ready|start|mode|visit [name]|leave [name]|place <x> <y>|tie|forfeit [owner|guest]|wager <meso>|settle <owner|guest|draw>|packetraw <hex>|packetrecv <opcode> <hex>>");
                            }


                        case SocialRoomKind.PersonalShop:
                            switch (action)
                            {
                                case "open":
                                    if (!TryShowSocialRoomWindow(kind, out string personalShopRestriction))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(personalShopRestriction ?? "Shop-room interactions are blocked in this map.");
                                    }
                                    return ChatCommandHandler.CommandResult.Ok("Personal-shop window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
                                case "packetowner":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribePacketOwnerStatus());
                                case "visit":
                                    string visitor = args.Length > actionIndex + 1 ? args[actionIndex + 1] : null;
                                    return Dispatch(SocialRoomPacketType.AddVisitor, out string visitMessage, actorName: visitor)
                                        ? ChatCommandHandler.CommandResult.Ok(visitMessage)
                                        : ChatCommandHandler.CommandResult.Error(visitMessage);
                                case "blacklist":
                                    string blocked = args.Length > actionIndex + 1 ? args[actionIndex + 1] : null;
                                    return Dispatch(SocialRoomPacketType.ToggleBlacklist, out string blacklistMessage, actorName: blocked)
                                        ? ChatCommandHandler.CommandResult.Ok(blacklistMessage)
                                        : ChatCommandHandler.CommandResult.Error(blacklistMessage);
                                case "list":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int shopItemId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom personalshop [packet] list <itemId> [qty] [price]");
                                    }


                                    int shopQty = args.Length > actionIndex + 2 && int.TryParse(args[actionIndex + 2], out int parsedShopQty) ? parsedShopQty : 1;
                                    int shopPrice = args.Length > actionIndex + 3 && int.TryParse(args[actionIndex + 3], out int parsedShopPrice) ? parsedShopPrice : 0;
                                    return Dispatch(SocialRoomPacketType.ListItem, out string listMessage, itemId: shopItemId, quantity: shopQty, meso: shopPrice)
                                        ? ChatCommandHandler.CommandResult.Ok(listMessage)
                                        : ChatCommandHandler.CommandResult.Error(listMessage);
                                case "autolist":
                                    return Dispatch(SocialRoomPacketType.AutoListItem, out string autoListMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(autoListMessage)
                                        : ChatCommandHandler.CommandResult.Error(autoListMessage);
                                case "buy":
                                    int bundleIndex = args.Length > actionIndex + 1 && int.TryParse(args[actionIndex + 1], out int parsedBundleIndex) ? parsedBundleIndex : -1;
                                    string buyerName = args.Length > actionIndex + 2 ? args[actionIndex + 2] : null;
                                    return Dispatch(SocialRoomPacketType.BuyItem, out string buyMessage, itemIndex: bundleIndex, actorName: buyerName)
                                        ? ChatCommandHandler.CommandResult.Ok(buyMessage)
                                        : ChatCommandHandler.CommandResult.Error(buyMessage);
                                case "arrange":
                                    return Dispatch(SocialRoomPacketType.ArrangeItems, out string arrangeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(arrangeMessage)
                                        : ChatCommandHandler.CommandResult.Error(arrangeMessage);
                                case "claim":
                                    return Dispatch(SocialRoomPacketType.ClaimMesos, out string claimMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(claimMessage)
                                        : ChatCommandHandler.CommandResult.Error(claimMessage);
                                case "close":
                                    return Dispatch(SocialRoomPacketType.CloseRoom, out string closeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(closeMessage)
                                        : ChatCommandHandler.CommandResult.Error(closeMessage);
                                case "employee":
                                    return HandleSocialRoomEmployeeCommand(runtime, kind, args, actionIndex);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom personalshop [packet] <open|status|packetowner|visit [name]|blacklist [name]|list <itemId> [qty] [price]|autolist|buy [index] [buyer]|arrange|claim|close|employee <status|template <itemId|clear>|offset <x> <y>|world <x> <y>|facing <left|right|random>|packetraw <hex>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|reset>|packetraw <hex>|packetrecv <opcode> <hex>>");
                            }


                        case SocialRoomKind.EntrustedShop:
                            switch (action)
                            {
                                case "open":
                                    if (!TryShowSocialRoomWindow(kind, out string entrustedShopRestriction))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(entrustedShopRestriction ?? "Shop-room interactions are blocked in this map.");
                                    }
                                    return ChatCommandHandler.CommandResult.Ok("Entrusted-shop window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
                                case "packetowner":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribePacketOwnerStatus());
                                case "mode":
                                    return Dispatch(SocialRoomPacketType.ToggleLedgerMode, out string ledgerMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(ledgerMessage)
                                        : ChatCommandHandler.CommandResult.Error(ledgerMessage);
                                case "list":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int entrustedItemId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom entrustedshop [packet] list <itemId> [qty] [price]");
                                    }


                                    int entrustedQty = args.Length > actionIndex + 2 && int.TryParse(args[actionIndex + 2], out int parsedEntrustedQty) ? parsedEntrustedQty : 1;
                                    int entrustedPrice = args.Length > actionIndex + 3 && int.TryParse(args[actionIndex + 3], out int parsedEntrustedPrice) ? parsedEntrustedPrice : 0;
                                    return Dispatch(SocialRoomPacketType.ListItem, out string entrustedListMessage, itemId: entrustedItemId, quantity: entrustedQty, meso: entrustedPrice)
                                        ? ChatCommandHandler.CommandResult.Ok(entrustedListMessage)
                                        : ChatCommandHandler.CommandResult.Error(entrustedListMessage);
                                case "autolist":
                                    return Dispatch(SocialRoomPacketType.AutoListItem, out string entrustedAutoMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(entrustedAutoMessage)
                                        : ChatCommandHandler.CommandResult.Error(entrustedAutoMessage);
                                case "arrange":
                                    return Dispatch(SocialRoomPacketType.ArrangeItems, out string entrustedArrangeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(entrustedArrangeMessage)
                                        : ChatCommandHandler.CommandResult.Error(entrustedArrangeMessage);
                                case "claim":
                                    return Dispatch(SocialRoomPacketType.ClaimMesos, out string entrustedClaimMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(entrustedClaimMessage)
                                        : ChatCommandHandler.CommandResult.Error(entrustedClaimMessage);
                                case "permit":
                                    if (args.Length > actionIndex + 1 && string.Equals(args[actionIndex + 1], "expire", StringComparison.OrdinalIgnoreCase))
                                    {
                                        return runtime.ExpireEntrustedPermit(out string expirePermitMessage)
                                            ? ChatCommandHandler.CommandResult.Ok(expirePermitMessage)
                                            : ChatCommandHandler.CommandResult.Error(expirePermitMessage);
                                    }


                                    int permitMinutes = args.Length > actionIndex + 1 && int.TryParse(args[actionIndex + 1], out int parsedPermitMinutes)
                                        ? parsedPermitMinutes
                                        : 24 * 60;
                                    return runtime.TryRenewEntrustedPermit(permitMinutes, out string permitMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(permitMessage)
                                        : ChatCommandHandler.CommandResult.Error(permitMessage);
                                case "employee":
                                    return HandleSocialRoomEmployeeCommand(runtime, kind, args, actionIndex);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom entrustedshop [packet] <open|status|packetowner|mode|list <itemId> [qty] [price]|autolist|arrange|claim|permit [minutes|expire]|employee <status|template <itemId|clear>|offset <x> <y>|world <x> <y>|facing <left|right|random>|packetraw <hex>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|reset>|packetraw <hex>|packetrecv <opcode> <hex>>");
                            }


                        case SocialRoomKind.TradingRoom:
                            switch (action)
                            {
                                case "open":
                                    ShowSocialRoomWindow(kind);
                                    return ChatCommandHandler.CommandResult.Ok("Trading-room window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info($"{runtime.DescribeStatus()}{Environment.NewLine}{DescribeTradingRoomPacketInboxStatus()}");
                                case "packetowner":
                                    return ChatCommandHandler.CommandResult.Info($"{runtime.DescribePacketOwnerStatus()}{Environment.NewLine}{DescribeTradingRoomPacketInboxStatus()}");
                                case "inbox":
                                    return HandleTradingRoomInboxCommand(args, actionIndex);
                                case "offeritem":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int tradeItemId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] offeritem <itemId> [qty]");
                                    }


                                    int tradeQty = args.Length > actionIndex + 2 && int.TryParse(args[actionIndex + 2], out int parsedTradeQty) ? parsedTradeQty : 1;
                                    return Dispatch(SocialRoomPacketType.OfferTradeItem, out string tradeItemMessage, itemId: tradeItemId, quantity: tradeQty)
                                        ? ChatCommandHandler.CommandResult.Ok(tradeItemMessage)
                                        : ChatCommandHandler.CommandResult.Error(tradeItemMessage);
                                case "offermeso":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int tradeMeso))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] offermeso <amount>");
                                    }


                                    return Dispatch(SocialRoomPacketType.OfferTradeMeso, out string tradeMesoMessage, meso: tradeMeso)
                                        ? ChatCommandHandler.CommandResult.Ok(tradeMesoMessage)
                                        : ChatCommandHandler.CommandResult.Error(tradeMesoMessage);
                                case "lock":
                                    return Dispatch(SocialRoomPacketType.LockTrade, out string lockMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(lockMessage)
                                        : ChatCommandHandler.CommandResult.Error(lockMessage);
                                case "accept":
                                    return runtime.ToggleTradeAcceptance(out string acceptMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(acceptMessage)
                                        : ChatCommandHandler.CommandResult.Error(acceptMessage);
                                case "remoteofferitem":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int remoteTradeItemId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] remoteofferitem <itemId> [qty]");
                                    }


                                    int remoteTradeQty = args.Length > actionIndex + 2 && int.TryParse(args[actionIndex + 2], out int parsedRemoteTradeQty) ? parsedRemoteTradeQty : 1;
                                    return runtime.TryOfferRemoteTradeItem(remoteTradeItemId, remoteTradeQty, out string remoteTradeItemMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(remoteTradeItemMessage)
                                        : ChatCommandHandler.CommandResult.Error(remoteTradeItemMessage);
                                case "remoteoffermeso":
                                    if (args.Length <= actionIndex + 1 || !int.TryParse(args[actionIndex + 1], out int remoteTradeMeso))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] remoteoffermeso <amount>");
                                    }


                                    return runtime.TryOfferRemoteTradeMeso(remoteTradeMeso, out string remoteTradeMesoMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(remoteTradeMesoMessage)
                                        : ChatCommandHandler.CommandResult.Error(remoteTradeMesoMessage);
                                case "remotelock":
                                    return runtime.ToggleTradeLock(out string remoteLockMessage, remoteParty: true)
                                        ? ChatCommandHandler.CommandResult.Ok(remoteLockMessage)
                                        : ChatCommandHandler.CommandResult.Error(remoteLockMessage);
                                case "remoteaccept":
                                    return runtime.ToggleTradeAcceptance(out string remoteAcceptMessage, remoteParty: true)
                                        ? ChatCommandHandler.CommandResult.Ok(remoteAcceptMessage)
                                        : ChatCommandHandler.CommandResult.Error(remoteAcceptMessage);
                                case "remoteinventory":
                                    if (args.Length <= actionIndex + 1)
                                    {
                                        return ChatCommandHandler.CommandResult.Info(runtime.DescribeRemoteTradeInventory());
                                    }


                                    string remoteInventoryAction = args[actionIndex + 1].ToLowerInvariant();
                                    switch (remoteInventoryAction)
                                    {
                                        case "status":
                                            return ChatCommandHandler.CommandResult.Info(runtime.DescribeRemoteTradeInventory());
                                        case "additem":
                                            if (args.Length <= actionIndex + 2 || !int.TryParse(args[actionIndex + 2], out int seedRemoteItemId))
                                            {
                                                return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] remoteinventory additem <itemId> [qty]");
                                            }


                                            int seedRemoteQty = args.Length > actionIndex + 3 && int.TryParse(args[actionIndex + 3], out int parsedSeedRemoteQty) ? parsedSeedRemoteQty : 1;
                                            return runtime.TrySeedRemoteTradeInventoryItem(seedRemoteItemId, seedRemoteQty, out string seedRemoteItemMessage)
                                                ? ChatCommandHandler.CommandResult.Ok(seedRemoteItemMessage)
                                                : ChatCommandHandler.CommandResult.Error(seedRemoteItemMessage);
                                        case "addmeso":
                                            if (args.Length <= actionIndex + 2 || !int.TryParse(args[actionIndex + 2], out int seedRemoteMeso))
                                            {
                                                return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] remoteinventory addmeso <amount>");
                                            }


                                            return runtime.TrySeedRemoteTradeInventoryMeso(seedRemoteMeso, out string seedRemoteMesoMessage)
                                                ? ChatCommandHandler.CommandResult.Ok(seedRemoteMesoMessage)
                                                : ChatCommandHandler.CommandResult.Error(seedRemoteMesoMessage);
                                        case "clear":
                                            runtime.ClearRemoteTradeInventory();
                                            return ChatCommandHandler.CommandResult.Ok("Cleared the simulator remote trade inventory.");
                                        default:
                                            return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] remoteinventory <status|additem <itemId> [qty]|addmeso <amount>|clear>");
                                    }
                                case "complete":
                                    return Dispatch(SocialRoomPacketType.CompleteTrade, out string completeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(completeMessage)
                                        : ChatCommandHandler.CommandResult.Error(completeMessage);
                                case "reset":
                                    return Dispatch(SocialRoomPacketType.ResetTrade, out string resetMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(resetMessage)
                                        : ChatCommandHandler.CommandResult.Error(resetMessage);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] <open|status|packetowner|inbox [status|start [port]|stop]|offeritem <itemId> [qty]|offermeso <amount>|lock|accept|remoteofferitem <itemId> [qty]|remoteoffermeso <amount>|remotelock|remoteaccept|remoteinventory <status|additem <itemId> [qty]|addmeso <amount>|clear>|complete|reset|packetraw <hex>|packetrecv <opcode> <hex>>");
                            }

                    }



                    return ChatCommandHandler.CommandResult.Error("Unsupported social-room request.");

                });



            _chat.CommandHandler.RegisterCommand(
                "memo",
                "Drive simulator parcel receive/send/quick-send and package-claim flows",
                "/memo [status|open|compose|quick|tab <receive|send|quick>|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]|packet <status|clear|reset|deliver <sender>|<subject>|<body> [|read|keep|claimed|item:<id>:<qty>|meso:<amount>]>]",
                args =>
                {
                    MemoMailboxSnapshot mailboxSnapshot = _memoMailbox.GetSnapshot();
                    MemoMailboxDraftSnapshot draftSnapshot = _memoMailbox.GetDraftSnapshot();
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"Inbox: {mailboxSnapshot.Entries.Count} memo(s), {mailboxSnapshot.UnreadCount} unread, {mailboxSnapshot.ClaimableCount} claimable package(s). Draft to {draftSnapshot.Recipient}: '{draftSnapshot.Subject}'.");
                    }


                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                        case "inbox":
                            _memoMailbox.SetActiveTab(ParcelDialogTab.Receive);
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok("Opened the parcel receive tab.");
                        case "compose":
                            _memoMailbox.SetActiveTab(ParcelDialogTab.Send);
                            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok("Opened the parcel send tab. Use /memo draft ... to edit the current draft.");
                        case "quick":
                            _memoMailbox.SetActiveTab(ParcelDialogTab.QuickSend);
                            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok("Opened the parcel quick-send tab.");
                        case "tab":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memo tab <receive|send|quick>");
                            }

                            ParcelDialogTab requestedTab = args[1].ToLowerInvariant() switch
                            {
                                "receive" or "keep" => ParcelDialogTab.Receive,
                                "send" => ParcelDialogTab.Send,
                                "quick" or "quicksend" => ParcelDialogTab.QuickSend,
                                _ => (ParcelDialogTab)(-1)
                            };
                            if (!Enum.IsDefined(typeof(ParcelDialogTab), requestedTab))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memo tab <receive|send|quick>");
                            }

                            _memoMailbox.SetActiveTab(requestedTab);
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok($"Switched the parcel dialog to the {args[1].Trim()} tab.");
                        case "send":
                            if (_memoMailbox.TryDispatchActiveDraft(out string sendMessage))
                            {
                                uiWindowManager?.HideWindow(MapSimulatorWindowNames.MemoSend);
                                ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);
                                return ChatCommandHandler.CommandResult.Ok(sendMessage);
                            }


                            return ChatCommandHandler.CommandResult.Error(sendMessage);
                        case "claim":
                        {
                            int memoId = -1;
                            if (args.Length >= 2)
                            {
                                if (!int.TryParse(args[1], out memoId) || memoId <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo claim [memoId]");
                                }
                            }
                            else
                            {
                                memoId = mailboxSnapshot.Entries.FirstOrDefault(entry => entry.CanClaimAttachment)?.MemoId ?? -1;
                            }


                            if (memoId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("No claimable memo package is available.");
                            }


                            _activeMemoAttachmentId = memoId;
                            return _memoMailbox.TryClaimAttachment(memoId, out string claimMessage)
                                ? ChatCommandHandler.CommandResult.Ok(claimMessage)
                                : ChatCommandHandler.CommandResult.Error(claimMessage);
                        }
                        case "draft":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"Draft to {draftSnapshot.Recipient} / '{draftSnapshot.Subject}' / package {draftSnapshot.AttachmentSummary}. Use /memo draft <recipient|subject|body|item|meso|clearattachment|reset> ...");
                            }


                            string draftAction = args[1].ToLowerInvariant();
                            string payload = string.Join(" ", args.Skip(2));
                            switch (draftAction)
                            {
                                case "recipient":
                                case "to":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft recipient <name>");
                                    }


                                    _memoMailbox.SetDraftRecipient(payload);
                                    return ChatCommandHandler.CommandResult.Ok($"Draft recipient set to {payload.Trim()}.");
                                case "subject":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft subject <text>");
                                    }


                                    _memoMailbox.SetDraftSubject(payload);
                                    return ChatCommandHandler.CommandResult.Ok("Draft subject updated.");
                                case "body":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft body <text>");
                                    }


                                    _memoMailbox.SetDraftBody(payload);
                                    return ChatCommandHandler.CommandResult.Ok("Draft body updated.");
                                case "item":
                                {
                                    if (args.Length < 3 || !int.TryParse(args[2], out int itemId) || itemId <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft item <itemId> [quantity]");
                                    }


                                    int quantity = args.Length >= 4 && int.TryParse(args[3], out int parsedQuantity)
                                        ? parsedQuantity
                                        : 1;
                                    return _memoMailbox.SetDraftItemAttachment(itemId, quantity, out string itemMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(itemMessage)
                                        : ChatCommandHandler.CommandResult.Error(itemMessage);
                                }
                                case "meso":
                                    if (args.Length < 3 || !int.TryParse(args[2], out int meso))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft meso <amount>");
                                    }


                                    return _memoMailbox.SetDraftMesoAttachment(meso, out string mesoMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(mesoMessage)
                                        : ChatCommandHandler.CommandResult.Error(mesoMessage);
                                case "clearattachment":
                                    _memoMailbox.ClearDraftAttachment();
                                    return ChatCommandHandler.CommandResult.Ok("Draft attachment cleared.");
                                case "reset":
                                    _memoMailbox.ResetDraftState();
                                    return ChatCommandHandler.CommandResult.Ok("Draft reset.");
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo draft <recipient|subject|body|item|meso|clearattachment|reset> ...");
                            }
                        }
                        case "deliver":
                        {
                            string joined = string.Join(" ", args.Skip(1));
                            string[] segments = joined.Split('|');
                            if (segments.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memo deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]");
                            }


                            string sender = segments[0].Trim();
                            string subject = segments[1].Trim();
                            string body = segments[2].Trim();
                            int attachmentItemId = 0;
                            int attachmentQuantity = 0;
                            int attachmentMeso = 0;


                            if (segments.Length >= 4)
                            {
                                string attachmentSpec = segments[3].Trim();
                                if (attachmentSpec.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] itemParts = attachmentSpec.Split(':');
                                    if (itemParts.Length < 2
                                        || !int.TryParse(itemParts[1], out attachmentItemId)
                                        || attachmentItemId <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Item attachment format is item:<itemId>:<qty>.");
                                    }


                                    attachmentQuantity = itemParts.Length >= 3 && int.TryParse(itemParts[2], out int parsedQty)
                                        ? parsedQty
                                        : 1;
                                }
                                else if (attachmentSpec.StartsWith("meso:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] mesoParts = attachmentSpec.Split(':');
                                    if (mesoParts.Length < 2 || !int.TryParse(mesoParts[1], out attachmentMeso) || attachmentMeso <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Meso attachment format is meso:<amount>.");
                                    }
                                }
                                else
                                {
                                    return ChatCommandHandler.CommandResult.Error("Attachment format must be item:<itemId>:<qty> or meso:<amount>.");
                                }
                            }


                            _memoMailbox.DeliverMemo(sender, subject, body, DateTimeOffset.Now, false, false, attachmentItemId, attachmentQuantity, attachmentMeso);
                            return ChatCommandHandler.CommandResult.Ok($"Delivered memo '{subject}' from {sender}.");
                        }
                        case "packet":
                        {
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{GetPacketOwnedSocialUtilityDialogDispatcher().DescribeParcelStatus()} Parcel session: {mailboxSnapshot.Entries.Count} row(s), active tab {mailboxSnapshot.ActiveTab}, unread {mailboxSnapshot.UnreadCount}, claimable {mailboxSnapshot.ClaimableCount}.");
                            }

                            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                            {
                                _memoMailbox.ClearParcelSession();
                                return ChatCommandHandler.CommandResult.Ok("Cleared the packet-shaped parcel receive session.");
                            }

                            if (string.Equals(args[1], "reset", StringComparison.OrdinalIgnoreCase))
                            {
                                _memoMailbox.ResetToSeedParcelSession();
                                return ChatCommandHandler.CommandResult.Ok("Restored the seeded parcel receive session.");
                            }

                            if (string.Equals(args[1], "deliver", StringComparison.OrdinalIgnoreCase))
                            {
                                string joinedPacket = string.Join(" ", args.Skip(2));
                                string[] segments = joinedPacket.Split('|');
                                if (segments.Length < 3)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo packet deliver <sender>|<subject>|<body> [|read|keep|claimed|item:<id>:<qty>|meso:<amount>]");
                                }

                                bool isRead = false;
                                bool isKept = false;
                                bool isClaimed = false;
                                int attachmentItemId = 0;
                                int attachmentQuantity = 0;
                                int attachmentMeso = 0;
                                for (int i = 3; i < segments.Length; i++)
                                {
                                    string flag = segments[i].Trim();
                                    if (flag.Equals("read", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isRead = true;
                                        continue;
                                    }

                                    if (flag.Equals("keep", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isKept = true;
                                        continue;
                                    }

                                    if (flag.Equals("claimed", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isClaimed = true;
                                        continue;
                                    }

                                    if (flag.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string[] itemParts = flag.Split(':');
                                        if (itemParts.Length < 2
                                            || !int.TryParse(itemParts[1], out attachmentItemId)
                                            || attachmentItemId <= 0)
                                        {
                                            return ChatCommandHandler.CommandResult.Error("Item attachment format is item:<itemId>:<qty>.");
                                        }

                                        attachmentQuantity = itemParts.Length >= 3 && int.TryParse(itemParts[2], out int parsedQty)
                                            ? parsedQty
                                            : 1;
                                        continue;
                                    }

                                    if (flag.StartsWith("meso:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string[] mesoParts = flag.Split(':');
                                        if (mesoParts.Length < 2 || !int.TryParse(mesoParts[1], out attachmentMeso) || attachmentMeso <= 0)
                                        {
                                            return ChatCommandHandler.CommandResult.Error("Meso attachment format is meso:<amount>.");
                                        }

                                        continue;
                                    }

                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo packet deliver <sender>|<subject>|<body> [|read|keep|claimed|item:<id>:<qty>|meso:<amount>]");
                                }

                                return GetPacketOwnedSocialUtilityDialogDispatcher().TryDeliverParcel(
                                    segments[0].Trim(),
                                    segments[1].Trim(),
                                    segments[2].Trim(),
                                    isRead,
                                    isKept,
                                    isClaimed,
                                    attachmentItemId,
                                    attachmentQuantity,
                                    attachmentMeso,
                                    out string packetMessage)
                                    ? ChatCommandHandler.CommandResult.Ok(packetMessage)
                                    : ChatCommandHandler.CommandResult.Error(packetMessage);
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /memo packet <status|clear|reset|deliver <sender>|<subject>|<body> [|read|keep|claimed|item:<id>:<qty>|meso:<amount>]>");
                        }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /memo [status|open|compose|quick|tab <receive|send|quick>|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]|packet <status|clear|reset|deliver <sender>|<subject>|<body> [|read|keep|claimed|item:<id>:<qty>|meso:<amount>]>]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(
                "family",
                "Drive the family chart UI and packet-shaped family roster synchronization",
                "/family [open|tree|status|reset|select <memberId>|packet <clear|seed|name <familyName>|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_familyChartRuntime.DescribeStatus());
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "open":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyChart);
                            return ChatCommandHandler.CommandResult.Ok("Family chart opened.");
                        case "tree":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyChart);
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyTree);
                            return ChatCommandHandler.CommandResult.Ok("Family tree opened.");
                        case "reset":
                            return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.ResetToSeedFamily());
                        case "select":
                            if (args.Length < 2 || !int.TryParse(args[1], out int selectedMemberId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /family select <memberId>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.SelectMemberById(selectedMemberId));
                        case "packet":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /family packet <clear|seed|name <familyName>|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>");
                            }


                            switch (args[1].ToLowerInvariant())
                            {
                                case "clear":
                                    return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.ClearRosterFromPacket());
                                case "seed":
                                    return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.ResetToSeedFamily());
                                case "name":
                                    return ChatCommandHandler.CommandResult.Ok(
                                        _familyChartRuntime.SetFamilyNameFromPacket(
                                            args.Length >= 3
                                                ? string.Join(" ", args.Skip(2))
                                                : string.Empty));
                                case "remove":
                                    if (args.Length < 3 || !int.TryParse(args[2], out int removedMemberId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /family packet remove <memberId>");
                                    }


                                    return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.RemoveMemberFromPacket(removedMemberId));
                                case "upsert":
                                    if (args.Length < 9)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /family packet upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>");
                                    }


                                    if (!int.TryParse(args[2], out int memberId))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet member id must be an integer.");
                                    }


                                    int? parentId = args[3].Equals("root", StringComparison.OrdinalIgnoreCase)
                                        ? null
                                        : int.TryParse(args[3], out int parsedParentId)
                                            ? parsedParentId
                                            : null;
                                    if (!args[3].Equals("root", StringComparison.OrdinalIgnoreCase) && !parentId.HasValue)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet parent id must be an integer or `root`.");
                                    }


                                    if (!int.TryParse(args[4], out int level))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet level must be an integer.");
                                    }


                                    bool? isOnline = args[5].ToLowerInvariant() switch
                                    {
                                        "online" => true,
                                        "offline" => false,
                                        _ => null
                                    };
                                    if (!isOnline.HasValue)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet presence must be `online` or `offline`.");
                                    }


                                    if (!int.TryParse(args[6], out int currentReputation))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet current reputation must be an integer.");
                                    }


                                    if (!int.TryParse(args[7], out int todayReputation))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet today reputation must be an integer.");
                                    }


                                    string payload = string.Join(" ", args.Skip(8));
                                    string[] fields = payload.Split('|');
                                    if (fields.Length < 3)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Family packet upsert payload must be `<name>|<job>|<location>`.");
                                    }


                                    string memberName = fields[0].Trim();
                                    string jobName = fields[1].Trim();
                                    string locationSummary = string.Join("|", fields.Skip(2)).Trim();
                                    return ChatCommandHandler.CommandResult.Ok(
                                        _familyChartRuntime.UpsertMemberFromPacket(
                                            memberId,
                                            parentId,
                                            memberName,
                                            jobName,
                                            level,
                                            locationSummary,
                                            isOnline.Value,
                                            currentReputation,
                                            todayReputation));
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /family packet <clear|seed|name <familyName>|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>");
                            }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /family [open|tree|status|reset|select <memberId>|packet <clear|seed|name <familyName>|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(

                "messenger",

                "Drive Messenger state, invite, claim, and remote social lifecycle flows",

                "/messenger [open|status|invite [name]|claim|leave|state <max|min|min2|next|prev>|presence <name> <online|offline>|packet <seed|clear|remove <name>|upsert <name>|invite <name>|accept [name]|reject [name]|leave <name>|room <name> <message>|whisper <name> <message>|member <payloadhex=..|payloadb64=..>|<invite|accept|reject|leave|room|whisper|member> <payloadhex=..|payloadb64=..>>|packetraw <invite|accept|reject|leave|room|whisper|member> <hex>|remote <invite|accept|reject|leave|room|whisper> ...]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_messengerRuntime.DescribeStatus());
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "open":
                            ShowMessengerWindow();
                            return ChatCommandHandler.CommandResult.Ok("Messenger window opened.");
                        case "invite":
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.Messenger);
                            return ChatCommandHandler.CommandResult.Ok(
                                args.Length >= 2
                                    ? _messengerRuntime.InviteContact(string.Join(" ", args.Skip(1)))
                                    : _messengerRuntime.InviteNextContact());
                        case "claim":
                            return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SubmitClaim());
                        case "leave":
                            return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.LeaveMessenger());
                        case "state":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messenger state <max|min|min2|next|prev>");
                            }


                            return args[1].ToLowerInvariant() switch
                            {
                                "max" => ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SetWindowState(MessengerWindowState.Max)),
                                "min" => ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SetWindowState(MessengerWindowState.Min)),
                                "min2" => ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SetWindowState(MessengerWindowState.Min2)),
                                "next" => ChatCommandHandler.CommandResult.Ok(_messengerRuntime.CycleState(true)),
                                "prev" => ChatCommandHandler.CommandResult.Ok(_messengerRuntime.CycleState(false)),
                                _ => ChatCommandHandler.CommandResult.Error("Usage: /messenger state <max|min|min2|next|prev>")
                            };
                        case "presence":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messenger presence <name> <online|offline>");
                            }


                            bool? online = args[^1].ToLowerInvariant() switch
                            {
                                "online" => true,
                                "offline" => false,
                                _ => null
                            };
                            if (!online.HasValue)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messenger presence <name> <online|offline>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.SetPresence(string.Join(" ", args.Skip(1).Take(args.Length - 2)), online.Value));

                        case "packet":
                            return MessengerCommandRouter.HandlePacketCommand(args, _messengerRuntime, TryParseBinaryPayloadArgument);
                        case "packetraw":
                            return MessengerCommandRouter.HandlePacketRawCommand(args, _messengerRuntime, TryDecodeHexBytes);
                        case "remote":
                            return MessengerCommandRouter.HandleRemoteCommand(args, _messengerRuntime);

                        default:

                            return ChatCommandHandler.CommandResult.Error("/messenger [open|status|invite [name]|claim|leave|state <max|min|min2|next|prev>|presence <name> <online|offline>|packet <seed|clear|remove <name>|upsert <name>|invite <name>|accept [name]|reject [name]|leave <name>|room <name> <message>|whisper <name> <message>|member <payloadhex=..|payloadb64=..>|<invite|accept|reject|leave|room|whisper|member> <payloadhex=..|payloadb64=..>>|packetraw <invite|accept|reject|leave|room|whisper|member> <hex>|remote <invite|accept|reject|leave|room|whisper> ...]");
                    }

                });



            // /goto <x> <y> - Move camera to position
            _chat.CommandHandler.RegisterCommand(
                "goto",
                "Move camera to X,Y position",
                "/goto <x> <y>",
                args =>
                {
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /goto <x> <y>");
                    }


                    if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                    {
                        return ChatCommandHandler.CommandResult.Error("Invalid coordinates");
                    }


                    mapShiftX = x;
                    mapShiftY = y;
                    return ChatCommandHandler.CommandResult.Ok($"Moved to ({x}, {y})");
                });


            // /mob - Toggle mob movement
            _chat.CommandHandler.RegisterCommand(
                "mob",
                "Toggle mob movement on/off",
                "/mob",
                args =>
                {
                    _gameState.MobMovementEnabled = !_gameState.MobMovementEnabled;
                    return ChatCommandHandler.CommandResult.Ok($"Mob movement: {(_gameState.MobMovementEnabled ? "ON" : "OFF")}");
                });


            // /debug - Toggle debug mode
            _chat.CommandHandler.RegisterCommand(
                "debug",
                "Toggle debug overlay",
                "/debug",
                args =>
                {
                    _gameState.ShowDebugMode = !_gameState.ShowDebugMode;
                    return ChatCommandHandler.CommandResult.Ok($"Debug mode: {(_gameState.ShowDebugMode ? "ON" : "OFF")}");
                });


            // /hideui - Toggle UI visibility
            _chat.CommandHandler.RegisterCommand(
                "hideui",
                "Toggle UI visibility",
                "/hideui",
                args =>
                {
                    _gameState.HideUIMode = !_gameState.HideUIMode;
                    return ChatCommandHandler.CommandResult.Ok($"UI hidden: {(_gameState.HideUIMode ? "YES" : "NO")}");
                });


            // /clear - Clear chat messages
            _chat.CommandHandler.RegisterCommand(
                "clear",
                "Clear chat messages",
                "/clear",
                args =>
                {
                    _chat.ClearMessages();
                    return ChatCommandHandler.CommandResult.Ok("Chat cleared");
                });


            _chat.CommandHandler.RegisterCommand(
                "hpwarn",
                "Set the low-HP warning threshold percentage",
                "/hpwarn <percent>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"HP warning threshold: {_statusBarHpWarningThresholdPercent}%");
                    }


                    if (!TryUpdateLowResourceWarningThreshold(args[0], isHp: true, out string message))
                    {
                        return ChatCommandHandler.CommandResult.Error(message);
                    }


                    return ChatCommandHandler.CommandResult.Ok(message);

                });



            _chat.CommandHandler.RegisterCommand(
                "mpwarn",
                "Set the low-MP warning threshold percentage",
                "/mpwarn <percent>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"MP warning threshold: {_statusBarMpWarningThresholdPercent}%");
                    }


                    if (!TryUpdateLowResourceWarningThreshold(args[0], isHp: false, out string message))
                    {
                        return ChatCommandHandler.CommandResult.Error(message);
                    }


                    return ChatCommandHandler.CommandResult.Ok(message);

                });



            _chat.CommandHandler.RegisterCommand(
                "quickslotitem",
                "Assign or clear an inventory-backed quick-slot item",
                "/quickslotitem <slot 1-28> <itemId|clear>",
                args =>
                {
                    if (_playerManager?.Skills == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Player skills are not available");
                    }


                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /quickslotitem <slot 1-28> <itemId|clear>");
                    }


                    if (!int.TryParse(args[0], out int oneBasedSlot) || oneBasedSlot < 1 || oneBasedSlot > SkillManager.TOTAL_SLOT_COUNT)
                    {
                        return ChatCommandHandler.CommandResult.Error("Slot must be between 1 and 28");
                    }


                    int slotIndex = oneBasedSlot - 1;
                    if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Skills.ClearHotkey(slotIndex);
                        return ChatCommandHandler.CommandResult.Ok($"Cleared quick-slot {oneBasedSlot}.");
                    }


                    if (!int.TryParse(args[1], out int itemId) || itemId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid item ID: {args[1]}");
                    }


                    if (!_playerManager.Skills.TrySetItemHotkey(slotIndex, itemId))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Unable to assign item {itemId} to quick-slot {oneBasedSlot}. Only owned USE/CASH entries can be quick-slotted.");
                    }


                    int itemCount = _playerManager.Skills.GetHotkeyItemCount(slotIndex);
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Assigned item {itemId} to quick-slot {oneBasedSlot} (count {itemCount}).");
                });


            _chat.CommandHandler.RegisterCommand(
                "messagebox",
                "Inspect or drive the field message-box and chalkboard pool",
                "/messagebox [status|create [itemId] <text>|leave <id> [fade|immediate]|clear|fail|packet <325|326|327> [payloadhex=..|payloadb64=..]|packetraw <325|326|327> [hex]]",
                args =>
                {
                    _fieldMessageBoxRuntime.Initialize(GraphicsDevice);
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_fieldMessageBoxRuntime.DescribeStatus());
                    }


                    switch (args[0].ToLowerInvariant())
                    {
                        case "create":
                        case "show":
                        {
                            PlayerCharacter player = _playerManager?.Player;
                            if (player == null)
                            {
                                return ChatCommandHandler.CommandResult.Error("The local player must be loaded before creating a field message-box.");
                            }


                            int itemId = 5370000;
                            int textStartIndex = 1;
                            if (args.Length >= 2 && int.TryParse(args[1], out int parsedItemId) && parsedItemId > 0)
                            {
                                itemId = parsedItemId;
                                textStartIndex = 2;
                            }


                            if (args.Length <= textStartIndex)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messagebox create [itemId] <text>");
                            }


                            string text = string.Join(" ", args.Skip(textStartIndex));
                            string ownerName = _playerManager?.Player?.Build?.Name ?? "Player";
                            Point hostPosition = new((int)Math.Round(player.X), (int)Math.Round(player.Y));
                            return ChatCommandHandler.CommandResult.Ok(
                                _fieldMessageBoxRuntime.CreateLocalMessageBox(
                                    itemId,
                                    text,
                                    ownerName,
                                    hostPosition,
                                    currTickCount));
                        }
                        case "leave":
                        case "remove":
                            if (args.Length < 2 || !int.TryParse(args[1], out int leaveMessageBoxId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messagebox leave <id> [fade|immediate]");
                            }


                            bool immediate = args.Length >= 3 && string.Equals(args[2], "immediate", StringComparison.OrdinalIgnoreCase);

                            return ChatCommandHandler.CommandResult.Ok(

                                _fieldMessageBoxRuntime.RemoveMessageBox(leaveMessageBoxId, immediate, currTickCount));



                        case "clear":

                            _fieldMessageBoxRuntime.Clear();

                            return ChatCommandHandler.CommandResult.Ok(_fieldMessageBoxRuntime.DescribeStatus());



                        case "fail":

                            return ChatCommandHandler.CommandResult.Ok(_fieldMessageBoxRuntime.ApplyCreateFailed());



                        case "packet":
                            if (args.Length < 2 || !int.TryParse(args[1], out int packetType) || packetType < 325 || packetType > 327)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messagebox packet <325|326|327> [payloadhex=..|payloadb64=..]");
                            }


                            byte[] packetPayload = Array.Empty<byte>();
                            string packetPayloadError = null;
                            if (packetType != 325)
                            {
                                if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out packetPayload, out packetPayloadError))
                                {
                                    return ChatCommandHandler.CommandResult.Error(packetPayloadError ?? "Usage: /messagebox packet <325|326|327> [payloadhex=..|payloadb64=..]");
                                }
                            }


                            return TryApplyFieldMessageBoxPacket(packetType, packetPayload, out string packetMessage)

                                ? ChatCommandHandler.CommandResult.Ok(packetMessage)

                                : ChatCommandHandler.CommandResult.Error(packetMessage);



                        case "packetraw":
                            if (args.Length < 2 || !int.TryParse(args[1], out int rawPacketType) || rawPacketType < 325 || rawPacketType > 327)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messagebox packetraw <325|326|327> [hex]");
                            }


                            byte[] rawPayload = Array.Empty<byte>();
                            if (rawPacketType != 325)
                            {
                                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out rawPayload))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /messagebox packetraw <325|326|327> [hex]");
                                }
                            }


                            return TryApplyFieldMessageBoxPacket(rawPacketType, rawPayload, out string rawPacketMessage)

                                ? ChatCommandHandler.CommandResult.Ok(rawPacketMessage)

                                : ChatCommandHandler.CommandResult.Error(rawPacketMessage);



                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /messagebox [status|create [itemId] <text>|leave <id> [fade|immediate]|clear|fail|packet <325|326|327> [payloadhex=..|payloadb64=..]|packetraw <325|326|327> [hex]]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(
                "localoverlay",
                "Inspect or drive packet-authored local overlays, damage-meter timing, and field-hazard notices",
                "/localoverlay [status|clear [fade|balloon|damagemeter|hazard|all]|fade <fadeInMs> <holdMs> <fadeOutMs> [alpha]|balloon avatar <width> <lifetimeSec> <text>|balloon world <x> <y> <width> <lifetimeSec> <text>|damagemeter <seconds>|damagemeterclear|hazard <damage> [force] [buffskill] [message]|hazardclear|packet <fade|balloon|damagemeter|hpdec> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon|damagemeter|hpdec> <hex>]",
                HandlePacketOwnedLocalOverlayCommand);
            _chat.CommandHandler.RegisterCommand(
                "localoverlaypacket",
                "Inspect or inject packet-owned field fade and balloon payloads directly",
                "/localoverlaypacket [status|packet <fade|balloon> [payloadhex=..|payloadb64=..]|packetraw <fade|balloon> <hex>]",
                HandlePacketOwnedLocalOverlayPacketAliasCommand);
            _chat.CommandHandler.RegisterCommand(
                "combopacket",
                "Inspect or drive packet-owned combo counter and combo-command HUD parity",
                "/combopacket [status|clear|set <count>|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>|inbox [status|start [port]|stop|packet <inccombo> [payloadhex=..|payloadb64=..]|packetraw <inccombo> <hex>]]",
                HandlePacketOwnedComboCommand);


            _chat.CommandHandler.RegisterCommand(
                "localutility",
                "Inspect or drive packet-authored local utility and event dispatch handlers",
            "/localutility [status|inbox [status|start [port]|stop|packet <sitresult|emotion|randomemotion|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|passivemove|hpdec|skillcooltime|193|231|232|242|243|246|247|250|251|252|258|262|263|264|265|266|267|268|269|270|273|274|275|276|1011|1012|1013|1014|classcompetition|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]|outbox [status|start [port]|stop]|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]|directionmode <on|off|1|0> [delayMs]|standalone <on|off|1|0>|openui <uiType> [defaultTab]|openuiwithoption <uiType> <option>|commodity <serialNumber>|notice <text>|chat [channel] <text>|buffzone [text]|eventsound <image/path or path>|minigamesound <image/path or path>|questguide <questId> <mobId:mapId[,mapId...]>...|questguide clear|delivery <questId> <itemId> [blockedQuestIdsCsv]|classcompetition|skillguide|antimacro [status|launch <normal|admin> [first|retry]|notice <noticeType> [antiMacroType]|result <mode> [antiMacroType] [userName]|clear]|apsp [status|seed [characterId]|receive <token>|send <token>|context <receiveToken> [sendToken]|<contextToken> <11|12|13>|text]|follow <status|request <driverId|name> [auto|manual] [keyinput]|withdraw|release|ask <requesterId|name>|accept|decline|attach <driverId|name>|detach [transferX transferY]|passengerdetach [requesterId|name] [transferX transferY]>|followfail [reasonCode [driverId]|text]|packet <sitresult|emotion|randomemotion|questresult|openui|openuiwithoption|commodity|fade|balloon|damagemeter|passivemove|hpdec|notice|chat|buffzone|eventsound|minigamesound|questguide|delivery|classcompetition|skillguide|antimacro|apspevent|directionmode|standalone|follow|followfail|193|231|232|242|243|246|247|250|251|252|258|262|263|264|265|266|267|268|269|270|273|274|275|276|1011|1012|1013|1014> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|packetclientraw <hex>]",
                HandlePacketOwnedUtilityCommand);
            _chat.CommandHandler.RegisterCommand(
                "expedition",
                "Inspect or drive the dedicated expedition intermediary owner discovered in the client",
                        "/expedition [status|open|search|clear|start|register|get|modified|invite|response|notice|master|removed|payload|packetraw|inbox|bridge] ...",
                HandleExpeditionIntermediaryCommand);

            _chat.CommandHandler.RegisterCommand(
                "fieldfeedback",
                "Inspect or drive packet-authored field chat, field effects, warning dialogs, obstacle toggles, and boss-feedback surfaces",
                        "/fieldfeedback [status|clear|group <family> <sender> <text>|whisperin <sender> <channel> <text>|whisperresult <target> <ok|fail>|whisperavailability <target> <0|1>|whisperfind <find|findreply> <target> <result> <value>|couplechat <sender> <text>|couplenotice [text]|warn <text>|obstacle <tag> <state>|obstaclereset|bosshp <mobId> <currentHp> <maxHp> [color] [phase]|tremble <force> <durationMs>|fieldsound <descriptor>|fieldbgm <descriptor>|jukebox <itemId> <owner>|transferfieldignored <reason>|transferchannelignored <reason>|summonunavailable [0|1]|destroyclock|zakumtimer <mode> <value>|hontailtimer <mode> <value>|chaoszakumtimer <mode> <value>|hontaletimer <mode> <value>|fadeoutforce [key]|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]",
                HandlePacketOwnedFieldFeedbackCommand);

            _chat.CommandHandler.RegisterCommand(
                "fieldutility",
                "Inspect or drive packet-authored field weather, admin-result, quiz, stalk, quickslot-init, and foothold-info handlers",
                "/fieldutility [status|clear|weather <itemId|clear> [message...]|quiz <question|answer|clear> <category> <problemId>|stalk <add <characterId> <name> <x> <y>|remove <characterId>>|quickslot <default|k1 k2 k3 k4 k5 k6 k7 k8>|footholdrequest|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]",
                HandlePacketOwnedFieldUtilityCommand);


            _chat.CommandHandler.RegisterCommand(
                "localutilitypacket",
                "Inspect or inject packet-owned local utility and event dispatch payloads through the loopback inbox",
            "/localutilitypacket [status|start [port]|stop|packet <sitresult|questresult|openui|openuiwithoption|commodity|notice|chat|buffzone|eventsound|minigamesound|skillguide|antimacro|apspevent|follow|followfail|directionmode|standalone|damagemeter|passivemove|hpdec|skillcooltime|193|231|242|243|246|247|250|251|252|262|263|264|265|266|267|268|269|270|273|274|275|276|1011|1012|1013|1014|classcompetition|questguide|deliveryquest> [payloadhex=..|payloadb64=..]|packetraw <type> [hex]|packetclientraw <hex>]",
                HandlePacketOwnedUtilityCommand);
            _chat.CommandHandler.RegisterCommand(
                "npcutility",
                "Inspect or drive packet-owned NPC shop, store-bank, and battle-record owners",
                "/npcutility [status|packet <364|365|369|370|420|421|422|423> [payloadhex=..|payloadb64=..]|packetraw <364|365|369|370|420|421|422|423> <hex>|shop [status|show|buy <itemId> [quantity]|sell <itemId> [quantity]|recharge <itemId> [targetQuantity]|close]|storebank [status|show|getall|close]|battlerecord [status|show|page <summary|dot|packets>|close]]",
                HandlePacketOwnedNpcUtilityCommand);


            _chat.CommandHandler.RegisterCommand(

                "mapletv",
                "Inspect or drive the MapleTV send board and broadcast lifecycle",
                "/mapletv [open|status|sample|set|clear|toggleto|sender|receiver|item|line|wait|result|packet|packetraw] [...]",
                args =>
                {
                    _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build);
                    if (args.Length == 0)
                    {
                        ShowMapleTvWindow();
                        return ChatCommandHandler.CommandResult.Info(_mapleTvRuntime.DescribeStatus(currTickCount));
                    }


                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                            ShowMapleTvWindow();
                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.DescribeStatus(currTickCount));


                        case "status":

                            return ChatCommandHandler.CommandResult.Info(_mapleTvRuntime.DescribeStatus(currTickCount));



                        case "sample":
                            ShowMapleTvWindow();
                            return ChatCommandHandler.CommandResult.Ok(
                                _mapleTvRuntime.LoadSample(
                                    _playerManager?.Player?.Build,
                                    GetCurrentMapTransferDisplayName()));


                        case "set":
                        {
                            string publishMessage = PublishMapleTvDraft();
                            return publishMessage.StartsWith("MapleTV message set", StringComparison.Ordinal)
                                ? ChatCommandHandler.CommandResult.Ok(publishMessage)
                                : ChatCommandHandler.CommandResult.Error(publishMessage);
                        }


                        case "clear":

                            return ChatCommandHandler.CommandResult.Ok(ClearMapleTvMessage());



                        case "toggleto":

                        case "to":

                            return ChatCommandHandler.CommandResult.Ok(ToggleMapleTvReceiverMode());



                        case "sender":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv sender <name>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetSender(string.Join(" ", args.Skip(1))));



                        case "receiver":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv receiver <name|self|clear>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetReceiver(string.Join(" ", args.Skip(1))));



                        case "item":
                            if (args.Length < 2 || !int.TryParse(args[1], out int itemId) || itemId < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv item <itemId>");
                            }


                                return ChatCommandHandler.CommandResult.Ok(
                                    _mapleTvRuntime.SetItem(
                                        itemId,
                                        itemId > 0 ? ResolvePickupItemName(itemId) : "Maple TV",
                                        itemId > 0 && InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string itemDescription) ? itemDescription : null));


                        case "line":
                            if (args.Length < 3 || !int.TryParse(args[1], out int lineNumber))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv line <1-5> <text>");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetDraftLine(lineNumber, string.Join(" ", args.Skip(2))));



                        case "wait":
                            if (args.Length < 2 || !int.TryParse(args[1], out int durationMs))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv wait <durationMs>");
                            }


                            string durationMessage = _mapleTvRuntime.SetDuration(durationMs);
                            return durationMs >= 1000 && durationMs <= 60000
                                ? ChatCommandHandler.CommandResult.Ok(durationMessage)
                                : ChatCommandHandler.CommandResult.Error(durationMessage);


                        case "result":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv result <sent|wrongname|queue|fail>");
                            }


                            MapleTvSendResultKind? resultKind = args[1].ToLowerInvariant() switch
                            {
                                "sent" => MapleTvSendResultKind.Sent,
                                "success" => MapleTvSendResultKind.Sent,
                                "wrongname" => MapleTvSendResultKind.WrongUserName,
                                "wrong-user-name" => MapleTvSendResultKind.WrongUserName,
                                "offline" => MapleTvSendResultKind.WrongUserName,
                                "queue" => MapleTvSendResultKind.QueueTooLong,
                                "queue-too-long" => MapleTvSendResultKind.QueueTooLong,
                                "busy" => MapleTvSendResultKind.QueueTooLong,
                                "fail" => MapleTvSendResultKind.Failed,
                                "failed" => MapleTvSendResultKind.Failed,
                                _ => null
                            };


                            if (resultKind == null)
                            {
                                return ChatCommandHandler.CommandResult.Error("Result must be one of: sent, wrongname, queue, fail");
                            }


                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.OnSendMessageResult(resultKind.Value));



                        case "packet":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packet <set|clear|result> [payloadhex=..|payloadb64=..]");
                            }


                            switch (args[1].ToLowerInvariant())
                            {
                                case "set":
                                    byte[] setPayload = null;
                                    string setPayloadError = null;
                                    if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out setPayload, out setPayloadError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(setPayloadError ?? "Usage: /mapletv packet set <payloadhex=..|payloadb64=..>");
                                    }


                                    if (!TryApplyMapleTvSetMessagePacket(setPayload, out string setPacketMessage))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(setPacketMessage);
                                    }


                                    ShowMapleTvWindow();

                                    return ChatCommandHandler.CommandResult.Ok(setPacketMessage);



                                case "clear":
                                    return TryApplyMapleTvPacket(MapleTvRuntime.PacketTypeClearMessage, Array.Empty<byte>(), out string clearPacketMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(clearPacketMessage)
                                        : ChatCommandHandler.CommandResult.Error(clearPacketMessage);


                                case "result":
                                    byte[] resultPayload = null;
                                    string resultPayloadError = null;
                                    if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out resultPayload, out resultPayloadError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(resultPayloadError ?? "Usage: /mapletv packet result <payloadhex=..|payloadb64=..>");
                                    }


                                    return TryApplyMapleTvPacket(MapleTvRuntime.PacketTypeSendMessageResult, resultPayload, out string resultPacketMessage)

                                        ? ChatCommandHandler.CommandResult.Ok(resultPacketMessage)

                                        : ChatCommandHandler.CommandResult.Error(resultPacketMessage);



                                default:

                                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packet <set|clear|result> [payloadhex=..|payloadb64=..]");

                            }



                        case "packetraw":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packetraw <set|clear|result> [hex bytes]");
                            }


                            switch (args[1].ToLowerInvariant())
                            {
                                case "set":
                                    if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] rawSetPayload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packetraw set <hex bytes>");
                                    }


                                    if (!TryApplyMapleTvSetMessagePacket(rawSetPayload, out string rawSetMessage))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(rawSetMessage);
                                    }


                                    ShowMapleTvWindow();

                                    return ChatCommandHandler.CommandResult.Ok(rawSetMessage);



                                case "clear":
                                    return TryApplyMapleTvPacket(MapleTvRuntime.PacketTypeClearMessage, Array.Empty<byte>(), out string rawClearMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(rawClearMessage)
                                        : ChatCommandHandler.CommandResult.Error(rawClearMessage);


                                case "result":
                                    if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] rawResultPayload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packetraw result <hex bytes>");
                                    }


                                    return TryApplyMapleTvPacket(MapleTvRuntime.PacketTypeSendMessageResult, rawResultPayload, out string rawResultMessage)

                                        ? ChatCommandHandler.CommandResult.Ok(rawResultMessage)

                                        : ChatCommandHandler.CommandResult.Error(rawResultMessage);



                                default:

                                    return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packetraw <set|clear|result> [hex bytes]");

                            }



                        default:
                            return ChatCommandHandler.CommandResult.Error(
                                "Usage: /mapletv [open|status|sample|set|clear|toggleto|sender|receiver|item|line|wait|result|packet|packetraw] [...]");
                    }
                });


            _chat.CommandHandler.RegisterCommand(
                "chair",
                "Activate or clear an owned portable chair",
                "/chair <itemId|clear|pair <characterId|clear>>",
                args =>
                {
                    if (_playerManager?.Player == null || _playerManager.Loader == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Player runtime is not available");
                    }


                    if (args.Length == 0)
                    {
                        PortableChair activeChair = _playerManager.Player.Build?.ActivePortableChair;
                        int? preferredPairCharacterId = _remoteUserPool?.LocalPortableChairPreferredPairCharacterId;
                        string preferredPairText = preferredPairCharacterId.HasValue
                            ? $" Preferred pair: {preferredPairCharacterId.Value}."
                            : string.Empty;
                        return activeChair != null
                            ? ChatCommandHandler.CommandResult.Info($"Active chair: {activeChair.Name} ({activeChair.ItemId}).{preferredPairText}")
                            : ChatCommandHandler.CommandResult.Info($"No portable chair is active.{preferredPairText}");
                    }


                    if (string.Equals(args[0], "pair", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_remoteUserPool == null || args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /chair pair <characterId|clear>");
                        }

                        if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                        {
                            _remoteUserPool.ClearLocalPortableChairPairPreference();
                            return ChatCommandHandler.CommandResult.Ok("Cleared the preferred couple-chair partner.");
                        }

                        if (!int.TryParse(args[1], out int pairCharacterId) || pairCharacterId <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid pair character ID: {args[1]}");
                        }

                        return _remoteUserPool.TrySetLocalPortableChairPairPreference(_playerManager.Player, pairCharacterId, out string pairMessage)
                            ? ChatCommandHandler.CommandResult.Ok(pairMessage)
                            : ChatCommandHandler.CommandResult.Error(pairMessage);
                    }


                    if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Player.ClearPortableChair();
                        _remoteUserPool?.ClearLocalPortableChairPairPreference();
                        return ChatCommandHandler.CommandResult.Ok("Portable chair cleared.");
                    }


                    if (!int.TryParse(args[0], out int itemId) || itemId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid chair item ID: {args[0]}");
                    }


                    return TryTogglePortableChair(itemId, out string chairMessage)
                        ? ChatCommandHandler.CommandResult.Ok(chairMessage)
                        : ChatCommandHandler.CommandResult.Error(chairMessage);
                });


            _chat.CommandHandler.RegisterCommand(
                "petevent",
                "Trigger a WZ-backed pet auto-speech event",
                "/petevent <rest|levelup|prelevelup|hpalert|nohppotion|nomppotion> [slot 1-3]",
                args =>
                {
                    string petRestrictionMessage = GetPetFieldRestrictionMessage();
                    if (!string.IsNullOrWhiteSpace(petRestrictionMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(petRestrictionMessage);
                    }
                    if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No active pets are available");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            "Usage: /petevent <rest|levelup|prelevelup|hpalert|nohppotion|nomppotion> [slot 1-3]");
                    }


                    if (!TryParsePetSpeechEvent(args[0], out PetAutoSpeechEvent eventType, out string eventName))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Unknown pet event '{args[0]}'. Expected rest, levelup, prelevelup, hpalert, nohppotion, or nomppotion.");
                    }


                    int? petSlotIndex = null;
                    if (args.Length >= 2)
                    {
                        if (!TryParsePetSlot(args[1], out int parsedSlotIndex, out string slotError))
                        {
                            return ChatCommandHandler.CommandResult.Error(slotError);
                        }


                        if (_playerManager.Pets.GetPetAt(parsedSlotIndex) == null)
                        {
                            return ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {parsedSlotIndex + 1}");
                        }


                        petSlotIndex = parsedSlotIndex;

                    }



                    if (!_playerManager.Pets.TryTriggerSpeechEvent(eventType, currTickCount, petSlotIndex))
                    {
                        string slotLabel = petSlotIndex.HasValue
                            ? $"pet {petSlotIndex.Value + 1}"
                            : "the active pet roster";
                        return ChatCommandHandler.CommandResult.Error(
                            $"No loaded speech lines are available for '{eventName}' on {slotLabel}.");
                    }


                    if (petSlotIndex.HasValue)
                    {
                        PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex.Value);
                        return ChatCommandHandler.CommandResult.Ok(
                            $"Triggered {eventName} speech for pet {petSlotIndex.Value + 1} ({pet?.Name ?? "Unknown"}).");
                    }


                    return ChatCommandHandler.CommandResult.Ok(

                        $"Triggered {eventName} speech for the first eligible active pet.");

                });



            _chat.CommandHandler.RegisterCommand(
                "petautohp",
                "Inspect or configure per-pet auto-HP settings used by field hazards",
                "/petautohp [status|slot 1-3 [on|off] [itemId [use|cash]|clear]]",
                args =>
                {
                    string petRestrictionMessage = GetPetFieldRestrictionMessage();
                    if (!string.IsNullOrWhiteSpace(petRestrictionMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(petRestrictionMessage);
                    }
                    if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No active pets are available");
                    }


                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribePetAutoConsumeHpSettings());
                    }


                    if (!TryResolvePetCommandSlot(args, 0, out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }


                    PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    if (pet == null)
                    {
                        return ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {petSlotIndex + 1}");
                    }


                    bool enabled = pet.AutoConsumeHpEnabled;
                    int nextArgumentIndex = 1;
                    if (args.Length >= 2 &&
                        (string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase)))
                    {
                        enabled = string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase);
                        _playerManager.Pets.TrySetAutoConsumeHpEnabled(petSlotIndex, enabled);
                        nextArgumentIndex = 2;
                    }

                    if (nextArgumentIndex >= args.Length)
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribePetAutoConsumeHpSetting(petSlotIndex, pet));
                    }

                    if (string.Equals(args[nextArgumentIndex], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Pets.TrySetAutoConsumeHpItem(petSlotIndex, 0, InventoryType.NONE);
                        return ChatCommandHandler.CommandResult.Ok(
                            $"Cleared auto-HP potion preference for pet {petSlotIndex + 1} ({pet.Name ?? "Unknown"}). {DescribePetAutoConsumeHpSetting(petSlotIndex, pet)}");
                    }

                    if (!int.TryParse(args[nextArgumentIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)
                        || itemId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /petautohp [status|slot 1-3 [on|off] [itemId [use|cash]|clear]]");
                    }

                    InventoryType inventoryType = InventoryType.NONE;
                    if (args.Length > nextArgumentIndex + 1
                        && !TryParsePetAutoConsumeInventoryType(args[nextArgumentIndex + 1], out inventoryType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Auto-HP inventory type must be 'use' or 'cash'.");
                    }

                    if (inventoryType == InventoryType.NONE)
                    {
                        inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
                    }

                    if (inventoryType != InventoryType.USE && inventoryType != InventoryType.CASH)
                    {
                        return ChatCommandHandler.CommandResult.Error("Auto-HP potions must come from the USE or CASH inventory.");
                    }

                    _playerManager.Pets.TrySetAutoConsumeHpEnabled(petSlotIndex, enabled: true);
                    _playerManager.Pets.TrySetAutoConsumeHpItem(petSlotIndex, itemId, inventoryType);
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Configured auto-HP potion for pet {petSlotIndex + 1} ({pet.Name ?? "Unknown"}). {DescribePetAutoConsumeHpSetting(petSlotIndex, pet)}");
                });



            _chat.CommandHandler.RegisterCommand(
                "petlevel",
                "Inspect or set the simulated pet command level for WZ command gating",
                "/petlevel [slot 1-3] [level 1-30]",
                args =>
                {
                    string petRestrictionMessage = GetPetFieldRestrictionMessage();
                    if (!string.IsNullOrWhiteSpace(petRestrictionMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(petRestrictionMessage);
                    }
                    if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No active pets are available");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribePetCommandLevels());
                    }


                    if (!TryParsePetSlot(args[0], out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }


                    if (args.Length == 1)
                    {
                        PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                        return pet == null
                            ? ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {petSlotIndex + 1}")
                            : ChatCommandHandler.CommandResult.Info($"Pet {petSlotIndex + 1} ({pet.Name}) command level: {pet.CommandLevel}");
                    }


                    if (!int.TryParse(args[1], out int level) || level < 1 || level > 30)
                    {
                        return ChatCommandHandler.CommandResult.Error("Level must be between 1 and 30");
                    }


                    if (!_playerManager.Pets.TrySetCommandLevel(petSlotIndex, level))
                    {
                        return ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {petSlotIndex + 1}");
                    }


                    PetRuntime updatedPet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    string petName = updatedPet != null
                        ? (!string.IsNullOrWhiteSpace(updatedPet.Name) ? updatedPet.Name : updatedPet.ItemId.ToString())
                        : "Unknown";
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Pet {petSlotIndex + 1} ({petName}) command level set to {level}.");
                });


            _chat.CommandHandler.RegisterCommand(
                "petslang",
                "Trigger the WZ-backed pet slang feedback line for an active pet",
                "/petslang [slot 1-3]",
                args =>
                {
                    string petRestrictionMessage = GetPetFieldRestrictionMessage();
                    if (!string.IsNullOrWhiteSpace(petRestrictionMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(petRestrictionMessage);
                    }
                    if (!TryResolvePetCommandSlot(args, 0, out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }


                    if (!_playerManager.Pets.TryTriggerSlangFeedback(petSlotIndex, currTickCount))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Pet {petSlotIndex + 1} has no slang feedback loaded.");
                    }


                    PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);

                    return ChatCommandHandler.CommandResult.Ok($"Triggered slang feedback for pet {petSlotIndex + 1} ({pet?.Name ?? "Unknown"}).");

                });



            _chat.CommandHandler.RegisterCommand(
                "petfeed",
                "Trigger a WZ-backed pet feeding feedback line",
                "/petfeed <variant 1-4> <success|fail> [slot 1-3]",
                args =>
                {
                    string petRestrictionMessage = GetPetFieldRestrictionMessage();
                    if (!string.IsNullOrWhiteSpace(petRestrictionMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error(petRestrictionMessage);
                    }

                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /petfeed <variant 1-4> <success|fail> [slot 1-3]");
                    }


                    if (!int.TryParse(args[0], out int variant) || variant < 1 || variant > 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Variant must be between 1 and 4");
                    }


                    bool? success = args[1].ToLowerInvariant() switch
                    {
                        "success" => true,
                        "fail" => false,
                        "failure" => false,
                        _ => null
                    };
                    if (success == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Result must be 'success' or 'fail'");
                    }


                    if (!TryResolvePetCommandSlot(args, 2, out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }


                    if (!_playerManager.Pets.TryTriggerFoodFeedback(petSlotIndex, variant, success.Value, currTickCount))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Pet {petSlotIndex + 1} has no loaded food feedback for variant {variant}.");
                    }


                    PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Triggered food feedback {variant} ({(success.Value ? "success" : "fail")}) for pet {petSlotIndex + 1} ({pet?.Name ?? "Unknown"}).");
                });


            _chat.CommandHandler.RegisterCommand(
                "portalpacket",
                "Drive packet-authored remote town portal and open gate pools",
                "/portalpacket <status|clear|packet <330|331|332|333> <payloadhex>|towncreate <ownerId> <x> <y> [state]|townremove <ownerId> [state]|opengatecreate <ownerId> <first|second> <x> <y> [partyId] [state]|opengateremove <ownerId> <first|second> [state]>",
                args =>
                {
                    if (_temporaryPortalField == null || _mapBoard?.MapInfo == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Remote portal pools are unavailable until a field is loaded.");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket <status|clear|packet <330|331|332|333> <payloadhex>|towncreate <ownerId> <x> <y> [state]|townremove <ownerId> [state]|opengatecreate <ownerId> <first|second> <x> <y> [partyId] [state]|opengateremove <ownerId> <first|second> [state]>");
                    }


                    string action = args[0];
                    if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_temporaryPortalField.DescribeRemotePortalStatus(_mapBoard.MapInfo.id));
                    }


                    if (string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))

                    {

                        _temporaryPortalField.ClearRemotePortals();
                        return ChatCommandHandler.CommandResult.Ok($"Cleared packet-authored remote portals. {_temporaryPortalField.DescribeRemotePortalStatus(_mapBoard.MapInfo.id)}");

                    }



                    if (string.Equals(action, "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int packetType))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket packet <330|331|332|333> <payloadhex>");
                        }


                        byte[] payload;
                        try
                        {
                            payload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                        }
                        catch (Exception ex)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid portal packet payload: {ex.Message}");
                        }


                        return ApplyRemotePortalPacketCommand(packetType, payload);

                    }



                    if (string.Equals(action, "towncreate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !uint.TryParse(args[1], out uint ownerId)
                            || !short.TryParse(args[2], out short x)
                            || !short.TryParse(args[3], out short y))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket towncreate <ownerId> <x> <y> [state]");
                        }


                        byte state = 1;
                        if (args.Length >= 5 && !byte.TryParse(args[4], out state))
                        {
                            return ChatCommandHandler.CommandResult.Error("state must be a byte");
                        }


                        return ApplyRemotePortalPacketCommand(
                            (int)RemotePortalPacketType.TownPortalCreate,
                            BuildTownPortalCreatePayload(state, ownerId, x, y));
                    }


                    if (string.Equals(action, "townremove", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !uint.TryParse(args[1], out uint ownerId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket townremove <ownerId> [state]");
                        }


                        byte state = 1;
                        if (args.Length >= 3 && !byte.TryParse(args[2], out state))
                        {
                            return ChatCommandHandler.CommandResult.Error("state must be a byte");
                        }


                        return ApplyRemotePortalPacketCommand(
                            (int)RemotePortalPacketType.TownPortalRemove,
                            BuildTownPortalRemovePayload(state, ownerId));
                    }


                    if (string.Equals(action, "opengatecreate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 5
                            || !uint.TryParse(args[1], out uint ownerId)
                            || !TryParseOpenGateSlot(args[2], out bool isFirstSlot)
                            || !short.TryParse(args[3], out short x)
                            || !short.TryParse(args[4], out short y))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket opengatecreate <ownerId> <first|second> <x> <y> [partyId] [state]");
                        }


                        uint partyId = 0;
                        if (args.Length >= 6 && !uint.TryParse(args[5], out partyId))
                        {
                            return ChatCommandHandler.CommandResult.Error("partyId must be an unsigned integer");
                        }


                        byte state = 1;
                        if (args.Length >= 7 && !byte.TryParse(args[6], out state))
                        {
                            return ChatCommandHandler.CommandResult.Error("state must be a byte");
                        }


                        return ApplyRemotePortalPacketCommand(
                            (int)RemotePortalPacketType.OpenGateCreate,
                            BuildOpenGateCreatePayload(state, ownerId, x, y, isFirstSlot, partyId));
                    }


                    if (string.Equals(action, "opengateremove", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3
                            || !uint.TryParse(args[1], out uint ownerId)
                            || !TryParseOpenGateSlot(args[2], out bool isFirstSlot))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket opengateremove <ownerId> <first|second> [state]");
                        }


                        byte state = 1;
                        if (args.Length >= 4 && !byte.TryParse(args[3], out state))
                        {
                            return ChatCommandHandler.CommandResult.Error("state must be a byte");
                        }


                        return ApplyRemotePortalPacketCommand(
                            (int)RemotePortalPacketType.OpenGateRemove,
                            BuildOpenGateRemovePayload(state, ownerId, isFirstSlot));
                    }


                    return ChatCommandHandler.CommandResult.Error("Usage: /portalpacket <status|clear|packet <330|331|332|333> <payloadhex>|towncreate <ownerId> <x> <y> [state]|townremove <ownerId> [state]|opengatecreate <ownerId> <first|second> <x> <y> [partyId] [state]|opengateremove <ownerId> <first|second> [state]>");

                });



            _chat.CommandHandler.RegisterCommand(
                "stagepacket",
                "Inspect or drive packet-owned CStage and CMapLoadable transition packets",
                "/stagepacket [status|clear|inbox [status|start [port]|stop]|field <mapId> [portalIndex]|itc|cashshop|backeffect <show|hide> [pageId] [durationMs]|clearbackeffect|objectvisible <name...> <on|off>|packet <141|142|143|144|145|146> [payloadhex=..|payloadb64=..]|packetraw <141|142|143|144|145|146> [hex]]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedStageTransitionStatus());
                    }

                    switch (args[0].ToLowerInvariant())
                    {
                        case "clear":
                            ResetPacketOwnedStageTransitionRuntimeState();
                            return ChatCommandHandler.CommandResult.Ok(DescribePacketOwnedStageTransitionStatus());

                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(DescribePacketOwnedStageTransitionStatus());
                            }

                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int configuredPort = StageTransitionPacketInboxManager.DefaultPort;
                                if (args.Length >= 3
                                    && (!int.TryParse(args[2], out configuredPort) || configuredPort <= 0))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket inbox start [port]");
                                }

                                _stageTransitionPacketInbox.Start(configuredPort);
                                return ChatCommandHandler.CommandResult.Ok(DescribePacketOwnedStageTransitionStatus());
                            }

                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _stageTransitionPacketInbox.Stop();
                                return ChatCommandHandler.CommandResult.Ok(DescribePacketOwnedStageTransitionStatus());
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket inbox [status|start [port]|stop]");

                        case "field":
                            if (args.Length < 2 || !int.TryParse(args[1], out int targetMapId) || targetMapId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket field <mapId> [portalIndex]");
                            }

                            byte targetPortalIndex = 0;
                            if (args.Length >= 3 && !byte.TryParse(args[2], out targetPortalIndex))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket field <mapId> [portalIndex]");
                            }

                            return TryApplyPacketOwnedStageTransitionPacket(
                                141,
                                PacketStageTransitionRuntime.BuildOfficialSetFieldPayload(targetMapId, targetPortalIndex),
                                out string setFieldMessage)
                                ? ChatCommandHandler.CommandResult.Ok(setFieldMessage)
                                : ChatCommandHandler.CommandResult.Error(setFieldMessage);

                        case "itc":
                            return TryApplyPacketOwnedStageTransitionPacket(142, Array.Empty<byte>(), out string itcMessage)
                                ? ChatCommandHandler.CommandResult.Ok(itcMessage)
                                : ChatCommandHandler.CommandResult.Error(itcMessage);

                        case "cashshop":
                            return TryApplyPacketOwnedStageTransitionPacket(143, Array.Empty<byte>(), out string cashShopMessage)
                                ? ChatCommandHandler.CommandResult.Ok(cashShopMessage)
                                : ChatCommandHandler.CommandResult.Error(cashShopMessage);

                        case "backeffect":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket backeffect <show|hide> [pageId] [durationMs]");
                            }

                            byte effect = args[1].ToLowerInvariant() switch
                            {
                                "show" => (byte)0,
                                "hide" => (byte)1,
                                _ => byte.MaxValue
                            };
                            if (effect == byte.MaxValue)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket backeffect <show|hide> [pageId] [durationMs]");
                            }

                            byte pageId = 0;
                            int durationMs = 0;
                            if (args.Length >= 3 && !byte.TryParse(args[2], out pageId))
                            {
                                return ChatCommandHandler.CommandResult.Error("pageId must be a byte");
                            }

                            if (args.Length >= 4 && (!int.TryParse(args[3], out durationMs) || durationMs < 0))
                            {
                                return ChatCommandHandler.CommandResult.Error("durationMs must be a non-negative integer");
                            }

                            return TryApplyPacketOwnedStageTransitionPacket(
                                144,
                                PacketStageTransitionRuntime.BuildBackEffectPayload(effect, _mapBoard?.MapInfo?.id ?? 0, pageId, durationMs),
                                out string backEffectMessage)
                                ? ChatCommandHandler.CommandResult.Ok(backEffectMessage)
                                : ChatCommandHandler.CommandResult.Error(backEffectMessage);

                        case "clearbackeffect":
                            return TryApplyPacketOwnedStageTransitionPacket(146, Array.Empty<byte>(), out string clearBackEffectMessage)
                                ? ChatCommandHandler.CommandResult.Ok(clearBackEffectMessage)
                                : ChatCommandHandler.CommandResult.Error(clearBackEffectMessage);

                        case "objectvisible":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket objectvisible <name...> <on|off>");
                            }

                            bool? objectVisible = args[^1].ToLowerInvariant() switch
                            {
                                "1" => true,
                                "on" => true,
                                "true" => true,
                                "0" => false,
                                "off" => false,
                                "false" => false,
                                _ => null
                            };
                            if (!objectVisible.HasValue)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket objectvisible <name...> <on|off>");
                            }

                            string objectName = string.Join(" ", args.Skip(1).Take(args.Length - 2));
                            return TryApplyPacketOwnedStageTransitionPacket(
                                145,
                                PacketStageTransitionRuntime.BuildMapObjectVisiblePayload((objectName, objectVisible.Value)),
                                out string objectVisibilityMessage)
                                ? ChatCommandHandler.CommandResult.Ok(objectVisibilityMessage)
                                : ChatCommandHandler.CommandResult.Error(objectVisibilityMessage);

                        case "packet":
                            if (args.Length < 2
                                || !int.TryParse(args[1], out int stagePacketType)
                                || stagePacketType < 141
                                || stagePacketType > 146)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket packet <141|142|143|144|145|146> [payloadhex=..|payloadb64=..]");
                            }

                            byte[] stagePacketPayload = Array.Empty<byte>();
                            string packetPayloadError = null;
                            if (stagePacketType is not 142 and not 143 and not 146)
                            {
                                if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out stagePacketPayload, out packetPayloadError))
                                {
                                    return ChatCommandHandler.CommandResult.Error(packetPayloadError ?? "Usage: /stagepacket packet <141|142|143|144|145|146> [payloadhex=..|payloadb64=..]");
                                }
                            }

                            return TryApplyPacketOwnedStageTransitionPacket(stagePacketType, stagePacketPayload, out string stagePacketMessage)
                                ? ChatCommandHandler.CommandResult.Ok(stagePacketMessage)
                                : ChatCommandHandler.CommandResult.Error(stagePacketMessage);

                        case "packetraw":
                            if (args.Length < 2
                                || !int.TryParse(args[1], out int rawStagePacketType)
                                || rawStagePacketType < 141
                                || rawStagePacketType > 146)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket packetraw <141|142|143|144|145|146> [hex]");
                            }

                            byte[] rawStagePayload = Array.Empty<byte>();
                            if (rawStagePacketType is not 142 and not 143 and not 146)
                            {
                                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out rawStagePayload))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket packetraw <141|142|143|144|145|146> [hex]");
                                }
                            }

                            return TryApplyPacketOwnedStageTransitionPacket(rawStagePacketType, rawStagePayload, out string rawStageMessage)
                                ? ChatCommandHandler.CommandResult.Ok(rawStageMessage)
                                : ChatCommandHandler.CommandResult.Error(rawStageMessage);

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /stagepacket [status|clear|inbox [status|start [port]|stop]|field <mapId> [portalIndex]|itc|cashshop|backeffect <show|hide> [pageId] [durationMs]|clearbackeffect|objectvisible <name...> <on|off>|packet <141|142|143|144|145|146> [payloadhex=..|payloadb64=..]|packetraw <141|142|143|144|145|146> [hex]]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "fieldstate",
                "Inspect or drive packet-authored field help, quest timers, field-specific data, and object-state flips",
                "/fieldstate [status|wrapperstatus|desc <index>|questtime <questId> <seconds>|questclear|objectstate <tag> <on|off|0|1>|fieldspecific <payloadhex=..|payloadb64=..>|wrappervalue <huntingadballoon|escortresult> <key> <value>|packet <149|162|166|167|169|174|178|334|335|336|337> [payloadhex=..|payloadb64=..]|packetraw <149|162|166|167|169|174|178|334|335|336|337> [hex]|packetclientraw <hex>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]",
                args =>
                {
                    _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        string wrapperStatus = DescribeClientOwnedFieldWrapperStatus();
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_packetFieldStateRuntime.DescribeStatus(currTickCount)}{Environment.NewLine}{wrapperStatus}{Environment.NewLine}{DescribePacketFieldOfficialSessionBridgeStatus()}");

                    }



                    switch (args[0].ToLowerInvariant())
                    {
                        case "desc":
                        case "help":
                            if (args.Length < 2 || !byte.TryParse(args[1], out byte helpIndex))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate desc <index>");
                            }


                            return TryApplyPacketOwnedFieldStatePacket(162, new[] { helpIndex }, out string descMessage)
                                ? ChatCommandHandler.CommandResult.Ok(descMessage)
                                : ChatCommandHandler.CommandResult.Error(descMessage);
                        case "wrapperstatus":
                            return ChatCommandHandler.CommandResult.Info(DescribeClientOwnedFieldWrapperStatus());


                        case "questtime":

                            if (args.Length < 3 ||
                                !int.TryParse(args[1], out int questTimerQuestId) ||
                                questTimerQuestId <= 0 ||
                                !int.TryParse(args[2], out int questTimerSeconds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate questtime <questId> <seconds>");
                            }


                            using (var stream = new MemoryStream())
                            using (var writer = new BinaryWriter(stream))
                            {
                                DateTime startUtc = DateTime.UtcNow;
                                DateTime endUtc = startUtc.AddSeconds(Math.Max(0, questTimerSeconds));
                                writer.Write((byte)1);
                                writer.Write(questTimerQuestId);
                                writer.Write(startUtc.ToFileTimeUtc());
                                writer.Write(endUtc.ToFileTimeUtc());
                                return TryApplyPacketOwnedFieldStatePacket(167, stream.ToArray(), out string questTimeMessage)
                                    ? ChatCommandHandler.CommandResult.Ok(questTimeMessage)
                                    : ChatCommandHandler.CommandResult.Error(questTimeMessage);
                            }



                        case "questclear":
                        case "clear":
                            return TryApplyPacketOwnedFieldStatePacket(166, Array.Empty<byte>(), out string questClearMessage)
                                ? ChatCommandHandler.CommandResult.Ok(questClearMessage)
                                : ChatCommandHandler.CommandResult.Error(questClearMessage);


                        case "objectstate":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate objectstate <tag> <on|off|0|1>");
                            }


                            bool? isEnabled = args[2].ToLowerInvariant() switch
                            {
                                "1" => true,
                                "on" => true,
                                "true" => true,
                                "0" => false,
                                "off" => false,
                                "false" => false,
                                _ => null
                            };
                            if (!isEnabled.HasValue)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate objectstate <tag> <on|off|0|1>");
                            }



                            using (var stream = new MemoryStream())
                            using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
                            {
                                byte[] tagBytes = Encoding.ASCII.GetBytes(args[1]);
                                writer.Write((short)tagBytes.Length);
                                writer.Write(tagBytes);
                                writer.Write(isEnabled.Value ? 1 : 0);
                                return TryApplyPacketOwnedFieldStatePacket(169, stream.ToArray(), out string objectStateMessage)
                                    ? ChatCommandHandler.CommandResult.Ok(objectStateMessage)
                                    : ChatCommandHandler.CommandResult.Error(objectStateMessage);
                            }



                        case "fieldspecific":

                            byte[] fieldSpecificPayload = null;
                            string fieldSpecificPayloadError = null;
                            if (args.Length < 2 || !TryParseBinaryPayloadArgument(args[1], out fieldSpecificPayload, out fieldSpecificPayloadError))
                            {
                                return ChatCommandHandler.CommandResult.Error(fieldSpecificPayloadError ?? "Usage: /fieldstate fieldspecific <payloadhex=..|payloadb64=..>");
                            }


                            return TryApplyPacketOwnedFieldStatePacket(149, fieldSpecificPayload, out string fieldSpecificMessage)

                                ? ChatCommandHandler.CommandResult.Ok(fieldSpecificMessage)

                                : ChatCommandHandler.CommandResult.Error(fieldSpecificMessage);



                        case "wrappervalue":
                            if (args.Length < 4)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate wrappervalue <huntingadballoon|escortresult> <key> <value>");
                            }


                            string wrapperValue = string.Join(" ", args.Skip(3));
                            return TryApplyClientOwnedWrapperFieldValue(args[1], args[2], wrapperValue, currTickCount, out string wrapperValueMessage)

                                ? ChatCommandHandler.CommandResult.Ok(wrapperValueMessage)

                                : ChatCommandHandler.CommandResult.Error(wrapperValueMessage ?? $"wrapper key not accepted ({args[1]} {args[2]}={wrapperValue})");



                        case "packet":

                            if (args.Length < 2 ||
                                !int.TryParse(args[1], out int fieldPacketType) ||
                                !PacketFieldIngressRouter.IsSupportedFieldScopedPacketType(fieldPacketType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate packet <149|162|166|167|169|174|178|334|335|336|337> [payloadhex=..|payloadb64=..]");
                            }


                            byte[] fieldPacketPayload = Array.Empty<byte>();
                            string fieldPacketPayloadError = null;
                            if (fieldPacketType != 166)
                            {
                                if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out fieldPacketPayload, out fieldPacketPayloadError))
                                {
                                    return ChatCommandHandler.CommandResult.Error(fieldPacketPayloadError ?? "Usage: /fieldstate packet <149|162|166|167|169|174|178|334|335|336|337> [payloadhex=..|payloadb64=..]");
                                }
                            }



                            return TryApplyPacketOwnedFieldScopedPacket(fieldPacketType, fieldPacketPayload, out string fieldPacketMessage)

                                ? ChatCommandHandler.CommandResult.Ok(fieldPacketMessage)

                                : ChatCommandHandler.CommandResult.Error(fieldPacketMessage);



                        case "packetraw":

                            if (args.Length < 2 ||
                                !int.TryParse(args[1], out int rawFieldPacketType) ||
                                !PacketFieldIngressRouter.IsSupportedFieldScopedPacketType(rawFieldPacketType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate packetraw <149|162|166|167|169|174|178|334|335|336|337> [hex]");
                            }


                            byte[] rawFieldPayload = Array.Empty<byte>();
                            if (rawFieldPacketType != 166)
                            {
                                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out rawFieldPayload))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate packetraw <149|162|166|167|169|174|178|334|335|336|337> [hex]");
                                }
                            }



                            return TryApplyPacketOwnedFieldScopedPacket(rawFieldPacketType, rawFieldPayload, out string rawFieldMessage)

                                ? ChatCommandHandler.CommandResult.Ok(rawFieldMessage)

                                : ChatCommandHandler.CommandResult.Error(rawFieldMessage);

                        case "packetclientraw":

                            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawFieldPacket))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate packetclientraw <hex>");
                            }

                            if (!PacketFieldIngressRouter.TryDecodeClientOpcodePacket(rawFieldPacket, out int framedFieldPacketType, out byte[] framedFieldPayload, out string framedFieldError))
                            {
                                return ChatCommandHandler.CommandResult.Error(framedFieldError ?? "Usage: /fieldstate packetclientraw <hex>");
                            }

                            return TryApplyPacketOwnedFieldScopedPacket(framedFieldPacketType, framedFieldPayload, out string framedFieldMessage)

                                ? ChatCommandHandler.CommandResult.Ok($"Applied field-scoped client opcode {framedFieldPacketType}. {framedFieldMessage}")

                                : ChatCommandHandler.CommandResult.Error(framedFieldMessage);

                        case "session":
                            return HandlePacketOwnedFieldStateSessionCommand(args.Skip(1).ToArray());



                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /fieldstate [status|wrapperstatus|desc <index>|questtime <questId> <seconds>|questclear|objectstate <tag> <on|off|0|1>|fieldspecific <payloadhex=..|payloadb64=..>|wrappervalue <huntingadballoon|escortresult> <key> <value>|packet <149|162|166|167|169|174|178|334|335|336|337> [payloadhex=..|payloadb64=..]|packetraw <149|162|166|167|169|174|178|334|335|336|337> [hex]|packetclientraw <hex>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop]]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "scriptmsg",
                "Inspect or drive packet-authored CScriptMan script-message dialogs",
                "/scriptmsg [status|clear|transport <status|start [port]|stop>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop>|say <npcId> <text>|sayimage <npcId> <imagePath[,imagePath...]>|yesno <npcId> <text>|menu <npcId> <text>|quiz <npcId> <default> <min> <max> <prompt>|speedquiz <npcId> <defaultText> <prompt> [option1,option2,...]|avatar <npcId> <prompt> <itemId[,itemId...]>|mavatar <npcId> <prompt> <itemId[,itemId...]>|pet <npcId> <prompt> <itemId[,itemId...]>|petall <npcId> <prompt> <itemId[,itemId...]>|slidemenu <npcId> <skin> <prompt> <option1,option2,...>|text <npcId> <minLen> <maxLen> <defaultText> <prompt>|number <npcId> <default> <min> <max> <prompt>|box <npcId> <columns> <lines> <defaultText> <prompt>|packet <payloadhex=..|payloadb64=..>|packetraw <hex>]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_packetScriptMessageRuntime.DescribeStatus()}{Environment.NewLine}{_initialQuizTimerRuntime.DescribeStatus(currTickCount)}{Environment.NewLine}{_speedQuizOwnerRuntime.DescribeStatus(currTickCount)}{Environment.NewLine}{_packetScriptReplyTransport.LastStatus}{Environment.NewLine}{DescribePacketScriptOfficialSessionBridgeStatus()}");
                    }


                    switch (args[0].ToLowerInvariant())

                    {

                        case "clear":

                            _packetScriptMessageRuntime.Clear();
                            ClearPacketScriptClientOwnerRuntimes();
                            _npcInteractionOverlay?.Close();
                            return ChatCommandHandler.CommandResult.Ok(_packetScriptMessageRuntime.DescribeStatus());

                        case "transport":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(_packetScriptReplyTransport.LastStatus);
                            }

                            if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                            {
                                int port = PacketScriptReplyTransportManager.DefaultPort;
                                if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0 || port > ushort.MaxValue))
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg transport start [port]");
                                }

                                _packetScriptReplyTransport.Start(port);
                                return ChatCommandHandler.CommandResult.Ok(_packetScriptReplyTransport.LastStatus);
                            }

                            if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                            {
                                _packetScriptReplyTransport.Stop();
                                return ChatCommandHandler.CommandResult.Ok(_packetScriptReplyTransport.LastStatus);
                            }

                            return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg transport <status|start [port]|stop>");

                        case "session":
                            return HandlePacketOwnedScriptSessionCommand(args.Skip(1).ToArray());



                        case "say":
                            if (args.Length < 3 || !int.TryParse(args[1], out int sayNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg say <npcId> <text>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSayPacket(sayNpcId, string.Join(" ", args.Skip(2))),
                                out string sayMessage)
                                ? ChatCommandHandler.CommandResult.Ok(sayMessage)
                                : ChatCommandHandler.CommandResult.Error(sayMessage);



                        case "yesno":
                            if (args.Length < 3 || !int.TryParse(args[1], out int yesNoNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg yesno <npcId> <text>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageYesNoPacket(yesNoNpcId, string.Join(" ", args.Skip(2))),
                                out string yesNoMessage)
                                ? ChatCommandHandler.CommandResult.Ok(yesNoMessage)
                                : ChatCommandHandler.CommandResult.Error(yesNoMessage);



                        case "menu":
                            if (args.Length < 3 || !int.TryParse(args[1], out int menuNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg menu <npcId> <text>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageMenuPacket(menuNpcId, string.Join(" ", args.Skip(2))),
                                out string menuMessage)
                                ? ChatCommandHandler.CommandResult.Ok(menuMessage)
                                : ChatCommandHandler.CommandResult.Error(menuMessage);

                        case "quiz":
                            if (args.Length < 6 ||
                                !int.TryParse(args[1], out int quizNpcId) ||
                                !int.TryParse(args[2], out int quizDefaultValue) ||
                                !int.TryParse(args[3], out int quizMinValue) ||
                                !int.TryParse(args[4], out int quizMaxValue))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg quiz <npcId> <default> <min> <max> <prompt>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageQuizPacket(quizNpcId, string.Join(" ", args.Skip(5)), quizDefaultValue, quizMinValue, quizMaxValue),
                                out string quizMessage)
                                ? ChatCommandHandler.CommandResult.Ok(quizMessage)
                                : ChatCommandHandler.CommandResult.Error(quizMessage);

                        case "quizclient":
                            if (args.Length < 6 ||
                                !int.TryParse(args[1], out int quizClientNpcId) ||
                                !int.TryParse(args[3], out int quizClientAnswer) ||
                                !int.TryParse(args[4], out int quizClientQuestionNumber) ||
                                !int.TryParse(args[5], out int quizClientRemainingSeconds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg quizclient <npcId> <title|problem|hint> <answer> <questionNo> <seconds>");
                            }

                            string[] quizClientSegments = args[2].Split(new[] { '|' }, 3, StringSplitOptions.None);
                            if (quizClientSegments.Length != 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg quizclient <npcId> <title|problem|hint> <answer> <questionNo> <seconds>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageQuizClientPacket(
                                    quizClientNpcId,
                                    quizClientSegments[0],
                                    quizClientSegments[1],
                                    quizClientSegments[2],
                                    quizClientAnswer,
                                    quizClientQuestionNumber,
                                    quizClientRemainingSeconds),
                                out string quizClientMessage)
                                ? ChatCommandHandler.CommandResult.Ok(quizClientMessage)
                                : ChatCommandHandler.CommandResult.Error(quizClientMessage);

                        case "quizclose":
                            if (args.Length < 2 || !int.TryParse(args[1], out int quizCloseNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg quizclose <npcId>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageQuizClientClosePacket(quizCloseNpcId),
                                out string quizCloseMessage)
                                ? ChatCommandHandler.CommandResult.Ok(quizCloseMessage)
                                : ChatCommandHandler.CommandResult.Error(quizCloseMessage);

                        case "speedquiz":
                            if (args.Length < 4 || !int.TryParse(args[1], out int speedQuizNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg speedquiz <npcId> <defaultText> <prompt> [option1,option2,...]");
                            }

                            string[] speedQuizOptions = args.Length >= 5
                                ? args[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                : Array.Empty<string>();
                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSpeedQuizPacket(speedQuizNpcId, args[3], args[2], speedQuizOptions),
                                out string speedQuizMessage)
                                ? ChatCommandHandler.CommandResult.Ok(speedQuizMessage)
                                : ChatCommandHandler.CommandResult.Error(speedQuizMessage);

                        case "speedquizclient":
                            if (args.Length < 7 ||
                                !int.TryParse(args[1], out int speedQuizClientNpcId) ||
                                !int.TryParse(args[2], out int currentQuestion) ||
                                !int.TryParse(args[3], out int totalQuestions) ||
                                !int.TryParse(args[4], out int correctAnswers) ||
                                !int.TryParse(args[5], out int remainingQuestions) ||
                                !int.TryParse(args[6], out int speedQuizRemainingSeconds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg speedquizclient <npcId> <currentQuestion> <totalQuestions> <correctAnswers> <remainingQuestions> <seconds>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSpeedQuizClientPacket(
                                    speedQuizClientNpcId,
                                    currentQuestion,
                                    totalQuestions,
                                    correctAnswers,
                                    remainingQuestions,
                                    speedQuizRemainingSeconds),
                                out string speedQuizClientMessage)
                                ? ChatCommandHandler.CommandResult.Ok(speedQuizClientMessage)
                                : ChatCommandHandler.CommandResult.Error(speedQuizClientMessage);

                        case "speedquizclose":
                            if (args.Length < 2 || !int.TryParse(args[1], out int speedQuizCloseNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg speedquizclose <npcId>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSpeedQuizClientClosePacket(speedQuizCloseNpcId),
                                out string speedQuizCloseMessage)
                                ? ChatCommandHandler.CommandResult.Ok(speedQuizCloseMessage)
                                : ChatCommandHandler.CommandResult.Error(speedQuizCloseMessage);

                        case "sayimage":
                            if (args.Length < 3 || !int.TryParse(args[1], out int sayImageNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg sayimage <npcId> <imagePath[,imagePath...]>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSayImagePacket(
                                    sayImageNpcId,
                                    args[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                                out string sayImageMessage)
                                ? ChatCommandHandler.CommandResult.Ok(sayImageMessage)
                                : ChatCommandHandler.CommandResult.Error(sayImageMessage);

                        case "avatar":
                            if (args.Length < 4 || !int.TryParse(args[1], out int avatarNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg avatar <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            if (!TryParsePacketOwnedItemIdList(args[3], out int[] avatarItemIds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg avatar <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageAvatarPacket(avatarNpcId, args[2], avatarItemIds),
                                out string avatarMessage)
                                ? ChatCommandHandler.CommandResult.Ok(avatarMessage)
                                : ChatCommandHandler.CommandResult.Error(avatarMessage);

                        case "mavatar":
                            if (args.Length < 4 || !int.TryParse(args[1], out int membershopAvatarNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg mavatar <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            if (!TryParsePacketOwnedItemIdList(args[3], out int[] membershopAvatarItemIds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg mavatar <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageMembershopAvatarPacket(membershopAvatarNpcId, args[2], membershopAvatarItemIds),
                                out string membershopAvatarMessage)
                                ? ChatCommandHandler.CommandResult.Ok(membershopAvatarMessage)
                                : ChatCommandHandler.CommandResult.Error(membershopAvatarMessage);

                        case "pet":
                            if (args.Length < 4 || !int.TryParse(args[1], out int petNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg pet <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            if (!TryParsePacketOwnedItemIdList(args[3], out int[] petItemIds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg pet <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessagePetPacket(petNpcId, args[2], petItemIds),
                                out string petMessage)
                                ? ChatCommandHandler.CommandResult.Ok(petMessage)
                                : ChatCommandHandler.CommandResult.Error(petMessage);

                        case "petall":
                            if (args.Length < 4 || !int.TryParse(args[1], out int petAllNpcId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg petall <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            if (!TryParsePacketOwnedItemIdList(args[3], out int[] petAllItemIds))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg petall <npcId> <prompt> <itemId[,itemId...]>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessagePetAllPacket(petAllNpcId, args[2], petAllItemIds),
                                out string petAllMessage)
                                ? ChatCommandHandler.CommandResult.Ok(petAllMessage)
                                : ChatCommandHandler.CommandResult.Error(petAllMessage);

                        case "slidemenu":
                            if (args.Length < 5 ||
                                !int.TryParse(args[1], out int slideMenuNpcId) ||
                                !byte.TryParse(args[2], out byte slideMenuSkin))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg slidemenu <npcId> <skin> <prompt> <option1,option2,...>");
                            }

                            string[] slideMenuOptions = args[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (slideMenuOptions.Length == 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg slidemenu <npcId> <skin> <prompt> <option1,option2,...>");
                            }

                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageSlideMenuPacket(slideMenuNpcId, slideMenuSkin, args[3], slideMenuOptions),
                                out string slideMenuMessage)
                                ? ChatCommandHandler.CommandResult.Ok(slideMenuMessage)
                                : ChatCommandHandler.CommandResult.Error(slideMenuMessage);

                        case "text":

                            if (args.Length < 6 ||
                                !int.TryParse(args[1], out int textNpcId) ||
                                !short.TryParse(args[2], out short textMinLength) ||
                                !short.TryParse(args[3], out short textMaxLength))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg text <npcId> <minLen> <maxLen> <defaultText> <prompt>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageTextPacket(textNpcId, string.Join(" ", args.Skip(5)), args[4], textMinLength, textMaxLength),
                                out string textMessage)
                                ? ChatCommandHandler.CommandResult.Ok(textMessage)
                                : ChatCommandHandler.CommandResult.Error(textMessage);



                        case "number":

                            if (args.Length < 6 ||
                                !int.TryParse(args[1], out int numberNpcId) ||
                                !int.TryParse(args[2], out int numberDefaultValue) ||
                                !int.TryParse(args[3], out int numberMinValue) ||
                                !int.TryParse(args[4], out int numberMaxValue))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg number <npcId> <default> <min> <max> <prompt>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageNumberPacket(numberNpcId, string.Join(" ", args.Skip(5)), numberDefaultValue, numberMinValue, numberMaxValue),
                                out string numberMessage)
                                ? ChatCommandHandler.CommandResult.Ok(numberMessage)
                                : ChatCommandHandler.CommandResult.Error(numberMessage);



                        case "box":

                            if (args.Length < 6 ||
                                !int.TryParse(args[1], out int boxNpcId) ||
                                !short.TryParse(args[2], out short boxColumns) ||
                                !short.TryParse(args[3], out short boxLines))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg box <npcId> <columns> <lines> <defaultText> <prompt>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(
                                BuildScriptMessageBoxTextPacket(boxNpcId, string.Join(" ", args.Skip(5)), args[4], boxColumns, boxLines),
                                out string boxMessage)
                                ? ChatCommandHandler.CommandResult.Ok(boxMessage)
                                : ChatCommandHandler.CommandResult.Error(boxMessage);



                        case "packet":

                            byte[] packetPayload = null;
                            string packetPayloadError = null;
                            if (args.Length < 2 || !TryParseBinaryPayloadArgument(args[1], out packetPayload, out packetPayloadError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetPayloadError ?? "Usage: /scriptmsg packet <payloadhex=..|payloadb64=..>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(packetPayload, out string packetMessage)

                                ? ChatCommandHandler.CommandResult.Ok(packetMessage)

                                : ChatCommandHandler.CommandResult.Error(packetMessage);



                        case "packetraw":
                            if (args.Length < 2 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(1)), out byte[] rawPacketPayload))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg packetraw <hex>");
                            }


                            return TryApplyPacketOwnedScriptMessagePacket(rawPacketPayload, out string rawPacketMessage)

                                ? ChatCommandHandler.CommandResult.Ok(rawPacketMessage)

                                : ChatCommandHandler.CommandResult.Error(rawPacketMessage);



                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /scriptmsg [status|clear|transport <status|start [port]|stop>|session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|stop>|say <npcId> <text>|sayimage <npcId> <imagePath[,imagePath...]>|yesno <npcId> <text>|menu <npcId> <text>|quiz <npcId> <default> <min> <max> <prompt>|speedquiz <npcId> <defaultText> <prompt> [option1,option2,...]|avatar <npcId> <prompt> <itemId[,itemId...]>|mavatar <npcId> <prompt> <itemId[,itemId...]>|pet <npcId> <prompt> <itemId[,itemId...]>|petall <npcId> <prompt> <itemId[,itemId...]>|slidemenu <npcId> <skin> <prompt> <option1,option2,...>|text <npcId> <minLen> <maxLen> <defaultText> <prompt>|number <npcId> <default> <min> <max> <prompt>|box <npcId> <columns> <lines> <defaultText> <prompt>|packet <payloadhex=..|payloadb64=..>|packetraw <hex>]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "affectedpacket",
                "Drive packet-authored remote affected-area create/remove flow",
                "/affectedpacket <status|clear|packet <328|329> <payloadhex>|create <objectId> <type> <ownerId> <skillId> <skillLevel> <left> <top> <right> <bottom> [startDelayUnits] [elemAttr] [phase]|remove <objectId>>",
                args =>
                {
                    if (_affectedAreaPool == null || _mapBoard?.MapInfo == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Remote affected-area pool is unavailable until a field is loaded.");
                    }


                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /affectedpacket <status|clear|packet <328|329> <payloadhex>|create <objectId> <type> <ownerId> <skillId> <skillLevel> <left> <top> <right> <bottom> [startDelayUnits] [elemAttr] [phase]|remove <objectId>>");
                    }


                    string action = args[0];
                    if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribeRemoteAffectedAreaStatus());
                    }


                    if (string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))

                    {

                        ClearRemoteAffectedAreas();
                        return ChatCommandHandler.CommandResult.Ok($"Cleared packet-authored remote affected areas. {DescribeRemoteAffectedAreaStatus()}");

                    }



                    if (string.Equals(action, "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int packetType))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /affectedpacket packet <328|329> <payloadhex>");
                        }


                        byte[] payload;
                        try
                        {
                            payload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                        }
                        catch (Exception ex)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid affected-area packet payload: {ex.Message}");
                        }


                        return ApplyRemoteAffectedAreaPacketCommand(packetType, payload);

                    }



                    if (string.Equals(action, "create", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 10
                            || !int.TryParse(args[1], out int objectId)
                            || !int.TryParse(args[2], out int type)
                            || !uint.TryParse(args[3], out uint ownerId)
                            || !int.TryParse(args[4], out int skillId)
                            || !byte.TryParse(args[5], out byte skillLevel)
                            || !int.TryParse(args[6], out int left)
                            || !int.TryParse(args[7], out int top)
                            || !int.TryParse(args[8], out int right)
                            || !int.TryParse(args[9], out int bottom))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /affectedpacket create <objectId> <type> <ownerId> <skillId> <skillLevel> <left> <top> <right> <bottom> [startDelayUnits] [elemAttr] [phase]");
                        }


                        short startDelayUnits = 0;
                        if (args.Length >= 11 && !short.TryParse(args[10], out startDelayUnits))
                        {
                            return ChatCommandHandler.CommandResult.Error("startDelayUnits must be a signed short");
                        }


                        int elemAttr = 0;
                        if (args.Length >= 12 && !int.TryParse(args[11], out elemAttr))
                        {
                            return ChatCommandHandler.CommandResult.Error("elemAttr must be an integer");
                        }


                        int phase = 0;
                        if (args.Length >= 13 && !int.TryParse(args[12], out phase))
                        {
                            return ChatCommandHandler.CommandResult.Error("phase must be an integer");
                        }


                        Rectangle bounds = new Rectangle(
                            Math.Min(left, right),
                            Math.Min(top, bottom),
                            Math.Max(1, Math.Abs(right - left)),
                            Math.Max(1, Math.Abs(bottom - top)));


                        return ApplyRemoteAffectedAreaPacketCommand(
                            (int)RemoteAffectedAreaPacketType.Create,
                            RemoteAffectedAreaPacketCodec.BuildCreatePayload(
                                objectId,
                                type,
                                ownerId,
                                skillId,
                                skillLevel,
                                startDelayUnits,
                                bounds,
                                elemAttr,
                                phase));
                    }


                    if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int objectId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /affectedpacket remove <objectId>");
                        }


                        return ApplyRemoteAffectedAreaPacketCommand(
                            (int)RemoteAffectedAreaPacketType.Remove,
                            RemoteAffectedAreaPacketCodec.BuildRemovePayload(objectId));
                    }


                    return ChatCommandHandler.CommandResult.Error("Usage: /affectedpacket <status|clear|packet <328|329> <payloadhex>|create <objectId> <type> <ownerId> <skillId> <skillLevel> <left> <top> <right> <bottom> [startDelayUnits] [elemAttr] [phase]|remove <objectId>>");

                });



            _chat.CommandHandler.RegisterCommand(
                "droppacket",
                "Drive packet-owned drop enter/leave flow",
                "/droppacket <status|clear|packet <322|324> <payloadhex>|create <dropId> <enterType> <meso|item> <info> <ownerId> <ownerType> <x> <y> [sourceId] [startX startY delayMs] [petPickup] [elevateLayer]|leave <dropId> <remove|playerpickup|petpickup|mobpickup|explode> [actorId|delayMs] [petIndex]>",
                args =>
                {
                    if (_dropPool == null || _mapBoard?.MapInfo == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Drop pool is unavailable until a field is loaded.");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /droppacket <status|clear|packet <322|324> <payloadhex>|create <dropId> <enterType> <meso|item> <info> <ownerId> <ownerType> <x> <y> [sourceId] [startX startY delayMs] [petPickup] [elevateLayer]|leave <dropId> <remove|playerpickup|petpickup|mobpickup|explode> [actorId|delayMs] [petIndex]>");
                    }

                    string action = args[0];
                    if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribeRemoteDropStatus());
                    }

                    if (string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _dropPool.ClearPacketDrops();
                        return ChatCommandHandler.CommandResult.Ok($"Cleared packet-authored drops. {DescribeRemoteDropStatus()}");
                    }

                    if (string.Equals(action, "packet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int packetType))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /droppacket packet <322|324> <payloadhex>");
                        }

                        byte[] payload;
                        try
                        {
                            payload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                        }
                        catch (Exception ex)
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid drop packet payload: {ex.Message}");
                        }

                        return ApplyRemoteDropPacketCommand(packetType, payload);
                    }

                    if (string.Equals(action, "create", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 9
                            || !int.TryParse(args[1], out int dropId)
                            || !byte.TryParse(args[2], out byte enterType)
                            || !int.TryParse(args[4], out int info)
                            || !int.TryParse(args[5], out int ownerId)
                            || !byte.TryParse(args[6], out byte ownerTypeValue)
                            || !short.TryParse(args[7], out short x)
                            || !short.TryParse(args[8], out short y))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /droppacket create <dropId> <enterType> <meso|item> <info> <ownerId> <ownerType> <x> <y> [sourceId] [startX startY delayMs] [petPickup] [elevateLayer]");
                        }

                        bool isMoney = string.Equals(args[3], "meso", StringComparison.OrdinalIgnoreCase);
                        if (!isMoney && !string.Equals(args[3], "item", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Error("drop type must be meso or item");
                        }

                        int sourceId = 0;
                        if (args.Length >= 10 && !int.TryParse(args[9], out sourceId))
                        {
                            return ChatCommandHandler.CommandResult.Error("sourceId must be an integer");
                        }

                        short? startX = null;
                        short? startY = null;
                        short delayMs = 0;
                        if (args.Length >= 13)
                        {
                            if (!short.TryParse(args[10], out short parsedStartX)
                                || !short.TryParse(args[11], out short parsedStartY)
                                || !short.TryParse(args[12], out delayMs))
                            {
                                return ChatCommandHandler.CommandResult.Error("startX, startY, and delayMs must be signed shorts");
                            }

                            startX = parsedStartX;
                            startY = parsedStartY;
                        }

                        bool petPickup = true;
                        if (args.Length >= 14 && !bool.TryParse(args[13], out petPickup))
                        {
                            return ChatCommandHandler.CommandResult.Error("petPickup must be true or false");
                        }

                        bool elevateLayer = false;
                        if (args.Length >= 15 && !bool.TryParse(args[14], out elevateLayer))
                        {
                            return ChatCommandHandler.CommandResult.Error("elevateLayer must be true or false");
                        }

                        byte[] payload = RemoteDropPacketCodec.BuildEnterPayload(
                            enterType,
                            dropId,
                            isMoney,
                            info,
                            ownerId,
                            (DropOwnershipType)ownerTypeValue,
                            x,
                            y,
                            sourceId,
                            startX,
                            startY,
                            delayMs,
                            expireRaw: 0,
                            allowPetPickup: petPickup,
                            elevateLayer: elevateLayer);
                        return ApplyRemoteDropPacketCommand((int)RemoteDropPacketType.Enter, payload);
                    }

                    if (string.Equals(action, "leave", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[1], out int dropId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /droppacket leave <dropId> <remove|playerpickup|petpickup|mobpickup|explode> [actorId|delayMs] [petIndex]");
                        }

                        PacketDropLeaveReason reason = args[2].ToLowerInvariant() switch
                        {
                            "playerpickup" => PacketDropLeaveReason.PlayerPickup,
                            "petpickup" => PacketDropLeaveReason.PetPickup,
                            "mobpickup" => PacketDropLeaveReason.MobPickup,
                            "explode" => PacketDropLeaveReason.Explode,
                            _ => PacketDropLeaveReason.Remove
                        };

                        int actorId = 0;
                        short delayMs = 0;
                        int secondaryActorId = 0;
                        if (reason == PacketDropLeaveReason.Explode)
                        {
                            if (args.Length >= 4 && !short.TryParse(args[3], out delayMs))
                            {
                                return ChatCommandHandler.CommandResult.Error("delayMs must be a signed short");
                            }
                        }
                        else
                        {
                            if (args.Length >= 4 && !int.TryParse(args[3], out actorId))
                            {
                                return ChatCommandHandler.CommandResult.Error("actorId must be an integer");
                            }

                            if (reason == PacketDropLeaveReason.PetPickup
                                && args.Length >= 5
                                && !int.TryParse(args[4], out secondaryActorId))
                            {
                                return ChatCommandHandler.CommandResult.Error("petIndex must be an integer");
                            }
                        }

                        byte[] payload = RemoteDropPacketCodec.BuildLeavePayload(reason, dropId, actorId, delayMs, secondaryActorId);
                        return ApplyRemoteDropPacketCommand((int)RemoteDropPacketType.Leave, payload);
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /droppacket <status|clear|packet <322|324> <payloadhex>|create <dropId> <enterType> <meso|item> <info> <ownerId> <ownerType> <x> <y> [sourceId] [startX startY delayMs] [petPickup] [elevateLayer]|leave <dropId> <remove|playerpickup|petpickup|mobpickup|explode> [actorId|delayMs] [petIndex]>");
                });


            _chat.CommandHandler.RegisterCommand(

                "objtag",
                "Publish or clear a dynamic object-tag state",
                "/objtag <tag> <on|off|clear> [transitionMs]",
                args =>
                {
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /objtag <tag> <on|off|clear> [transitionMs]");
                    }


                    string tag = args[0];
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        return ChatCommandHandler.CommandResult.Error("Tag must not be empty");
                    }


                    string action = args[1];
                    bool? isEnabled = action.ToLowerInvariant() switch
                    {
                        "on" => true,
                        "off" => false,
                        "clear" => null,
                        _ => null
                    };


                    if (!string.Equals(action, "on", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(action, "off", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Error("State must be one of: on, off, clear");
                    }


                    int transitionMs = 0;
                    if (args.Length >= 3 && !int.TryParse(args[2], out transitionMs))
                    {
                        return ChatCommandHandler.CommandResult.Error("transitionMs must be an integer");
                    }


                    bool changed = SetDynamicObjectTagState(tag, isEnabled, transitionMs, currTickCount);
                    if (!changed)
                    {
                        return ChatCommandHandler.CommandResult.Error($"No published state existed for object tag '{tag}'.");
                    }


                    string stateLabel = isEnabled.HasValue ? (isEnabled.Value ? "ON" : "OFF") : "CLEARED";
                    return ChatCommandHandler.CommandResult.Ok($"Object tag '{tag}' set to {stateLabel}");
                });
        }
        private static byte[] BuildScriptMessageSayPacket(int npcId, string text)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)0);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, text);
            writer.Write((byte)0);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageSayImagePacket(int npcId, IReadOnlyList<string> imagePaths)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)1);
            writer.Write((byte)0);
            writer.Write((byte)Math.Min(byte.MaxValue, imagePaths?.Count ?? 0));
            foreach (string imagePath in imagePaths ?? Array.Empty<string>())
            {
                WritePacketOwnedMapleString(writer, imagePath);
            }

            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageYesNoPacket(int npcId, string text)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)2);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, text);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageMenuPacket(int npcId, string text)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)5);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, text);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageQuizPacket(int npcId, string prompt, int defaultValue, int minValue, int maxValue)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)6);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            writer.Write(defaultValue);
            writer.Write(minValue);
            writer.Write(maxValue);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageQuizClientPacket(
            int npcId,
            string title,
            string problemText,
            string hintText,
            int correctAnswer,
            int questionNumber,
            int remainingSeconds)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)6);
            writer.Write((byte)0);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, title);
            WritePacketOwnedMapleString(writer, problemText);
            WritePacketOwnedMapleString(writer, hintText);
            writer.Write(correctAnswer);
            writer.Write(questionNumber);
            writer.Write(remainingSeconds);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageQuizClientClosePacket(int npcId)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)6);
            writer.Write((byte)0);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageSpeedQuizPacket(int npcId, string prompt, string defaultText, IReadOnlyList<string> options)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)7);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            WritePacketOwnedMapleString(writer, defaultText);
            writer.Write((byte)Math.Min(byte.MaxValue, options?.Count ?? 0));
            foreach (string option in options ?? Array.Empty<string>())
            {
                WritePacketOwnedMapleString(writer, option);
            }

            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageSpeedQuizClientPacket(
            int npcId,
            int currentQuestion,
            int totalQuestions,
            int correctAnswers,
            int remainingQuestions,
            int remainingSeconds)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)7);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(currentQuestion);
            writer.Write(totalQuestions);
            writer.Write(correctAnswers);
            writer.Write(remainingQuestions);
            writer.Write(remainingSeconds);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageSpeedQuizClientClosePacket(int npcId)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)7);
            writer.Write((byte)0);
            writer.Write((byte)1);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageAvatarPacket(int npcId, string prompt, IReadOnlyList<int> itemIds)
        {
            return BuildScriptMessageAvatarPacketCore(npcId, 8, prompt, itemIds);
        }

        private static byte[] BuildScriptMessageMembershopAvatarPacket(int npcId, string prompt, IReadOnlyList<int> itemIds)
        {
            return BuildScriptMessageAvatarPacketCore(npcId, 9, prompt, itemIds);
        }

        private static byte[] BuildScriptMessagePetPacket(int npcId, string prompt, IReadOnlyList<int> petIds)
        {
            return BuildScriptMessageIndexedSelectionPacket(npcId, 10, prompt, petIds);
        }

        private static byte[] BuildScriptMessagePetAllPacket(int npcId, string prompt, IReadOnlyList<int> petIds)
        {
            return BuildScriptMessageIndexedSelectionPacket(npcId, 11, prompt, petIds);
        }

        private static byte[] BuildScriptMessageSlideMenuPacket(int npcId, byte skin, string prompt, IReadOnlyList<string> options)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)15);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            writer.Write(skin);
            writer.Write((byte)Math.Min(byte.MaxValue, options?.Count ?? 0));
            foreach (string option in options ?? Array.Empty<string>())
            {
                WritePacketOwnedMapleString(writer, option);
            }

            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageAvatarPacketCore(int npcId, byte messageType, string prompt, IReadOnlyList<int> itemIds)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write(messageType);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            writer.Write((byte)Math.Min(byte.MaxValue, itemIds?.Count ?? 0));
            foreach (int itemId in itemIds ?? Array.Empty<int>())
            {
                writer.Write(itemId);
            }

            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageIndexedSelectionPacket(int npcId, byte messageType, string prompt, IReadOnlyList<int> values)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write(messageType);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            writer.Write((byte)Math.Min(byte.MaxValue, values?.Count ?? 0));
            foreach (int value in values ?? Array.Empty<int>())
            {
                writer.Write(value);
            }

            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageTextPacket(int npcId, string prompt, string defaultText, short minLength, short maxLength)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)3);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            WritePacketOwnedMapleString(writer, defaultText);
            writer.Write(minLength);
            writer.Write(maxLength);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageNumberPacket(int npcId, string prompt, int defaultValue, int minValue, int maxValue)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)4);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            writer.Write(defaultValue);
            writer.Write(minValue);
            writer.Write(maxValue);
            return stream.ToArray();
        }

        private static byte[] BuildScriptMessageBoxTextPacket(int npcId, string prompt, string defaultText, short columns, short lines)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)14);
            writer.Write((byte)0);
            WritePacketOwnedMapleString(writer, prompt);
            WritePacketOwnedMapleString(writer, defaultText);
            writer.Write(columns);
            writer.Write(lines);
            return stream.ToArray();
        }

        private static void WritePacketOwnedMapleString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.Default.GetBytes(text ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private ChatCommandHandler.CommandResult HandleExpeditionIntermediaryCommand(string[] args)
        {
            _socialListRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);
            if (args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Info(
                    $"{_socialListRuntime.DescribeExpeditionStatus()}{Environment.NewLine}{DescribeExpeditionIntermediaryPacketInboxStatus()}{Environment.NewLine}{DescribeExpeditionIntermediaryOfficialSessionBridgeStatus()}");
            }

            int index = 0;
            bool packetOwned = false;
            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "local", StringComparison.OrdinalIgnoreCase))
            {
                packetOwned = string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase);
                index = 1;
                if (args.Length <= index)
                {
                    return ChatCommandHandler.CommandResult.Info(_socialListRuntime.DescribeExpeditionStatus());
                }
            }

            string action = args[index].ToLowerInvariant();
            switch (action)
            {
                case "status":
                    return ChatCommandHandler.CommandResult.Info(
                        $"{_socialListRuntime.DescribeExpeditionStatus()}{Environment.NewLine}{DescribeExpeditionIntermediaryPacketInboxStatus()}{Environment.NewLine}{DescribeExpeditionIntermediaryOfficialSessionBridgeStatus()}");

                case "payload":
                    return HandleExpeditionPayloadCommand(args, index + 1, packetOwned);

                case "packetraw":
                    return HandleExpeditionRawPacketCommand(args, index + 1, packetOwned);

                case "inbox":
                    return HandleExpeditionInboxCommand(args, index + 1);

                case "bridge":
                    return HandleExpeditionBridgeCommand(args, index + 1);

                case "open":
                case "search":
                    _socialListRuntime.OpenSearchWindow(SocialSearchTab.Expedition);
                    WireSocialSearchWindowData();
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialSearch);
                    return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.DescribeExpeditionStatus());

                case "clear":
                    return ChatCommandHandler.CommandResult.Ok(_socialListRuntime.ClearExpeditionIntermediary(packetOwned));

                case "start":
                case "register":
                {
                    string title = TryGetExpeditionCommandValue(args, index + 1, "title", out string titledValue)
                        ? NormalizeExpeditionCommandText(titledValue)
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalTitle)
                            ? NormalizeExpeditionCommandText(positionalTitle)
                            : null;
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.StartLocalExpeditionIntermediary(title, registrationDraft: string.Equals(action, "register", StringComparison.OrdinalIgnoreCase)));
                }

                case "get":
                {
                    string title = TryGetExpeditionCommandValue(args, index + 1, "title", out string titledValue)
                        ? NormalizeExpeditionCommandText(titledValue)
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalTitle)
                            ? NormalizeExpeditionCommandText(positionalTitle)
                            : null;
                    int masterPartyIndex = TryGetExpeditionCommandInt(args, index + 1, "master", out int parsedMaster) ? parsedMaster : 0;
                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode) ? parsedRetCode : 59;
                    if (!TryGetExpeditionCommandValue(args, index + 1, "parties", out string partySpec)
                        || string.IsNullOrWhiteSpace(partySpec))
                    {
                        return ChatCommandHandler.CommandResult.Ok(
                            _socialListRuntime.ApplyExpeditionGet(title, masterPartyIndex, null, packetOwned, retCode));
                    }

                    if (!TryParseExpeditionPartySeeds(
                            partySpec,
                            _playerManager?.Player?.Build?.Name,
                            GetCurrentMapTransferDisplayName(),
                            1,
                            out IReadOnlyList<ExpeditionPartySeed> parties,
                            out string error))
                    {
                        return ChatCommandHandler.CommandResult.Error(error);
                    }

                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionGet(title, masterPartyIndex, parties, packetOwned, retCode));
                }

                case "modified":
                {
                    int partyIndex = TryGetExpeditionCommandInt(args, index + 1, "party", out int parsedPartyIndex)
                        ? parsedPartyIndex
                        : 0;
                    int? masterPartyIndex = TryGetExpeditionCommandInt(args, index + 1, "master", out int parsedMaster)
                        ? parsedMaster
                        : null;
                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode) ? parsedRetCode : 70;
                    TryGetExpeditionCommandValue(args, index + 1, "members", out string memberSpec);
                    if (!TryParseExpeditionMemberSeeds(
                            memberSpec,
                            _playerManager?.Player?.Build?.Name,
                            GetCurrentMapTransferDisplayName(),
                            1,
                            out IReadOnlyList<ExpeditionMemberSeed> members,
                            out string error))
                    {
                        return ChatCommandHandler.CommandResult.Error(error);
                    }

                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionModified(partyIndex, members, masterPartyIndex, packetOwned, retCode));
                }

                case "invite":
                {
                    string inviterName = TryGetExpeditionCommandValue(args, index + 1, "name", out string namedInviter)
                        ? NormalizeExpeditionCommandText(namedInviter)
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalInviter)
                            ? NormalizeExpeditionCommandText(positionalInviter)
                            : "Expedition Leader";
                    int level = TryGetExpeditionCommandInt(args, index + 1, "level", out int parsedLevel) ? parsedLevel : 1;
                    int jobCode = TryGetExpeditionCommandInt(args, index + 1, "job", out int parsedJobCode) ? parsedJobCode : 0;
                    int partyQuestId = TryGetExpeditionCommandInt(args, index + 1, "pq", out int parsedPartyQuestId) ? parsedPartyQuestId : 0;
                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode) ? parsedRetCode : 72;
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionInvite(inviterName, level, jobCode, partyQuestId, packetOwned, retCode));
                }

                case "response":
                {
                    string inviterName = TryGetExpeditionCommandValue(args, index + 1, "name", out string namedInviter)
                        ? NormalizeExpeditionCommandText(namedInviter)
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalInviter)
                            ? NormalizeExpeditionCommandText(positionalInviter)
                            : null;
                    string responseToken = TryGetExpeditionCommandValue(args, index + 1, "result", out string namedResponse)
                        ? namedResponse
                        : TryGetExpeditionPositionalValue(args, index + 2, out string positionalResponse)
                            ? positionalResponse
                            : "accept";
                    if (!TryParseExpeditionResponseCode(responseToken, out int responseCode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Response must be accept, decline, busy, changed, blocked, unavailable, fail6, promptopen, or a numeric code.");
                    }

                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode) ? parsedRetCode : 73;
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionResponseInvite(inviterName, responseCode, packetOwned, retCode));
                }

                case "notice":
                {
                    string kindToken = TryGetExpeditionCommandValue(args, index + 1, "kind", out string namedKind)
                        ? namedKind
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalKind)
                            ? positionalKind
                            : "joined";
                    if (!TryParseExpeditionNoticeKind(kindToken, out ExpeditionNoticeKind noticeKind))
                    {
                        return ChatCommandHandler.CommandResult.Error("Notice kind must be joined, left, or removed.");
                    }

                    string characterName = TryGetExpeditionCommandValue(args, index + 1, "name", out string namedCharacter)
                        ? NormalizeExpeditionCommandText(namedCharacter)
                        : TryGetExpeditionPositionalValue(args, index + 2, out string positionalCharacter)
                            ? NormalizeExpeditionCommandText(positionalCharacter)
                            : null;
                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode)
                        ? parsedRetCode
                        : noticeKind switch
                        {
                            ExpeditionNoticeKind.Left => 64,
                            ExpeditionNoticeKind.Removed => 66,
                            _ => 60
                        };
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionNotice(noticeKind, characterName, packetOwned, retCode));
                }

                case "master":
                {
                    int masterPartyIndex = TryGetExpeditionCommandInt(args, index + 1, "party", out int parsedPartyIndex)
                        ? parsedPartyIndex
                        : TryGetExpeditionPositionalInt(args, index + 1, out int positionalPartyIndex)
                            ? positionalPartyIndex
                            : 0;
                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode) ? parsedRetCode : 69;
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionMasterChanged(masterPartyIndex, packetOwned, retCode));
                }

                case "removed":
                {
                    string removalToken = TryGetExpeditionCommandValue(args, index + 1, "kind", out string namedKind)
                        ? namedKind
                        : TryGetExpeditionPositionalValue(args, index + 1, out string positionalKind)
                            ? positionalKind
                            : "disband";
                    if (!TryParseExpeditionRemovalKind(removalToken, out ExpeditionRemovalKind removalKind))
                    {
                        return ChatCommandHandler.CommandResult.Error("Removal kind must be leave, disband, removed, or kicked.");
                    }

                    int retCode = TryGetExpeditionCommandInt(args, index + 1, "ret", out int parsedRetCode)
                        ? parsedRetCode
                        : removalKind switch
                        {
                            ExpeditionRemovalKind.Leave => 65,
                            ExpeditionRemovalKind.Removed => 68,
                            _ => 67
                        };
                    return ChatCommandHandler.CommandResult.Ok(
                        _socialListRuntime.ApplyExpeditionRemoved(removalKind, packetOwned, retCode));
                }

                default:
                    return ChatCommandHandler.CommandResult.Error(
                        "Usage: /expedition [packet|local] [status|open|search|clear|start [title=Name]|register [title=Name]|get [title=Name] [master=n] [parties=party~name~role~level~map~channel~online~local;...]|modified [party=n] [members=name~role~level~map~channel~online~local;...] [master=n]|invite [name=Leader] [level=n] [job=n] [pq=n]|response [name=Leader] [result=accept|decline|busy|changed|blocked|unavailable|fail6|promptopen|n]|notice [kind=joined|left|removed] [name=Member]|master [party=n]|removed [kind=leave|disband|removed]|payload <payloadhex=..|payloadb64=..>|packetraw <hex> [opcode=n]|inbox [status|start [port]|stop]|bridge [status|history [count]|clearhistory|replay <historyIndex>|sendraw <hex>|discoverstatus <remotePort> [process=selector] [localPort=n]|start <listenPort> <remoteHost> <remotePort> [opcode]|discover <remotePort> [opcode] [listenPort] [process=selector] [localPort=n]|stop]]");
            }
        }

        private static bool TryGetExpeditionCommandValue(string[] args, int startIndex, string key, out string value)
        {
            value = null;
            if (args == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string prefix = key.Trim() + "=";
            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i][prefix.Length..];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetExpeditionCommandInt(string[] args, int startIndex, string key, out int value)
        {
            value = 0;
            return TryGetExpeditionCommandValue(args, startIndex, key, out string rawValue)
                   && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetExpeditionPositionalValue(string[] args, int index, out string value)
        {
            value = null;
            return args != null
                   && index >= 0
                   && index < args.Length
                   && !args[index].Contains("=")
                   && !string.IsNullOrWhiteSpace(args[index])
                   && ((value = args[index]) != null);
        }

        private static bool TryGetExpeditionPositionalInt(string[] args, int index, out int value)
        {
            value = 0;
            return TryGetExpeditionPositionalValue(args, index, out string token)
                   && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string NormalizeExpeditionCommandText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('_', ' ');
        }

        private static bool TryParseExpeditionPartySeeds(
            string rawSpec,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel,
            out IReadOnlyList<ExpeditionPartySeed> parties,
            out string error)
        {
            parties = Array.Empty<ExpeditionPartySeed>();
            error = null;
            if (string.IsNullOrWhiteSpace(rawSpec))
            {
                return true;
            }

            Dictionary<int, List<ExpeditionMemberSeed>> groupedMembers = new();
            string[] memberSpecs = rawSpec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawMemberSpec in memberSpecs)
            {
                string[] fields = rawMemberSpec.Split('~');
                if (fields.Length < 2)
                {
                    error = "Party specs must use partyIndex~name~role~level~map~channel~online~local.";
                    return false;
                }

                if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int partyIndex))
                {
                    error = $"Invalid expedition party index in '{rawMemberSpec}'.";
                    return false;
                }

                if (!TryCreateExpeditionMemberSeed(
                        fields.Skip(1).ToArray(),
                        localPlayerName,
                        defaultLocation,
                        defaultChannel,
                        out ExpeditionMemberSeed member,
                        out error))
                {
                    return false;
                }

                if (!groupedMembers.TryGetValue(Math.Max(0, partyIndex), out List<ExpeditionMemberSeed> members))
                {
                    members = new List<ExpeditionMemberSeed>();
                    groupedMembers[Math.Max(0, partyIndex)] = members;
                }

                members.Add(member);
            }

            parties = groupedMembers
                .OrderBy(entry => entry.Key)
                .Select(entry => new ExpeditionPartySeed(entry.Key, entry.Value.ToArray()))
                .ToArray();
            return true;
        }

        private static bool TryParseExpeditionMemberSeeds(
            string rawSpec,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel,
            out IReadOnlyList<ExpeditionMemberSeed> members,
            out string error)
        {
            members = Array.Empty<ExpeditionMemberSeed>();
            error = null;
            if (string.IsNullOrWhiteSpace(rawSpec) || string.Equals(rawSpec, "clear", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            List<ExpeditionMemberSeed> parsedMembers = new();
            string[] memberSpecs = rawSpec.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawMemberSpec in memberSpecs)
            {
                if (!TryCreateExpeditionMemberSeed(
                        rawMemberSpec.Split('~'),
                        localPlayerName,
                        defaultLocation,
                        defaultChannel,
                        out ExpeditionMemberSeed member,
                        out error))
                {
                    return false;
                }

                parsedMembers.Add(member);
            }

            members = parsedMembers.ToArray();
            return true;
        }

        private static bool TryCreateExpeditionMemberSeed(
            string[] fields,
            string localPlayerName,
            string defaultLocation,
            int defaultChannel,
            out ExpeditionMemberSeed member,
            out string error)
        {
            member = default;
            error = null;
            if (fields == null || fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0]))
            {
                error = "Expedition member specs require at least a name field.";
                return false;
            }

            string name = NormalizeExpeditionCommandText(fields[0]);
            string role = fields.Length >= 2 && !string.IsNullOrWhiteSpace(fields[1])
                ? NormalizeExpeditionCommandText(fields[1])
                : "Member";
            int level = fields.Length >= 3 && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLevel)
                ? Math.Max(1, parsedLevel)
                : 1;
            string location = fields.Length >= 4 && !string.IsNullOrWhiteSpace(fields[3])
                ? NormalizeExpeditionCommandText(fields[3])
                : defaultLocation;
            int channel = fields.Length >= 5 && int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedChannel)
                ? Math.Max(1, parsedChannel)
                : Math.Max(1, defaultChannel);
            bool isOnline = fields.Length < 6 || TryParseExpeditionFlag(fields[5], true);
            bool isLocalPlayer = fields.Length >= 7
                ? TryParseExpeditionFlag(fields[6], string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase))
                : string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase);
            member = new ExpeditionMemberSeed(name, role, level, location, channel, isOnline, isLocalPlayer);
            return true;
        }

        private static bool TryParseExpeditionResponseCode(string token, out int responseCode)
        {
            responseCode = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "accept":
                case "accepted":
                    responseCode = 1;
                    return true;
                case "decline":
                case "reject":
                case "declined":
                    responseCode = 0;
                    return true;
                case "busy":
                    responseCode = 2;
                    return true;
                case "changed":
                    responseCode = 3;
                    return true;
                case "blocked":
                    responseCode = 4;
                    return true;
                case "unavailable":
                    responseCode = 5;
                    return true;
                case "fail6":
                    responseCode = 6;
                    return true;
                case "promptopen":
                case "pending":
                    responseCode = 7;
                    return true;
                default:
                    return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out responseCode);
            }
        }

        private static bool TryParseExpeditionNoticeKind(string token, out ExpeditionNoticeKind noticeKind)
        {
            noticeKind = ExpeditionNoticeKind.Joined;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "join":
                case "joined":
                    noticeKind = ExpeditionNoticeKind.Joined;
                    return true;
                case "left":
                case "leave":
                    noticeKind = ExpeditionNoticeKind.Left;
                    return true;
                case "remove":
                case "removed":
                case "kick":
                case "kicked":
                    noticeKind = ExpeditionNoticeKind.Removed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseExpeditionRemovalKind(string token, out ExpeditionRemovalKind removalKind)
        {
            removalKind = ExpeditionRemovalKind.Disband;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "leave":
                case "left":
                case "withdraw":
                    removalKind = ExpeditionRemovalKind.Leave;
                    return true;
                case "remove":
                case "removed":
                case "kick":
                case "kicked":
                    removalKind = ExpeditionRemovalKind.Removed;
                    return true;
                case "disband":
                case "clear":
                    removalKind = ExpeditionRemovalKind.Disband;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseExpeditionFlag(string token, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return defaultValue;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "on":
                case "online":
                case "local":
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                case "offline":
                case "remote":
                    return false;
                default:
                    return defaultValue;
            }
        }

        private static bool TryParsePacketOwnedItemIdList(string input, out int[] itemIds)
        {
            itemIds = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] tokens = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                return false;
            }

            itemIds = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], out int itemId) || itemId <= 0)
                {
                    itemIds = Array.Empty<int>();
                    return false;
                }

                itemIds[i] = itemId;
            }

            return true;
        }

        private string DescribePetAutoConsumeHpSettings()
        {
            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;
            if (activePets == null || activePets.Count == 0)
            {
                return "No active pets are available";
            }

            return string.Join(
                Environment.NewLine,
                activePets.Select((pet, index) => DescribePetAutoConsumeHpSetting(index, pet)));
        }

        private static string DescribePetAutoConsumeHpSetting(int petSlotIndex, PetRuntime pet)
        {
            if (pet == null)
            {
                return $"Pet {petSlotIndex + 1}: unavailable";
            }

            string petName = string.IsNullOrWhiteSpace(pet.Name) ? "Unknown" : pet.Name;
            string enabled = pet.AutoConsumeHpEnabled ? "enabled" : "disabled";
            if (pet.AutoConsumeHpItemId <= 0 || pet.AutoConsumeHpInventoryType == InventoryType.NONE)
            {
                return $"Pet {petSlotIndex + 1} ({petName}): {enabled}, no configured potion, fallback order=hotkeys -> USE/CASH inventory";
            }

            string itemName = InventoryItemMetadataResolver.TryResolveItemName(pet.AutoConsumeHpItemId, out string resolvedItemName)
                && !string.IsNullOrWhiteSpace(resolvedItemName)
                ? resolvedItemName.Trim()
                : $"Item {pet.AutoConsumeHpItemId}";
            return $"Pet {petSlotIndex + 1} ({petName}): {enabled}, configured potion={itemName} [{pet.AutoConsumeHpInventoryType}]";
        }

        private static bool TryParsePetAutoConsumeInventoryType(string value, out InventoryType inventoryType)
        {
            inventoryType = InventoryType.NONE;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "use":
                    inventoryType = InventoryType.USE;
                    return true;

                case "cash":
                    inventoryType = InventoryType.CASH;
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseOptionalPositiveInt(string[] args, int index, out int value)
        {
            value = 0;
            return args != null
                && args.Length > index
                && int.TryParse(args[index], out value)
                && value > 0;
        }

        private static bool TryParseSocialListTabToken(string token, out SocialListTab tab)
        {
            tab = SocialListTab.Friend;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "friend":
                case "friends":
                    tab = SocialListTab.Friend;
                    return true;

                case "party":
                    tab = SocialListTab.Party;
                    return true;

                case "guild":
                    tab = SocialListTab.Guild;
                    return true;

                case "alliance":
                case "union":
                    tab = SocialListTab.Alliance;
                    return true;

                case "blacklist":
                case "black":
                    tab = SocialListTab.Blacklist;
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseSocialSearchTabToken(string token, out SocialSearchTab tab)
        {
            tab = SocialSearchTab.Party;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "party":
                    tab = SocialSearchTab.Party;
                    return true;

                case "partymember":
                case "member":
                    tab = SocialSearchTab.PartyMember;
                    return true;

                case "expedition":
                case "exp":
                    tab = SocialSearchTab.Expedition;
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseGuildManageTabToken(string token, out GuildManageTab tab)
        {
            tab = GuildManageTab.Position;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "position":
                case "rank":
                    tab = GuildManageTab.Position;
                    return true;

                case "admission":
                case "admit":
                    tab = GuildManageTab.Admission;
                    return true;

                case "change":
                case "notice":
                    tab = GuildManageTab.Change;
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseAllianceEditorFocusToken(string token, out AllianceEditorFocus focus)
        {
            focus = AllianceEditorFocus.RankTitle;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "rank":
                case "ranks":
                case "title":
                case "titles":
                    focus = AllianceEditorFocus.RankTitle;
                    return true;

                case "notice":
                    focus = AllianceEditorFocus.Notice;
                    return true;

                default:
                    return false;
            }
        }

    }

}
