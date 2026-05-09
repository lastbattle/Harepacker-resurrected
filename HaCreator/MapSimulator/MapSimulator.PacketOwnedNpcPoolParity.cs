using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketNpcPoolRuntime _packetOwnedNpcPoolRuntime = new();
        private readonly NpcPoolPacketInboxManager _npcPoolPacketInbox = new();

        private bool TryApplyPacketOwnedNpcPoolPacket(int packetType, byte[] payload, out string message, string source = null)
        {
            message = null;
            payload ??= Array.Empty<byte>();
            if (!Enum.IsDefined(typeof(PacketNpcPoolPacketKind), (PacketNpcPoolPacketKind)packetType))
            {
                message = $"Unsupported NPC pool packet {packetType.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            var kind = (PacketNpcPoolPacketKind)packetType;
            bool applied = _packetOwnedNpcPoolRuntime.TryApplyPacket(
                kind,
                payload,
                currTickCount,
                BuildPacketNpcPoolCallbacks(),
                out message);
            if (applied)
            {
                RefreshNpcLookup();
            }

            return applied;
        }

        private PacketNpcPoolCallbacks BuildPacketNpcPoolCallbacks()
        {
            return new PacketNpcPoolCallbacks
            {
                EnterField = ApplyPacketNpcEnterField,
                LeaveField = ApplyPacketNpcLeaveField,
                ChangeController = ApplyPacketNpcChangeController,
                Move = ApplyPacketNpcMove,
                UpdateLimitedInfo = ApplyPacketNpcLimitedInfo,
                SetSpecialAction = ApplyPacketNpcSpecialAction,
                ImitateData = ApplyPacketNpcImitateData,
                TemplatePacket = (payload, _) => new PacketNpcPoolApplyResult(
                    true,
                    $"CNpcPool::OnNpcTemplatePacket retained {payload?.Length ?? 0} byte(s) for template refresh parity.")
            };
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcEnterField(PacketNpcEnterFieldPacket packet, int currentTick)
        {
            NpcItem existing = FindPacketNpc(packet.ObjectId);
            if (existing != null)
            {
                existing.ApplyPacketInit(
                    packet.ObjectId,
                    packet.X,
                    packet.Y,
                    packet.MoveAction,
                    packet.FootholdId,
                    packet.Rx0,
                    packet.Rx1,
                    packet.Enabled,
                    existing.PacketControllerOwnedByLocalUser);
                return new PacketNpcPoolApplyResult(
                    true,
                    $"CNpcPool::OnNpcEnterField refreshed object {packet.ObjectId} template {packet.TemplateId} at ({packet.X},{packet.Y}).");
            }

            NpcItem npc = CreatePacketOwnedNpcItem(packet, localController: false);
            if (npc == null)
            {
                return new PacketNpcPoolApplyResult(false, $"NPC template {packet.TemplateId} could not be loaded for packet object {packet.ObjectId}.");
            }

            mapObjects_NPCs.Add(npc);
            RefreshNpcLookup();
            return new PacketNpcPoolApplyResult(
                true,
                $"CNpcPool::OnNpcEnterField admitted object {packet.ObjectId} template {packet.TemplateId} at ({packet.X},{packet.Y}).");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcLeaveField(PacketNpcLeaveFieldPacket packet, int currentTick)
        {
            NpcItem npc = FindPacketNpc(packet.ObjectId);
            if (npc == null)
            {
                return new PacketNpcPoolApplyResult(true, $"CNpcPool::OnNpcLeaveField ignored missing object {packet.ObjectId}.");
            }

            mapObjects_NPCs.Remove(npc);
            RefreshNpcLookup();
            return new PacketNpcPoolApplyResult(true, $"CNpcPool::OnNpcLeaveField removed object {packet.ObjectId}.");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcChangeController(PacketNpcChangeControllerPacket packet, int currentTick)
        {
            NpcItem npc = FindPacketNpc(packet.ObjectId);
            if (!packet.LocalController)
            {
                if (npc != null)
                {
                    npc.ApplyPacketInit(
                        packet.ObjectId,
                        npc.CurrentX,
                        npc.CurrentY,
                        npc.LastPacketMoveAction,
                        npc.PacketFootholdId,
                        npc.MovementInfo?.RX0 ?? npc.CurrentX,
                        npc.MovementInfo?.RX1 ?? npc.CurrentX,
                        npc.PacketEnabled,
                        localController: false);
                }

                return new PacketNpcPoolApplyResult(true, $"CNpcPool::OnNpcChangeController set object {packet.ObjectId} remote-controlled.");
            }

            if (packet.LocalInit is not PacketNpcEnterFieldPacket init)
            {
                return new PacketNpcPoolApplyResult(false, $"CNpcPool::OnNpcChangeController local object {packet.ObjectId} did not include init data.");
            }

            if (npc == null)
            {
                npc = CreatePacketOwnedNpcItem(init, localController: true);
                if (npc == null)
                {
                    return new PacketNpcPoolApplyResult(false, $"NPC template {init.TemplateId} could not be loaded for local-controller object {packet.ObjectId}.");
                }

                mapObjects_NPCs.Add(npc);
            }
            else
            {
                npc.ApplyPacketInit(
                    init.ObjectId,
                    init.X,
                    init.Y,
                    init.MoveAction,
                    init.FootholdId,
                    init.Rx0,
                    init.Rx1,
                    init.Enabled,
                    localController: true);
            }

            RefreshNpcLookup();
            return new PacketNpcPoolApplyResult(true, $"CNpcPool::OnNpcChangeController set object {packet.ObjectId} local-controlled.");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcMove(PacketNpcMovePacket packet, int currentTick)
        {
            NpcItem npc = FindPacketNpc(packet.ObjectId);
            if (npc == null)
            {
                return new PacketNpcPoolApplyResult(true, $"CNpc::OnMove ignored missing object {packet.ObjectId}.");
            }

            npc.ApplyPacketMove(packet.OneTimeAction, packet.ChatIndex, packet.MovePathElements);
            return new PacketNpcPoolApplyResult(
                true,
                $"CNpc::OnMove applied action {packet.OneTimeAction} chat {packet.ChatIndex} to object {packet.ObjectId}; move-path bytes={packet.MovePathPayload?.Length ?? 0}, decoded={packet.MovePathElements?.Count ?? 0}.");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcLimitedInfo(PacketNpcLimitedInfoPacket packet, int currentTick)
        {
            NpcItem npc = FindPacketNpc(packet.ObjectId);
            if (npc == null)
            {
                return new PacketNpcPoolApplyResult(true, $"CNpc::OnUpdateLimitedInfo ignored missing object {packet.ObjectId}.");
            }

            npc.ApplyPacketLimitedInfo(packet.Enabled, currentTick);
            string state = packet.Enabled ? "enabled" : "disabled";
            return new PacketNpcPoolApplyResult(
                true,
                $"CNpc::OnUpdateLimitedInfo marked object {packet.ObjectId} {state} and reset its action layer/effect marker.");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcSpecialAction(PacketNpcSpecialActionPacket packet, int currentTick)
        {
            NpcItem npc = FindPacketNpc(packet.ObjectId);
            if (npc == null)
            {
                return new PacketNpcPoolApplyResult(true, $"CNpc::OnSetSpecialAction ignored missing object {packet.ObjectId}.");
            }

            if (!npc.TryApplyPacketSpecialAction(packet.ActionName))
            {
                return new PacketNpcPoolApplyResult(
                    true,
                    $"CNpc::OnSetSpecialAction kept object {packet.ObjectId} unchanged because action '{packet.ActionName}' is not in the loaded template action set.");
            }

            return new PacketNpcPoolApplyResult(true, $"CNpc::OnSetSpecialAction routed '{packet.ActionName}' to object {packet.ObjectId}.");
        }

        private PacketNpcPoolApplyResult ApplyPacketNpcImitateData(IReadOnlyList<PacketNpcImitateEntry> entries, int currentTick)
        {
            int applied = 0;
            foreach (PacketNpcImitateEntry entry in entries ?? Array.Empty<PacketNpcImitateEntry>())
            {
                string templateId = NormalizeNpcTemplateId(entry.TemplateId);
                foreach (NpcItem npc in EnumerateLiveNpcs())
                {
                    if (!string.Equals(NormalizeNpcTemplateId(npc?.NpcInstance?.NpcInfo?.ID), templateId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    npc.ApplyImitatedLook(entry.Name, entry.AvatarLookPayload?.ToArray());
                    if (!string.IsNullOrWhiteSpace(entry.Name) && _texturePool != null && GraphicsDevice != null)
                    {
                        npc.ReplaceNameTooltip(MapSimulatorLoader.CreateNPCMobNameTooltip(
                            entry.Name.Trim(),
                            npc.CurrentX,
                            npc.CurrentY,
                            System.Drawing.Color.FromArgb(255, 255, 255, 0),
                            _texturePool,
                            UserScreenScaleFactor,
                            GraphicsDevice));
                    }

                    applied++;
                }
            }

            return new PacketNpcPoolApplyResult(
                true,
                $"CNpcPool::OnNpcImitateData applied {applied.ToString(CultureInfo.InvariantCulture)} live NPC mutation(s) from {entries?.Count ?? 0} template entr{((entries?.Count ?? 0) == 1 ? "y" : "ies")}.");
        }

        private NpcItem CreatePacketOwnedNpcItem(PacketNpcEnterFieldPacket packet, bool localController)
        {
            if (_mapBoard == null || _texturePool == null || GraphicsDevice == null)
            {
                return null;
            }

            NpcInfo info = NpcInfo.Get(packet.TemplateId.ToString(CultureInfo.InvariantCulture));
            if (info == null)
            {
                return null;
            }

            int rx0Shift = Math.Max(0, packet.X - packet.Rx0);
            int rx1Shift = Math.Max(0, packet.Rx1 - packet.X);
            var instance = new NpcInstance(
                info,
                _mapBoard,
                packet.X,
                packet.Y,
                rx0Shift,
                rx1Shift,
                yShift: 0,
                limitedname: null,
                mobTime: 0,
                flip: (packet.MoveAction & 1) != 0,
                hide: false,
                info: null,
                team: null);

            NpcItem npc = LifeLoader.CreateNpcFromProperty(
                _texturePool,
                instance,
                UserScreenScaleFactor,
                GraphicsDevice,
                new ConcurrentBag<MapleLib.WzLib.WzObject>(),
                includeTooltips: true,
                _playerManager?.Player?.Build?.Gender,
                hasQuestCheckContext: true,
                _questRuntime.GetCurrentState,
                questId => _questRuntime.TryGetQuestRecordValue(questId, out string value) ? value : string.Empty);
            npc?.ApplyPacketInit(
                packet.ObjectId,
                packet.X,
                packet.Y,
                packet.MoveAction,
                packet.FootholdId,
                packet.Rx0,
                packet.Rx1,
                packet.Enabled,
                localController);
            return npc;
        }

        private NpcItem FindPacketNpc(int objectId)
        {
            RefreshNpcLookup();
            if (_npcsById.TryGetValue(objectId, out NpcItem npc))
            {
                return npc;
            }

            return EnumerateLiveNpcs().FirstOrDefault(item => item.PacketObjectId == objectId);
        }

        private void RefreshNpcLookup()
        {
            _npcsArray = mapObjects_NPCs.OfType<NpcItem>().ToArray();
            _npcsById.Clear();
            foreach (NpcItem npc in _npcsArray)
            {
                if (npc == null)
                {
                    continue;
                }

                int objectId = npc.PacketObjectId >= 0
                    ? npc.PacketObjectId
                    : TryParseNpcTemplateId(npc.NpcInstance?.NpcInfo?.ID, out int templateId) ? templateId : -1;
                if (objectId >= 0)
                {
                    _npcsById[objectId] = npc;
                }
            }
        }

        private IEnumerable<NpcItem> EnumerateLiveNpcs()
        {
            if (_npcsArray == null || _npcsArray.Length == 0)
            {
                RefreshNpcLookup();
            }

            return _npcsArray ?? Array.Empty<NpcItem>();
        }

        private static string NormalizeNpcTemplateId(int templateId)
        {
            return templateId.ToString(CultureInfo.InvariantCulture).PadLeft(7, '0');
        }

        private static string NormalizeNpcTemplateId(string templateId)
        {
            if (TryParseNpcTemplateId(templateId, out int parsed))
            {
                return NormalizeNpcTemplateId(parsed);
            }

            return templateId ?? string.Empty;
        }

        private static bool TryParseNpcTemplateId(string templateId, out int parsed)
        {
            parsed = 0;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            string normalized = templateId.EndsWith(".img", StringComparison.OrdinalIgnoreCase)
                ? templateId[..^4]
                : templateId;
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcPoolCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                RefreshNpcLookup();
                return ChatCommandHandler.CommandResult.Info(_packetOwnedNpcPoolRuntime.DescribeStatus(_npcsArray?.Length ?? 0));
            }

            switch (args[0].ToLowerInvariant())
            {
                case "packet":
                case "packetraw":
                {
                    bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
                    if (args.Length < 2 || !NpcPoolPacketInboxManager.TryParsePacketType(args[1], out int packetType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcpool packet <imitate|limiteddisable|enter|leave|controller|move|limited|special|template> [payloadhex=..|payloadb64=..]");
                    }

                    byte[] payload = Array.Empty<byte>();
                    if (rawHex)
                    {
                        if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /npcpool packetraw <kind> <hex>");
                        }
                    }
                    else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
                    {
                        return ChatCommandHandler.CommandResult.Error(payloadError ?? "NPC pool packet payload must use payloadhex=.. or payloadb64=...");
                    }

                    _npcPoolPacketInbox.EnqueueLocal(packetType, payload, "npcpool-command");
                    if (!_npcPoolPacketInbox.TryDequeue(out NpcPoolPacketInboxMessage queuedMessage))
                    {
                        return ChatCommandHandler.CommandResult.Error("NPC pool inbox did not retain the injected packet.");
                    }

                    bool applied = TryApplyPacketOwnedNpcPoolPacket(queuedMessage.PacketType, queuedMessage.Payload, out string message, queuedMessage.Source);
                    _npcPoolPacketInbox.RecordDispatchResult(queuedMessage, applied, message);
                    return applied
                        ? ChatCommandHandler.CommandResult.Ok(message)
                        : ChatCommandHandler.CommandResult.Error(message);
                }

                case "enter":
                    return HandlePacketOwnedNpcPoolEnterCommand(args.Skip(1).ToArray());

                case "leave":
                    if (args.Length < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int leaveObjectId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcpool leave <objectId>");
                    }

                    return ApplyBuiltNpcPoolPayload(
                        PacketNpcPoolPacketKind.LeaveField,
                        PacketNpcPoolRuntime.BuildLeaveFieldPayload(leaveObjectId));

                case "move":
                    if (args.Length < 4
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int moveObjectId)
                        || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int action)
                        || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int chatIndex))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcpool move <objectId> <action> <chatIndex>");
                    }

                    return ApplyBuiltNpcPoolPayload(
                        PacketNpcPoolPacketKind.Move,
                        PacketNpcPoolRuntime.BuildMovePayload(moveObjectId, action, chatIndex));

                case "limited":
                    if (args.Length < 3
                        || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int limitedObjectId)
                        || !TryParseOnOffArgument(args[2], out bool enabled))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcpool limited <objectId> <on|off>");
                    }

                    return ApplyBuiltNpcPoolPayload(
                        PacketNpcPoolPacketKind.UpdateLimitedInfo,
                        PacketNpcPoolRuntime.BuildLimitedInfoPayload(limitedObjectId, enabled));

                case "special":
                    if (args.Length < 3 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int specialObjectId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /npcpool special <objectId> <actionName>");
                    }

                    return ApplyBuiltNpcPoolPayload(
                        PacketNpcPoolPacketKind.SetSpecialAction,
                        PacketNpcPoolRuntime.BuildSpecialActionPayload(specialObjectId, string.Join(" ", args.Skip(2))));

                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /npcpool [status|enter <objectId> <templateId> <x> <y> [rx0 rx1 enabled]|leave <objectId>|move <objectId> <action> <chatIndex>|limited <objectId> <on|off>|special <objectId> <actionName>|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]");
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedNpcPoolEnterCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int templateId)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /npcpool enter <objectId> <templateId> <x> <y> [rx0 rx1 enabled]");
            }

            int rx0 = args.Length >= 5 && int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRx0) ? parsedRx0 : x - 20;
            int rx1 = args.Length >= 6 && int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRx1) ? parsedRx1 : x + 20;
            bool enabled = args.Length < 7 || TryParseOnOffArgument(args[6], out bool parsedEnabled) && parsedEnabled;
            return ApplyBuiltNpcPoolPayload(
                PacketNpcPoolPacketKind.EnterField,
                PacketNpcPoolRuntime.BuildEnterFieldPayload(objectId, templateId, x, y, moveAction: 0, footholdId: 0, rx0, rx1, enabled));
        }

        private ChatCommandHandler.CommandResult ApplyBuiltNpcPoolPayload(PacketNpcPoolPacketKind kind, byte[] payload)
        {
            return TryApplyPacketOwnedNpcPoolPacket((int)kind, payload, out string message, "npcpool-command")
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }
    }
}
