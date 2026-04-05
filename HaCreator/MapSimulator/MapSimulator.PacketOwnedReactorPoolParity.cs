using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using System;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketReactorPoolRuntime _packetReactorPoolRuntime = new();
        private readonly ReactorPoolPacketInboxManager _reactorPoolPacketInbox = new();
        private bool _reactorPoolPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _reactorPoolPacketInboxConfiguredPort = ReactorPoolPacketInboxManager.DefaultPort;

        private void RegisterReactorPoolPacketChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "reactorpacket",
                "Inspect or drive packet-owned CReactorPool lifecycle packets",
                "/reactorpacket [status|clear|enter <objectId> <templateId> <state> <x> <y> [flip] [name...]|changestate <objectId> <state> <x> <y> [hitStartDelayMs] [properEventIndex] [stateEndDelayTicks]|move <objectId> <x> <y>|leave <objectId> <state> <x> <y>|packet <changestate|move|enter|leave|334|335|336|337> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]",
                HandlePacketOwnedReactorPoolCommand);
        }

        private bool TryApplyPacketOwnedReactorPoolPacket(int packetType, byte[] payload, out string message)
        {
            if (!TryParsePacketReactorPoolKind(packetType, out PacketReactorPoolPacketKind kind))
            {
                message = $"Unsupported reactor packet type {packetType}.";
                return false;
            }

            _packetReactorPoolRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
            return _packetReactorPoolRuntime.TryApplyPacket(
                kind,
                payload,
                currTickCount,
                BuildPacketReactorPoolCallbacks(),
                out message);
        }

        private PacketReactorPoolCallbacks BuildPacketReactorPoolCallbacks()
        {
            return new PacketReactorPoolCallbacks
            {
                EnterField = ApplyPacketOwnedReactorEnterField,
                ChangeState = ApplyPacketOwnedReactorChangeState,
                Move = ApplyPacketOwnedReactorMove,
                LeaveField = ApplyPacketOwnedReactorLeaveField
            };
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorEnterField(PacketReactorEnterFieldPacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor enter ignored because ReactorPool is unavailable.");
            }

            bool entered = _reactorPool.TryEnterPacketOwnedReactor(
                packet.ObjectId,
                packet.ReactorTemplateId,
                packet.InitialState,
                packet.X,
                packet.Y,
                packet.Flip,
                packet.Name,
                currentTick,
                out int reactorIndex,
                out string detail);
            if (entered && reactorIndex >= 0)
            {
                RegisterDynamicReactorForRendering(reactorIndex);
            }

            return new PacketReactorPoolApplyResult(entered, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorChangeState(PacketReactorChangeStatePacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor change-state ignored because ReactorPool is unavailable.");
            }

            bool changed = _reactorPool.TryChangePacketOwnedReactorState(
                packet.ObjectId,
                packet.State,
                packet.X,
                packet.Y,
                packet.HitStartDelayMs,
                packet.ProperEventIndex,
                packet.StateEndDelayTicks,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(changed, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorMove(PacketReactorMovePacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor move ignored because ReactorPool is unavailable.");
            }

            bool moved = _reactorPool.TryMovePacketOwnedReactor(
                packet.ObjectId,
                packet.X,
                packet.Y,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(moved, detail);
        }

        private PacketReactorPoolApplyResult ApplyPacketOwnedReactorLeaveField(PacketReactorLeaveFieldPacket packet, int currentTick)
        {
            if (_reactorPool == null)
            {
                return new PacketReactorPoolApplyResult(false, "Packet-owned reactor leave-field ignored because ReactorPool is unavailable.");
            }

            bool left = _reactorPool.TryLeavePacketOwnedReactor(
                packet.ObjectId,
                packet.State,
                packet.X,
                packet.Y,
                currentTick,
                out string detail);
            return new PacketReactorPoolApplyResult(left, detail);
        }

        private void BindPacketOwnedReactorPoolMapState()
        {
            _packetReactorPoolRuntime.BindMap(_mapBoard?.MapInfo?.id ?? 0);
        }

        private void ClearPacketOwnedReactorPoolState()
        {
            _packetReactorPoolRuntime.Clear();
        }

        private void EnsureReactorPoolPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_reactorPoolPacketInboxEnabled)
            {
                if (_reactorPoolPacketInbox.IsRunning)
                {
                    _reactorPoolPacketInbox.Stop();
                }

                return;
            }

            if (_reactorPoolPacketInbox.IsRunning && _reactorPoolPacketInbox.Port == _reactorPoolPacketInboxConfiguredPort)
            {
                return;
            }

            if (_reactorPoolPacketInbox.IsRunning)
            {
                _reactorPoolPacketInbox.Stop();
            }

            try
            {
                _reactorPoolPacketInbox.Start(_reactorPoolPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _reactorPoolPacketInbox.Stop();
                _chat?.AddErrorMessage($"Reactor packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainReactorPoolPacketInbox()
        {
            while (_reactorPoolPacketInbox.TryDequeue(out ReactorPoolPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedReactorPoolPacket(message.PacketType, message.Payload, out string detail);
                _reactorPoolPacketInbox.RecordDispatchResult(message, applied, detail);
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

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

        private string DescribeReactorPoolPacketInboxStatus()
        {
            string enabledText = _reactorPoolPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _reactorPoolPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_reactorPoolPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_reactorPoolPacketInboxConfiguredPort}";
            return $"Reactor packet inbox {enabledText}, {listeningText}, received {_reactorPoolPacketInbox.ReceivedCount} packet(s).";
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedReactorPoolCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{_packetReactorPoolRuntime.DescribeStatus()} {DescribeReactorPoolPacketInboxStatus()} {_reactorPoolPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetReactorPoolRuntime.Clear();
                return ChatCommandHandler.CommandResult.Ok($"{_packetReactorPoolRuntime.DescribeStatus()} {DescribeReactorPoolPacketInboxStatus()}");
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info($"{DescribeReactorPoolPacketInboxStatus()} {_reactorPoolPacketInbox.LastStatus}");
                }

                if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                {
                    int port = ReactorPoolPacketInboxManager.DefaultPort;
                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket inbox start [port]");
                    }

                    _reactorPoolPacketInboxConfiguredPort = port;
                    _reactorPoolPacketInboxEnabled = true;
                    EnsureReactorPoolPacketInboxState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(_reactorPoolPacketInbox.LastStatus);
                }

                if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                {
                    _reactorPoolPacketInboxEnabled = false;
                    EnsureReactorPoolPacketInboxState(shouldRun: false);
                    return ChatCommandHandler.CommandResult.Ok(_reactorPoolPacketInbox.LastStatus);
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket inbox [status|start [port]|stop]");
            }

            if (string.Equals(args[0], "enter", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 6
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int templateId)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int initialState)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket enter <objectId> <templateId> <state> <x> <y> [flip] [name...]");
                }

                bool flip = false;
                int nameStartIndex = 6;
                if (args.Length >= 7 && TryParseBooleanToken(args[6], out bool parsedFlip))
                {
                    flip = parsedFlip;
                    nameStartIndex = 7;
                }

                string name = args.Length > nameStartIndex ? string.Join(" ", args.Skip(nameStartIndex)) : string.Empty;
                byte[] payload = PacketReactorPoolRuntime.BuildEnterFieldPayload(objectId, templateId, initialState, x, y, flip, name);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.EnterField, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "changestate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "change", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 5
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket changestate <objectId> <state> <x> <y> [hitStartDelayMs] [properEventIndex] [stateEndDelayTicks]");
                }

                int hitStartDelayMs = 0;
                int properEventIndex = 0;
                int stateEndDelayTicks = 0;
                if (args.Length >= 6 && !int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out hitStartDelayMs))
                {
                    return ChatCommandHandler.CommandResult.Error("hitStartDelayMs must be an integer.");
                }

                if (args.Length >= 7 && !int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out properEventIndex))
                {
                    return ChatCommandHandler.CommandResult.Error("properEventIndex must be an integer.");
                }

                if (args.Length >= 8 && !int.TryParse(args[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out stateEndDelayTicks))
                {
                    return ChatCommandHandler.CommandResult.Error("stateEndDelayTicks must be an integer.");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildChangeStatePayload(
                    objectId,
                    state,
                    x,
                    y,
                    hitStartDelayMs,
                    properEventIndex,
                    stateEndDelayTicks);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.ChangeState, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "move", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 4
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket move <objectId> <x> <y>");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildMovePayload(objectId, x, y);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.Move, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "leave", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 5
                    || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int objectId)
                    || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state)
                    || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket leave <objectId> <state> <x> <y>");
                }

                byte[] payload = PacketReactorPoolRuntime.BuildLeaveFieldPayload(objectId, state, x, y);
                return TryApplyPacketOwnedReactorPoolPacket((int)PacketReactorPoolPacketKind.LeaveField, payload, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket [status|clear|enter ...|changestate ...|move ...|leave ...|packet <type> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]");
            }

            if (args.Length < 2 || !TryParsePacketReactorPoolKind(args[1], out PacketReactorPoolPacketKind kind))
            {
                return ChatCommandHandler.CommandResult.Error("Reactor packet type must be changestate, move, enter, leave, 334, 335, 336, or 337.");
            }

            byte[] packetPayload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Concat(args.Skip(2)), out packetPayload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /reactorpacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out packetPayload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyPacketOwnedReactorPoolPacket((int)kind, packetPayload, out string result)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }

        private static bool TryParsePacketReactorPoolKind(string value, out PacketReactorPoolPacketKind kind)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType))
            {
                return TryParsePacketReactorPoolKind(packetType, out kind);
            }

            return TryParsePacketReactorPoolKind(value?.Trim().ToLowerInvariant() switch
            {
                "changestate" or "change" => 334,
                "move" => 335,
                "enter" or "enterfield" => 336,
                "leave" or "leavefield" => 337,
                _ => -1
            }, out kind);
        }

        private static bool TryParsePacketReactorPoolKind(int packetType, out PacketReactorPoolPacketKind kind)
        {
            kind = packetType switch
            {
                334 => PacketReactorPoolPacketKind.ChangeState,
                335 => PacketReactorPoolPacketKind.Move,
                336 => PacketReactorPoolPacketKind.EnterField,
                337 => PacketReactorPoolPacketKind.LeaveField,
                _ => default
            };
            return Enum.IsDefined(typeof(PacketReactorPoolPacketKind), kind);
        }

        private static bool TryParseBooleanToken(string value, out bool result)
        {
            result = value?.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "on" or "yes" => true,
                "0" or "false" or "off" or "no" => false,
                _ => false
            };

            return value != null && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("0", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase)
                || value.Equals("off", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no", StringComparison.OrdinalIgnoreCase));
        }
    }
}
