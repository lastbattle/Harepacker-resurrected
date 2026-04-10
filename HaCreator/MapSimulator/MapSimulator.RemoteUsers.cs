using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
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
        private const string RemoteUserCommandUsage =
            "/remoteuser <status|clear|clone|avatar|move|action|chair|mount|effect|helper|team|follow|prepare|preparedclear|visible|inspect|remove|packet|packetraw|inbox> ...";
        private const string RemoteUserPacketTokenUsage =
            "<-1101|-1102|-1103|-1104|-1105|-1106|-1107|-1108|-1109|-1110|179|180|181|182|183|184|210|211|212|213|214|215|223|225|226|coupleadd|coupleremove|friendadd|friendremove|marriageadd|marriageremove|newyearadd|newyearremove|couplechairadd|couplechairremove|enter|leave|move|state|helper|team|follow|chair|mount|prepare|preparedclear|pickup|melee|effect|avatarmodified|tempset|tempreset>";
        private readonly RemoteUserPacketInboxManager _remoteUserPacketInbox = new();
        private readonly PacketOwnedRelationshipRecordRuntime _packetOwnedRelationshipRecordRuntime = new();
        private readonly PacketOwnedPortableChairRecordRuntime _packetOwnedPortableChairRecordRuntime = new();

        private void RememberRemoteTownPortalOwnerFieldObservation(
            uint ownerCharacterId,
            Vector2 position,
            TemporaryPortalField.RemoteTownPortalObservationSource observationSource = TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot)
        {
            if (_temporaryPortalField == null || _mapBoard?.MapInfo == null || ownerCharacterId == 0)
            {
                return;
            }

            if (!TryResolveMysticDoorReturnTargetForMap(_mapBoard.MapInfo.id, out int returnMapId, out float returnX, out float returnY)
                || returnMapId == _mapBoard.MapInfo.id)
            {
                return;
            }

            _temporaryPortalField.RememberRemoteTownPortalOwnerFieldObservation(
                ownerCharacterId,
                _mapBoard.MapInfo.id,
                position.X,
                position.Y,
                new TemporaryPortalField.RemoteTownPortalResolvedDestination(returnMapId, returnX, returnY),
                Environment.TickCount,
                observationSource);
        }

        internal static Vector2 ResolveRemoteTownPortalMoveObservationPositionForTesting(
            PlayerMovementSyncSnapshot movementSnapshot,
            Vector2? liveActorPosition)
        {
            if (liveActorPosition.HasValue)
            {
                return liveActorPosition.Value;
            }

            if (movementSnapshot == null)
            {
                return Vector2.Zero;
            }

            return new Vector2(movementSnapshot.PassivePosition.X, movementSnapshot.PassivePosition.Y);
        }

        private void RegisterRemoteUserChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "remoteuser",
                "Create or mutate shared remote user actors",
                RemoteUserCommandUsage,
                args => HandleRemoteUserCommand(args, currTickCount));
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserCommand(string[] args, int currentTime)
        {
            if (args == null || args.Length == 0)
            {
                return ChatCommandHandler.CommandResult.Error($"Usage: {RemoteUserCommandUsage}");
            }

            return args[0].ToLowerInvariant() switch
            {
                "status" => ChatCommandHandler.CommandResult.Info(
                    $"{_remoteUserPool.DescribeStatus()}{Environment.NewLine}{_packetOwnedRelationshipRecordRuntime.DescribeStatus()}"),
                "clear" => HandleRemoteUserClearCommand(),
                "clone" => HandleRemoteUserCloneCommand(args),
                "avatar" => HandleRemoteUserAvatarCommand(args),
                "move" => HandleRemoteUserMoveCommand(args),
                "action" => HandleRemoteUserActionCommand(args),
                "chair" => HandleRemoteUserChairCommand(args),
                "mount" => HandleRemoteUserMountCommand(args),
                "effect" => HandleRemoteUserEffectCommand(args, currentTime),
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
                "inbox" => HandleRemoteUserInboxCommand(args),
                _ => ChatCommandHandler.CommandResult.Error($"Usage: {RemoteUserCommandUsage}")
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
            _packetOwnedRelationshipRecordRuntime.Clear();
            _packetOwnedPortableChairRecordRuntime.Clear();
            _animationEffects.ClearUserStates();
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

        private ChatCommandHandler.CommandResult HandleRemoteUserEffectCommand(string[] args, int currentTime)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser effect <characterId> <itemId|clear> [pairCharacterId] or /remoteuser effect <characterId> <generic|couple|friend|newyear|marriage> <itemId|clear> [pairCharacterId]");
            }

            RemoteRelationshipOverlayType relationshipType = RemoteRelationshipOverlayType.Generic;
            int itemTokenIndex = 2;
            if (!int.TryParse(args[2], out _)
                && !string.Equals(args[2], "clear", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseRemoteRelationshipOverlayType(args[2], out relationshipType))
                {
                    return ChatCommandHandler.CommandResult.Error($"Invalid relationship overlay type: {args[2]}");
                }

                itemTokenIndex = 3;
                if (args.Length <= itemTokenIndex)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser effect <characterId> <generic|couple|friend|newyear|marriage> <itemId|clear> [pairCharacterId]");
                }
            }

            int? itemId = string.Equals(args[itemTokenIndex], "clear", StringComparison.OrdinalIgnoreCase)
                ? null
                : int.TryParse(args[itemTokenIndex], out int parsedItemId) ? parsedItemId : null;
            if (!string.Equals(args[itemTokenIndex], "clear", StringComparison.OrdinalIgnoreCase) && !itemId.HasValue)
            {
                return ChatCommandHandler.CommandResult.Error($"Invalid effect item ID: {args[itemTokenIndex]}");
            }

            int? pairCharacterId = null;
            int pairTokenIndex = itemTokenIndex + 1;
            if (args.Length > pairTokenIndex)
            {
                if (!int.TryParse(args[pairTokenIndex], out int parsedPairCharacterId))
                {
                    return ChatCommandHandler.CommandResult.Error($"Invalid pair character ID: {args[pairTokenIndex]}");
                }

                pairCharacterId = parsedPairCharacterId > 0 ? parsedPairCharacterId : null;
            }

            return _remoteUserPool.TrySetItemEffect(characterId, relationshipType, itemId, pairCharacterId, currentTime, out string message)
                ? ChatCommandHandler.CommandResult.Ok(itemId.HasValue
                    ? $"Remote user {characterId} {relationshipType} effect item set to {itemId.Value}."
                    : $"Remote user {characterId} {relationshipType} effect item cleared.")
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
            int gaugeDurationMs = 0;
            int holdDurationMs = 0;
            string stateToken = null;
            bool hasGaugeOverride = false;

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

                            hasGaugeOverride = true;
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

            if (autoEnterHold
                && holdDurationMs <= 0
                && !PreparedSkillHudRules.UsesReleaseTriggeredExecution(skillId))
            {
                return ChatCommandHandler.CommandResult.Error("Prepared-skill auto state requires hold=<ms>.");
            }

            skinKey ??= hudProfile.SkinKey;
            int resolvedGaugeDurationMs = PreparedSkillHudRules.ResolvePreparedGaugeDuration(
                skillId,
                hasGaugeOverride ? Math.Max(0, gaugeDurationMs) : 0,
                durationMs);
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
                gaugeDurationMs: resolvedGaugeDurationMs,
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
                            ? maxHoldDurationMs > 0
                                ? $"Remote user {characterId} prepared skill {skillId} armed for {durationMs}ms then holds for {maxHoldDurationMs}ms."
                                : $"Remote user {characterId} prepared skill {skillId} armed for {durationMs}ms then waits for release."
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
                ? HandleRemoteUserRemovalCommandResult(characterId)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserRemovalCommandResult(int characterId)
        {
            _animationEffects.RemoveUserState(characterId, currTickCount);
            return ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} removed.");
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
                return ChatCommandHandler.CommandResult.Error($"Usage: /remoteuser packet {RemoteUserPacketTokenUsage} [followCharacterId] <payloadhex=..|payloadb64=..>");
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
                return ChatCommandHandler.CommandResult.Error($"Usage: /remoteuser packetraw {RemoteUserPacketTokenUsage} [followCharacterId] <hex>");
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

        private ChatCommandHandler.CommandResult HandleRemoteUserInboxCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser inbox [status|start [port]|stop]");
            }

            switch (args[1].ToLowerInvariant())
            {
                case "status":
                    string listeningText = _remoteUserPacketInbox.IsRunning
                        ? $"listening on 127.0.0.1:{_remoteUserPacketInbox.Port}"
                        : $"inactive (default 127.0.0.1:{RemoteUserPacketInboxManager.DefaultPort})";
                    return ChatCommandHandler.CommandResult.Info(
                        $"Remote user packet inbox {listeningText}, received {_remoteUserPacketInbox.ReceivedCount} packet(s). {_remoteUserPacketInbox.LastStatus}");

                case "start":
                    int port = RemoteUserPacketInboxManager.DefaultPort;
                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0 || port > ushort.MaxValue))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser inbox start [port]");
                    }

                    _remoteUserPacketInbox.Start(port);
                    return ChatCommandHandler.CommandResult.Ok(_remoteUserPacketInbox.LastStatus);

                case "stop":
                    _remoteUserPacketInbox.Stop();
                    return ChatCommandHandler.CommandResult.Ok(_remoteUserPacketInbox.LastStatus);

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser inbox [status|start [port]|stop]");
            }
        }

        private void DrainRemoteUserPacketInbox(int currentTime)
        {
            while (_remoteUserPacketInbox.TryDequeue(out RemoteUserPacketInboxMessage message))
            {
                bool applied = TryApplyRemoteUserPacket(message.PacketType, message.Payload, currentTime, out string result, sourceTag: message.Source);
                _remoteUserPacketInbox.RecordDispatchResult(message, applied, result);
            }
        }

        private bool TryApplyRemoteUserPacket(int packetType, byte[] payload, int currentTime, out string result, int? followCharacterId = null, string sourceTag = null)
        {
            result = null;
            if (_packetOwnedRelationshipRecordRuntime.IsRelationshipRecordPacket(packetType))
            {
                return _packetOwnedRelationshipRecordRuntime.TryApplyPacket(
                    packetType,
                    payload,
                    _remoteUserPool,
                    currentTime,
                    sourceTag,
                    out result);
            }

            if (_packetOwnedPortableChairRecordRuntime.IsPortableChairRecordPacket(packetType))
            {
                return _packetOwnedPortableChairRecordRuntime.TryApplyPacket(
                    packetType,
                    payload,
                    _remoteUserPool,
                    sourceTag,
                    out result);
            }

            switch ((RemoteUserPacketType)packetType)
            {
                case RemoteUserPacketType.UserEnterField:
                    if (!RemoteUserPacketCodec.TryParseEnterField(payload, out RemoteUserEnterFieldPacket enterPacket, out string enterError))
                    {
                        result = enterError;
                        return false;
                    }

                    CharacterBuild template = _playerManager?.Player?.Build?.Clone();
                    bool applied = _remoteUserPool.TryAddOrUpdateAvatarLook(
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
                    if (applied)
                    {
                        _remoteUserPool.TryApplyProfileMetadata(
                            enterPacket.CharacterId,
                            enterPacket.Level,
                            enterPacket.GuildName,
                            enterPacket.JobId,
                            out _);
                    }

                    if (applied)
                    {
                        RemoteUserEnterFieldStateApplicator.TryApply(
                            _remoteUserPool,
                            enterPacket,
                            currentTime,
                            SyncAnimationDisplayerRemoteUserState,
                            out _);
                    }

                    result = applied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {enterPacket.Name} ({enterPacket.CharacterId})."
                        : enterMessage;
                    if (applied)
                    {
                        RememberRemoteTownPortalOwnerFieldObservation(
                            (uint)enterPacket.CharacterId,
                            new Vector2(enterPacket.X, enterPacket.Y),
                            TemporaryPortalField.RemoteTownPortalObservationSource.EnterField);
                    }

                    return applied;

                case RemoteUserPacketType.UserLeaveField:
                    if (!RemoteUserPacketCodec.TryParseLeaveField(payload, out RemoteUserLeaveFieldPacket leavePacket, out string leaveError))
                    {
                        result = leaveError;
                        return false;
                    }

                    Vector2? leavePosition = null;
                    if (_remoteUserPool.TryGetActor(leavePacket.CharacterId, out RemoteUserActor leavingActor))
                    {
                        leavePosition = leavingActor.Position;
                    }

                    bool removed = _remoteUserPool.TryRemove(leavePacket.CharacterId, out string leaveMessage);
                    if (removed)
                    {
                        _summonedPool.RemoveOwnerSummons(leavePacket.CharacterId, currentTime);
                        _animationEffects.RemoveUserState(leavePacket.CharacterId, currentTime);

                        if (leavePosition.HasValue)
                        {
                            RememberRemoteTownPortalOwnerFieldObservation(
                                (uint)leavePacket.CharacterId,
                                leavePosition.Value,
                                TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot);
                        }
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
                    if (moved)
                    {
                        Vector2 observationPosition = ResolveRemoteTownPortalMoveObservationPositionForTesting(
                            movePacket.Snapshot,
                            _remoteUserPool.TryGetActor(movePacket.CharacterId, out RemoteUserActor movedActor)
                                ? movedActor.Position
                                : null);
                        RememberRemoteTownPortalOwnerFieldObservation(
                            (uint)movePacket.CharacterId,
                            observationPosition,
                            TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot);
                    }

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
                    if (followApplied
                        && followPacket.TransferField
                        && followPacket.TransferX.HasValue
                        && followPacket.TransferY.HasValue)
                    {
                        RememberRemoteTownPortalOwnerFieldObservation(
                            (uint)followPacket.CharacterId,
                            new Vector2(followPacket.TransferX.Value, followPacket.TransferY.Value),
                            TemporaryPortalField.RemoteTownPortalObservationSource.FollowTransfer);
                    }
                    else if (followApplied && _remoteUserPool.TryGetActor(followPacket.CharacterId, out RemoteUserActor followActor))
                    {
                        RememberRemoteTownPortalOwnerFieldObservation(
                            (uint)followPacket.CharacterId,
                            followActor.Position,
                            TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot);
                    }

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
                    bool resolvedIsKeydownSkill = PreparedSkillHudRules.ResolveKeyDownSkillState(
                        preparePacket.SkillId,
                        preparePacket.IsKeydownSkill);
                    PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                        preparePacket.SkillId,
                        resolvedIsKeydownSkill,
                        preparePacket.IsHolding,
                        preparePacket.DurationMs,
                        preparePacket.MaxHoldDurationMs,
                        preparePacket.AutoEnterHold,
                        out int activeDurationMs,
                        out int prepareDurationMs,
                        out bool autoEnterHold);
                    bool preparedApplied = _remoteUserPool.TrySetPreparedSkill(
                        preparePacket.CharacterId,
                        preparePacket.SkillId,
                        preparePacket.SkillName,
                        activeDurationMs,
                        string.IsNullOrWhiteSpace(preparePacket.SkinKey) ? hudProfile.SkinKey : preparePacket.SkinKey,
                        resolvedIsKeydownSkill,
                        preparePacket.IsHolding,
                        PreparedSkillHudRules.ResolvePreparedGaugeDuration(
                            preparePacket.SkillId,
                            preparePacket.GaugeDurationMs,
                            preparePacket.DurationMs),
                        Math.Max(0, preparePacket.MaxHoldDurationMs),
                        PreparedSkillHudRules.ResolveTextVariant(preparePacket.SkillId),
                        preparePacket.ShowText && hudProfile.ShowText,
                        currentTime,
                        out string prepareMessage,
                        prepareDurationMs: prepareDurationMs,
                        autoEnterHold: autoEnterHold);
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

                case RemoteUserPacketType.UserDropPickup:
                    if (!RemoteUserPacketCodec.TryParseDropPickup(payload, out RemoteUserDropPickupPacket dropPickupPacket, out string dropPickupError))
                    {
                        result = dropPickupError;
                        return false;
                    }

                    if (_dropPool == null)
                    {
                        result = "Drop-pickup packets require an active drop pool.";
                        return false;
                    }

                    DropItem drop = _dropPool.GetDrop(dropPickupPacket.DropId);
                    if (drop == null)
                    {
                        result = $"Drop-pickup packet referenced drop {dropPickupPacket.DropId}, but that drop is not active.";
                        return false;
                    }

                    string pickupActorName = ResolveRemoteUserDropPickupActorName(
                        dropPickupPacket,
                        _remoteUserPool,
                        ResolveMobPickupSourceName,
                        ResolvePickupItemName);
                    Vector2? pickupTargetPosition = ResolveRemoteUserDropPickupTargetPosition(dropPickupPacket);
                    bool pickupApplied = _dropPool.ResolveRemotePickup(
                        drop,
                        dropPickupPacket.ActorId,
                        currentTime,
                        dropPickupPacket.ActorKind,
                        pickupActorName,
                        pickedByPet: dropPickupPacket.ActorKind == DropPickupActorKind.Pet,
                        pickupTargetPosition: pickupTargetPosition);
                    result = pickupApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for drop {dropPickupPacket.DropId}."
                        : $"Remote drop pickup could not be applied for drop {dropPickupPacket.DropId}.";
                    return pickupApplied;

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
                        meleePacket.ActionSpeed,
                        meleePacket.PreparedSkillReleaseFollowUpValue,
                        meleePacket.FacingRight,
                        currentTime,
                        out string meleeMessage);
                    result = meleeApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {meleePacket.CharacterId}."
                        : meleeMessage;
                    return meleeApplied;

                case RemoteUserPacketType.UserItemEffect:
                    if (!RemoteUserPacketCodec.TryParseItemEffect(payload, out RemoteUserItemEffectPacket itemEffectPacket, out string itemEffectError))
                    {
                        result = itemEffectError;
                        return false;
                    }

                    bool itemEffectApplied = _remoteUserPool.TrySetItemEffect(
                        itemEffectPacket.CharacterId,
                        itemEffectPacket.RelationshipType,
                        itemEffectPacket.ItemId,
                        itemEffectPacket.PairCharacterId,
                        currentTime,
                        out string itemEffectMessage);
                    result = itemEffectApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {itemEffectPacket.CharacterId}."
                        : itemEffectMessage;
                    return itemEffectApplied;

                case RemoteUserPacketType.UserAvatarModified:
                    if (!RemoteUserPacketCodec.TryParseAvatarModified(payload, out RemoteUserAvatarModifiedPacket avatarModifiedPacket, out string avatarModifiedError))
                    {
                        result = avatarModifiedError;
                        return false;
                    }

                    bool avatarModifiedApplied = _remoteUserPool.TryApplyAvatarModified(
                        avatarModifiedPacket,
                        currentTime,
                        out string avatarModifiedMessage);
                    result = avatarModifiedApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {avatarModifiedPacket.CharacterId}."
                        : avatarModifiedMessage;
                    return avatarModifiedApplied;

                case RemoteUserPacketType.UserTemporaryStatSet:
                    if (!RemoteUserPacketCodec.TryParseTemporaryStatSet(payload, out RemoteUserTemporaryStatSetPacket temporaryStatSetPacket, out string temporaryStatSetError))
                    {
                        result = temporaryStatSetError;
                        return false;
                    }

                    bool temporaryStatSetApplied = _remoteUserPool.TryApplyTemporaryStatSet(
                        temporaryStatSetPacket,
                        out string temporaryStatSetMessage);
                    if (temporaryStatSetApplied)
                    {
                        SyncAnimationDisplayerRemoteUserState(temporaryStatSetPacket.CharacterId);
                    }
                    result = temporaryStatSetApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {temporaryStatSetPacket.CharacterId}."
                        : temporaryStatSetMessage;
                    return temporaryStatSetApplied;

                case RemoteUserPacketType.UserTemporaryStatReset:
                    if (!RemoteUserPacketCodec.TryParseTemporaryStatReset(payload, out RemoteUserTemporaryStatResetPacket temporaryStatResetPacket, out string temporaryStatResetError))
                    {
                        result = temporaryStatResetError;
                        return false;
                    }

                    bool temporaryStatResetApplied = _remoteUserPool.TryApplyTemporaryStatReset(
                        temporaryStatResetPacket,
                        out string temporaryStatResetMessage);
                    if (temporaryStatResetApplied)
                    {
                        SyncAnimationDisplayerRemoteUserState(temporaryStatResetPacket.CharacterId);
                    }
                    result = temporaryStatResetApplied
                        ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {temporaryStatResetPacket.CharacterId}."
                        : temporaryStatResetMessage;
                    return temporaryStatResetApplied;

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

        private string ResolveRemotePickupActorName(
            DropPickupActorKind actorKind,
            int actorId,
            string actorName,
            int fallbackOwnerId = 0)
        {
            return ResolveRemotePickupActorName(
                actorKind,
                actorId,
                actorName,
                _remoteUserPool,
                ResolveMobPickupSourceName,
                ResolvePickupItemName,
                fallbackOwnerId);
        }

        internal static string ResolveRemotePickupActorName(
            DropPickupActorKind actorKind,
            int actorId,
            string actorName,
            RemoteUserActorPool remoteUserPool,
            Func<int, string> mobNameResolver,
            Func<int, string> itemNameResolver,
            int fallbackOwnerId = 0)
        {
            if (!string.IsNullOrWhiteSpace(actorName))
            {
                return actorName.Trim();
            }

            return actorKind switch
            {
                DropPickupActorKind.Player when actorId > 0 && remoteUserPool?.TryGetActor(actorId, out RemoteUserActor actor) == true
                    => actor.Name,
                DropPickupActorKind.Player
                    => FormatPlayerPickupActorLabel(actorId),
                DropPickupActorKind.Pet
                    => ResolveRemotePetPickupActorName(actorId, fallbackOwnerId, remoteUserPool, itemNameResolver),
                DropPickupActorKind.Mob when actorId > 0 && mobNameResolver != null
                    => mobNameResolver(actorId),
                DropPickupActorKind.Other
                    => FormatOtherPickupActorLabel(actorId),
                _ => null
            };
        }

        internal static string ResolveRemoteUserDropPickupActorName(
            RemoteUserDropPickupPacket packet,
            RemoteUserActorPool remoteUserPool,
            Func<int, string> mobNameResolver,
            Func<int, string> itemNameResolver)
        {
            return ResolveRemotePickupActorName(
                packet.ActorKind,
                packet.ActorId,
                packet.ActorName,
                remoteUserPool,
                mobNameResolver,
                itemNameResolver,
                packet.FallbackOwnerId);
        }

        internal static Vector2? ResolveRemoteUserDropPickupTargetPosition(RemoteUserDropPickupPacket packet)
        {
            return packet.TargetX.HasValue && packet.TargetY.HasValue
                ? new Vector2(packet.TargetX.Value, packet.TargetY.Value)
                : null;
        }

        internal static string FormatPlayerPickupActorLabel(int actorId)
        {
            return actorId > 0
                ? $"Character {actorId}"
                : null;
        }

        internal static string FormatOtherPickupActorLabel(int actorId)
        {
            return actorId > 0
                ? $"Actor {actorId}"
                : null;
        }

        internal static string ResolveRemotePetPickupActorName(
            int actorId,
            int fallbackOwnerId,
            RemoteUserActorPool remoteUserPool,
            Func<int, string> itemNameResolver)
        {
            if (TryDecodeRemotePetPickupActorId(actorId, out int ownerCharacterId, out int slotIndex)
                && remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor ownerActor) == true)
            {
                string petName = ResolveRemotePetPickupItemName(ownerActor, slotIndex, itemNameResolver);
                if (!string.IsNullOrWhiteSpace(petName))
                {
                    return petName;
                }

                return FormatRemoteOwnerPetLabel(ownerActor.Name);
            }

            int resolvedOwnerId = fallbackOwnerId;
            if (resolvedOwnerId <= 0 && actorId > 0 && remoteUserPool?.TryGetActor(actorId, out _) == true)
            {
                resolvedOwnerId = actorId;
            }

            if (resolvedOwnerId > 0 && remoteUserPool?.TryGetActor(resolvedOwnerId, out RemoteUserActor fallbackOwner) == true)
            {
                return FormatRemoteOwnerPetLabel(fallbackOwner.Name);
            }

            if (ownerCharacterId > 0)
            {
                return FormatRemoteOwnerPetLabel(FormatPlayerPickupActorLabel(ownerCharacterId));
            }

            if (resolvedOwnerId > 0)
            {
                return FormatRemoteOwnerPetLabel(FormatPlayerPickupActorLabel(resolvedOwnerId));
            }

            return null;
        }

        internal static string FormatRemoteOwnerPetLabel(string ownerName)
        {
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                return null;
            }

            string trimmedOwnerName = ownerName.Trim();
            return trimmedOwnerName.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? $"{trimmedOwnerName}' pet"
                : $"{trimmedOwnerName}'s pet";
        }

        private static string ResolveRemotePetPickupItemName(
            RemoteUserActor ownerActor,
            int slotIndex,
            Func<int, string> itemNameResolver)
        {
            if (ownerActor?.Build?.RemotePetItemIds == null
                || slotIndex < 0
                || slotIndex >= ownerActor.Build.RemotePetItemIds.Count)
            {
                return null;
            }

            int petItemId = ownerActor.Build.RemotePetItemIds[slotIndex];
            if (petItemId <= 0)
            {
                return null;
            }

            return itemNameResolver?.Invoke(petItemId);
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

        private static bool TryParseRemoteRelationshipOverlayType(string text, out RemoteRelationshipOverlayType relationshipType)
        {
            relationshipType = text?.Trim().ToLowerInvariant() switch
            {
                "generic" => RemoteRelationshipOverlayType.Generic,
                "couple" => RemoteRelationshipOverlayType.Couple,
                "friend" or "friendship" => RemoteRelationshipOverlayType.Friendship,
                "newyear" or "newyearcard" => RemoteRelationshipOverlayType.NewYearCard,
                "marriage" or "wedding" => RemoteRelationshipOverlayType.Marriage,
                _ => RemoteRelationshipOverlayType.Generic
            };

            return text != null && (relationshipType != RemoteRelationshipOverlayType.Generic || string.Equals(text.Trim(), "generic", StringComparison.OrdinalIgnoreCase));
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
                "coupleadd" => (int)RemoteUserPacketType.UserCoupleRecordAdd,
                "coupleremove" => (int)RemoteUserPacketType.UserCoupleRecordRemove,
                "friendadd" => (int)RemoteUserPacketType.UserFriendRecordAdd,
                "friendremove" => (int)RemoteUserPacketType.UserFriendRecordRemove,
                "marriageadd" => (int)RemoteUserPacketType.UserMarriageRecordAdd,
                "marriageremove" => (int)RemoteUserPacketType.UserMarriageRecordRemove,
                "newyearadd" => (int)RemoteUserPacketType.UserNewYearCardRecordAdd,
                "newyearremove" => (int)RemoteUserPacketType.UserNewYearCardRecordRemove,
                "couplechairadd" => (int)RemoteUserPacketType.UserCoupleChairRecordAdd,
                "couplechairremove" => (int)RemoteUserPacketType.UserCoupleChairRecordRemove,
                "enter" => (int)RemoteUserPacketType.UserEnterField,
                "leave" => (int)RemoteUserPacketType.UserLeaveField,
                "move" => (int)RemoteUserPacketType.UserMove,
                "state" => (int)RemoteUserPacketType.UserMoveAction,
                "helper" => (int)RemoteUserPacketType.UserHelper,
                "team" => (int)RemoteUserPacketType.UserBattlefieldTeam,
                "follow" => (int)RemoteUserPacketType.UserFollowCharacter,
                "couplerecordadd" or "coupleadd" => (int)RemoteUserPacketType.UserCoupleRecordAdd,
                "couplerecordremove" or "coupleremove" => (int)RemoteUserPacketType.UserCoupleRecordRemove,
                "friendrecordadd" or "friendadd" => (int)RemoteUserPacketType.UserFriendRecordAdd,
                "friendrecordremove" or "friendremove" => (int)RemoteUserPacketType.UserFriendRecordRemove,
                "marriagerecordadd" or "marriageadd" => (int)RemoteUserPacketType.UserMarriageRecordAdd,
                "marriagerecordremove" or "marriageremove" => (int)RemoteUserPacketType.UserMarriageRecordRemove,
                "newyearcardrecordadd" or "newyearadd" => (int)RemoteUserPacketType.UserNewYearCardRecordAdd,
                "newyearcardrecordremove" or "newyearremove" => (int)RemoteUserPacketType.UserNewYearCardRecordRemove,
                "couplechairrecordadd" or "couplechairadd" => (int)RemoteUserPacketType.UserCoupleChairRecordAdd,
                "couplechairrecordremove" or "couplechairremove" => (int)RemoteUserPacketType.UserCoupleChairRecordRemove,
                "chair" => (int)RemoteUserPacketType.UserPortableChair,
                "mount" => (int)RemoteUserPacketType.UserMount,
                "prepare" => (int)RemoteUserPacketType.UserPreparedSkill,
                "preparedclear" => (int)RemoteUserPacketType.UserPreparedSkillClear,
                "pickup" or "droppickup" => (int)RemoteUserPacketType.UserDropPickup,
                "melee" or "attack" or "meleeattack" => (int)RemoteUserPacketType.UserMeleeAttack,
                "effect" or "itemeffect" or "ringeffect" => (int)RemoteUserPacketType.UserItemEffect,
                "avatarmodified" or "avatarmod" or "look" => (int)RemoteUserPacketType.UserAvatarModified,
                "tempset" or "tempstatset" => (int)RemoteUserPacketType.UserTemporaryStatSet,
                "tempreset" or "tempstatreset" => (int)RemoteUserPacketType.UserTemporaryStatReset,
                _ => 0
            };

            return packetType != 0;
        }

        private static string DescribeRemoteUserPacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemoteUserPacketType.UserCoupleRecordAdd => "remote user couple-record add packet",
                (int)RemoteUserPacketType.UserCoupleRecordRemove => "remote user couple-record remove packet",
                (int)RemoteUserPacketType.UserFriendRecordAdd => "remote user friendship-record add packet",
                (int)RemoteUserPacketType.UserFriendRecordRemove => "remote user friendship-record remove packet",
                (int)RemoteUserPacketType.UserMarriageRecordAdd => "remote user marriage-record add packet",
                (int)RemoteUserPacketType.UserMarriageRecordRemove => "remote user marriage-record remove packet",
                (int)RemoteUserPacketType.UserNewYearCardRecordAdd => "remote user New Year card-record add packet",
                (int)RemoteUserPacketType.UserNewYearCardRecordRemove => "remote user New Year card-record remove packet",
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
                (int)RemoteUserPacketType.UserDropPickup => "remote user remote drop-pickup packet",
                (int)RemoteUserPacketType.UserMeleeAttack => "remote user remote melee-attack packet",
                (int)RemoteUserPacketType.UserItemEffect => "remote user remote item-effect packet",
                (int)RemoteUserPacketType.UserAvatarModified => "remote user remote avatar-modified packet",
                (int)RemoteUserPacketType.UserTemporaryStatSet => "remote user remote temporary-stat set packet",
                (int)RemoteUserPacketType.UserTemporaryStatReset => "remote user remote temporary-stat reset packet",
                _ => $"remote user packet {packetType}"
            };
        }
    }
}
