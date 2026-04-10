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
using System.Net;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string RemoteUserCommandUsage =
            "/remoteuser <status|clear|clone|avatar|move|action|chair|mount|effect|helper|team|follow|prepare|preparedclear|visible|inspect|remove|packet|packetraw|inbox|session> ...";
        private const string RemoteUserPacketTokenUsage =
            "<-1101|-1102|-1103|-1104|-1105|-1106|-1107|-1108|-1109|-1110|-1004|-1005|179|180|181|182|183|184|210|211|212|213|214|215|216|218|219|220|221|222|223|224|225|226|227|228|229|230|coupleadd|coupleremove|friendadd|friendremove|marriageadd|marriageremove|newyearadd|newyearremove|couplechairadd|couplechairremove|chat|outsidechat|enter|leave|move|state|helper|team|follow|chair|mount|prepare|movingshootprepare|preparedclear|hit|emotion|activeeffect|upgradetomb|officialchair|usereffect|receivehp|throwgrenade|pickup|melee|effect|avatarmodified|tempset|tempreset|guildname|guildmark>";
        private const int RemoteUserOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private readonly RemoteUserPacketInboxManager _remoteUserPacketInbox = new();
        private readonly RemoteUserOfficialSessionBridgeManager _remoteUserOfficialSessionBridge = new();
        private readonly PacketOwnedRelationshipRecordRuntime _packetOwnedRelationshipRecordRuntime = new();
        private readonly PacketOwnedPortableChairRecordRuntime _packetOwnedPortableChairRecordRuntime = new();
        private bool _remoteUserOfficialSessionBridgeEnabled;
        private bool _remoteUserOfficialSessionBridgeUseDiscovery;
        private int _remoteUserOfficialSessionBridgeConfiguredListenPort = RemoteUserOfficialSessionBridgeManager.DefaultListenPort;
        private string _remoteUserOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
        private int _remoteUserOfficialSessionBridgeConfiguredRemotePort;
        private string _remoteUserOfficialSessionBridgeConfiguredProcessSelector;
        private int? _remoteUserOfficialSessionBridgeConfiguredLocalPort;
        private int _nextRemoteUserOfficialSessionBridgeDiscoveryRefreshAt;

        private void HandleRemoteUpgradeTombEffect(RemoteUserActorPool.RemoteUpgradeTombPresentation presentation)
        {
            if (_animationEffects == null || _tombFallFrames == null || _tombFallFrames.Count == 0)
            {
                return;
            }

            _animationEffects.AddOneTime(
                _tombFallFrames,
                presentation.Position.X,
                presentation.Position.Y,
                flip: false,
                presentation.CurrentTime,
                zOrder: 1);
        }

        private void HandleRemoteHitFeedback(RemoteUserActorPool.RemoteHitFeedbackPresentation presentation)
        {
            if (_combatEffects == null)
            {
                return;
            }

            if (presentation.Delta < 0)
            {
                _combatEffects.AddPartyDamage(-presentation.Delta, presentation.Position.X, presentation.Position.Y, isCritical: false, presentation.CurrentTime);
                return;
            }

            if (presentation.Delta > 0)
            {
                _combatEffects.AddHealNumber(presentation.Delta, presentation.Position.X, presentation.Position.Y, presentation.CurrentTime);
                return;
            }

            if (presentation.GuardType != 0)
            {
                _combatEffects.AddGuard(presentation.Position.X, presentation.Position.Y, presentation.CurrentTime);
                return;
            }

            _combatEffects.AddMiss(presentation.Position.X, presentation.Position.Y, presentation.CurrentTime);
        }

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
                "session" => HandleRemoteUserSessionCommand(args.Skip(1).ToArray()),
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
                return ChatCommandHandler.CommandResult.Error("Helper marker must be user, party, partymaster, guild, guildmaster, friend, another, match, usertrader, anothertrader, or clear.");
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

            return _remoteUserPool.TryClearPreparedSkill(characterId, Environment.TickCount, out string message)
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

            if (!_remoteUserPool.TrySetWorldVisibility(characterId, isVisible.Value, out string message))
            {
                return ChatCommandHandler.CommandResult.Error(message);
            }

            if (isVisible.Value)
            {
                SyncAnimationDisplayerRemoteUserState(characterId);
                SyncAnimationDisplayerRemoteQuestDeliveryOwner(characterId);
            }
            else
            {
                ClearAnimationDisplayerRemotePresentationOwners(characterId);
            }

            return ChatCommandHandler.CommandResult.Ok($"Remote user {characterId} world visibility set to {args[2].ToLowerInvariant()}.");
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
            ClearAnimationDisplayerRemotePresentationOwners(characterId);
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
                        $"Remote user packet inbox {listeningText}, received {_remoteUserPacketInbox.ReceivedCount} packet(s). {_remoteUserPacketInbox.LastStatus}{Environment.NewLine}{DescribeRemoteUserOfficialSessionBridgeStatus()}");

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

        private string DescribeRemoteUserOfficialSessionBridgeStatus()
        {
            string enabledText = _remoteUserOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _remoteUserOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _remoteUserOfficialSessionBridgeUseDiscovery
                ? _remoteUserOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_remoteUserOfficialSessionBridgeConfiguredRemotePort} with local port {_remoteUserOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_remoteUserOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_remoteUserOfficialSessionBridgeConfiguredRemoteHost}:{_remoteUserOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_remoteUserOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_remoteUserOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _remoteUserOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_remoteUserOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_remoteUserOfficialSessionBridgeConfiguredListenPort}";
            return $"Remote-user session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}. {_remoteUserOfficialSessionBridge.DescribeStatus()}";
        }

        private void EnsureRemoteUserOfficialSessionBridgeState(bool shouldRun)
        {
            if (!shouldRun || !_remoteUserOfficialSessionBridgeEnabled)
            {
                if (_remoteUserOfficialSessionBridge.IsRunning)
                {
                    _remoteUserOfficialSessionBridge.Stop();
                }

                return;
            }

            if (_remoteUserOfficialSessionBridgeConfiguredListenPort <= 0
                || _remoteUserOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)
            {
                _remoteUserOfficialSessionBridge.Stop();
                _remoteUserOfficialSessionBridgeEnabled = false;
                _remoteUserOfficialSessionBridgeConfiguredListenPort = RemoteUserOfficialSessionBridgeManager.DefaultListenPort;
                return;
            }

            if (_remoteUserOfficialSessionBridgeUseDiscovery)
            {
                if (_remoteUserOfficialSessionBridgeConfiguredRemotePort <= 0
                    || _remoteUserOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)
                {
                    _remoteUserOfficialSessionBridge.Stop();
                    return;
                }

                _remoteUserOfficialSessionBridge.TryRefreshFromDiscovery(
                    _remoteUserOfficialSessionBridgeConfiguredListenPort,
                    _remoteUserOfficialSessionBridgeConfiguredRemotePort,
                    _remoteUserOfficialSessionBridgeConfiguredProcessSelector,
                    _remoteUserOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;
            }

            if (_remoteUserOfficialSessionBridgeConfiguredRemotePort <= 0
                || _remoteUserOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || string.IsNullOrWhiteSpace(_remoteUserOfficialSessionBridgeConfiguredRemoteHost))
            {
                _remoteUserOfficialSessionBridge.Stop();
                return;
            }

            if (_remoteUserOfficialSessionBridge.IsRunning
                && _remoteUserOfficialSessionBridge.ListenPort == _remoteUserOfficialSessionBridgeConfiguredListenPort
                && string.Equals(_remoteUserOfficialSessionBridge.RemoteHost, _remoteUserOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase)
                && _remoteUserOfficialSessionBridge.RemotePort == _remoteUserOfficialSessionBridgeConfiguredRemotePort)
            {
                return;
            }

            _remoteUserOfficialSessionBridge.Start(
                _remoteUserOfficialSessionBridgeConfiguredListenPort,
                _remoteUserOfficialSessionBridgeConfiguredRemoteHost,
                _remoteUserOfficialSessionBridgeConfiguredRemotePort);
        }

        private void RefreshRemoteUserOfficialSessionBridgeDiscovery(int currentTickCount)
        {
            if (!_remoteUserOfficialSessionBridgeEnabled
                || !_remoteUserOfficialSessionBridgeUseDiscovery
                || _remoteUserOfficialSessionBridgeConfiguredRemotePort <= 0
                || _remoteUserOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue
                || _remoteUserOfficialSessionBridge.HasAttachedClient
                || currentTickCount < _nextRemoteUserOfficialSessionBridgeDiscoveryRefreshAt)
            {
                return;
            }

            _nextRemoteUserOfficialSessionBridgeDiscoveryRefreshAt =
                currentTickCount + RemoteUserOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _remoteUserOfficialSessionBridge.TryRefreshFromDiscovery(
                _remoteUserOfficialSessionBridgeConfiguredListenPort,
                _remoteUserOfficialSessionBridgeConfiguredRemotePort,
                _remoteUserOfficialSessionBridgeConfiguredProcessSelector,
                _remoteUserOfficialSessionBridgeConfiguredLocalPort,
                out _);
        }

        private void DrainRemoteUserOfficialSessionBridge(int currentTime)
        {
            while (_remoteUserOfficialSessionBridge.TryDequeue(out RemoteUserOfficialSessionBridgeMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyRemoteUserPacket(message.PacketType, message.Payload, currentTime, out string detail, sourceTag: message.Source);
                _remoteUserOfficialSessionBridge.RecordDispatchResult(message, applied, detail);
            }
        }

        private ChatCommandHandler.CommandResult HandleRemoteUserSessionCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeRemoteUserOfficialSessionBridgeStatus());
            }

            switch (args[0].ToLowerInvariant())
            {
                case "discover":
                    if (args.Length < 2
                        || !int.TryParse(args[1], out int discoverRemotePort)
                        || discoverRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session discover <remotePort> [processName|pid] [localPort]");
                    }

                    string discoverProcessSelector = args.Length >= 3 ? args[2] : null;
                    int? discoverLocalPort = null;
                    if (args.Length >= 4)
                    {
                        if (!int.TryParse(args[3], out int parsedDiscoverLocalPort) || parsedDiscoverLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session discover <remotePort> [processName|pid] [localPort]");
                        }

                        discoverLocalPort = parsedDiscoverLocalPort;
                    }

                    return ChatCommandHandler.CommandResult.Info(
                        _remoteUserOfficialSessionBridge.DescribeDiscoveredSessions(
                            discoverRemotePort,
                            discoverProcessSelector,
                            discoverLocalPort));

                case "start":
                    if (args.Length < 4
                        || !int.TryParse(args[1], out int listenPort)
                        || listenPort <= 0
                        || !int.TryParse(args[3], out int remotePort)
                        || remotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session start <listenPort> <serverHost> <serverPort>");
                    }

                    _remoteUserOfficialSessionBridgeEnabled = true;
                    _remoteUserOfficialSessionBridgeUseDiscovery = false;
                    _remoteUserOfficialSessionBridgeConfiguredListenPort = listenPort;
                    _remoteUserOfficialSessionBridgeConfiguredRemoteHost = args[2];
                    _remoteUserOfficialSessionBridgeConfiguredRemotePort = remotePort;
                    _remoteUserOfficialSessionBridgeConfiguredProcessSelector = null;
                    _remoteUserOfficialSessionBridgeConfiguredLocalPort = null;
                    EnsureRemoteUserOfficialSessionBridgeState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(DescribeRemoteUserOfficialSessionBridgeStatus());

                case "startauto":
                    if (args.Length < 3
                        || !int.TryParse(args[1], out int autoListenPort)
                        || autoListenPort <= 0
                        || !int.TryParse(args[2], out int autoRemotePort)
                        || autoRemotePort <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                    }

                    string autoProcessSelector = args.Length >= 4 ? args[3] : null;
                    int? autoLocalPort = null;
                    if (args.Length >= 5)
                    {
                        if (!int.TryParse(args[4], out int parsedAutoLocalPort) || parsedAutoLocalPort <= 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session startauto <listenPort> <remotePort> [processName|pid] [localPort]");
                        }

                        autoLocalPort = parsedAutoLocalPort;
                    }

                    _remoteUserOfficialSessionBridgeEnabled = true;
                    _remoteUserOfficialSessionBridgeUseDiscovery = true;
                    _remoteUserOfficialSessionBridgeConfiguredListenPort = autoListenPort;
                    _remoteUserOfficialSessionBridgeConfiguredRemoteHost = IPAddress.Loopback.ToString();
                    _remoteUserOfficialSessionBridgeConfiguredRemotePort = autoRemotePort;
                    _remoteUserOfficialSessionBridgeConfiguredProcessSelector = autoProcessSelector;
                    _remoteUserOfficialSessionBridgeConfiguredLocalPort = autoLocalPort;
                    _nextRemoteUserOfficialSessionBridgeDiscoveryRefreshAt = 0;
                    return _remoteUserOfficialSessionBridge.TryRefreshFromDiscovery(
                            autoListenPort,
                            autoRemotePort,
                            autoProcessSelector,
                            autoLocalPort,
                            out string startStatus)
                        ? ChatCommandHandler.CommandResult.Ok($"{startStatus} {DescribeRemoteUserOfficialSessionBridgeStatus()}")
                        : ChatCommandHandler.CommandResult.Error(startStatus);

                case "map":
                    if (args.Length < 3
                        || !ushort.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort opcode)
                        || !TryParseRemoteUserPacketType(args[2], out int mappedPacketType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session map <opcode> <packetType>");
                    }

                    return _remoteUserOfficialSessionBridge.TryConfigurePacketMapping(opcode, mappedPacketType, out string mapStatus)
                        ? ChatCommandHandler.CommandResult.Ok(mapStatus)
                        : ChatCommandHandler.CommandResult.Error(mapStatus);

                case "unmap":
                    if (args.Length < 2
                        || !ushort.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort removeOpcode))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session unmap <opcode>");
                    }

                    return _remoteUserOfficialSessionBridge.RemovePacketMapping(removeOpcode, out string removeStatus)
                        ? ChatCommandHandler.CommandResult.Ok(removeStatus)
                        : ChatCommandHandler.CommandResult.Error(removeStatus);

                case "resetmap":
                    _remoteUserOfficialSessionBridge.ClearPacketMappings();
                    return ChatCommandHandler.CommandResult.Ok(_remoteUserOfficialSessionBridge.LastStatus);

                case "stop":
                    _remoteUserOfficialSessionBridgeEnabled = false;
                    _remoteUserOfficialSessionBridgeUseDiscovery = false;
                    _remoteUserOfficialSessionBridgeConfiguredRemotePort = 0;
                    _remoteUserOfficialSessionBridgeConfiguredProcessSelector = null;
                    _remoteUserOfficialSessionBridgeConfiguredLocalPort = null;
                    _remoteUserOfficialSessionBridge.Stop();
                    return ChatCommandHandler.CommandResult.Ok(DescribeRemoteUserOfficialSessionBridgeStatus());

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /remoteuser session [status|discover <remotePort> [processName|pid] [localPort]|start <listenPort> <serverHost> <serverPort>|startauto <listenPort> <remotePort> [processName|pid] [localPort]|map <opcode> <packetType>|unmap <opcode>|resetmap|stop]");
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

            if (packetType == (int)RemoteUserPacketType.UserMoveOfficial
                && RemoteUserPacketCodec.TryParseMove(payload, currentTime, out RemoteUserMovePacket officialMovePacket, out _))
            {
                bool moved = _remoteUserPool.TryApplyMoveSnapshot(officialMovePacket.CharacterId, officialMovePacket.Snapshot, officialMovePacket.MoveAction, currentTime, out string moveMessage);
                result = moved
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialMovePacket.CharacterId}."
                    : moveMessage;
                if (moved)
                {
                    Vector2 observationPosition = ResolveRemoteTownPortalMoveObservationPositionForTesting(
                        officialMovePacket.Snapshot,
                        _remoteUserPool.TryGetActor(officialMovePacket.CharacterId, out RemoteUserActor movedActor)
                            ? movedActor.Position
                            : null);
                    RememberRemoteTownPortalOwnerFieldObservation(
                        (uint)officialMovePacket.CharacterId,
                        observationPosition,
                        TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot);
                }

                return moved;
            }

            if (IsOfficialRemoteAttackPacketType(packetType)
                && RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket officialAttackPacket, out _))
            {
                bool meleeApplied = _remoteUserPool.TryRegisterMeleeAfterImage(
                    officialAttackPacket.CharacterId,
                    officialAttackPacket.SkillId,
                    officialAttackPacket.ActionName,
                    officialAttackPacket.ActionCode,
                    officialAttackPacket.MasteryPercent,
                    officialAttackPacket.ChargeSkillId,
                    officialAttackPacket.ActionSpeed,
                    officialAttackPacket.PreparedSkillReleaseFollowUpValue,
                    officialAttackPacket.FacingRight,
                    currentTime,
                    out string meleeMessage);
                result = meleeApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialAttackPacket.CharacterId}."
                    : meleeMessage;
                return meleeApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserPreparedSkillOfficial
                && RemoteUserPacketCodec.TryParsePreparedSkill(payload, out RemoteUserPreparedSkillPacket officialPreparePacket, out _))
            {
                PreparedSkillHudRules.PreparedSkillHudProfile hudProfile = PreparedSkillHudRules.ResolveProfile(officialPreparePacket.SkillId);
                bool resolvedIsKeydownSkill = PreparedSkillHudRules.ResolveKeyDownSkillState(
                    officialPreparePacket.SkillId,
                    officialPreparePacket.IsKeydownSkill);
                PreparedSkillHudRules.ResolveRemotePreparedSkillPhases(
                    officialPreparePacket.SkillId,
                    resolvedIsKeydownSkill,
                    officialPreparePacket.IsHolding,
                    officialPreparePacket.DurationMs,
                    officialPreparePacket.MaxHoldDurationMs,
                    officialPreparePacket.AutoEnterHold,
                    out int activeDurationMs,
                    out int prepareDurationMs,
                    out bool autoEnterHold);
                bool preparedApplied = _remoteUserPool.TrySetPreparedSkill(
                    officialPreparePacket.CharacterId,
                    officialPreparePacket.SkillId,
                    officialPreparePacket.SkillName,
                    activeDurationMs,
                    string.IsNullOrWhiteSpace(officialPreparePacket.SkinKey) ? hudProfile.SkinKey : officialPreparePacket.SkinKey,
                    resolvedIsKeydownSkill,
                    officialPreparePacket.IsHolding,
                    PreparedSkillHudRules.ResolvePreparedGaugeDuration(
                        officialPreparePacket.SkillId,
                        officialPreparePacket.GaugeDurationMs,
                        officialPreparePacket.DurationMs),
                    Math.Max(0, officialPreparePacket.MaxHoldDurationMs),
                    PreparedSkillHudRules.ResolveTextVariant(officialPreparePacket.SkillId),
                    officialPreparePacket.ShowText && hudProfile.ShowText,
                    currentTime,
                    out string prepareMessage,
                    prepareDurationMs: prepareDurationMs,
                    autoEnterHold: autoEnterHold);
                result = preparedApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialPreparePacket.CharacterId}."
                    : prepareMessage;
                return preparedApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserChat
                || packetType == (int)RemoteUserPacketType.UserChatFromOutsideMap)
            {
                bool fromOutsideOfMap = packetType == (int)RemoteUserPacketType.UserChatFromOutsideMap;
                if (!RemoteUserPacketCodec.TryParseChat(payload, fromOutsideOfMap, out RemoteUserChatPacket chatPacket, out string chatError))
                {
                    result = chatError;
                    return false;
                }

                result = ApplyRemoteUserChatPacket(chatPacket, fromOutsideOfMap);
                return true;
            }

            if (packetType == (int)RemoteUserPacketType.UserMovingShootAttackPrepareOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseMovingShootAttackPrepare(payload, out RemoteUserMovingShootAttackPreparePacket officialMovingShootPacket, out string movingShootError))
                {
                    result = movingShootError;
                    return false;
                }

                bool movingShootApplied = _remoteUserPool.TryApplyMovingShootAttackPrepare(
                    officialMovingShootPacket,
                    currentTime,
                    out string movingShootMessage);
                result = movingShootApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialMovingShootPacket.CharacterId}."
                    : movingShootMessage;
                return movingShootApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserPreparedSkillClearOfficial)
            {
                if (!RemoteUserPacketCodec.TryParsePreparedSkillClear(payload, out RemoteUserPreparedSkillClearPacket officialClearPacket, out string clearError))
                {
                    result = clearError;
                    return false;
                }

                bool cleared = _remoteUserPool.TryClearPreparedSkill(officialClearPacket.CharacterId, Environment.TickCount, out string clearMessage);
                result = cleared
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialClearPacket.CharacterId}."
                    : clearMessage;
                return cleared;
            }

            if (packetType == (int)RemoteUserPacketType.UserEmotionOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseEmotion(payload, out RemoteUserEmotionPacket emotionPacket, out string emotionError))
                {
                    result = emotionError;
                    return false;
                }

                bool emotionApplied = _remoteUserPool.TryApplyEmotion(emotionPacket, currentTime, out string emotionMessage);
                result = emotionApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {emotionPacket.CharacterId}."
                    : emotionMessage;
                return emotionApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserActiveEffectItemOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseActiveEffectItem(payload, out RemoteUserActiveEffectItemPacket activeEffectPacket, out string activeEffectError))
                {
                    result = activeEffectError;
                    return false;
                }

                bool activeEffectApplied = _remoteUserPool.TryApplyActiveEffectItem(activeEffectPacket, currentTime, out string activeEffectMessage);
                result = activeEffectApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {activeEffectPacket.CharacterId}."
                    : activeEffectMessage;
                return activeEffectApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserUpgradeTombOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseUpgradeTombEffect(payload, out RemoteUserUpgradeTombPacket tombPacket, out string tombError))
                {
                    result = tombError;
                    return false;
                }

                bool tombApplied = _remoteUserPool.TryApplyUpgradeTombEffect(tombPacket, currentTime, out string tombMessage);
                result = tombApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {tombPacket.CharacterId}."
                    : tombMessage;
                return tombApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserPortableChairOfficial)
            {
                if (!RemoteUserPacketCodec.TryParsePortableChairOfficial(payload, out RemoteUserPortableChairPacket officialChairPacket, out string chairError))
                {
                    result = chairError;
                    return false;
                }

                bool chairApplied = _remoteUserPool.TrySetPortableChair(
                    officialChairPacket.CharacterId,
                    officialChairPacket.ChairItemId,
                    out string chairMessage,
                    officialChairPacket.PairCharacterId,
                    syncPairRecordFromChairState: true);
                result = chairApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {officialChairPacket.CharacterId}."
                    : chairMessage;
                return chairApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserGuildNameChangedOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseGuildNameChanged(payload, out RemoteUserGuildNameChangedPacket guildPacket, out string guildError))
                {
                    result = guildError;
                    return false;
                }

                bool guildApplied = _remoteUserPool.TryApplyProfileMetadata(
                    guildPacket.CharacterId,
                    level: null,
                    guildPacket.GuildName,
                    jobId: null,
                    out string guildMessage);
                result = guildApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {guildPacket.CharacterId}."
                    : guildMessage;
                return guildApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserGuildMarkChangedOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseGuildMarkChanged(payload, out RemoteUserGuildMarkChangedPacket guildMarkPacket, out string guildMarkError))
                {
                    result = guildMarkError;
                    return false;
                }

                bool guildMarkApplied = _remoteUserPool.TryApplyGuildMark(
                    guildMarkPacket.CharacterId,
                    guildMarkPacket.MarkBackgroundId,
                    guildMarkPacket.MarkBackgroundColor,
                    guildMarkPacket.MarkId,
                    guildMarkPacket.MarkColor,
                    out string guildMarkMessage);
                result = guildMarkApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {guildMarkPacket.CharacterId}."
                    : guildMarkMessage;
                return guildMarkApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserReceiveHpOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseReceiveHp(payload, out RemoteUserReceiveHpPacket receiveHpPacket, out string receiveHpError))
                {
                    result = receiveHpError;
                    return false;
                }

                bool receiveHpApplied = _remoteUserPool.TryApplyReceiveHp(receiveHpPacket, out string receiveHpMessage);
                result = receiveHpApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {receiveHpPacket.CharacterId}."
                    : receiveHpMessage;
                return receiveHpApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserThrowGrenadeOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseThrowGrenade(payload, out RemoteUserThrowGrenadePacket throwGrenadePacket, out string throwGrenadeError))
                {
                    result = throwGrenadeError;
                    return false;
                }

                bool throwGrenadeApplied = _remoteUserPool.TryApplyThrowGrenade(throwGrenadePacket, currentTime, out string throwGrenadeMessage);
                result = throwGrenadeApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {throwGrenadePacket.CharacterId}."
                    : throwGrenadeMessage;
                return throwGrenadeApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserHitOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseHit(payload, out RemoteUserHitPacket hitPacket, out string hitError))
                {
                    result = hitError;
                    return false;
                }

                bool hitApplied = _remoteUserPool.TryApplyHit(hitPacket, currentTime, out string hitMessage);
                result = hitApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {hitPacket.CharacterId}."
                    : hitMessage;
                return hitApplied;
            }

            if (packetType == (int)RemoteUserPacketType.UserEffectOfficial)
            {
                if (!RemoteUserPacketCodec.TryParseEffect(payload, out RemoteUserEffectPacket effectPacket, out string effectError))
                {
                    result = effectError;
                    return false;
                }

                bool effectApplied = _remoteUserPool.TryApplyEffect(effectPacket, currentTime, out string effectMessage);
                if (effectApplied
                    && effectPacket.KnownSubtype is RemoteUserEffectSubtype.QuestDeliveryStart or RemoteUserEffectSubtype.QuestDeliveryEnd)
                {
                    SyncAnimationDisplayerRemoteQuestDeliveryOwner(effectPacket.CharacterId);
                }

                result = effectApplied
                    ? $"Applied {DescribeRemoteUserPacketType(packetType)} for {effectPacket.CharacterId}."
                    : effectMessage;
                return effectApplied;
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
                        ClearAnimationDisplayerRemotePresentationOwners(leavePacket.CharacterId);

                        if (leavePosition.HasValue)
                        {
                            RememberRemoteTownPortalOwnerFieldObservation(
                                (uint)leavePacket.CharacterId,
                                leavePosition.Value,
                                TemporaryPortalField.RemoteTownPortalObservationSource.LastLiveLeaveField);
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

                    bool cleared = _remoteUserPool.TryClearPreparedSkill(clearPacket.CharacterId, Environment.TickCount, out string clearMessage);
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
                "user" => MinimapUI.HelperMarkerType.User,
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
                "chat" or "onchat" => (int)RemoteUserPacketType.UserChat,
                "outsidechat" or "chatoutside" or "onchatoutside" => (int)RemoteUserPacketType.UserChatFromOutsideMap,
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
                "commonchat" or "userchat" => (int)RemoteUserPacketType.UserChat,
                "commonoutsidechat" or "useroutsidechat" => (int)RemoteUserPacketType.UserChatFromOutsideMap,
                "chair" => (int)RemoteUserPacketType.UserPortableChair,
                "mount" => (int)RemoteUserPacketType.UserMount,
                "prepare" => (int)RemoteUserPacketType.UserPreparedSkill,
                "movingshootprepare" or "movingshoot" or "movingprepare" => (int)RemoteUserPacketType.UserMovingShootAttackPrepareOfficial,
                "preparedclear" => (int)RemoteUserPacketType.UserPreparedSkillClear,
                "hit" => (int)RemoteUserPacketType.UserHitOfficial,
                "usereffect" or "officialeffect" => (int)RemoteUserPacketType.UserEffectOfficial,
                "receivehp" or "partyhp" => (int)RemoteUserPacketType.UserReceiveHpOfficial,
                "throwgrenade" or "grenade" => (int)RemoteUserPacketType.UserThrowGrenadeOfficial,
                "pickup" or "droppickup" => (int)RemoteUserPacketType.UserDropPickup,
                "melee" or "attack" or "meleeattack" => (int)RemoteUserPacketType.UserMeleeAttack,
                "effect" or "itemeffect" or "ringeffect" => (int)RemoteUserPacketType.UserItemEffect,
                "avatarmodified" or "avatarmod" or "look" => (int)RemoteUserPacketType.UserAvatarModified,
                "tempset" or "tempstatset" => (int)RemoteUserPacketType.UserTemporaryStatSet,
                "tempreset" or "tempstatreset" => (int)RemoteUserPacketType.UserTemporaryStatReset,
                "guildname" or "guildnamechanged" => (int)RemoteUserPacketType.UserGuildNameChangedOfficial,
                "guildmark" or "guildmarkchanged" => (int)RemoteUserPacketType.UserGuildMarkChangedOfficial,
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
                (int)RemoteUserPacketType.UserChat => "remote user common chat packet",
                (int)RemoteUserPacketType.UserChatFromOutsideMap => "remote user common outside-map chat packet",
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
                (int)RemoteUserPacketType.UserHitOfficial => "remote user official hit packet",
                (int)RemoteUserPacketType.UserEmotionOfficial => "remote user official emotion packet",
                (int)RemoteUserPacketType.UserActiveEffectItemOfficial => "remote user official active-effect-item packet",
                (int)RemoteUserPacketType.UserMovingShootAttackPrepareOfficial => "remote user official moving-shoot prepare packet",
                (int)RemoteUserPacketType.UserPortableChairOfficial => "remote user official portable-chair packet",
                (int)RemoteUserPacketType.UserEffectOfficial => "remote user official effect packet",
                (int)RemoteUserPacketType.UserReceiveHpOfficial => "remote user official receive-HP packet",
                (int)RemoteUserPacketType.UserGuildNameChangedOfficial => "remote user official guild-name packet",
                (int)RemoteUserPacketType.UserGuildMarkChangedOfficial => "remote user official guild-mark packet",
                (int)RemoteUserPacketType.UserThrowGrenadeOfficial => "remote user official throw-grenade packet",
                _ => $"remote user packet {packetType}"
            };
        }

        private string ApplyRemoteUserChatPacket(RemoteUserChatPacket packet, bool fromOutsideOfMap)
        {
            string speakerName = packet.OutsideMapCharacterName;
            if (string.IsNullOrWhiteSpace(speakerName)
                && _remoteUserPool.TryGetActor(packet.CharacterId, out RemoteUserActor actor)
                && !string.IsNullOrWhiteSpace(actor?.Name))
            {
                speakerName = actor.Name;
            }

            if (string.IsNullOrWhiteSpace(speakerName))
            {
                speakerName = $"User{packet.CharacterId}";
            }

            if (packet.OnlyBalloon)
            {
                return $"Applied {DescribeRemoteUserPacketType(fromOutsideOfMap ? (int)RemoteUserPacketType.UserChatFromOutsideMap : (int)RemoteUserPacketType.UserChat)} for {packet.CharacterId} as balloon-only text.";
            }

            string chatLine = $"{speakerName} : {SanitizeRemoteUserChatText(packet.Text)}";
            int chatLogType = packet.ChatType != 0 ? 11 : 0;
            _chat?.AddClientChatMessage(
                chatLine,
                Environment.TickCount,
                chatLogType,
                whisperTargetCandidate: speakerName);
            return $"Applied {DescribeRemoteUserPacketType(fromOutsideOfMap ? (int)RemoteUserPacketType.UserChatFromOutsideMap : (int)RemoteUserPacketType.UserChat)} for {packet.CharacterId}.";
        }

        private static string SanitizeRemoteUserChatText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            char[] buffer = text.Trim().ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (char.IsControl(buffer[i]) || buffer[i] == '\u007f')
                {
                    buffer[i] = ' ';
                }
            }

            return new string(buffer).Trim();
        }

        private static bool IsOfficialRemoteAttackPacketType(int packetType)
        {
            return packetType >= (int)RemoteUserPacketType.UserAttackOfficial1
                && packetType <= (int)RemoteUserPacketType.UserAttackOfficial4;
        }
    }
}
