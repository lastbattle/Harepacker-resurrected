using MapleLib.WzLib.WzStructure;

namespace HaCreator.MapSimulator.Fields
{
    public enum FieldEntryRestrictionType
    {
        None = 0,
        LevelLimit = 1,
        PartyOnly = 2,
        ExpeditionOnly = 3
    }

    public static class FieldEntryRestrictionEvaluator
    {
        public static string GetRestrictionMessage(MapInfo mapInfo, FieldEntryRestrictionContext context)
        {
            FieldEntryRestrictionType restrictionType = GetRestrictionType(mapInfo, context);
            return restrictionType switch
            {
                FieldEntryRestrictionType.LevelLimit => $"This map requires level {mapInfo?.lvLimit ?? 0}.",
                FieldEntryRestrictionType.PartyOnly => "This map can only be entered while in a party.",
                FieldEntryRestrictionType.ExpeditionOnly => "This map can only be entered while in an expedition.",
                _ => null
            };
        }

        public static FieldEntryRestrictionType GetRestrictionType(MapInfo mapInfo, FieldEntryRestrictionContext context)
        {
            if (mapInfo == null)
            {
                return FieldEntryRestrictionType.None;
            }

            int playerLevel = context.PlayerLevel;
            int requiredLevel = mapInfo.lvLimit ?? 0;
            if (requiredLevel > 0 && playerLevel < requiredLevel)
            {
                return FieldEntryRestrictionType.LevelLimit;
            }

            bool hasPartyAdmission = context.UsesPacketOwnedPartyAdmissionContext
                ? context.HasPacketOwnedPartyAdmission
                : context.HasParty;
            if (mapInfo.partyOnly == true && !hasPartyAdmission)
            {
                return FieldEntryRestrictionType.PartyOnly;
            }

            bool hasExpeditionAdmission = context.UsesPacketOwnedExpeditionAdmissionContext
                ? context.HasPacketOwnedExpeditionAdmission
                : context.HasExpedition;
            if (mapInfo.expeditionOnly == true && !hasExpeditionAdmission)
            {
                return FieldEntryRestrictionType.ExpeditionOnly;
            }

            return FieldEntryRestrictionType.None;
        }
    }

    public readonly struct FieldEntryRestrictionContext
    {
        public FieldEntryRestrictionContext(
            int playerLevel,
            bool hasParty,
            bool hasExpedition,
            bool usesPacketOwnedPartyAdmissionContext = false,
            bool hasPacketOwnedPartyAdmission = false,
            bool usesPacketOwnedExpeditionAdmissionContext = false,
            bool hasPacketOwnedExpeditionAdmission = false)
        {
            PlayerLevel = playerLevel;
            HasParty = hasParty;
            HasExpedition = hasExpedition;
            UsesPacketOwnedPartyAdmissionContext = usesPacketOwnedPartyAdmissionContext;
            HasPacketOwnedPartyAdmission = hasPacketOwnedPartyAdmission;
            UsesPacketOwnedExpeditionAdmissionContext = usesPacketOwnedExpeditionAdmissionContext;
            HasPacketOwnedExpeditionAdmission = hasPacketOwnedExpeditionAdmission;
        }

        public int PlayerLevel { get; }

        public bool HasParty { get; }

        public bool HasExpedition { get; }

        public bool UsesPacketOwnedPartyAdmissionContext { get; }

        public bool HasPacketOwnedPartyAdmission { get; }

        public bool UsesPacketOwnedExpeditionAdmissionContext { get; }

        public bool HasPacketOwnedExpeditionAdmission { get; }
    }
}
