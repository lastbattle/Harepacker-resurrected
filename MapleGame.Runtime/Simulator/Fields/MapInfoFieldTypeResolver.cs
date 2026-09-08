using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    internal static class MapInfoFieldTypeResolver
    {
        public static FieldType? Resolve(MapInfo mapInfo)
        {
            if (mapInfo?.fieldType.HasValue == true)
            {
                return mapInfo.fieldType.Value;
            }

            foreach (WzImageProperty property in EnumerateInfoProperties(mapInfo, "fieldType"))
            {
                if (TryReadInt(property, out int value))
                {
                    return (FieldType)value;
                }
            }

            return null;
        }

        private static IEnumerable<WzImageProperty> EnumerateInfoProperties(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                yield break;
            }

            foreach (WzImageProperty property in EnumerateMatchingProperties(mapInfo.additionalProps, propertyName))
            {
                yield return property;
            }

            foreach (WzImageProperty property in EnumerateMatchingProperties(mapInfo.unsupportedInfoProperties, propertyName))
            {
                yield return property;
            }

            if (FindImageInfoProperty(mapInfo, propertyName) is WzImageProperty imageProperty)
            {
                yield return imageProperty;
            }
        }

        private static IEnumerable<WzImageProperty> EnumerateMatchingProperties(
            IEnumerable<WzImageProperty> properties,
            string propertyName)
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

        private static bool TryReadInt(WzImageProperty property, out int value)
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

                return false;
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
}
