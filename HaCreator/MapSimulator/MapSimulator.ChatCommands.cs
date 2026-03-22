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
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Combat;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator : Microsoft.Xna.Framework.Game
    {
        private bool TryConfigureLoginPacketPayload(
            LoginPacketType packetType,
            string[] args,
            out string error,
            out string summary)
        {
            error = null;
            summary = null;

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
                        if (!TryParseRecommendWorldMessageEntry(args, ref i, out int worldId, out string message))
                        {
                            error = "Usage: /loginpacket recommendworld <worldId[=message] ...>";
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

                    if (!int.TryParse(args[0], out int latestWorldId) || latestWorldId < 0)
                    {
                        error = "Usage: /loginpacket latestworld <worldId>";
                        return false;
                    }

                    _loginPacketLatestConnectedWorldId = latestWorldId;
                    summary = $"Packet-authored latest connected world: {latestWorldId}.";
                    return true;

                case LoginPacketType.CheckUserLimitResult:
                    byte checkUserLimitResultCode = 0;
                    byte populationLevel = 0;
                    if (args.Length > 0 && !byte.TryParse(args[0], out checkUserLimitResultCode))
                    {
                        error = "Usage: /loginpacket checkuserlimit [resultCode] [populationLevel]";
                        return false;
                    }

                    if (args.Length > 1 && !byte.TryParse(args[1], out populationLevel))
                    {
                        error = "Usage: /loginpacket checkuserlimit [resultCode] [populationLevel]";
                        return false;
                    }

                    _loginPacketCheckUserLimitResultCode = args.Length > 0 ? checkUserLimitResultCode : null;
                    _loginPacketCheckUserLimitPopulationLevel = args.Length > 1 ? populationLevel : null;
                    summary = args.Length == 0
                        ? "Using generated CheckUserLimitResult behavior."
                        : $"Configured CheckUserLimitResult code {_loginPacketCheckUserLimitResultCode}"
                          + (_loginPacketCheckUserLimitPopulationLevel.HasValue ? $" with population level {_loginPacketCheckUserLimitPopulationLevel.Value}." : ".");
                    return true;

                case LoginPacketType.AccountInfoResult:
                case LoginPacketType.SetAccountResult:
                case LoginPacketType.ConfirmEulaResult:
                case LoginPacketType.CheckPinCodeResult:
                case LoginPacketType.UpdatePinCodeResult:
                case LoginPacketType.EnableSpwResult:
                case LoginPacketType.CheckSpwResult:
                case LoginPacketType.CreateNewCharacterResult:
                case LoginPacketType.DeleteCharacterResult:
                    return TryConfigureLoginPacketDialogPrompt(packetType, args, out error, out summary);

                case LoginPacketType.SelectWorldResult:
                    return TryConfigureSelectWorldPacketPayload(args, out error, out summary);

                case LoginPacketType.ViewAllCharResult:
                case LoginPacketType.SelectCharacterByVacResult:
                    return TryConfigureViewAllCharPacketPayload(args, out error, out summary);

                case LoginPacketType.ExtraCharInfoResult:
                    return TryConfigureExtraCharInfoPacketPayload(args, out error, out summary);
            }

            return true;
        }


        private static bool TryParseLoginPacketDialogPrompt(
            string[] args,
            out LoginPacketDialogPromptConfiguration prompt,
            out string error)
        {
            prompt = null;
            error = null;

            LoginPacketDialogOwner owner = LoginPacketDialogOwner.LoginUtilityDialog;
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
            int durationMs = 2400;

            for (int i = 0; i < args.Length; i++)
            {
                if (!TrySplitLoginPacketPromptArgument(args[i], out string key, out string initialValue))
                {
                    error = "Usage: /loginpacket <packet> [mode=utility|notice] [title=<text>] [body=<text>] [notice=<index>] [variant=notice|noticecog|loading|loadingsinglegauge] [buttons=ok|yesno|accept|nowlater|restartexit] [primary=<label>] [secondary=<label>] [inputlabel=<text>] [placeholder=<text>] [masked=true|false] [maxlength=<count>] [duration=<ms>]";
                    return false;
                }

                string value = CollectLoginPacketPromptValue(args, ref i, key, initialValue);
                switch (key)
                {
                    case "mode":
                        if (value.Equals("notice", StringComparison.OrdinalIgnoreCase))
                        {
                            owner = LoginPacketDialogOwner.ConnectionNotice;
                        }
                        else if (value.Equals("utility", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("dialog", StringComparison.OrdinalIgnoreCase))
                        {
                            owner = LoginPacketDialogOwner.LoginUtilityDialog;
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
                            error = "buttons must be ok, yesno, accept, nowlater, or restartexit.";
                            return false;
                        }

                        buttonLayout = parsedButtonLayout;
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
                Title = title,
                Body = body,
                NoticeTextIndex = noticeTextIndex,
                NoticeVariant = noticeVariant,
                ButtonLayout = buttonLayout,
                PrimaryLabel = primaryLabel,
                SecondaryLabel = secondaryLabel,
                InputLabel = inputLabel,
                InputPlaceholder = inputPlaceholder,
                InputMasked = inputMasked,
                InputMaxLength = inputMaxLength,
                DurationMs = durationMs,
            };
            return true;
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
                "/loginpacket <checkpassword|guestlogin|accountinfo|checkuserlimit|setaccount|eula|checkpin|updatepin|worldinfo|selectworld|selectchar|newcharresult|deletechar|enablespw|vac|recommendworld|latestworld|extracharinfo|checkspw> [...]",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }

                    if (args.Length == 0 || !LoginRuntimeManager.TryParsePacketType(args[0], out LoginPacketType packetType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket <checkpassword|guestlogin|accountinfo|checkuserlimit|setaccount|eula|checkpin|updatepin|worldinfo|selectworld|selectchar|newcharresult|deletechar|enablespw|vac|recommendworld|latestworld|extracharinfo|checkspw> [...]");
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
                "/wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y>|end]",
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
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (string.Equals(args[1], "bride", StringComparison.OrdinalIgnoreCase))
                        {
                            if (wedding.BrideId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding bride ID is not set yet. Use /wedding progress first.");
                            }

                            wedding.SetParticipantPosition(wedding.BrideId, new Vector2(actorX, actorY));
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        return ChatCommandHandler.CommandResult.Error("Wedding actor must be groom or bride");
                    }

                    if (string.Equals(args[0], "end", StringComparison.OrdinalIgnoreCase))
                    {
                        wedding.OnWeddingCeremonyEnd(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y>|end]");
                });

            _chat.CommandHandler.RegisterCommand(
                "guildboss",
                "Inspect or update guild boss healer and pulley state",
                "/guildboss [status|transport [status|start [port]|stop]|healer <y>|pulley <state>|packet <344|345> <value>|packetraw <hex>]",
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
                            $"{guildBoss.DescribeStatus()}{Environment.NewLine}{_guildBossTransport.LastStatus}");
                    }

                    if (string.Equals(args[0], "transport", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{guildBoss.DescribeStatus()}{Environment.NewLine}{_guildBossTransport.LastStatus}");
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

                    return ChatCommandHandler.CommandResult.Error("Usage: /guildboss [status|transport [status|start [port]|stop]|healer <y>|pulley <state>|packet <344|345> <value>|packetraw <hex>]");
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
                "/coconut [status|clock <seconds>|hit <target|-1> <delay> <state>|score <maple> <story>|raw <type> <hex>|inbox [status|start [port]|stop]|request [peek|clear]]",
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
                            $"{field.DescribeStatus()}{Environment.NewLine}{_coconutPacketInbox.LastStatus}");
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
                            if (args.Length < 3 || !int.TryParse(args[1], out int packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /coconut raw <type> <hex>");
                            }

                            byte[] payload = ByteUtils.HexToBytes(string.Join(string.Empty, args.Skip(2)));
                            if (!field.TryApplyPacket(packetType, payload, currTickCount, out string packetError))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetError ?? "Failed to apply Coconut packet.");
                            }

                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "inbox":
                            if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_coconutPacketInbox.LastStatus}");
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
                            return ChatCommandHandler.CommandResult.Error("Usage: /coconut [status|clock <seconds>|hit <target|-1> <delay> <state>|score <maple> <story>|raw <type> <hex>|inbox [status|start [port]|stop]|request [peek|clear]]");
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
                "/mcarnival [status|tab <mob|skill|guardian>|enter <team> <personalCP> <personalTotal> <myCP> <myTotal> <enemyCP> <enemyTotal>|cp <personalCP> <personalTotal> <team0CP> <team0Total> <team1CP> <team1Total>|cpdelta <personalDelta> <personalTotalDelta> <team0Delta> <team0TotalDelta> <team1Delta> <team1TotalDelta>|request <index> [message]|requestok <mob|skill|guardian> <index> [message]|requestfail <reason>|result <code>|death <team> <name> <remainingRevives>|spells <mobIndex> <count>|raw <type> <hex>|inbox [status|start [port]|stop]]",
                args =>
                {
                    MonsterCarnivalField field = _specialFieldRuntime.Minigames.MonsterCarnival;
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
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
                                    $"{field.DescribeStatus()}{Environment.NewLine}{_monsterCarnivalPacketInbox.LastStatus}");
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

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival [status|tab|enter|cp|cpdelta|request|requestok|requestfail|result|death|spells|raw|inbox] [...]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "dojo",
                "Inspect the Mu Lung Dojo HUD state or its loopback inbox",
                "/dojo [status|inbox [status|start [port]|stop]]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoPacketInbox.LastStatus}");
                    }

                    if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                        {
                            return ChatCommandHandler.CommandResult.Info(
                                $"{_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus()}{Environment.NewLine}{_dojoPacketInbox.LastStatus}");
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

                    return ChatCommandHandler.CommandResult.Error("Usage: /dojo [status|inbox [status|start [port]|stop]]");
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
                "/massacre [status|clock <seconds>|kill [gauge]|inc <value>|params <maxGauge> <decayPerSec>|bonus|result <clear|fail> [score] [rank]|reset]",
                args =>
                {
                    MassacreField massacre = _specialFieldRuntime.SpecialEffects.Massacre;
                    if (!massacre.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Massacre HUD is only active on Massacre maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(massacre.DescribeStatus());
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

                    return ChatCommandHandler.CommandResult.Error("Usage: /massacre [status|clock <seconds>|kill [gauge]|inc <value>|params <maxGauge> <decayPerSec>|bonus|result <clear|fail> [score] [rank]|reset]");
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
                "/guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>|title <text>|body <text>|replytext <text>|threadpage <prev|next>|commentpage <prev|next>]",
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
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>|title <text>|body <text>|replytext <text>|threadpage <prev|next>|commentpage <prev|next>]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "memorygame",
                "Drive the MiniRoom Match Cards runtime",
                "/memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetrecv|remote|inbox> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetrecv|remote|inbox> [...]");
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
                            ShowMiniRoomWindow();
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
                            if (!field.TryEndRoom(out string endMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(endMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(endMessage);
                        }
                        case "status":
                            return ChatCommandHandler.CommandResult.Info($"{field.DescribeStatus()} | inbox={_memoryGamePacketInbox.LastStatus}");
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
                            return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|packet|packetraw|packetrecv|remote|inbox> [...]");
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
                                    ShowSocialRoomWindow(kind);
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
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom miniroom [packet] <open|status|ready|start|mode|wager <meso>|settle <owner|guest|draw>>");
                            }

                        case SocialRoomKind.PersonalShop:
                            switch (action)
                            {
                                case "open":
                                    ShowSocialRoomWindow(kind);
                                    return ChatCommandHandler.CommandResult.Ok("Personal-shop window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
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
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom personalshop [packet] <open|status|visit [name]|blacklist [name]|list <itemId> [qty] [price]|autolist|buy [index] [buyer]|arrange|claim|close>");
                            }

                        case SocialRoomKind.EntrustedShop:
                            switch (action)
                            {
                                case "open":
                                    ShowSocialRoomWindow(kind);
                                    return ChatCommandHandler.CommandResult.Ok("Entrusted-shop window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
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
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom entrustedshop [packet] <open|status|mode|list <itemId> [qty] [price]|autolist|arrange|claim|permit [minutes|expire]>");
                            }

                        case SocialRoomKind.TradingRoom:
                            switch (action)
                            {
                                case "open":
                                    ShowSocialRoomWindow(kind);
                                    return ChatCommandHandler.CommandResult.Ok("Trading-room window opened.");
                                case "status":
                                    return ChatCommandHandler.CommandResult.Info(runtime.DescribeStatus());
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
                                case "complete":
                                    return Dispatch(SocialRoomPacketType.CompleteTrade, out string completeMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(completeMessage)
                                        : ChatCommandHandler.CommandResult.Error(completeMessage);
                                case "reset":
                                    return Dispatch(SocialRoomPacketType.ResetTrade, out string resetMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(resetMessage)
                                        : ChatCommandHandler.CommandResult.Error(resetMessage);
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /socialroom tradingroom [packet] <open|status|offeritem <itemId> [qty]|offermeso <amount>|lock|accept|remoteofferitem <itemId> [qty]|remoteoffermeso <amount>|remotelock|remoteaccept|complete|reset>");
                            }
                    }

                    return ChatCommandHandler.CommandResult.Error("Unsupported social-room request.");
                });

            _chat.CommandHandler.RegisterCommand(
                "memo",
                "Drive simulator memo inbox, compose, and package-claim flows",
                "/memo [status|open|compose|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]]",
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
                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok("Opened the memo inbox.");
                        case "compose":
                            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MemoSend);
                            return ChatCommandHandler.CommandResult.Ok("Opened the memo send dialog. Use /memo draft ... to edit the current draft.");
                        case "send":
                            if (_memoMailbox.TrySendDraft(out string sendMessage))
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

                            _memoMailbox.DeliverMemo(sender, subject, body, DateTimeOffset.Now, false, attachmentItemId, attachmentQuantity, attachmentMeso);
                            return ChatCommandHandler.CommandResult.Ok($"Delivered memo '{subject}' from {sender}.");
                        }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /memo [status|open|compose|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "family",
                "Drive the family chart UI and packet-shaped family roster synchronization",
                "/family [open|tree|status|reset|select <memberId>|packet <clear|seed|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>]",
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
                                return ChatCommandHandler.CommandResult.Error("Usage: /family packet <clear|seed|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>");
                            }

                            switch (args[1].ToLowerInvariant())
                            {
                                case "clear":
                                    return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.ClearRosterFromPacket());
                                case "seed":
                                    return ChatCommandHandler.CommandResult.Ok(_familyChartRuntime.ResetToSeedFamily());
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
                                    return ChatCommandHandler.CommandResult.Error("Usage: /family packet <clear|seed|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>");
                            }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /family [open|tree|status|reset|select <memberId>|packet <clear|seed|remove <memberId>|upsert <memberId> <parentId|root> <level> <online|offline> <currentRep> <todayRep> <name>|<job>|<location>>]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "messenger",
                "Drive Messenger state, invite, claim, and remote social lifecycle flows",
                "/messenger [open|status|invite [name]|claim|leave|state <max|min|min2|next|prev>|presence <name> <online|offline>|remote <accept|reject|leave|room|whisper> ...]",
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
                        case "remote":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote <accept|reject|leave|room|whisper> ...");
                            }

                            switch (args[1].ToLowerInvariant())
                            {
                                case "accept":
                                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvite(true, packetDriven: true));
                                case "reject":
                                    if (args.Length >= 3)
                                    {
                                        return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: true));
                                    }

                                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ResolvePendingInvite(false, packetDriven: true));
                                case "leave":
                                    if (args.Length < 3)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote leave <name>");
                                    }

                                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.RemoveParticipant(string.Join(" ", args.Skip(2)), rejectedInvite: false));
                                case "room":
                                    if (args.Length < 4)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote room <name> <message>");
                                    }

                                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRoomMessage(args[2], string.Join(" ", args.Skip(3))));
                                case "whisper":
                                    if (args.Length < 4)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote whisper <name> <message>");
                                    }

                                    return ChatCommandHandler.CommandResult.Ok(_messengerRuntime.ReceiveRemoteWhisper(args[2], string.Join(" ", args.Skip(3))));
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /messenger remote <accept|reject|leave|room|whisper> ...");
                            }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /messenger [open|status|invite [name]|claim|leave|state <max|min|min2|next|prev>|presence <name> <online|offline>|remote <accept|reject|leave|room|whisper> ...]");
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
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv result <success|busy|offline|fail>");
                            }

                            MapleTvSendResultKind? resultKind = args[1].ToLowerInvariant() switch
                            {
                                "success" => MapleTvSendResultKind.Success,
                                "busy" => MapleTvSendResultKind.Busy,
                                "offline" => MapleTvSendResultKind.RecipientOffline,
                                "fail" => MapleTvSendResultKind.Failed,
                                "failed" => MapleTvSendResultKind.Failed,
                                _ => null
                            };

                            if (resultKind == null)
                            {
                                return ChatCommandHandler.CommandResult.Error("Result must be one of: success, busy, offline, fail");
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
                                    return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.ApplyClearMessagePacket());

                                case "result":
                                    byte[] resultPayload = null;
                                    string resultPayloadError = null;
                                    if (args.Length < 3 || !TryParseBinaryPayloadArgument(args[2], out resultPayload, out resultPayloadError))
                                    {
                                        return ChatCommandHandler.CommandResult.Error(resultPayloadError ?? "Usage: /mapletv packet result <payloadhex=..|payloadb64=..>");
                                    }

                                    return _mapleTvRuntime.TryApplySendMessageResultPacket(resultPayload, out string resultPacketMessage)
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
                                    return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.ApplyClearMessagePacket());

                                case "result":
                                    if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out byte[] rawResultPayload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /mapletv packetraw result <hex bytes>");
                                    }

                                    return _mapleTvRuntime.TryApplySendMessageResultPacket(rawResultPayload, out string rawResultMessage)
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
                "/chair <itemId|clear>",
                args =>
                {
                    if (_playerManager?.Player == null || _playerManager.Loader == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Player runtime is not available");
                    }

                    if (args.Length == 0)
                    {
                        PortableChair activeChair = _playerManager.Player.Build?.ActivePortableChair;
                        return activeChair != null
                            ? ChatCommandHandler.CommandResult.Info($"Active chair: {activeChair.Name} ({activeChair.ItemId})")
                            : ChatCommandHandler.CommandResult.Info("No portable chair is active");
                    }

                    if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Player.ClearPortableChair();
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
                "petlevel",
                "Inspect or set the simulated pet command level for WZ command gating",
                "/petlevel [slot 1-3] [level 1-30]",
                args =>
                {
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
    }
}
