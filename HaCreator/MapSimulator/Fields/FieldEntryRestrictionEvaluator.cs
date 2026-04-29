using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;

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
        public static string GetLevelLimitEntryMessage(MapInfo mapInfo)
        {
            int requiredLevel = GetLevelLimit(mapInfo);
            return requiredLevel > 0
                ? $"Field admission requires level {requiredLevel}."
                : null;
        }

        public static string GetPartyOnlyEntryMessage(MapInfo mapInfo)
        {
            return IsPartyOnlyMap(mapInfo)
                ? "Party-only field admission is active in this map."
                : null;
        }

        public static string GetExpeditionOnlyEntryMessage(MapInfo mapInfo)
        {
            return IsExpeditionOnlyMap(mapInfo)
                ? "Expedition-only field admission is active in this map."
                : null;
        }

        public static string GetRestrictionMessage(MapInfo mapInfo, FieldEntryRestrictionContext context)
        {
            FieldEntryRestrictionType restrictionType = GetRestrictionType(mapInfo, context);
            return restrictionType switch
            {
                FieldEntryRestrictionType.LevelLimit => $"This map requires level {GetLevelLimit(mapInfo)}.",
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
            int requiredLevel = GetLevelLimit(mapInfo);
            if (requiredLevel > 0 && playerLevel < requiredLevel)
            {
                return FieldEntryRestrictionType.LevelLimit;
            }

            bool hasPartyAdmission = context.UsesPacketOwnedPartyAdmissionContext
                ? context.HasPacketOwnedPartyAdmission
                : context.HasParty;
            if (IsPartyOnlyMap(mapInfo) && !hasPartyAdmission)
            {
                return FieldEntryRestrictionType.PartyOnly;
            }

            bool hasExpeditionAdmission = context.UsesPacketOwnedExpeditionAdmissionContext
                ? context.HasPacketOwnedExpeditionAdmission
                : context.HasExpedition;
            if (IsExpeditionOnlyMap(mapInfo) && !hasExpeditionAdmission)
            {
                return FieldEntryRestrictionType.ExpeditionOnly;
            }

            return FieldEntryRestrictionType.None;
        }

        public static bool IsPartyOnlyMap(MapInfo mapInfo)
        {
            return mapInfo?.partyOnly == true
                   || IsInfoFlagSet(mapInfo, "PartyOnly");
        }

        public static bool IsExpeditionOnlyMap(MapInfo mapInfo)
        {
            return mapInfo?.expeditionOnly == true
                   || IsInfoFlagSet(mapInfo, "ExpeditionOnly");
        }

        public static int GetLevelLimit(MapInfo mapInfo)
        {
            return Math.Max(0, mapInfo?.lvLimit ?? GetInfoInt(mapInfo, "lvLimit") ?? 0);
        }

        private static bool IsInfoFlagSet(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            WzImageProperty property = FindInfoProperty(mapInfo, propertyName);
            if (property == null)
            {
                return false;
            }

            try
            {
                return property.GetInt() != 0;
            }
            catch
            {
                return property is WzStringProperty stringProperty
                       && int.TryParse(stringProperty.Value, out int value)
                       && value != 0;
            }
        }

        private static WzImageProperty FindInfoProperty(MapInfo mapInfo, string propertyName)
        {
            WzImageProperty property = FindNamedProperty(mapInfo.additionalProps, propertyName)
                ?? FindNamedProperty(mapInfo.unsupportedInfoProperties, propertyName);

            return property ?? mapInfo.Image?["info"]?[propertyName] as WzImageProperty;
        }

        private static int? GetInfoInt(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            WzImageProperty property = FindInfoProperty(mapInfo, propertyName);
            if (property == null)
            {
                return null;
            }

            try
            {
                return property.GetInt();
            }
            catch
            {
                return property is WzStringProperty stringProperty
                       && int.TryParse(stringProperty.Value, out int value)
                    ? value
                    : null;
            }
        }

        private static WzImageProperty FindNamedProperty(IEnumerable<WzImageProperty> properties, string propertyName)
        {
            if (properties == null)
            {
                return null;
            }

            foreach (WzImageProperty property in properties)
            {
                if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
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
