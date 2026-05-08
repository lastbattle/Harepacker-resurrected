using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldEnvironmentEffectEvaluator
    {
        public static WeatherType ResolveAmbientWeather(MapInfo mapInfo)
        {
            if (mapInfo?.snow == true || IsInfoFlagSet(mapInfo, "snow"))
            {
                return WeatherType.Snow;
            }

            if (mapInfo?.rain == true || IsInfoFlagSet(mapInfo, "rain"))
            {
                return WeatherType.Rain;
            }

            return WeatherType.None;
        }

        public static string GetAmbientWeatherEntryMessage(MapInfo mapInfo)
        {
            return ResolveAmbientWeather(mapInfo) switch
            {
                WeatherType.Rain => "Ambient field weather: rain.",
                WeatherType.Snow => "Ambient field weather: snow.",
                _ => null
            };
        }

        private static bool IsInfoFlagSet(MapInfo mapInfo, string propertyName)
        {
            foreach (WzImageProperty property in EnumerateInfoProperties(mapInfo, propertyName))
            {
                if (TryReadInfoFlag(property, out bool enabled))
                {
                    return enabled;
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

            if (mapInfo.Image?["info"]?[propertyName] is WzImageProperty imageProperty)
            {
                yield return imageProperty;
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

        private static bool TryReadInfoFlag(WzImageProperty property, out bool enabled)
        {
            enabled = false;
            if (property == null)
            {
                return false;
            }

            try
            {
                enabled = property.GetInt() != 0;
                return true;
            }
            catch
            {
                if (property is WzStringProperty stringProperty
                    && int.TryParse(stringProperty.Value, out int value))
                {
                    enabled = value != 0;
                    return true;
                }

                return false;
            }
        }
    }
}
