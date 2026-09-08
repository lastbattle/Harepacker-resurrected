using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Pools
{
    internal readonly record struct PacketOwnedMesoLayerDrawState(
        float Alpha,
        float Scale,
        int YOffset);

    internal readonly record struct PacketOwnedMesoLayerEnvelope(
        float MinAlpha,
        float MaxAlpha,
        float MinScale,
        float MaxScale,
        int MaxYOffset);

    internal static class PacketOwnedMesoAnimationPresentation
    {
        private static readonly PacketOwnedMesoLayerEnvelope[][] LayerEnvelopesByIconType =
        {
            new[]
            {
                new PacketOwnedMesoLayerEnvelope(0.42f, 0.60f, 0.94f, 1.02f, 0),
                new PacketOwnedMesoLayerEnvelope(0.66f, 0.86f, 1.00f, 1.09f, 3)
            },
            new[]
            {
                new PacketOwnedMesoLayerEnvelope(0.40f, 0.58f, 0.94f, 1.01f, 0),
                new PacketOwnedMesoLayerEnvelope(0.64f, 0.82f, 0.99f, 1.07f, 2)
            },
            new[]
            {
                new PacketOwnedMesoLayerEnvelope(0.32f, 0.48f, 0.90f, 0.98f, 0),
                new PacketOwnedMesoLayerEnvelope(0.48f, 0.66f, 0.96f, 1.04f, 3),
                new PacketOwnedMesoLayerEnvelope(0.68f, 0.88f, 1.03f, 1.12f, 6)
            },
            new[]
            {
                new PacketOwnedMesoLayerEnvelope(0.26f, 0.42f, 0.88f, 0.96f, 0),
                new PacketOwnedMesoLayerEnvelope(0.38f, 0.56f, 0.93f, 1.01f, 2),
                new PacketOwnedMesoLayerEnvelope(0.54f, 0.74f, 0.98f, 1.08f, 5),
                new PacketOwnedMesoLayerEnvelope(0.72f, 0.94f, 1.05f, 1.16f, 8)
            }
        };

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

        internal static PacketOwnedMesoLayerEnvelope ResolveLayerEnvelope(int iconType, int layerIndex, int layerCount)
        {
            int normalizedIconType = Math.Clamp(iconType, 0, LayerEnvelopesByIconType.Length - 1);
            PacketOwnedMesoLayerEnvelope[] envelopes = LayerEnvelopesByIconType[normalizedIconType];
            if (envelopes.Length == 0 || layerCount <= 0)
            {
                return new PacketOwnedMesoLayerEnvelope(0f, 0f, 1f, 1f, 0);
            }

            int visibleLayerCount = Math.Min(layerCount, envelopes.Length);
            int clampedLayerIndex = Math.Clamp(layerIndex, 0, visibleLayerCount - 1);
            return envelopes[clampedLayerIndex];
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

            PacketOwnedMesoLayerEnvelope envelope = ResolveLayerEnvelope(iconType, layerIndex, layerCount);
            int phaseOffsetMs = layerIndex * ResolveLayerDelayMs(iconType);
            float elapsedMs = Math.Max(0, tickCount - spawnTime - phaseOffsetMs);
            float fadeIn = MathHelper.Clamp(elapsedMs / (iconType >= 3 ? 240f : 160f), 0f, 1f);
            float phase = 0.5f + (0.5f * MathF.Sin((elapsedMs / ResolveLayerDelayMs(iconType)) + (layerIndex * 0.65f)));

            float alpha = MathHelper.Clamp(
                MathHelper.Lerp(envelope.MinAlpha, envelope.MaxAlpha, phase) * baseAlpha * fadeIn,
                0f,
                1f);
            float scale = MathHelper.Lerp(envelope.MinScale, envelope.MaxScale, phase);
            int yOffset = -(int)MathF.Round(MathHelper.Lerp(0f, envelope.MaxYOffset, phase));

            return new PacketOwnedMesoLayerDrawState(alpha, scale, yOffset);
        }
    }
}
