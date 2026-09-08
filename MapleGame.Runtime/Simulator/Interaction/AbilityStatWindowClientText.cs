using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class AbilityStatWindowClientText
    {
        internal const int PrimaryStatDeltaFormatStringPoolId = 0x07BB;
        private const string PrimaryStatDeltaFallbackFormat = "{0} ({1}{2:+#;-#;0})";

        public static string FormatPrimaryStatValue(int totalValue, int baseValue)
        {
            if (totalValue == baseValue)
            {
                return totalValue.ToString(CultureInfo.InvariantCulture);
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                PrimaryStatDeltaFormatStringPoolId,
                PrimaryStatDeltaFallbackFormat,
                maxPlaceholderCount: 3,
                out _);

            return string.Format(
                CultureInfo.InvariantCulture,
                format,
                totalValue,
                baseValue,
                totalValue - baseValue);
        }
    }
}
