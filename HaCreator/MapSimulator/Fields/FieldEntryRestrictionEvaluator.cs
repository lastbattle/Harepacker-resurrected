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
        ExpeditionOnly = 3,
        LevelForceMove = 4
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

        public static string GetLevelForceMoveEntryMessage(MapInfo mapInfo)
        {
            int maximumLevel = GetLevelForceMove(mapInfo);
            return maximumLevel > 0
                ? $"Field admission forces characters at or above level {maximumLevel} out of this map."
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
                FieldEntryRestrictionType.LevelForceMove => $"This map only allows characters below level {GetLevelForceMove(mapInfo)}.",
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

            int forceMoveLevel = GetLevelForceMove(mapInfo);
            if (forceMoveLevel > 0 && playerLevel >= forceMoveLevel)
            {
                return FieldEntryRestrictionType.LevelForceMove;
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

        public static int GetLevelForceMove(MapInfo mapInfo)
        {
            return Math.Max(0, mapInfo?.lvForceMove ?? GetInfoInt(mapInfo, "lvForceMove") ?? 0);
        }

        private static bool IsInfoFlagSet(MapInfo mapInfo, string propertyName)
        {
            foreach (WzImageProperty property in EnumerateInfoProperties(mapInfo, propertyName))
            {
                if (TryReadInfoInt(property, out int value))
                {
                    return value != 0;
                }
            }

            return false;
        }

        private static IEnumerable<WzImageProperty> EnumerateInfoProperties(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                yield break;
            }

            foreach (WzImageProperty property in EnumerateNamedProperties(mapInfo.additionalProps, propertyName))
            {
                yield return property;
            }

            foreach (WzImageProperty property in EnumerateNamedProperties(mapInfo.unsupportedInfoProperties, propertyName))
            {
                yield return property;
            }

            if (FindImageInfoProperty(mapInfo, propertyName) is WzImageProperty imageProperty)
            {
                yield return imageProperty;
            }
        }

        private static int? GetInfoInt(MapInfo mapInfo, string propertyName)
        {
            foreach (WzImageProperty property in EnumerateInfoProperties(mapInfo, propertyName))
            {
                if (TryReadInfoInt(property, out int value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool TryReadInfoInt(WzImageProperty property, out int value)
        {
            value = 0;
            if (property == null)
            {
                return false;
            }

            try
            {
                value = property.GetInt();
                return true;
            }
            catch
            {
                if (property is WzStringProperty stringProperty
                    && int.TryParse(stringProperty.Value, out value))
                {
                    return true;
                }

                value = 0;
                return false;
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateNamedProperties(IEnumerable<WzImageProperty> properties, string propertyName)
        {
            if (properties == null)
            {
                yield break;
            }

            foreach (WzImageProperty property in properties)
            {
                if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return property;
                }
            }
        }

        private static WzImageProperty FindImageInfoProperty(MapInfo mapInfo, string propertyName)
        {
            WzImageProperty info = mapInfo?.Image?["info"];
            if (info == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            WzImageProperty exactProperty = info[propertyName] as WzImageProperty;
            if (exactProperty != null)
            {
                return exactProperty;
            }

            if (info.WzProperties == null)
            {
                return null;
            }

            foreach (WzImageProperty property in info.WzProperties)
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
