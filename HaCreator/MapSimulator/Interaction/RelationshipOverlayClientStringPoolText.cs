using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class RelationshipOverlayClientStringPoolText
    {
        internal const int NewYearCardVectorClassStringPoolId = 0x03D2;
        internal const int NewYearCardEffectTemplateStringPoolId = 0x09AB;
        internal const int NewYearCardDefaultItemId = 4300000;
        internal const int NewYearCardMidpointOffsetY = -20;
        internal const int NewYearCardRatioNumerator = 2;
        internal const int NewYearCardRatioDenominator = 2;
        internal const int NewYearCardRatioOriginX = 1;
        internal const int NewYearCardRatioOriginY = 1;
        internal const int NewYearCardLoadLayerAlpha = 255;
        internal const int NewYearCardLoadLayerX = 0;
        internal const int NewYearCardLoadLayerY = 0;
        internal const int NewYearCardLoadLayerZ = 0;
        internal const int NewYearCardAnimateModeRepeat = 2;

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

        public static NewYearCardLoadLayerSpec ResolveNewYearCardLoadLayerSpec(
            int itemId = NewYearCardDefaultItemId)
        {
            int resolvedItemId = itemId > 0 ? itemId : NewYearCardDefaultItemId;
            return new NewYearCardLoadLayerSpec(
                resolvedItemId,
                ResolveNewYearCardVectorClass(),
                ResolveNewYearCardEffectPath(resolvedItemId),
                NewYearCardMidpointOffsetY,
                NewYearCardRatioNumerator,
                NewYearCardRatioDenominator,
                NewYearCardRatioOriginX,
                NewYearCardRatioOriginY,
                NewYearCardLoadLayerAlpha);
        }
    }

    internal readonly record struct NewYearCardLoadLayerSpec(
        int ItemId,
        string VectorClass,
        string EffectPath,
        int MidpointOffsetY,
        int RatioNumerator,
        int RatioDenominator,
        int RatioOriginX,
        int RatioOriginY,
        int Alpha);
}
