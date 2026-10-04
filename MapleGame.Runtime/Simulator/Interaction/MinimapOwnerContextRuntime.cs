using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed record PacketOwnedMiniMapOnOffResult(
        bool IsMiniMapVisible,
        string Summary);

    internal static class MinimapOwnerContextRuntime
    {
        internal const int PacketType = 89;

        internal static bool TryDecodePayload(byte[] payload, out PacketOwnedMiniMapOnOffResult result, out string error)
        {
            result = null;
            error = null;

            if (payload == null || payload.Length != 1)
            {
                error = "MiniMapOnOff payload must contain a single on/off byte.";
                return false;
            }

            bool isVisible = payload[0] != 0;
            result = new PacketOwnedMiniMapOnOffResult(
                isVisible,
                isVisible
                    ? "CWvsContext enabled the minimap owner and requested a reload."
                    : "CWvsContext disabled the minimap owner and requested a reload.");
            return true;
        }

        internal static byte[] BuildPayload(bool isVisible)
        {
            return new[] { isVisible ? (byte)1 : (byte)0 };
        }
    }
}
