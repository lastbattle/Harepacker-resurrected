using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Character
{
    public readonly record struct ActiveEffectItemMotionBlurDefinition(
        int ItemId,
        bool Spectrum,
        bool Follow,
        int DelayMs,
        int IntervalMs,
        byte Alpha)
    {
        public bool IsValid => ItemId > 0 && Spectrum && DelayMs > 0 && IntervalMs > 0 && Alpha > 0;
    }

    internal static class ActiveEffectItemMotionBlurResolver
    {
        private const int DefaultDelayMs = 1000;
        private const int DefaultIntervalMs = 100;
        private const int DefaultAlpha = 128;

        public static bool TryResolve(
            int itemId,
            out ActiveEffectItemMotionBlurDefinition definition,
            out string error)
        {
            definition = default;
            error = null;
            if (itemId <= 0)
            {
                error = "Active-effect item id must be positive.";
                return false;
            }

            WzSubProperty effectProperty = LoadEffectProperty(itemId);
            if (effectProperty == null)
            {
                error = $"Active-effect item {itemId} does not publish an effect metadata node.";
                return false;
            }

            definition = CreateDefinition(
                itemId,
                GetIntValue(effectProperty["spectrum"], defaultValue: 0) != 0,
                GetIntValue(effectProperty["follow"], defaultValue: 0) != 0,
                GetIntValue(effectProperty["delay"], DefaultDelayMs),
                GetIntValue(effectProperty["interval"], DefaultIntervalMs),
                GetIntValue(effectProperty["alpha"], DefaultAlpha));
            if (!definition.IsValid)
            {
                error = $"Active-effect item {itemId} does not publish usable spectrum motion-blur metadata.";
                return false;
            }

            return true;
        }

        internal static ActiveEffectItemMotionBlurDefinition CreateDefinition(
            int itemId,
            bool spectrum,
            bool follow,
            int delayMs,
            int intervalMs,
            int alpha)
        {
            return new ActiveEffectItemMotionBlurDefinition(
                itemId,
                spectrum,
                follow,
                Math.Max(1, delayMs),
                Math.Max(1, intervalMs),
                unchecked((byte)alpha));
        }

        internal static bool ShouldRetainSnapshot(
            int snapshotTime,
            int currentTime,
            ActiveEffectItemMotionBlurDefinition definition)
        {
            return definition.IsValid
                   && currentTime >= snapshotTime
                   && currentTime - snapshotTime < definition.DelayMs;
        }

        private static WzSubProperty LoadEffectProperty(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            foreach (string itemNodeName in EnumerateItemNodeNames(category, imagePath, itemId))
            {
                if ((itemImage[itemNodeName] as WzSubProperty)?["effect"] is WzSubProperty effectProperty)
                {
                    return effectProperty;
                }
            }

            return null;
        }

        internal static string[] EnumerateItemNodeNames(string category, string imagePath, int itemId)
        {
            string sevenDigit = itemId.ToString("D7", CultureInfo.InvariantCulture);
            string eightDigit = itemId.ToString("D8", CultureInfo.InvariantCulture);
            bool usesEightDigitNode = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                                      || (imagePath?.StartsWith("Cash/", StringComparison.OrdinalIgnoreCase) == true)
                                      || itemId / 10000 == 429;
            return usesEightDigitNode && !string.Equals(sevenDigit, eightDigit, StringComparison.Ordinal)
                ? new[] { eightDigit, sevenDigit }
                : new[] { sevenDigit };
        }

        private static int GetIntValue(WzImageProperty property, int defaultValue)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue),
                WzStringProperty stringProperty when int.TryParse(
                    stringProperty.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedValue) => parsedValue,
                _ => defaultValue
            };
        }
    }
}
