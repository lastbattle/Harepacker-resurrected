using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketScriptOwnerButtonVisualState
    {
        Normal,
        Hover,
        Pressed,
        Disabled
    }

    internal static class PacketScriptOwnerVisualStateResolver
    {
        internal static int ResolveAnimatedFrameIndex(int currentTickCount, IReadOnlyList<int> frameDurationsMs)
        {
            if (frameDurationsMs == null || frameDurationsMs.Count == 0)
            {
                return 0;
            }

            int totalDuration = 0;
            for (int i = 0; i < frameDurationsMs.Count; i++)
            {
                totalDuration += Math.Max(1, frameDurationsMs[i]);
            }

            if (totalDuration <= 0)
            {
                return 0;
            }

            int tick = Math.Abs(currentTickCount % totalDuration);
            int elapsed = 0;
            for (int i = 0; i < frameDurationsMs.Count; i++)
            {
                elapsed += Math.Max(1, frameDurationsMs[i]);
                if (tick < elapsed)
                {
                    return i;
                }
            }

            return frameDurationsMs.Count - 1;
        }

        internal static PacketScriptOwnerButtonVisualState ResolveButtonState(bool enabled, bool hovered, bool pressed)
        {
            if (!enabled)
            {
                return PacketScriptOwnerButtonVisualState.Disabled;
            }

            if (hovered && pressed)
            {
                return PacketScriptOwnerButtonVisualState.Pressed;
            }

            if (hovered)
            {
                return PacketScriptOwnerButtonVisualState.Hover;
            }

            return PacketScriptOwnerButtonVisualState.Normal;
        }
    }
}
