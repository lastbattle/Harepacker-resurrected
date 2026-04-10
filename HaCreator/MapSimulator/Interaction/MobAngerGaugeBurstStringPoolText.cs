using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class MobAngerGaugeBurstStringPoolText
    {
        internal const int MobTemplatePathStringPoolId = 0x03CE;
        internal const int AngerGaugeEffectNameStringPoolId = 0x0C2F;

        private const string MobTemplatePathFallback = "Mob/%07d.img";
        private const string AngerGaugeEffectNameFallback = "AngerGaugeEffect";

        public static string ResolvePath(string mobTemplateId)
        {
            if (!int.TryParse(mobTemplateId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTemplateId))
            {
                return null;
            }

            return ResolvePath(parsedTemplateId);
        }

        public static string ResolvePath(int mobTemplateId)
        {
            string templatePath = MapleStoryStringPool.GetCompositeFormatOrFallback(
                MobTemplatePathStringPoolId,
                MobTemplatePathFallback,
                maxPlaceholderCount: 1,
                out _);
            string effectName = MapleStoryStringPool.GetOrFallback(
                AngerGaugeEffectNameStringPoolId,
                AngerGaugeEffectNameFallback,
                appendFallbackSuffix: false);

            if (string.IsNullOrWhiteSpace(templatePath) || string.IsNullOrWhiteSpace(effectName))
            {
                return null;
            }

            return string.Format(CultureInfo.InvariantCulture, templatePath, mobTemplateId)
                + "/"
                + effectName.Trim().Trim('/');
        }
    }
}
