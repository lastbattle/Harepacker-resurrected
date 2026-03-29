using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using MapleLib.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private void RegisterRemoteUserChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "remoteuser",
                "Create or mutate shared remote user actors",
                "/remoteuser <status|clear|clone|avatar|move|action|chair|mount|helper|team|follow|prepare|preparedclear|visible|inspect|remove|packet|packetraw> ...",
                args => HandleRemoteUserCommand(args, currTickCount));
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserCommand(string[] args, int currentTime)
        {
            if (args == null || args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser <status|clear|clone|avatar|move|action|chair|mount|helper|team|follow|prepare|preparedclear|visible|inspect|remove|packet|packetraw> ...");
            }

            return args[0].ToLowerInvariant() switch
            {
                "status" => ChatCommandHandler.CommandResult.Info(_remoteUserPool.DescribeStatus()),
                "clear" => HandleRemoteUserClearCommand(),
                "clone" => HandleRemoteUserCloneCommand(args),
                "avatar" => HandleRemoteUserAvatarCommand(args),
                "move" => HandleRemoteUserMoveCommand(args),
                "action" => HandleRemoteUserActionCommand(args),
                "chair" => HandleRemoteUserChairCommand(args),
                "mount" => HandleRemoteUserMountCommand(args),
                "helper" => HandleRemoteUserHelperCommand(args),
                "team" => HandleRemoteUserTeamCommand(args, currentTime),
                "follow" => HandleRemoteUserFollowCommand(args),
                "prepare" => HandleRemoteUserPrepareCommand(args, currentTime),
                "preparedclear" => HandleRemoteUserPreparedClearCommand(args),
                "visible" => HandleRemoteUserVisibleCommand(args),
                "inspect" => HandleRemoteUserInspectCommand(args),
                "remove" => HandleRemoteUserRemoveCommand(args),
                "packet" => HandleRemoteUserPacketCommand(args, currentTime),
                "packetraw" => HandleRemoteUserPacketRawCommand(args, currentTime),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /remoteuser <status|clear|clone|avatar|move|action|chair|mount|helper|team|follow|prepare|preparedclear|visible|inspect|remove|packet|packetraw> ...")
            };
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserInspectCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser inspect <characterId|name>");
            }

            return TryShowRemoteCharacterInfoWindow(args[1])
                ? ChatCommandHandler.CommandResult.Ok($"Opened Character Info for remote target {args[1]}.")
                : ChatCommandHandler.CommandResult.Error($"Remote user {args[1]} was not found.");
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserClearCommand()
        {
            foreach (RemoteUserActor actor in _remoteUserPool.Actors.ToArray())
            {
                _summonedPool.RemoveOwnerSummons(actor.CharacterId, currTickCount);
            }

            _remoteUserPool.Clear();
            return ChatCommandHandler.CommandResult.Ok("Remote user pool cleared.");
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserCloneCommand(string[] args)
        {
            if (args.Length < 5
                || !int.TryParse(args[1], out int characterId)
                || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser clone <characterId> <name> <x> <y> [action] [left|right]");
            }

            CharacterBuild build = _playerManager?.Player?.Build?.Clone();
            if (build == null)
            {
                return ChatCommandHandler.CommandResult.Error("No local player build is available to clone for the remote user.");
            }

            build.Id = characterId;
            build.Name = args[2];
            string actionName = args.Length >= 6 ? args[5] : null;
            bool facingRight = args.Length < 7 || !string.Equals(args[6], "left", StringComparison.OrdinalIgnoreCase);
            return _remoteUserPool.TryAddOrUpdate(characterId, build, new Vector2(x, y), out string message, facingRight, actionName, "chat", true)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {build.Name} ({characterId}) inserted at ({x:0},{y:0}).")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserAvatarCommand(string[] args)
        {
            if (args.Length < 6
                || !int.TryParse(args[1], out int characterId)
                || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser avatar <characterId> <name> <x> <y> <avatarLookHex> [action] [left|right]");
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

            CharacterBuild template = _playerManager?.Player?.Build?.Clone();
            string actionName = args.Length >= 7 ? args[6] : null;
            bool facingRight = args.Length < 8 || !string.Equals(args[7], "left", StringComparison.OrdinalIgnoreCase);
            return _remoteUserPool.TryAddOrUpdateAvatarLook(characterId, args[2], avatarLook, template, new Vector2(x, y), out string message, facingRight, actionName, "chat", true)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {args[2]} ({characterId}) spawned from AvatarLook at ({x:0},{y:0}).")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserMoveCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], out int characterId)
                || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser move <characterId> <x> <y> [action] [left|right]");
            }

            string actionName = args.Length >= 5 ? args[4] : null;
            bool? facingRight = args.Length >= 6
                ? !string.Equals(args[5], "left", StringComparison.OrdinalIgnoreCase)
                : null;
            return _remoteUserPool.TryMove(characterId, new Vector2(x, y), facingRight, actionName, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} moved to ({x:0},{y:0}).")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserActionCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser action <characterId> <action> [left|right]");
            }

            bool? facingRight = args.Length >= 4
                ? !string.Equals(args[3], "left", StringComparison.OrdinalIgnoreCase)
                : null;
            return _remoteUserPool.TrySetAction(characterId, args[2], facingRight, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} action set to {args[2]}.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserChairCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser chair <characterId> <itemId|clear>");
            }

            int? itemId = string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                ? null
                : int.TryParse(args[2], out int parsedChairId) ? parsedChairId : null;
            if (!string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase) && !itemId.HasValue)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid chair item ID: {args[2]}");
            }

            return _remoteUserPool.TrySetPortableChair(characterId, itemId, out string message)
                ? ChatCommandHandler.CommandResult.Ok(itemId.HasValue
                    ? $"Remote user {characterId} chair set to {itemId.Value}."
                    : $"Remote user {characterId} chair cleared.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserMountCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser mount <characterId> <itemId|clear>");
            }

            int? itemId = string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                ? null
                : int.TryParse(args[2], out int parsedMountId) ? parsedMountId : null;
            if (!string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase) && !itemId.HasValue)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid taming mob item ID: {args[2]}");
            }

            return _remoteUserPool.TrySetMount(characterId, itemId, out string message)
                ? ChatCommandHandler.CommandResult.Ok(itemId.HasValue
                    ? $"Remote user {characterId} mount set to {itemId.Value}."
                    : $"Remote user {characterId} mount cleared.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserHelperCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser helper <characterId> <marker|clear> [dir=true|false]");
            }

            if (!TryParseRemoteUserHelperMarker(args[2], out MinimapUI.HelperMarkerType? markerType))
            {
                return ChatCommandHandler.CommandResult.Error("Helper marker must be party, partymaster, guild, guildmaster, friend, another, match, usertrader, anothertrader, or clear.");
            }

            bool showDirectionOverlay = true;
            if (args.Length >= 4 && args[3].StartsWith("dir=", StringComparison.OrdinalIgnoreCase))
            {
                showDirectionOverlay = !string.Equals(args[3][4..], "false", StringComparison.OrdinalIgnoreCase);
            }

            return _remoteUserPool.TrySetHelperMarker(characterId, markerType, showDirectionOverlay, out string message)
                ? ChatCommandHandler.CommandResult.Ok(markerType.HasValue
                    ? $"Remote user {characterId} helper marker set to {markerType.Value}."
                    : $"Remote user {characterId} helper marker cleared.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserTeamCommand(string[] args, int currentTime)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser team <characterId> <teamId|clear>");
            }

            int? teamId = string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase)
                ? null
                : int.TryParse(args[2], out int parsedTeamId) ? parsedTeamId : null;
            if (!string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase) && !teamId.HasValue)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid team ID: {args[2]}");
            }

            BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;
            if (battlefield.IsActive)
            {
                battlefield.OnTeamChanged(characterId, teamId ?? -1, currentTime);
            }

            return _remoteUserPool.TrySetBattlefieldTeam(characterId, teamId, out string message)
                ? ChatCommandHandler.CommandResult.Ok(teamId.HasValue
                    ? $"Remote user {characterId} Battlefield team set to {teamId.Value}."
                    : $"Remote user {characterId} Battlefield team cleared.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserPrepareCommand(string[] args, int currentTime)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], out int characterId)
                || !int.TryParse(args[2], out int skillId)
                || !int.TryParse(args[3], out int durationMs))
            {
                return ChatCommandHandler.CommandResult.Error(
                    "Usage: /remoteuser prepare <characterId> <skillId> <durationMs> [skinKey] [skillName] [gauge=<ms>] [hold=<ms>] [state=prepare|holding|auto]");
            }

            PreparedSkillHudRules.PreparedSkillHudProfile hudProfile = PreparedSkillHudRules.ResolveProfile(skillId);
            string skinKey = null;
            List<string> skillNameParts = null;
            int gaugeDurationMs = hudProfile.GaugeDurationMs > 0 ? hudProfile.GaugeDurationMs : durationMs;
            int holdDurationMs = 0;
            string stateToken = null;

            for (int index = 4; index < args.Length; index++)
            {
                string token = args[index];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (TryParsePreparedSkillCommandOption(token, out string optionName, out string optionValue))
                {
                    switch (optionName)
                    {
                        case "gauge":
                            if (!int.TryParse(optionValue, out gaugeDurationMs))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid prepared-skill gauge duration: {optionValue}");
                            }

                            break;

                        case "hold":
                            if (!int.TryParse(optionValue, out holdDurationMs))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid prepared-skill hold duration: {optionValue}");
                            }

                            break;

                        case "state":
                            stateToken = optionValue;
                            break;
                    }

                    continue;
                }

                if (skinKey == null)
                {
                    skinKey = token;
                    continue;
                }

                skillNameParts ??= new List<string>();
                skillNameParts.Add(token);
            }

            string normalizedState = string.IsNullOrWhiteSpace(stateToken)
                ? (holdDurationMs > 0 ? "auto" : "prepare")
                : stateToken.Trim().ToLowerInvariant();
            bool startHolding = string.Equals(normalizedState, "holding", StringComparison.OrdinalIgnoreCase);
            bool autoEnterHold = string.Equals(normalizedState, "auto", StringComparison.OrdinalIgnoreCase);
            if (!startHolding
                && !autoEnterHold
                && !string.Equals(normalizedState, "prepare", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid prepared-skill state: {stateToken}");
            }

            if (gaugeDurationMs < 0 || holdDurationMs < 0)
            {
                return ChatCommandHandler.CommandResult.Error("Prepared-skill gauge and hold durations must be zero or greater.");
            }

            if (autoEnterHold && holdDurationMs <= 0)
            {
                return ChatCommandHandler.CommandResult.Error("Prepared-skill auto state requires hold=<ms>.");
            }

            skinKey ??= hudProfile.SkinKey;
            string skillName = skillNameParts != null && skillNameParts.Count > 0
                ? string.Join(" ", skillNameParts)
                : null;
            int activeDurationMs = startHolding
                ? Math.Max(0, holdDurationMs > 0 ? holdDurationMs : durationMs)
                : autoEnterHold
                    ? Math.Max(0, holdDurationMs)
                    : Math.Max(0, durationMs);
            int maxHoldDurationMs = startHolding || autoEnterHold
                ? Math.Max(0, holdDurationMs > 0 ? holdDurationMs : durationMs)
                : Math.Max(0, durationMs);
            return _remoteUserPool.TrySetPreparedSkill(
                characterId,
                skillId,
                skillName,
                activeDurationMs,
                skinKey,
                isKeydownSkill: true,
                isHolding: startHolding,
                gaugeDurationMs: Math.Max(0, gaugeDurationMs),
                maxHoldDurationMs: maxHoldDurationMs,
                PreparedSkillHudRules.ResolveTextVariant(skillId),
                showText: hudProfile.ShowText,
                currentTime,
                out string message,
                prepareDurationMs: autoEnterHold ? Math.Max(0, durationMs) : 0,
                autoEnterHold: autoEnterHold)
                ? ChatCommandHandler.CommandResult.Ok(
                    startHolding
                        ? $"Remote user {characterId} prepared skill {skillId} entered hold state for {activeDurationMs}ms."
                        : autoEnterHold
                            ? $"Remote user {characterId} prepared skill {skillId} armed for {durationMs}ms then holds for {maxHoldDurationMs}ms."
                            : $"Remote user {characterId} prepared skill {skillId} armed for {activeDurationMs}ms.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private static bool TryParsePreparedSkillCommandOption(string token, out string optionName, out string optionValue)
        {
            optionName = null;
            optionValue = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            int separatorIndex = token.IndexOf('=');
            if (separatorIndex > 0)
            {
                optionName = token[..separatorIndex].Trim().ToLowerInvariant();
                optionValue = token[(separatorIndex + 1)..].Trim();
                return optionName is "gauge" or "hold" or "state";
            }

            if (token.Equals("prepare", StringComparison.OrdinalIgnoreCase)
                || token.Equals("holding", StringComparison.OrdinalIgnoreCase)
                || token.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                optionName = "state";
                optionValue = token;
                return true;
            }

            return false;
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserPreparedClearCommand(string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser preparedclear <characterId>");
            }

            return _remoteUserPool.TryClearPreparedSkill(characterId, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} prepared skill cleared.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserVisibleCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser visible <characterId> <on|off>");
            }

            bool? isVisible = args[2].ToLowerInvariant() switch
            {
                "on" => true,
                "off" => false,
                _ => null
            };
            if (!isVisible.HasValue)
            {
                return ChatCommandHandler.CommandResult.Error("Visible state must be on or off.");
            }

            return _remoteUserPool.TrySetWorldVisibility(characterId, isVisible.Value, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} world visibility set to {args[2].ToLowerInvariant()}.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserRemoveCommand(string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser remove <characterId>");
            }

            return _remoteUserPool.TryRemove(characterId, out string message)
                ? ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} removed.")
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserFollowCommand(string[] args)
        {
            if (args.Length < 3
                || !int.TryParse(args[1], out int characterId)
                || !int.TryParse(args[2], out int driverId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser follow <characterId> <driverId|0> [transferX transferY]");
            }

            bool transferField = false;
            Vector2? transferPosition = null;
            if (driverId == 0 && args.Length >= 5)
            {
                if (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float transferX)
                    || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float transferY))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser follow <characterId> <driverId|0> [transferX transferY]");
                }

                transferField = true;
                transferPosition = new Vector2(transferX, transferY);
            }

            bool applied = _remoteUserPool.TryApplyFollowCharacter(
                characterId,
                driverId,
                transferField,
                transferPosition,
                _playerManager?.Player?.Build?.Id ?? 0,
                _playerManager?.Player?.Position ?? Vector2.Zero,
                out string message);
            if (!applied)
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            return ChatCommandHandler.CommandResult.Ok(
                driverId > 0
                    ? $"Remote user {characterId} is now attached to follow driver {driverId}."
                    : transferField
                        ? $"Remote user {characterId} detached with transfer-field position ({transferPosition.Value.X:0},{transferPosition.Value.Y:0})."
                        : $"Remote user {characterId} detached from its follow driver.");
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserPacketCommand(string[] args, int currentTime)
        {
            if (args.Length < 3 || !TryParseRemoteUserPacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser packet <179|180|181|182|183|184|210|211|212|213|214|enter|leave|move|state|helper|team|follow|chair|mount|prepare|preparedclear|melee> [followCharacterId] <payloadhex=..|payloadb64=..>");
            }

            int? followCharacterId = null;
            int payloadArgumentIndex = 2;
            if ((RemoteUserPacketType)packetType == RemoteUserPacketType.UserFollowCharacter
                && args.Length >= 4
                && int.TryParse(args[2], out int parsedFollowCharacterId))
            {
                followCharacterId = parsedFollowCharacterId;
                payloadArgumentIndex = 3;
            }

            byte[] payload = null;
            string payloadError = null;
            if (args.Length <= payloadArgumentIndex
                || !TryParseBinaryPayloadArgument(args[payloadArgumentIndex], out payload, out payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Packet payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyRemoteUserPacket(packetType, payload, currentTime, out string result, followCharacterId)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserPacketRawCommand(string[] args, int currentTime)
        {
            if (args.Length < 3 || !TryParseRemoteUserPacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser packetraw <179|180|181|182|183|184|210|211|212|213|214|enter|leave|move|state|helper|team|follow|chair|mount|prepare|preparedclear|melee> [followCharacterId] <hex>");
            }

            int? followCharacterId = null;
            int payloadArgumentIndex = 2;
            if ((RemoteUserPacketType)packetType == RemoteUserPacketType.UserFollowCharacter
                && args.Length >= 4
                && int.TryParse(args[2], out int parsedFollowCharacterId))
            {
                followCharacterId = parsedFollowCharacterId;
                payloadArgumentIndex = 3;
            }

            if (args.Length <= payloadArgumentIndex)
            {
                return ChatCommandHandler.CommandResult.Error("Invalid packet hex payload: payload is missing.");
            }

            byte[] payload;
            try
            {
                payload = ByteUtils.HexToBytes(args[payloadArgumentIndex]);
            }
            catch (Exception ex)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid packet hex payload: {ex.Message}");
            }

            return TryApplyRemoteUserPacket(packetType, payload, currentTime, out string result, followCharacterId)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }

        private bool TryApplyRemoteUserPacket(int packetType, byte[] payload, int currentTime, out string result, int? followCharacterId = null)
        {
            result = null;
            switch ((RemoteUserPacketType)packetType)
            {
                case RemoteUserPacketType.UserEnterField:
                    if (!RemoteUserPacketCodec.TryParseEnterField(payload, out RemoteUserEnterFieldPacket enterPacket, out string enterError))
                    {
                        result = enterError;
                        return false;
                    }

                    CharacterBuild template = _playerManager?.Player?.Build?.Clone();
                    bool created = _remoteUserPool.TryAddOrUpdateAvatarLook(
                        enterPacket.CharacterId,
                        enterPacket.Name,
                        enterPacket.AvatarLook,
                        template,
                        new Vector2(enterPacket.X, enterPacket.Y),
                        out string enterMessage,
                        enterPacket.FacingRight,
                        enterPacket.ActionName,
                        "packet",
                        enterPacket.IsVisibleInWorld);
                    if (created && enterPacket.PortableChairItemId.HasValue)
                    {
                        _remoteUserPool.TrySetPortableChair(enterPacket.CharacterId, enterPacket.PortableChairItemId, out _);
                    }

                    result = created
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {enterPacket.Name} ({enterPacket.CharacterId})."
                        : enterMessage;
                    return created;

                case RemoteUserPacketType.UserLeaveField:
                    if (!RemoteUserPacketCodec.TryParseLeaveField(payload, out RemoteUserLeaveFieldPacket leavePacket, out string leaveError))
                    {
                        result = leaveError;
                        return false;
                    }

                    bool removed = _remoteUserPool.TryRemove(leavePacket.CharacterId, out string leaveMessage);
                    if (removed)
                    {
                        _summonedPool.RemoveOwnerSummons(leavePacket.CharacterId, currentTime);
                    }

                    result = removed
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {leavePacket.CharacterId}."
                        : leaveMessage;
                    return removed;

                case RemoteUserPacketType.UserMove:
                    if (!RemoteUserPacketCodec.TryParseMove(payload, currentTime, out RemoteUserMovePacket movePacket, out string moveError))
                    {
                        result = moveError;
                        return false;
                    }

                    bool moved = _remoteUserPool.TryApplyMoveSnapshot(movePacket.CharacterId, movePacket.Snapshot, movePacket.MoveAction, currentTime, out string moveMessage);
                    result = moved
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {movePacket.CharacterId}."
                        : moveMessage;
                    return moved;

                case RemoteUserPacketType.UserMoveAction:
                    if (!RemoteUserPacketCodec.TryParseMoveAction(payload, out RemoteUserMoveActionPacket moveActionPacket, out string moveActionError))
                    {
                        result = moveActionError;
                        return false;
                    }

                    bool appliedMoveAction = _remoteUserPool.TryApplyMoveAction(moveActionPacket.CharacterId, moveActionPacket.MoveAction, out string moveActionMessage);
                    result = appliedMoveAction
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {moveActionPacket.CharacterId}."
                        : moveActionMessage;
                    return appliedMoveAction;

                case RemoteUserPacketType.UserHelper:
                    if (!RemoteUserPacketCodec.TryParseHelper(payload, out RemoteUserHelperPacket helperPacket, out string helperError))
                    {
                        result = helperError;
                        return false;
                    }

                    bool helperApplied = _remoteUserPool.TrySetHelperMarker(helperPacket.CharacterId, helperPacket.MarkerType, helperPacket.ShowDirectionOverlay, out string helperMessage);
                    result = helperApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {helperPacket.CharacterId}."
                        : helperMessage;
                    return helperApplied;

                case RemoteUserPacketType.UserBattlefieldTeam:
                    if (!RemoteUserPacketCodec.TryParseBattlefieldTeam(payload, out RemoteUserBattlefieldTeamPacket teamPacket, out string teamError))
                    {
                        result = teamError;
                        return false;
                    }

                    BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;
                    if (battlefield.IsActive)
                    {
                        battlefield.OnTeamChanged(teamPacket.CharacterId, teamPacket.TeamId ?? -1, currentTime);
                    }

                    bool teamApplied = _remoteUserPool.TrySetBattlefieldTeam(teamPacket.CharacterId, teamPacket.TeamId, out string teamMessage);
                    result = teamApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {teamPacket.CharacterId}."
                        : teamMessage;
                    return teamApplied;

                case RemoteUserPacketType.UserFollowCharacter:
                    if (!RemoteUserPacketCodec.TryParseFollowCharacter(
                            payload,
                            out RemoteUserFollowCharacterPacket followPacket,
                            out string followError,
                            followCharacterId))
                    {
                        result = followError;
                        return false;
                    }

                    bool followApplied = _remoteUserPool.TryApplyFollowCharacter(
                        followPacket.CharacterId,
                        followPacket.DriverId,
                        followPacket.TransferField,
                        followPacket.TransferX.HasValue && followPacket.TransferY.HasValue
                            ? new Vector2(followPacket.TransferX.Value, followPacket.TransferY.Value)
                            : null,
                        _playerManager?.Player?.Build?.Id ?? 0,
                        _playerManager?.Player?.Position ?? Vector2.Zero,
                        out string followMessage);
                    result = followApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {followPacket.CharacterId}."
                        : followMessage;
                    return followApplied;

                case RemoteUserPacketType.UserPortableChair:
                    if (!RemoteUserPacketCodec.TryParsePortableChair(payload, out RemoteUserPortableChairPacket chairPacket, out string chairError))
                    {
                        result = chairError;
                        return false;
                    }

                    bool chairApplied = _remoteUserPool.TrySetPortableChair(
                        chairPacket.CharacterId,
                        chairPacket.ChairItemId,
                        out string chairMessage,
                        chairPacket.PairCharacterId);
                    result = chairApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {chairPacket.CharacterId}."
                        : chairMessage;
                    return chairApplied;

                case RemoteUserPacketType.UserMount:
                    if (!RemoteUserPacketCodec.TryParseMount(payload, out RemoteUserMountPacket mountPacket, out string mountError))
                    {
                        result = mountError;
                        return false;
                    }

                    bool mountApplied = _remoteUserPool.TrySetMount(mountPacket.CharacterId, mountPacket.TamingMobItemId, out string mountMessage);
                    result = mountApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {mountPacket.CharacterId}."
                        : mountMessage;
                    return mountApplied;

                case RemoteUserPacketType.UserPreparedSkill:
                    if (!RemoteUserPacketCodec.TryParsePreparedSkill(payload, out RemoteUserPreparedSkillPacket preparePacket, out string prepareError))
                    {
                        result = prepareError;
                        return false;
                    }

                    PreparedSkillHudRules.PreparedSkillHudProfile hudProfile = PreparedSkillHudRules.ResolveProfile(preparePacket.SkillId);
                    bool preparedApplied = _remoteUserPool.TrySetPreparedSkill(
                        preparePacket.CharacterId,
                        preparePacket.SkillId,
                        preparePacket.SkillName,
                        preparePacket.DurationMs,
                        string.IsNullOrWhiteSpace(preparePacket.SkinKey) ? hudProfile.SkinKey : preparePacket.SkinKey,
                        preparePacket.IsKeydownSkill,
                        preparePacket.IsHolding,
                        preparePacket.GaugeDurationMs > 0 ? preparePacket.GaugeDurationMs : hudProfile.GaugeDurationMs,
                        preparePacket.MaxHoldDurationMs,
                        PreparedSkillHudRules.ResolveTextVariant(preparePacket.SkillId),
                        preparePacket.ShowText && hudProfile.ShowText,
                        currentTime,
                        out string prepareMessage);
                    result = preparedApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {preparePacket.CharacterId}."
                        : prepareMessage;
                    return preparedApplied;

                case RemoteUserPacketType.UserPreparedSkillClear:
                    if (!RemoteUserPacketCodec.TryParsePreparedSkillClear(payload, out RemoteUserPreparedSkillClearPacket clearPacket, out string clearError))
                    {
                        result = clearError;
                        return false;
                    }

                    bool cleared = _remoteUserPool.TryClearPreparedSkill(clearPacket.CharacterId, out string clearMessage);
                    result = cleared
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {clearPacket.CharacterId}."
                        : clearMessage;
                    return cleared;

                case RemoteUserPacketType.UserMeleeAttack:
                    if (!RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket meleePacket, out string meleeError))
                    {
                        result = meleeError;
                        return false;
                    }

                    bool meleeApplied = _remoteUserPool.TryRegisterMeleeAfterImage(
                        meleePacket.CharacterId,
                        meleePacket.SkillId,
                        meleePacket.ActionName,
                        meleePacket.ActionCode,
                        meleePacket.MasteryPercent,
                        meleePacket.ChargeSkillId,
                        meleePacket.FacingRight,
                        currentTime,
                        out string meleeMessage);
                    result = meleeApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {meleePacket.CharacterId}."
                        : meleeMessage;
                    return meleeApplied;

                default:
                    result = $"Unsupported remote user packet type {packetType}.";
                    return false;
            }
        }

        private static int ResolveSyntheticRemoteUserId(string scope, string name)
        {
            string normalized = $"{scope}:{name?.Trim()}";
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return 1;
            }

            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(normalized);
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }

            return 0x40000000 | (Math.Abs(hash) & 0x3FFFFFFF);
        }

        private static bool TryParseRemoteUserHelperMarker(string text, out MinimapUI.HelperMarkerType? markerType)
        {
            markerType = text?.ToLowerInvariant() switch
            {
                "another" => MinimapUI.HelperMarkerType.Another,
                "friend" => MinimapUI.HelperMarkerType.Friend,
                "guild" => MinimapUI.HelperMarkerType.Guild,
                "guildmaster" => MinimapUI.HelperMarkerType.GuildMaster,
                "match" => MinimapUI.HelperMarkerType.Match,
                "party" => MinimapUI.HelperMarkerType.Party,
                "partymaster" => MinimapUI.HelperMarkerType.PartyMaster,
                "usertrader" => MinimapUI.HelperMarkerType.UserTrader,
                "anothertrader" => MinimapUI.HelperMarkerType.AnotherTrader,
                "clear" => null,
                _ => null
            };

            return text != null && (markerType.HasValue || string.Equals(text, "clear", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryParseRemoteUserPacketType(string text, out int packetType)
        {
            packetType = 0;
            if (int.TryParse(text, out int numericType))
            {
                packetType = numericType;
                return true;
            }

            packetType = text?.Trim().ToLowerInvariant() switch
            {
                "enter" => (int)RemoteUserPacketType.UserEnterField,
                "leave" => (int)RemoteUserPacketType.UserLeaveField,
                "move" => (int)RemoteUserPacketType.UserMove,
                "state" => (int)RemoteUserPacketType.UserMoveAction,
                "helper" => (int)RemoteUserPacketType.UserHelper,
                "team" => (int)RemoteUserPacketType.UserBattlefieldTeam,
                "follow" => (int)RemoteUserPacketType.UserFollowCharacter,
                "chair" => (int)RemoteUserPacketType.UserPortableChair,
                "mount" => (int)RemoteUserPacketType.UserMount,
                "prepare" => (int)RemoteUserPacketType.UserPreparedSkill,
                "preparedclear" => (int)RemoteUserPacketType.UserPreparedSkillClear,
                "melee" or "attack" or "meleeattack" => (int)RemoteUserPacketType.UserMeleeAttack,
                _ => 0
            };

            return packetType != 0;
        }

        private static string DescribeRemoteUserPacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemoteUserPacketType.UserEnterField => "remote user enter packet",
                (int)RemoteUserPacketType.UserLeaveField => "remote user leave packet",
                (int)RemoteUserPacketType.UserMove => "remote user common move packet",
                (int)RemoteUserPacketType.UserMoveAction => "remote user common state packet",
                (int)RemoteUserPacketType.UserHelper => "remote user common helper packet",
                (int)RemoteUserPacketType.UserBattlefieldTeam => "remote user common Battlefield team packet",
                (int)RemoteUserPacketType.UserFollowCharacter => "remote user follow-character lifecycle packet",
                (int)RemoteUserPacketType.UserPortableChair => "remote user remote chair packet",
                (int)RemoteUserPacketType.UserMount => "remote user remote mount packet",
                (int)RemoteUserPacketType.UserPreparedSkill => "remote user remote prepared-skill packet",
                (int)RemoteUserPacketType.UserPreparedSkillClear => "remote user remote prepared-skill clear packet",
                (int)RemoteUserPacketType.UserMeleeAttack => "remote user remote melee-attack packet",
                _ => $"remote user packet {packetType}"
            };
        }
    }
}
