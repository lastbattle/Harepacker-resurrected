using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class RelationshipOverlayClientStringPoolText
    {
        internal const int NewYearCardVectorClassStringPoolId = 0x03D2;
        internal const int NewYearCardEffectTemplateStringPoolId = 0x09AB;
        internal const int NewYearCardDefaultItemId = 4300000;

        private const string NewYearCardVectorClassFallback = "Shape2D#Vector2D";
        private const string NewYearCardEffectTemplateFallback = "Effect/ItemEff.img/%d";

        public static string ResolveNewYearCardVectorClass()
        {
            return MapleStoryStringPool.GetOrFallback(
                NewYearCardVectorClassStringPoolId,
                NewYearCardVectorClassFallback);
        }

        public static string ResolveNewYearCardEffectPath(int itemId = NewYearCardDefaultItemId)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                NewYearCardEffectTemplateStringPoolId,
                NewYearCardEffectTemplateFallback,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, itemId);
        }
    }
}
