using System;
using System.Linq;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly MobAttackPacketInboxManager _mobAttackPacketInbox = new();
        private bool _mobAttackPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _mobAttackPacketInboxConfiguredPort = MobAttackPacketInboxManager.DefaultPort;

        private void RegisterMobAttackPacketChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "mobattackpacket",
                "Inspect or drive packet-owned mob attack override traffic",
                "/mobattackpacket [status|packet <move|287|0x11F> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]",
                HandleMobAttackPacketCommand);
        }

        private void EnsureMobAttackPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_mobAttackPacketInboxEnabled)
            {
                if (_mobAttackPacketInbox.IsRunning)
                {
                    _mobAttackPacketInbox.Stop();
                }

                return;
            }

            if (_mobAttackPacketInbox.IsRunning && _mobAttackPacketInbox.Port == _mobAttackPacketInboxConfiguredPort)
            {
                return;
            }

            if (_mobAttackPacketInbox.IsRunning)
            {
                _mobAttackPacketInbox.Stop();
            }

            try
            {
                _mobAttackPacketInbox.Start(_mobAttackPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _mobAttackPacketInbox.Stop();
                _chat?.AddErrorMessage($"Mob attack packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainMobAttackPacketInbox()
        {
            while (_mobAttackPacketInbox.TryDequeue(out MobAttackPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyMobAttackPacket(message.PacketType, message.Payload, currTickCount, out string detail);
                _mobAttackPacketInbox.RecordDispatchResult(message, applied, detail);
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

        private string DescribeMobAttackPacketInboxStatus()
        {
            string enabledText = _mobAttackPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _mobAttackPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_mobAttackPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_mobAttackPacketInboxConfiguredPort}";
            return $"Mob attack packet inbox {enabledText}, {listeningText}, received {_mobAttackPacketInbox.ReceivedCount} packet(s).";
        }

        private bool TryApplyMobAttackPacket(int packetType, byte[] payload, int currentTime, out string message)
        {
            if (!MobMoveAttackPacketCodec.TryDecode(packetType, payload, out var decodedPacket, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            if (decodedPacket.AttackId <= 0)
            {
                message = $"Ignored {MobAttackPacketInboxManager.DescribePacketType(packetType)} for mob {decodedPacket.MobId}: move action {decodedPacket.MoveAction} was not an attack branch.";
                return false;
            }

            if ((_mobPool?.GetMob(decodedPacket.MobId))?.AI == null)
            {
                message = $"Ignored {MobAttackPacketInboxManager.DescribePacketType(packetType)} for mob {decodedPacket.MobId}: mob was not active in the current pool.";
                return false;
            }

            var liveMob = _mobPool.GetMob(decodedPacket.MobId);
            liveMob?.MovementInfo?.ApplyPacketMoveInterrupt(decodedPacket.NotForceLandingWhenDiscard);

            if (!decodedPacket.NextAttackPossible)
            {
                _mobAttackSystem.SetNextAttackPacketOverrides(decodedPacket.MobId, decodedPacket.AttackId, currentTime);
                message = $"Cleared packet attack overrides for mob {decodedPacket.MobId} attack{decodedPacket.AttackId} from {MobAttackPacketInboxManager.DescribePacketType(packetType)} because NextAttackPossible=0.";
                return true;
            }

            if (!MobMoveAttackPacketCodec.ShouldQueueSimulatorAttackOverrides(decodedPacket))
            {
                _mobAttackSystem.SetNextAttackPacketOverrides(decodedPacket.MobId, decodedPacket.AttackId, currentTime);
                message = $"Ignored packet attack overrides for mob {decodedPacket.MobId} attack{decodedPacket.AttackId} from {MobAttackPacketInboxManager.DescribePacketType(packetType)} because bNotChangeAction=1 suppressed CMob::DoAttack.";
                return true;
            }

            bool sourceFacesRight = !decodedPacket.FacingLeft;
            bool hasMultiTargetOverrides = decodedPacket.MultiTargetForBall?.Count > 0;
            bool hasAreaDelayOverrides = decodedPacket.RandTimeForAreaAttack?.Count > 0;
            MobTargetInfo lockedTargetOverride = MobMoveAttackPacketCodec.CreateLockedTargetOverride(decodedPacket.LockedTargetInfo);
            bool hasLockedTargetOverride = lockedTargetOverride?.IsValid == true;
            if (!hasMultiTargetOverrides && !hasAreaDelayOverrides && !hasLockedTargetOverride)
            {
                _mobAttackSystem.SetNextAttackPacketOverrides(
                    decodedPacket.MobId,
                    decodedPacket.AttackId,
                    currentTime,
                    sourceFacesRight: sourceFacesRight);
                message = $"Queued packet facing override for mob {decodedPacket.MobId} attack{decodedPacket.AttackId} from {MobAttackPacketInboxManager.DescribePacketType(packetType)} with no locked target, multiball lanes, or area delays.";
                return true;
            }

            _mobAttackSystem.SetNextAttackPacketOverrides(
                decodedPacket.MobId,
                decodedPacket.AttackId,
                currentTime,
                lockedTargetOverride,
                decodedPacket.MultiTargetForBall,
                decodedPacket.RandTimeForAreaAttack,
                sourceFacesRight);

            string multiTargetSummary = hasMultiTargetOverrides
                ? $"{decodedPacket.MultiTargetForBall.Count} multiball lane point(s)"
                : "no multiball lane points";
            string randDelaySummary = hasAreaDelayOverrides
                ? $"{decodedPacket.RandTimeForAreaAttack.Count} area-delay value(s)"
                : "no area-delay values";
            string lockedTargetSummary = hasLockedTargetOverride
                ? $"locked target {lockedTargetOverride.TargetType}:{lockedTargetOverride.TargetId}"
                : "no locked target";
            string facingSummary = sourceFacesRight ? "facing right" : "facing left";
            message = $"Queued packet attack overrides for mob {decodedPacket.MobId} attack{decodedPacket.AttackId} from {MobAttackPacketInboxManager.DescribePacketType(packetType)} with {lockedTargetSummary}, {multiTargetSummary}, {randDelaySummary}, and {facingSummary}.";
            return true;
        }

        private ChatCommandHandler.CommandResult HandleMobAttackPacketCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{DescribeMobAttackPacketInboxStatus()} {_mobAttackPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info($"{DescribeMobAttackPacketInboxStatus()} {_mobAttackPacketInbox.LastStatus}");
                }

                if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                {
                    int port = MobAttackPacketInboxManager.DefaultPort;
                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /mobattackpacket inbox start [port]");
                    }

                    _mobAttackPacketInboxConfiguredPort = port;
                    _mobAttackPacketInboxEnabled = true;
                    EnsureMobAttackPacketInboxState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(_mobAttackPacketInbox.LastStatus);
                }

                if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                {
                    _mobAttackPacketInboxEnabled = false;
                    EnsureMobAttackPacketInboxState(shouldRun: false);
                    return ChatCommandHandler.CommandResult.Ok(_mobAttackPacketInbox.LastStatus);
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /mobattackpacket inbox [status|start [port]|stop]");
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /mobattackpacket [status|packet <move|287|0x11F> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]");
            }

            if (args.Length < 2 || !MobAttackPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Mob attack packet type must be move, 287, or 0x11F.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Concat(args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /mobattackpacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyMobAttackPacket(packetType, payload, currTickCount, out string result)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }
    }
}
