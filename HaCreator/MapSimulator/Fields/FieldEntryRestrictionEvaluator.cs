using MapleLib.WzLib.WzStructure;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldEntryRestrictionEvaluator
    {
        public static string GetRestrictionMessage(MapInfo mapInfo, FieldEntryRestrictionContext context)
        {
            if (mapInfo == null)
            {
                return null;
            }

            int playerLevel = context.PlayerLevel;
            int requiredLevel = mapInfo.lvLimit ?? 0;
            if (requiredLevel > 0 && playerLevel < requiredLevel)
            {
                return $"This map requires level {requiredLevel}.";
            }

            if (mapInfo.partyOnly == true && !context.HasParty)
            {
                return "This map can only be entered while in a party.";
            }

            if (mapInfo.expeditionOnly == true && !context.HasExpedition)
            {
                return "This map can only be entered while in an expedition.";
            }

            return null;
        }
    }

    public readonly struct FieldEntryRestrictionContext
    {
        public FieldEntryRestrictionContext(int playerLevel, bool hasParty, bool hasExpedition)
        {
            PlayerLevel = playerLevel;
            HasParty = hasParty;
            HasExpedition = hasExpedition;
        }

        public int PlayerLevel { get; }

        public bool HasParty { get; }

        public bool HasExpedition { get; }
    }
}
