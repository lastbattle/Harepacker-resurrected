using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketFieldFeedbackRuntime _packetFieldFeedbackRuntime = new();

        private void UpdatePacketOwnedFieldFeedbackState(int currentTickCount)
        {
            _packetFieldFeedbackRuntime.Initialize(GraphicsDevice);
            _packetFieldFeedbackRuntime.Update(currentTickCount);
        }

        private void DrawPacketOwnedFieldFeedbackState(int currentTickCount)
        {
            _packetFieldFeedbackRuntime.Draw(_spriteBatch, _fontChat, _renderParams.RenderWidth, currentTickCount);
        }

        private bool TryApplyPacketOwnedFieldFeedbackPacket(PacketFieldFeedbackPacketKind kind, byte[] payload, out string message)
        {
            _packetFieldFeedbackRuntime.Initialize(GraphicsDevice);
            return _packetFieldFeedbackRuntime.TryApplyPacket(
                kind,
                payload,
                currTickCount,
                BuildPacketFieldFeedbackCallbacks(),
                out message);
        }

        private PacketFieldFeedbackCallbacks BuildPacketFieldFeedbackCallbacks()
        {
            return new PacketFieldFeedbackCallbacks
            {
                AddClientChatMessage = (text, chatLogType, whisperTargetCandidate) =>
                    _chat?.AddClientChatMessage(text, currTickCount, chatLogType, whisperTargetCandidate),
                ShowUtilityFeedback = ShowUtilityFeedbackMessage,
                ShowModalWarning = ShowPacketOwnedFieldWarning,
                RememberWhisperTarget = target => _chat?.AddClientChatMessage($"[System] Reply target set to {target}.", currTickCount, 12, target),
                TriggerTremble = (force, durationMs) => _screenEffects.TriggerTremble(Math.Max(1, force), false, 0, Math.Max(0, durationMs), true, currTickCount),
                ClearFieldFade = () => ClearPacketOwnedLocalOverlayState("fade"),
                RequestBgm = RequestSpecialFieldBgmOverride,
                PlayFieldSound = descriptor => TryPlayPacketOwnedFieldFeedbackSound(descriptor),
                SetObjectTagState = (tag, state, transition, currentTime) => SetDynamicObjectTagState(tag, state, transition, currentTime),
                ResolveMobName = ResolvePacketFieldFeedbackMobName,
                ResolveMapName = mapId => ResolveMapTransferDisplayName(mapId, null),
                ResolveItemName = ResolvePacketFieldFeedbackItemName,
                ResolveChannelName = ResolvePacketFieldFeedbackChannelName,
                IsBlacklistedName = name => _socialListRuntime.IsBlacklisted(name)
            };
        }

        private void ShowPacketOwnedFieldWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _chat?.AddClientChatMessage($"[System] {message}", currTickCount, 12);
            ShowConnectionNoticePrompt(new LoginPacketDialogPromptConfiguration
            {
                Owner = LoginPacketDialogOwner.ConnectionNotice,
                Title = "Warning",
                Body = message.Trim(),
                NoticeVariant = ConnectionNoticeWindowVariant.Notice,
                DurationMs = 5000
            });
        }

        private bool TryPlayPacketOwnedFieldFeedbackSound(string descriptor)
        {
            if (!TryPlayPacketOwnedWzSound(descriptor, "FieldSound", out string resolvedDescriptor, out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowUtilityFeedbackMessage(error);
                }

                return false;
            }

            ShowUtilityFeedbackMessage($"Played packet-owned field sound {resolvedDescriptor}.");
            return true;
        }

        private static string ResolvePacketFieldFeedbackMobName(int mobId)
        {
            return ResolvePacketGuideMobName(mobId);
        }

        private static string ResolvePacketFieldFeedbackItemName(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                ? itemName
                : $"Item {itemId}";
        }

        private static string ResolvePacketFieldFeedbackChannelName(int channelId)
        {
            return channelId > 0
                ? $"Ch. {channelId}"
                : string.Empty;
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_packetFieldFeedbackRuntime.DescribeStatus(currTickCount));
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetFieldFeedbackRuntime.Clear();
                return ChatCommandHandler.CommandResult.Ok(_packetFieldFeedbackRuntime.DescribeStatus(currTickCount));
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedFieldFeedbackPacketCommand(
                    args,
                    rawHex: string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase));
            }

            return args[0].ToLowerInvariant() switch
            {
                "group" => HandlePacketOwnedFieldFeedbackGroupCommand(args),
                "whisperin" => HandlePacketOwnedFieldFeedbackWhisperIncomingCommand(args),
                "whisperresult" => HandlePacketOwnedFieldFeedbackWhisperResultCommand(args),
                "whisperavailability" => HandlePacketOwnedFieldFeedbackWhisperAvailabilityCommand(args),
                "whisperfind" => HandlePacketOwnedFieldFeedbackWhisperFindCommand(args),
                "couplechat" => HandlePacketOwnedFieldFeedbackCoupleChatCommand(args),
                "couplenotice" => HandlePacketOwnedFieldFeedbackCoupleNoticeCommand(args),
                "warn" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.WarnMessage, PacketFieldFeedbackRuntime.BuildWarnMessagePayload(string.Join(" ", args.Skip(1)))),
                "obstacle" => HandlePacketOwnedFieldFeedbackObstacleCommand(args),
                "obstaclereset" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldObstacleAllReset, Array.Empty<byte>()),
                "bosshp" => HandlePacketOwnedFieldFeedbackBossHpCommand(args),
                "tremble" => HandlePacketOwnedFieldFeedbackTrembleCommand(args),
                "fieldsound" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldEffect, PacketFieldFeedbackRuntime.BuildSoundFieldEffectPayload(string.Join(" ", args.Skip(1)))),
                "fieldbgm" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldEffect, PacketFieldFeedbackRuntime.BuildBgmFieldEffectPayload(string.Join(" ", args.Skip(1)))),
                "jukebox" => HandlePacketOwnedFieldFeedbackJukeboxCommand(args),
                "transferfieldignored" => HandlePacketOwnedFieldFeedbackTransferReasonCommand(args, PacketFieldFeedbackPacketKind.TransferFieldReqIgnored),
                "transferchannelignored" => HandlePacketOwnedFieldFeedbackTransferReasonCommand(args, PacketFieldFeedbackPacketKind.TransferChannelReqIgnored),
                "summonunavailable" => HandlePacketOwnedFieldFeedbackSummonUnavailableCommand(args),
                "destroyclock" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.DestroyClock, Array.Empty<byte>()),
                "zakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ZakumTimer),
                "hontailtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontailTimer),
                "chaoszakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ChaosZakumTimer),
                "hontaletimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontaleTimer),
                "fadeoutforce" => HandlePacketOwnedFieldFeedbackFadeOutForceCommand(args),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback [status|clear|group <family> <sender> <text>|whisperin <sender> <channel> <text>|whisperresult <target> <ok|fail>|whisperavailability <target> <0|1>|whisperfind <find|findreply> <target> <result> <value>|couplechat <sender> <text>|couplenotice [text]|warn <text>|obstacle <tag> <state>|obstaclereset|bosshp <mobId> <currentHp> <maxHp> [color] [phase]|tremble <force> <durationMs>|fieldsound <descriptor>|fieldbgm <descriptor>|jukebox <itemId> <owner>|transferfieldignored <reason>|transferchannelignored <reason>|summonunavailable [0|1]|destroyclock|zakumtimer <mode> <value>|hontailtimer <mode> <value>|chaoszakumtimer <mode> <value>|hontaletimer <mode> <value>|fadeoutforce [key]|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]"),
            };
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 2 || !TryParsePacketFieldFeedbackKind(args[1], out PacketFieldFeedbackPacketKind kind))
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /fieldfeedback packetraw <kind> <hex>"
                        : "Usage: /fieldfeedback packet <kind> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback packetraw <kind> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /fieldfeedback packet <kind> [payloadhex=..|payloadb64=..]");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, payload);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackGroupCommand(string[] args)
        {
            if (args.Length < 4 || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte family))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback group <family> <sender> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.GroupMessage,
                PacketFieldFeedbackRuntime.BuildGroupMessagePayload(family, args[2], string.Join(" ", args.Skip(3))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperIncomingCommand(string[] args)
        {
            if (args.Length < 4 || !byte.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte channelId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperin <sender> <channel> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload(args[1], channelId, fromAdmin: false, string.Join(" ", args.Skip(3))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperResultCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperresult <target> <ok|fail>");
            }

            bool success = args[2].Equals("ok", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("success", StringComparison.OrdinalIgnoreCase)
                || args[2] == "1";
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildWhisperResultPayload(args[1], success));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperAvailabilityCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperavailability <target> <0|1>");
            }

            bool available = args[2].Equals("1", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("true", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("yes", StringComparison.OrdinalIgnoreCase);
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildWhisperAvailabilityPayload(args[1], available));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperFindCommand(string[] args)
        {
            if (args.Length < 5
                || !byte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result)
                || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperfind <find|findreply> <target> <result> <value>");
            }

            byte subtype = args[1].Equals("findreply", StringComparison.OrdinalIgnoreCase) ? (byte)72 : (byte)9;
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(subtype, args[2], result, value));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCoupleChatCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback couplechat <sender> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.CoupleMessage,
                PacketFieldFeedbackRuntime.BuildCoupleChatPayload(args[1], string.Join(" ", args.Skip(2))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCoupleNoticeCommand(string[] args)
        {
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.CoupleMessage,
                PacketFieldFeedbackRuntime.BuildCoupleNoticePayload(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackObstacleCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback obstacle <tag> <state>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldObstacleOnOff,
                PacketFieldFeedbackRuntime.BuildObstaclePayload(args[1], state));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackBossHpCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mobId)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentHp)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxHp))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback bosshp <mobId> <currentHp> <maxHp> [color] [phase]");
            }

            byte color = 1;
            byte phase = 0;
            if (args.Length >= 5)
            {
                byte.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out color);
            }

            if (args.Length >= 6)
            {
                byte.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out phase);
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldEffect,
                PacketFieldFeedbackRuntime.BuildBossHpFieldEffectPayload(mobId, currentHp, maxHp, color, phase));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackTrembleCommand(string[] args)
        {
            if (args.Length < 3
                || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte force)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int durationMs))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback tremble <force> <durationMs>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldEffect,
                PacketFieldFeedbackRuntime.BuildTrembleFieldEffectPayload(force, durationMs));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackJukeboxCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback jukebox <itemId> <owner>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.PlayJukeBox,
                PacketFieldFeedbackRuntime.BuildJukeBoxPayload(itemId, string.Join(" ", args.Skip(2))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackTransferReasonCommand(string[] args, PacketFieldFeedbackPacketKind kind)
        {
            if (args.Length < 2 || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte reason))
            {
                return ChatCommandHandler.CommandResult.Error($"Usage: /fieldfeedback {(kind == PacketFieldFeedbackPacketKind.TransferFieldReqIgnored ? "transferfieldignored" : "transferchannelignored")} <reason>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, new[] { reason });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackSummonUnavailableCommand(string[] args)
        {
            byte blocked = 0;
            if (args.Length >= 2)
            {
                byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out blocked);
            }

            return ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.SummonItemUnavailable, new[] { blocked });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackBossTimerCommand(string[] args, PacketFieldFeedbackPacketKind kind)
        {
            if (args.Length < 3
                || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte mode)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return ChatCommandHandler.CommandResult.Error($"Usage: /fieldfeedback {args[0]} <mode> <value>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, PacketFieldFeedbackRuntime.BuildBossTimerPayload(mode, value));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackFadeOutForceCommand(string[] args)
        {
            int fadeKey = 0;
            if (args.Length >= 2 && !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out fadeKey))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback fadeoutforce [key]");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldFadeOutForce,
                PacketFieldFeedbackRuntime.BuildFadeOutForcePayload(fadeKey));
        }

        private ChatCommandHandler.CommandResult ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind kind, byte[] payload)
        {
            return TryApplyPacketOwnedFieldFeedbackPacket(kind, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private static bool TryParsePacketFieldFeedbackKind(string value, out PacketFieldFeedbackPacketKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "group" or "groupmessage" or "150" => Assign(PacketFieldFeedbackPacketKind.GroupMessage, out kind),
                "whisper" or "151" => Assign(PacketFieldFeedbackPacketKind.Whisper, out kind),
                "couple" or "couplemessage" or "152" => Assign(PacketFieldFeedbackPacketKind.CoupleMessage, out kind),
                "fieldeffect" or "154" => Assign(PacketFieldFeedbackPacketKind.FieldEffect, out kind),
                "obstacle" or "fieldobstacleonoff" or "155" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleOnOff, out kind),
                "obstaclestatus" or "fieldobstacleonoffstatus" or "156" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleOnOffStatus, out kind),
                "warn" or "warnmessage" or "157" => Assign(PacketFieldFeedbackPacketKind.WarnMessage, out kind),
                "jukebox" or "playjukebox" or "158" => Assign(PacketFieldFeedbackPacketKind.PlayJukeBox, out kind),
                "obstaclereset" or "fieldobstacleallreset" or "159" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleAllReset, out kind),
                "transferfieldignored" or "160" => Assign(PacketFieldFeedbackPacketKind.TransferFieldReqIgnored, out kind),
                "transferchannelignored" or "161" => Assign(PacketFieldFeedbackPacketKind.TransferChannelReqIgnored, out kind),
                "destroyclock" or "163" => Assign(PacketFieldFeedbackPacketKind.DestroyClock, out kind),
                "summonunavailable" or "summonitemunavailable" or "164" => Assign(PacketFieldFeedbackPacketKind.SummonItemUnavailable, out kind),
                "zakumtimer" => Assign(PacketFieldFeedbackPacketKind.ZakumTimer, out kind),
                "hontailtimer" or "horntailtimer" => Assign(PacketFieldFeedbackPacketKind.HontailTimer, out kind),
                "chaoszakumtimer" => Assign(PacketFieldFeedbackPacketKind.ChaosZakumTimer, out kind),
                "hontaletimer" => Assign(PacketFieldFeedbackPacketKind.HontaleTimer, out kind),
                "fadeoutforce" or "fieldfadeoutforce" => Assign(PacketFieldFeedbackPacketKind.FieldFadeOutForce, out kind),
                _ => false
            };
        }

        private static bool Assign(PacketFieldFeedbackPacketKind value, out PacketFieldFeedbackPacketKind kind)
        {
            kind = value;
            return true;
        }
    }
}
