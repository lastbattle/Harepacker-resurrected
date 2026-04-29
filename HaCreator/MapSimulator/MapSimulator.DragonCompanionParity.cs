using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Managers;
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

        private void HandleDragonCompanionClientOutboundPacketObserved(
            object sender,
            LocalUtilityOutboundPacketObservedEventArgs e)
        {
            DragonCompanionRuntime dragonRuntime = _playerManager?.Dragon;
            if (dragonRuntime == null || e == null || !IsDragonCompanionClientOutboundPacket(e))
            {
                return;
            }

            byte[] rawPacket = e.RawPacket ?? Array.Empty<byte>();
            if (rawPacket.Length > 0)
            {
                dragonRuntime.TryRecordClientDragonEndUpdateActiveFlushTailCapture(
                    rawPacket,
                    opcodeFramed: true,
                    BuildDragonCompanionObservedClientOutboundSource(e),
                    out _);
                return;
            }

            dragonRuntime.TryRecordClientDragonEndUpdateActiveFlushTailCapture(
                e.Payload ?? Array.Empty<byte>(),
                opcodeFramed: false,
                BuildDragonCompanionObservedClientOutboundSource(e),
                out _);
        }

        internal static bool IsDragonCompanionClientOutboundPacket(LocalUtilityOutboundPacketObservedEventArgs e)
        {
            return e?.Opcode == DragonCompanionRuntime.ClientVecCtrlDragonMovePacketOpcode;
        }

        internal static string BuildDragonCompanionObservedClientOutboundSource(LocalUtilityOutboundPacketObservedEventArgs e)
        {
            string source = string.IsNullOrWhiteSpace(e?.Source)
                ? "official-session:outbound"
                : e.Source.Trim();
            int ordinal = e?.ObservedOrdinal ?? 0;
            return ordinal > 0
                ? $"{source} outbound#{ordinal}"
                : source;
        }

        private ChatCommandHandler.CommandResult HandleDragonCompanionCommand(string[] args)
        {
            const string usage = "Usage: /dragoncompanion [status|capture <auto|payload|packet|keypad|keypadpacked> <hex> [-- source...]]";
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

            bool opcodeFramed = false;
            bool autoDetectPacketShape = false;
            bool keyPadCapture = false;
            bool packedKeyPadCapture = false;
            if (string.Equals(args[1], "auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "detect", StringComparison.OrdinalIgnoreCase))
            {
                autoDetectPacketShape = true;
            }
            else if (string.Equals(args[1], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "raw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "opcode", StringComparison.OrdinalIgnoreCase))
            {
                opcodeFramed = true;
            }
            else if (string.Equals(args[1], "payload", StringComparison.OrdinalIgnoreCase))
            {
                opcodeFramed = false;
            }
            else if (string.Equals(args[1], "keypad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "keypads", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "keypadraw", StringComparison.OrdinalIgnoreCase))
            {
                keyPadCapture = true;
            }
            else if (string.Equals(args[1], "keypadpacked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "packedkeypad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[1], "packedkeypads", StringComparison.OrdinalIgnoreCase))
            {
                keyPadCapture = true;
                packedKeyPadCapture = true;
            }
            else
            {
                return ChatCommandHandler.CommandResult.Error(usage);
            }

            if (!TryDecodeDragonCaptureBytes(args, 2, out byte[] bytes, out string source))
            {
                return ChatCommandHandler.CommandResult.Error("Dragon companion capture hex is invalid.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                source = keyPadCapture
                    ? packedKeyPadCapture ? "packed keypad capture" : "keypad capture"
                    : autoDetectPacketShape ? "auto-detected capture"
                    : opcodeFramed ? "opcode-framed capture" : "payload capture";
            }

            if (keyPadCapture)
            {
                return dragonRuntime.TryRecordClientDragonEndUpdateActiveKeyPadStateCapture(
                        bytes,
                        packedKeyPadCapture,
                        source,
                        out string keyPadMessage)
                    ? ChatCommandHandler.CommandResult.Ok(keyPadMessage)
                    : ChatCommandHandler.CommandResult.Error(keyPadMessage);
            }

            if (autoDetectPacketShape)
            {
                return dragonRuntime.TryRecordClientDragonEndUpdateActiveFlushTailCapture(
                        bytes,
                        source,
                        out string autoMessage)
                    ? ChatCommandHandler.CommandResult.Ok(autoMessage)
                    : ChatCommandHandler.CommandResult.Error(autoMessage);
            }

            return dragonRuntime.TryRecordClientDragonEndUpdateActiveFlushTailCapture(
                    bytes,
                    opcodeFramed,
                    source,
                    out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private static bool TryDecodeDragonCaptureBytes(
            string[] args,
            int hexStartIndex,
            out byte[] bytes,
            out string source)
        {
            bytes = Array.Empty<byte>();
            source = null;
            if (args == null || hexStartIndex >= args.Length)
            {
                return false;
            }

            int sourceSeparatorIndex = Array.FindIndex(
                args,
                hexStartIndex,
                arg => string.Equals(arg, "--", StringComparison.Ordinal));
            int hexEndIndex = sourceSeparatorIndex >= 0 ? sourceSeparatorIndex : hexStartIndex + 1;
            if (hexEndIndex <= hexStartIndex)
            {
                return false;
            }

            string hexText = string.Concat(args, hexStartIndex, hexEndIndex - hexStartIndex);
            if (!TryDecodeHexBytes(hexText, out bytes))
            {
                return false;
            }

            if (sourceSeparatorIndex >= 0 && sourceSeparatorIndex + 1 < args.Length)
            {
                source = string.Join(" ", args, sourceSeparatorIndex + 1, args.Length - sourceSeparatorIndex - 1);
            }
            else if (sourceSeparatorIndex < 0 && args.Length > hexStartIndex + 1)
            {
                source = string.Join(" ", args, hexStartIndex + 1, args.Length - hexStartIndex - 1);
            }

            return true;
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
