using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly SummonedPacketInboxManager _summonedPacketInbox = new();
        private bool _summonedPacketInboxEnabled = EnablePacketConnectionsByDefault;
        private int _summonedPacketInboxConfiguredPort = SummonedPacketInboxManager.DefaultPort;

        private void RegisterSummonedPacketChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "summonedpacket",
                "Inspect or drive packet-owned summoned-pool traffic",
                "/summonedpacket [status|packet <create|remove|move|attack|skill|hit|0x116-0x11B> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]",
                HandlePacketOwnedSummonedCommand);
        }

        private void EnsureSummonedPacketInboxState(bool shouldRun)
        {
            if (!shouldRun || !_summonedPacketInboxEnabled)
            {
                if (_summonedPacketInbox.IsRunning)
                {
                    _summonedPacketInbox.Stop();
                }

                return;
            }

            if (_summonedPacketInbox.IsRunning && _summonedPacketInbox.Port == _summonedPacketInboxConfiguredPort)
            {
                return;
            }

            if (_summonedPacketInbox.IsRunning)
            {
                _summonedPacketInbox.Stop();
            }

            try
            {
                _summonedPacketInbox.Start(_summonedPacketInboxConfiguredPort);
            }
            catch (Exception ex)
            {
                _summonedPacketInbox.Stop();
                _chat?.AddErrorMessage($"Summoned packet inbox failed to start: {ex.Message}", currTickCount);
            }
        }

        private void DrainSummonedPacketInbox()
        {
            while (_summonedPacketInbox.TryDequeue(out SummonedPacketInboxMessage message))
            {
                if (message == null)
                {
                    continue;
                }

                bool applied = TryApplyPacketOwnedSummonedPacket(message.PacketType, message.Payload, currTickCount, out string detail);
                _summonedPacketInbox.RecordDispatchResult(message, applied, detail);
                if (!string.IsNullOrWhiteSpace(detail))
                {
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
        }

        private string DescribeSummonedPacketInboxStatus()
        {
            string enabledText = _summonedPacketInboxEnabled ? "enabled" : "disabled";
            string listeningText = _summonedPacketInbox.IsRunning
                ? $"listening on 127.0.0.1:{_summonedPacketInbox.Port}"
                : $"configured for 127.0.0.1:{_summonedPacketInboxConfiguredPort}";
            return $"Summoned packet inbox {enabledText}, {listeningText}, received {_summonedPacketInbox.ReceivedCount} packet(s).";
        }

        private bool TryApplyPacketOwnedSummonedPacket(int packetType, byte[] payload, int currentTime, out string message)
        {
            if (!_summonedPool.TryDispatchPacket(packetType, payload, currentTime, out string detail))
            {
                message = detail;
                return false;
            }

            string packetLabel = SummonedPacketInboxManager.DescribePacketType(packetType);
            message = string.IsNullOrWhiteSpace(detail)
                ? $"Applied {packetLabel}."
                : $"Applied {packetLabel}. {detail}";
            return true;
        }

        internal static bool TryRouteLocalPacketOwnedSummonExpiryToClientCancel(
            PacketOwnedSummonTimerExpiration expiration,
            Func<int, int, bool> requestClientSkillCancel)
        {
            if (!expiration.OwnerIsLocal
                || expiration.SkillId <= 0
                || requestClientSkillCancel == null)
            {
                return false;
            }

            return requestClientSkillCancel(expiration.SkillId, expiration.CurrentTime);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedSummonedCommand(string[] args)
        {
            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info($"{_summonedPool.DescribeStatus()} {DescribeSummonedPacketInboxStatus()} {_summonedPacketInbox.LastStatus}");
            }

            if (string.Equals(args[0], "inbox", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length == 1 || string.Equals(args[1], "status", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatCommandHandler.CommandResult.Info($"{DescribeSummonedPacketInboxStatus()} {_summonedPacketInbox.LastStatus}");
                }

                if (string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
                {
                    int port = SummonedPacketInboxManager.DefaultPort;
                    if (args.Length >= 3 && (!int.TryParse(args[2], out port) || port <= 0))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket inbox start [port]");
                    }

                    _summonedPacketInboxConfiguredPort = port;
                    _summonedPacketInboxEnabled = true;
                    EnsureSummonedPacketInboxState(shouldRun: true);
                    return ChatCommandHandler.CommandResult.Ok(_summonedPacketInbox.LastStatus);
                }

                if (string.Equals(args[1], "stop", StringComparison.OrdinalIgnoreCase))
                {
                    _summonedPacketInboxEnabled = false;
                    EnsureSummonedPacketInboxState(shouldRun: false);
                    return ChatCommandHandler.CommandResult.Ok(_summonedPacketInbox.LastStatus);
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket inbox [status|start [port]|stop]");
            }

            bool rawHex = string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase);
            if (!rawHex && !string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket [status|packet <type> [payloadhex=..|payloadb64=..]|packetraw <type> <hex>|inbox [status|start [port]|stop]]");
            }

            if (args.Length < 2 || !SummonedPacketInboxManager.TryParsePacketType(args[1], out int packetType))
            {
                return ChatCommandHandler.CommandResult.Error("Summoned packet type must be create, remove, move, attack, skill, hit, or 0x116-0x11B.");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Concat(args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /summonedpacket packetraw <type> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Payload must use payloadhex=.. or payloadb64=..");
            }

            return TryApplyPacketOwnedSummonedPacket(packetType, payload, currTickCount, out string result)
                ? ChatCommandHandler.CommandResult.Ok(result)
                : ChatCommandHandler.CommandResult.Error(result);
        }
    }
}
