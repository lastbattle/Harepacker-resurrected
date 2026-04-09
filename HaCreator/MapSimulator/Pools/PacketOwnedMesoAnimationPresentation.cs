using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Pools
{
    internal readonly record struct PacketOwnedMesoLayerDrawState(
        float Alpha,
        float Scale,
        int YOffset);

    internal static class PacketOwnedMesoAnimationPresentation
    {
        internal static int ResolveLayerCount(int iconType, int availableFrameCount)
        {
            if (availableFrameCount <= 0)
            {
                return 0;
            }

            int desiredLayerCount = iconType switch
            {
                <= 1 => 2,
                2 => 3,
                _ => 4
            };

            return Math.Min(availableFrameCount, desiredLayerCount);
        }

        internal static int ResolveLayerDelayMs(int iconType)
        {
            return iconType <= 1 ? 80 : 120;
        }

        internal static PacketOwnedMesoLayerDrawState ResolveLayerDrawState(
            int iconType,
            int layerIndex,
            int layerCount,
            int tickCount,
            int spawnTime,
            float baseAlpha)
        {
            if (layerCount <= 0)
            {
                return new PacketOwnedMesoLayerDrawState(0f, 1f, 0);
            }

            int phaseOffsetMs = layerIndex * ResolveLayerDelayMs(iconType);
            float elapsedMs = Math.Max(0, tickCount - spawnTime - phaseOffsetMs);
            float fadeIn = MathHelper.Clamp(elapsedMs / (iconType >= 3 ? 240f : 160f), 0f, 1f);
            float pulse = MathF.Sin(elapsedMs / 120f);
            float layerProgress = layerCount == 1 ? 0f : layerIndex / (float)(layerCount - 1);

            float alpha = MathHelper.Clamp((0.34f + 0.16f * layerIndex + 0.08f * pulse) * baseAlpha * fadeIn, 0f, 1f);
            float scale = 0.92f + 0.06f * layerIndex + 0.025f * pulse;
            int yOffset = -(int)MathF.Round(layerProgress * (iconType >= 3 ? 6f : 4f));

            return new PacketOwnedMesoLayerDrawState(alpha, scale, yOffset);
        }
    }
}
