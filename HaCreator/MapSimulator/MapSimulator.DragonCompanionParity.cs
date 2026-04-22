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
