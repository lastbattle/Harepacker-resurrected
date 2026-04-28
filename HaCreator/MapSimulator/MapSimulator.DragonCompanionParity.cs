using HaCreator.MapSimulator.Companions;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private void FlushDragonCompanionVecCtrlEndUpdateActivePackets()
        {
            DragonCompanionRuntime dragonRuntime = _playerManager?.Dragon;
            if (dragonRuntime == null)
            {
                return;
            }

            while (dragonRuntime.TryConsumeClientVecCtrlEndUpdateActiveFlushPacket(
                       out int packetOpcode,
                       out byte[] packetPayload))
            {
                DispatchDragonCompanionVecCtrlEndUpdateActivePacket(packetOpcode, packetPayload);
            }
        }

        private void DispatchDragonCompanionVecCtrlEndUpdateActivePacket(int packetOpcode, byte[] packetPayload)
        {
            if (packetOpcode <= 0)
            {
                return;
            }

            byte[] payload = packetPayload ?? Array.Empty<byte>();
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(packetOpcode, payload, out _))
            {
                return;
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(packetOpcode, payload, out _))
            {
                return;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(packetOpcode, payload, out _))
            {
                return;
            }

            _localUtilityPacketOutbox.TryQueueOutboundPacket(packetOpcode, payload, out _);
        }

        private ChatCommandHandler.CommandResult HandleDragonCompanionCommand(string[] args)
        {
            const string usage = "Usage: /dragoncompanion [status|capture <payload|packet> <hex> [source...]]";
            DragonCompanionRuntime dragonRuntime = _playerManager?.Dragon;
            if (dragonRuntime == null)
            {
                return ChatCommandHandler.CommandResult.Error("Dragon companion runtime is unavailable.");
            }

            if (args == null || args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(dragonRuntime.DescribeClientVecCtrlEndUpdateActiveParityStatus());
            }

            if (!string.Equals(args[0], "capture", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            bool opcodeFramed;
            if (string.Equals(args[1], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "raw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "opcode", StringComparison.OrdinalIgnoreCase))
            {
                opcodeFramed = true;
            }
            else if (string.Equals(args[1], "payload", StringComparison.OrdinalIgnoreCase))
            {
                opcodeFramed = false;
            }
            else
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!TryDecodeHexBytes(args[2], out byte[] bytes))
            {
                return ChatCommandHandler.CommandResult.Error("Dragon companion capture hex is invalid.");
            }

            string source = args.Length > 3
                ? string.Join(" ", args, 3, args.Length - 3)
                : (opcodeFramed ? "opcode-framed capture" : "payload capture");

            return dragonRuntime.TryRecordClientDragonEndUpdateActiveFlushTailCapture(
                    bytes,
                    opcodeFramed,
                    source,
                    out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private DragonCompanionRuntime.OwnerPhaseContext ResolveDragonOwnerPhaseContextParity()
        {
            bool hasOwnerBuild = _playerManager?.Player?.Build != null;
            bool hasScreenFadeTransition = _screenEffects?.IsFadeActive == true;
            float screenFadeAlpha = _screenEffects?.FadeAlpha ?? 0f;
            return ResolveDragonOwnerPhaseContextParity(
                hasOwnerBuild,
                hasScreenFadeTransition,
                screenFadeAlpha);
        }

        internal static DragonCompanionRuntime.OwnerPhaseContext ResolveDragonOwnerPhaseContextParity(
            bool hasOwnerBuild,
            bool hasScreenFadeTransition,
            float screenFadeAlpha)
        {
            if (!hasOwnerBuild)
            {
                return DragonCompanionRuntime.OwnerPhaseContext.NoLocalUser;
            }

            int phaseAlpha = ResolveDragonOwnerPhaseAlphaForTransition(hasScreenFadeTransition, screenFadeAlpha);
            bool ownerMatchesLocalPhase = !hasScreenFadeTransition;
            return new DragonCompanionRuntime.OwnerPhaseContext(
                hasLocalUser: true,
                ownerMatchesLocalPhase,
                phaseAlpha);
        }

        internal static int ResolveDragonOwnerPhaseAlphaForTransition(bool hasScreenFadeTransition, float screenFadeAlpha)
        {
            if (!hasScreenFadeTransition)
            {
                return byte.MaxValue;
            }

            float clampedOpacity = MathHelper.Clamp(screenFadeAlpha, 0f, 1f);
            float ownerPhaseAlpha = 1f - clampedOpacity;
            return (int)Math.Round(ownerPhaseAlpha * byte.MaxValue, MidpointRounding.AwayFromZero);
        }
    }
}
