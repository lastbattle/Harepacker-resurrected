using MapleLib.WzLib.WzStructure;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldEntryRestrictionEvaluator
    {
        public static string GetRestrictionMessage(MapInfo mapInfo, int playerLevel)
        {
            if (mapInfo == null)
            {
                return null;
            }

            int requiredLevel = mapInfo.lvLimit ?? 0;
            if (requiredLevel > 0 && playerLevel < requiredLevel)
            {
                return $"This map requires level {requiredLevel}.";
            }

            return null;
        }
    }
}
